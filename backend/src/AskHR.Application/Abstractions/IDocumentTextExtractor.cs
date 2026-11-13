namespace AskHR.Application.Abstractions;

/// <summary>Turns an uploaded file into plain text. Infrastructure implements PDF (PdfPig) and .txt.</summary>
public interface IDocumentTextExtractor
{
    /// <exception cref="DocumentIngestionException">The file has no usable text (scanned PDF, encrypted, corrupt...).</exception>
    string ExtractText(byte[] content, string contentType);
}

/// <summary>
/// A problem with the document itself (not with our code or the database). The message is safe to show to an
/// HR Admin as the failure reason, and retrying will not help.
/// </summary>
public sealed class DocumentIngestionException(string message, Exception? inner = null) : Exception(message, inner);
