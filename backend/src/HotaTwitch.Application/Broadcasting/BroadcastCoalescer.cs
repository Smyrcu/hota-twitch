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
    /// <summary>Channels are published side by side so that one slow call cannot hold up the rest.</summary>
    private const int MaxConcurrentBroadcasts = 8;

    private readonly ConcurrentDictionary<ChannelId, PendingChannel> pending = new();

    public void Submit(ChannelId channelId, string message) =>
        pending.GetOrAdd(channelId, static _ => new PendingChannel()).Set(message);

    /// <summary>Drops whatever a channel still has waiting, because it no longer has a token.</summary>
    public void Forget(ChannelId channelId) => pending.TryRemove(channelId, out _);

    /// <summary>Publishes every channel whose interval has elapsed. Returns how many were sent.</summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "One channel's publisher failure must not stop the dispatcher or the other channels.")]
    public async Task<int> PublishDueAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var due = new List<(ChannelId ChannelId, string Message)>();

        foreach (var (channelId, channel) in pending)
        {
            if (channel.TryTakeDue(now, out var message))
            {
                due.Add((channelId, message));
            }
        }

        if (due.Count == 0)
        {
            return 0;
        }

        var published = 0;
        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = MaxConcurrentBroadcasts,
        };

        await Parallel.ForEachAsync(due, options, async (broadcast, publishToken) =>
        {
            try
            {
                await publisher.PublishAsync(broadcast.ChannelId, broadcast.Message, publishToken);
                Interlocked.Increment(ref published);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // A publisher timeout also arrives here, as a TaskCanceledException that the host
                // did not ask for; swallowing it keeps the dispatcher alive for the next tick.
                BroadcastCoalescerLog.PublishFailed(logger, broadcast.ChannelId.Value, exception);
            }
        });

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
    }
}

internal static partial class BroadcastCoalescerLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Broadcast to channel {ChannelId} failed.")]
    public static partial void PublishFailed(ILogger logger, string channelId, Exception exception);
}
