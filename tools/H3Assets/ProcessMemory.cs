using System.Globalization;
using Microsoft.Win32.SafeHandles;

namespace H3Assets;

/// <summary>
/// Reads a running process's memory through <c>/proc/&lt;pid&gt;/mem</c>, with the region list taken
/// from <c>/proc/&lt;pid&gt;/maps</c>. Opened read-only; nothing here ever writes to the game.
/// </summary>
public sealed class ProcessMemory : IProcessMemory, IDisposable
{
    private const string GameProcessName = "h3hota HD.exe";

    private readonly SafeFileHandle _handle;

    private ProcessMemory(SafeFileHandle handle, IReadOnlyList<MemoryRegion> regions)
    {
        _handle = handle;
        ReadableRegions = regions;
    }

    public IReadOnlyList<MemoryRegion> ReadableRegions { get; }

    public static int? FindGamePid()
    {
        foreach (var directory in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(directory), out var pid))
            {
                continue;
            }

            try
            {
                if (File.ReadAllText($"/proc/{pid}/comm").Trim() == GameProcessName)
                {
                    return pid;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        return null;
    }

    public static ProcessMemory Open(int pid)
    {
        var handle = File.OpenHandle($"/proc/{pid}/mem", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return new ProcessMemory(handle, ReadRegions(pid));
    }

    public bool TryRead(uint address, Span<byte> buffer)
    {
        try
        {
            var total = 0;
            while (total < buffer.Length)
            {
                var read = RandomAccess.Read(_handle, buffer[total..], address + (uint)total);
                if (read <= 0)
                {
                    return false;
                }

                total += read;
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _handle.Dispose();
    }

    private static List<MemoryRegion> ReadRegions(int pid)
    {
        var regions = new List<MemoryRegion>();
        foreach (var line in File.ReadLines($"/proc/{pid}/maps"))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !parts[1].StartsWith('r'))
            {
                continue;
            }

            var range = parts[0].Split('-');
            var start = ulong.Parse(range[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var end = ulong.Parse(range[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (start >= 0x1_0000_0000UL)
            {
                continue;
            }

            regions.Add(new MemoryRegion((uint)start, (uint)Math.Min(end, 0xFFFF_FFFFUL)));
        }

        return regions;
    }
}
