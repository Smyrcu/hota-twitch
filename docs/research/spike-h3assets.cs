#!/usr/bin/env dotnet
#:package SixLabors.ImageSharp@3.*
#:property LangVersion=preview
#:property Nullable=enable
#:property PublishAot=false
#:property TreatWarningsAsErrors=true

// SPIKE (throwaway). Ekstrakcja sprite'ów z archiwów LOD / plików DEF Heroes 3 (SoD + HotA).
//
//   dotnet run h3assets.cs -- list <pattern>                 # wpisy w LOD-ach pasujące do wzorca (substring, bez wielkości liter)
//   dotnet run h3assets.cs -- info <name.def>                # bloki i klatki
//   dotnet run h3assets.cs -- sheet <name.def> <out.png> [cols]   # arkusz kontaktowy, indeks klatki = wiersz*cols + kolumna
//   dotnet run h3assets.cs -- frames <name.def> <outdir>     # każda klatka jako <index>.png
//
// Kolejność archiwów: HotA.lod nadpisuje h3sprite.lod / h3bitmap.lod (jak w grze).

using System.Globalization;
using System.IO.Compression;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

const string GameDir = "/home/smyrcu/Games/Heroic/Heroes of Might and Magic III - Horn of the Abyss";
string[] archives = ["Data/HotA.lod", "Data/HotA_lng.lod", "Data/h3sprite.lod", "Data/h3bitmap.lod"];

if (args.Length < 2)
{
    Console.Error.WriteLine("użycie: list <pattern> | info <def> | sheet <def> <out.png> [cols] | frames <def> <outdir>");
    return 1;
}

var lods = archives.Select(a => LodArchive.Open(Path.Combine(GameDir, a))).ToList();

switch (args[0])
{
    case "list":
        foreach (var lod in lods)
        {
            foreach (var entry in lod.Entries.Where(e => e.Name.Contains(args[1], StringComparison.OrdinalIgnoreCase)).OrderBy(e => e.Name))
            {
                Console.WriteLine($"{Path.GetFileName(lod.Path),-16} {entry.Name,-16} size={entry.Size,8} csize={entry.CompressedSize,8} type={entry.Type}");
            }
        }
        return 0;

    case "info":
    {
        var def = LoadDef(lods, args[1]);
        Console.WriteLine($"{args[1]}: type={def.Type} {def.Width}x{def.Height} blocks={def.Blocks.Count} frames={def.Frames.Count}");
        foreach (var block in def.Blocks)
        {
            Console.WriteLine($"  block id={block.Id} entries={block.Names.Count} first={block.Names.FirstOrDefault()} last={block.Names.LastOrDefault()}");
        }
        if (def.Frames.Count <= 200)
        {
            Console.WriteLine("  nazwy klatek: " + string.Join(" ", def.Frames.Select((f, i) => $"{i}={f.Name}")));
        }
        for (var i = 0; i < Math.Min(def.Frames.Count, 5); i++)
        {
            var f = def.Frames[i];
            Console.WriteLine($"  frame[{i}] {f.Name} fmt={f.Format} full={f.FullWidth}x{f.FullHeight} data={f.Width}x{f.Height} margin=({f.LeftMargin},{f.TopMargin})");
        }
        return 0;
    }

    case "sheet":
    {
        var def = LoadDef(lods, args[1]);
        var cols = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 16;
        var cell = 2; // odstęp
        var cw = def.Width + cell;
        var ch = def.Height + cell;
        var rows = (def.Frames.Count + cols - 1) / cols;
        using var sheet = new Image<Rgba32>(cols * cw, rows * ch, new Rgba32(40, 40, 40, 255));
        for (var i = 0; i < def.Frames.Count; i++)
        {
            using var img = def.Render(i);
            var x = (i % cols) * cw;
            var y = (i / cols) * ch;
            sheet.Mutate(ctx => ctx.DrawImage(img, new Point(x, y), 1f));
        }
        sheet.SaveAsPng(args[2]);
        Console.WriteLine($"{args[1]}: {def.Frames.Count} klatek, {cols} kolumn, komórka {cw}x{ch} → {args[2]} ({sheet.Width}x{sheet.Height})");
        return 0;
    }

    case "frames":
    {
        var def = LoadDef(lods, args[1]);
        Directory.CreateDirectory(args[2]);
        for (var i = 0; i < def.Frames.Count; i++)
        {
            using var img = def.Render(i);
            img.SaveAsPng(Path.Combine(args[2], $"{i}.png"));
        }
        Console.WriteLine($"{args[1]}: zapisano {def.Frames.Count} klatek do {args[2]}");
        return 0;
    }

    case "pcx":
    {
        // PCX z LOD-a: u32 size, u32 width, u32 height; 8 bpp (paleta 256*3 na końcu) albo 24 bpp BGR.
        LodEntry? entry = null;
        LodArchive? owner = null;
        foreach (var lod in lods)
        {
            entry = lod.Find(args[1]);
            if (entry is not null) { owner = lod; break; }
        }
        if (entry is null || owner is null) throw new FileNotFoundException($"{args[1]} nie ma w żadnym LOD");
        var data = owner.Read(entry);
        var size = BitConverter.ToInt32(data, 0);
        var width = BitConverter.ToInt32(data, 4);
        var height = BitConverter.ToInt32(data, 8);
        using var img = new Image<Rgba32>(width, height);
        if (size == width * height)
        {
            var paletteOffset = 12 + size;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var idx = data[12 + y * width + x];
                    img[x, y] = new Rgba32(data[paletteOffset + idx * 3], data[paletteOffset + idx * 3 + 1], data[paletteOffset + idx * 3 + 2], 255);
                }
            }
        }
        else if (size == width * height * 3)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var o = 12 + (y * width + x) * 3;
                    img[x, y] = new Rgba32(data[o + 2], data[o + 1], data[o], 255);
                }
            }
        }
        else
        {
            throw new InvalidDataException($"PCX {args[1]}: size={size} nie pasuje do {width}x{height}");
        }
        img.SaveAsPng(args[2]);
        Console.WriteLine($"{args[1]}: {width}x{height} {(size == width * height ? "8bpp" : "24bpp")} → {args[2]}");
        return 0;
    }

    case "dumpdef":
    {
        // dumpdef <name.def> <outdir> — zrzut załadowanego DEF-a z pamięci gry (HotA trzyma tam wersje rozszerzone,
        // np. CPRSMALL.def z portretami Cove/Factory/Bulwark). Struktury: H3LoadedDef (H3API), H3DefFrame, H3Palette888.
        var pid = GameProcess.FindPid() ?? throw new InvalidOperationException("gra nie działa (brak procesu 'h3hota HD.exe')");
        using var mem = ProcMem.Open(pid);
        var wanted = args[1];
        var hits = mem.FindAscii(wanted);
        Console.Error.WriteLine($"[dumpdef] pid={pid}, '{wanted}' wystąpień: {hits.Count}");
        LoadedDef? def = null;
        foreach (var hit in hits)
        {
            var candidate = LoadedDef.TryParse(mem, hit - 4); // nazwa zasobu leży pod +0x04
            if (candidate is null) continue;
            var loaded = candidate.CountLoadedFrames(mem);
            Console.Error.WriteLine($"[dumpdef] H3LoadedDef @0x{hit - 4:X8}: {candidate.Width}x{candidate.Height}, grup={candidate.GroupsCount}, klatek w grupie 0={candidate.FrameCount}, z danymi={loaded}");
            if (def is null || loaded > def.CountLoadedFrames(mem)) def = candidate;
        }
        if (def is null)
        {
            Console.Error.WriteLine("[dumpdef] nie znaleziono poprawnej struktury H3LoadedDef");
            return 4;
        }
        Directory.CreateDirectory(args[2]);
        var saved = 0;
        for (var i = 0; i < def.FrameCount; i++)
        {
            using var img = def.ReadFrame(mem, i);
            if (img is null) continue;
            img.SaveAsPng(Path.Combine(args[2], $"{i}.png"));
            saved++;
        }
        Console.WriteLine($"{wanted}: {saved}/{def.FrameCount} klatek z pamięci → {args[2]}");
        return 0;
    }

    case "portraits":
    {
        // portraits <outdir> [--large] — portrety bohaterów wg id obrazka (małe 48x32 albo duże 58x64): nazwa z tablicy
        // HotA w pamięci, PCX z h3bitmap.lod; bohaterowie HotA z pamięci (tylko już wczytane przez grę).
        var pid = GameProcess.FindPid() ?? throw new InvalidOperationException("gra nie działa (brak procesu 'h3hota HD.exe')");
        using var mem = ProcMem.Open(pid);
        var large = args.Contains("--large");
        var table = PortraitTable.Locate(mem, large ? PortraitTable.LargeSeed : PortraitTable.SmallSeed);
        if (table is null)
        {
            Console.Error.WriteLine("[portraits] nie znaleziono tablicy nazw portretów");
            return 4;
        }
        Directory.CreateDirectory(args[1]);
        var saved = 0;
        var fromLod = 0;
        var wanted = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase); // nazwa → id obrazków (HotA)
        for (var id = 0; id < table.Count; id++)
        {
            var name = table.Name(mem, id);
            LodEntry? entry = null;
            LodArchive? owner = null;
            foreach (var lod in lods)
            {
                entry = lod.Find(name);
                if (entry is not null) { owner = lod; break; }
            }
            if (entry is null || owner is null)
            {
                if (!wanted.TryGetValue(name, out var ids)) wanted[name] = ids = [];
                ids.Add(id);
                continue;
            }
            using var img = PcxImage.Decode(owner.Read(entry));
            img.SaveAsPng(Path.Combine(args[1], $"{id}.png"));
            saved++;
            fromLod++;
        }
        // Bohaterowie HotA: PCX w zaszyfrowanym HotA.lod — bierzemy załadowane obrazy z pamięci (tylko te, które gra już wczytała).
        var missing = new List<string>();
        if (wanted.Count > 0)
        {
            var found = LoadedPcx.FindAll(mem, wanted.Keys.ToList());
            foreach (var (name, ids) in wanted)
            {
                if (!found.TryGetValue(name, out var img))
                {
                    missing.AddRange(ids.Select(id => $"{id}:{name}"));
                    continue;
                }
                foreach (var id in ids)
                {
                    img.SaveAsPng(Path.Combine(args[1], $"{id}.png"));
                    saved++;
                }
                img.Dispose();
            }
        }
        Console.WriteLine($"portrety: {saved}/{table.Count} (LOD {fromLod} + pamięć {saved - fromLod}) → {args[1]}; brak (niezaładowane w grze): {missing.Count}");
        if (missing.Count > 0) Console.WriteLine("  " + string.Join(" ", missing.Take(40)) + (missing.Count > 40 ? " …" : ""));
        return 0;
    }

    default:
        Console.Error.WriteLine($"nieznana komenda {args[0]}");
        return 1;
}

static DefFile LoadDef(List<LodArchive> lods, string name)
{
    foreach (var lod in lods)
    {
        var entry = lod.Find(name);
        if (entry is not null)
        {
            Console.Error.WriteLine($"[h3assets] {name} z {Path.GetFileName(lod.Path)}");
            return DefFile.Parse(lod.Read(entry));
        }
    }
    throw new FileNotFoundException($"{name} nie ma w żadnym LOD");
}

// ---------------------------------------------------------------------------

static class GameProcess
{
    public static int? FindPid()
    {
        foreach (var dir in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(dir), out var pid)) continue;
            try
            {
                if (File.ReadAllText($"/proc/{pid}/comm").Trim() == "h3hota HD.exe") return pid;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return null;
    }
}

/// <summary>Odczyt pamięci procesu gry przez /proc/pid/mem (jak w readerze).</summary>
sealed class ProcMem : IDisposable
{
    private readonly Microsoft.Win32.SafeHandles.SafeFileHandle _handle;
    public int Pid { get; }

    private ProcMem(int pid, Microsoft.Win32.SafeHandles.SafeFileHandle handle)
    {
        Pid = pid;
        _handle = handle;
    }

    public static ProcMem Open(int pid) =>
        new(pid, File.OpenHandle($"/proc/{pid}/mem", FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

    public bool TryRead(uint address, Span<byte> buffer)
    {
        try
        {
            var total = 0;
            while (total < buffer.Length)
            {
                var n = RandomAccess.Read(_handle, buffer[total..], address + total);
                if (n <= 0) return false;
                total += n;
            }
            return true;
        }
        catch (IOException) { return false; }
    }

    public byte[]? TryRead(uint address, int length)
    {
        var buffer = new byte[length];
        return TryRead(address, buffer) ? buffer : null;
    }

    public int ReadI32(uint address) => BitConverter.ToInt32(TryRead(address, 4) ?? throw new IOException($"0x{address:X8}"));
    public uint ReadU32(uint address) => BitConverter.ToUInt32(TryRead(address, 4) ?? throw new IOException($"0x{address:X8}"));

    public IEnumerable<(uint Start, uint End)> ReadableRegions(bool includeReadOnly = false)
    {
        foreach (var line in File.ReadLines($"/proc/{Pid}/maps"))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            var readable = includeReadOnly ? parts[1].StartsWith('r') : parts[1].StartsWith("rw", StringComparison.Ordinal);
            if (!readable) continue;
            var range = parts[0].Split('-');
            var start = ulong.Parse(range[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var end = ulong.Parse(range[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (start >= 0x1_0000_0000UL) continue;
            yield return ((uint)start, (uint)Math.Min(end, 0xFFFF_FFFFUL));
        }
    }

    /// <summary>Adresy wystąpień napisu ASCII zakończonego zerem (bez rozróżniania wielkości liter).</summary>
    public List<uint> FindAscii(string text, bool includeReadOnly = false)
    {
        var hits = new List<uint>();
        var lower = Encoding.ASCII.GetBytes(text.ToLowerInvariant());
        const int chunk = 4 * 1024 * 1024;
        foreach (var (start, end) in ReadableRegions(includeReadOnly))
        {
            for (var pos = start; pos < end; pos += (uint)(chunk - 64))
            {
                var len = (int)Math.Min(chunk, end - pos);
                var buffer = TryRead(pos, len);
                if (buffer is null) break;
                for (var i = 0; i + lower.Length < len; i++)
                {
                    var match = true;
                    for (var k = 0; k < lower.Length && match; k++)
                    {
                        var b = buffer[i + k];
                        if (b >= 'A' && b <= 'Z') b += 32;
                        match = b == lower[k];
                    }
                    if (match && buffer[i + lower.Length] == 0) hits.Add(pos + (uint)i);
                }
            }
        }
        return hits;
    }

    /// <summary>Wyrównane do 4 wystąpienia 32-bitowej wartości (wskaźnika) w pamięci.</summary>
    public List<uint> FindPointer(uint value, bool includeReadOnly = false)
    {
        var hits = new List<uint>();
        var pattern = BitConverter.GetBytes(value);
        const int chunk = 4 * 1024 * 1024;
        foreach (var (start, end) in ReadableRegions(includeReadOnly))
        {
            for (var pos = start; pos < end; pos += (uint)(chunk - 8))
            {
                var len = (int)Math.Min(chunk, end - pos);
                var buffer = TryRead(pos, len);
                if (buffer is null) break;
                var span = buffer.AsSpan();
                var idx = 0;
                while (idx < len)
                {
                    var rel = span[idx..].IndexOf(pattern);
                    if (rel < 0) break;
                    var address = pos + (uint)(idx + rel);
                    if (address % 4 == 0) hits.Add(address);
                    idx += rel + 1;
                }
            }
        }
        return hits;
    }

    public string ReadCString(uint address, int max)
    {
        var bytes = TryRead(address, max);
        if (bytes is null) return string.Empty;
        var len = Array.IndexOf(bytes, (byte)0);
        return Encoding.Latin1.GetString(bytes, 0, len < 0 ? max : len);
    }

    public void Dispose() => _handle.Dispose();
}

/// <summary>
/// Tablica nazw portretów bohaterów (HotA): wskaźniki LPCSTR w kolejności id obrazka (H3Hero.picture),
/// np. "HPS000Kn.PCX", "HPS001Kn.PCX"… (małe) albo "HPL000Kn.PCX"… (duże). Szukana po napisie pierwszego wpisu
/// i wskaźniku do niego.
/// </summary>
sealed class PortraitTable
{
    public const string SmallSeed = "hps000kn.pcx";
    public const string LargeSeed = "hpl000kn.pcx";

    public uint Base { get; }
    public int Count { get; }

    private PortraitTable(uint @base, int count)
    {
        Base = @base;
        Count = count;
    }

    public static PortraitTable? Locate(ProcMem mem, string seed)
    {
        PortraitTable? best = null;
        foreach (var text in mem.FindAscii(seed, includeReadOnly: true))
        {
            foreach (var reference in mem.FindPointer(text, includeReadOnly: true))
            {
                var count = 0;
                while (count < 1024)
                {
                    var ptr = mem.TryRead(reference + (uint)(4 * count), 4);
                    if (ptr is null) break;
                    var name = mem.ReadCString(BitConverter.ToUInt32(ptr), 16);
                    if (!IsPortraitName(name)) break;
                    count++;
                }
                Console.Error.WriteLine($"[portraits] kandydat @0x{reference:X8}: {count} wpisów");
                if (best is null || count > best.Count) best = new PortraitTable(reference, count);
            }
        }
        return best;
    }

    public string Name(ProcMem mem, int id) => mem.ReadCString(BitConverter.ToUInt32(mem.TryRead(Base + (uint)(4 * id), 4)!), 16);

    private static bool IsPortraitName(string name) =>
        name.Length is >= 8 and <= 16 && name.EndsWith(".pcx", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Załadowany PCX pod HD Modem (H3LoadedPcx): +0x04 name, +0x14 sygnatura "P32F", +0x1C bufSize, +0x24 width, +0x28 height,
/// +0x2C scanline (bajty), +0x30 bufor BGRA 32 bpp. Szukany po nazwie zasobu w regionach rw.
/// </summary>
static class LoadedPcx
{
    private const uint SignatureP32F = 0x46323350;

    /// <summary>Jeden przebieg po pamięci: dla każdej nazwy (bez wielkości liter) pierwszy poprawny H3LoadedPcx "P32F".</summary>
    public static Dictionary<string, Image<Rgba32>> FindAll(ProcMem mem, IReadOnlyCollection<string> names)
    {
        var result = new Dictionary<string, Image<Rgba32>>(StringComparer.OrdinalIgnoreCase);
        var patterns = names.Select(n => (Name: n, Bytes: Encoding.ASCII.GetBytes(n.ToLowerInvariant() + "\0"))).ToList();
        const int chunk = 4 * 1024 * 1024;
        foreach (var (start, end) in mem.ReadableRegions())
        {
            for (var pos = start; pos < end; pos += (uint)(chunk - 64))
            {
                var len = (int)Math.Min(chunk, end - pos);
                var buffer = mem.TryRead(pos, len);
                if (buffer is null) break;
                for (var i = 0; i < len; i++)
                {
                    if (buffer[i] != 'h' && buffer[i] != 'H') continue;
                    foreach (var (name, bytes) in patterns)
                    {
                        if (result.ContainsKey(name) || i + bytes.Length > len) continue;
                        var match = true;
                        for (var k = 0; k < bytes.Length && match; k++)
                        {
                            var b = buffer[i + k];
                            if (b >= 'A' && b <= 'Z') b += 32;
                            match = b == bytes[k];
                        }
                        if (!match) continue;
                        var img = Decode(mem, pos + (uint)i - 4);
                        if (img is not null) result[name] = img;
                    }
                }
            }
        }
        return result;
    }

    /// <summary>H3LoadedPcx pod HD Modem: +0x14 "P32F", +0x24 width, +0x28 height, +0x2C scanline, +0x30 bufor BGRA.</summary>
    private static Image<Rgba32>? Decode(ProcMem mem, uint address)
    {
        var header = mem.TryRead(address, 0x38);
        if (header is null || BitConverter.ToUInt32(header, 0x14) != SignatureP32F) return null;
        var width = BitConverter.ToInt32(header, 0x24);
        var height = BitConverter.ToInt32(header, 0x28);
        var scanline = BitConverter.ToInt32(header, 0x2C);
        var buffer = BitConverter.ToUInt32(header, 0x30);
        if (width is < 1 or > 1024 || height is < 1 or > 1024 || scanline < width * 4 || buffer < 0x10000) return null;
        var raw = mem.TryRead(buffer, scanline * height);
        if (raw is null) return null;
        var img = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var o = y * scanline + x * 4;
                img[x, y] = new Rgba32(raw[o + 2], raw[o + 1], raw[o], 255);
            }
        }
        return img;
    }
}

static class PcxImage
{
    /// <summary>PCX z LOD-a: u32 size, u32 width, u32 height; 8 bpp (paleta 256*3 na końcu) albo 24 bpp BGR.</summary>
    public static Image<Rgba32> Decode(byte[] data)
    {
        var size = BitConverter.ToInt32(data, 0);
        var width = BitConverter.ToInt32(data, 4);
        var height = BitConverter.ToInt32(data, 8);
        var img = new Image<Rgba32>(width, height);
        if (size == width * height)
        {
            var paletteOffset = 12 + size;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var idx = data[12 + y * width + x];
                    img[x, y] = new Rgba32(data[paletteOffset + idx * 3], data[paletteOffset + idx * 3 + 1], data[paletteOffset + idx * 3 + 2], 255);
                }
            }
            return img;
        }
        if (size == width * height * 3)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var o = 12 + (y * width + x) * 3;
                    img[x, y] = new Rgba32(data[o + 2], data[o + 1], data[o], 255);
                }
            }
            return img;
        }
        throw new InvalidDataException($"PCX: size={size} nie pasuje do {width}x{height}");
    }
}

/// <summary>H3LoadedDef z pamięci: +0x04 name[13], +0x1C DefGroup** groups, +0x24 H3Palette888*, +0x28 groupsCount, +0x30/+0x34 wymiary.</summary>
sealed class LoadedDef
{
    public uint Address { get; private init; }
    public int Width { get; private init; }
    public int Height { get; private init; }
    public int GroupsCount { get; private init; }
    public int FrameCount { get; private init; }
    public uint FramesPtr { get; private init; }   // H3DefFrame** grupy 0
    public Rgba32[] Palette { get; private init; } = [];

    public static LoadedDef? TryParse(ProcMem mem, uint address)
    {
        var head = mem.TryRead(address, 0x38);
        if (head is null) return null;
        var groups = BitConverter.ToUInt32(head, 0x1C);
        var palette888 = BitConverter.ToUInt32(head, 0x24);
        var groupsCount = BitConverter.ToInt32(head, 0x28);
        var width = BitConverter.ToInt32(head, 0x30);
        var height = BitConverter.ToInt32(head, 0x34);
        if (groups < 0x10000 || groupsCount < 1 || groupsCount > 64) return null;
        if (width < 1 || width > 2048 || height < 1 || height > 2048) return null;
        var group0 = mem.TryRead(mem.ReadU32(groups), 0x0C);
        if (group0 is null) return null;
        var count = BitConverter.ToInt32(group0, 0);
        var framesPtr = BitConverter.ToUInt32(group0, 8);
        if (count < 1 || count > 4096 || framesPtr < 0x10000) return null;
        // Pod HD Modem palette888 bywa 0 — klatki są wtedy w formacie 4 (gotowe BGRA), paleta niepotrzebna.
        var paletteBytes = palette888 >= 0x10000 ? mem.TryRead(palette888 + 0x1C, 256 * 3) : null; // H3Palette888: nagłówek zasobu 0x1C + RGB[256]
        return new LoadedDef
        {
            Address = address,
            Width = width,
            Height = height,
            GroupsCount = groupsCount,
            FrameCount = count,
            FramesPtr = framesPtr,
            Palette = paletteBytes is null ? [] : DefFile.BuildPalette(paletteBytes, 0),
        };
    }

    /// <summary>Ile klatek ma dane (rawDataSize > 0 i wskaźnik) — gra ładuje klatki leniwie, bywa kilka kopii tego samego DEF-a.</summary>
    public int CountLoadedFrames(ProcMem mem)
    {
        var loaded = 0;
        Span<byte> h = stackalloc byte[0x48];
        for (var i = 0; i < FrameCount; i++)
        {
            var ptr = mem.TryRead(FramesPtr + (uint)(4 * i), 4);
            if (ptr is null) break;
            var framePtr = BitConverter.ToUInt32(ptr);
            if (framePtr < 0x10000 || !mem.TryRead(framePtr, h)) continue;
            if (BitConverter.ToInt32(h[0x1C..]) > 0 && BitConverter.ToUInt32(h[0x44..]) >= 0x10000) loaded++;
        }
        return loaded;
    }

    /// <summary>
    /// H3DefFrame: +0x1C rawDataSize, +0x24 compression, +0x28/+0x2C width/height (pełne), +0x30/+0x34 frameWidth/Height,
    /// +0x38/+0x3C marginesy, +0x40 stride, +0x44 rawData*. Format 0-3 = jak w pliku DEF (indeksy palety),
    /// format 4 = HD Mod: pełna klatka jako BGRA 32 bpp (stride pod +0x40).
    /// </summary>
    public Image<Rgba32>? ReadFrame(ProcMem mem, int index)
    {
        var framePtr = mem.ReadU32(FramesPtr + (uint)(4 * index));
        var h = mem.TryRead(framePtr, 0x48);
        if (h is null) return null;
        var rawSize = BitConverter.ToInt32(h, 0x1C);
        var format = BitConverter.ToUInt32(h, 0x24);
        var fullWidth = BitConverter.ToInt32(h, 0x28);
        var fullHeight = BitConverter.ToInt32(h, 0x2C);
        var width = BitConverter.ToInt32(h, 0x30);
        var height = BitConverter.ToInt32(h, 0x34);
        var left = BitConverter.ToInt32(h, 0x38);
        var top = BitConverter.ToInt32(h, 0x3C);
        var stride = BitConverter.ToInt32(h, 0x40);
        var rawPtr = BitConverter.ToUInt32(h, 0x44);
        if (format > 4 || width < 0 || height < 0 || width > 4096 || height > 4096 || rawSize <= 0 || rawSize > 16 * 1024 * 1024) return null;
        var raw = mem.TryRead(rawPtr, rawSize);
        if (raw is null) return null;

        if (format == 4)
        {
            // BGRA tylko dla prostokąta danych (width×height, stride pod +0x40), wklejany w pełną klatkę na marginesach.
            if (stride < width * 4 || rawSize < stride * height) return null;
            var img = new Image<Rgba32>(Math.Max(1, fullWidth), Math.Max(1, fullHeight), new Rgba32(0, 0, 0, 0));
            for (var y = 0; y < height; y++)
            {
                var ty = y + top;
                if (ty < 0 || ty >= img.Height) continue;
                for (var x = 0; x < width; x++)
                {
                    var tx = x + left;
                    if (tx < 0 || tx >= img.Width) continue;
                    var o = y * stride + x * 4;
                    img[tx, ty] = new Rgba32(raw[o + 2], raw[o + 1], raw[o], raw[o + 3]);
                }
            }
            return img;
        }

        if (Palette.Length == 0) return null;
        var header = new DefFrame($"mem{index}", 0, format, fullWidth, fullHeight, width, height, left, top);
        var pixels = width == 0 || height == 0 ? [] : DefFile.DecodeBody(raw, 0, format, width, height);
        return DefFile.Compose(header, pixels, Palette);
    }
}


// ---------------------------------------------------------------------------

sealed record LodEntry(string Name, uint Offset, uint Size, uint Type, uint CompressedSize);

sealed class LodArchive
{
    public string Path { get; }
    public List<LodEntry> Entries { get; }
    private readonly byte[] _data;

    private LodArchive(string path, byte[] data, List<LodEntry> entries)
    {
        Path = path;
        _data = data;
        Entries = entries;
    }

    public static LodArchive Open(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length < 92 || data[0] != 'L' || data[1] != 'O' || data[2] != 'D')
        {
            return new LodArchive(path, data, []);
        }
        var count = BitConverter.ToUInt32(data, 8);
        var entries = new List<LodEntry>((int)count);
        var pos = 92;
        for (var i = 0; i < count; i++)
        {
            var nameLen = Array.IndexOf(data, (byte)0, pos, 16) - pos;
            if (nameLen < 0) nameLen = 16;
            var name = Encoding.ASCII.GetString(data, pos, nameLen);
            var offset = BitConverter.ToUInt32(data, pos + 16);
            var size = BitConverter.ToUInt32(data, pos + 20);
            var type = BitConverter.ToUInt32(data, pos + 24);
            var csize = BitConverter.ToUInt32(data, pos + 28);
            entries.Add(new LodEntry(name, offset, size, type, csize));
            pos += 32;
        }
        return new LodArchive(path, data, entries);
    }

    public LodEntry? Find(string name) => Entries.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public byte[] Read(LodEntry entry)
    {
        if (entry.CompressedSize == 0)
        {
            return _data.AsSpan((int)entry.Offset, (int)entry.Size).ToArray();
        }
        using var input = new MemoryStream(_data, (int)entry.Offset, (int)entry.CompressedSize);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream((int)entry.Size);
        zlib.CopyTo(output);
        return output.ToArray();
    }
}

sealed record DefBlock(uint Id, List<string> Names, List<uint> Offsets);

sealed record DefFrame(string Name, uint Offset, uint Format, int FullWidth, int FullHeight, int Width, int Height, int LeftMargin, int TopMargin);

sealed class DefFile
{
    public uint Type { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public Rgba32[] Palette { get; init; } = [];
    public List<DefBlock> Blocks { get; init; } = [];
    public List<DefFrame> Frames { get; init; } = [];
    private byte[] _data = [];

    public static DefFile Parse(byte[] data)
    {
        var type = BitConverter.ToUInt32(data, 0);
        var width = BitConverter.ToInt32(data, 4);
        var height = BitConverter.ToInt32(data, 8);
        var blockCount = BitConverter.ToInt32(data, 12);
        var palette = new Rgba32[256];
        for (var i = 0; i < 256; i++)
        {
            palette[i] = new Rgba32(data[16 + i * 3], data[17 + i * 3], data[18 + i * 3], 255);
        }
        // Specjalne indeksy palety H3: 0 przezroczysty, 1-7 cienie/zaznaczenie (tylko gdy kolor jest "magiczny").
        palette[0] = new Rgba32(0, 0, 0, 0);
        byte[] shadowAlpha = [0, 32, 64, 128, 128, 0, 128, 64];
        for (var i = 1; i <= 7; i++)
        {
            if (IsMagic(palette[i])) palette[i] = new Rgba32(0, 0, 0, shadowAlpha[i]);
        }

        var pos = 16 + 256 * 3;
        var blocks = new List<DefBlock>();
        var frames = new List<DefFrame>();
        var seen = new HashSet<uint>();
        for (var b = 0; b < blockCount; b++)
        {
            var id = BitConverter.ToUInt32(data, pos);
            var entries = BitConverter.ToInt32(data, pos + 4);
            pos += 16;
            var names = new List<string>(entries);
            for (var i = 0; i < entries; i++)
            {
                var len = Array.IndexOf(data, (byte)0, pos, 13) - pos;
                if (len < 0) len = 13;
                names.Add(Encoding.ASCII.GetString(data, pos, len));
                pos += 13;
            }
            var offsets = new List<uint>(entries);
            for (var i = 0; i < entries; i++)
            {
                offsets.Add(BitConverter.ToUInt32(data, pos));
                pos += 4;
            }
            blocks.Add(new DefBlock(id, names, offsets));
            for (var i = 0; i < entries; i++)
            {
                var o = (int)offsets[i];
                if (!seen.Add(offsets[i])) continue;
                frames.Add(new DefFrame(
                    names[i], offsets[i],
                    BitConverter.ToUInt32(data, o + 4),
                    BitConverter.ToInt32(data, o + 8), BitConverter.ToInt32(data, o + 12),
                    BitConverter.ToInt32(data, o + 16), BitConverter.ToInt32(data, o + 20),
                    BitConverter.ToInt32(data, o + 24), BitConverter.ToInt32(data, o + 28)));
            }
        }
        return new DefFile { Type = type, Width = width, Height = height, Palette = palette, Blocks = blocks, Frames = frames, _data = data };
    }

    private static bool IsMagic(Rgba32 c) =>
        (c.R, c.G, c.B) is (0, 255, 255) or (255, 150, 255) or (255, 100, 255) or (255, 50, 255) or (255, 0, 255) or (255, 255, 0) or (180, 0, 255) or (0, 255, 0);

    public Image<Rgba32> Render(int index) => Compose(Frames[index], Decode(Frames[index]), Palette);

    public static Image<Rgba32> Compose(DefFrame f, byte[] indices, Rgba32[] palette)
    {
        var img = new Image<Rgba32>(Math.Max(1, f.FullWidth), Math.Max(1, f.FullHeight), new Rgba32(0, 0, 0, 0));
        for (var y = 0; y < f.Height; y++)
        {
            var ty = y + f.TopMargin;
            if (ty < 0 || ty >= img.Height) continue;
            for (var x = 0; x < f.Width; x++)
            {
                var tx = x + f.LeftMargin;
                if (tx < 0 || tx >= img.Width) continue;
                img[tx, ty] = palette[indices[y * f.Width + x]];
            }
        }
        return img;
    }

    /// <summary>Paleta 256×RGB z bajtów + specjalne indeksy H3 (0 przezroczysty, 1-7 cienie gdy kolor "magiczny").</summary>
    public static Rgba32[] BuildPalette(byte[] data, int offset)
    {
        var palette = new Rgba32[256];
        for (var i = 0; i < 256; i++)
        {
            palette[i] = new Rgba32(data[offset + i * 3], data[offset + i * 3 + 1], data[offset + i * 3 + 2], 255);
        }
        palette[0] = new Rgba32(0, 0, 0, 0);
        byte[] shadowAlpha = [0, 32, 64, 128, 128, 0, 128, 64];
        for (var i = 1; i <= 7; i++)
        {
            if (IsMagic(palette[i])) palette[i] = new Rgba32(0, 0, 0, shadowAlpha[i]);
        }
        return palette;
    }

    private byte[] Decode(DefFrame f) => DecodeBody(_data, (int)f.Offset + 32, f.Format, f.Width, f.Height); // dane po 32-bajtowym nagłówku klatki

    /// <summary>Dekoduje piksele (indeksy palety) z ciała klatki DEF; body = offset danych (offsety linii + piksele).</summary>
    public static byte[] DecodeBody(byte[] data, int body, uint format, int width, int height)
    {
        var f = new DefFrame(string.Empty, 0, format, width, height, width, height, 0, 0);
        var pixels = new byte[f.Width * f.Height];
        switch (f.Format)
        {
            case 0:
                Array.Copy(data, body, pixels, 0, pixels.Length);
                break;

            case 1:
                for (var y = 0; y < f.Height; y++)
                {
                    var p = body + (int)BitConverter.ToUInt32(data, body + y * 4);
                    var x = 0;
                    while (x < f.Width)
                    {
                        var code = data[p++];
                        var len = data[p++] + 1;
                        if (code == 0xFF)
                        {
                            Array.Copy(data, p, pixels, y * f.Width + x, Math.Min(len, f.Width - x));
                            p += len;
                        }
                        else
                        {
                            Array.Fill(pixels, code, y * f.Width + x, Math.Min(len, f.Width - x));
                        }
                        x += len;
                    }
                }
                break;

            case 2:
                for (var y = 0; y < f.Height; y++)
                {
                    var p = body + BitConverter.ToUInt16(data, body + y * 2);
                    DecodeSegments(data, ref p, pixels, y * f.Width, f.Width);
                }
                break;

            case 3:
                for (var y = 0; y < f.Height; y++)
                {
                    var chunks = f.Width / 32;
                    for (var c = 0; c < chunks; c++)
                    {
                        var p = body + BitConverter.ToUInt16(data, body + (y * chunks + c) * 2);
                        DecodeSegments(data, ref p, pixels, y * f.Width + c * 32, 32);
                    }
                }
                break;

            default:
                throw new InvalidDataException($"nieznany format klatki {f.Format}");
        }
        return pixels;
    }

    /// <summary>Segmenty fmt 2/3: górne 3 bity = kod (7 = surowe piksele), dolne 5 bitów + 1 = długość.</summary>
    private static void DecodeSegments(byte[] data, ref int p, byte[] pixels, int dest, int count)
    {
        var x = 0;
        while (x < count)
        {
            var segment = data[p++];
            var code = segment >> 5;
            var len = (segment & 0x1F) + 1;
            var n = Math.Min(len, count - x);
            if (code == 7)
            {
                Array.Copy(data, p, pixels, dest + x, n);
                p += len;
            }
            else
            {
                Array.Fill(pixels, (byte)code, dest + x, n);
            }
            x += len;
        }
    }
}
