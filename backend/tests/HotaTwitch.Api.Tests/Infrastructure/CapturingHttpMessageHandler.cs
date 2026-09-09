using System.Net;

namespace HotaTwitch.Api.Tests.Infrastructure;

internal sealed class CapturingHttpMessageHandler(HttpStatusCode status, string responseBody = "") : HttpMessageHandler
{
    public HttpRequestMessage? Request { get; private set; }

    public string RequestBody { get; private set; } = string.Empty;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Request = request;
        RequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(status) { Content = new StringContent(responseBody) };
    }
}
