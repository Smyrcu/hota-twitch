using System.IO.Compression;
using System.Text;
using AwesomeAssertions;
using Xunit;

namespace H3Assets.Tests;

public class HotaLodArchiveTests
{
    private const int HeaderSize = 92;
    private const int EntrySize = 32;
    private const uint Key = 0xB5A4D744;

    [Fact]
    public void NameHash_IsFnv1aOverTheLowercasedName()
    {
        // 32-bit FNV-1a of "cprsmall.def", the value HotA.lod itself stores for that resource.
        HotaLodArchive.NameHash("cprsmall.def").Should().Be(0xA85B8546);
    }

    [Fact]
    public void NameHash_IgnoresCase()
    {
        HotaLodArchive.NameHash("CPRSMALL.def").Should().Be(HotaLodArchive.NameHash("cprsmall.def"));
    }

    [Fact]
    public void Read_StoredEntry_ReturnsExactBytes()
    {
        var archive = HotaLodArchive.Open(WriteArchive(("a.txt", "hello"u8.ToArray(), false)));

        Encoding.ASCII.GetString(archive.TryRead("a.txt")!).Should().Be("hello");
    }

    [Fact]
    public void Read_CompressedEntry_Decompresses()
    {
        var original = Encoding.ASCII.GetBytes(new string('x', 500) + "tail");
        var archive = HotaLodArchive.Open(WriteArchive(("b.dat", original, true)));

        archive.TryRead("b.dat").Should().Equal(original);
    }

    [Fact]
    public void TryRead_UnknownName_ReturnsNull()
    {
        var archive = HotaLodArchive.Open(WriteArchive(("a.txt", "hello"u8.ToArray(), false)));

        archive.TryRead("missing.txt").Should().BeNull();
    }

    [Fact]
    public void Find_UnknownName_Throws()
    {
        var archive = HotaLodArchive.Open(WriteArchive(("a.txt", "hello"u8.ToArray(), false)));

        var act = () => archive.Find("missing.txt");

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Open_ReadsTheKeyFromTheHeaderAndDecodesTheEntryNumbers()
    {
        var archive = HotaLodArchive.Open(WriteArchive(("a.txt", "hello"u8.ToArray(), false)));

        archive.Key.Should().Be(Key);
        archive.DataStart.Should().Be(HeaderSize + EntrySize);
        archive.Entries.Should().ContainSingle();
        archive.Entries[0].Offset.Should().Be(archive.DataStart);
        archive.Entries[0].Size.Should().Be(5);
        archive.Entries[0].CompressedSize.Should().Be(0);
    }

    [Fact]
    public void Open_EntriesTileThePayloadRegionExactly()
    {
        var archive = HotaLodArchive.Open(WriteArchive(
            ("a.txt", "hello"u8.ToArray(), false),
            ("b.dat", Encoding.ASCII.GetBytes(new string('y', 400)), true),
            ("c.txt", "tail"u8.ToArray(), false)));

        var next = archive.DataStart;
        foreach (var entry in archive.Entries)
        {
            entry.Offset.Should().Be(next);
            next += entry.CompressedSize == 0 ? entry.Size : entry.CompressedSize;
        }

        next.Should().Be((uint)archive.Length);
    }

    [Fact]
    public void Open_MissingMagic_ThrowsInvalidDataException()
    {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[HeaderSize]);

        var act = () => HotaLodArchive.Open(path);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Open_DirectoryLargerThanTheFile_ThrowsInvalidDataException()
    {
        var bytes = WriteArchiveBytes(("a.txt", "hello"u8.ToArray(), false));
        BitConverter.GetBytes(9999u).CopyTo(bytes, 8);
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, bytes);

        var act = () => HotaLodArchive.Open(path);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Read_EntryRangePastEndOfArchive_ThrowsInvalidDataException()
    {
        var archive = HotaLodArchive.Open(WriteArchive(("a.txt", "hello"u8.ToArray(), false)));
        var corrupt = archive.Find("a.txt") with { Size = 1_000_000 };

        var act = () => archive.Read(corrupt);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Read_CompressedEntryWithAnOversizedDeclaredSize_ThrowsInvalidDataException()
    {
        // HotA.lod's sizes are obfuscated, so a wrong key or one flipped byte yields an arbitrary
        // uncompressed size; it must not reach an allocation.
        var archive = HotaLodArchive.Open(WriteArchive(("b.dat", Encoding.ASCII.GetBytes(new string('x', 400)), true)));
        var corrupt = archive.Find("b.dat") with { Size = 0x80000000 };

        var act = () => archive.Read(corrupt);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Read_CompressedEntryThatInflatesToTheWrongLength_ThrowsInvalidDataException()
    {
        var archive = HotaLodArchive.Open(WriteArchive(("b.dat", Encoding.ASCII.GetBytes(new string('x', 400)), true)));
        var corrupt = archive.Find("b.dat") with { Size = 399 };

        var act = () => archive.Read(corrupt);

        act.Should().Throw<InvalidDataException>();
    }

    private static string WriteArchive(params (string Name, byte[] Data, bool Compress)[] files)
    {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, WriteArchiveBytes(files));
        return path;
    }

    private static byte[] WriteArchiveBytes(params (string Name, byte[] Data, bool Compress)[] files)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write("LOD\0"u8.ToArray());
        writer.Write((uint)0xC8);
        writer.Write((uint)files.Length);
        writer.Write(Key); // the key sits where a plaintext LOD keeps a zero
        writer.Write(new byte[HeaderSize - 16]);

        var payloads = files.Select(f => f.Compress ? Compress(f.Data) : f.Data).ToArray();
        var offset = (uint)(HeaderSize + (files.Length * EntrySize));
        for (var i = 0; i < files.Length; i++)
        {
            writer.Write(HotaLodArchive.NameHash(files[i].Name));
            writer.Write(offset ^ Key);
            writer.Write((uint)files[i].Data.Length ^ Key);
            writer.Write((uint)(files[i].Compress ? payloads[i].Length : 0) ^ Key);
            writer.Write(new byte[16]); // opaque tail, not the name
            offset += (uint)payloads[i].Length;
        }

        foreach (var payload in payloads)
        {
            writer.Write(payload);
        }

        return stream.ToArray();
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }
}
