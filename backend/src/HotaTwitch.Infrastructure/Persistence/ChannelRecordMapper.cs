using HotaTwitch.Domain.Channels;

namespace HotaTwitch.Infrastructure.Persistence;

internal static class ChannelRecordMapper
{
    public static Channel ToDomain(this ChannelRecord record) =>
        Channel.Restore(
            new ChannelId(record.ChannelId),
            new TokenHash(record.TokenHash),
            record.TokenHint,
            record.CreatedAt,
            record.LastStateAt);

    public static ChannelRecord ToRecord(this Channel channel) =>
        new()
        {
            ChannelId = channel.Id.Value,
            TokenHash = channel.TokenHash.Value,
            TokenHint = channel.TokenHint,
            CreatedAt = channel.CreatedAt,
            LastStateAt = channel.LastStateAt,
        };

    public static void CopyInto(this Channel channel, ChannelRecord record)
    {
        record.TokenHash = channel.TokenHash.Value;
        record.TokenHint = channel.TokenHint;
        record.LastStateAt = channel.LastStateAt;
    }
}
