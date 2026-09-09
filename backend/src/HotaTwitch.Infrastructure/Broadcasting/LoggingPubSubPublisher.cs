using HotaTwitch.Application.Abstractions;
using HotaTwitch.Domain.Channels;
using Microsoft.Extensions.Logging;

namespace HotaTwitch.Infrastructure.Broadcasting;

/// <summary>
/// Stands in for Twitch when <c>TWITCH_FAKE_PUBSUB</c> is on, so the service can be exercised end
/// to end against a made-up extension secret instead of a real one.
/// </summary>
internal sealed class LoggingPubSubPublisher(ILogger<LoggingPubSubPublisher> logger) : IPubSubPublisher
{
    public Task PublishAsync(ChannelId channelId, string message, CancellationToken cancellationToken)
    {
        LoggingPubSubLog.WouldBroadcast(logger, channelId.Value, message.Length);
        LoggingPubSubLog.Payload(logger, message);
        return Task.CompletedTask;
    }
}

internal static partial class LoggingPubSubLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Fake PubSub: would broadcast {MessageLength} B to channel {ChannelId}.")]
    public static partial void WouldBroadcast(ILogger logger, string channelId, int messageLength);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fake PubSub payload: {Message}")]
    public static partial void Payload(ILogger logger, string message);
}
