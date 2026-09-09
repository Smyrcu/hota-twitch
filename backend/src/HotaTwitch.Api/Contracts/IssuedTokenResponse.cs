namespace HotaTwitch.Api.Contracts;

/// <summary>Body of <c>POST /v1/config/token</c>; the only place a plain token is ever returned.</summary>
internal sealed record IssuedTokenResponse(string Token);
