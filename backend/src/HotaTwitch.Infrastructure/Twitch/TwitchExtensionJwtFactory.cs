using HotaTwitch.Application.Abstractions;
using HotaTwitch.Domain.Channels;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HotaTwitch.Infrastructure.Twitch;

internal sealed class TwitchExtensionJwtFactory(
    ExtensionSecret secret,
    IOptions<TwitchOptions> options,
    IClock clock) : ITwitchExtensionJwtFactory
{
    private static readonly JsonWebTokenHandler Handler = new();

    public string CreateExternalToken(ChannelId channelId)
    {
        var settings = options.Value;
        var issuedAt = clock.UtcNow.UtcDateTime;

        var descriptor = new SecurityTokenDescriptor
        {
            IssuedAt = issuedAt,
            Expires = issuedAt + settings.ExternalTokenLifetime,
            SigningCredentials = new SigningCredentials(secret.SigningKey, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["user_id"] = settings.OwnerUserId,
                ["role"] = "external",
                ["channel_id"] = channelId.Value,
                ["pubsub_perms"] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["send"] = new[] { "broadcast" },
                },
            },
        };

        return Handler.CreateToken(descriptor);
    }
}
