namespace HotaTwitch.Application.Ingest;

/// <summary>How an ingest request ended; the API maps these to the status codes in the protocol.</summary>
public enum IngestOutcome
{
    Accepted,
    UnknownToken,
    PayloadTooLarge,
    RateLimited,
    InvalidDocument,
}
