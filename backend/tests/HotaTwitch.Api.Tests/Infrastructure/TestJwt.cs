using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HotaTwitch.Api.Tests.Infrastructure;

/// <summary>Mints the JWTs the Twitch extension helper would hand to a page.</summary>
internal static class TestJwt
{
    private static readonly JsonWebTokenHandler Handler = new();

    public static string ForBroadcaster(string channelId, string secretBase64) =>
        Create(channelId, "broadcaster", secretBase64, TimeSpan.FromMinutes(5));

    public static string ForViewer(string channelId, string secretBase64) =>
        Create(channelId, "viewer", secretBase64, TimeSpan.FromMinutes(5));

    public static string Expired(string channelId, string secretBase64) =>
        Create(channelId, "broadcaster", secretBase64, TimeSpan.FromMinutes(-5));

    private static string Create(string channelId, string role, string secretBase64, TimeSpan lifetime)
    {
        var key = new SymmetricSecurityKey(Convert.FromBase64String(secretBase64));

        return Handler.CreateToken(new SecurityTokenDescriptor
        {
            Expires = DateTime.UtcNow + lifetime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["channel_id"] = channelId,
                ["user_id"] = channelId,
                ["opaque_user_id"] = "U" + channelId,
                ["role"] = role,
            },
        });
    }
}
