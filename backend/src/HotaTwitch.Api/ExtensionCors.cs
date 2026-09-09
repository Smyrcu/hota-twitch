namespace HotaTwitch.Api;

/// <summary>
/// The configuration page is served by Twitch from <c>https://&lt;client-id&gt;.ext-twitch.tv</c>, so every
/// call to <c>/v1/config</c> is cross-origin and its Authorization header forces a preflight.
/// The producer's <c>/v1/state</c> is not called from a browser and stays outside the policy.
/// </summary>
internal static class ExtensionCors
{
    public const string PolicyName = "twitch-extension";

    private const string OriginsKey = "TWITCH_ALLOWED_ORIGINS";
    private const string ClientIdKey = "TWITCH_CLIENT_ID";

    public static IServiceCollection AddExtensionCors(this IServiceCollection services, IConfiguration configuration) =>
        services.AddCors(cors => cors.AddPolicy(
            PolicyName,
            policy => policy
                .WithOrigins(AllowedOrigins(configuration))
                .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Delete)
                .WithHeaders(Microsoft.Net.Http.Headers.HeaderNames.Authorization)));

    /// <summary>
    /// The extension's own origin, plus whatever <c>TWITCH_ALLOWED_ORIGINS</c> adds for the local
    /// developer rig Twitch serves on <c>https://localhost:8080</c>.
    /// </summary>
    private static string[] AllowedOrigins(IConfiguration configuration)
    {
        var clientId = configuration[ClientIdKey];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException($"{ClientIdKey} is missing, so the extension's own origin cannot be allowed.");
        }

        var origins = new List<string> { $"https://{clientId}.ext-twitch.tv" };
        var extra = configuration[OriginsKey];
        if (!string.IsNullOrWhiteSpace(extra))
        {
            origins.AddRange(extra.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return [.. origins];
    }
}
