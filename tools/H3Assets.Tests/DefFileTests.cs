using AwesomeAssertions;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace H3Assets.Tests;

public class DefFileTests
{
    // Palette index i is written as RGB (i,i,i), so a decoded pixel's red channel reveals
    // exactly which palette index the RLE decoder produced for that pixel.
    [Fact]
    public void Render_Format0_CopiesRawBytesRowMajor()
    {
        byte[] body = [10, 11, 12, 13, 20, 21, 22, 23];
        var bytes = DefFileBuilder.Build(format: 0, width: 4, height: 2, body);

        var def = DefFile.Parse(bytes);
        using var image = def.Render(0);

        AssertRow(image, y: 0, [10, 11, 12, 13]);
        AssertRow(image, y: 1, [20, 21, 22, 23]);
    }

    [Fact]
    public void Render_Format1_DecodesRawAndFillSegments()
    {
        // Row offset table (uint32, relative to body start): row0 at 8, row1 at 14.
        // Row0: [0xFF, len-1=3, 10, 11, 12, 13]  -> raw run of 4 pixels.
        // Row1: [20, len-1=3]                    -> fill run of 4 pixels with value 20.
        byte[] body =
        [
            8, 0, 0, 0, 14, 0, 0, 0,
            0xFF, 3, 10, 11, 12, 13,
            20, 3,
        ];
        var bytes = DefFileBuilder.Build(format: 1, width: 4, height: 2, body);

        var def = DefFile.Parse(bytes);
        using var image = def.Render(0);

        AssertRow(image, y: 0, [10, 11, 12, 13]);
        AssertRow(image, y: 1, [20, 20, 20, 20]);
    }

    [Fact]
    public void Render_Format2_DecodesSegmentBytesNotOffsetPairs()
    {
        // This is the regression the code review caught: format 2 was routed to the format-1
        // [code,len]-pair decoder instead of the 3bit-code/5bit-length segment decoder that
        // formats 2 and 3 actually share. Row offset table is uint16 here (not uint32).
        // Row0: segment 0xE3 = (code=7 raw, len-1=3) then 4 raw bytes.
        // Row1: segment 0x63 = (code=3 fill, len-1=3) -> fill run of 4 pixels with value 3.
        byte[] body =
        [
            4, 0, 9, 0,
            0xE3, 30, 31, 32, 33,
            0x63,
        ];
        var bytes = DefFileBuilder.Build(format: 2, width: 4, height: 2, body);

        var def = DefFile.Parse(bytes);
        using var image = def.Render(0);

        AssertRow(image, y: 0, [30, 31, 32, 33]);
        AssertRow(image, y: 1, [3, 3, 3, 3]);
    }

    [Fact]
    public void Render_Format3_DecodesOneSegmentPer32PixelChunk()
    {
        // Two 32-pixel chunks in one row. Chunk0: raw segment 0xFF = (code=7, len-1=31) then
        // 32 raw bytes 40..71. Chunk1: fill segment 0xDF = (code=6, len-1=31) -> 32 pixels of 6.
        var chunk0Raw = Enumerable.Range(40, 32).Select(v => (byte)v).ToArray();
        var body = new List<byte> { 4, 0, 37, 0, 0xFF };
        body.AddRange(chunk0Raw);
        body.Add(0xDF);

        var bytes = DefFileBuilder.Build(format: 3, width: 64, height: 1, [.. body]);

        var def = DefFile.Parse(bytes);
        using var image = def.Render(0);

        for (var x = 0; x < 32; x++)
        {
            image[x, 0].R.Should().Be((byte)(40 + x));
        }

        for (var x = 32; x < 64; x++)
        {
            image[x, 0].R.Should().Be(6);
        }
    }

    [Fact]
    public void Render_Format3_RejectsWidthNotMultipleOf32()
    {
        byte[] body = [0, 0, 0xBF];
        var bytes = DefFileBuilder.Build(format: 3, width: 33, height: 1, body);

        var def = DefFile.Parse(bytes);
        var act = () => def.Render(0);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Render_PaletteIndexZero_IsAlwaysTransparent()
    {
        byte[] body = [0, 1];
        var bytes = DefFileBuilder.Build(format: 0, width: 2, height: 1, body);

        var def = DefFile.Parse(bytes);
        using var image = def.Render(0);

        image[0, 0].Should().Be(new Rgba32(0, 0, 0, 0));
        image[1, 0].Should().Be(new Rgba32(1, 1, 1, 255));
    }

    [Fact]
    public void Frames_KeepsEveryDeclaredSlotEvenWhenTwoSharePixelData()
    {
        // Two groups whose frames all point at the same data offset must still produce two
        // Frames entries - deduplicating by offset was the bug that turned a 44-frame DEF into
        // 35 exported files.
        var bytes = TwoGroupsSharingOneFrame();

        var def = DefFile.Parse(bytes);

        def.Frames.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_TruncatedFile_ThrowsInvalidDataException()
    {
        var act = () => DefFile.Parse(new byte[10]);

        act.Should().Throw<InvalidDataException>();
    }

    private static void AssertRow(SixLabors.ImageSharp.Image<Rgba32> image, int y, byte[] expected)
    {
        for (var x = 0; x < expected.Length; x++)
        {
            image[x, y].R.Should().Be(expected[x], $"pixel ({x},{y})");
        }
    }

    private static byte[] TwoGroupsSharingOneFrame()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((uint)0);
        writer.Write(2);
        writer.Write(1);
        writer.Write(2); // group count

        for (var i = 0; i < 256; i++)
        {
            writer.Write((byte)i);
            writer.Write((byte)i);
            writer.Write((byte)i);
        }

        const int groupBlockSize = 4 + 4 + 8 + 13 + 4; // id + frameCount + reserved + name + offset
        var frameHeaderOffset = (uint)(stream.Position + (2 * groupBlockSize));

        void WriteGroup(uint groupId)
        {
            writer.Write(groupId);
            writer.Write(1); // frame count
            writer.Write(new byte[8]); // reserved
            writer.Write(new byte[13]); // frame name
            writer.Write(frameHeaderOffset);
        }

        WriteGroup(0);
        WriteGroup(1);

        writer.Write((uint)0); // unused
        writer.Write((uint)0); // format
        writer.Write(2); // full width
        writer.Write(1); // full height
        writer.Write(2); // width
        writer.Write(1); // height
        writer.Write(0); // left margin
        writer.Write(0); // top margin
        byte[] unusedPixels = [200, 201];
        writer.Write(unusedPixels);

        return stream.ToArray();
    }
}
