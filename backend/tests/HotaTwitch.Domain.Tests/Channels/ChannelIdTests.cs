using AwesomeAssertions;
using HotaTwitch.Domain.Channels;
using Xunit;

namespace HotaTwitch.Domain.Tests.Channels;

public sealed class ChannelIdTests
{
    [Fact]
    public void Constructor_NumericValue_KeepsValue()
    {
        var channelId = new ChannelId("141981764");

        channelId.Value.Should().Be("141981764");
        channelId.ToString().Should().Be("141981764");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1234a")]
    [InlineData("-1")]
    public void Constructor_NonNumericValue_Throws(string value)
    {
        var construct = () => new ChannelId(value);

        construct.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryParse_NonNumericValue_ReturnsFalse() =>
        ChannelId.TryParse("nope", out _).Should().BeFalse();

    [Fact]
    public void TryParse_NumericValue_ReturnsParsedId()
    {
        ChannelId.TryParse("42", out var channelId).Should().BeTrue();

        channelId.Value.Should().Be("42");
    }
}
