namespace HotaTwitch.Domain.Channels;

/// <summary>
/// A Twitch channel the extension knows about. The streamer token is optional: a channel exists
/// as soon as the streamer configures anything, keeps its settings when the token is cleared, and
/// only relays state while a token is bound to it.
/// </summary>
public sealed class Channel
{
    private Channel(
        ChannelId id,
        TokenHash? tokenHash,
        string? tokenHint,
        DateTimeOffset createdAt,
        DateTimeOffset? lastStateAt,
        ChannelSettings settings)
    {
        Id = id;
        TokenHash = tokenHash;
        TokenHint = tokenHint;
        CreatedAt = createdAt;
        LastStateAt = lastStateAt;
        Settings = settings;
    }

    public ChannelId Id { get; }

    public TokenHash? TokenHash { get; private set; }

    public string? TokenHint { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? LastStateAt { get; private set; }

    /// <summary>What the streamer configured on the extension's configuration page.</summary>
    public ChannelSettings Settings { get; private set; }

    /// <summary>Whether a producer can post state for this channel at all.</summary>
    public bool HasToken => TokenHash is not null;

    public static Channel Create(ChannelId id, DateTimeOffset createdAt) =>
        new(id, tokenHash: null, tokenHint: null, createdAt, lastStateAt: null, ChannelSettings.Default);

    /// <summary>Rebuilds a channel from storage without re-running creation rules.</summary>
    public static Channel Restore(
        ChannelId id,
        TokenHash? tokenHash,
        string? tokenHint,
        DateTimeOffset createdAt,
        DateTimeOffset? lastStateAt,
        ChannelSettings settings) =>
        new(id, tokenHash, tokenHint, createdAt, lastStateAt, settings);

    public void RotateToken(StreamerToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        TokenHash = token.Hash();
        TokenHint = token.Hint();
    }

    /// <summary>
    /// Unbinds the streamer token. The channel and its settings stay; what goes with the token is
    /// the traffic it carried, so the configuration page stops reporting a state that can no longer
    /// arrive.
    /// </summary>
    public void ClearToken()
    {
        TokenHash = null;
        TokenHint = null;
        LastStateAt = null;
    }

    public void UpdateSettings(ChannelSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Settings = settings;
    }

    public void MarkStateReceived(DateTimeOffset at) => LastStateAt = at;
}
