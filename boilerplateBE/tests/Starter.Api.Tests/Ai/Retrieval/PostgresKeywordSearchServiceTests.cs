using FluentAssertions;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Settings;
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

        var docA = AiDocument.Create(tenantA, "Doc A", "doc-a.pdf", Guid.NewGuid(), "application/pdf", 1024, uploaderA);
        var docB = AiDocument.Create(tenantB, "Doc B", "doc-b.pdf", Guid.NewGuid(), "application/pdf", 1024, uploaderB);
        db.AiDocuments.AddRange(docA, docB);

        var chunkA1 = AiDocumentChunk.Create(docA.Id, "child", "photosynthesis light reactions chlorophyll", 0, 10, Guid.NewGuid());
        var chunkA2 = AiDocumentChunk.Create(docA.Id, "child", "mitosis prophase metaphase anaphase", 1, 10, Guid.NewGuid());
        var chunkB1 = AiDocumentChunk.Create(docB.Id, "child", "photosynthesis light reactions chlorophyll", 0, 10, Guid.NewGuid());
        db.AiDocumentChunks.AddRange(chunkA1, chunkA2, chunkB1);

        await db.SaveChangesAsync();

        var svc = new PostgresKeywordSearchService(db, _fixture.Logger<PostgresKeywordSearchService>(), Options.Create(new AiRagSettings()));

        var results = await svc.SearchAsync(tenantA, "photosynthesis", null, 10, CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].ChunkId.Should().Be(chunkA1.QdrantPointId);
        results[0].Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SearchAsync_Honours_DocumentFilter()
    {
        var tenant = Guid.NewGuid();
        var uploader = Guid.NewGuid();

        await using var db = _fixture.CreateDbContext();

        var docX = AiDocument.Create(tenant, "Doc X", "doc-x.pdf", Guid.NewGuid(), "application/pdf", 512, uploader);
        var docY = AiDocument.Create(tenant, "Doc Y", "doc-y.pdf", Guid.NewGuid(), "application/pdf", 512, uploader);
        db.AiDocuments.AddRange(docX, docY);

        var chunkX = AiDocumentChunk.Create(docX.Id, "child", "photosynthesis converts sunlight into energy", 0, 8, Guid.NewGuid());
        var chunkY = AiDocumentChunk.Create(docY.Id, "child", "photosynthesis occurs in chloroplasts", 0, 8, Guid.NewGuid());
        db.AiDocumentChunks.AddRange(chunkX, chunkY);

        await db.SaveChangesAsync();

        var svc = new PostgresKeywordSearchService(db, _fixture.Logger<PostgresKeywordSearchService>(), Options.Create(new AiRagSettings()));

        var results = await svc.SearchAsync(tenant, "photosynthesis", [docX.Id], 10, CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].ChunkId.Should().Be(chunkX.QdrantPointId);
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmpty()
    {
        await using var db = _fixture.CreateDbContext();
        var svc = new PostgresKeywordSearchService(db, _fixture.Logger<PostgresKeywordSearchService>(), Options.Create(new AiRagSettings()));

        var results = await svc.SearchAsync(Guid.NewGuid(), "   ", null, 10, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Arabic_Query_Matches_Chunk_With_Different_Alef_Spelling()
    {
        var tenant = Guid.NewGuid();
        var uploader = Guid.NewGuid();

        await using var db = _fixture.CreateDbContext();

        var doc = AiDocument.Create(tenant, "Doc AR", "doc-ar.pdf", Guid.NewGuid(), "application/pdf", 512, uploader);
        db.AiDocuments.Add(doc);

        // Chunk body uses أ; query will use ا. After normalization both become ا.
        var chunk = AiDocumentChunk.Create(doc.Id, "child", "أكاديمي العلوم", 0, 5, Guid.NewGuid());
        chunk.SetNormalizedContent(Starter.Module.AI.Infrastructure.Retrieval.ArabicTextNormalizer.Normalize(
            chunk.Content,
            new Starter.Module.AI.Infrastructure.Retrieval.ArabicNormalizationOptions(true, true)));
        db.AiDocumentChunks.Add(chunk);
        await db.SaveChangesAsync();

        var settings = Options.Create(new AiRagSettings());
        var svc = new PostgresKeywordSearchService(db, _fixture.Logger<PostgresKeywordSearchService>(), settings);
        var results = await svc.SearchAsync(tenant, "اكاديمي", null, 10, CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].ChunkId.Should().Be(chunk.QdrantPointId);
    }

    [Fact]
    public async Task Arabic_Query_Matches_Through_Diacritics()
    {
        var tenant = Guid.NewGuid();
        var uploader = Guid.NewGuid();

        await using var db = _fixture.CreateDbContext();

        var doc = AiDocument.Create(tenant, "Doc AR2", "doc-ar2.pdf", Guid.NewGuid(), "application/pdf", 512, uploader);
        db.AiDocuments.Add(doc);

        var chunk = AiDocumentChunk.Create(doc.Id, "child", "مُؤَسَّسَة تعليمية", 0, 5, Guid.NewGuid());
        chunk.SetNormalizedContent(Starter.Module.AI.Infrastructure.Retrieval.ArabicTextNormalizer.Normalize(
            chunk.Content,
            new Starter.Module.AI.Infrastructure.Retrieval.ArabicNormalizationOptions(true, true)));
        db.AiDocumentChunks.Add(chunk);
        await db.SaveChangesAsync();

        var settings = Options.Create(new AiRagSettings());
        var svc = new PostgresKeywordSearchService(db, _fixture.Logger<PostgresKeywordSearchService>(), settings);
        var results = await svc.SearchAsync(tenant, "مؤسسه", null, 10, CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].ChunkId.Should().Be(chunk.QdrantPointId);
    }

    [Fact]
    public async Task Mixed_Content_Chunk_Keeps_English_Matching()
    {
        var tenant = Guid.NewGuid();
        var uploader = Guid.NewGuid();

        await using var db = _fixture.CreateDbContext();

        var doc = AiDocument.Create(tenant, "Doc Mixed", "mix.pdf", Guid.NewGuid(), "application/pdf", 512, uploader);
        db.AiDocuments.Add(doc);

        var chunk = AiDocumentChunk.Create(doc.Id, "child", "photosynthesis التمثيل الضوئي", 0, 5, Guid.NewGuid());
        chunk.SetNormalizedContent(Starter.Module.AI.Infrastructure.Retrieval.ArabicTextNormalizer.Normalize(
            chunk.Content,
            new Starter.Module.AI.Infrastructure.Retrieval.ArabicNormalizationOptions(true, true)));
        db.AiDocumentChunks.Add(chunk);
        await db.SaveChangesAsync();

        var settings = Options.Create(new AiRagSettings());
        var svc = new PostgresKeywordSearchService(db, _fixture.Logger<PostgresKeywordSearchService>(), settings);
        var results = await svc.SearchAsync(tenant, "photosynthesis", null, 10, CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].ChunkId.Should().Be(chunk.QdrantPointId);
    }

    [Fact]
    public async Task Keyword_query_matches_chunk_whose_body_lacks_term_but_breadcrumb_has_it()
    {
        var tenantId = Guid.NewGuid();
        var seeded = await _fixture.SeedChunkAsync(
            tenantId,
            content: "Rotational energy is transferred to the impeller.",
            normalizedContent: "Chapter 1 > Pumps\nRotational energy is transferred to the impeller.");

        await using var db = _fixture.CreateDbContext();
        var svc = new PostgresKeywordSearchService(
            db,
            _fixture.Logger<PostgresKeywordSearchService>(),
            Options.Create(new AiRagSettings()));

        var results = await svc.SearchAsync(tenantId, "Pumps", null, 5, CancellationToken.None);

        results.Should().ContainSingle();
        results.Single().ChunkId.Should().Be(seeded.Chunk.QdrantPointId);
    }
}
