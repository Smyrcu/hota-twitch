using HotaTwitch.Domain.Channels;
using HotaTwitch.Infrastructure.Twitch;

namespace HotaTwitch.Api.Authentication;

/// <summary>
/// Section 4 of the protocol: the configuration API only answers the broadcaster, identified by
/// the JWT the Twitch extension helper hands to the configuration page.
/// </summary>
internal sealed class BroadcasterEndpointFilter(ITwitchExtensionJwtVerifier verifier) : IEndpointFilter
{
    private const string ChannelIdItem = "hota-twitch.channel-id";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var claims = await verifier.VerifyAsync(BearerHeader.Read(context.HttpContext.Request));
        if (claims is null)
        {
            return Results.Unauthorized();
        }

        if (!claims.IsBroadcaster)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!ChannelId.TryParse(claims.ChannelId, out var channelId))
        {
            return Results.Unauthorized();
        }

        context.HttpContext.Items[ChannelIdItem] = channelId;
        return await next(context);
    }

    /// <summary>The channel this request was authenticated for.</summary>
    public static ChannelId ChannelIdOf(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Items[ChannelIdItem] is ChannelId channelId
            ? channelId
            : throw new InvalidOperationException($"{nameof(BroadcasterEndpointFilter)} did not run for this endpoint.");
    }
}
