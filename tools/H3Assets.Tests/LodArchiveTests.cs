using System.IO.Compression;
using System.Text;
using AwesomeAssertions;
using Xunit;

namespace H3Assets.Tests;

public class LodArchiveTests
{
    private const int HeaderSize = 92;
    private const int EntrySize = 32;

    [Fact]
    public void Read_StoredEntry_ReturnsExactBytes()
    {
        var path = WriteArchive(("a.txt", "hello"u8.ToArray(), false));

        var archive = LodArchive.Open(path);
        var bytes = archive.Read(archive.Find("a.txt"));

        Encoding.ASCII.GetString(bytes).Should().Be("hello");
    }

    [Fact]
    public void Read_CompressedEntry_Decompresses()
    {
        var original = Encoding.ASCII.GetBytes(new string('x', 500) + "tail");
        var path = WriteArchive(("b.dat", original, true));

        var archive = LodArchive.Open(path);
        var bytes = archive.Read(archive.Find("b.dat"));

        bytes.Should().Equal(original);
    }

    [Fact]
    public void Find_UnknownName_Throws()
    {
        var path = WriteArchive(("a.txt", "hello"u8.ToArray(), false));
        var archive = LodArchive.Open(path);

        var act = () => archive.Find("missing.txt");

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void TryFind_UnknownName_ReturnsNull()
    {
        var path = WriteArchive(("a.txt", "hello"u8.ToArray(), false));
        var archive = LodArchive.Open(path);

        archive.TryFind("missing.txt").Should().BeNull();
    }

    [Fact]
    public void Find_IsCaseInsensitive()
    {
        var path = WriteArchive(("A.txt", "hello"u8.ToArray(), false));
        var archive = LodArchive.Open(path);

        archive.Find("a.TXT").Name.Should().Be("A.txt");
    }

    [Fact]
    public void Open_DuplicateNames_FirstOccurrenceWins()
    {
        var path = WriteArchive(
            ("dup.txt", "first"u8.ToArray(), false),
            ("dup.txt", "second"u8.ToArray(), false));

        var archive = LodArchive.Open(path);
        var bytes = archive.Read(archive.Find("dup.txt"));

        Encoding.ASCII.GetString(bytes).Should().Be("first");
    }

    [Fact]
    public void Read_EntryRangePastEndOfArchive_ThrowsInvalidDataException()
    {
        var path = WriteArchive(("a.txt", "hello"u8.ToArray(), false));
        var archive = LodArchive.Open(path);
        var corrupt = archive.Find("a.txt") with { Size = 1_000_000 };

        var act = () => archive.Read(corrupt);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Open_TruncatedDirectory_ThrowsInvalidDataException()
    {
        var full = WriteArchiveBytes(("a.txt", "hello"u8.ToArray(), false));
        var truncated = full[..(HeaderSize + 10)];
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, truncated);

        var act = () => LodArchive.Open(path);

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
        writer.Write((uint)0);
        writer.Write((uint)files.Length);
        writer.Write(new byte[HeaderSize - 12]);

        var payloads = files.Select(f => f.Compress ? Compress(f.Data) : f.Data).ToArray();
        var offsets = new int[files.Length];
        var offset = HeaderSize + (files.Length * EntrySize);
        for (var i = 0; i < files.Length; i++)
        {
            offsets[i] = offset;
            offset += payloads[i].Length;
        }

        for (var i = 0; i < files.Length; i++)
        {
            var name = new byte[16];
            Encoding.ASCII.GetBytes(files[i].Name).CopyTo(name, 0);
            writer.Write(name);
            writer.Write((uint)offsets[i]);
            writer.Write((uint)files[i].Data.Length);
            writer.Write((uint)0); // type, unused
            writer.Write((uint)(files[i].Compress ? payloads[i].Length : 0));
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
