using System.Globalization;
using HotaTwitch.Api.Authentication;
using HotaTwitch.Api.Contracts;
using HotaTwitch.Application.Channels;

namespace HotaTwitch.Api.Endpoints;

/// <summary>Section 4 of the protocol: what the extension's configuration page calls.</summary>
internal static class ConfigEndpoints
{
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public static IEndpointRouteBuilder MapConfigEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var config = endpoints.MapGroup("/v1/config").AddEndpointFilter<BroadcasterEndpointFilter>();

        config.MapGet("/channel", GetChannelAsync);
        config.MapPost("/token", IssueTokenAsync);
        config.MapDelete("/token", RevokeTokenAsync);

        return endpoints;
    }

    private static async Task<IResult> GetChannelAsync(
        HttpContext context,
        GetChannelStatusHandler handler,
        CancellationToken cancellationToken)
    {
        var status = await handler.HandleAsync(BroadcasterEndpointFilter.ChannelIdOf(context), cancellationToken);

        return Results.Ok(new ChannelStatusResponse(status.HasToken, status.TokenHint, Format(status.LastStateAt)));
    }

    private static async Task<IResult> IssueTokenAsync(
        HttpContext context,
        IssueTokenHandler handler,
        CancellationToken cancellationToken)
    {
        var token = await handler.HandleAsync(BroadcasterEndpointFilter.ChannelIdOf(context), cancellationToken);

        return Results.Ok(new IssuedTokenResponse(token));
    }

    private static async Task<IResult> RevokeTokenAsync(
        HttpContext context,
        RevokeTokenHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(BroadcasterEndpointFilter.ChannelIdOf(context), cancellationToken);

        return Results.NoContent();
    }

    private static string? Format(DateTimeOffset? instant) =>
        instant?.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);
}
