using System.Collections.Concurrent;
using HotaTwitch.Application.Abstractions;
using HotaTwitch.Domain.Broadcasting;
using HotaTwitch.Domain.Channels;

namespace HotaTwitch.Application.Ingest;

/// <summary>
/// Allows <see cref="BroadcastPolicy.MaxIngestRequestsPerWindow"/> requests per token inside a
/// sliding <see cref="BroadcastPolicy.IngestRateWindow"/>, by remembering that many timestamps.
/// </summary>
public sealed class SlidingWindowIngestRateLimiter(IClock clock) : IIngestRateLimiter
{
    private readonly ConcurrentDictionary<TokenHash, Window> windows = new();

    public bool TryAcquire(TokenHash tokenHash) =>
        windows.GetOrAdd(tokenHash, static _ => new Window()).TryAcquire(clock.UtcNow);

    private sealed class Window
    {
        private readonly Lock gate = new();
        private readonly DateTimeOffset[] hits = new DateTimeOffset[BroadcastPolicy.MaxIngestRequestsPerWindow];
        private int next;

        public bool TryAcquire(DateTimeOffset now)
        {
            lock (gate)
            {
                var oldest = hits[next];
                if (oldest != default && now - oldest < BroadcastPolicy.IngestRateWindow)
                {
                    return false;
                }

                hits[next] = now;
                next = (next + 1) % hits.Length;
                return true;
            }
        }
    }
}
