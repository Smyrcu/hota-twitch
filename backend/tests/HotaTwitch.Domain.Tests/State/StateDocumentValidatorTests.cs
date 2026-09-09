using System.Text;
using AwesomeAssertions;
using HotaTwitch.Domain.State;
using Xunit;

namespace HotaTwitch.Domain.Tests.State;

public sealed class StateDocumentValidatorTests
{
    private const string Valid = """
        {"v":1,"ts":1788907728157,"screen":"adventure","date":{"day":1,"week":3,"month":1},
        "display":{"width":2560,"height":1440,"uiScale":1},
        "player":{"id":0,"name":"HaveFunMate","currentHero":184,"heroListTop":0,"townListTop":0},
        "heroes":[],"towns":[]}
        """;

    [Fact]
    public void TryValidate_DocumentFromTheProtocol_Succeeds()
    {
        StateDocumentValidator.TryValidate(Encoding.UTF8.GetBytes(Valid), out var error).Should().BeTrue();

        error.Should().BeNull();
    }

    [Theory]
    [InlineData("none")]
    [InlineData("town")]
    [InlineData("combat")]
    [InlineData("other")]
    public void TryValidate_EveryScreenFromTheProtocol_Succeeds(string screen)
    {
        var document = Valid.Replace("\"adventure\"", $"\"{screen}\"", System.StringComparison.Ordinal);

        StateDocumentValidator.TryValidate(Encoding.UTF8.GetBytes(document), out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("", "empty body")]
    [InlineData("not json at all", "not JSON")]
    [InlineData("[]", "root is an array")]
    [InlineData("""{"ts":1,"screen":"adventure","player":{"id":0},"heroes":[],"towns":[]}""", "no version")]
    [InlineData("""{"v":2,"ts":1,"screen":"adventure","player":{"id":0},"heroes":[],"towns":[]}""", "wrong version")]
    [InlineData("""{"v":1,"screen":"adventure","player":{"id":0},"heroes":[],"towns":[]}""", "no timestamp")]
    [InlineData("""{"v":1,"ts":1,"screen":"lobby","player":{"id":0},"heroes":[],"towns":[]}""", "unknown screen")]
    [InlineData("""{"v":1,"ts":1,"screen":"adventure","heroes":[],"towns":[]}""", "no player")]
    [InlineData("""{"v":1,"ts":1,"screen":"adventure","player":{},"heroes":[],"towns":[]}""", "player without id")]
    [InlineData("""{"v":1,"ts":1,"screen":"adventure","player":{"id":0},"towns":[]}""", "no heroes")]
    [InlineData("""{"v":1,"ts":1,"screen":"adventure","player":{"id":0},"heroes":{},"towns":[]}""", "heroes not an array")]
    [InlineData("""{"v":1,"ts":1,"screen":"adventure","player":{"id":0},"heroes":[]}""", "no towns")]
    [InlineData("""{"v":1,"ts":1,"screen":"adventure","player":{"id":0},"heroes":[],"towns":[]} trailing""", "trailing content")]
    public void TryValidate_MalformedDocument_FailsWithAReason(string document, string because)
    {
        StateDocumentValidator.TryValidate(Encoding.UTF8.GetBytes(document), out var error).Should().BeFalse(because);

        error.Should().NotBeNullOrWhiteSpace();
    }
}
