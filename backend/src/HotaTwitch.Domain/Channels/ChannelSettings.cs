using System.Diagnostics.CodeAnalysis;

namespace HotaTwitch.Domain.Channels;

/// <summary>
/// What a streamer configures for their own channel. Today that is the HD Mod interface scale,
/// which the backend fills into a state document whose producer could not read it.
/// </summary>
public sealed record ChannelSettings
{
    /// <summary>The smallest scale HD Mod offers.</summary>
    public const decimal MinimumUiScale = 1m;

    /// <summary>The largest scale HD Mod offers.</summary>
    public const decimal MaximumUiScale = 4m;

    /// <summary>Pixel-exact 800x600 widgets: what a channel runs at until the streamer says otherwise.</summary>
    public const decimal DefaultUiScale = 1m;

    private const int UiScaleDecimals = 2;

    private ChannelSettings(decimal uiScale) => UiScale = uiScale;

    public static ChannelSettings Default { get; } = new(DefaultUiScale);

    /// <summary>The HD Mod interface scale the streamer plays with.</summary>
    public decimal UiScale { get; }

    /// <summary>
    /// Builds settings from a scale the streamer sent. Returns <see langword="false"/> for a scale
    /// outside the protocol's range or with more than two decimals; that is what the configuration
    /// API answers 400 to.
    /// </summary>
    public static bool TryCreate(decimal uiScale, [NotNullWhen(true)] out ChannelSettings? settings)
    {
        settings = null;

        var rounded = decimal.Round(uiScale, UiScaleDecimals);
        if (rounded != uiScale || uiScale < MinimumUiScale || uiScale > MaximumUiScale)
        {
            return false;
        }

        settings = new ChannelSettings(rounded);
        return true;
    }

    /// <summary>Rebuilds settings from storage without re-running the creation rules.</summary>
    public static ChannelSettings Restore(decimal uiScale) => new(uiScale);
}
