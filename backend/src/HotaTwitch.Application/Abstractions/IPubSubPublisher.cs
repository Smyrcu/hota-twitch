using HotaTwitch.Domain.Channels;

namespace HotaTwitch.Application.Abstractions;

/// <summary>Sends one already encoded broadcast message to the viewers of a channel.</summary>
public interface IPubSubPublisher
{
    Task PublishAsync(ChannelId channelId, string message, CancellationToken cancellationToken);
}
