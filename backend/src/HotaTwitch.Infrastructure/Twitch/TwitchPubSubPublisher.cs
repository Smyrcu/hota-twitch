using System.Net.Http.Headers;
using System.Net.Http.Json;
using HotaTwitch.Application.Abstractions;
using HotaTwitch.Domain.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotaTwitch.Infrastructure.Twitch;

/// <summary>Section 3 of the protocol: one broadcast through the Twitch Extensions PubSub endpoint.</summary>
internal sealed class TwitchPubSubPublisher(
    IHttpClientFactory httpClientFactory,
    ITwitchExtensionJwtFactory jwtFactory,
    IOptions<TwitchOptions> options,
    ILogger<TwitchPubSubPublisher> logger) : IPubSubPublisher
{
    /// <summary>Name of the configured client; the publisher is a singleton, so it asks for one per call.</summary>
    public const string HttpClientName = "twitch-helix";

    private const string Path = "helix/extensions/pubsub";
    private const int LoggedResponseCharacters = 500;

    public async Task PublishAsync(ChannelId channelId, string message, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Path)
        {
            Content = JsonContent.Create(
                PubSubBroadcastRequest.ToChannel(channelId.Value, message),
                PubSubJsonContext.Default.PubSubBroadcastRequest),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtFactory.CreateExternalToken(channelId));
        request.Headers.TryAddWithoutValidation("Client-Id", options.Value.ClientId);

        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            TwitchPubSubLog.Broadcast(logger, channelId.Value, message.Length);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        TwitchPubSubLog.Refused(logger, channelId.Value, (int)response.StatusCode, Shorten(body));
        response.EnsureSuccessStatusCode();
    }

    private static string Shorten(string body) =>
        body.Length <= LoggedResponseCharacters ? body : body[..LoggedResponseCharacters];
}

internal static partial class TwitchPubSubLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Broadcast {MessageLength} B to channel {ChannelId}.")]
    public static partial void Broadcast(ILogger logger, string channelId, int messageLength);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Twitch refused the broadcast to channel {ChannelId} with {StatusCode}: {Body}")]
    public static partial void Refused(ILogger logger, string channelId, int statusCode, string body);
}
