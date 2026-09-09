using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HotaTwitch.Infrastructure.Twitch;

internal sealed class TwitchExtensionJwtVerifier : ITwitchExtensionJwtVerifier
{
    private static readonly JsonWebTokenHandler Handler = new();

    /// <summary>The default of five minutes keeps a JWT usable long after Twitch considers it dead.</summary>
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromSeconds(30);

    private readonly ILogger<TwitchExtensionJwtVerifier> logger;
    private readonly TokenValidationParameters parameters;

    public TwitchExtensionJwtVerifier(ExtensionSecret secret, ILogger<TwitchExtensionJwtVerifier> logger)
    {
        ArgumentNullException.ThrowIfNull(secret);

        this.logger = logger;
        parameters = new TokenValidationParameters
        {
            IssuerSigningKey = secret.SigningKey,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = AllowedClockSkew,
        };
    }

    public async Task<TwitchExtensionClaims?> VerifyAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var result = await Handler.ValidateTokenAsync(token, parameters);
        if (!result.IsValid)
        {
            TwitchExtensionJwtLog.Rejected(logger, result.Exception?.Message);
            return null;
        }

        var jwt = (JsonWebToken)result.SecurityToken;
        if (!jwt.TryGetPayloadValue<string>("role", out var role))
        {
            TwitchExtensionJwtLog.Rejected(logger, "the token carries no role claim");
            return null;
        }

        jwt.TryGetPayloadValue<string>("channel_id", out var channelId);
        jwt.TryGetPayloadValue<string>("user_id", out var userId);

        return new TwitchExtensionClaims(role, channelId, userId);
    }
}

internal static partial class TwitchExtensionJwtLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Rejected a Twitch extension JWT: {Reason}")]
    public static partial void Rejected(ILogger logger, string? reason);
}
