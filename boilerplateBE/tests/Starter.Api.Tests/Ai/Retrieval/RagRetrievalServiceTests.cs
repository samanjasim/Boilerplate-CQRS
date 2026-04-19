using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

internal sealed class FakeEmbeddingService : IEmbeddingService
{
    public int VectorSize => 1536;

    public Task<float[][]> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct,
        EmbedAttribution? attribution = null,
        AiRequestType requestType = AiRequestType.Embedding)
        => Task.FromResult(texts.Select(_ => new float[1536]).ToArray());
}

public sealed class RagRetrievalServiceTests
{
    private static AiDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-{Guid.NewGuid():N}")
            .Options;
        return new AiDbContext(options, currentUserService: null);
    }

    private static RagRetrievalService BuildService(
        AiDbContext db,
        FakeVectorStore? vs = null,
        FakeKeywordSearchService? kw = null,
        AiRagSettings? settings = null)
    {
        var ragSettings = settings ?? new AiRagSettings
        {
            TopK = 5,
            RetrievalTopK = 20,
            VectorWeight = 1.0m,
            KeywordWeight = 1.0m,
            MaxContextTokens = 4000,
            IncludeParentContext = true,
            MinHybridScore = 0.0m
        };

        return new RagRetrievalService(
            db,
            vs ?? new FakeVectorStore(),
            kw ?? new FakeKeywordSearchService(),
            new FakeEmbeddingService(),
            new TokenCounter(),
            Options.Create(ragSettings),
            NullLogger<RagRetrievalService>.Instance);
    }

    private static AiDocumentChunk SeedParentChunk(AiDbContext db)
    {
        var chunk = AiDocumentChunk.Create(
            documentId: Guid.NewGuid(),
            chunkLevel: "parent",
            content: "parent content body",
            chunkIndex: 0,
            tokenCount: 10,
            qdrantPointId: Guid.NewGuid());
        db.AiDocumentChunks.Add(chunk);
        return chunk;
    }

    private static AiDocumentChunk SeedChildChunk(AiDbContext db, Guid parentId, Guid documentId)
    {
        var chunk = AiDocumentChunk.Create(
            documentId: documentId,
            chunkLevel: "child",
            content: "child chunk content",
            chunkIndex: 0,
            tokenCount: 5,
            qdrantPointId: Guid.NewGuid(),
            parentChunkId: parentId);
        db.AiDocumentChunks.Add(chunk);
        return chunk;
    }

    [Fact]
    public void RetrievedContext_Empty_HasEmptyDegradedStages()
    {
        RetrievedContext.Empty.DegradedStages.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task RagScope_None_Throws()
    {
        await using var db = CreateDb();
        var svc = BuildService(db);

        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "A", null, "p");
        // RagScope defaults to None — do not call SetRagScope

        var act = async () =>
            await svc.RetrieveForTurnAsync(assistant, "query", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*RagScope != None*");
    }

    [Fact]
    public async Task SelectedDocuments_Filters_Qdrant_To_DocIds()
    {
        await using var db = CreateDb();
        var fakeVs = new FakeVectorStore { HitsToReturn = [] };
        var svc = BuildService(db, vs: fakeVs);

        var tenantId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        var assistant = AiAssistant.Create(tenantId, "A", null, "p");
        assistant.SetKnowledgeBase([docId]);
        assistant.SetRagScope(AiRagScope.SelectedDocuments);

        await svc.RetrieveForTurnAsync(assistant, "query", CancellationToken.None);

        fakeVs.LastDocFilter.Should().NotBeNull();
        fakeVs.LastDocFilter.Should().Contain(docId);
        fakeVs.LastDocFilter.Should().HaveCount(1);
    }

    [Fact]
    public async Task AllTenantDocuments_Sends_Null_DocFilter()
    {
        await using var db = CreateDb();
        var fakeVs = new FakeVectorStore { HitsToReturn = [] };
        var svc = BuildService(db, vs: fakeVs);

        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "A", null, "p");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        await svc.RetrieveForTurnAsync(assistant, "query", CancellationToken.None);

        fakeVs.LastDocFilter.Should().BeNull();
    }

    [Fact]
    public async Task Both_Search_Sides_Empty_Returns_Empty_Context()
    {
        await using var db = CreateDb();
        var svc = BuildService(db);

        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "A", null, "p");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var ctx = await svc.RetrieveForTurnAsync(assistant, "query", CancellationToken.None);

        ctx.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task TopK_Plus_Parent_Dedup_Work()
    {
        await using var db = CreateDb();

        var parent = SeedParentChunk(db);
        var childA = SeedChildChunk(db, parent.Id, parent.DocumentId);
        var childB = SeedChildChunk(db, parent.Id, parent.DocumentId);
        await db.SaveChangesAsync();

        var fakeVs = new FakeVectorStore
        {
            HitsToReturn =
            [
                new VectorSearchHit(childA.QdrantPointId, 0.9m),
                new VectorSearchHit(childB.QdrantPointId, 0.8m)
            ]
        };

        var settings = new AiRagSettings
        {
            TopK = 5,
            RetrievalTopK = 20,
            VectorWeight = 1.0m,
            KeywordWeight = 1.0m,
            MaxContextTokens = 4000,
            IncludeParentContext = true,
            MinHybridScore = 0.0m
        };

        var svc = BuildService(db, vs: fakeVs, settings: settings);

        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "A", null, "p");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var ctx = await svc.RetrieveForTurnAsync(assistant, "query", CancellationToken.None);

        ctx.Children.Count.Should().Be(2);
        ctx.Parents.Count.Should().Be(1);
        ctx.Parents[0].ChunkId.Should().Be(parent.Id);
    }
}
