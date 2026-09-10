using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace H3Assets;

/// <summary>
/// A sheet of sprites addressed by frame index. Heroes III ships two containers for these — the
/// palette-indexed <see cref="DefFile"/> and HotA's 32bpp <see cref="Def32File"/> — and a caller
/// resolving a name across archives cannot know in advance which one it will get.
/// </summary>
public interface ISpriteSheet
{
    int FrameCount { get; }

    Image<Rgba32> Render(int frameIndex);
}
