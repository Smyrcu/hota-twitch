using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using HotaTwitch.Domain.Broadcasting;
using Xunit;

namespace HotaTwitch.Domain.Tests.Broadcasting;

public sealed class BroadcastMessageTests
{
    private const string SampleDocument = """
        {"v":1,"ts":1788907728157,"screen":"adventure","date":{"day":1,"week":3,"month":1},
        "display":{"width":2560,"height":1440,"uiScale":1},
        "player":{"id":0,"name":"HaveFunMate","currentHero":184,"heroListTop":0,"townListTop":0},
        "heroes":[{"id":184,"name":"Todd","class":21,"picture":196,"level":22,"exp":132486,
        "mana":27,"manaMax":390,"move":1500,"moveMax":1500,"primary":[26,25,25,26],
        "skills":[[7,3],[19,2]],"equipped":[[0,12],[18,143]],"backpack":[5,7,122],
        "army":[[0,13,3],[1,110,15]]}],
        "towns":[{"id":1,"name":"New Dolere","type":10,"fort":3,"hall":3,"guild":5,
        "spells":[[15,27],[],[],[38,9],[36]],"research":null,"garrison":[[0,13,1]],
        "garrisonHero":null,"visitingHero":null}]}
        """;

    [Fact]
    public void TryEncode_StateDocument_ProducesGzipPrefixedBase64ThatDecodesToTheOriginal()
    {
        var document = Encoding.UTF8.GetBytes(SampleDocument);

        BroadcastMessage.TryEncode(document, out var message).Should().BeTrue();

        var encoded = message!;
        encoded.Should().StartWith("gz:");
        Gunzip(Convert.FromBase64String(encoded["gz:".Length..])).Should().Equal(document);
    }

    [Fact]
    public void TryEncode_StateDocument_StaysWithinTheEncodedMessageCap()
    {
        BroadcastMessage.TryEncode(Encoding.UTF8.GetBytes(SampleDocument), out var message).Should().BeTrue();

        Encoding.UTF8.GetByteCount(message!).Should().BeLessThanOrEqualTo(BroadcastPolicy.MaxEncodedMessageBytes);
    }

    [Fact]
    public void TryEncode_IncompressibleDocument_ReturnsFalseAndNoMessage()
    {
        var noise = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16 * 1024));
        var document = Encoding.UTF8.GetBytes($$"""{"v":1,"noise":"{{noise}}"}""");

        BroadcastMessage.TryEncode(document, out var message).Should().BeFalse();

        message.Should().BeNull();
    }

    [Fact]
    public void TryEncode_EmptyDocument_ReturnsFalse() =>
        BroadcastMessage.TryEncode([], out _).Should().BeFalse();

    private static byte[] Gunzip(byte[] compressed)
    {
        using var source = new MemoryStream(compressed);
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var target = new MemoryStream();
        gzip.CopyTo(target);
        return target.ToArray();
    }
}
