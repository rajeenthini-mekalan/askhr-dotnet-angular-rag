using System.Text;
using AskHR.Application.Abstractions;
using AskHR.Application.Documents;
using AskHR.Domain.Documents;
using AskHR.Infrastructure.Documents;
using AskHR.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AskHR.Tests.Documents;

/// <summary>The background job's use case, run directly (no Hangfire) against an in-memory SQLite database.</summary>
public sealed class IngestDocumentTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private async Task<Guid> SeedAsync(string text, string contentType = UploadRules.Text)
    {
        await using var db = _database.CreateContext();
        var document = Document.Create("policy.txt", contentType, 100, "admin@askhr.demo", DateTime.UtcNow);
        db.Documents.Add(document);
        db.DocumentFiles.Add(new DocumentFile(document.Id, Encoding.UTF8.GetBytes(text)));
        await db.SaveChangesAsync();
        return document.Id;
    }

    private async Task RunAsync(Guid id, IDocumentTextExtractor? extractor = null)
    {
        await using var db = _database.CreateContext();   // a fresh context, like a Hangfire job's own DI scope
        await new IngestDocument(db, extractor ?? new DocumentTextExtractor(), NullLogger<IngestDocument>.Instance)
            .HandleAsync(id);
    }

    private async Task<Document> LoadAsync(Guid id)
    {
        await using var db = _database.CreateContext();
        return await db.Documents.SingleAsync(d => d.Id == id);
    }

    [Fact]
    public async Task Text_document_becomes_Ready_with_its_chunks_in_order()
    {
        string text = string.Join(' ', Enumerable.Range(0, 1200).Select(i => $"w{i}"));
        var id = await SeedAsync(text);

        await RunAsync(id);

        var document = await LoadAsync(id);
        Assert.Equal(DocumentStatus.Ready, document.Status);
        Assert.Equal(3, document.ChunkCount);
        Assert.Null(document.FailureReason);

        await using var db = _database.CreateContext();
        var chunks = await db.Chunks.Where(c => c.DocumentId == id).OrderBy(c => c.Index).ToListAsync();
        Assert.Equal([0, 1, 2], chunks.Select(c => c.Index));
        Assert.StartsWith("w0 ", chunks[0].Text);
    }

    [Fact]
    public async Task Document_without_text_becomes_Failed_with_the_reason()
    {
        var id = await SeedAsync("   \r\n  ");

        await RunAsync(id);

        var document = await LoadAsync(id);
        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal("The document contains no text.", document.FailureReason);
    }

    [Fact]
    public async Task Extraction_problem_becomes_Failed_with_its_message_and_no_chunks()
    {
        var id = await SeedAsync("irrelevant");

        await RunAsync(id, new ThrowingExtractor(new DocumentIngestionException("The PDF is password-protected.")));

        var document = await LoadAsync(id);
        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal("The PDF is password-protected.", document.FailureReason);
        await using var db = _database.CreateContext();
        Assert.False(await db.Chunks.AnyAsync(c => c.DocumentId == id));
    }

    [Fact]
    public async Task Unexpected_error_becomes_Failed_without_leaking_details()
    {
        var id = await SeedAsync("irrelevant");

        await RunAsync(id, new ThrowingExtractor(new InvalidOperationException("secret internal detail")));

        var document = await LoadAsync(id);
        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal("Unexpected error (InvalidOperationException). See the server log.", document.FailureReason);
    }

    [Fact]
    public async Task Running_twice_does_not_duplicate_chunks()
    {
        var id = await SeedAsync("Annual leave is 18 days.");

        await RunAsync(id);
        await RunAsync(id);   // e.g. Hangfire retried after a crash

        await using var db = _database.CreateContext();
        Assert.Equal(1, await db.Chunks.CountAsync(c => c.DocumentId == id));
    }

    [Fact]
    public async Task Deleted_document_is_skipped_quietly() =>
        await RunAsync(Guid.NewGuid());   // does not throw

    private sealed class ThrowingExtractor(Exception exception) : IDocumentTextExtractor
    {
        public string ExtractText(byte[] content, string contentType) => throw exception;
    }
}
