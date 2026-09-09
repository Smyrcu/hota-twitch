using System.Diagnostics.CodeAnalysis;

namespace HotaTwitch.Domain.Channels;

/// <summary>Twitch channel (broadcaster) id, a decimal number carried as text.</summary>
public readonly record struct ChannelId
{
    public ChannelId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!IsNumeric(value))
        {
            throw new ArgumentException("A Twitch channel id contains decimal digits only.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public static bool TryParse([NotNullWhen(true)] string? value, out ChannelId channelId)
    {
        if (string.IsNullOrEmpty(value) || !IsNumeric(value))
        {
            channelId = default;
            return false;
        }

        channelId = new ChannelId(value);
        return true;
    }

    public override string ToString() => Value;

    private static bool IsNumeric(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
