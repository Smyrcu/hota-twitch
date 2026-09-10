using AwesomeAssertions;
using HotaTwitch.Domain.Channels;
using Xunit;

namespace HotaTwitch.Domain.Tests.Channels;

public sealed class ChannelTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_NewChannel_HasNoTokenAndTheDefaultSettings()
    {
        var channel = Channel.Create(new ChannelId("1"), Now);

        channel.Id.Value.Should().Be("1");
        channel.HasToken.Should().BeFalse();
        channel.TokenHash.Should().BeNull();
        channel.TokenHint.Should().BeNull();
        channel.CreatedAt.Should().Be(Now);
        channel.LastStateAt.Should().BeNull();
        channel.Settings.Should().Be(ChannelSettings.Default);
    }

    [Fact]
    public void RotateToken_ChannelWithoutAToken_StoresHashAndHintButNotTheToken()
    {
        var channel = Channel.Create(new ChannelId("1"), Now);
        var token = StreamerToken.Generate();

        channel.RotateToken(token);

        channel.HasToken.Should().BeTrue();
        channel.TokenHash.Should().Be(token.Hash());
        channel.TokenHint.Should().Be(token.Hint());
    }

    [Fact]
    public void RotateToken_ChannelThatAlreadyHasOne_ReplacesHashAndHintAndKeepsCreatedAt()
    {
        var channel = WithToken(StreamerToken.Generate());
        var rotated = StreamerToken.Generate();

        channel.RotateToken(rotated);

        channel.TokenHash.Should().Be(rotated.Hash());
        channel.TokenHint.Should().Be(rotated.Hint());
        channel.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void ClearToken_ChannelWithAToken_UnbindsItAndForgetsTheTrafficItCarried()
    {
        var channel = WithToken(StreamerToken.Generate());
        channel.MarkStateReceived(Now.AddSeconds(5));

        channel.ClearToken();

        channel.HasToken.Should().BeFalse();
        channel.TokenHash.Should().BeNull();
        channel.TokenHint.Should().BeNull();
        channel.LastStateAt.Should().BeNull();
    }

    [Fact]
    public void ClearToken_ChannelWithSettings_KeepsThem()
    {
        var channel = WithToken(StreamerToken.Generate());
        ChannelSettings.TryCreate(2.5m, out var settings).Should().BeTrue();
        channel.UpdateSettings(settings!);

        channel.ClearToken();

        channel.Settings.UiScale.Should().Be(2.5m);
    }

    [Fact]
    public void UpdateSettings_Always_ReplacesThemAndLeavesTheTokenAlone()
    {
        var token = StreamerToken.Generate();
        var channel = WithToken(token);
        ChannelSettings.TryCreate(1.5m, out var settings).Should().BeTrue();

        channel.UpdateSettings(settings!);

        channel.Settings.UiScale.Should().Be(1.5m);
        channel.TokenHash.Should().Be(token.Hash());
    }

    [Fact]
    public void MarkStateReceived_Always_MovesLastStateAtForward()
    {
        var channel = WithToken(StreamerToken.Generate());

        channel.MarkStateReceived(Now.AddSeconds(5));

        channel.LastStateAt.Should().Be(Now.AddSeconds(5));
    }

    [Fact]
    public void Restore_ChannelWithoutAToken_ComesBackWithoutOne()
    {
        var channel = Channel.Restore(
            new ChannelId("1"),
            tokenHash: null,
            tokenHint: null,
            Now,
            lastStateAt: null,
            ChannelSettings.Restore(3m));

        channel.HasToken.Should().BeFalse();
        channel.Settings.UiScale.Should().Be(3m);
    }

    private static Channel WithToken(StreamerToken token)
    {
        var channel = Channel.Create(new ChannelId("1"), Now);
        channel.RotateToken(token);
        return channel;
    }
}
