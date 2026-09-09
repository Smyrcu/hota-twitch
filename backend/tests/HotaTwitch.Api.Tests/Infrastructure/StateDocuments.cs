using System.Text;

namespace HotaTwitch.Api.Tests.Infrastructure;

internal static class StateDocuments
{
    public static string Text(long timestamp = 1788907728157) =>
        $$"""
          {"v":1,"ts":{{timestamp}},"screen":"adventure","date":{"day":1,"week":3,"month":1},
          "display":{"width":2560,"height":1440,"uiScale":1},
          "player":{"id":0,"name":"HaveFunMate","currentHero":184,"heroListTop":0,"townListTop":0},
          "heroes":[],"towns":[]}
          """;

    public static byte[] Bytes(long timestamp = 1788907728157) => Encoding.UTF8.GetBytes(Text(timestamp));
}
