using HotaTwitch.Domain.Channels;

namespace HotaTwitch.Application.Abstractions;

public interface IChannelRepository
{
    Task<Channel?> FindByTokenHashAsync(TokenHash tokenHash, CancellationToken cancellationToken);

    Task<Channel?> FindByIdAsync(ChannelId channelId, CancellationToken cancellationToken);

    /// <summary>Writes the channel's streamer token, creating the channel when it has no row yet.</summary>
    Task SaveAsync(Channel channel, CancellationToken cancellationToken);

    /// <summary>
    /// Writes <see cref="Channel.LastStateAt"/> only, and only while the channel still carries the
    /// token it was loaded with. Returns <see langword="false"/> when the row is gone or the token
    /// has been rotated or cleared meanwhile, so an in-flight ingest can never resurrect it.
    /// </summary>
    Task<bool> TouchLastStateAsync(Channel channel, CancellationToken cancellationToken);

    /// <summary>
    /// Writes <see cref="Channel.Settings"/> only, inserting the row when the channel has none.
    /// Touching the one column keeps a settings change and a token rotation from overwriting
    /// each other.
    /// </summary>
    Task SaveSettingsAsync(Channel channel, CancellationToken cancellationToken);
}
