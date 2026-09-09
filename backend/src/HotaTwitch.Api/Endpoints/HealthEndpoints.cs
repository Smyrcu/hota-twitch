using HotaTwitch.Api.Contracts;

namespace HotaTwitch.Api.Endpoints;

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", () => Results.Ok(new HealthResponse(Ok: true)));

        return endpoints;
    }
}
