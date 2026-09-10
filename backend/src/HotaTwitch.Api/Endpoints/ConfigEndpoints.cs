using System.Globalization;
using System.Text.Json;
using HotaTwitch.Api.Authentication;
using HotaTwitch.Api.Contracts;
using HotaTwitch.Application.Channels;
using HotaTwitch.Domain.Channels;

namespace HotaTwitch.Api.Endpoints;

/// <summary>Section 4 of the protocol: what the extension's configuration page calls.</summary>
internal static class ConfigEndpoints
{
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public static IEndpointRouteBuilder MapConfigEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var config = endpoints.MapGroup("/v1/config")
            .RequireCors(ExtensionCors.PolicyName)
            .AddEndpointFilter<BroadcasterEndpointFilter>();

        config.MapGet("/channel", GetChannelAsync);
        config.MapPost("/token", IssueTokenAsync);
        config.MapDelete("/token", RevokeTokenAsync);
        config.MapPut("/settings", UpdateSettingsAsync);

        return endpoints;
    }

    private static async Task<IResult> GetChannelAsync(
        HttpContext context,
        GetChannelStatusHandler handler,
        CancellationToken cancellationToken)
    {
        var status = await handler.HandleAsync(BroadcasterEndpointFilter.ChannelIdOf(context), cancellationToken);

        return Results.Ok(new ChannelStatusResponse(
            status.HasToken,
            status.TokenHint,
            Format(status.LastStateAt),
            new ChannelSettingsResponse(status.Settings.UiScale)));
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

    /// <summary>
    /// The body is read here rather than bound as a parameter, because parameter binding runs
    /// before the endpoint filter: a caller with no valid broadcaster token would otherwise have
    /// its JSON buffered and parsed before anyone checked who it was.
    /// </summary>
    private static async Task<IResult> UpdateSettingsAsync(
        HttpContext context,
        UpdateChannelSettingsHandler handler,
        CancellationToken cancellationToken)
    {
        var channelId = BroadcasterEndpointFilter.ChannelIdOf(context);

        if (!context.Request.HasJsonContentType())
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        ChannelSettingsRequest? request;
        try
        {
            request = await context.Request.ReadFromJsonAsync<ChannelSettingsRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        if (request?.UiScale is not { } uiScale || !ChannelSettings.TryCreate(uiScale, out var settings))
        {
            return Results.BadRequest();
        }

        await handler.HandleAsync(channelId, settings, cancellationToken);

        return Results.NoContent();
    }

    private static string? Format(DateTimeOffset? instant) =>
        instant?.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);
}
