using System.ComponentModel.DataAnnotations;

namespace AskHR.Infrastructure.Ai;

/// <summary>
/// Binds the "Ai" section of appsettings.json. Nothing secret here: local Ollama needs no API key.
/// (A future Claude provider would read its key from user secrets / environment variables, never from this file.)
/// </summary>
public sealed class AiOptions
{
    public const string Section = "Ai";

    /// <summary>Base URL of the Ollama HTTP API, e.g. http://localhost:11434.</summary>
    [Required, Url]
    public string OllamaUrl { get; set; } = "";

    /// <summary>Ollama model tag used for chat, e.g. llama3.2:3b.</summary>
    [Required]
    public string ChatModel { get; set; } = "";

    /// <summary>0 = always the most likely next token (factual, repeatable); higher = more varied wording.</summary>
    [Range(0.0, 2.0)]
    public float Temperature { get; set; } = 0.2f;

    /// <summary>Upper bound on the answer length in tokens (≈ ¾ of a word each). Keeps answers short and CPU time bounded.</summary>
    [Range(16, 4096)]
    public int MaxOutputTokens { get; set; } = 512;

    /// <summary>A 3B model on CPU can take a while, especially the first call that loads it into RAM.</summary>
    [Range(5, 600)]
    public int TimeoutSeconds { get; set; } = 120;
}
