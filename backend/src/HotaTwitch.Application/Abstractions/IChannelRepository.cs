using HotaTwitch.Domain.Channels;

namespace HotaTwitch.Application.Abstractions;

public interface IChannelRepository
{
    Task<Channel?> FindByTokenHashAsync(TokenHash tokenHash, CancellationToken cancellationToken);

    Task<Channel?> FindByIdAsync(ChannelId channelId, CancellationToken cancellationToken);

    Task SaveAsync(Channel channel, CancellationToken cancellationToken);

    Task RemoveAsync(ChannelId channelId, CancellationToken cancellationToken);
}
