using AskHR.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace AskHR.Application.Abstractions;

/// <summary>
/// The database as the use cases see it. Application depends on EF Core's abstractions (DbSet, LINQ) but not on
/// SQL Server: the real implementation is AskHrDbContext in Infrastructure, and tests can back it with SQLite.
/// </summary>
public interface IAskHrDbContext
{
    DbSet<Document> Documents { get; }
    DbSet<DocumentFile> DocumentFiles { get; }
    DbSet<Chunk> Chunks { get; }

    /// <summary>DbContext already has this; exposed so a use case can discard unsaved changes after a failure.</summary>
    Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
