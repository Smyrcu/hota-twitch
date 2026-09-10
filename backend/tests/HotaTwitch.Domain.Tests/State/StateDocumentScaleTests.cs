using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using HotaTwitch.Domain.State;
using Xunit;

namespace HotaTwitch.Domain.Tests.State;

public sealed class StateDocumentScaleTests
{
    [Fact]
    public void WithUiScale_DisplayCarriesAScale_RelaysThePostedBytes()
    {
        var document = Utf8("""{"v":1,"display":{"width":2560,"height":1440,"uiScale":2}}""");

        var relayed = StateDocumentScale.WithUiScale(document, 1.5m);

        Text(relayed).Should().Be(Text(document));
    }

    [Fact]
    public void WithUiScale_DisplayWithoutAScale_FillsInTheChannelsScale()
    {
        var document = Utf8("""{"v":1,"display":{"width":2560,"height":1440}}""");

        var relayed = StateDocumentScale.WithUiScale(document, 1.5m);

        UiScaleOf(relayed).Should().Be(1.5m);
    }

    [Fact]
    public void WithUiScale_DisplayWithoutAScale_KeepsEverythingElse()
    {
        var document = Utf8(
            """{"v":1,"ts":1788907728157,"display":{"width":2560,"height":1440},"heroes":[{"name":"Todd"}]}""");

        var relayed = StateDocumentScale.WithUiScale(document, 2m);

        using var parsed = JsonDocument.Parse(Text(relayed));
        parsed.RootElement.GetProperty("ts").GetInt64().Should().Be(1788907728157);
        parsed.RootElement.GetProperty("display").GetProperty("width").GetInt32().Should().Be(2560);
        parsed.RootElement.GetProperty("heroes")[0].GetProperty("name").GetString().Should().Be("Todd");
    }

    [Fact]
    public void WithUiScale_ScaleExplicitlyNull_FillsItIn()
    {
        var document = Utf8("""{"v":1,"display":{"width":2560,"height":1440,"uiScale":null}}""");

        var relayed = StateDocumentScale.WithUiScale(document, 2.25m);

        UiScaleOf(relayed).Should().Be(2.25m);
    }

    [Fact]
    public void WithUiScale_NoDisplayAtAll_AddsOneCarryingTheScale()
    {
        var document = Utf8("""{"v":1,"screen":"adventure"}""");

        var relayed = StateDocumentScale.WithUiScale(document, 3m);

        UiScaleOf(relayed).Should().Be(3m);
    }

    [Theory]
    [InlineData("""{"v":1,"display":5}""")]
    [InlineData("""{"v":1,"display":null}""")]
    [InlineData("""{"v":1,"display":[]}""")]
    public void WithUiScale_DisplayThatIsNotAnObject_RelaysThePostedBytes(string json)
    {
        var document = Utf8(json);

        var relayed = StateDocumentScale.WithUiScale(document, 1.5m);

        Text(relayed).Should().Be(json);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    [InlineData("\"a string\"")]
    public void WithUiScale_BodyThatIsNotAStateObject_RelaysThePostedBytes(string body)
    {
        var document = Utf8(body);

        var relayed = StateDocumentScale.WithUiScale(document, 1.5m);

        Text(relayed).Should().Be(body);
    }

    [Fact]
    public void WithUiScale_NonAsciiNames_AreNotBlownUpIntoEscapes()
    {
        var document = Utf8("""{"v":1,"display":{"width":800},"towns":[{"name":"Żółć"}]}""");

        var relayed = StateDocumentScale.WithUiScale(document, 2m);

        Text(relayed).Should().Contain("Żółć", "escaping every name would push documents past the PubSub cap");
    }

    [Fact]
    public void WithUiScale_LargeNumbers_KeepTheirExactValue()
    {
        var document = Utf8("""{"v":1,"ts":1788907728157,"display":{"width":800},"exp":9007199254740993}""");

        var relayed = StateDocumentScale.WithUiScale(document, 2m);

        Text(relayed).Should().Contain("9007199254740993");
    }

    [Theory]
    [InlineData("""{"v":1,"ts":1,"ts":2,"display":{"width":800}}""")]
    [InlineData("""{"v":1,"display":{"width":800},"display":{"width":1024}}""")]
    [InlineData("""{"v":1,"display":{"width":800,"width":1024}}""")]
    public void WithUiScale_DocumentThatRepeatsAProperty_RelaysThePostedBytes(string json)
    {
        var document = Utf8(json);

        var relayed = StateDocumentScale.WithUiScale(document, 1.5m);

        Text(relayed).Should().Be(json);
    }

    private static ReadOnlyMemory<byte> Utf8(string json) => Encoding.UTF8.GetBytes(json);

    private static string Text(ReadOnlyMemory<byte> document) => Encoding.UTF8.GetString(document.Span);

    private static decimal UiScaleOf(ReadOnlyMemory<byte> document)
    {
        using var parsed = JsonDocument.Parse(Text(document));
        return parsed.RootElement.GetProperty("display").GetProperty("uiScale").GetDecimal();
    }
}
