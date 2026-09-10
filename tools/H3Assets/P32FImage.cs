using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace H3Assets;

/// <summary>
/// Decodes the "P32F" 32bpp bitmaps HotA stores alongside the classic <see cref="PcxImage"/> ones.
/// Header dwords: magic "P32F", reserved, bit depth (32), total size, header size (40), pixel byte
/// count, width, height. Pixels follow at the header size as BGRA rows; the row stride is derived
/// from the pixel byte count rather than assumed, because the format allows padded scanlines.
/// </summary>
public static class P32FImage
{
    private const int MinimumHeaderSize = 32;

    public static bool IsP32F(byte[] data)
    {
        return data.Length >= MinimumHeaderSize && data.AsSpan(0, 4).SequenceEqual("P32F"u8);
    }

    public static Image<Rgba32> Decode(byte[] data)
    {
        try
        {
            return DecodeBitmap(data);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
        {
            throw new InvalidDataException($"P32F bitmap is truncated or corrupt: {ex.Message}", ex);
        }
    }

    private static Image<Rgba32> DecodeBitmap(byte[] data)
    {
        if (!IsP32F(data))
        {
            throw new InvalidDataException("P32F: missing magic header");
        }

        var headerSize = BitConverter.ToInt32(data, 16);
        var pixelBytes = BitConverter.ToInt32(data, 20);
        var width = BitConverter.ToInt32(data, 24);
        var height = BitConverter.ToInt32(data, 28);

        if (width <= 0 || height <= 0 || headerSize < MinimumHeaderSize || pixelBytes <= 0)
        {
            throw new InvalidDataException($"P32F: implausible header ({width}x{height}, header {headerSize}, pixels {pixelBytes})");
        }

        if (pixelBytes % height != 0)
        {
            throw new InvalidDataException($"P32F: {pixelBytes} pixel bytes do not divide into {height} rows");
        }

        var stride = pixelBytes / height;
        if (stride < (long)width * 4)
        {
            throw new InvalidDataException($"P32F: row stride {stride} is too short for {width} pixels");
        }

        if ((long)headerSize + pixelBytes > data.Length)
        {
            throw new InvalidDataException($"P32F: {width}x{height} needs {headerSize + pixelBytes} bytes, got {data.Length}");
        }

        var image = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var p = headerSize + ((height - 1 - y) * stride) + (x * 4);
                image[x, y] = new Rgba32(data[p + 2], data[p + 1], data[p], data[p + 3]);
            }
        }

        return image;
    }
}
