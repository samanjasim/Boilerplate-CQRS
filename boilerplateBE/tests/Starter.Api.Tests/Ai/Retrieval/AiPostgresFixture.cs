using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class AiPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("ai_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public AiDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AiDbContext(options, currentUserService: null);
    }

    public Microsoft.Extensions.Logging.ILogger<T> Logger<T>() => NullLogger<T>.Instance;

    public async Task<(AiDocument Document, AiDocumentChunk Chunk)> SeedChunkAsync(
        Guid tenantId,
        string content,
        string normalizedContent,
        string? sectionTitle = null)
    {
        await using var db = CreateDbContext();
        var doc = AiDocument.Create(
            tenantId: tenantId,
            name: "seed",
            fileName: "seed.txt",
            fileRef: "ref://seed",
            contentType: "text/plain",
            sizeBytes: content.Length,
            uploadedByUserId: Guid.NewGuid());
        db.AiDocuments.Add(doc);
        var chunk = AiDocumentChunk.Create(
            documentId: doc.Id,
            chunkLevel: "child",
            content: content,
            chunkIndex: 0,
            tokenCount: 1,
            qdrantPointId: Guid.NewGuid(),
            sectionTitle: sectionTitle);
        chunk.SetNormalizedContent(normalizedContent);
        db.AiDocumentChunks.Add(chunk);
        await db.SaveChangesAsync();
        return (doc, chunk);
    }
}
