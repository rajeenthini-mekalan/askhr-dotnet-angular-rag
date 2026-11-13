using Microsoft.Extensions.AI;

namespace AskHR.Tests.Fakes;

/// <summary>
/// An IChatClient that never calls a model: it records what it was sent and returns a canned answer.
/// Because the app depends on the IChatClient abstraction, swapping this in is a one-line DI change.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    public string Answer { get; set; } = "Fake answer from the test model.";

    /// <summary>Set to make the next call throw (e.g. HttpRequestException = Ollama down).</summary>
    public Exception? ThrowOnCall { get; set; }

    public IReadOnlyList<ChatMessage> LastMessages { get; private set; } = [];
    public ChatOptions? LastOptions { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        LastMessages = messages.ToList();
        LastOptions = options;
        if (ThrowOnCall is { } ex) throw ex;

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Answer))
        {
            ModelId = "fake-model",
            Usage = new UsageDetails { InputTokenCount = 42, OutputTokenCount = 7 },
        });
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Streaming arrives in M5.");

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
