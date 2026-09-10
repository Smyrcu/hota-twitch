using System.Text;

namespace H3Assets;

/// <summary>
/// The game's array of hero portrait resource names, indexed by a hero's picture id. It lives only in
/// the running game (HotA.lod stores names as hashes, so the archive cannot be enumerated), and is
/// found by locating the first entry's name and then a pointer to it. Names are copied out on
/// <see cref="Locate"/> so the table stays usable after the game exits.
/// </summary>
public sealed class PortraitTable
{
    public const string SmallSeed = "hps000kn.pcx";
    public const string LargeSeed = "hpl000kn.pcx";

    private const int MaximumEntries = 1024;
    private const int MaximumNameLength = 16;
    private const int ChunkSize = 4 * 1024 * 1024;

    private PortraitTable(IReadOnlyList<string> names)
    {
        Names = names;
    }

    public IReadOnlyList<string> Names { get; }

    public static PortraitTable? Locate(IProcessMemory memory, string seed)
    {
        PortraitTable? best = null;
        foreach (var text in FindAscii(memory, seed))
        {
            foreach (var reference in FindAligned(memory, BitConverter.GetBytes(text)))
            {
                var names = ReadNames(memory, reference);
                if (best is null || names.Count > best.Names.Count)
                {
                    best = new PortraitTable(names);
                }
            }
        }

        return best?.Names.Count > 0 ? best : null;
    }

    private static List<string> ReadNames(IProcessMemory memory, uint tableAddress)
    {
        var names = new List<string>();
        var pointer = new byte[4];
        while (names.Count < MaximumEntries)
        {
            if (!memory.TryRead(tableAddress + (uint)(4 * names.Count), pointer))
            {
                break;
            }

            var name = ReadCString(memory, BitConverter.ToUInt32(pointer), MaximumNameLength);
            if (!IsPortraitName(name))
            {
                break;
            }

            names.Add(name);
        }

        return names;
    }

    private static bool IsPortraitName(string name)
    {
        return name.Length is >= 8 and <= 15 && name.EndsWith(".pcx", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadCString(IProcessMemory memory, uint address, int maximum)
    {
        var buffer = new byte[maximum];
        if (!memory.TryRead(address, buffer))
        {
            return string.Empty;
        }

        var length = Array.IndexOf(buffer, (byte)0);
        return Encoding.ASCII.GetString(buffer, 0, length < 0 ? maximum : length);
    }

    /// <summary>Addresses of a NUL-terminated, case-insensitive ASCII match for <paramref name="text"/>.</summary>
    private static List<uint> FindAscii(IProcessMemory memory, string text)
    {
        var needle = Encoding.ASCII.GetBytes(text.ToLowerInvariant());
        var hits = new List<uint>();
        Scan(memory, needle.Length + 1, (buffer, length, baseAddress) =>
        {
            for (var i = 0; i + needle.Length < length; i++)
            {
                var match = true;
                for (var k = 0; k < needle.Length && match; k++)
                {
                    var b = buffer[i + k];
                    match = (b is >= (byte)'A' and <= (byte)'Z' ? (byte)(b + 32) : b) == needle[k];
                }

                if (match && buffer[i + needle.Length] == 0)
                {
                    hits.Add(baseAddress + (uint)i);
                }
            }
        });
        return hits;
    }

    /// <summary>4-byte-aligned addresses holding <paramref name="value"/>.</summary>
    private static List<uint> FindAligned(IProcessMemory memory, byte[] value)
    {
        var hits = new List<uint>();
        Scan(memory, value.Length, (buffer, length, baseAddress) =>
        {
            var index = 0;
            while (index < length)
            {
                var relative = buffer.AsSpan(index, length - index).IndexOf(value);
                if (relative < 0)
                {
                    break;
                }

                var address = baseAddress + (uint)(index + relative);
                if (address % 4 == 0)
                {
                    hits.Add(address);
                }

                index += relative + 1;
            }
        });
        return hits;
    }

    private static void Scan(IProcessMemory memory, int overlap, Action<byte[], int, uint> onChunk)
    {
        var buffer = new byte[ChunkSize];
        var step = (uint)(ChunkSize - overlap);
        foreach (var region in memory.ReadableRegions)
        {
            var pos = region.Start;
            while (pos < region.End)
            {
                var length = (int)Math.Min(ChunkSize, region.End - pos);
                if (!memory.TryRead(pos, buffer.AsSpan(0, length)))
                {
                    break;
                }

                onChunk(buffer, length, pos);

                // A region reaching the top of the address space would wrap this cursor back to a
                // low address and scan for ever.
                if (uint.MaxValue - pos < step)
                {
                    break;
                }

                pos += step;
            }
        }
    }
}
