using HotaTwitch.Application.Abstractions;
using HotaTwitch.Application.Broadcasting;
using HotaTwitch.Domain.Broadcasting;
using HotaTwitch.Domain.Channels;
using HotaTwitch.Domain.State;
using Microsoft.Extensions.Logging;

namespace HotaTwitch.Application.Ingest;

/// <summary>Section 2 of the protocol: authenticate the producer, then queue its state for broadcast.</summary>
public sealed class IngestStateHandler(
    IChannelRepository channels,
    IIngestRateLimiter rateLimiter,
    BroadcastCoalescer coalescer,
    IClock clock,
    ILogger<IngestStateHandler> logger)
{
    public async Task<IngestOutcome> HandleAsync(IngestStateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!StreamerToken.TryParse(command.BearerToken, out var token))
        {
            return IngestOutcome.UnknownToken;
        }

        var tokenHash = token.Hash();
        var channel = await channels.FindByTokenHashAsync(tokenHash, cancellationToken);
        if (channel is null)
        {
            return IngestOutcome.UnknownToken;
        }

        if (command.Document.Length > BroadcastPolicy.MaxStateDocumentBytes)
        {
            return IngestOutcome.PayloadTooLarge;
        }

        if (!rateLimiter.TryAcquire(tokenHash))
        {
            return IngestOutcome.RateLimited;
        }

        if (!StateDocumentValidator.TryValidate(command.Document.Span, out var error))
        {
            IngestStateLog.DocumentRejected(logger, channel.Id.Value, error);
            return IngestOutcome.InvalidDocument;
        }

        channel.MarkStateReceived(clock.UtcNow);
        if (!await channels.TouchLastStateAsync(channel, cancellationToken))
        {
            return IngestOutcome.UnknownToken;
        }

        if (BroadcastMessage.TryEncode(command.Document.Span, out var message))
        {
            coalescer.Submit(channel.Id, message);
        }
        else
        {
            IngestStateLog.MessageTooLargeToBroadcast(logger, channel.Id.Value, command.Document.Length);
        }

        return IngestOutcome.Accepted;
    }
}

internal static partial class IngestStateLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Channel {ChannelId} posted a state document that is not valid: {Reason}")]
    public static partial void DocumentRejected(ILogger logger, string channelId, string? reason);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "State of channel {ChannelId} ({DocumentBytes} B) does not fit the PubSub message cap; the previous state stays.")]
    public static partial void MessageTooLargeToBroadcast(ILogger logger, string channelId, int documentBytes);
}
