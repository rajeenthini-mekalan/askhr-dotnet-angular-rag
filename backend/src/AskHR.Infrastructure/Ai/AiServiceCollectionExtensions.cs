using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OllamaSharp;

namespace AskHR.Infrastructure.Ai;

public static class AiServiceCollectionExtensions
{
    /// <summary>Named HttpClient shared by the chat client and the health check (base address + timeout from AiOptions).</summary>
    public const string OllamaHttpClient = "ollama";

    /// <summary>
    /// Registers <see cref="IChatClient"/> backed by local Ollama. The rest of the app only sees IChatClient,
    /// so switching to another provider (e.g. the Claude API) means changing this method, not the callers.
    /// </summary>
    public static IServiceCollection AddAskHrAi(this IServiceCollection services)
    {
        services.AddOptions<AiOptions>()
            .BindConfiguration(AiOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();   // a typo in appsettings fails at startup, not on the first question

        services.AddHttpClient(OllamaHttpClient, (sp, http) =>
        {
            var ai = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            http.BaseAddress = new Uri(ai.OllamaUrl);
            http.Timeout = TimeSpan.FromSeconds(ai.TimeoutSeconds);
        });

        // AddChatClient takes the *inner* client (the one that really talks to a model) and returns a
        // ChatClientBuilder. Each .Use(...) wraps it in a middleware layer, like ASP.NET Core middleware:
        //
        //   caller → ConfigureOptions → Logging → OllamaApiClient → HTTP POST /api/chat on Ollama
        //
        // The first layer added is the outermost one. The built pipeline is itself an IChatClient.
        services.AddChatClient(sp =>
            {
                var ai = sp.GetRequiredService<IOptions<AiOptions>>().Value;
                var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(OllamaHttpClient);
                return new OllamaApiClient(http, ai.ChatModel);   // OllamaSharp implements IChatClient
            })
            .Use((inner, sp) =>
            {
                // Fill in app-wide defaults; a caller that sets a value explicitly keeps its own value.
                var ai = sp.GetRequiredService<IOptions<AiOptions>>().Value;
                return new ConfigureOptionsChatClient(inner, options =>
                {
                    options.ModelId ??= ai.ChatModel;
                    options.Temperature ??= ai.Temperature;
                    options.MaxOutputTokens ??= ai.MaxOutputTokens;
                });
            })
            .UseLogging();   // Debug: one line per call; Trace: also the messages and the response (never enable in prod)

        services.AddHealthChecks()
            // Degraded, not Unhealthy: the API itself is up and auth still works without the model.
            .AddCheck<OllamaHealthCheck>("ollama", failureStatus: HealthStatus.Degraded, tags: ["ai"]);

        return services;
    }
}
