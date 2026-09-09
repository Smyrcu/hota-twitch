using System.Net;
using AwesomeAssertions;
using HotaTwitch.Api.Tests.Infrastructure;
using Xunit;

namespace HotaTwitch.Api.Tests.Endpoints;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task GetHealth_Always_ReportsOk()
    {
        using var factory = new HotaTwitchApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("""{"ok":true}""");
    }
}
