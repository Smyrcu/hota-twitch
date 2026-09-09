using System.Buffers;
using HotaTwitch.Api.Authentication;
using HotaTwitch.Application.Ingest;
using HotaTwitch.Domain.Broadcasting;
using Microsoft.Net.Http.Headers;

namespace HotaTwitch.Api.Endpoints;

/// <summary>Section 2 of the protocol: the producer posts the player's state here.</summary>
internal static class StateEndpoints
{
    private const int ReadBufferSize = 8 * 1024;

    public static IEndpointRouteBuilder MapStateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/state", PostStateAsync);

        return endpoints;
    }

    private static async Task<IResult> PostStateAsync(
        HttpContext context,
        IngestStateHandler handler,
        CancellationToken cancellationToken)
    {
        if (context.Request.ContentLength > BroadcastPolicy.MaxStateDocumentBytes)
        {
            return TooLarge();
        }

        var document = await TryReadBodyAsync(context.Request.Body, cancellationToken);
        if (document is null)
        {
            return TooLarge();
        }

        var command = new IngestStateCommand(BearerHeader.Read(context.Request), document);
        var outcome = await handler.HandleAsync(command, cancellationToken);

        return outcome switch
        {
            IngestOutcome.Accepted => Results.Accepted(),
            IngestOutcome.UnknownToken => Unauthorized(context),
            IngestOutcome.PayloadTooLarge => TooLarge(),
            IngestOutcome.RateLimited => RateLimited(context),
            IngestOutcome.InvalidDocument => Results.BadRequest(),
            _ => throw new InvalidOperationException($"Unhandled ingest outcome {outcome}."),
        };
    }

    /// <summary>
    /// Reads the body without trusting <c>Content-Length</c>, so a chunked request cannot slip
    /// past the size limit. Returns <see langword="null"/> once the limit is exceeded.
    /// </summary>
    private static async Task<byte[]?> TryReadBodyAsync(Stream body, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ReadBufferSize);
        using var document = new MemoryStream();

        try
        {
            int read;
            while ((read = await body.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
            {
                if (document.Length + read > BroadcastPolicy.MaxStateDocumentBytes)
                {
                    return null;
                }

                document.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return document.ToArray();
    }

    private static IResult TooLarge() => Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

    private static IResult Unauthorized(HttpContext context)
    {
        context.Response.Headers[HeaderNames.WWWAuthenticate] = "Bearer";
        return Results.Unauthorized();
    }

    private static IResult RateLimited(HttpContext context)
    {
        context.Response.Headers[HeaderNames.RetryAfter] = "1";
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }
}
