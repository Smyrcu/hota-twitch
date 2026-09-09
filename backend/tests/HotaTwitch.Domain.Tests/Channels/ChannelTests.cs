using AwesomeAssertions;
using HotaTwitch.Domain.Channels;
using Xunit;

namespace HotaTwitch.Domain.Tests.Channels;

public sealed class ChannelTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_NewChannel_StoresHashAndHintButNotTheToken()
    {
        var token = StreamerToken.Generate();

        var channel = Channel.Create(new ChannelId("1"), token, Now);

        channel.Id.Value.Should().Be("1");
        channel.TokenHash.Should().Be(token.Hash());
        channel.TokenHint.Should().Be(token.Hint());
        channel.CreatedAt.Should().Be(Now);
        channel.LastStateAt.Should().BeNull();
    }

    [Fact]
    public void RotateToken_ExistingChannel_ReplacesHashAndHintAndKeepsCreatedAt()
    {
        var channel = Channel.Create(new ChannelId("1"), StreamerToken.Generate(), Now);
        var rotated = StreamerToken.Generate();

        channel.RotateToken(rotated);

        channel.TokenHash.Should().Be(rotated.Hash());
        channel.TokenHint.Should().Be(rotated.Hint());
        channel.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void MarkStateReceived_Always_MovesLastStateAtForward()
    {
        var channel = Channel.Create(new ChannelId("1"), StreamerToken.Generate(), Now);

        channel.MarkStateReceived(Now.AddSeconds(5));

        channel.LastStateAt.Should().Be(Now.AddSeconds(5));
    }
}
