using AwesomeAssertions;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace H3Assets.Tests;

public class P32FImageTests
{
    private const int HeaderSize = 40;

    [Fact]
    public void IsP32F_RecognisesTheMagicOnly()
    {
        P32FImage.IsP32F(Build(2, 2, stride: 8)).Should().BeTrue();
        P32FImage.IsP32F(new byte[64]).Should().BeFalse();
    }

    [Fact]
    public void Decode_ReadsRowsBottomUp()
    {
        // Row 0 of the stored data is the bottom row of the image.
        var data = Build(1, 2, stride: 4, pixels: [10, 20, 30, 255, 40, 50, 60, 255]);

        using var image = P32FImage.Decode(data);

        image[0, 0].Should().Be(new Rgba32(60, 50, 40, 255));
        image[0, 1].Should().Be(new Rgba32(30, 20, 10, 255));
    }

    [Fact]
    public void Decode_ReadsBgraAndKeepsAlpha()
    {
        var data = Build(1, 1, stride: 4, pixels: [1, 2, 3, 128]);

        using var image = P32FImage.Decode(data);

        image[0, 0].Should().Be(new Rgba32(3, 2, 1, 128));
    }

    [Fact]
    public void Decode_HonoursAPaddedRowStride()
    {
        var pixels = new byte[] { 9, 9, 9, 255, 0xFF, 0xFF, 1, 1, 1, 255, 0xFF, 0xFF };
        var data = Build(1, 2, stride: 6, pixels: pixels);

        using var image = P32FImage.Decode(data);

        image[0, 0].Should().Be(new Rgba32(1, 1, 1, 255));
        image[0, 1].Should().Be(new Rgba32(9, 9, 9, 255));
    }

    [Fact]
    public void Decode_WithoutMagic_Throws()
    {
        var act = () => P32FImage.Decode(new byte[64]);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Decode_StrideTooShortForTheWidth_Throws()
    {
        var act = () => P32FImage.Decode(Build(4, 1, stride: 4));

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Decode_TruncatedPixels_Throws()
    {
        var data = Build(2, 2, stride: 8);
        var act = () => P32FImage.Decode(data[..(HeaderSize + 4)]);

        act.Should().Throw<InvalidDataException>();
    }

    private static byte[] Build(int width, int height, int stride, byte[]? pixels = null)
    {
        var pixelBytes = stride * height;
        var data = new byte[HeaderSize + pixelBytes];
        "P32F"u8.CopyTo(data);
        BitConverter.GetBytes(32).CopyTo(data, 8);
        BitConverter.GetBytes(HeaderSize + pixelBytes).CopyTo(data, 12);
        BitConverter.GetBytes(HeaderSize).CopyTo(data, 16);
        BitConverter.GetBytes(pixelBytes).CopyTo(data, 20);
        BitConverter.GetBytes(width).CopyTo(data, 24);
        BitConverter.GetBytes(height).CopyTo(data, 28);
        pixels?.CopyTo(data, HeaderSize);
        return data;
    }
}
