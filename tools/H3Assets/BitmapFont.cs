namespace H3Assets;

public sealed record GlyphMetrics(int Left, uint Width, int Right, uint PixelOffset)
{
    public int Advance => Left + (int)Width + Right;
}

/// <summary>
/// Reads the Heroes III bitmap ".fnt" format (VCMI CBitmapFont): line height at byte 5,
/// 256 x (int32 left, uint32 width, int32 right) metrics from byte 32, 256 x uint32 pixel
/// offsets right after, then a pixel blob (one byte per pixel, used verbatim as alpha) whose
/// base is fixed at byte 4128 regardless of file size.
/// </summary>
public sealed class BitmapFont
{
    private const int CharCount = 256;
    private const int MetricsBase = 32;
    private const int MetricsEntrySize = 12;
    private const int OffsetsBase = MetricsBase + (CharCount * MetricsEntrySize);
    private const int PixelsBase = OffsetsBase + (CharCount * sizeof(uint));

    private readonly byte[] _data;

    private BitmapFont(byte height, IReadOnlyList<GlyphMetrics> glyphs, byte[] data)
    {
        LineHeight = height;
        Glyphs = glyphs;
        _data = data;
    }

    public byte LineHeight { get; }

    public IReadOnlyList<GlyphMetrics> Glyphs { get; }

    public static BitmapFont Parse(byte[] data)
    {
        if (data.Length < PixelsBase)
        {
            throw new InvalidDataException($"font file too short: {data.Length} bytes, expected at least {PixelsBase}");
        }

        var height = data[5];
        var glyphs = new GlyphMetrics[CharCount];
        for (var code = 0; code < CharCount; code++)
        {
            var metricsOffset = MetricsBase + (code * MetricsEntrySize);
            var left = BitConverter.ToInt32(data, metricsOffset);
            var width = BitConverter.ToUInt32(data, metricsOffset + 4);
            var right = BitConverter.ToInt32(data, metricsOffset + 8);
            var pixelOffset = BitConverter.ToUInt32(data, OffsetsBase + (code * sizeof(uint)));

            var glyphEnd = PixelsBase + pixelOffset + (width * (long)height);
            if (glyphEnd > data.Length)
            {
                throw new InvalidDataException($"glyph {code}: pixel data runs past end of file ({glyphEnd} > {data.Length})");
            }

            glyphs[code] = new GlyphMetrics(left, width, right, pixelOffset);
        }

        return new BitmapFont(height, glyphs, data);
    }

    /// <summary>Alpha values (one byte per pixel, row-major) for the glyph at <paramref name="code"/>.</summary>
    public ReadOnlySpan<byte> GlyphPixels(int code)
    {
        var glyph = Glyphs[code];
        var start = PixelsBase + (int)glyph.PixelOffset;
        return _data.AsSpan(start, (int)glyph.Width * LineHeight);
    }
}
