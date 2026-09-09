using System.Text;
using AwesomeAssertions;
using HotaTwitch.Application.Abstractions;
using HotaTwitch.Application.Broadcasting;
using HotaTwitch.Application.Ingest;
using HotaTwitch.Application.Tests.Doubles;
using HotaTwitch.Domain.Broadcasting;
using HotaTwitch.Domain.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace HotaTwitch.Application.Tests.Ingest;

public sealed class IngestStateHandlerTests
{
    private static readonly ChannelId ChannelIdentifier = new("141981764");
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 22, 0, 0, TimeSpan.Zero);

    private readonly IChannelRepository channels = Substitute.For<IChannelRepository>();
    private readonly IPubSubPublisher publisher = Substitute.For<IPubSubPublisher>();
    private readonly IIngestRateLimiter rateLimiter = Substitute.For<IIngestRateLimiter>();
    private readonly MutableClock clock = new(Now);
    private readonly StreamerToken token = StreamerToken.Generate();
    private readonly Channel channel;
    private readonly BroadcastCoalescer coalescer;
    private readonly IngestStateHandler handler;

    public IngestStateHandlerTests()
    {
        channel = Channel.Create(ChannelIdentifier, token, Now);
        coalescer = new BroadcastCoalescer(publisher, clock, NullLogger<BroadcastCoalescer>.Instance);
        handler = new IngestStateHandler(channels, rateLimiter, coalescer, clock, NullLogger<IngestStateHandler>.Instance);

        rateLimiter.TryAcquire(Arg.Any<TokenHash>()).Returns(true);
        channels.FindByTokenHashAsync(token.Hash(), Arg.Any<CancellationToken>()).Returns(channel);
    }

    [Fact]
    public async Task HandleAsync_ValidDocument_IsAccepted()
    {
        var outcome = await HandleAsync(token.Value, StateDocuments.Valid());

        outcome.Should().Be(IngestOutcome.Accepted);
    }

    [Fact]
    public async Task HandleAsync_ValidDocument_MarksTheChannelAsSeenAndSavesIt()
    {
        clock.Advance(TimeSpan.FromMinutes(3));

        await HandleAsync(token.Value, StateDocuments.Valid());

        channel.LastStateAt.Should().Be(Now.AddMinutes(3));
        await channels.Received(1).SaveAsync(channel, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ValidDocument_QueuesTheDocumentForBroadcast()
    {
        await HandleAsync(token.Value, StateDocuments.Valid());
        await coalescer.PublishDueAsync(TestContext.Current.CancellationToken);

        await publisher.Received(1).PublishAsync(ChannelIdentifier, Arg.Is<string>(m => m.StartsWith(BroadcastMessage.Prefix, StringComparison.Ordinal)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ThreeDocumentsInsideOneSecond_BroadcastsOnlyTheLastOne()
    {
        await HandleAsync(token.Value, StateDocuments.Valid(1));
        await HandleAsync(token.Value, StateDocuments.Valid(2));
        await HandleAsync(token.Value, StateDocuments.Valid(3));

        var published = await coalescer.PublishDueAsync(TestContext.Current.CancellationToken);

        published.Should().Be(1);
        await publisher.Received(1).PublishAsync(
            ChannelIdentifier,
            Arg.Is<string>(message => Decoded(message) == StateDocuments.Text(3)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task HandleAsync_MalformedToken_IsUnknownToken(string? bearer)
    {
        var outcome = await HandleAsync(bearer, StateDocuments.Valid());

        outcome.Should().Be(IngestOutcome.UnknownToken);
    }

    [Fact]
    public async Task HandleAsync_TokenThatNoChannelHas_IsUnknownToken()
    {
        var outcome = await HandleAsync(StreamerToken.Generate().Value, StateDocuments.Valid());

        outcome.Should().Be(IngestOutcome.UnknownToken);
    }

    [Fact]
    public async Task HandleAsync_DocumentOverTheSizeLimit_IsPayloadTooLarge()
    {
        var oversized = new byte[BroadcastPolicy.MaxStateDocumentBytes + 1];

        var outcome = await HandleAsync(token.Value, oversized);

        outcome.Should().Be(IngestOutcome.PayloadTooLarge);
    }

    [Fact]
    public async Task HandleAsync_TokenOverItsRate_IsRateLimited()
    {
        rateLimiter.TryAcquire(token.Hash()).Returns(false);

        var outcome = await HandleAsync(token.Value, StateDocuments.Valid());

        outcome.Should().Be(IngestOutcome.RateLimited);
    }

    [Fact]
    public async Task HandleAsync_BodyThatIsNotAStateDocument_IsInvalidDocument()
    {
        var outcome = await HandleAsync(token.Value, Encoding.UTF8.GetBytes("""{"hello":"world"}"""));

        outcome.Should().Be(IngestOutcome.InvalidDocument);
    }

    [Fact]
    public async Task HandleAsync_DocumentThatDoesNotFitAPubSubMessage_IsAcceptedButNotBroadcast()
    {
        var noise = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16 * 1024));
        var document = Encoding.UTF8.GetBytes(StateDocuments.Text().Replace("\"heroes\":[]", $"\"heroes\":[\"{noise}\"]", StringComparison.Ordinal));

        var outcome = await HandleAsync(token.Value, document);
        await coalescer.PublishDueAsync(TestContext.Current.CancellationToken);

        outcome.Should().Be(IngestOutcome.Accepted);
        await publisher.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<ChannelId>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private Task<IngestOutcome> HandleAsync(string? bearer, ReadOnlyMemory<byte> document) =>
        handler.HandleAsync(new IngestStateCommand(bearer, document), TestContext.Current.CancellationToken);

    private static string Decoded(string message)
    {
        using var source = new MemoryStream(Convert.FromBase64String(message[BroadcastMessage.Prefix.Length..]));
        using var gzip = new System.IO.Compression.GZipStream(source, System.IO.Compression.CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
