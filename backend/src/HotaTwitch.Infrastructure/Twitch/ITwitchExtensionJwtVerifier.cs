namespace HotaTwitch.Infrastructure.Twitch;

public interface ITwitchExtensionJwtVerifier
{
    /// <summary>
    /// Verifies a JWT the extension helper handed to a page. Returns <see langword="null"/> when the
    /// signature, the algorithm or the lifetime does not hold up.
    /// </summary>
    Task<TwitchExtensionClaims?> VerifyAsync(string? token);
}
