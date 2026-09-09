using System.ComponentModel.DataAnnotations;

namespace HotaTwitch.Infrastructure.Twitch;

public sealed class TwitchOptions
{
    /// <summary>The extension's client id, sent as the <c>Client-Id</c> header.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>The extension secret exactly as the developer console shows it, in base64.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ExtensionSecret { get; set; } = string.Empty;

    /// <summary>Twitch user id of the extension owner; it goes into the external JWT's user_id claim.</summary>
    [Required(AllowEmptyStrings = false)]
    public string OwnerUserId { get; set; } = string.Empty;

    /// <summary>Twitch API root. Overridden in tests; never in production.</summary>
    public Uri HelixBaseAddress { get; set; } = new("https://api.twitch.tv/");

    /// <summary>How long an external JWT stays valid. Twitch only needs it for the one call.</summary>
    public TimeSpan ExternalTokenLifetime { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long one broadcast may take. It is short on purpose: the dispatcher publishes channels
    /// side by side, but a call that hangs still delays the state a viewer sees.
    /// </summary>
    public TimeSpan HelixTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
