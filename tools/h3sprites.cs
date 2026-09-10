#:project H3Assets/H3Assets.csproj
#:property LangVersion=preview
#:property Nullable=enable
#:property PublishAot=false
#:property TreatWarningsAsErrors=true

// DEF sprite explorer/exporter for the plaintext archives, used to regenerate individual entries
// under overlay/assets/ once the source resource name is known. HotA.lod is deliberately absent:
// its directory stores name hashes rather than names, so it can neither be listed nor read by
// LodArchive - use h3lodmem.cs for anything HotA replaces.
//
//   dotnet run h3sprites.cs -- list <pattern>              # resource names matching pattern
//   dotnet run h3sprites.cs -- frames <name.def> <outdir>  # one PNG per frame, <outdir>/<index>.png

using H3Assets;
using SixLabors.ImageSharp;

const string GameDir = "/home/smyrcu/Games/Heroic/Heroes of Might and Magic III - Horn of the Abyss";
(string Path, string Label)[] archiveFiles =
[
    ("Data/h3sprite.lod", "h3sprite.lod"),
    ("Data/h3bitmap.lod", "h3bitmap.lod"),
];

switch (args)
{
    case ["list", var pattern]:
        return List(pattern);
    case ["frames", var name, var outDir]:
        return ExportFrames(name, outDir);
    default:
        Console.Error.WriteLine("usage: list <pattern> | frames <name.def> <outdir>");
        return 1;
}

int List(string pattern)
{
    foreach (var (path, label) in archiveFiles)
    {
        var archive = LodArchive.Open(Path.Combine(GameDir, path));
        foreach (var entry in archive.Entries.Where(e => e.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase)).OrderBy(e => e.Name))
        {
            Console.WriteLine($"{label,-12} {entry.Name}");
        }
    }

    return 0;
}

int ExportFrames(string name, string outDir)
{
    var def = LoadDef(name);
    Directory.CreateDirectory(outDir);

    var exported = 0;
    for (var i = 0; i < def.Frames.Count; i++)
    {
        try
        {
            using var frame = def.Render(i);
            frame.SaveAsPng(Path.Combine(outDir, $"{i}.png"));
            exported++;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"skipping frame {i}: {ex.Message}");
        }
    }

    Console.WriteLine($"{name}: {exported}/{def.Frames.Count} frames -> {outDir}");
    return 0;
}

DefFile LoadDef(string name)
{
    foreach (var (path, label) in archiveFiles)
    {
        var archive = LodArchive.Open(Path.Combine(GameDir, path));
        var entry = archive.TryFind(name);
        if (entry is not null)
        {
            Console.Error.WriteLine($"[h3sprites] {name} from {label}");
            return DefFile.Parse(archive.Read(entry));
        }
    }

    throw new FileNotFoundException($"{name} is not in {string.Join(", ", archiveFiles.Select(a => a.Label))}");
}
