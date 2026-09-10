using System.Text.Json;
using AwesomeAssertions;
using HotaTwitch.Api.Tests.Infrastructure;
using HotaTwitch.Domain.Channels;
using HotaTwitch.Infrastructure.Time;
using HotaTwitch.Infrastructure.Twitch;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace HotaTwitch.Api.Tests.Twitch;

public sealed class TwitchExtensionJwtFactoryTests
{
    private static readonly ChannelId Channel = new(HotaTwitchApiFactory.ChannelId);

    private readonly TestClock clock = new();
    private readonly IOptions<TwitchOptions> options = Options.Create(new TwitchOptions
    {
        ClientId = HotaTwitchApiFactory.ClientId,
        ExtensionSecret = HotaTwitchApiFactory.ExtensionSecretBase64,
        OwnerUserId = HotaTwitchApiFactory.OwnerUserId,
    });

    [Fact]
    public void CreateExternalToken_Always_IsSignedWithHs256()
    {
        var jwt = new JsonWebToken(CreateToken());

        jwt.Alg.Should().Be(SecurityAlgorithms.HmacSha256);
    }

    [Fact]
    public void CreateExternalToken_Always_CarriesTheClaimsTwitchRequires()
    {
        var payload = PayloadOf(CreateToken());

        payload.GetProperty("role").GetString().Should().Be("external");
        payload.GetProperty("user_id").GetString().Should().Be(HotaTwitchApiFactory.OwnerUserId);
        payload.GetProperty("channel_id").GetString().Should().Be(HotaTwitchApiFactory.ChannelId);
    }

    [Fact]
    public void CreateExternalToken_Always_GrantsSendPermissionOnTheBroadcastTopic()
    {
        var payload = PayloadOf(CreateToken());

        var send = payload.GetProperty("pubsub_perms").GetProperty("send");
        send.ValueKind.Should().Be(JsonValueKind.Array);
        send.EnumerateArray().Select(topic => topic.GetString()).Should().Equal("broadcast");
    }

    [Fact]
    public void CreateExternalToken_Always_ExpiresAfterTheConfiguredLifetime()
    {
        var payload = PayloadOf(CreateToken());

        var expires = DateTimeOffset.FromUnixTimeSeconds(payload.GetProperty("exp").GetInt64());
        expires.Should().Be(clock.UtcNow + options.Value.ExternalTokenLifetime);
    }

    [Fact]
    public async Task CreateExternalToken_Always_VerifiesAgainstTheExtensionSecret()
    {
        // The verifier checks the lifetime against the wall clock, so this token is minted against
        // the wall clock too rather than against the instant the other tests pin.
        var factory = new TwitchExtensionJwtFactory(new ExtensionSecret(options), options, new SystemClock());
        var verifier = new TwitchExtensionJwtVerifier(
            new ExtensionSecret(options),
            NullLogger<TwitchExtensionJwtVerifier>.Instance);

        var claims = await verifier.VerifyAsync(factory.CreateExternalToken(Channel));

        claims.Should().Be(new TwitchExtensionClaims("external", HotaTwitchApiFactory.ChannelId, HotaTwitchApiFactory.OwnerUserId));
    }

    private string CreateToken() =>
        new TwitchExtensionJwtFactory(new ExtensionSecret(options), options, clock).CreateExternalToken(Channel);

    private static JsonElement PayloadOf(string token) =>
        JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(token.Split('.')[1])).RootElement;
}
