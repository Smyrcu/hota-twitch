using HotaTwitch.Application.Abstractions;
using HotaTwitch.Domain.Channels;

namespace HotaTwitch.Application.Channels;

public sealed class GetChannelStatusHandler(IChannelRepository channels)
{
    public async Task<ChannelStatus> HandleAsync(ChannelId channelId, CancellationToken cancellationToken)
    {
        var channel = await channels.FindByIdAsync(channelId, cancellationToken);

        return channel is null
            ? ChannelStatus.Unconfigured
            : new ChannelStatus(channel.HasToken, channel.TokenHint, channel.LastStateAt, channel.Settings);
    }
}
