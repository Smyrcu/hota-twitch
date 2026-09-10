namespace H3Assets;

/// <summary>
/// Parses a sprite sheet resource read from a LOD archive into whichever container it turns out to
/// be: HotA's 32bpp <see cref="Def32File"/> or the palette-indexed <see cref="DefFile"/>.
/// </summary>
public static class SpriteSheet
{
    public static ISpriteSheet Parse(byte[] data)
    {
        return Def32File.IsDef32(data) ? Def32File.Parse(data) : DefFile.Parse(data);
    }
}
