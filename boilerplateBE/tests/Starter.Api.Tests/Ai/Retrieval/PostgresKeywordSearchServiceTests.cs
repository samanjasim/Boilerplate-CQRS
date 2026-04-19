using FluentAssertions;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

[Collection("AiPostgres")]
public sealed class PostgresKeywordSearchServiceTests : IClassFixture<AiPostgresFixture>
{
    private readonly AiPostgresFixture _fixture;

    public PostgresKeywordSearchServiceTests(AiPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SearchAsync_Returns_ChunksMatchingQueryTerms_ScopedToTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var uploaderA = Guid.NewGuid();
        var uploaderB = Guid.NewGuid();

        await using var db = _fixture.CreateDbContext();

        var docA = AiDocument.Create(tenantA, "Doc A", "doc-a.pdf", "ref-a", "application/pdf", 1024, uploaderA);
        var docB = AiDocument.Create(tenantB, "Doc B", "doc-b.pdf", "ref-b", "application/pdf", 1024, uploaderB);
        db.AiDocuments.AddRange(docA, docB);

        var chunkA1 = AiDocumentChunk.Create(docA.Id, "child", "photosynthesis light reactions chlorophyll", 0, 10, Guid.NewGuid());
        var chunkA2 = AiDocumentChunk.Create(docA.Id, "child", "mitosis prophase metaphase anaphase", 1, 10, Guid.NewGuid());
        var chunkB1 = AiDocumentChunk.Create(docB.Id, "child", "photosynthesis light reactions chlorophyll", 0, 10, Guid.NewGuid());
        db.AiDocumentChunks.AddRange(chunkA1, chunkA2, chunkB1);

        await db.SaveChangesAsync();

        var svc = new PostgresKeywordSearchService(db, _fixture.Logger<PostgresKeywordSearchService>());

        var results = await svc.SearchAsync(tenantA, "photosynthesis", null, 10, CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].ChunkId.Should().Be(chunkA1.Id);
        results[0].Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SearchAsync_Honours_DocumentFilter()
    {
        var tenant = Guid.NewGuid();
        var uploader = Guid.NewGuid();

        await using var db = _fixture.CreateDbContext();

        var docX = AiDocument.Create(tenant, "Doc X", "doc-x.pdf", "ref-x", "application/pdf", 512, uploader);
        var docY = AiDocument.Create(tenant, "Doc Y", "doc-y.pdf", "ref-y", "application/pdf", 512, uploader);
        db.AiDocuments.AddRange(docX, docY);

        var chunkX = AiDocumentChunk.Create(docX.Id, "child", "photosynthesis converts sunlight into energy", 0, 8, Guid.NewGuid());
        var chunkY = AiDocumentChunk.Create(docY.Id, "child", "photosynthesis occurs in chloroplasts", 0, 8, Guid.NewGuid());
        db.AiDocumentChunks.AddRange(chunkX, chunkY);

        await db.SaveChangesAsync();

        var svc = new PostgresKeywordSearchService(db, _fixture.Logger<PostgresKeywordSearchService>());

        var results = await svc.SearchAsync(tenant, "photosynthesis", [docX.Id], 10, CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].ChunkId.Should().Be(chunkX.Id);
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmpty()
    {
        await using var db = _fixture.CreateDbContext();
        var svc = new PostgresKeywordSearchService(db, _fixture.Logger<PostgresKeywordSearchService>());

        var results = await svc.SearchAsync(Guid.NewGuid(), "   ", null, 10, CancellationToken.None);

        results.Should().BeEmpty();
    }
}
