using AwesomeAssertions;
using HotaTwitch.Application.Abstractions;
using HotaTwitch.Application.Channels;
using HotaTwitch.Application.Tests.Doubles;
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
    private readonly MutableClock clock = new(Now);

    [Fact]
    public async Task IssueToken_ChannelWithoutAToken_CreatesTheChannelAndReturnsThePlainToken()
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
        var existing = Channel.Create(ChannelIdentifier, StreamerToken.Generate(), Now);
        channels.FindByIdAsync(ChannelIdentifier, Arg.Any<CancellationToken>()).Returns(existing);
        var handler = new IssueTokenHandler(channels, clock, NullLogger<IssueTokenHandler>.Instance);

        var token = await handler.HandleAsync(ChannelIdentifier, TestContext.Current.CancellationToken);

        existing.TokenHash.Should().Be(StreamerToken.HashOf(token));
        existing.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task RevokeToken_Always_RemovesTheChannel()
    {
        var handler = new RevokeTokenHandler(channels, NullLogger<RevokeTokenHandler>.Instance);

        await handler.HandleAsync(ChannelIdentifier, TestContext.Current.CancellationToken);

        await channels.Received(1).RemoveAsync(ChannelIdentifier, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetChannelStatus_UnknownChannel_ReportsNoToken()
    {
        var handler = new GetChannelStatusHandler(channels);

        var status = await handler.HandleAsync(ChannelIdentifier, TestContext.Current.CancellationToken);

        status.Should().Be(ChannelStatus.Unconfigured);
    }

    [Fact]
    public async Task GetChannelStatus_ConfiguredChannel_ReportsHintAndLastState()
    {
        var token = StreamerToken.Generate();
        var channel = Channel.Create(ChannelIdentifier, token, Now);
        channel.MarkStateReceived(Now.AddMinutes(1));
        channels.FindByIdAsync(ChannelIdentifier, Arg.Any<CancellationToken>()).Returns(channel);
        var handler = new GetChannelStatusHandler(channels);

        var status = await handler.HandleAsync(ChannelIdentifier, TestContext.Current.CancellationToken);

        status.Should().Be(new ChannelStatus(HasToken: true, token.Hint(), Now.AddMinutes(1)));
    }
}
