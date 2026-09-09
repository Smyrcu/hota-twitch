using System.Net;

namespace HotaTwitch.Api.Tests.Infrastructure;

/// <summary>Body without a Content-Length, so the size limit has to be enforced while reading.</summary>
internal sealed class ChunkedContent(byte[] payload) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        stream.WriteAsync(payload, 0, payload.Length);

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
