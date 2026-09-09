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

    /// <summary>A token that was issued an hour ago and died <paramref name="ago"/> before now.</summary>
    public static string Expired(string channelId, string secretBase64, TimeSpan ago) =>
        Create(channelId, "broadcaster", secretBase64, -ago, issuedAgo: TimeSpan.FromHours(1));

    /// <summary>An unsigned token, the classic algorithm-confusion attempt.</summary>
    public static string WithAlgorithmNone(string channelId)
    {
        var header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        var expires = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var payload = Base64UrlEncoder.Encode(
            $$"""{"channel_id":"{{channelId}}","role":"broadcaster","exp":{{expires}}}""");

        return $"{header}.{payload}.";
    }

    /// <summary>A validly issued token with its signature cut off.</summary>
    public static string WithoutSignature(string channelId, string secretBase64)
    {
        var parts = ForBroadcaster(channelId, secretBase64).Split('.');
        return $"{parts[0]}.{parts[1]}.";
    }

    private static string Create(
        string channelId,
        string role,
        string secretBase64,
        TimeSpan lifetime,
        TimeSpan? issuedAgo = null)
    {
        var issuedAt = DateTime.UtcNow - (issuedAgo ?? TimeSpan.Zero);
        var key = new SymmetricSecurityKey(Convert.FromBase64String(secretBase64));

        return Handler.CreateToken(new SecurityTokenDescriptor
        {
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
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
