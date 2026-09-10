using HotaTwitch.Application.Abstractions;
using HotaTwitch.Domain.Channels;
using Microsoft.Extensions.Logging;

namespace HotaTwitch.Application.Channels;

/// <summary>Stores what the streamer configured, creating the channel when it has no row yet.</summary>
public sealed class UpdateChannelSettingsHandler(
    IChannelRepository channels,
    IClock clock,
    ILogger<UpdateChannelSettingsHandler> logger)
{
    public async Task HandleAsync(ChannelId channelId, ChannelSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var channel = await channels.FindByIdAsync(channelId, cancellationToken)
            ?? Channel.Create(channelId, clock.UtcNow);

        channel.UpdateSettings(settings);

        await channels.SaveSettingsAsync(channel, cancellationToken);
        UpdateChannelSettingsLog.SettingsUpdated(logger, channelId.Value, settings.UiScale);
    }
}

internal static partial class UpdateChannelSettingsLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Channel {ChannelId} now plays at interface scale {UiScale}.")]
    public static partial void SettingsUpdated(ILogger logger, string channelId, decimal uiScale);
}
