using System.Text;
using AwesomeAssertions;
using Xunit;

namespace H3Assets.Tests;

public class PortraitTableTests
{
    private const uint Base = 0x10000;

    [Fact]
    public void Locate_FollowsThePointerArrayAndCopiesTheNames()
    {
        var memory = BuildImage(["hps000kn.pcx", "hps001kn.pcx", "hps002kn.pcx"]);

        var table = PortraitTable.Locate(memory, PortraitTable.SmallSeed);

        table.Should().NotBeNull();
        table!.Names.Should().Equal("hps000kn.pcx", "hps001kn.pcx", "hps002kn.pcx");
    }

    [Fact]
    public void Locate_StopsAtTheFirstEntryThatIsNotAPortraitName()
    {
        var memory = BuildImage(["hps000kn.pcx", "hps001kn.pcx", "shortname.dat"]);

        var table = PortraitTable.Locate(memory, PortraitTable.SmallSeed);

        table!.Names.Should().Equal("hps000kn.pcx", "hps001kn.pcx");
    }

    [Fact]
    public void Locate_MatchesTheSeedRegardlessOfCase()
    {
        var memory = BuildImage(["HPS000Kn.PCX", "HPS001Kn.PCX"]);

        var table = PortraitTable.Locate(memory, PortraitTable.SmallSeed);

        table!.Names.Should().Equal("HPS000Kn.PCX", "HPS001Kn.PCX");
    }

    [Fact]
    public void Locate_IgnoresAnUnalignedPointerToTheSeed()
    {
        var memory = BuildImage(["hps000kn.pcx", "hps001kn.pcx"], alignTable: false);

        PortraitTable.Locate(memory, PortraitTable.SmallSeed).Should().BeNull();
    }

    [Fact]
    public void Locate_WithoutTheSeed_ReturnsNull()
    {
        var memory = BuildImage(["something.dat"]);

        PortraitTable.Locate(memory, PortraitTable.SmallSeed).Should().BeNull();
    }

    /// <summary>
    /// Lays out a fake address space: the names first, then a 4-byte-aligned array of pointers to
    /// them, then one pointer into a zeroed area so the walk stops there.
    /// </summary>
    private static FakeProcessMemory BuildImage(string[] names, bool alignTable = true)
    {
        var image = new byte[0x1000];
        var stringOffsets = new List<int>();
        var cursor = 0x40;
        foreach (var name in names)
        {
            stringOffsets.Add(cursor);
            var bytes = Encoding.ASCII.GetBytes(name);
            bytes.CopyTo(image, cursor);
            cursor += bytes.Length + 1;
        }

        var table = alignTable ? (cursor + 3) & ~3 : ((cursor + 3) & ~3) + 1;
        for (var i = 0; i < names.Length; i++)
        {
            BitConverter.GetBytes(Base + (uint)stringOffsets[i]).CopyTo(image, table + (i * 4));
        }

        BitConverter.GetBytes(Base + 0x800u).CopyTo(image, table + (names.Length * 4));
        return new FakeProcessMemory(Base, image);
    }

    private sealed class FakeProcessMemory(uint baseAddress, byte[] image) : IProcessMemory
    {
        public IReadOnlyList<MemoryRegion> ReadableRegions { get; } =
            [new MemoryRegion(baseAddress, baseAddress + (uint)image.Length)];

        public bool TryRead(uint address, Span<byte> buffer)
        {
            if (address < baseAddress || address + buffer.Length > baseAddress + image.Length)
            {
                return false;
            }

            image.AsSpan((int)(address - baseAddress), buffer.Length).CopyTo(buffer);
            return true;
        }
    }
}
