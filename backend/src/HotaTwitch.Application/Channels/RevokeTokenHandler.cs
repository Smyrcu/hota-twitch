using HotaTwitch.Application.Abstractions;
using HotaTwitch.Application.Broadcasting;
using HotaTwitch.Domain.Channels;
using Microsoft.Extensions.Logging;

namespace HotaTwitch.Application.Channels;

/// <summary>
/// Unbinds a channel's streamer token and drops anything the channel still had waiting, so no
/// broadcast outlives the token.
/// </summary>
public sealed class RevokeTokenHandler(
    IChannelRepository channels,
    BroadcastCoalescer coalescer,
    ILogger<RevokeTokenHandler> logger)
{
    public async Task HandleAsync(ChannelId channelId, CancellationToken cancellationToken)
    {
        var channel = await channels.FindByIdAsync(channelId, cancellationToken);
        if (channel is not null)
        {
            channel.ClearToken();
            await channels.SaveAsync(channel, cancellationToken);
            RevokeTokenLog.TokenRevoked(logger, channelId.Value);
        }

        coalescer.Forget(channelId);
    }
}

internal static partial class RevokeTokenLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Revoked the streamer token of channel {ChannelId}.")]
    public static partial void TokenRevoked(ILogger logger, string channelId);
}
