using AskHR.Application.Abstractions;
using AskHR.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AskHR.Infrastructure.Persistence;

public sealed class AskHrDbContext(DbContextOptions<AskHrDbContext> options) : DbContext(options), IAskHrDbContext
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentFile> DocumentFiles => Set<DocumentFile>();
    public DbSet<Chunk> Chunks => Set<Chunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AskHrDbContext).Assembly);
}

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.ToTable("Documents");
        b.HasKey(d => d.Id);
        b.Property(d => d.Name).HasMaxLength(Document.MaxNameLength).IsRequired();
        b.Property(d => d.ContentType).HasMaxLength(Document.MaxContentTypeLength).IsRequired();
        b.Property(d => d.UploadedBy).HasMaxLength(Document.MaxUploadedByLength).IsRequired();
        b.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);   // "Ready" in SSMS, not 1
        b.Property(d => d.FailureReason).HasMaxLength(Document.MaxFailureReasonLength);
        b.HasIndex(d => d.UploadedAtUtc);   // the list is sorted newest first

        // Both relationships have no navigation properties: the aggregate stays small and nothing loads by accident.
        // OnDelete(Cascade) makes SQL Server itself delete the file and the chunks when a document row is deleted.
        b.HasOne<DocumentFile>().WithOne().HasForeignKey<DocumentFile>(f => f.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany<Chunk>().WithOne().HasForeignKey(c => c.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DocumentFileConfiguration : IEntityTypeConfiguration<DocumentFile>
{
    public void Configure(EntityTypeBuilder<DocumentFile> b)
    {
        b.ToTable("DocumentFiles");
        b.HasKey(f => f.DocumentId);
        b.Property(f => f.Content).IsRequired();   // varbinary(max)
    }
}

internal sealed class ChunkConfiguration : IEntityTypeConfiguration<Chunk>
{
    public void Configure(EntityTypeBuilder<Chunk> b)
    {
        b.ToTable("Chunks");
        b.HasKey(c => c.Id);
        b.Property(c => c.Text).IsRequired();      // nvarchar(max): ~500 words is ~3,000 characters, over nvarchar(4000) for some languages
        b.HasIndex(c => new { c.DocumentId, c.Index }).IsUnique();
    }
}
