namespace HotaTwitch.Application.Ingest;

/// <param name="BearerToken">The raw value of the producer's Authorization header, without the scheme.</param>
/// <param name="Document">The posted body, verbatim; it is relayed unchanged.</param>
public sealed record IngestStateCommand(string? BearerToken, ReadOnlyMemory<byte> Document)
{
    /// <summary>Keeps the plain streamer token out of logs and exception messages.</summary>
    public override string ToString() => $"{nameof(IngestStateCommand)} {{ Document = {Document.Length} B }}";
}
