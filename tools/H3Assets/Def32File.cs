using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace H3Assets;

public sealed record Def32Frame(
    string Name,
    uint Offset,
    int FullWidth,
    int FullHeight,
    int Width,
    int Height,
    int LeftMargin,
    int TopMargin);

/// <summary>
/// Reads HotA's "D32F" sprite sheets, the 32bpp counterpart of <see cref="DefFile"/>. Layout:
/// a 24-byte header (magic, version, header size, sheet width, sheet height, group count), one
/// 24-byte group record per group whose last fields are the frame count and the bytes per pixel,
/// then a 13-byte name per frame and a dword offset per frame. Each frame is a 40-byte header
/// (data size, sheet size, the sprite's own size, and its margins within the sheet) followed by
/// raw BGRA rows stored bottom-up, as in <see cref="P32FImage"/>; the margins place the sprite on
/// an otherwise transparent canvas.
/// </summary>
public sealed class Def32File : ISpriteSheet
{
    private const int HeaderSize = 24;
    private const int GroupRecordSize = 24;
    private const int NameLength = 13;
    private const int FrameHeaderSize = 40;
    private const int BytesPerPixel = 4;

    private readonly byte[] _data;

    private Def32File(byte[] data, int width, int height, IReadOnlyList<Def32Frame> frames)
    {
        _data = data;
        Width = width;
        Height = height;
        Frames = frames;
    }

    public int Width { get; }

    public int Height { get; }

    public IReadOnlyList<Def32Frame> Frames { get; }

    public int FrameCount => Frames.Count;

    public static bool IsDef32(byte[] data)
    {
        return data.Length >= HeaderSize + GroupRecordSize && data.AsSpan(0, 4).SequenceEqual("D32F"u8);
    }

    public static Def32File Parse(byte[] data)
    {
        try
        {
            return ParseSheet(data);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
        {
            throw new InvalidDataException($"D32F sheet is truncated or corrupt: {ex.Message}", ex);
        }
    }

    private static Def32File ParseSheet(byte[] data)
    {
        if (!IsDef32(data))
        {
            throw new InvalidDataException("D32F: missing magic header");
        }

        var groups = BitConverter.ToInt32(data, 20);
        if (groups != 1)
        {
            throw new InvalidDataException($"D32F: {groups} groups; only single-group sheets are decoded");
        }

        var width = BitConverter.ToInt32(data, 12);
        var height = BitConverter.ToInt32(data, 16);
        var count = BitConverter.ToInt32(data, 40);
        var bytesPerPixel = BitConverter.ToInt32(data, 44);
        if (bytesPerPixel != BytesPerPixel)
        {
            throw new InvalidDataException($"D32F: {bytesPerPixel} bytes per pixel, expected {BytesPerPixel}");
        }

        var namesStart = (long)HeaderSize + GroupRecordSize;
        var offsetsStart = namesStart + ((long)count * NameLength);
        if (count <= 0 || offsetsStart + ((long)count * 4) > data.Length)
        {
            throw new InvalidDataException($"D32F: frame table of {count} frames does not fit in {data.Length} bytes");
        }

        var frames = new List<Def32Frame>(count);
        for (var i = 0; i < count; i++)
        {
            var name = ReadFixedString(data, (int)(namesStart + ((long)i * NameLength)), NameLength);
            var offset = BitConverter.ToUInt32(data, (int)(offsetsStart + ((long)i * 4)));
            if ((long)offset + FrameHeaderSize > data.Length)
            {
                throw new InvalidDataException($"D32F: frame {i} starts past the end of the sheet");
            }

            frames.Add(new Def32Frame(
                name,
                offset,
                BitConverter.ToInt32(data, (int)offset + 8),
                BitConverter.ToInt32(data, (int)offset + 12),
                BitConverter.ToInt32(data, (int)offset + 16),
                BitConverter.ToInt32(data, (int)offset + 20),
                BitConverter.ToInt32(data, (int)offset + 24),
                BitConverter.ToInt32(data, (int)offset + 28)));
        }

        return new Def32File(data, width, height, frames);
    }

    public Image<Rgba32> Render(int frameIndex)
    {
        var frame = Frames[frameIndex];
        if (frame.Width <= 0 || frame.Height <= 0)
        {
            throw new InvalidDataException($"D32F frame '{frame.Name}': implausible size {frame.Width}x{frame.Height}");
        }

        var declared = BitConverter.ToInt32(_data, (int)frame.Offset + 4);
        var expected = (long)frame.Width * frame.Height * BytesPerPixel;
        if (declared != expected)
        {
            throw new InvalidDataException($"D32F frame '{frame.Name}': {declared} bytes for {frame.Width}x{frame.Height}, expected {expected}");
        }

        var pixels = (long)frame.Offset + FrameHeaderSize;
        if (pixels + declared > _data.Length)
        {
            throw new InvalidDataException($"D32F frame '{frame.Name}' is truncated");
        }

        if (frame.LeftMargin < 0 || frame.TopMargin < 0 ||
            frame.LeftMargin + frame.Width > frame.FullWidth ||
            frame.TopMargin + frame.Height > frame.FullHeight)
        {
            throw new InvalidDataException($"D32F frame '{frame.Name}': {frame.Width}x{frame.Height} at ({frame.LeftMargin},{frame.TopMargin}) does not fit {frame.FullWidth}x{frame.FullHeight}");
        }

        var image = new Image<Rgba32>(frame.FullWidth, frame.FullHeight);
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var p = (int)pixels + ((((frame.Height - 1 - y) * frame.Width) + x) * BytesPerPixel);
                image[frame.LeftMargin + x, frame.TopMargin + y] =
                    new Rgba32(_data[p + 2], _data[p + 1], _data[p], _data[p + 3]);
            }
        }

        return image;
    }

    private static string ReadFixedString(byte[] data, int offset, int maxLength)
    {
        var length = Array.IndexOf(data, (byte)0, offset, maxLength) - offset;
        return Encoding.ASCII.GetString(data, offset, length < 0 ? maxLength : length);
    }
}
