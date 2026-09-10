using System.Text;
using AwesomeAssertions;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace H3Assets.Tests;

public class Def32FileTests
{
    private const int HeaderSize = 24;
    private const int GroupRecordSize = 24;
    private const int NameLength = 13;
    private const int FrameHeaderSize = 40;

    [Fact]
    public void IsDef32_RecognisesTheMagicOnly()
    {
        Def32File.IsDef32(Build(4, 4, [Frame(4, 4, 1, 1, 0, 0, [1, 2, 3, 255])])).Should().BeTrue();
        Def32File.IsDef32(new byte[64]).Should().BeFalse();
    }

    [Fact]
    public void Parse_ReadsFrameNamesAndGeometry()
    {
        var sheet = Def32File.Parse(BuildNamed(8, 8,
            [
                Frame(8, 8, 2, 1, 3, 4, [1, 2, 3, 255, 4, 5, 6, 255]),
                Frame(8, 8, 1, 1, 0, 0, [7, 8, 9, 255]),
            ],
            ["first", "second"]));

        sheet.Width.Should().Be(8);
        sheet.Height.Should().Be(8);
        sheet.FrameCount.Should().Be(2);
        sheet.Frames[0].Name.Should().Be("first");
        sheet.Frames[0].Width.Should().Be(2);
        sheet.Frames[0].LeftMargin.Should().Be(3);
        sheet.Frames[0].TopMargin.Should().Be(4);
    }

    [Fact]
    public void Render_PlacesTheSpriteAtItsMarginsOnATransparentCanvas()
    {
        var sheet = Def32File.Parse(Build(4, 4,
            [Frame(4, 4, 1, 1, 2, 3, [10, 20, 30, 255])]));

        using var image = sheet.Render(0);

        image.Width.Should().Be(4);
        image.Height.Should().Be(4);
        image[2, 3].Should().Be(new Rgba32(30, 20, 10, 255));
        image[0, 0].Should().Be(new Rgba32(0, 0, 0, 0));
    }

    [Fact]
    public void Render_ReadsRowsBottomUp()
    {
        var sheet = Def32File.Parse(Build(1, 2,
            [Frame(1, 2, 1, 2, 0, 0, [10, 20, 30, 255, 40, 50, 60, 255])]));

        using var image = sheet.Render(0);

        image[0, 0].Should().Be(new Rgba32(60, 50, 40, 255));
        image[0, 1].Should().Be(new Rgba32(30, 20, 10, 255));
    }

    [Fact]
    public void Parse_UnexpectedBytesPerPixel_Throws()
    {
        var data = Build(4, 4, [Frame(4, 4, 1, 1, 0, 0, [1, 2, 3, 255])]);
        BitConverter.GetBytes(1).CopyTo(data, 44);

        var act = () => Def32File.Parse(data);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Render_SpriteLargerThanTheCanvas_Throws()
    {
        var sheet = Def32File.Parse(Build(2, 2,
            [Frame(2, 2, 1, 1, 5, 0, [1, 2, 3, 255])]));

        var act = () => sheet.Render(0);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Parse_MultiGroupSheet_Throws()
    {
        // Group 0's tables sit where a single-group sheet keeps them, so a reader that ignored the
        // group count would return group 0's frames and silently drop the rest.
        var data = Build(2, 2, [Frame(2, 2, 1, 1, 0, 0, [1, 2, 3, 255])]);
        BitConverter.GetBytes(16).CopyTo(data, 20);

        var act = () => Def32File.Parse(data);

        act.Should().Throw<InvalidDataException>().WithMessage("*16 groups*");
    }

    [Fact]
    public void Parse_FrameCountThatOverflowsTheTableMath_Throws()
    {
        var data = Build(2, 2, [Frame(2, 2, 1, 1, 0, 0, [1, 2, 3, 255])]);
        BitConverter.GetBytes(0x0F0F0F0F).CopyTo(data, 40);

        var act = () => Def32File.Parse(data);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void SpriteSheet_Parse_FallsBackToTheIndexedReader()
    {
        var indexed = DefFileBuilder.Build(format: 0, width: 2, height: 2, body: new byte[4]);

        SpriteSheet.Parse(indexed).Should().BeOfType<DefFile>();
    }

    [Fact]
    public void SpriteSheet_Parse_PicksTheD32FReader()
    {
        var sheet = SpriteSheet.Parse(Build(2, 2, [Frame(2, 2, 1, 1, 0, 0, [1, 2, 3, 255])]));

        sheet.Should().BeOfType<Def32File>();
    }

    private static byte[] Frame(int fullWidth, int fullHeight, int width, int height, int left, int top, byte[] pixels)
    {
        var frame = new byte[FrameHeaderSize + pixels.Length];
        BitConverter.GetBytes(32).CopyTo(frame, 0);
        BitConverter.GetBytes(pixels.Length).CopyTo(frame, 4);
        BitConverter.GetBytes(fullWidth).CopyTo(frame, 8);
        BitConverter.GetBytes(fullHeight).CopyTo(frame, 12);
        BitConverter.GetBytes(width).CopyTo(frame, 16);
        BitConverter.GetBytes(height).CopyTo(frame, 20);
        BitConverter.GetBytes(left).CopyTo(frame, 24);
        BitConverter.GetBytes(top).CopyTo(frame, 28);
        pixels.CopyTo(frame, FrameHeaderSize);
        return frame;
    }

    private static byte[] Build(int width, int height, byte[][] frames)
    {
        return BuildNamed(width, height, frames, [.. frames.Select((_, i) => $"frame{i}")]);
    }

    private static byte[] BuildNamed(int width, int height, byte[][] frames, string[] names)
    {
        var count = frames.Length;
        var namesStart = HeaderSize + GroupRecordSize;
        var offsetsStart = namesStart + (count * NameLength);
        var dataStart = offsetsStart + (count * 4);
        var data = new byte[dataStart + frames.Sum(f => f.Length)];

        "D32F"u8.CopyTo(data);
        BitConverter.GetBytes(1).CopyTo(data, 4);
        BitConverter.GetBytes(HeaderSize).CopyTo(data, 8);
        BitConverter.GetBytes(width).CopyTo(data, 12);
        BitConverter.GetBytes(height).CopyTo(data, 16);
        BitConverter.GetBytes(1).CopyTo(data, 20);
        BitConverter.GetBytes(count).CopyTo(data, 40);
        BitConverter.GetBytes(4).CopyTo(data, 44);

        var offset = dataStart;
        for (var i = 0; i < count; i++)
        {
            Encoding.ASCII.GetBytes(names[i]).CopyTo(data, namesStart + (i * NameLength));
            BitConverter.GetBytes(offset).CopyTo(data, offsetsStart + (i * 4));
            frames[i].CopyTo(data, offset);
            offset += frames[i].Length;
        }

        return data;
    }
}
