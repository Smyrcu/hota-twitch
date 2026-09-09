using HotaTwitch.Domain.Channels;

namespace HotaTwitch.Infrastructure.Twitch;

public interface ITwitchExtensionJwtFactory
{
    /// <summary>
    /// Signs the external JWT that authorises one PubSub broadcast to <paramref name="channelId"/>.
    /// </summary>
    string CreateExternalToken(ChannelId channelId);
}
