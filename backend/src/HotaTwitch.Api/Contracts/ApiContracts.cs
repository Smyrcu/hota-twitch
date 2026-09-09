namespace HotaTwitch.Api.Contracts;

/// <summary>Bodies of the endpoints in sections 4 and 5 of the protocol.</summary>
internal sealed record ChannelStatusResponse(bool HasToken, string? TokenHint, string? LastStateAt);

internal sealed record IssuedTokenResponse(string Token);

internal sealed record HealthResponse(bool Ok);
