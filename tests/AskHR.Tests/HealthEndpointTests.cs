using System.Net;
using AskHR.Tests.Auth;

namespace AskHR.Tests;

// WebApplicationFactory boots the real API in memory (no port, no Docker) so tests can call it over HTTP.
// TestAuthApiFactory also swaps out Ollama, so this test needs no running model.
public class HealthEndpointTests(TestAuthApiFactory factory) : IClassFixture<TestAuthApiFactory>
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
