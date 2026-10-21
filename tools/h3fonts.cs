#:project H3Assets/H3Assets.csproj
#:property LangVersion=preview
#:property Nullable=enable
#:property PublishAot=false
#:property TreatWarningsAsErrors=true

// Exports the Heroes III bitmap fonts to overlay/assets/fonts/ as a white, alpha-only atlas PNG
// plus a JSON glyph table keyed by Unicode code point (design spec section 3 "Fonts"). Each atlas
// is the English game's font from Data/h3bitmap.lod (Windows-1252) with the Cyrillic letters of
// the Russian release's font added, which the HD Mod carries in _HD3_Data/Common/Rus.Fonts
// (Windows-1251).
//
//   dotnet run h3fonts.cs -- export
//   dotnet run h3fonts.cs -- render <font> <text> <out.png>   # verification render, PNG+JSON only

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using H3Assets;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

const string GameDir = "/home/smyrcu/Games/Heroic/Heroes of Might and Magic III - Horn of the Abyss";
const string BitmapLodPath = "Data/h3bitmap.lod";
const string CyrillicFontsPath = "_HD3_Data/Common/Rus.Fonts";
const int WesternCodePage = 1252;
const int CyrillicCodePage = 1251;
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
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    var lod = LodArchive.Open(Path.Combine(GameDir, BitmapLodPath));
    Directory.CreateDirectory(fontsDir);
    foreach (var (lodName, outName) in fonts)
    {
        var western = BitmapFont.Parse(lod.Read(lod.Find(lodName)));
        var cyrillic = BitmapFont.Parse(File.ReadAllBytes(Path.Combine(GameDir, CyrillicFontsPath, lodName)));
        var glyphs = GlyphSources(western, cyrillic);
        ExportFont(western.LineHeight, glyphs, outName, fontsDir);
        Console.WriteLine($"{lodName} -> {outName}.png + {outName}.json (lineHeight={western.LineHeight}, {glyphs.Count} glyphs)");
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

// Every glyph of the western font under the code point its byte stands for in Windows-1252, then
// the Russian font's upper half under its Windows-1251 code points where the western font has no
// glyph of its own. The Russian font is a separate design with a shorter line, so its glyphs are
// lowered onto the western baseline rather than hung from the top of the line.
static SortedDictionary<int, GlyphSource> GlyphSources(BitmapFont western, BitmapFont cyrillic)
{
    var sources = new SortedDictionary<int, GlyphSource>();
    AddCodePage(sources, western, WesternCodePage, 0, 0);

    var shift = Baseline(western) - Baseline(cyrillic);
    if (shift < 0 || cyrillic.LineHeight + shift > western.LineHeight)
    {
        throw new InvalidDataException(
            $"the Russian font (line {cyrillic.LineHeight}) does not fit the western line ({western.LineHeight}) lowered by {shift}");
    }

    AddCodePage(sources, cyrillic, CyrillicCodePage, 0x80, shift);
    return sources;
}

static void AddCodePage(SortedDictionary<int, GlyphSource> sources, BitmapFont font, int codePage, int firstCode, int shift)
{
    var encoding = Encoding.GetEncoding(codePage);
    for (var code = firstCode; code < 256; code++)
    {
        // A byte the code page leaves undefined comes back as a C1 control: nothing to draw.
        var character = encoding.GetString([(byte)code])[0];
        if (char.IsControl(character))
        {
            continue;
        }

        if (sources.TryGetValue(character, out var held) && (held.Metrics.Width > 0 || character == ' '))
        {
            continue;
        }

        sources[character] = new GlyphSource(font, code, shift);
    }
}

// The row the capital H stands on: the one line both designs agree on.
static int Baseline(BitmapFont font)
{
    var width = (int)font.Glyphs['H'].Width;
    var pixels = font.GlyphPixels('H');
    for (var row = font.LineHeight - 1; row >= 0; row--)
    {
        if (pixels.Slice(row * width, width).ContainsAnyExcept((byte)0))
        {
            return row;
        }
    }

    throw new InvalidDataException("the font's H has no pixels");
}

static void ExportFont(int lineHeight, SortedDictionary<int, GlyphSource> sources, string name, string outDir)
{
    var placements = new Dictionary<int, (int X, int Y)>();
    var cursorX = 0;
    var cursorY = 0;
    var atlasWidth = 0;
    foreach (var (codePoint, source) in sources)
    {
        var width = (int)source.Metrics.Width;
        if (width == 0)
        {
            continue;
        }

        if (cursorX + width > AtlasMaxWidth)
        {
            cursorX = 0;
            cursorY += lineHeight;
        }

        placements[codePoint] = (cursorX, cursorY);
        atlasWidth = Math.Max(atlasWidth, cursorX + width);
        cursorX += width;
    }

    var atlasHeight = cursorY + lineHeight;
    using var atlas = new Image<Rgba32>(Math.Max(1, atlasWidth), Math.Max(1, atlasHeight));

    var glyphsJson = new Dictionary<string, GlyphJson>();
    foreach (var (codePoint, source) in sources)
    {
        var glyph = source.Metrics;
        var key = codePoint.ToString(CultureInfo.InvariantCulture);
        if (glyph.Width == 0)
        {
            glyphsJson[key] = new GlyphJson(0, 0, 0, 0, glyph.Left, glyph.Advance);
            continue;
        }

        var (x, y) = placements[codePoint];
        var alphas = source.Font.GlyphPixels(source.Code);
        for (var row = 0; row < source.Font.LineHeight; row++)
        {
            for (var col = 0; col < glyph.Width; col++)
            {
                atlas[x + col, y + source.Shift + row] = new Rgba32(255, 255, 255, alphas[row * (int)glyph.Width + col]);
            }
        }

        glyphsJson[key] = new GlyphJson(x, y, (int)glyph.Width, lineHeight, glyph.Left, glyph.Advance);
    }

    atlas.SaveAsPng(Path.Combine(outDir, $"{name}.png"));
    var fontJson = new FontJson(name, lineHeight, glyphsJson);
    File.WriteAllText(Path.Combine(outDir, $"{name}.json"), JsonSerializer.Serialize(fontJson));
}

static void RenderText(string fontsDir, string fontName, string text, string outPath)
{
    var fontJson = JsonSerializer.Deserialize<FontJson>(File.ReadAllText(Path.Combine(fontsDir, $"{fontName}.json")))
        ?? throw new InvalidDataException($"could not parse {fontName}.json");
    using var atlas = Image.Load<Rgba32>(Path.Combine(fontsDir, $"{fontName}.png"));

    const int padding = 4;
    var glyphs = new List<GlyphJson?>(text.Length);
    foreach (var rune in text.EnumerateRunes())
    {
        var key = rune.Value.ToString(CultureInfo.InvariantCulture);
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

sealed record GlyphSource(BitmapFont Font, int Code, int Shift)
{
    public GlyphMetrics Metrics => Font.Glyphs[Code];
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
