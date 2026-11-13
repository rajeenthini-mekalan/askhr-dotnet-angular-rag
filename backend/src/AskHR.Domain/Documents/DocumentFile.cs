namespace AskHR.Domain.Documents;

/// <summary>
/// The uploaded file's bytes, one row per <see cref="Document"/> (same key). Stored in SQL Server so the
/// Hangfire job can read it from any machine and deleting the document removes it in the same transaction.
/// Kept after ingestion so a document can be re-processed when the chunking or embedding model changes.
/// </summary>
public sealed class DocumentFile
{
    private DocumentFile() { }   // for EF Core

    public DocumentFile(Guid documentId, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        DocumentId = documentId;
        Content = content;
    }

    public Guid DocumentId { get; private set; }
    public byte[] Content { get; private set; } = [];
}
