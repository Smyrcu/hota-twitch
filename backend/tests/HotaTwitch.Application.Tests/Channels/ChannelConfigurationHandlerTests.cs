using AwesomeAssertions;
using HotaTwitch.Application.Abstractions;
using HotaTwitch.Application.Broadcasting;
using HotaTwitch.Application.Channels;
using HotaTwitch.Application.Tests.Doubles;
using HotaTwitch.Domain.Broadcasting;
using HotaTwitch.Domain.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace HotaTwitch.Application.Tests.Channels;

public sealed class ChannelConfigurationHandlerTests
{
    private static readonly ChannelId ChannelIdentifier = new("141981764");
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 22, 0, 0, TimeSpan.Zero);

    private readonly IChannelRepository channels = Substitute.For<IChannelRepository>();
    private readonly IPubSubPublisher publisher = Substitute.For<IPubSubPublisher>();
    private readonly MutableClock clock = new(Now);

    private BroadcastCoalescer NewCoalescer() =>
        new(publisher, clock, NullLogger<BroadcastCoalescer>.Instance);

    [Fact]
    public async Task IssueToken_ChannelWithoutARow_CreatesTheChannelAndReturnsThePlainToken()
    {
        var handler = new IssueTokenHandler(channels, clock, NullLogger<IssueTokenHandler>.Instance);

        var token = await handler.HandleAsync(ChannelIdentifier, TestContext.Current.CancellationToken);

        token.Should().StartWith(StreamerToken.Prefix);
        await channels.Received(1).SaveAsync(
            Arg.Is<Channel>(saved => saved.Id == ChannelIdentifier && saved.TokenHash == StreamerToken.HashOf(token)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueToken_ChannelThatAlreadyHasAToken_RotatesItAndKeepsCreatedAt()
    {
        var existing = WithToken(StreamerToken.Generate());
        channels.FindByIdAsync(ChannelIdentifier, Arg.Any<CancellationToken>()).Returns(existing);
        var handler = new IssueTokenHandler(channels, clock, NullLogger<IssueTokenHandler>.Instance);

        var token = await handler.HandleAsync(ChannelIdentifier, TestContext.Current.CancellationToken);

        existing.TokenHash.Should().Be(StreamerToken.HashOf(token));
        existing.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task IssueToken_ChannelThatOnlyHadSettings_KeepsThem()
    {
        var existing = Channel.Create(ChannelIdentifier, Now);
        existing.UpdateSettings(Scale(2.5m));
        channels.FindByIdAsync(ChannelIdentifier, Arg.Any<CancellationToken>()).Returns(existing);
        var handler = new IssueTokenHandler(channels, clock, NullLogger<IssueTokenHandler>.Instance);

        await handler.HandleAsync(ChannelIdentifier, TestContext.Current.CancellationToken);

        existing.Settings.UiScale.Should().Be(2.5m);
    }

    [Fact]
    public async Task RevokeToken_ChannelWithAToken_ClearsTheTokenAndKeepsTheChannel()
    {
        var existing = WithToken(StreamerToken.Generate());
        existing.UpdateSettings(Scale(1.5m));
        channels.FindByIdAsync(ChannelIdentifier, Arg.Any<CancellationToken>()).Returns(existing);
        var handler = new RevokeTokenHandler(channels, NewCoalescer(), NullLogger<RevokeTokenHandler>.Instance);

        await handler.HandleAsync(ChannelIdentifier, TestContext.Current.CancellationToken);

        existing.HasToken.Should().BeFalse();
        existing.Settings.UiScale.Should().Be(1.5m);
        await channels.Received(1).SaveAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeToken_ChannelThatHasNoRow_WritesNothing()
    {
        var handler = new RevokeTokenHandler(channels, NewCoalescer(), NullLogger<RevokeTokenHandler>.Instance);

        await handler.HandleAsync(ChannelIdentifier, TestContext.Current.CancellationToken);

        await channels.DidNotReceiveWithAnyArgs().SaveAsync(Arg.Any<Channel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeToken_ChannelWithAQueuedBroadcast_DropsItSoItNeverReachesViewers()
    {
        var coalescer = NewCoalescer();
        BroadcastMessage.TryEncode("state"u8, out var message).Should().BeTrue();
        coalescer.Submit(ChannelIdentifier, message!);
        var handler = new RevokeTokenHandler(channels, coalescer, NullLogger<RevokeTokenHandler>.Instance);

        await handler.HandleAsync(ChannelIdentifier, TestContext.Current.CancellationToken);
        await coalescer.PublishDueAsync(TestContext.Current.CancellationToken);

        await publisher.DidNotReceiveWithAnyArgs()
            .PublishAsync(Arg.Any<ChannelId>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetChannelStatus_UnknownChannel_ReportsNoTokenAndTheDefaultScale()
    {
        var handler = new GetChannelStatusHandler(channels);

        var status = await handler.HandleAsync(ChannelIdentifier, TestContext.Current.CancellationToken);

        status.Should().Be(ChannelStatus.Unconfigured);
        status.Settings.Should().Be(ChannelSettings.Default);
    }

    [Fact]
    public async Task GetChannelStatus_ChannelWithAToken_ReportsHintLastStateAndSettings()
    {
        var token = StreamerToken.Generate();
        var channel = WithToken(token);
        channel.UpdateSettings(Scale(2m));
        channel.MarkStateReceived(Now.AddMinutes(1));
        channels.FindByIdAsync(ChannelIdentifier, Arg.Any<CancellationToken>()).Returns(channel);
        var handler = new GetChannelStatusHandler(channels);

        var status = await handler.HandleAsync(ChannelIdentifier, TestContext.Current.CancellationToken);

        status.Should().Be(new ChannelStatus(HasToken: true, token.Hint(), Now.AddMinutes(1), Scale(2m)));
    }

    [Fact]
    public async Task GetChannelStatus_ChannelWithSettingsButNoToken_StillReportsTheSettings()
    {
        var channel = Channel.Create(ChannelIdentifier, Now);
        channel.UpdateSettings(Scale(3m));
        channels.FindByIdAsync(ChannelIdentifier, Arg.Any<CancellationToken>()).Returns(channel);
        var handler = new GetChannelStatusHandler(channels);

        var status = await handler.HandleAsync(ChannelIdentifier, TestContext.Current.CancellationToken);

        status.HasToken.Should().BeFalse();
        status.TokenHint.Should().BeNull();
        status.Settings.UiScale.Should().Be(3m);
    }

    [Fact]
    public async Task UpdateSettings_ChannelThatHasNoRow_CreatesItCarryingTheScale()
    {
        var handler = NewSettingsHandler();

        await handler.HandleAsync(ChannelIdentifier, Scale(1.5m), TestContext.Current.CancellationToken);

        await channels.Received(1).SaveSettingsAsync(
            Arg.Is<Channel>(saved =>
                saved.Id == ChannelIdentifier && !saved.HasToken && saved.Settings.UiScale == 1.5m &&
                saved.CreatedAt == Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSettings_ChannelWithAToken_KeepsTheToken()
    {
        var token = StreamerToken.Generate();
        var existing = WithToken(token);
        channels.FindByIdAsync(ChannelIdentifier, Arg.Any<CancellationToken>()).Returns(existing);
        var handler = NewSettingsHandler();

        await handler.HandleAsync(ChannelIdentifier, Scale(4m), TestContext.Current.CancellationToken);

        existing.Settings.UiScale.Should().Be(4m);
        existing.TokenHash.Should().Be(token.Hash());
        await channels.Received(1).SaveSettingsAsync(existing, Arg.Any<CancellationToken>());
    }

    private UpdateChannelSettingsHandler NewSettingsHandler() =>
        new(channels, clock, NullLogger<UpdateChannelSettingsHandler>.Instance);

    private static Channel WithToken(StreamerToken token)
    {
        var channel = Channel.Create(ChannelIdentifier, Now);
        channel.RotateToken(token);
        return channel;
    }

    private static ChannelSettings Scale(decimal uiScale)
    {
        ChannelSettings.TryCreate(uiScale, out var settings).Should().BeTrue();
        return settings!;
    }
}
