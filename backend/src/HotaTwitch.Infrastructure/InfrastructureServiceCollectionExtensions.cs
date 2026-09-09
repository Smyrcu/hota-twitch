using HotaTwitch.Application.Abstractions;
using HotaTwitch.Infrastructure.Broadcasting;
using HotaTwitch.Infrastructure.Persistence;
using HotaTwitch.Infrastructure.Time;
using HotaTwitch.Infrastructure.Twitch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HotaTwitch.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    private const string DefaultConnectionString = "Data Source=hota-twitch.db";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<TwitchOptions>()
            .Configure(options =>
            {
                options.ClientId = configuration["TWITCH_CLIENT_ID"] ?? string.Empty;
                options.ExtensionSecret = configuration["TWITCH_EXTENSION_SECRET"] ?? string.Empty;
                options.OwnerUserId = configuration["TWITCH_OWNER_USER_ID"] ?? string.Empty;
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ExtensionSecret>();
        services.AddSingleton<ITwitchExtensionJwtFactory, TwitchExtensionJwtFactory>();
        services.AddSingleton<ITwitchExtensionJwtVerifier, TwitchExtensionJwtVerifier>();
        services.AddSingleton<IClock, SystemClock>();

        services.AddDbContext<HotaTwitchDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("Default") ?? DefaultConnectionString));
        services.AddScoped<IChannelRepository, EfChannelRepository>();
        services.AddHostedService<DatabaseMigrator>();

        AddPubSubPublisher(services, configuration);
        services.AddHostedService<BroadcastDispatcher>();

        return services;
    }

    private static void AddPubSubPublisher(IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue("TWITCH_FAKE_PUBSUB", defaultValue: false))
        {
            services.AddSingleton<IPubSubPublisher, LoggingPubSubPublisher>();
            return;
        }

        services.AddHttpClient(TwitchPubSubPublisher.HttpClientName, (provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptions<TwitchOptions>>().Value;
            client.BaseAddress = settings.HelixBaseAddress;
            client.Timeout = settings.HelixTimeout;
        });
        services.AddSingleton<IPubSubPublisher, TwitchPubSubPublisher>();
    }
}
