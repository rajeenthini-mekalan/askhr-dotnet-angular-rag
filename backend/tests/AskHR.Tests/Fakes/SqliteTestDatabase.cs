using AskHR.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AskHR.Tests.Fakes;

/// <summary>
/// The real AskHrDbContext model on an in-memory SQLite database: relational (foreign keys, cascade deletes,
/// unique indexes are enforced), fast, and needs no SQL Server. The database lives as long as the open connection.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    public SqliteConnection Connection { get; } = new("DataSource=:memory:");

    public SqliteTestDatabase()
    {
        Connection.Open();
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public DbContextOptions<AskHrDbContext> Options =>
        new DbContextOptionsBuilder<AskHrDbContext>().UseSqlite(Connection).Options;

    public AskHrDbContext CreateContext() => new(Options);

    public void Dispose() => Connection.Dispose();
}
