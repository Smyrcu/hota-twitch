using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using HotaTwitch.Application.Abstractions;
using HotaTwitch.Domain.Broadcasting;
using HotaTwitch.Domain.Channels;
using Microsoft.Extensions.Logging;

namespace HotaTwitch.Application.Broadcasting;

/// <summary>
/// Holds at most one pending message per channel and releases it no more often than
/// <see cref="BroadcastPolicy.MinimumBroadcastInterval"/>. A message submitted while another one
/// is still waiting replaces it, so viewers always receive the newest state.
/// </summary>
public sealed class BroadcastCoalescer(IPubSubPublisher publisher, IClock clock, ILogger<BroadcastCoalescer> logger)
{
    private readonly ConcurrentDictionary<ChannelId, PendingChannel> pending = new();

    public void Submit(ChannelId channelId, string message) =>
        pending.GetOrAdd(channelId, static _ => new PendingChannel()).Set(message);

    /// <summary>Publishes every channel whose interval has elapsed. Returns how many were sent.</summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "One channel's publisher failure must not stop the dispatcher or the other channels.")]
    public async Task<int> PublishDueAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var published = 0;

        foreach (var (channelId, channel) in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!channel.TryTakeDue(now, out var message))
            {
                continue;
            }

            try
            {
                await publisher.PublishAsync(channelId, message, cancellationToken);
                published++;
            }
            catch (OperationCanceledException)
            {
                channel.PutBack(message);
                throw;
            }
            catch (Exception exception)
            {
                BroadcastCoalescerLog.PublishFailed(logger, channelId.Value, exception);
            }
        }

        return published;
    }

    private sealed class PendingChannel
    {
        private readonly Lock gate = new();
        private string? message;
        private DateTimeOffset lastPublishedAt = DateTimeOffset.MinValue;

        public void Set(string value)
        {
            lock (gate)
            {
                message = value;
            }
        }

        public bool TryTakeDue(DateTimeOffset now, [NotNullWhen(true)] out string? value)
        {
            lock (gate)
            {
                if (message is null || now - lastPublishedAt < BroadcastPolicy.MinimumBroadcastInterval)
                {
                    value = null;
                    return false;
                }

                value = message;
                message = null;
                lastPublishedAt = now;
                return true;
            }
        }

        /// <summary>Returns a taken message to the slot unless a newer one has arrived meanwhile.</summary>
        public void PutBack(string value)
        {
            lock (gate)
            {
                message ??= value;
            }
        }
    }
}

internal static partial class BroadcastCoalescerLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Broadcast to channel {ChannelId} failed.")]
    public static partial void PublishFailed(ILogger logger, string channelId, Exception exception);
}
