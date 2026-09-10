namespace H3Assets;

/// <summary>
/// A Heroes III resource archive that can resolve a resource by name. Implemented by both the
/// plaintext <see cref="LodArchive"/> and the obfuscated <see cref="HotaLodArchive"/> so callers can
/// search a list of archives in the game's own override order without caring which format each uses.
/// </summary>
public interface IResourceArchive
{
    string Label { get; }

    byte[]? TryRead(string name);
}
