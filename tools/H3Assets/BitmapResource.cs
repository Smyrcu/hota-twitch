using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace H3Assets;

/// <summary>
/// Decodes a bitmap resource read from a LOD archive, which is either a classic
/// <see cref="PcxImage"/> or one of HotA's <see cref="P32FImage"/> ones. Callers that resolve a name
/// across both archive formats cannot know in advance which container they will get back.
/// </summary>
public static class BitmapResource
{
    public static Image<Rgba32> Decode(byte[] data)
    {
        return P32FImage.IsP32F(data) ? P32FImage.Decode(data) : PcxImage.Decode(data);
    }
}
