using HotaTwitch.Application.Broadcasting;
using HotaTwitch.Application.Channels;
using HotaTwitch.Application.Ingest;
using Microsoft.Extensions.DependencyInjection;

namespace HotaTwitch.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IIngestRateLimiter, SlidingWindowIngestRateLimiter>();
        services.AddSingleton<BroadcastCoalescer>();
        services.AddScoped<IngestStateHandler>();
        services.AddScoped<IssueTokenHandler>();
        services.AddScoped<RevokeTokenHandler>();
        services.AddScoped<GetChannelStatusHandler>();
        services.AddScoped<UpdateChannelSettingsHandler>();

        return services;
    }
}
