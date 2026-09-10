using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using HotaTwitch.Api.Tests.Infrastructure;
using HotaTwitch.Domain.Broadcasting;
using HotaTwitch.Domain.Channels;
using Xunit;

namespace HotaTwitch.Api.Tests.Endpoints;

public sealed class StateEndpointTests : IAsyncLifetime
{
    private static readonly Uri State = new("/v1/state", UriKind.Relative);
    private static readonly Uri Token = new("/v1/config/token", UriKind.Relative);
    private static readonly Uri Settings = new("/v1/config/settings", UriKind.Relative);

    private readonly HotaTwitchApiFactory factory = new();
    private string streamerToken = string.Empty;

    public async ValueTask InitializeAsync()
    {
        using var broadcaster = factory.CreateBroadcasterClient();
        using var response = await broadcaster.PostAsync(Token, content: null, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        streamerToken = body.GetProperty("token").GetString()!;
    }

    public ValueTask DisposeAsync()
    {
        factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task PostState_ValidTokenAndDocument_IsAccepted()
    {
        var status = await PostAsync(streamerToken, StateDocuments.Bytes());

        status.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PostState_ValidDocument_BroadcastsItToTheChannel()
    {
        await PostAsync(streamerToken, StateDocuments.Bytes());
        await factory.DrainBroadcastsAsync();

        factory.Publisher.Broadcasts.Should().ContainSingle();
        var (channel, message) = factory.Publisher.Broadcasts[0];
        channel.Value.Should().Be(HotaTwitchApiFactory.ChannelId);
        Gunzip(message).Should().Be(StateDocuments.Text());
    }

    [Fact]
    public async Task PostState_ThreeDocumentsInsideOneSecond_RefusesTheThirdAndBroadcastsTheSecond()
    {
        (await PostAsync(streamerToken, StateDocuments.Bytes(1))).Should().Be(HttpStatusCode.Accepted);
        (await PostAsync(streamerToken, StateDocuments.Bytes(2))).Should().Be(HttpStatusCode.Accepted);

        (await PostAsync(streamerToken, StateDocuments.Bytes(3))).Should().Be(HttpStatusCode.TooManyRequests);

        await factory.DrainBroadcastsAsync();
        factory.Publisher.Broadcasts.Should().ContainSingle();
        Gunzip(factory.Publisher.Broadcasts[0].Message).Should().Be(StateDocuments.Text(2));
    }

    [Fact]
    public async Task PostState_AfterTheRateWindow_IsAcceptedAgain()
    {
        await PostAsync(streamerToken, StateDocuments.Bytes(1));
        await PostAsync(streamerToken, StateDocuments.Bytes(2));
        factory.Clock.Advance(TimeSpan.FromSeconds(1));

        var status = await PostAsync(streamerToken, StateDocuments.Bytes(3));

        status.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PostState_RateLimited_AsksTheProducerToRetry()
    {
        await PostAsync(streamerToken, StateDocuments.Bytes(1));
        await PostAsync(streamerToken, StateDocuments.Bytes(2));

        using var response = await SendAsync(streamerToken, new ByteArrayContent(StateDocuments.Bytes(3)));

        response.Headers.RetryAfter.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("hts_ZmFrZS10b2tlbi10aGF0LW5vLWNoYW5uZWwtaGFz")]
    public async Task PostState_MalformedToken_IsUnauthorized(string? bearer)
    {
        var status = await PostAsync(bearer, StateDocuments.Bytes());

        status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostState_WellFormedTokenThatNoChannelHolds_IsUnauthorized()
    {
        var status = await PostAsync(StreamerToken.Generate().Value, StateDocuments.Bytes());

        status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostState_UnknownToken_AnnouncesTheBearerScheme()
    {
        using var response = await SendAsync(bearer: null, new ByteArrayContent(StateDocuments.Bytes()));

        response.Headers.WwwAuthenticate.Should().ContainSingle(header => header.Scheme == "Bearer");
    }

    [Fact]
    public async Task PostState_BodyOverTheSizeLimit_IsPayloadTooLarge()
    {
        var status = await PostAsync(streamerToken, new byte[BroadcastPolicy.MaxStateDocumentBytes + 1]);

        status.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task PostState_BodyOverTheSizeLimitWithoutContentLength_IsPayloadTooLarge()
    {
        using var content = new ChunkedContent(new byte[BroadcastPolicy.MaxStateDocumentBytes + 1]);

        using var response = await SendAsync(streamerToken, content);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"hello":"world"}""")]
    [InlineData("""{"v":2,"ts":1,"screen":"adventure","player":{"id":0},"heroes":[],"towns":[]}""")]
    [InlineData("""{"v":1,"ts":1,"screen":"lobby","player":{"id":0},"heroes":[],"towns":[]}""")]
    public async Task PostState_BodyThatIsNotAStateDocument_IsBadRequest(string body)
    {
        var status = await PostAsync(streamerToken, Encoding.UTF8.GetBytes(body));

        status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostState_DocumentThatDoesNotFitAPubSubMessage_IsAcceptedButNotBroadcast()
    {
        var noise = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16 * 1024));
        var document = StateDocuments.Text().Replace("\"towns\":[]", $"\"towns\":[\"{noise}\"]", StringComparison.Ordinal);

        var status = await PostAsync(streamerToken, Encoding.UTF8.GetBytes(document));
        await factory.DrainBroadcastsAsync();

        status.Should().Be(HttpStatusCode.Accepted);
        factory.Publisher.Broadcasts.Should().BeEmpty();
    }

    [Fact]
    public async Task PostState_DocumentWithoutAScale_BroadcastsTheScaleTheStreamerConfigured()
    {
        await ConfigureScaleAsync("""{"uiScale":2.5}""");

        await PostAsync(streamerToken, StateDocuments.WithoutUiScale());
        await factory.DrainBroadcastsAsync();

        factory.Publisher.Broadcasts.Should().ContainSingle();
        using var relayed = JsonDocument.Parse(Gunzip(factory.Publisher.Broadcasts[0].Message));
        relayed.RootElement.GetProperty("display").GetProperty("uiScale").GetDecimal().Should().Be(2.5m);
        relayed.RootElement.GetProperty("display").GetProperty("width").GetInt32().Should().Be(2560);
    }

    [Fact]
    public async Task PostState_DocumentThatCarriesAScale_BroadcastsThePostedDocumentUntouched()
    {
        await ConfigureScaleAsync("""{"uiScale":2.5}""");

        await PostAsync(streamerToken, StateDocuments.Bytes());
        await factory.DrainBroadcastsAsync();

        factory.Publisher.Broadcasts.Should().ContainSingle();
        Gunzip(factory.Publisher.Broadcasts[0].Message).Should().Be(StateDocuments.Text());
    }

    private async Task ConfigureScaleAsync(string body)
    {
        using var broadcaster = factory.CreateBroadcasterClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await broadcaster.PutAsync(Settings, content, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<HttpStatusCode> PostAsync(string? bearer, byte[] document)
    {
        using var content = new ByteArrayContent(document);
        using var response = await SendAsync(bearer, content);
        return response.StatusCode;
    }

    private async Task<HttpResponseMessage> SendAsync(string? bearer, HttpContent content)
    {
        using var client = factory.CreateClient();
        if (bearer is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return await client.PostAsync(State, content, TestContext.Current.CancellationToken);
    }

    private static string Gunzip(string message)
    {
        using var source = new MemoryStream(Convert.FromBase64String(message[BroadcastMessage.Prefix.Length..]));
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
