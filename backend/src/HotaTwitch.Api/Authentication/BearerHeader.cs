using Microsoft.Net.Http.Headers;

namespace HotaTwitch.Api.Authentication;

internal static class BearerHeader
{
    private const string Scheme = "Bearer ";

    /// <summary>Reads the credential out of an <c>Authorization: Bearer &lt;value&gt;</c> header.</summary>
    public static string? Read(HttpRequest request)
    {
        var header = request.Headers[HeaderNames.Authorization].ToString();

        return header.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)
            ? header[Scheme.Length..].Trim()
            : null;
    }
}
