using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HotaTwitch.Domain.State;

/// <summary>
/// Section 1 of the protocol: a producer that cannot read the HD Mod interface scale leaves
/// <c>display.uiScale</c> out, and the backend fills it in from the channel's settings.
/// </summary>
public static class StateDocumentScale
{
    /// <summary>
    /// Relaying a document the backend does not model must not turn non-ASCII names into six-byte
    /// escapes: filling one field in would then be able to push a document past the PubSub cap.
    /// The result is gzipped and base64-encoded, never written into a page, so the relaxed
    /// encoder's HTML-unsafe characters cannot reach a markup context.
    /// </summary>
    private static readonly JsonWriterOptions RelayOptions =
        new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// A document may legally repeat a property; the validator accepts one and the consumer's own
    /// parser takes the last value. The node model cannot hold such an object — reading a property
    /// off it throws rather than returning a value — so a repeat is refused at the parse, which
    /// puts the document on the verbatim path instead of failing the request.
    /// </summary>
    private static readonly JsonDocumentOptions RepeatedKeysAreNotEditable = new() { AllowDuplicateProperties = false };

    /// <summary>
    /// The document the viewers should receive. A scale the producer sent is left alone, and so is
    /// a document shaped in a way the backend cannot edit with confidence; in both cases the posted
    /// bytes are relayed verbatim. A <c>null</c> scale counts as absent, because the consumer reads
    /// it as 1 either way and the streamer configured something else.
    /// </summary>
    public static ReadOnlyMemory<byte> WithUiScale(ReadOnlyMemory<byte> document, decimal uiScale)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(document.Span, documentOptions: RepeatedKeysAreNotEditable);
        }
        catch (JsonException)
        {
            return document;
        }

        if (root is not JsonObject state)
        {
            return document;
        }

        if (!state.TryGetPropertyValue("display", out var displayNode))
        {
            state["display"] = new JsonObject { ["uiScale"] = JsonValue.Create(uiScale) };
            return Serialize(state);
        }

        if (displayNode is not JsonObject display || display["uiScale"] is not null)
        {
            return document;
        }

        display["uiScale"] = JsonValue.Create(uiScale);
        return Serialize(state);
    }

    private static byte[] Serialize(JsonObject state)
    {
        using var target = new MemoryStream();
        using (var writer = new Utf8JsonWriter(target, RelayOptions))
        {
            state.WriteTo(writer);
        }

        return target.ToArray();
    }
}
