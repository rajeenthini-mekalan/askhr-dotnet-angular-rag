using System.ComponentModel.DataAnnotations;

namespace AskHR.Infrastructure.Ingestion;

/// <summary>Binds the "Ingestion" section of appsettings.json.</summary>
public sealed class IngestionOptions
{
    public const string Section = "Ingestion";

    /// <summary>
    /// Hangfire worker threads in this process. 1 on an 8 GB machine: ingestion is CPU-heavy (PDF parsing now,
    /// Ollama embeddings in M4) and documents are uploaded rarely, so running them one at a time is fine.
    /// </summary>
    [Range(1, 20)]
    public int WorkerCount { get; set; } = 1;

    /// <summary>Run the Hangfire server (the workers) in the API process. Tests turn it off.</summary>
    public bool RunServer { get; set; } = true;

    /// <summary>Map the Hangfire dashboard at /hangfire. It is only ever mapped in Development, and only for hr-admin.</summary>
    public bool Dashboard { get; set; } = true;
}
