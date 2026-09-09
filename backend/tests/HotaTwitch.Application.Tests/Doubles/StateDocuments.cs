using System.Text;

namespace HotaTwitch.Application.Tests.Doubles;

internal static class StateDocuments
{
    /// <summary>A version 1 document shaped like the example in docs/protocol.md.</summary>
    public static byte[] Valid(long timestamp = 1788907728157) => Encoding.UTF8.GetBytes(Text(timestamp));

    public static string Text(long timestamp = 1788907728157) =>
        $$"""
          {"v":1,"ts":{{timestamp}},"screen":"adventure","date":{"day":1,"week":3,"month":1},
          "display":{"width":2560,"height":1440,"uiScale":1},
          "player":{"id":0,"name":"HaveFunMate","currentHero":184,"heroListTop":0,"townListTop":0},
          "heroes":[],"towns":[]}
          """;
}
