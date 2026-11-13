using Microsoft.Extensions.AI;

namespace AskHR.Application.Chat;

/// <summary>What the endpoint returns: the answer plus a little metadata that is useful while learning.</summary>
public sealed record ChatAnswer(string Answer, string? Model, long? InputTokens, long? OutputTokens);

/// <summary>
/// Use case "an employee asks a question". Depends only on <see cref="IChatClient"/> (the abstraction),
/// never on Ollama, so it is provider-agnostic and easy to test with a fake client.
/// M2: plain chat. M4/M5 add retrieval: the closest policy chunks get added to the prompt.
/// </summary>
public sealed class AskQuestion(IChatClient chatClient)
{
    public const int MaxQuestionLength = 1000;

    public async Task<ChatAnswer> HandleAsync(string question, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        // A chat call is a list of messages, each with a role:
        //   System    = standing instructions from us, the app (who you are, rules, tone). The user never sees it.
        //   User      = what the employee typed.
        //   Assistant = what the model said earlier (used for multi-turn history; not needed yet).
        List<ChatMessage> messages =
        [
            new(ChatRole.System, Prompts.System),
            new(ChatRole.User, question.Trim()),
        ];

        // No ChatOptions here: model, temperature and max tokens come from the defaults set in the
        // ChatClientBuilder pipeline (appsettings "Ai"). A caller could still override them per call.
        ChatResponse response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);

        return new ChatAnswer(
            response.Text.Trim(),
            response.ModelId,
            response.Usage?.InputTokenCount,
            response.Usage?.OutputTokenCount);
    }
}
