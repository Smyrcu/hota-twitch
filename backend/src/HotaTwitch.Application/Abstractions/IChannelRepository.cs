using HotaTwitch.Domain.Channels;

namespace HotaTwitch.Application.Abstractions;

public interface IChannelRepository
{
    Task<Channel?> FindByTokenHashAsync(TokenHash tokenHash, CancellationToken cancellationToken);

    Task<Channel?> FindByIdAsync(ChannelId channelId, CancellationToken cancellationToken);

    Task SaveAsync(Channel channel, CancellationToken cancellationToken);

    Task RemoveAsync(ChannelId channelId, CancellationToken cancellationToken);

    /// <summary>
    /// Writes <see cref="Channel.LastStateAt"/> only, and only while the channel still carries the
    /// token it was loaded with. Returns <see langword="false"/> when the row is gone or the token
    /// has been rotated meanwhile, so an in-flight ingest can never resurrect a revoked token.
    /// </summary>
    Task<bool> TouchLastStateAsync(Channel channel, CancellationToken cancellationToken);
}
