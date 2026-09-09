using System.IO.Compression;
using System.Text;

namespace H3Assets;

public sealed record LodEntry(string Name, uint Offset, uint Size, uint CompressedSize);

public sealed class LodArchive
{
    private const int HeaderSize = 92;
    private const int EntrySize = 32;

    private readonly string _path;
    private readonly byte[] _data;
    private readonly Dictionary<string, LodEntry> _entriesByName;

    private LodArchive(string path, byte[] data, IReadOnlyList<LodEntry> entries)
    {
        _path = path;
        _data = data;

        // An encrypted archive's entry names decode to garbage and can collide (observed in
        // HotA.lod); the first occurrence of a name wins.
        _entriesByName = entries
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        Entries = entries;
    }

    public IReadOnlyList<LodEntry> Entries { get; }

    public static LodArchive Open(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length < HeaderSize || data[0] != 'L' || data[1] != 'O' || data[2] != 'D')
        {
            throw new InvalidDataException($"{path}: missing LOD magic header");
        }

        var count = BitConverter.ToUInt32(data, 8);
        var entries = new List<LodEntry>((int)count);
        var pos = HeaderSize;
        for (var i = 0; i < count; i++)
        {
            if (pos + EntrySize > data.Length)
            {
                throw new InvalidDataException($"{path}: truncated LOD directory at entry {i} of {count}");
            }

            var nameLength = Array.IndexOf(data, (byte)0, pos, 16) - pos;
            if (nameLength < 0)
            {
                nameLength = 16;
            }

            var name = Encoding.ASCII.GetString(data, pos, nameLength);
            var offset = BitConverter.ToUInt32(data, pos + 16);
            var size = BitConverter.ToUInt32(data, pos + 20);
            var compressedSize = BitConverter.ToUInt32(data, pos + 28);
            entries.Add(new LodEntry(name, offset, size, compressedSize));
            pos += EntrySize;
        }

        return new LodArchive(path, data, entries);
    }

    public LodEntry Find(string name)
    {
        return TryFind(name) ?? throw new FileNotFoundException($"{name} is not in {_path}");
    }

    public LodEntry? TryFind(string name)
    {
        return _entriesByName.GetValueOrDefault(name);
    }

    public byte[] Read(LodEntry entry)
    {
        var storedLength = entry.CompressedSize == 0 ? entry.Size : entry.CompressedSize;
        if ((long)entry.Offset + storedLength > _data.Length)
        {
            throw new InvalidDataException($"{_path}: entry '{entry.Name}' declares a range past the end of the archive");
        }

        if (entry.CompressedSize == 0)
        {
            return _data.AsSpan((int)entry.Offset, (int)entry.Size).ToArray();
        }

        using var compressed = new MemoryStream(_data, (int)entry.Offset, (int)entry.CompressedSize);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var decompressed = new MemoryStream((int)entry.Size);
        zlib.CopyTo(decompressed);
        return decompressed.ToArray();
    }
}
