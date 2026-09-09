#:project H3Assets/H3Assets.csproj
#:property LangVersion=preview
#:property Nullable=enable
#:property PublishAot=false
#:property TreatWarningsAsErrors=true

// Exports the hero and town popup backgrounds from Data/h3bitmap.lod to overlay/assets/ui/
// (design spec section 3 "Cards": HEROQVBK.PCX / TOWNQVBK.PCX, 194x186).
//
//   dotnet run h3popups.cs

using System.Runtime.CompilerServices;
using H3Assets;
using SixLabors.ImageSharp;

const string GameDir = "/home/smyrcu/Games/Heroic/Heroes of Might and Magic III - Horn of the Abyss";
const string BitmapLodPath = "Data/h3bitmap.lod";
const int ExpectedWidth = 194;
const int ExpectedHeight = 186;

(string LodName, string OutName)[] popups =
[
    ("HEROQVBK.PCX", "popup-hero"),
    ("TOWNQVBK.PCX", "popup-town"),
];

var uiDir = Path.Combine(RepoRoot(), "overlay", "assets", "ui");
Directory.CreateDirectory(uiDir);

var lod = LodArchive.Open(Path.Combine(GameDir, BitmapLodPath));
foreach (var (lodName, outName) in popups)
{
    using var image = PcxImage.Decode(lod.Read(lod.Find(lodName)));
    if (image.Width != ExpectedWidth || image.Height != ExpectedHeight)
    {
        throw new InvalidDataException($"{lodName}: expected {ExpectedWidth}x{ExpectedHeight}, got {image.Width}x{image.Height}");
    }

    var outPath = Path.Combine(uiDir, $"{outName}.png");
    image.SaveAsPng(outPath);
    Console.WriteLine($"{lodName} -> {outName}.png ({image.Width}x{image.Height})");
}

return 0;

static string RepoRoot([CallerFilePath] string sourcePath = "")
{
    return Directory.GetParent(Path.GetDirectoryName(sourcePath)!)!.FullName;
}
