namespace HotaTwitch.Domain.Broadcasting;

/// <summary>The limits from <c>docs/protocol.md</c> sections 2 and 3.</summary>
public static class BroadcastPolicy
{
    /// <summary>Largest state document a producer may post.</summary>
    public const int MaxStateDocumentBytes = 64 * 1024;

    /// <summary>Largest PubSub message the backend will hand to Twitch.</summary>
    public const int MaxEncodedMessageBytes = 5120;

    /// <summary>Ingest requests a single token may make inside <see cref="IngestRateWindow"/>.</summary>
    public const int MaxIngestRequestsPerWindow = 2;

    public static TimeSpan IngestRateWindow => TimeSpan.FromSeconds(1);

    /// <summary>Shortest gap between two broadcasts on one channel.</summary>
    public static TimeSpan MinimumBroadcastInterval => TimeSpan.FromSeconds(1);
}
