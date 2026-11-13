using System.Security.Claims;
using AskHR.Api.Auth;
using AskHR.Application.Documents;

namespace AskHR.Api.Documents;

public static class DocumentEndpoints
{
    /// <summary>/api/documents — HR Admin only: upload, list, get one, delete.</summary>
    public static RouteGroupBuilder MapDocumentEndpoints(this RouteGroupBuilder api)
    {
        var documents = api.MapGroup("/documents").RequireAuthorization(Policies.HrAdmin).WithTags("Documents");

        documents.MapGet("/", async (DocumentQueries queries, CancellationToken ct) =>
                Results.Ok(await queries.ListAsync(ct)))
            .WithName("ListDocuments")
            .Produces<IReadOnlyList<DocumentSummary>>();

        documents.MapGet("/{id:guid}", async (Guid id, DocumentQueries queries, CancellationToken ct) =>
                await queries.GetAsync(id, ct) is { } document ? Results.Ok(document) : Results.NotFound())
            .WithName("GetDocument")
            .Produces<DocumentSummary>()
            .Produces(StatusCodes.Status404NotFound);

        documents.MapPost("/", UploadAsync)
            .WithName("UploadDocument")
            // Antiforgery protects cookie-authenticated forms from CSRF. This API is called with a bearer token,
            // which a malicious site cannot make the browser attach, so the check does not apply here.
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<DocumentSummary>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem();

        documents.MapDelete("/{id:guid}", async (Guid id, DocumentQueries queries, CancellationToken ct) =>
                await queries.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .WithName("DeleteDocument")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return api;
    }

    /// <summary>
    /// Validates and stores the file, queues ingestion, and answers 202 Accepted at once: "I have it, the work is
    /// not done yet; poll the Location URL for the status".
    /// </summary>
    private static async Task<IResult> UploadAsync(IFormFile? file, UploadDocument upload, ClaimsPrincipal user, CancellationToken ct)
    {
        if (file is null)
            return FileError("Send the file as multipart/form-data in a field named 'file'.");

        // Read the bytes only when the declared length is within the limit (Kestrel already caps the body at 30 MB).
        byte[] content = file.Length is > 0 and <= UploadRules.MaxBytes ? await ReadAllAsync(file, ct) : [];

        var (contentType, error) = UploadRules.Check(file.FileName, file.Length, content.AsSpan(0, Math.Min(content.Length, 8192)));
        if (error is not null)
            return FileError(error);

        string uploadedBy = user.Identity?.Name ?? user.FindFirstValue("sub") ?? "unknown";
        DocumentSummary document = await upload.HandleAsync(file.FileName, contentType!, content, uploadedBy, ct);

        return Results.AcceptedAtRoute("GetDocument", new { id = document.Id }, document);
    }

    private static async Task<byte[]> ReadAllAsync(IFormFile file, CancellationToken ct)
    {
        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    private static IResult FileError(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [message] });
}
