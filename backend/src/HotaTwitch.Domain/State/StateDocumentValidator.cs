using System.Text.Json;

namespace HotaTwitch.Domain.State;

/// <summary>
/// Checks that a posted body is a state document of the protocol version this backend relays.
/// The backend does not model the payload; it only guarantees that what it forwards is a
/// version 1 document with the fields the overlay needs to key off.
/// </summary>
public static class StateDocumentValidator
{
    public const int SupportedVersion = 1;

    private static readonly string[] KnownScreens = ["none", "adventure", "town", "combat", "other"];

    public static bool TryValidate(ReadOnlySpan<byte> document, out string? error)
    {
        if (document.IsEmpty)
        {
            error = "the body is empty";
            return false;
        }

        JsonDocument parsed;
        try
        {
            var reader = new Utf8JsonReader(document);
            parsed = JsonDocument.ParseValue(ref reader);

            if (reader.BytesConsumed != document.Length && HasTrailingContent(document[(int)reader.BytesConsumed..]))
            {
                parsed.Dispose();
                error = "the body carries content after the state document";
                return false;
            }
        }
        catch (JsonException exception)
        {
            error = $"the body is not JSON: {exception.Message}";
            return false;
        }

        using (parsed)
        {
            return TryValidateRoot(parsed.RootElement, out error);
        }
    }

    private static bool TryValidateRoot(JsonElement root, out string? error)
    {
        if (root.ValueKind is not JsonValueKind.Object)
        {
            error = "the state document is not a JSON object";
            return false;
        }

        if (!root.TryGetProperty("v"u8, out var version) || version.ValueKind is not JsonValueKind.Number ||
            !version.TryGetInt32(out var versionNumber) || versionNumber != SupportedVersion)
        {
            error = $"\"v\" is missing or is not {SupportedVersion}";
            return false;
        }

        if (!root.TryGetProperty("ts"u8, out var timestamp) || timestamp.ValueKind is not JsonValueKind.Number)
        {
            error = "\"ts\" is missing or is not a number";
            return false;
        }

        if (!root.TryGetProperty("screen"u8, out var screen) || screen.ValueKind is not JsonValueKind.String ||
            Array.IndexOf(KnownScreens, screen.GetString()) < 0)
        {
            error = "\"screen\" is missing or is not one of the values in the protocol";
            return false;
        }

        if (!root.TryGetProperty("player"u8, out var player) || player.ValueKind is not JsonValueKind.Object ||
            !player.TryGetProperty("id"u8, out var playerId) || playerId.ValueKind is not JsonValueKind.Number)
        {
            error = "\"player\" is missing or has no numeric \"id\"";
            return false;
        }

        if (!IsArray(root, "heroes"u8))
        {
            error = "\"heroes\" is missing or is not an array";
            return false;
        }

        if (!IsArray(root, "towns"u8))
        {
            error = "\"towns\" is missing or is not an array";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsArray(JsonElement root, ReadOnlySpan<byte> name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.Array;

    private static bool HasTrailingContent(ReadOnlySpan<byte> rest)
    {
        foreach (var value in rest)
        {
            if (value is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
            {
                return true;
            }
        }

        return false;
    }
}
