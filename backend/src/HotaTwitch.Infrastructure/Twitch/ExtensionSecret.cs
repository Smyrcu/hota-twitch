using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HotaTwitch.Infrastructure.Twitch;

/// <summary>
/// The signing key shared with Twitch. The developer console shows the secret in base64; both the
/// JWT the backend signs and the JWT the extension helper issues use the decoded bytes.
/// </summary>
internal sealed class ExtensionSecret
{
    /// <summary>HS256 refuses keys shorter than 128 bits.</summary>
    private const int MinimumByteCount = 16;

    public ExtensionSecret(IOptions<TwitchOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(options.Value.ExtensionSecret);
        }
        catch (FormatException exception)
        {
            throw new OptionsValidationException(
                nameof(TwitchOptions),
                typeof(TwitchOptions),
                [$"TWITCH_EXTENSION_SECRET is not base64: {exception.Message}"]);
        }

        if (decoded.Length < MinimumByteCount)
        {
            throw new OptionsValidationException(
                nameof(TwitchOptions),
                typeof(TwitchOptions),
                [$"TWITCH_EXTENSION_SECRET decodes to {decoded.Length} bytes; HS256 needs at least {MinimumByteCount}."]);
        }

        SigningKey = new SymmetricSecurityKey(decoded);
    }

    public SymmetricSecurityKey SigningKey { get; }
}
