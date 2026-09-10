using HotaTwitch.Domain.Channels;

namespace HotaTwitch.Infrastructure.Persistence;

internal static class ChannelRecordMapper
{
    public static Channel ToDomain(this ChannelRecord record) =>
        Channel.Restore(
            new ChannelId(record.ChannelId),
            record.TokenHash is null ? null : new TokenHash(record.TokenHash),
            record.TokenHint,
            record.CreatedAt,
            record.LastStateAt,
            ChannelSettings.Restore(record.UiScale));

    public static ChannelRecord ToRecord(this Channel channel) =>
        new()
        {
            ChannelId = channel.Id.Value,
            TokenHash = channel.TokenHash?.Value,
            TokenHint = channel.TokenHint,
            CreatedAt = channel.CreatedAt,
            LastStateAt = channel.LastStateAt,
            UiScale = channel.Settings.UiScale,
        };

    /// <summary>
    /// Writes back the columns the token lifecycle owns. The settings have a write path of their
    /// own, so issuing, rotating or clearing a token cannot revert a scale the streamer changed
    /// meanwhile.
    /// </summary>
    public static void CopyInto(this Channel channel, ChannelRecord record)
    {
        record.TokenHash = channel.TokenHash?.Value;
        record.TokenHint = channel.TokenHint;
        record.LastStateAt = channel.LastStateAt;
    }
}
