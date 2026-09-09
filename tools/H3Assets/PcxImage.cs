using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace H3Assets;

/// <summary>
/// Decodes the PCX-like bitmap resources stored in Heroes III LOD archives: a 12-byte header
/// (int32 size, int32 width, int32 height) followed by either an 8bpp indexed image with a
/// trailing 256-entry RGB palette, or a 24bpp BGR image with no palette.
/// </summary>
public static class PcxImage
{
    public static Image<Rgba32> Decode(byte[] data)
    {
        var size = BitConverter.ToInt32(data, 0);
        var width = BitConverter.ToInt32(data, 4);
        var height = BitConverter.ToInt32(data, 8);

        try
        {
            return size == width * height
                ? DecodeIndexed(data, width, height, size)
                : size == width * height * 3
                    ? DecodeBgr(data, width, height)
                    : throw new InvalidDataException($"PCX: size={size} does not match {width}x{height} at either 8bpp or 24bpp");
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
        {
            throw new InvalidDataException($"PCX {width}x{height} (size={size}) is truncated or corrupt: {ex.Message}", ex);
        }
    }

    private static Image<Rgba32> DecodeIndexed(byte[] data, int width, int height, int size)
    {
        var paletteOffset = 12 + size;
        var image = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = data[12 + (y * width) + x];
                var p = paletteOffset + (index * 3);
                image[x, y] = new Rgba32(data[p], data[p + 1], data[p + 2], 255);
            }
        }

        return image;
    }

    private static Image<Rgba32> DecodeBgr(byte[] data, int width, int height)
    {
        var image = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var p = 12 + (((y * width) + x) * 3);
                image[x, y] = new Rgba32(data[p + 2], data[p + 1], data[p], 255);
            }
        }

        return image;
    }
}
