namespace AskHR.Application.Abstractions;

/// <summary>
/// Hands a document to background processing. Infrastructure implements it with Hangfire; the upload use case
/// does not know or care which job library runs the work.
/// </summary>
public interface IIngestionQueue
{
    /// <summary>Queues ingestion for the document and returns the background job id.</summary>
    string Enqueue(Guid documentId);
}
