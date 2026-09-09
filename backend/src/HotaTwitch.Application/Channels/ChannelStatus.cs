namespace HotaTwitch.Application.Channels;

/// <summary>What the configuration page shows about a channel's connection.</summary>
public sealed record ChannelStatus(bool HasToken, string? TokenHint, DateTimeOffset? LastStateAt)
{
    public static ChannelStatus Unconfigured { get; } = new(HasToken: false, TokenHint: null, LastStateAt: null);
}
