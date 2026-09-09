using HotaTwitch.Application.Abstractions;
using HotaTwitch.Domain.Channels;
using Microsoft.Extensions.Logging;

namespace HotaTwitch.Application.Channels;

/// <summary>
/// Drops a channel's token. The channel row goes with it: without a token there is nothing left
/// to relay, and the configuration page reports the channel as unconfigured again.
/// </summary>
public sealed class RevokeTokenHandler(IChannelRepository channels, ILogger<RevokeTokenHandler> logger)
{
    public async Task HandleAsync(ChannelId channelId, CancellationToken cancellationToken)
    {
        await channels.RemoveAsync(channelId, cancellationToken);
        RevokeTokenLog.TokenRevoked(logger, channelId.Value);
    }
}

internal static partial class RevokeTokenLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Revoked the streamer token of channel {ChannelId}.")]
    public static partial void TokenRevoked(ILogger logger, string channelId);
}
