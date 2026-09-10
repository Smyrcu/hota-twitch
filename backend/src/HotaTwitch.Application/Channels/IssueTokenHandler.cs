using HotaTwitch.Application.Abstractions;
using HotaTwitch.Domain.Channels;
using Microsoft.Extensions.Logging;

namespace HotaTwitch.Application.Channels;

/// <summary>
/// Generates a streamer token for a channel, replacing any token it already has. The plain token
/// is returned once; from here on only its hash and hint exist.
/// </summary>
public sealed class IssueTokenHandler(IChannelRepository channels, IClock clock, ILogger<IssueTokenHandler> logger)
{
    public async Task<string> HandleAsync(ChannelId channelId, CancellationToken cancellationToken)
    {
        var token = StreamerToken.Generate();
        var hint = token.Hint();
        var channel = await channels.FindByIdAsync(channelId, cancellationToken)
            ?? Channel.Create(channelId, clock.UtcNow);

        channel.RotateToken(token);

        await channels.SaveAsync(channel, cancellationToken);
        IssueTokenLog.TokenIssued(logger, channelId.Value, hint);

        return token.Value;
    }
}

internal static partial class IssueTokenLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Issued streamer token {TokenHint} for channel {ChannelId}.")]
    public static partial void TokenIssued(ILogger logger, string channelId, string tokenHint);
}
