using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace H3Assets;

public sealed record DefFrame(
    string Name,
    uint Format,
    int FullWidth,
    int FullHeight,
    int Width,
    int Height,
    int LeftMargin,
    int TopMargin,
    uint DataOffset);

public sealed record DefGroup(uint Id, IReadOnlyList<string> FrameNames);

/// <summary>
/// Reads the Heroes III sprite ".def" format: a 16-byte header (type, width, height, group
/// count), a 256-entry RGB palette, then one block per animation group listing frame names and
/// their byte offsets. Each frame has its own 32-byte header (format 0-3, full/data size,
/// margins) followed by RLE-compressed (formats 1-3) or raw (format 0) palette-index rows.
/// Palette index 0 is always transparent; indices 1-7 are shadow levels when the palette entry
/// at that index is one of the game's reserved "magic" colours.
/// </summary>
public sealed class DefFile
{
    private static readonly (byte R, byte G, byte B)[] ShadowKeyColors =
    [
        (0, 255, 255), (255, 150, 255), (255, 100, 255), (255, 50, 255),
        (255, 0, 255), (255, 255, 0), (180, 0, 255), (0, 255, 0),
    ];

    private static readonly byte[] ShadowAlpha = [0, 32, 64, 128, 128, 0, 128, 64];

    private readonly byte[] _data;

    private DefFile(uint type, int width, int height, Rgba32[] palette, IReadOnlyList<DefGroup> groups, IReadOnlyList<DefFrame> frames, byte[] data)
    {
        Type = type;
        Width = width;
        Height = height;
        Palette = palette;
        Groups = groups;
        Frames = frames;
        _data = data;
    }

    public uint Type { get; }

    public int Width { get; }

    public int Height { get; }

    public IReadOnlyList<Rgba32> Palette { get; }

    public IReadOnlyList<DefGroup> Groups { get; }

    public IReadOnlyList<DefFrame> Frames { get; }

    public static DefFile Parse(byte[] data)
    {
        try
        {
            return ParseHeader(data);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
        {
            throw new InvalidDataException($"DEF file is truncated or corrupt: {ex.Message}", ex);
        }
    }

    private static DefFile ParseHeader(byte[] data)
    {
        var type = BitConverter.ToUInt32(data, 0);
        var width = BitConverter.ToInt32(data, 4);
        var height = BitConverter.ToInt32(data, 8);
        var groupCount = BitConverter.ToInt32(data, 12);
        var palette = BuildPalette(data, 16);

        var pos = 16 + (256 * 3);
        var groups = new List<DefGroup>(groupCount);
        var frames = new List<DefFrame>();

        // Frames keep every declared slot, even ones sharing a data offset with another slot
        // (the game reuses frame data across states, e.g. a disabled icon pointing at the
        // normal one) - h3sprites.cs "frames" writes "<index>.png" per slot, and that index
        // must match the DEF's own declared frame count or callers can no longer address a
        // specific frame by number.
        for (var g = 0; g < groupCount; g++)
        {
            var groupId = BitConverter.ToUInt32(data, pos);
            var frameCount = BitConverter.ToInt32(data, pos + 4);
            pos += 16;

            var names = new List<string>(frameCount);
            for (var i = 0; i < frameCount; i++)
            {
                names.Add(ReadFixedString(data, pos, 13));
                pos += 13;
            }

            var offsets = new uint[frameCount];
            for (var i = 0; i < frameCount; i++)
            {
                offsets[i] = BitConverter.ToUInt32(data, pos);
                pos += 4;
            }

            groups.Add(new DefGroup(groupId, names));
            for (var i = 0; i < frameCount; i++)
            {
                frames.Add(ParseFrameHeader(data, names[i], offsets[i]));
            }
        }

        return new DefFile(type, width, height, palette, groups, frames, data);
    }

    public Image<Rgba32> Render(int frameIndex)
    {
        var frame = Frames[frameIndex];
        try
        {
            var indices = DecodeBody(_data, (int)frame.DataOffset + 32, frame.Format, frame.Width, frame.Height);
            return Compose(frame, indices, Palette);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
        {
            throw new InvalidDataException($"frame {frameIndex} ('{frame.Name}', format {frame.Format}) is malformed: {ex.Message}", ex);
        }
    }

    private static DefFrame ParseFrameHeader(byte[] data, string name, uint offset)
    {
        var o = (int)offset;
        return new DefFrame(
            name,
            BitConverter.ToUInt32(data, o + 4),
            BitConverter.ToInt32(data, o + 8),
            BitConverter.ToInt32(data, o + 12),
            BitConverter.ToInt32(data, o + 16),
            BitConverter.ToInt32(data, o + 20),
            BitConverter.ToInt32(data, o + 24),
            BitConverter.ToInt32(data, o + 28),
            offset);
    }

    private static Rgba32[] BuildPalette(byte[] data, int offset)
    {
        var palette = new Rgba32[256];
        for (var i = 0; i < 256; i++)
        {
            palette[i] = new Rgba32(data[offset + (i * 3)], data[offset + (i * 3) + 1], data[offset + (i * 3) + 2], 255);
        }

        palette[0] = new Rgba32(0, 0, 0, 0);
        for (var i = 1; i <= 7; i++)
        {
            if (IsShadowKeyColor(palette[i]))
            {
                palette[i] = new Rgba32(0, 0, 0, ShadowAlpha[i]);
            }
        }

        return palette;
    }

    private static bool IsShadowKeyColor(Rgba32 c)
    {
        return Array.Exists(ShadowKeyColors, k => k.R == c.R && k.G == c.G && k.B == c.B);
    }

    private static string ReadFixedString(byte[] data, int offset, int maxLength)
    {
        var length = Array.IndexOf(data, (byte)0, offset, maxLength) - offset;
        if (length < 0)
        {
            length = maxLength;
        }

        return Encoding.ASCII.GetString(data, offset, length);
    }

    private static Image<Rgba32> Compose(DefFrame frame, byte[] indices, IReadOnlyList<Rgba32> palette)
    {
        var image = new Image<Rgba32>(Math.Max(1, frame.FullWidth), Math.Max(1, frame.FullHeight), new Rgba32(0, 0, 0, 0));
        for (var y = 0; y < frame.Height; y++)
        {
            var targetY = y + frame.TopMargin;
            if (targetY < 0 || targetY >= image.Height)
            {
                continue;
            }

            for (var x = 0; x < frame.Width; x++)
            {
                var targetX = x + frame.LeftMargin;
                if (targetX < 0 || targetX >= image.Width)
                {
                    continue;
                }

                image[targetX, targetY] = palette[indices[(y * frame.Width) + x]];
            }
        }

        return image;
    }

    /// <summary>Decodes palette-index pixels from a frame body. Format 0 is raw; 1-3 are row/segment RLE.</summary>
    private static byte[] DecodeBody(byte[] data, int body, uint format, int width, int height)
    {
        var pixels = new byte[width * height];
        switch (format)
        {
            case 0:
                Array.Copy(data, body, pixels, 0, pixels.Length);
                break;
            case 1:
                DecodeRowOffsetPairRle(data, body, pixels, width, height);
                break;
            case 2:
                DecodeRowOffsetSegmentRle(data, body, pixels, width, height);
                break;
            case 3:
                DecodeChunkedSegmentRle(data, body, pixels, width, height);
                break;
            default:
                throw new InvalidDataException($"unknown DEF frame format {format}");
        }

        return pixels;
    }

    /// <summary>Format 1: uint32 row offsets, rows encoded as [code, len-1] pairs (code 0xFF = raw pixels follow).</summary>
    private static void DecodeRowOffsetPairRle(byte[] data, int body, byte[] pixels, int width, int height)
    {
        for (var y = 0; y < height; y++)
        {
            var p = body + (int)BitConverter.ToUInt32(data, body + (y * 4));
            var x = 0;
            while (x < width)
            {
                var code = data[p++];
                var length = data[p++] + 1;
                var n = Math.Min(length, width - x);
                if (code == 0xFF)
                {
                    Array.Copy(data, p, pixels, (y * width) + x, n);
                    p += length;
                }
                else
                {
                    Array.Fill(pixels, code, (y * width) + x, n);
                }

                x += length;
            }
        }
    }

    /// <summary>Format 2: uint16 row offsets, rows encoded with the same segment byte as format 3.</summary>
    private static void DecodeRowOffsetSegmentRle(byte[] data, int body, byte[] pixels, int width, int height)
    {
        for (var y = 0; y < height; y++)
        {
            var p = body + BitConverter.ToUInt16(data, body + (y * 2));
            DecodeSegments(data, ref p, pixels, y * width, width);
        }
    }

    /// <summary>Format 3: uint16 offset per 32-pixel chunk, each chunk segment-encoded.</summary>
    private static void DecodeChunkedSegmentRle(byte[] data, int body, byte[] pixels, int width, int height)
    {
        if (width % 32 != 0)
        {
            throw new InvalidDataException($"format 3 frame width {width} is not a multiple of 32");
        }

        var chunksPerRow = width / 32;
        for (var y = 0; y < height; y++)
        {
            for (var chunk = 0; chunk < chunksPerRow; chunk++)
            {
                var p = body + BitConverter.ToUInt16(data, body + (((y * chunksPerRow) + chunk) * 2));
                DecodeSegments(data, ref p, pixels, (y * width) + (chunk * 32), 32);
            }
        }
    }

    /// <summary>Segment byte: top 3 bits = code (7 = raw pixels follow), bottom 5 bits + 1 = run length.</summary>
    private static void DecodeSegments(byte[] data, ref int p, byte[] pixels, int destination, int count)
    {
        var x = 0;
        while (x < count)
        {
            var segment = data[p++];
            var code = segment >> 5;
            var length = (segment & 0x1F) + 1;
            var n = Math.Min(length, count - x);
            if (code == 7)
            {
                Array.Copy(data, p, pixels, destination + x, n);
                p += length;
            }
            else
            {
                Array.Fill(pixels, (byte)code, destination + x, n);
            }

            x += length;
        }
    }
}
