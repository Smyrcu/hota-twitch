using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text;

namespace HotaTwitch.Domain.Broadcasting;

/// <summary>
/// The PubSub payload from <c>docs/protocol.md</c> section 3: <see cref="Prefix"/> followed by
/// base64 of gzip(state JSON).
/// </summary>
public static class BroadcastMessage
{
    public const string Prefix = "gz:";

    /// <summary>
    /// Encodes a state document. Returns <see langword="false"/> when the document is empty or
    /// the encoded message would exceed <see cref="BroadcastPolicy.MaxEncodedMessageBytes"/>.
    /// </summary>
    public static bool TryEncode(ReadOnlySpan<byte> stateJson, [NotNullWhen(true)] out string? message)
    {
        message = null;

        if (stateJson.IsEmpty)
        {
            return false;
        }

        var candidate = Prefix + Convert.ToBase64String(Compress(stateJson));
        if (Encoding.UTF8.GetByteCount(candidate) > BroadcastPolicy.MaxEncodedMessageBytes)
        {
            return false;
        }

        message = candidate;
        return true;
    }

    private static byte[] Compress(ReadOnlySpan<byte> payload)
    {
        using var target = new MemoryStream();
        using (var gzip = new GZipStream(target, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(payload);
        }

        return target.ToArray();
    }
}
