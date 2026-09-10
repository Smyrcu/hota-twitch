using AwesomeAssertions;
using HotaTwitch.Domain.Channels;
using Xunit;

namespace HotaTwitch.Domain.Tests.Channels;

public sealed class ChannelSettingsTests
{
    [Fact]
    public void Default_Always_IsThePixelExactScale() =>
        ChannelSettings.Default.UiScale.Should().Be(1m);

    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    [InlineData(3.99)]
    [InlineData(4)]
    public void TryCreate_ScaleInsideTheProtocolsRange_Succeeds(decimal uiScale)
    {
        ChannelSettings.TryCreate(uiScale, out var settings).Should().BeTrue();

        settings!.UiScale.Should().Be(uiScale);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(0.99)]
    [InlineData(-1)]
    [InlineData(4.01)]
    [InlineData(5)]
    public void TryCreate_ScaleOutsideTheProtocolsRange_Fails(decimal uiScale)
    {
        ChannelSettings.TryCreate(uiScale, out var settings).Should().BeFalse();

        settings.Should().BeNull();
    }

    [Theory]
    [InlineData(1.005)]
    [InlineData(1.333)]
    [InlineData(3.9999)]
    public void TryCreate_ScaleWithMoreThanTwoDecimals_Fails(decimal uiScale) =>
        ChannelSettings.TryCreate(uiScale, out _).Should().BeFalse();

    [Fact]
    public void TryCreate_ScaleWrittenWithTrailingZeros_IsTheSameScale()
    {
        ChannelSettings.TryCreate(1.5000m, out var padded).Should().BeTrue();
        ChannelSettings.TryCreate(1.5m, out var plain).Should().BeTrue();

        padded.Should().Be(plain);
    }

    /// <summary>A row written under an older rule still has to load rather than fail the request.</summary>
    [Fact]
    public void Restore_ScaleTryCreateWouldRefuse_IsStillTakenFromStorage() =>
        ChannelSettings.Restore(0.5m).UiScale.Should().Be(0.5m);
}
