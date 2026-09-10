namespace H3Assets;

public sealed record HotaLodEntry(uint NameHash, uint Offset, uint Size, uint CompressedSize);

/// <summary>
/// Reader for HotA's variant of the LOD archive (<c>Data/HotA.lod</c>), whose directory is obfuscated
/// while its payloads are not. Each 32-byte entry is
/// <c>{uint nameHash; uint offset ^ key; uint size ^ key; uint compressedSize ^ key; byte[16] opaque}</c>.
/// The key is the dword at header offset 12, a field the plaintext format does not use. Decoding
/// with it is what makes the directory tile the payload region exactly — first entry at the end of
/// the directory, each next where the previous ended, last ending on the final byte — which is the
/// check that establishes the key rather than assuming it. Names survive only as
/// <see cref="HotaLodEntry.NameHash"/> (32-bit FNV-1a over the lowercased name), which makes
/// entries impossible to enumerate but exact to look up. Payloads are plain zlib, or stored
/// verbatim when <see cref="HotaLodEntry.CompressedSize"/> is 0.
/// </summary>
public sealed class HotaLodArchive : IResourceArchive
{
    private const int HeaderSize = 92;
    private const int EntrySize = 32;

    private readonly byte[] _data;
    private readonly Dictionary<uint, HotaLodEntry> _entriesByHash;

    private HotaLodArchive(string path, byte[] data, uint key, IReadOnlyList<HotaLodEntry> entries)
    {
        _data = data;
        // Two names hashing alike would be indistinguishable here; the shipped archive has no such
        // pair, and Entries keeps them all so a caller can check.
        _entriesByHash = entries
            .GroupBy(e => e.NameHash)
            .ToDictionary(g => g.Key, g => g.First());

        Label = Path.GetFileName(path);
        Key = key;
        Entries = entries;
    }

    public string Label { get; }

    public uint Key { get; }

    public IReadOnlyList<HotaLodEntry> Entries { get; }

    public long Length => _data.Length;

    /// <summary>Offset of the first payload byte: everything before it is header plus directory.</summary>
    public uint DataStart => (uint)(HeaderSize + (Entries.Count * EntrySize));

    public static uint NameHash(string name)
    {
        var hash = 2166136261u;
        foreach (var c in name)
        {
            hash ^= (byte)(c is >= 'A' and <= 'Z' ? c + 32 : c);
            hash *= 16777619u;
        }

        return hash;
    }

    public static HotaLodArchive Open(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length < HeaderSize || data[0] != 'L' || data[1] != 'O' || data[2] != 'D')
        {
            throw new InvalidDataException($"{path}: missing LOD magic header");
        }

        var count = BitConverter.ToUInt32(data, 8);
        var key = BitConverter.ToUInt32(data, 12);
        if (HeaderSize + ((long)count * EntrySize) > data.Length)
        {
            throw new InvalidDataException($"{path}: directory of {count} entries does not fit in the file");
        }

        var entries = new List<HotaLodEntry>((int)count);
        for (var i = 0; i < count; i++)
        {
            var pos = HeaderSize + (i * EntrySize);
            entries.Add(new HotaLodEntry(
                BitConverter.ToUInt32(data, pos),
                BitConverter.ToUInt32(data, pos + 4) ^ key,
                BitConverter.ToUInt32(data, pos + 8) ^ key,
                BitConverter.ToUInt32(data, pos + 12) ^ key));
        }

        return new HotaLodArchive(path, data, key, entries);
    }

    public HotaLodEntry? TryFind(string name)
    {
        return _entriesByHash.GetValueOrDefault(NameHash(name));
    }

    public HotaLodEntry Find(string name)
    {
        return TryFind(name) ?? throw new FileNotFoundException($"{name} is not in {Label}");
    }

    public byte[] Read(HotaLodEntry entry)
    {
        return LodPayload.Read(
            _data, entry.Offset, entry.Size, entry.CompressedSize, $"{Label}: entry {entry.NameHash:X8}");
    }

    public byte[]? TryRead(string name)
    {
        var entry = TryFind(name);
        return entry is null ? null : Read(entry);
    }
}
