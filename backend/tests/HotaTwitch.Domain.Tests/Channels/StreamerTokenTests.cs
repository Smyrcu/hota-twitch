using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using HotaTwitch.Domain.Channels;
using Xunit;

namespace HotaTwitch.Domain.Tests.Channels;

public sealed class StreamerTokenTests
{
    [Fact]
    public void Generate_Always_ProducesPrefixedBase64UrlOf32Bytes()
    {
        var token = StreamerToken.Generate();

        token.Value.Should().StartWith("hts_");
        var body = token.Value["hts_".Length..];
        body.Should().MatchRegex("^[A-Za-z0-9_-]+$");
        Base64Url.DecodeFromChars(body).Should().HaveCount(32);
    }

    [Fact]
    public void Generate_TwoCalls_ProducesDifferentTokens() =>
        StreamerToken.Generate().Value.Should().NotBe(StreamerToken.Generate().Value);

    [Fact]
    public void Hash_KnownToken_IsSha256HexOfTheTokenText()
    {
        StreamerToken.TryParse("hts_AAAA", out var token).Should().BeTrue();

        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("hts_AAAA")));
        token.Hash().Value.Should().Be(expected);
    }

    [Fact]
    public void HashOf_RawText_MatchesHashOfParsedToken()
    {
        var token = StreamerToken.Generate();

        StreamerToken.HashOf(token.Value).Should().Be(token.Hash());
    }

    [Fact]
    public void Hint_KnownToken_ShowsPrefixAndTwoCharacters()
    {
        StreamerToken.TryParse("hts_abcdef", out var token).Should().BeTrue();

        token.Hint().Should().Be("hts_ab…");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("hts_")]
    [InlineData("hts_not+base64url")]
    public void TryParse_MalformedText_ReturnsFalse(string? value) =>
        StreamerToken.TryParse(value, out _).Should().BeFalse();

    [Fact]
    public void ToString_Always_HidesTheSecret()
    {
        var token = StreamerToken.Generate();

        token.ToString().Should().NotContain(token.Value["hts_".Length..]);
    }
}
