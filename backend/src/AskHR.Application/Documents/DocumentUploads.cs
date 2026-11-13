using AskHR.Application.Abstractions;
using AskHR.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace AskHR.Application.Documents;

/// <summary>What the API returns for a document (no file bytes, no chunk text).</summary>
public sealed record DocumentSummary(
    Guid Id,
    string Name,
    string ContentType,
    long SizeBytes,
    string UploadedBy,
    DateTime UploadedAtUtc,
    string Status,
    int ChunkCount,
    string? FailureReason)
{
    public static DocumentSummary From(Document d) =>
        new(d.Id, d.Name, d.ContentType, d.SizeBytes, d.UploadedBy, d.UploadedAtUtc, d.Status.ToString(), d.ChunkCount, d.FailureReason);
}

/// <summary>Which files an HR Admin may upload. The endpoint checks these before anything is stored.</summary>
public static class UploadRules
{
    public const long MaxBytes = 10 * 1024 * 1024;   // 10 MB
    public const string Pdf = "application/pdf";
    public const string Text = "text/plain";

    /// <summary>
    /// Decides the real content type from the file name AND the first bytes. The browser-sent Content-Type is
    /// only a hint (any client can send anything), so a .pdf must also start with the PDF signature "%PDF-".
    /// </summary>
    /// <returns>The content type to store, or an error message for the user.</returns>
    public static (string? ContentType, string? Error) Check(string fileName, long length, ReadOnlySpan<byte> header)
    {
        if (length <= 0)
            return (null, "The file is empty.");
        if (length > MaxBytes)
            return (null, $"The file is {length / 1024.0 / 1024.0:0.0} MB; the limit is {MaxBytes / 1024 / 1024} MB.");

        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        switch (extension)
        {
            case ".pdf" when header.StartsWith("%PDF-"u8):
                return (Pdf, null);
            case ".pdf":
                return (null, "The file has a .pdf name but is not a PDF.");
            case ".txt" when !header.Contains((byte)0):   // a NUL byte means binary, not text
                return (Text, null);
            case ".txt":
                return (null, "The file has a .txt name but is not a text file.");
            default:
                return (null, "Only PDF (.pdf) and plain text (.txt) files can be uploaded.");
        }
    }
}

/// <summary>
/// Use case "HR Admin uploads a policy": store the document as Processing, queue the ingestion job, return at once.
/// The slow part (extracting and chunking; in M4 also embedding) never runs on the HTTP request.
/// </summary>
public sealed class UploadDocument(IAskHrDbContext db, IIngestionQueue queue, TimeProvider clock)
{
    public async Task<DocumentSummary> HandleAsync(string fileName, string contentType, byte[] content, string uploadedBy,
        CancellationToken cancellationToken = default)
    {
        var document = Document.Create(Path.GetFileName(fileName), contentType, content.LongLength, uploadedBy,
            clock.GetUtcNow().UtcDateTime);

        db.Documents.Add(document);   // EF Core assigns document.Id here, before SaveChanges
        db.DocumentFiles.Add(new DocumentFile(document.Id, content));
        await db.SaveChangesAsync(cancellationToken);

        // Enqueue only after the row is committed, so the job can always find it. (If the process died between
        // the two lines the document would stay Processing; an outbox would close that gap, overkill for now.)
        queue.Enqueue(document.Id);

        return DocumentSummary.From(document);
    }
}

/// <summary>Read and delete use cases for the Documents page.</summary>
public sealed class DocumentQueries(IAskHrDbContext db)
{
    public async Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var documents = await db.Documents.AsNoTracking()
            .OrderByDescending(d => d.UploadedAtUtc)
            .ToListAsync(cancellationToken);
        return documents.Select(DocumentSummary.From).ToList();
    }

    public async Task<DocumentSummary?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        return document is null ? null : DocumentSummary.From(document);
    }

    /// <summary>Deletes the document; the database cascades the delete to its file and chunks.</summary>
    /// <returns>false when there was no such document.</returns>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.Documents.Where(d => d.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
}
