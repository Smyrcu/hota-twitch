using System.Text.Json.Serialization;

namespace HotaTwitch.Infrastructure.Twitch;

/// <summary>
/// Body of <c>POST https://api.twitch.tv/helix/extensions/pubsub</c>. Twitch takes <c>target</c>
/// as an array even though only one value is meaningful for a channel broadcast.
/// </summary>
internal sealed record PubSubBroadcastRequest(
    [property: JsonPropertyName("target")] IReadOnlyList<string> Target,
    [property: JsonPropertyName("broadcaster_id")] string BroadcasterId,
    [property: JsonPropertyName("is_global_broadcast")] bool IsGlobalBroadcast,
    [property: JsonPropertyName("message")] string Message)
{
    public static PubSubBroadcastRequest ToChannel(string broadcasterId, string message) =>
        new(["broadcast"], broadcasterId, IsGlobalBroadcast: false, message);
}

[JsonSerializable(typeof(PubSubBroadcastRequest))]
internal sealed partial class PubSubJsonContext : JsonSerializerContext;
