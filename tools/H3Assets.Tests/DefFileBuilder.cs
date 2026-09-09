using System.Text;

namespace H3Assets.Tests;

/// <summary>Assembles a minimal, single-group, single-frame ".def" byte array for tests, so
/// each format's row/segment decoder can be exercised without a real game asset.</summary>
internal static class DefFileBuilder
{
    public static byte[] Build(uint format, int width, int height, byte[] body)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((uint)0); // type
        writer.Write(width);
        writer.Write(height);
        writer.Write(1); // group count

        for (var i = 0; i < 256; i++)
        {
            writer.Write((byte)i);
            writer.Write((byte)i);
            writer.Write((byte)i);
        }

        writer.Write((uint)0); // group id
        writer.Write(1); // frame count
        writer.Write(new byte[8]); // reserved

        var name = new byte[13];
        Encoding.ASCII.GetBytes("f.pcx").CopyTo(name, 0);
        writer.Write(name);

        var frameHeaderOffset = (uint)stream.Position + 4;
        writer.Write(frameHeaderOffset);

        writer.Write((uint)0); // unused
        writer.Write(format);
        writer.Write(width); // full width
        writer.Write(height); // full height
        writer.Write(width);
        writer.Write(height);
        writer.Write(0); // left margin
        writer.Write(0); // top margin

        writer.Write(body);

        return stream.ToArray();
    }
}
