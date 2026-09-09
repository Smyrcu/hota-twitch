using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

    [Fact]
    public async Task GetChannel_ExpiredJwt_IsUnauthorized()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwt.Expired(HotaTwitchApiFactory.ChannelId, HotaTwitchApiFactory.ExtensionSecretBase64));

        using var response = await client.GetAsync(Channel, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
    public async Task DeleteToken_Broadcaster_AnswersNoContentAndForgetsTheChannel()
    {
        using var client = factory.CreateBroadcasterClient();
        var issued = await IssueTokenAsync(client);

        using var response = await client.DeleteAsync(Token, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostStateAsync(issued)).Should().Be(HttpStatusCode.Unauthorized);
        var status = await client.GetFromJsonAsync<JsonElement>(Channel, TestContext.Current.CancellationToken);
        status.GetProperty("hasToken").GetBoolean().Should().BeFalse();
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
