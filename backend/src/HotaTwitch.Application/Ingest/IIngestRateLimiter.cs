using HotaTwitch.Domain.Channels;

namespace HotaTwitch.Application.Ingest;

public interface IIngestRateLimiter
{
    /// <summary>Takes a slot for one ingest request, or reports that the token is over its rate.</summary>
    bool TryAcquire(TokenHash tokenHash);
}
