#:project H3Assets/H3Assets.csproj
#:property LangVersion=preview
#:property Nullable=enable
#:property PublishAot=false
#:property TreatWarningsAsErrors=true

// Exports the Heroes III bitmap fonts from Data/h3bitmap.lod to overlay/assets/fonts/ as a
// white, alpha-only atlas PNG plus a JSON glyph table (design spec section 3 "Fonts").
//
//   dotnet run h3fonts.cs -- export
//   dotnet run h3fonts.cs -- render <font> <text> <out.png>   # verification render, PNG+JSON only

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using H3Assets;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

const string GameDir = "/home/smyrcu/Games/Heroic/Heroes of Might and Magic III - Horn of the Abyss";
const string BitmapLodPath = "Data/h3bitmap.lod";
const int AtlasMaxWidth = 512;

(string LodName, string OutName)[] fonts =
[
    ("bigfont.fnt", "bigfont"),
    ("MedFont.fnt", "medfont"),
    ("smalfont.fnt", "smalfont"),
    ("tiny.fnt", "tiny"),
    ("verd10b.fnt", "verd10b"),
    ("CALLI10R.FNT", "calli10r"),
];

var fontsDir = Path.Combine(RepoRoot(), "overlay", "assets", "fonts");

if (args.Length == 0 || args[0] == "export")
{
    var lod = LodArchive.Open(Path.Combine(GameDir, BitmapLodPath));
    Directory.CreateDirectory(fontsDir);
    foreach (var (lodName, outName) in fonts)
    {
        var font = BitmapFont.Parse(lod.Read(lod.Find(lodName)));
        ExportFont(font, outName, fontsDir);
        Console.WriteLine($"{lodName} -> {outName}.png + {outName}.json (lineHeight={font.LineHeight})");
    }

    return 0;
}

if (args[0] == "render" && args.Length == 4)
{
    RenderText(fontsDir, args[1], args[2], args[3]);
    return 0;
}

Console.Error.WriteLine("usage: export | render <font> <text> <out.png>");
return 1;

static void ExportFont(BitmapFont font, string name, string outDir)
{
    var placements = new (int X, int Y)[256];
    var cursorX = 0;
    var cursorY = 0;
    var atlasWidth = 0;
    for (var code = 0; code < 256; code++)
    {
        var glyph = font.Glyphs[code];
        if (glyph.Width == 0)
        {
            continue;
        }

        if (cursorX + glyph.Width > AtlasMaxWidth)
        {
            cursorX = 0;
            cursorY += font.LineHeight;
        }

        placements[code] = (cursorX, cursorY);
        atlasWidth = Math.Max(atlasWidth, cursorX + (int)glyph.Width);
        cursorX += (int)glyph.Width;
    }

    var atlasHeight = cursorY + font.LineHeight;
    using var atlas = new Image<Rgba32>(Math.Max(1, atlasWidth), Math.Max(1, atlasHeight));

    var glyphsJson = new Dictionary<string, GlyphJson>();
    for (var code = 0; code < 256; code++)
    {
        var glyph = font.Glyphs[code];
        var key = code.ToString(CultureInfo.InvariantCulture);
        if (glyph.Width == 0)
        {
            glyphsJson[key] = new GlyphJson(0, 0, 0, 0, glyph.Left, glyph.Advance);
            continue;
        }

        var (x, y) = placements[code];
        var alphas = font.GlyphPixels(code);
        for (var row = 0; row < font.LineHeight; row++)
        {
            for (var col = 0; col < glyph.Width; col++)
            {
                atlas[x + col, y + row] = new Rgba32(255, 255, 255, alphas[row * (int)glyph.Width + col]);
            }
        }

        glyphsJson[key] = new GlyphJson(x, y, (int)glyph.Width, font.LineHeight, glyph.Left, glyph.Advance);
    }

    atlas.SaveAsPng(Path.Combine(outDir, $"{name}.png"));
    var fontJson = new FontJson(name, font.LineHeight, glyphsJson);
    File.WriteAllText(Path.Combine(outDir, $"{name}.json"), JsonSerializer.Serialize(fontJson));
}

static void RenderText(string fontsDir, string fontName, string text, string outPath)
{
    var fontJson = JsonSerializer.Deserialize<FontJson>(File.ReadAllText(Path.Combine(fontsDir, $"{fontName}.json")))
        ?? throw new InvalidDataException($"could not parse {fontName}.json");
    using var atlas = Image.Load<Rgba32>(Path.Combine(fontsDir, $"{fontName}.png"));

    const int padding = 4;
    var glyphs = new List<GlyphJson?>(text.Length);
    foreach (var c in text)
    {
        var key = ((byte)c).ToString(CultureInfo.InvariantCulture);
        glyphs.Add(fontJson.Glyphs.TryGetValue(key, out var glyph) ? glyph : null);
    }

    var width = padding * 2 + glyphs.Sum(g => g?.Advance ?? 0);
    var height = padding * 2 + fontJson.LineHeight;

    using var canvas = new Image<Rgba32>(Math.Max(1, width), Math.Max(1, height), new Rgba32(24, 24, 32, 255));
    var penX = padding;
    foreach (var glyph in glyphs)
    {
        if (glyph is { W: > 0, H: > 0 })
        {
            BlitGlyph(atlas, canvas, glyph, penX + glyph.Left, padding);
        }

        penX += glyph?.Advance ?? 0;
    }

    canvas.SaveAsPng(outPath);
    Console.WriteLine($"{fontName} \"{text}\" ({width}x{height}) -> {outPath}");
}

static void BlitGlyph(Image<Rgba32> atlas, Image<Rgba32> canvas, GlyphJson glyph, int destX, int destY)
{
    for (var row = 0; row < glyph.H; row++)
    {
        var y = destY + row;
        if (y < 0 || y >= canvas.Height)
        {
            continue;
        }

        for (var col = 0; col < glyph.W; col++)
        {
            var x = destX + col;
            if (x < 0 || x >= canvas.Width)
            {
                continue;
            }

            var source = atlas[glyph.X + col, glyph.Y + row];
            if (source.A == 0)
            {
                continue;
            }

            canvas[x, y] = AlphaBlend(canvas[x, y], source);
        }
    }
}

static Rgba32 AlphaBlend(Rgba32 background, Rgba32 foreground)
{
    var a = foreground.A / 255f;
    var r = (byte)((foreground.R * a) + (background.R * (1 - a)));
    var g = (byte)((foreground.G * a) + (background.G * (1 - a)));
    var b = (byte)((foreground.B * a) + (background.B * (1 - a)));
    return new Rgba32(r, g, b, 255);
}

static string RepoRoot([CallerFilePath] string sourcePath = "")
{
    return Directory.GetParent(Path.GetDirectoryName(sourcePath)!)!.FullName;
}

sealed record GlyphJson(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("w")] int W,
    [property: JsonPropertyName("h")] int H,
    [property: JsonPropertyName("left")] int Left,
    [property: JsonPropertyName("advance")] int Advance);

sealed record FontJson(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("lineHeight")] int LineHeight,
    [property: JsonPropertyName("glyphs")] Dictionary<string, GlyphJson> Glyphs);
