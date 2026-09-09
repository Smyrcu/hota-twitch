namespace HotaTwitch.Domain.Channels;

/// <summary>Lower-case hex SHA-256 of a streamer token. The token itself is never stored.</summary>
public readonly record struct TokenHash(string Value)
{
    public override string ToString() => Value;
}
