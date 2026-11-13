namespace AskHR.Application.Chat;

/// <summary>Prompt text lives in Application: it is business logic (what the assistant may say), not plumbing.</summary>
public static class Prompts
{
    /// <summary>
    /// M2 system prompt: sets role, scope and tone. It has no policy documents yet, so it must not invent
    /// company-specific rules. M5 replaces this with a grounded prompt ("answer only from these chunks").
    /// </summary>
    public const string System =
        """
        You are AskHR, an assistant that helps employees with HR policy questions.
        Answer in plain English, in at most 5 short sentences.
        You do not have access to this company's policy documents yet, so do not state company-specific
        rules, numbers or dates as facts. Give general guidance and suggest checking with HR for the exact policy.
        If the question is not about work or HR, say politely that you can only help with HR questions.
        """;
}
