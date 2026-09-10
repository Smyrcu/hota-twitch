using System.IO.Compression;

namespace H3Assets;

/// <summary>
/// Reads one payload out of a LOD archive. Both archive formats store payloads the same way — zlib,
/// or verbatim when the compressed size is 0 — and both take their offsets and sizes from a
/// directory that a corrupt file, or a wrong key, can make arbitrary. Every bound is therefore
/// checked in 64-bit arithmetic before anything is allocated or indexed.
/// </summary>
internal static class LodPayload
{
    public static byte[] Read(byte[] archive, uint offset, uint size, uint compressedSize, string what)
    {
        var stored = compressedSize == 0 ? size : compressedSize;
        if ((long)offset + stored > archive.Length)
        {
            throw new InvalidDataException($"{what} declares a range past the end of the archive");
        }

        if (compressedSize == 0)
        {
            return archive.AsSpan((int)offset, (int)size).ToArray();
        }

        // DEFLATE cannot expand by more than 1032:1, so anything beyond that is a corrupt size
        // field rather than a large resource — checked before it becomes an allocation.
        var largestPossible = ((long)compressedSize * 1032) + 1024;
        if (size > largestPossible)
        {
            throw new InvalidDataException(
                $"{what} declares {size} bytes from {compressedSize} compressed, beyond what zlib can expand to");
        }

        using var compressed = new MemoryStream(archive, (int)offset, (int)compressedSize);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        var decompressed = new byte[size];
        var total = 0;
        while (total < decompressed.Length)
        {
            var read = zlib.Read(decompressed, total, decompressed.Length - total);
            if (read == 0)
            {
                throw new InvalidDataException($"{what} inflated to {total} bytes, short of the declared {size}");
            }

            total += read;
        }

        return zlib.ReadByte() == -1
            ? decompressed
            : throw new InvalidDataException($"{what} inflated past its declared size of {size}");
    }
}
