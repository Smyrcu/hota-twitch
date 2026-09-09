
namespace HotaTwitch.Domain.Channels;

/// <summary>A Twitch channel with a streamer token bound to it.</summary>
public sealed class Channel
{
    private Channel(ChannelId id, TokenHash tokenHash, string tokenHint, DateTimeOffset createdAt, DateTimeOffset? lastStateAt)
    {
        Id = id;
        TokenHash = tokenHash;
        TokenHint = tokenHint;
        CreatedAt = createdAt;
        LastStateAt = lastStateAt;
    }

    public ChannelId Id { get; }

    public TokenHash TokenHash { get; private set; }

    public string TokenHint { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? LastStateAt { get; private set; }

    public static Channel Create(ChannelId id, StreamerToken token, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(token);

        return new Channel(id, token.Hash(), token.Hint(), createdAt, lastStateAt: null);
    }

    /// <summary>Rebuilds a channel from storage without re-running creation rules.</summary>
    public static Channel Restore(ChannelId id, TokenHash tokenHash, string tokenHint, DateTimeOffset createdAt, DateTimeOffset? lastStateAt) =>
        new(id, tokenHash, tokenHint, createdAt, lastStateAt);

    public void RotateToken(StreamerToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        TokenHash = token.Hash();
        TokenHint = token.Hint();
    }

    public void MarkStateReceived(DateTimeOffset at) => LastStateAt = at;
}
