using System.IO.Compression;
using System.Text;
using AwesomeAssertions;
using HotaTwitch.Application.Abstractions;
using HotaTwitch.Application.Broadcasting;
using HotaTwitch.Application.Tests.Doubles;
using HotaTwitch.Domain.Broadcasting;
using HotaTwitch.Domain.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace HotaTwitch.Application.Tests.Broadcasting;

public sealed class BroadcastCoalescerTests
{
    private static readonly ChannelId Channel = new("141981764");
    private static readonly ChannelId OtherChannel = new("22222");

    private readonly IPubSubPublisher publisher = Substitute.For<IPubSubPublisher>();
    private readonly MutableClock clock = new(new DateTimeOffset(2026, 9, 9, 22, 0, 0, TimeSpan.Zero));
    private readonly BroadcastCoalescer coalescer;

    public BroadcastCoalescerTests() =>
        coalescer = new BroadcastCoalescer(publisher, clock, NullLogger<BroadcastCoalescer>.Instance);

    [Fact]
    public async Task PublishDueAsync_NothingSubmitted_PublishesNothing()
    {
        (await coalescer.PublishDueAsync(TestContext.Current.CancellationToken)).Should().Be(0);

        await publisher.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<ChannelId>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishDueAsync_ThreeSubmitsInsideOneSecond_PublishesOnlyTheLastOne()
    {
        Submit("first");
        clock.Advance(TimeSpan.FromMilliseconds(300));
        Submit("second");
        clock.Advance(TimeSpan.FromMilliseconds(300));
        Submit("third");

        var published = await coalescer.PublishDueAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(300));
        published += await coalescer.PublishDueAsync(TestContext.Current.CancellationToken);

        published.Should().Be(1);
        CapturedDocuments().Should().Equal("third");
    }

    [Fact]
    public async Task PublishDueAsync_SubmitAfterTheInterval_PublishesTheSecondMessage()
    {
        Submit("first");
        await coalescer.PublishDueAsync(TestContext.Current.CancellationToken);

        clock.Advance(BroadcastPolicy.MinimumBroadcastInterval);
        Submit("second");
        await coalescer.PublishDueAsync(TestContext.Current.CancellationToken);

        CapturedDocuments().Should().Equal("first", "second");
    }

    [Fact]
    public async Task PublishDueAsync_ChannelsAreIndependent_BothArePublishedInTheSameTick()
    {
        coalescer.Submit(Channel, "gz:one");
        coalescer.Submit(OtherChannel, "gz:two");

        (await coalescer.PublishDueAsync(TestContext.Current.CancellationToken)).Should().Be(2);
    }

    [Fact]
    public async Task PublishDueAsync_PublisherThrows_KeepsServingTheOtherChannels()
    {
        publisher.PublishAsync(Channel, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Twitch is down"));
        coalescer.Submit(Channel, "gz:one");
        coalescer.Submit(OtherChannel, "gz:two");

        (await coalescer.PublishDueAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task PublishDueAsync_PublisherThrows_DoesNotRetryBeforeTheNextInterval()
    {
        publisher.PublishAsync(Channel, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Twitch is down"));
        coalescer.Submit(Channel, "gz:one");

        await coalescer.PublishDueAsync(TestContext.Current.CancellationToken);
        await coalescer.PublishDueAsync(TestContext.Current.CancellationToken);

        await publisher.Received(1).PublishAsync(Channel, "gz:one", Arg.Any<CancellationToken>());
    }

    private void Submit(string document)
    {
        BroadcastMessage.TryEncode(Encoding.UTF8.GetBytes(document), out var message).Should().BeTrue();
        coalescer.Submit(Channel, message!);
    }

    private List<string> CapturedDocuments() =>
        [.. publisher.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IPubSubPublisher.PublishAsync))
            .Select(call => Gunzip((string)call.GetArguments()[1]!))];

    private static string Gunzip(string message)
    {
        using var source = new MemoryStream(Convert.FromBase64String(message[BroadcastMessage.Prefix.Length..]));
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
