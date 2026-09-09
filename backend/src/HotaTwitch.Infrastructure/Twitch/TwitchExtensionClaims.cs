namespace HotaTwitch.Infrastructure.Twitch;

/// <summary>The claims the backend reads out of a JWT issued by the Twitch extension helper.</summary>
public sealed record TwitchExtensionClaims(string Role, string? ChannelId, string? UserId)
{
    public const string BroadcasterRole = "broadcaster";

    public bool IsBroadcaster => string.Equals(Role, BroadcasterRole, StringComparison.Ordinal);
}
