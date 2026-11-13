using System.Net;
using System.Text;
using AskHR.Infrastructure.Ai;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AskHR.Tests.Chat;

public class OllamaHealthCheckTests
{
    private static async Task<HealthCheckResult> RunAsync(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var check = new OllamaHealthCheck(
            new StubHttpClientFactory(new StubHandler(respond)),
            Options.Create(new AiOptions { OllamaUrl = "http://ollama.test", ChatModel = "llama3.2:3b" }));

        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("ollama", check, HealthStatus.Degraded, tags: null),
        };
        return await check.CheckHealthAsync(context);
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Healthy_when_the_chat_model_is_pulled()
    {
        var result = await RunAsync(_ => Json("""{"models":[{"name":"nomic-embed-text:latest"},{"name":"llama3.2:3b"}]}"""));
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Degraded_when_the_chat_model_is_missing()
    {
        var result = await RunAsync(_ => Json("""{"models":[{"name":"nomic-embed-text:latest"}]}"""));
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("ollama pull llama3.2:3b", result.Description);
    }

    [Fact]
    public async Task Degraded_when_ollama_is_not_reachable()
    {
        var result = await RunAsync(_ => throw new HttpRequestException("Connection refused"));
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request));
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler) { BaseAddress = new Uri("http://ollama.test/") };
    }
}
