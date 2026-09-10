#:project H3Assets/H3Assets.csproj
#:property LangVersion=preview
#:property Nullable=enable
#:property PublishAot=false
#:property TreatWarningsAsErrors=true

// Exporter for the resources HotA adds on top of the SoD archives.
//
// HotA.lod's directory is obfuscated but its payloads are not, so everything is read straight from
// the file on disk (see H3Assets/HotaLodArchive.cs for the format). The one exception is the
// hero picture id -> portrait name table, which the archive cannot supply: HotA.lod stores names
// only as hashes, and the ordering by picture id exists nowhere in it. That table is extracted once
// from a running game by `names` and kept in hero-portraits.tsv, which every later export reads.
//
//   dotnet run h3lodmem.cs -- verify                        # prove the HotA.lod directory decodes
//   dotnet run h3lodmem.cs -- names                         # refresh hero-portraits.tsv (game must run)
//   dotnet run h3lodmem.cs -- portraits                     # overlay/assets/heroes[/large]/<id>.png
//   dotnet run h3lodmem.cs -- export <name> <out.png>       # one bitmap resource by name
//   dotnet run h3lodmem.cs -- frames <name.def> <outdir> [--first-id N]
//   dotnet run h3lodmem.cs -- sheet <dir> <out.png> <firstId> <lastId> [cols]

using System.Globalization;
using System.Runtime.CompilerServices;
using H3Assets;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

const string GameDir = "/home/smyrcu/Games/Heroic/Heroes of Might and Magic III - Horn of the Abyss";
const int SmallWidth = 48;
const int SmallHeight = 32;
const int LargeWidth = 58;
const int LargeHeight = 64;
const string PortraitNameFile = "hero-portraits.tsv";

try
{
    return args switch
    {
        ["verify"] => Verify(),
        ["names"] => Names(),
        ["portraits"] => Portraits(),
        ["export", var name, var outPath] => Export(name, outPath),
        ["frames", var name, var outDir] => Frames(name, outDir, 0),
        ["frames", var name, var outDir, "--first-id", var first] => Frames(name, outDir, Number(first)),
        ["sheet", var dir, var outPath, var first, var last] => Sheet(dir, outPath, Number(first), Number(last), 10),
        ["sheet", var dir, var outPath, var first, var last, var cols] => Sheet(dir, outPath, Number(first), Number(last), Number(cols)),
        _ => Usage(),
    };
}
catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or IOException or FormatException)
{
    Console.Error.WriteLine(ex.Message);
    return 8;
}

static int Number(string value) => int.Parse(value, CultureInfo.InvariantCulture);

static int Usage()
{
    Console.Error.WriteLine("usage: verify | names | portraits | export <name> <out.png> | frames <name.def> <outdir> [--first-id N] | sheet <dir> <out.png> <firstId> <lastId> [cols]");
    return 1;
}

static HotaLodArchive OpenHota() => HotaLodArchive.Open(Path.Combine(GameDir, "Data/HotA.lod"));

// The game resolves a name against HotA.lod first, so HotA's replacements win over the SoD originals.
static List<IResourceArchive> OpenArchives() =>
[
    OpenHota(),
    LodArchive.Open(Path.Combine(GameDir, "Data/h3sprite.lod")),
    LodArchive.Open(Path.Combine(GameDir, "Data/h3bitmap.lod")),
];

static byte[]? TryReadResource(List<IResourceArchive> archives, string name, out string source)
{
    foreach (var archive in archives)
    {
        var data = archive.TryRead(name);
        if (data is not null)
        {
            source = archive.Label;
            return data;
        }
    }

    source = string.Empty;
    return null;
}

static byte[] ReadResource(List<IResourceArchive> archives, string name, out string source)
{
    return TryReadResource(archives, name, out source)
        ?? throw new FileNotFoundException($"{name} is in none of {string.Join(", ", archives.Select(a => a.Label))}");
}

static int Verify()
{
    var archive = OpenHota();
    Console.WriteLine($"{archive.Label}: {archive.Entries.Count} entries, key 0x{archive.Key:X8}, data starts at 0x{archive.DataStart:X}");

    // The directory is trustworthy only if its entries tile the payload region exactly: each one
    // starts where the previous ended, the first at the end of the directory, the last at EOF.
    var chained = 0;
    var expected = archive.DataStart;
    foreach (var entry in archive.Entries)
    {
        if (entry.Offset != expected)
        {
            break;
        }

        expected += entry.CompressedSize == 0 ? entry.Size : entry.CompressedSize;
        chained++;
    }

    Console.WriteLine($"contiguous entries: {chained}/{archive.Entries.Count}; chain ends at 0x{expected:X}, file ends at 0x{archive.Length:X}");

    var inflated = 0;
    var stored = 0;
    var failures = new List<string>();
    foreach (var entry in archive.Entries)
    {
        if (entry.CompressedSize == 0)
        {
            stored++;
            continue;
        }

        try
        {
            if (archive.Read(entry).Length == entry.Size)
            {
                inflated++;
            }
            else
            {
                failures.Add($"{entry.NameHash:X8}: inflated length != {entry.Size}");
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            failures.Add($"{entry.NameHash:X8}: {ex.Message}");
        }
    }

    var distinctHashes = archive.Entries.Select(e => e.NameHash).Distinct().Count();
    Console.WriteLine($"name hashes: {distinctHashes} distinct of {archive.Entries.Count}");
    Console.WriteLine($"payloads: {inflated} inflated to their declared size, {stored} stored verbatim, {failures.Count} failed");
    foreach (var failure in failures.Take(10))
    {
        Console.Error.WriteLine($"  {failure}");
    }

    var exact = chained == archive.Entries.Count
        && expected == archive.Length
        && failures.Count == 0
        && distinctHashes == archive.Entries.Count;
    Console.WriteLine(exact
        ? "directory decodes exactly: every entry accounted for, no payload is encrypted"
        : "directory does NOT decode exactly - see the counts above");
    return exact ? 0 : 2;
}

static int Names()
{
    var pid = ProcessMemory.FindGamePid();
    if (pid is null)
    {
        Console.Error.WriteLine("the game is not running; the portrait name table can only be read from it");
        return 3;
    }

    using var memory = ProcessMemory.Open(pid.Value);
    var small = PortraitTable.Locate(memory, PortraitTable.SmallSeed);
    var large = PortraitTable.Locate(memory, PortraitTable.LargeSeed);
    if (small is null || large is null)
    {
        Console.Error.WriteLine("portrait name tables not found in the running game");
        return 4;
    }

    if (small.Names.Count != large.Names.Count)
    {
        Console.Error.WriteLine($"the two tables disagree: {small.Names.Count} small names, {large.Names.Count} large");
        return 4;
    }

    var path = ToolPath(PortraitNameFile);
    File.WriteAllLines(path, small.Names.Select((name, id) => $"{id}\t{name}\t{large.Names[id]}"));
    Console.WriteLine($"{small.Names.Count} portrait names -> {path}");
    return 0;
}

static List<(int Id, string Small, string Large)> ReadPortraitNames()
{
    var path = ToolPath(PortraitNameFile);
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"{path} is missing; run `names` while the game is running to create it");
    }

    var rows = new List<(int, string, string)>();
    foreach (var line in File.ReadLines(path))
    {
        if (line.Length == 0)
        {
            continue;
        }

        var fields = line.Split('\t');
        if (fields.Length != 3)
        {
            throw new InvalidDataException($"{path}: expected 'id<TAB>small<TAB>large', got '{line}'");
        }

        rows.Add((Number(fields[0]), fields[1], fields[2]));
    }

    return rows;
}

static int Portraits()
{
    var names = ReadPortraitNames();
    var archives = OpenArchives();
    var failed = false;

    foreach (var (directory, width, height, pick) in new (string, int, int, Func<(int Id, string Small, string Large), string>)[]
             {
                 ("heroes", SmallWidth, SmallHeight, row => row.Small),
                 ("heroes/large", LargeWidth, LargeHeight, row => row.Large),
             })
    {
        var outDir = AssetPath(directory);
        Directory.CreateDirectory(outDir);
        int written = 0, unchanged = 0, replaced = 0, added = 0, wrongSize = 0;
        var missing = new List<string>();
        var sources = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var row in names)
        {
            var name = pick(row);
            var data = TryReadResource(archives, name, out var source);
            if (data is null)
            {
                missing.Add($"{row.Id}:{name}");
                continue;
            }

            sources[source] = sources.GetValueOrDefault(source) + 1;
            using var image = BitmapResource.Decode(data);
            if (image.Width != width || image.Height != height)
            {
                wrongSize++;
                Console.Error.WriteLine($"  {row.Id} {name}: {image.Width}x{image.Height}, expected {width}x{height}");
            }

            var path = Path.Combine(outDir, $"{row.Id}.png");
            var before = File.Exists(path) ? File.ReadAllBytes(path) : null;
            image.SaveAsPng(path);
            written++;
            if (before is null)
            {
                added++;
            }
            else if (before.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
            {
                unchanged++;
            }
            else
            {
                replaced++;
            }
        }

        var sourceList = string.Join(", ", sources.OrderByDescending(s => s.Value).Select(s => $"{s.Key} {s.Value}"));
        Console.WriteLine($"{directory}: {written}/{names.Count} written ({sourceList}); unchanged {unchanged}, replaced {replaced}, new {added}, unexpected size {wrongSize}, missing {missing.Count}");
        if (missing.Count > 0)
        {
            Console.WriteLine("  missing: " + string.Join(' ', missing));
            failed = true;
        }
    }

    return failed ? 5 : 0;
}

static int Export(string name, string outPath)
{
    var data = ReadResource(OpenArchives(), name, out var source);
    using var image = BitmapResource.Decode(data);
    image.SaveAsPng(outPath);
    Console.WriteLine($"{name} from {source}: {image.Width}x{image.Height} -> {outPath}");
    return 0;
}

static int Frames(string name, string outDir, int firstId)
{
    var data = ReadResource(OpenArchives(), name, out var source);
    var sheet = SpriteSheet.Parse(data);
    Directory.CreateDirectory(outDir);

    int written = 0, unchanged = 0, replaced = 0, added = 0, skipped = 0, failed = 0;
    for (var i = 0; i < sheet.FrameCount; i++)
    {
        var id = i + firstId;
        if (id < 0)
        {
            // The first frames of some sheets are placeholders that sit before game id 0.
            skipped++;
            continue;
        }

        try
        {
            using var frame = sheet.Render(i);
            var path = Path.Combine(outDir, $"{id}.png");
            var before = File.Exists(path) ? File.ReadAllBytes(path) : null;
            frame.SaveAsPng(path);
            written++;
            if (before is null)
            {
                added++;
            }
            else if (before.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
            {
                unchanged++;
            }
            else
            {
                replaced++;
            }
        }
        catch (InvalidDataException ex)
        {
            failed++;
            Console.Error.WriteLine($"skipping frame {i}: {ex.Message}");
        }
    }

    Console.WriteLine($"{name} from {source}: {written}/{sheet.FrameCount} frames -> {outDir} as id {firstId}..{sheet.FrameCount - 1 + firstId}; unchanged {unchanged}, replaced {replaced}, new {added}, before id 0 {skipped}, failed {failed}");
    return failed == 0 ? 0 : 7;
}

static int Sheet(string dir, string outPath, int firstId, int lastId, int columns)
{
    const int padding = 4;
    if (columns < 1 || lastId < firstId)
    {
        Console.Error.WriteLine($"columns must be at least 1 and {firstId}..{lastId} must be a forward range");
        return 6;
    }

    var tiles = Enumerable.Range(firstId, lastId - firstId + 1)
        .Select(id => Path.Combine(dir, $"{id}.png"))
        .Where(File.Exists)
        .ToList();
    if (tiles.Count == 0)
    {
        Console.Error.WriteLine($"no {firstId}..{lastId}.png under {dir}");
        return 6;
    }

    using var first = Image.Load<Rgba32>(tiles[0]);
    var cellWidth = first.Width + padding;
    var cellHeight = first.Height + padding;
    var rows = (tiles.Count + columns - 1) / columns;

    using var sheet = new Image<Rgba32>((columns * cellWidth) + padding, (rows * cellHeight) + padding);
    sheet.Mutate(context => context.BackgroundColor(Color.FromRgb(24, 20, 16)));
    for (var i = 0; i < tiles.Count; i++)
    {
        using var tile = Image.Load<Rgba32>(tiles[i]);
        var x = padding + (i % columns * cellWidth);
        var y = padding + (i / columns * cellHeight);
        sheet.Mutate(context => context.DrawImage(tile, new Point(x, y), 1f));
    }

    sheet.SaveAsPng(outPath);
    Console.WriteLine($"contact sheet: {tiles.Count} tiles from {firstId}..{lastId}, {columns} columns -> {outPath}");
    return 0;
}

static string ToolPath(string relative, [CallerFilePath] string scriptPath = "")
{
    return Path.Combine(Path.GetDirectoryName(scriptPath)!, relative);
}

static string AssetPath(string relative, [CallerFilePath] string scriptPath = "")
{
    var repositoryRoot = Path.GetDirectoryName(Path.GetDirectoryName(scriptPath))!;
    return Path.Combine(repositoryRoot, "overlay/assets", relative);
}
