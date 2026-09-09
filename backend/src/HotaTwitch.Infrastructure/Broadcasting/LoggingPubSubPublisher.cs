using HotaTwitch.Application.Abstractions;
using HotaTwitch.Domain.Channels;
using Microsoft.Extensions.Logging;

namespace HotaTwitch.Infrastructure.Broadcasting;

/// <summary>
/// Stands in for Twitch when <c>TWITCH_FAKE_PUBSUB</c> is on, so the service can be exercised
/// end to end on a machine that has no extension secret.
/// </summary>
internal sealed class LoggingPubSubPublisher(ILogger<LoggingPubSubPublisher> logger) : IPubSubPublisher
{
    public Task PublishAsync(ChannelId channelId, string message, CancellationToken cancellationToken)
    {
        LoggingPubSubLog.WouldBroadcast(logger, channelId.Value, message.Length, message);
        return Task.CompletedTask;
    }
}

internal static partial class LoggingPubSubLog
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Fake PubSub: would broadcast {MessageLength} B to channel {ChannelId}: {Message}")]
    public static partial void WouldBroadcast(ILogger logger, string channelId, int messageLength, string message);
}
