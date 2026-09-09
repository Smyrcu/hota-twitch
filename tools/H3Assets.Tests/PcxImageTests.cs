using AwesomeAssertions;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace H3Assets.Tests;

public class PcxImageTests
{
    [Fact]
    public void Decode_Indexed8Bpp_MapsThroughTrailingPalette()
    {
        byte[] indices = [0, 1, 2, 3];
        byte[] palette = new byte[256 * 3];
        SetPaletteEntry(palette, 0, 10, 20, 30);
        SetPaletteEntry(palette, 1, 40, 50, 60);
        SetPaletteEntry(palette, 2, 70, 80, 90);
        SetPaletteEntry(palette, 3, 100, 110, 120);

        var data = BuildHeader(size: 4, width: 2, height: 2)
            .Concat(indices)
            .Concat(palette)
            .ToArray();

        using var image = PcxImage.Decode(data);

        image[0, 0].Should().Be(new Rgba32(10, 20, 30, 255));
        image[1, 0].Should().Be(new Rgba32(40, 50, 60, 255));
        image[0, 1].Should().Be(new Rgba32(70, 80, 90, 255));
        image[1, 1].Should().Be(new Rgba32(100, 110, 120, 255));
    }

    [Fact]
    public void Decode_24Bpp_SwapsBgrToRgb()
    {
        byte[] bgr = [30, 20, 10, 60, 50, 40];
        var data = BuildHeader(size: 6, width: 2, height: 1).Concat(bgr).ToArray();

        using var image = PcxImage.Decode(data);

        image[0, 0].Should().Be(new Rgba32(10, 20, 30, 255));
        image[1, 0].Should().Be(new Rgba32(40, 50, 60, 255));
    }

    [Fact]
    public void Decode_SizeMatchesNeitherBppVariant_Throws()
    {
        var data = BuildHeader(size: 999, width: 2, height: 2).Concat(new byte[999]).ToArray();

        var act = () => PcxImage.Decode(data);

        act.Should().Throw<InvalidDataException>();
    }

    private static byte[] BuildHeader(int size, int width, int height)
    {
        var header = new byte[12];
        BitConverter.GetBytes(size).CopyTo(header, 0);
        BitConverter.GetBytes(width).CopyTo(header, 4);
        BitConverter.GetBytes(height).CopyTo(header, 8);
        return header;
    }

    private static void SetPaletteEntry(byte[] palette, int index, byte r, byte g, byte b)
    {
        palette[index * 3] = r;
        palette[(index * 3) + 1] = g;
        palette[(index * 3) + 2] = b;
    }
}
