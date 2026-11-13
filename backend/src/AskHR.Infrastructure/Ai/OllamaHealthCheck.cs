using System.Net.Http.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AskHR.Infrastructure.Ai;

/// <summary>
/// Calls Ollama's GET /api/tags (the list of pulled models) and checks the configured chat model is there.
/// Cheap: it does not load the model into RAM, unlike a real chat call would.
/// </summary>
public sealed class OllamaHealthCheck(IHttpClientFactory httpClientFactory, IOptions<AiOptions> options) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var model = options.Value.ChatModel;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);

            var http = httpClientFactory.CreateClient(AiServiceCollectionExtensions.OllamaHttpClient);
            var tags = await http.GetFromJsonAsync<TagsResponse>("api/tags", cts.Token);

            var found = tags?.Models?.Any(m => string.Equals(m.Name, model, StringComparison.OrdinalIgnoreCase)) ?? false;
            return found
                ? HealthCheckResult.Healthy($"Ollama is up and '{model}' is pulled.")
                : new HealthCheckResult(context.Registration.FailureStatus, $"Ollama is up but '{model}' is not pulled. Run: ollama pull {model}");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Ollama is not reachable.", ex);
        }
    }

    private sealed record TagsResponse(List<TagModel>? Models);
    private sealed record TagModel(string Name);
}
