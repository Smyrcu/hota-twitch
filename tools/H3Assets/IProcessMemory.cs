namespace H3Assets;

public readonly record struct MemoryRegion(uint Start, uint End);

/// <summary>
/// Read-only view of another process's address space, limited to the low 4 GB the 32-bit game uses.
/// Exists so the readers built on it can be exercised against an in-memory fake instead of a live game.
/// </summary>
public interface IProcessMemory
{
    IReadOnlyList<MemoryRegion> ReadableRegions { get; }

    bool TryRead(uint address, Span<byte> buffer);
}
