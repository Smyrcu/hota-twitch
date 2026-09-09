using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using HotaTwitch.Api.Tests.Infrastructure;
using HotaTwitch.Domain.Broadcasting;
using HotaTwitch.Domain.Channels;
using HotaTwitch.Infrastructure.Twitch;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace HotaTwitch.Api.Tests.Twitch;

public sealed class TwitchPubSubPublisherTests : IDisposable
{
    private const string Message = "gz:H4sIAAAAAAAAA6tWKkstKlGyUkrOzytJzStRqgUAAAD__wMA";

    private static readonly ChannelId Channel = new(HotaTwitchApiFactory.ChannelId);

    private readonly IOptions<TwitchOptions> options = Options.Create(new TwitchOptions
    {
        ClientId = HotaTwitchApiFactory.ClientId,
        ExtensionSecret = HotaTwitchApiFactory.ExtensionSecretBase64,
        OwnerUserId = HotaTwitchApiFactory.OwnerUserId,
    });

    private CapturingHttpMessageHandler handler = new(HttpStatusCode.NoContent);
    private HttpClient? httpClient;

    public void Dispose()
    {
        httpClient?.Dispose();
        handler.Dispose();
    }

    [Fact]
    public async Task PublishAsync_Always_PostsToTheHelixExtensionPubSubEndpoint()
    {
        await PublishAsync();

        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri.Should().Be(new Uri("https://api.twitch.tv/helix/extensions/pubsub"));
    }

    [Fact]
    public async Task PublishAsync_Always_SendsTheClientIdAndAnExternalJwt()
    {
        await PublishAsync();

        handler.Request!.Headers.GetValues("Client-Id").Should().Equal(HotaTwitchApiFactory.ClientId);
        var authorization = handler.Request.Headers.Authorization;
        authorization!.Scheme.Should().Be("Bearer");
        PayloadOf(authorization.Parameter!).GetProperty("role").GetString().Should().Be("external");
    }

    [Fact]
    public async Task PublishAsync_Always_SendsABroadcastForTheChannelWithTheMessageVerbatim()
    {
        await PublishAsync();

        var body = JsonDocument.Parse(handler.RequestBody).RootElement;
        body.GetProperty("target").EnumerateArray().Select(target => target.GetString()).Should().Equal("broadcast");
        body.GetProperty("broadcaster_id").GetString().Should().Be(HotaTwitchApiFactory.ChannelId);
        body.GetProperty("is_global_broadcast").GetBoolean().Should().BeFalse();
        body.GetProperty("message").GetString().Should().Be(Message);
    }

    [Fact]
    public async Task PublishAsync_MessageAtTheCap_IsStillSent()
    {
        var atTheCap = BroadcastMessage.Prefix + new string('A', BroadcastPolicy.MaxEncodedMessageBytes - BroadcastMessage.Prefix.Length);

        await PublishAsync(atTheCap);

        JsonDocument.Parse(handler.RequestBody).RootElement.GetProperty("message").GetString()!
            .Should().HaveLength(BroadcastPolicy.MaxEncodedMessageBytes);
    }

    [Fact]
    public async Task PublishAsync_TwitchRefusesTheCall_Throws()
    {
        handler.Dispose();
        handler = new CapturingHttpMessageHandler(HttpStatusCode.Unauthorized, """{"error":"Unauthorized"}""");

        var publish = async () => await PublishAsync();

        await publish.Should().ThrowAsync<HttpRequestException>();
    }

    private async Task PublishAsync(string message = Message)
    {
        httpClient?.Dispose();
        httpClient = new HttpClient(handler, disposeHandler: false) { BaseAddress = options.Value.HelixBaseAddress };

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(TwitchPubSubPublisher.HttpClientName).Returns(httpClient);

        var publisher = new TwitchPubSubPublisher(
            httpClientFactory,
            new TwitchExtensionJwtFactory(new ExtensionSecret(options), options, new TestClock()),
            options,
            NullLogger<TwitchPubSubPublisher>.Instance);

        await publisher.PublishAsync(Channel, message, TestContext.Current.CancellationToken);
    }

    private static JsonElement PayloadOf(string token) =>
        JsonDocument.Parse(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(token.Split('.')[1])).RootElement;
}
