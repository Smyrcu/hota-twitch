using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using HotaTwitch.Api.Tests.Infrastructure;
using HotaTwitch.Domain.Channels;
using Xunit;

namespace HotaTwitch.Api.Tests.Endpoints;

public sealed class ConfigEndpointTests : IDisposable
{
    private static readonly Uri Channel = new("/v1/config/channel", UriKind.Relative);
    private static readonly Uri Token = new("/v1/config/token", UriKind.Relative);
    private static readonly Uri Settings = new("/v1/config/settings", UriKind.Relative);

    private readonly HotaTwitchApiFactory factory = new();

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task GetChannel_WithoutAuthorization_IsUnauthorized()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Channel, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetChannel_JwtSignedWithAnotherSecret_IsUnauthorized()
    {
        const string OtherSecret = "b3RoZXItc2VjcmV0LTMyLWJ5dGVzLWxvbmctZm9yLWhzMjU2";
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.ForBroadcaster(HotaTwitchApiFactory.ChannelId, OtherSecret));

        using var response = await client.GetAsync(Channel, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(3600)]
    public async Task GetChannel_ExpiredJwt_IsUnauthorized(int secondsAgo)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwt.Expired(
                HotaTwitchApiFactory.ChannelId,
                HotaTwitchApiFactory.ExtensionSecretBase64,
                TimeSpan.FromSeconds(secondsAgo)));

        using var response = await client.GetAsync(Channel, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetChannel_JwtExpiredInsideTheClockSkew_IsStillAccepted()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwt.Expired(
                HotaTwitchApiFactory.ChannelId,
                HotaTwitchApiFactory.ExtensionSecretBase64,
                TimeSpan.FromSeconds(1)));

        using var response = await client.GetAsync(Channel, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "a small clock skew is tolerated on purpose");
    }

    [Fact]
    public async Task GetChannel_UnsignedJwt_IsUnauthorized()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.WithAlgorithmNone(HotaTwitchApiFactory.ChannelId));

        using var response = await client.GetAsync(Channel, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetChannel_JwtWithTheSignatureStripped_IsUnauthorized()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwt.WithoutSignature(HotaTwitchApiFactory.ChannelId, HotaTwitchApiFactory.ExtensionSecretBase64));

        using var response = await client.GetAsync(Channel, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PreflightForToken_FromTheExtensionOrigin_IsAllowed()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, Token);
        request.Headers.Add("Origin", $"https://{HotaTwitchApiFactory.ClientId}.ext-twitch.tv");
        request.Headers.Add("Access-Control-Request-Method", "DELETE");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().Equal($"https://{HotaTwitchApiFactory.ClientId}.ext-twitch.tv");
        response.Headers.GetValues("Access-Control-Allow-Methods").Should().Contain(methods => methods.Contains("DELETE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PreflightForToken_FromAnotherOrigin_IsNotAllowed()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, Token);
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "DELETE");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task GetChannel_ViewerJwt_IsForbidden()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwt.ForViewer(HotaTwitchApiFactory.ChannelId, HotaTwitchApiFactory.ExtensionSecretBase64));

        using var response = await client.GetAsync(Channel, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetChannel_BroadcasterWithoutAToken_ReportsNoToken()
    {
        using var client = factory.CreateBroadcasterClient();

        var status = await client.GetFromJsonAsync<JsonElement>(Channel, TestContext.Current.CancellationToken);

        status.GetProperty("hasToken").GetBoolean().Should().BeFalse();
        status.GetProperty("tokenHint").ValueKind.Should().Be(JsonValueKind.Null);
        status.GetProperty("lastStateAt").ValueKind.Should().Be(JsonValueKind.Null);
        status.GetProperty("settings").GetProperty("uiScale").GetDecimal().Should().Be(1m);
    }

    [Fact]
    public async Task PostToken_Broadcaster_ReturnsAStreamerTokenOnce()
    {
        using var client = factory.CreateBroadcasterClient();

        var issued = await IssueTokenAsync(client);

        issued.Should().StartWith(StreamerToken.Prefix);
        StreamerToken.TryParse(issued, out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetChannel_AfterIssuingAToken_ReportsTheHint()
    {
        using var client = factory.CreateBroadcasterClient();
        var issued = await IssueTokenAsync(client);

        var status = await client.GetFromJsonAsync<JsonElement>(Channel, TestContext.Current.CancellationToken);

        status.GetProperty("hasToken").GetBoolean().Should().BeTrue();
        StreamerToken.TryParse(issued, out var token).Should().BeTrue();
        status.GetProperty("tokenHint").GetString().Should().Be(token.Hint());
    }

    [Fact]
    public async Task PostToken_Twice_RotatesTheToken()
    {
        using var client = factory.CreateBroadcasterClient();

        var first = await IssueTokenAsync(client);
        var second = await IssueTokenAsync(client);

        second.Should().NotBe(first);
        (await PostStateAsync(first)).Should().Be(HttpStatusCode.Unauthorized);
        (await PostStateAsync(second)).Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task DeleteToken_Broadcaster_AnswersNoContentAndUnbindsTheToken()
    {
        using var client = factory.CreateBroadcasterClient();
        var issued = await IssueTokenAsync(client);

        using var response = await client.DeleteAsync(Token, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostStateAsync(issued)).Should().Be(HttpStatusCode.Unauthorized);
        var status = await client.GetFromJsonAsync<JsonElement>(Channel, TestContext.Current.CancellationToken);
        status.GetProperty("hasToken").GetBoolean().Should().BeFalse();
        status.GetProperty("tokenHint").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task DeleteToken_ChannelWithSettings_KeepsThemForTheNextToken()
    {
        using var client = factory.CreateBroadcasterClient();
        await IssueTokenAsync(client);
        (await PutSettingsAsync(client, """{"uiScale":2.5}""")).Should().Be(HttpStatusCode.NoContent);

        await client.DeleteAsync(Token, TestContext.Current.CancellationToken);

        var status = await client.GetFromJsonAsync<JsonElement>(Channel, TestContext.Current.CancellationToken);
        status.GetProperty("hasToken").GetBoolean().Should().BeFalse();
        status.GetProperty("settings").GetProperty("uiScale").GetDecimal().Should().Be(2.5m);
    }

    [Fact]
    public async Task DeleteToken_AfterAStateWasPosted_StopsReportingAStateThatCanNoLongerArrive()
    {
        using var client = factory.CreateBroadcasterClient();
        var issued = await IssueTokenAsync(client);
        await PostStateAsync(issued);

        await client.DeleteAsync(Token, TestContext.Current.CancellationToken);

        var status = await client.GetFromJsonAsync<JsonElement>(Channel, TestContext.Current.CancellationToken);
        status.GetProperty("lastStateAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task PutSettings_Broadcaster_IsReportedBackByGetChannel()
    {
        using var client = factory.CreateBroadcasterClient();
        await IssueTokenAsync(client);

        (await PutSettingsAsync(client, """{"uiScale":1.5}""")).Should().Be(HttpStatusCode.NoContent);

        var status = await client.GetFromJsonAsync<JsonElement>(Channel, TestContext.Current.CancellationToken);
        status.GetProperty("settings").GetProperty("uiScale").GetDecimal().Should().Be(1.5m);
    }

    [Fact]
    public async Task PutSettings_BeforeAnyTokenExists_IsStoredAgainstTheChannel()
    {
        using var client = factory.CreateBroadcasterClient();

        (await PutSettingsAsync(client, """{"uiScale":3.25}""")).Should().Be(HttpStatusCode.NoContent);

        var status = await client.GetFromJsonAsync<JsonElement>(Channel, TestContext.Current.CancellationToken);
        status.GetProperty("hasToken").GetBoolean().Should().BeFalse();
        status.GetProperty("settings").GetProperty("uiScale").GetDecimal().Should().Be(3.25m);
    }

    [Fact]
    public async Task PutSettings_ThenIssuingAToken_KeepsTheScale()
    {
        using var client = factory.CreateBroadcasterClient();
        await PutSettingsAsync(client, """{"uiScale":2}""");

        await IssueTokenAsync(client);

        var status = await client.GetFromJsonAsync<JsonElement>(Channel, TestContext.Current.CancellationToken);
        status.GetProperty("hasToken").GetBoolean().Should().BeTrue();
        status.GetProperty("settings").GetProperty("uiScale").GetDecimal().Should().Be(2m);
    }

    [Fact]
    public async Task PutSettings_Twice_KeepsTheLastScale()
    {
        using var client = factory.CreateBroadcasterClient();

        await PutSettingsAsync(client, """{"uiScale":2}""");
        await PutSettingsAsync(client, """{"uiScale":1.25}""");

        var status = await client.GetFromJsonAsync<JsonElement>(Channel, TestContext.Current.CancellationToken);
        status.GetProperty("settings").GetProperty("uiScale").GetDecimal().Should().Be(1.25m);
    }

    [Theory]
    [InlineData("""{"uiScale":0.5}""")]
    [InlineData("""{"uiScale":5}""")]
    [InlineData("""{"uiScale":"abc"}""")]
    [InlineData("""{"uiScale":1.005}""")]
    [InlineData("""{"uiScale":null}""")]
    [InlineData("{}")]
    [InlineData("")]
    public async Task PutSettings_ScaleTheProtocolDoesNotAllow_IsBadRequest(string body)
    {
        using var client = factory.CreateBroadcasterClient();

        (await PutSettingsAsync(client, body)).Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutSettings_RejectedScale_LeavesTheStoredOneAlone()
    {
        using var client = factory.CreateBroadcasterClient();
        await PutSettingsAsync(client, """{"uiScale":2}""");

        await PutSettingsAsync(client, """{"uiScale":9}""");

        var status = await client.GetFromJsonAsync<JsonElement>(Channel, TestContext.Current.CancellationToken);
        status.GetProperty("settings").GetProperty("uiScale").GetDecimal().Should().Be(2m);
    }

    [Fact]
    public async Task PutSettings_ViewerJwt_IsForbidden()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwt.ForViewer(HotaTwitchApiFactory.ChannelId, HotaTwitchApiFactory.ExtensionSecretBase64));

        (await PutSettingsAsync(client, """{"uiScale":1.5}""")).Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutSettings_WithoutAuthorization_IsUnauthorized()
    {
        using var client = factory.CreateClient();

        (await PutSettingsAsync(client, """{"uiScale":1.5}""")).Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutSettings_WithoutAuthorizationAndABodyThatIsNotJson_IsUnauthorizedRatherThanBadRequest()
    {
        using var client = factory.CreateClient();

        (await PutSettingsAsync(client, """{"uiScale":"abc"}"""))
            .Should().Be(HttpStatusCode.Unauthorized, "who the caller is settles before their body is read");
    }

    [Fact]
    public async Task PutSettings_BodyThatIsNotJsonAtAll_IsUnsupportedMediaType()
    {
        using var client = factory.CreateBroadcasterClient();
        using var content = new StringContent("1.5", Encoding.UTF8, "text/plain");

        using var response = await client.PutAsync(Settings, content, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task PutSettings_TwoChannels_KeepTheirScalesApart()
    {
        using var first = factory.CreateBroadcasterClient("111");
        using var second = factory.CreateBroadcasterClient("222");

        await PutSettingsAsync(first, """{"uiScale":2}""");
        await PutSettingsAsync(second, """{"uiScale":4}""");

        var status = await first.GetFromJsonAsync<JsonElement>(Channel, TestContext.Current.CancellationToken);
        status.GetProperty("settings").GetProperty("uiScale").GetDecimal().Should().Be(2m);
    }

    [Fact]
    public async Task PreflightForSettings_FromTheExtensionOrigin_AllowsPutAndTheContentType()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, Settings);
        request.Headers.Add("Origin", $"https://{HotaTwitchApiFactory.ClientId}.ext-twitch.tv");
        request.Headers.Add("Access-Control-Request-Method", "PUT");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().Equal($"https://{HotaTwitchApiFactory.ClientId}.ext-twitch.tv");
        response.Headers.GetValues("Access-Control-Allow-Methods")
            .Should().Contain(methods => methods.Contains("PUT", StringComparison.Ordinal));
        response.Headers.GetValues("Access-Control-Allow-Headers")
            .Should().Contain(headers => headers.Contains("Content-Type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeleteToken_ChannelThatNeverHadAToken_AnswersNoContent()
    {
        using var client = factory.CreateBroadcasterClient();

        using var response = await client.DeleteAsync(Token, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PostToken_TwoChannels_KeepsTheirTokensApart()
    {
        using var first = factory.CreateBroadcasterClient("111");
        using var second = factory.CreateBroadcasterClient("222");

        var firstToken = await IssueTokenAsync(first);
        var secondToken = await IssueTokenAsync(second);

        var status = await first.GetFromJsonAsync<JsonElement>(Channel, TestContext.Current.CancellationToken);
        StreamerToken.TryParse(firstToken, out var expected).Should().BeTrue();
        status.GetProperty("tokenHint").GetString().Should().Be(expected.Hint());
        secondToken.Should().NotBe(firstToken);
    }

    [Fact]
    public async Task GetChannel_AfterAStateWasPosted_ReportsLastStateAtInUtc()
    {
        using var client = factory.CreateBroadcasterClient();
        var issued = await IssueTokenAsync(client);
        factory.Clock.Advance(TimeSpan.FromMinutes(2));
        await PostStateAsync(issued);

        var status = await client.GetFromJsonAsync<JsonElement>(Channel, TestContext.Current.CancellationToken);

        status.GetProperty("lastStateAt").GetString().Should().Be("2026-09-09T22:02:00Z");
    }

    private static async Task<HttpStatusCode> PutSettingsAsync(HttpClient client, string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PutAsync(Settings, content, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private static async Task<string> IssueTokenAsync(HttpClient client)
    {
        using var response = await client.PostAsync(Token, content: null, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("token").GetString()!;
    }

    private async Task<HttpStatusCode> PostStateAsync(string streamerToken)
    {
        using var producer = factory.CreateClient();
        producer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", streamerToken);
        using var content = new ByteArrayContent(StateDocuments.Bytes());
        using var response = await producer.PostAsync(new Uri("/v1/state", UriKind.Relative), content, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }
}
