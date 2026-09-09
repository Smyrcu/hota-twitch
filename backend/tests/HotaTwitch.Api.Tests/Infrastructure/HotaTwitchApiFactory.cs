using System.Net.Http.Headers;
using HotaTwitch.Application.Abstractions;
using HotaTwitch.Application.Broadcasting;
using HotaTwitch.Infrastructure.Broadcasting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HotaTwitch.Api.Tests.Infrastructure;

/// <summary>
/// Hosts the API against a throwaway SQLite file, a clock the test drives and a publisher that
/// records instead of calling Twitch. The broadcast dispatcher is left out so that a test decides
/// when the coalescer is drained.
/// </summary>
internal sealed class HotaTwitchApiFactory : WebApplicationFactory<Program>
{
    public const string ExtensionSecretBase64 = "c2VjcmV0LWZvci10ZXN0cy0zMi1ieXRlcy1sb25nISE=";
    public const string ClientId = "test-client-id";
    public const string OwnerUserId = "1000";
    public const string ChannelId = "141981764";

    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"hota-twitch-{Guid.NewGuid():N}.db");

    public TestClock Clock { get; } = new();

    public RecordingPubSubPublisher Publisher { get; } = new();

    public HttpClient CreateBroadcasterClient(string channelId = ChannelId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.ForBroadcaster(channelId, ExtensionSecretBase64));
        return client;
    }

    /// <summary>Releases everything the coalescer is holding, as the dispatcher would.</summary>
    public Task DrainBroadcastsAsync() =>
        Services.GetRequiredService<BroadcastCoalescer>().PublishDueAsync(CancellationToken.None);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("TWITCH_CLIENT_ID", ClientId);
        builder.UseSetting("TWITCH_EXTENSION_SECRET", ExtensionSecretBase64);
        builder.UseSetting("TWITCH_OWNER_USER_ID", OwnerUserId);
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={databasePath}");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);

            services.RemoveAll<IPubSubPublisher>();
            services.AddSingleton<IPubSubPublisher>(Publisher);

            var dispatcher = services.Single(service =>
                service.ServiceType == typeof(IHostedService) && service.ImplementationType == typeof(BroadcastDispatcher));
            services.Remove(dispatcher);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }
}
