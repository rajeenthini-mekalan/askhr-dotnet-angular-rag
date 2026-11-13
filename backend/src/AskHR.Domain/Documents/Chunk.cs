namespace AskHR.Domain.Documents;

/// <summary>
/// One passage of a document's text: the unit that RAG retrieves. M4 adds its embedding (a VECTOR column),
/// so a question can be matched against every chunk; M5 cites chunks as an answer's sources.
/// </summary>
public sealed class Chunk
{
    private Chunk() { }   // for EF Core

    public Chunk(Guid documentId, int index, string text)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        DocumentId = documentId;
        Index = index;
        Text = text;
    }

    /// <summary>Assigned by EF Core when the entity is added.</summary>
    public Guid Id { get; private set; }

    public Guid DocumentId { get; private set; }

    /// <summary>0-based position in the document, so chunks can be read back in order.</summary>
    public int Index { get; private set; }

    public string Text { get; private set; } = "";
}
