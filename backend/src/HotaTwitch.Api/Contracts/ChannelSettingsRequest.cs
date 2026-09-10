namespace HotaTwitch.Api.Contracts;

/// <summary>
/// Body of <c>PUT /v1/config/settings</c>. The scale is nullable so that a body without it is
/// answered 400 rather than read as a zero the streamer never sent.
/// </summary>
internal sealed record ChannelSettingsRequest(decimal? UiScale);
