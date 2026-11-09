using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AskHR.Tests;

// WebApplicationFactory boots the real API in memory (no port, no Docker) so tests can call it over HTTP.
public class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_returns_200_Healthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
