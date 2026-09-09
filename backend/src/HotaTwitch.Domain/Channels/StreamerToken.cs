using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace HotaTwitch.Domain.Channels;

/// <summary>
/// The secret a streamer pastes into <c>hota-twitch.ini</c>: <see cref="Prefix"/> followed by
/// 32 random bytes in base64url. Only its <see cref="Hash"/> and <see cref="Hint"/> are persisted.
/// </summary>
public sealed class StreamerToken
{
    public const string Prefix = "hts_";

    private const int SecretByteCount = 32;
    private const int HintCharacterCount = 2;

    private static readonly int SecretCharacterCount = Base64Url.GetEncodedLength(SecretByteCount);

    private StreamerToken(string value) => Value = value;

    public string Value { get; }

    public static StreamerToken Generate() =>
        new(Prefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretByteCount)));

    public static bool TryParse(string? value, out StreamerToken token)
    {
        token = null!;

        if (value is null || !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var body = value[Prefix.Length..];
        if (body.Length != SecretCharacterCount || !IsBase64Url(body))
        {
            return false;
        }

        token = new StreamerToken(value);
        return true;
    }

    public static TokenHash HashOf(string tokenText) =>
        new(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(tokenText))));

    public TokenHash Hash() => HashOf(Value);

    public string Hint()
    {
        var body = Value[Prefix.Length..];
        var shown = body.Length <= HintCharacterCount ? body : body[..HintCharacterCount];
        return Prefix + shown + '…';
    }

    /// <summary>Deliberately opaque so that a stray log line cannot leak the secret.</summary>
    public override string ToString() => Hint();

    private static bool IsBase64Url(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))
            {
                return false;
            }
        }

        return true;
    }
}
