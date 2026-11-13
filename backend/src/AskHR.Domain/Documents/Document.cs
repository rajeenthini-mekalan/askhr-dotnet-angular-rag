namespace AskHR.Domain.Documents;

/// <summary>Where a document is in the ingestion pipeline.</summary>
public enum DocumentStatus
{
    /// <summary>Uploaded; the background job has not finished yet.</summary>
    Processing,

    /// <summary>Text extracted and chunked (M4 adds: chunks embedded). Answers can use it.</summary>
    Ready,

    /// <summary>The job gave up; <see cref="Document.FailureReason"/> says why.</summary>
    Failed,
}

/// <summary>
/// A policy file uploaded by an HR Admin. The raw bytes live in <see cref="DocumentFile"/> (kept out of this
/// entity so listing documents never loads megabytes), and the extracted text lives in <see cref="Chunk"/>s.
/// </summary>
public sealed class Document
{
    public const int MaxNameLength = 260;
    public const int MaxContentTypeLength = 100;
    public const int MaxUploadedByLength = 256;
    public const int MaxFailureReasonLength = 1000;

    private Document() { }   // for EF Core

    /// <summary>Assigned by EF Core when the entity is added (a sequential GUID, friendly to SQL Server's clustered index).</summary>
    public Guid Id { get; private set; }

    public string Name { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long SizeBytes { get; private set; }
    public string UploadedBy { get; private set; } = "";
    public DateTime UploadedAtUtc { get; private set; }
    public DocumentStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public int ChunkCount { get; private set; }

    public static Document Create(string name, string contentType, long sizeBytes, string uploadedBy, DateTime uploadedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedBy);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);

        return new Document
        {
            Name = Truncate(name, MaxNameLength),
            ContentType = contentType,
            SizeBytes = sizeBytes,
            UploadedBy = Truncate(uploadedBy, MaxUploadedByLength),
            UploadedAtUtc = uploadedAtUtc,
            Status = DocumentStatus.Processing,
        };
    }

    public void MarkReady(int chunkCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkCount);
        Status = DocumentStatus.Ready;
        ChunkCount = chunkCount;
        FailureReason = null;
    }

    public void MarkFailed(string reason)
    {
        Status = DocumentStatus.Failed;
        ChunkCount = 0;
        FailureReason = Truncate(string.IsNullOrWhiteSpace(reason) ? "Unknown error." : reason, MaxFailureReasonLength);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
