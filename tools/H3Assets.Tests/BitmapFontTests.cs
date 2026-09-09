using AwesomeAssertions;
using Xunit;

namespace H3Assets.Tests;

public class BitmapFontTests
{
    private const int MetricsBase = 32;
    private const int OffsetsBase = MetricsBase + (256 * 12);
    private const int PixelsBase = OffsetsBase + (256 * 4);

    [Fact]
    public void Parse_ReadsLineHeightMetricsAndPixels()
    {
        byte[] pixels = [10, 20, 30, 40, 50, 60]; // 2 wide x 3 tall
        var data = BuildFont(lineHeight: 3, (Code: 65, Left: 1, Width: 2u, Right: 1, pixels));

        var font = BitmapFont.Parse(data);

        font.LineHeight.Should().Be(3);
        font.Glyphs[65].Should().Be(new GlyphMetrics(1, 2, 1, 0));
        font.Glyphs[65].Advance.Should().Be(4);
        font.GlyphPixels(65).ToArray().Should().Equal(pixels);
    }

    [Fact]
    public void Parse_UnusedCode_HasZeroMetrics()
    {
        var data = BuildFont(lineHeight: 3, (Code: 65, Left: 1, Width: 2u, Right: 1, [1, 2, 3, 4, 5, 6]));

        var font = BitmapFont.Parse(data);

        font.Glyphs[0].Should().Be(new GlyphMetrics(0, 0, 0, 0));
    }

    [Fact]
    public void Parse_FileShorterThanPixelsBase_Throws()
    {
        var act = () => BitmapFont.Parse(new byte[100]);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Parse_GlyphPixelRangePastEndOfFile_Throws()
    {
        var data = BuildFont(lineHeight: 3, (Code: 65, Left: 0, Width: 2u, Right: 0, [1, 2, 3, 4, 5, 6]));
        var truncated = data[..^1]; // chop the last pixel byte off

        var act = () => BitmapFont.Parse(truncated);

        act.Should().Throw<InvalidDataException>();
    }

    private static byte[] BuildFont(byte lineHeight, params (int Code, int Left, uint Width, int Right, byte[] Pixels)[] glyphs)
    {
        var header = new byte[PixelsBase];
        header[5] = lineHeight;
        var pixelBlob = new List<byte>();

        foreach (var (code, left, width, right, pixels) in glyphs)
        {
            var metricsOffset = MetricsBase + (code * 12);
            BitConverter.GetBytes(left).CopyTo(header, metricsOffset);
            BitConverter.GetBytes(width).CopyTo(header, metricsOffset + 4);
            BitConverter.GetBytes(right).CopyTo(header, metricsOffset + 8);

            var pixelOffset = (uint)pixelBlob.Count;
            BitConverter.GetBytes(pixelOffset).CopyTo(header, OffsetsBase + (code * 4));
            pixelBlob.AddRange(pixels);
        }

        return [.. header, .. pixelBlob];
    }
}
