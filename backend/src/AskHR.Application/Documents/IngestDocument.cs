using AskHR.Application.Abstractions;
using AskHR.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AskHR.Application.Documents;

/// <summary>
/// Use case "process an uploaded document", run by the background job: extract text → chunk → save the chunks and
/// mark the document Ready, all in one SaveChanges (one transaction), so a crash never leaves half the chunks.
/// M4 adds one step between chunking and saving: embed each chunk.
/// </summary>
public sealed class IngestDocument(IAskHrDbContext db, IDocumentTextExtractor extractor, ILogger<IngestDocument> logger)
{
    public async Task HandleAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        if (document is null)
        {
            logger.LogWarning("Document {DocumentId} no longer exists (deleted before ingestion); nothing to do.", documentId);
            return;
        }
        if (document.Status != DocumentStatus.Processing)
        {
            // Jobs can run twice (a retry, a restarted server). Doing nothing the second time keeps it idempotent.
            logger.LogInformation("Document {DocumentId} is already {Status}; skipping.", documentId, document.Status);
            return;
        }

        try
        {
            byte[] content = await db.DocumentFiles
                .Where(f => f.DocumentId == documentId)
                .Select(f => f.Content)
                .SingleAsync(cancellationToken);

            string text = extractor.ExtractText(content, document.ContentType);
            IReadOnlyList<string> pieces = Chunker.Split(text);
            if (pieces.Count == 0)
                throw new DocumentIngestionException("The document contains no text.");

            for (int i = 0; i < pieces.Count; i++)
                db.Chunks.Add(new Chunk(documentId, i, pieces[i]));
            document.MarkReady(pieces.Count);

            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Document {DocumentId} ({Name}) is Ready with {ChunkCount} chunks from {CharCount} characters.",
                documentId, document.Name, pieces.Count, text.Length);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Shutdown (OperationCanceledException) is not a failure: Hangfire requeues the job.
            // Everything else marks the document Failed so the HR Admin sees why, instead of "Processing" forever.
            string reason = ex is DocumentIngestionException ? ex.Message : $"Unexpected error ({ex.GetType().Name}). See the server log.";
            logger.LogError(ex, "Ingestion of document {DocumentId} ({Name}) failed: {Reason}", documentId, document.Name, reason);
            await MarkFailedAsync(documentId, reason, cancellationToken);
        }
    }

    private async Task MarkFailedAsync(Guid documentId, string reason, CancellationToken cancellationToken)
    {
        // Throw away the half-done work (chunks added but not saved) and record only the failure.
        db.ChangeTracker.Clear();

        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        if (document is null)
            return;   // deleted meanwhile
        document.MarkFailed(reason);
        await db.SaveChangesAsync(cancellationToken);
    }
}
