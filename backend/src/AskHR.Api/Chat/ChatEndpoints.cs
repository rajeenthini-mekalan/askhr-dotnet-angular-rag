using AskHR.Api.Auth;
using AskHR.Application.Chat;

namespace AskHR.Api.Chat;

public sealed record ChatRequest(string? Question);

public static class ChatEndpoints
{
    /// <summary>POST /api/chat — one question in, one complete answer out (streaming comes in M5).</summary>
    public static RouteGroupBuilder MapChatEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/chat", async (ChatRequest request, AskQuestion askQuestion, ILoggerFactory loggers, CancellationToken ct) =>
            {
                var question = request.Question?.Trim();
                if (string.IsNullOrEmpty(question))
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["question"] = ["A question is required."],
                    });
                if (question.Length > AskQuestion.MaxQuestionLength)
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["question"] = [$"A question can be at most {AskQuestion.MaxQuestionLength} characters."],
                    });

                try
                {
                    return Results.Ok(await askQuestion.HandleAsync(question, ct));
                }
                catch (HttpRequestException ex)
                {
                    // Ollama not running / not reachable: tell the client it is a dependency problem, not a bug.
                    loggers.CreateLogger(typeof(ChatEndpoints)).LogError(ex, "The AI model could not be reached.");
                    return Results.Problem("The AI model is not available right now. Try again later.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }
                catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
                {
                    // HttpClient timeout (the request itself was not cancelled by the caller).
                    loggers.CreateLogger(typeof(ChatEndpoints)).LogError(ex, "The AI model timed out.");
                    return Results.Problem("The AI model took too long to answer.",
                        statusCode: StatusCodes.Status504GatewayTimeout);
                }
            })
            .RequireAuthorization(Policies.Employee)
            .WithName("Chat")
            .Produces<ChatAnswer>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return api;
    }
}
