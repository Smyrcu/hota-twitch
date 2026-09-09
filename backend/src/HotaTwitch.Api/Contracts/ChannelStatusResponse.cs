namespace HotaTwitch.Api.Contracts;

/// <summary>Body of <c>GET /v1/config/channel</c>.</summary>
internal sealed record ChannelStatusResponse(bool HasToken, string? TokenHint, string? LastStateAt);
