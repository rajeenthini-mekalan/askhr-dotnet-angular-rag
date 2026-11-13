using System.ComponentModel;
using AskHR.Application.Abstractions;
using AskHR.Application.Documents;
using Hangfire;

namespace AskHR.Infrastructure.Ingestion;

/// <summary>
/// The Hangfire job: a thin adapter so Hangfire attributes stay out of Application. Hangfire stores only the
/// method and its arguments (the document id) in SQL Server; a worker later creates this class from DI in its own
/// scope (its own DbContext) and calls <see cref="RunAsync"/>.
/// </summary>
public sealed class IngestDocumentJob(IngestDocument ingestDocument)
{
    // Problems with the file itself never throw out of IngestDocument (it marks the document Failed), so a
    // retry here only happens for infrastructure errors, e.g. SQL Server down while recording the result.
    // Three tries with Hangfire's growing delays, then the job shows as Failed in the dashboard.
    [AutomaticRetry(Attempts = 3)]
    [DisplayName("Ingest document {0}")]
    public Task RunAsync(Guid documentId, CancellationToken cancellationToken) =>
        ingestDocument.HandleAsync(documentId, cancellationToken);   // Hangfire passes a token that fires on shutdown
}

/// <summary><see cref="IIngestionQueue"/> on Hangfire: one INSERT into Hangfire's tables, no waiting for the work.</summary>
public sealed class HangfireIngestionQueue(IBackgroundJobClient jobs) : IIngestionQueue
{
    public string Enqueue(Guid documentId) =>
        jobs.Enqueue<IngestDocumentJob>(job => job.RunAsync(documentId, CancellationToken.None));
}
