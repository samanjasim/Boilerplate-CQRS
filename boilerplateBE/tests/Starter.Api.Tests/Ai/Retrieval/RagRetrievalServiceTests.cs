using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Reranking;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

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
        AiRagSettings? settings = null,
        IReranker? reranker = null,
        IQuestionClassifier? classifier = null,
        INeighborExpander? neighborExpander = null)
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
            new NoOpQueryRewriter(),
            new NoOpContextualQueryResolver(),
            classifier ?? new NoOpQuestionClassifier(),
            reranker ?? new NoOpReranker(),
            new RerankStrategySelector(ragSettings),
            neighborExpander ?? new NoOpNeighborExpander(),
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
        var assistant = AiAssistant.Create(tenantId, "A", null, "p", createdByUserId: Guid.NewGuid());
        // RagScope defaults to None — do not call SetRagScope

        var act = async () =>
            await svc.RetrieveForTurnAsync(assistant, "query", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

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

        var assistant = AiAssistant.Create(tenantId, "A", null, "p", createdByUserId: Guid.NewGuid());
        assistant.SetKnowledgeBase([docId]);
        assistant.SetRagScope(AiRagScope.SelectedDocuments);

        await svc.RetrieveForTurnAsync(assistant, "query", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

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
        var assistant = AiAssistant.Create(tenantId, "A", null, "p", createdByUserId: Guid.NewGuid());
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        await svc.RetrieveForTurnAsync(assistant, "query", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        fakeVs.LastDocFilter.Should().BeNull();
    }

    [Fact]
    public async Task Both_Search_Sides_Empty_Returns_Empty_Context()
    {
        await using var db = CreateDb();
        var svc = BuildService(db);

        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "A", null, "p", createdByUserId: Guid.NewGuid());
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var ctx = await svc.RetrieveForTurnAsync(assistant, "query", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        ctx.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task Retrieve_reranker_reorders_fused_candidates()
    {
        await using var db = CreateDb();

        // Seed 4 pool candidates (no parents — this test is about child ordering).
        var documentId = Guid.NewGuid();
        var chunks = new List<AiDocumentChunk>();
        for (var i = 0; i < 4; i++)
        {
            var chunk = AiDocumentChunk.Create(
                documentId: documentId,
                chunkLevel: "child",
                content: $"excerpt body {i}",
                chunkIndex: i,
                tokenCount: 5,
                qdrantPointId: Guid.NewGuid());
            db.AiDocumentChunks.Add(chunk);
            chunks.Add(chunk);
        }
        await db.SaveChangesAsync();

        // RRF order = [chunk0, chunk1, chunk2, chunk3] (by descending vector score).
        var fakeVs = new FakeVectorStore
        {
            HitsToReturn =
            [
                new VectorSearchHit(chunks[0].QdrantPointId, 0.9m),
                new VectorSearchHit(chunks[1].QdrantPointId, 0.8m),
                new VectorSearchHit(chunks[2].QdrantPointId, 0.7m),
                new VectorSearchHit(chunks[3].QdrantPointId, 0.6m),
            ]
        };

        var settings = new AiRagSettings
        {
            TopK = 2,
            RetrievalTopK = 20,
            VectorWeight = 1.0m,
            KeywordWeight = 1.0m,
            MaxContextTokens = 4000,
            IncludeParentContext = false,
            MinHybridScore = 0.0m,
            RerankStrategy = RerankStrategy.Listwise,
            ListwisePoolMultiplier = 2, // pool size = max(2, 2*2) = 4
        };

        // Reranker reorders to [1, 0, 2, 3]; trim to topK=2 → [chunk1, chunk0].
        var provider = new FakeAiProvider();
        provider.EnqueueContent("[1, 0, 2, 3]");
        var cache = new FakeCacheService();
        var factory = new FakeAiProviderFactory(provider);
        var opts = Options.Create(settings);
        var listwise = new ListwiseReranker(factory, cache, opts, NullLogger<ListwiseReranker>.Instance);
        var pointwise = new PointwiseReranker(factory, cache, opts, NullLogger<PointwiseReranker>.Instance);
        var selector = new RerankStrategySelector(settings);
        var reranker = new Reranker(selector, listwise, pointwise, opts, NullLogger<Reranker>.Instance);

        var svc = BuildService(db, vs: fakeVs, settings: settings, reranker: reranker);

        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "A", null, "p", createdByUserId: Guid.NewGuid());
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var ctx = await svc.RetrieveForTurnAsync(assistant, "query", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        ctx.Children.Should().HaveCount(2);
        ctx.Children[0].ChunkId.Should().Be(chunks[1].Id);
        ctx.Children[1].ChunkId.Should().Be(chunks[0].Id);
        provider.Calls.Should().Be(1);
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
        var assistant = AiAssistant.Create(tenantId, "A", null, "p", createdByUserId: Guid.NewGuid());
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var ctx = await svc.RetrieveForTurnAsync(assistant, "query", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        ctx.Children.Count.Should().Be(2);
        ctx.Parents.Count.Should().Be(1);
        ctx.Parents[0].ChunkId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task Greeting_short_circuits_and_returns_empty_context()
    {
        await using var db = CreateDb();
        var classifier = new FakeQuestionClassifier(QuestionType.Greeting);
        var svc = BuildService(db, classifier: classifier);

        var result = await svc.RetrieveForQueryAsync(
            tenantId: Guid.NewGuid(),
            queryText: "hi there",
            documentFilter: null,
            topK: 5,
            minScore: null,
            includeParents: true,
            ct: CancellationToken.None);

        result.Children.Should().BeEmpty();
        result.Parents.Should().BeEmpty();
        result.DegradedStages.Should().NotContain(RagStages.EmbedQuery);
    }

    [Fact]
    public async Task QuestionType_is_threaded_into_rerank_context()
    {
        await using var db = CreateDb();

        // Seed 1 child chunk
        var documentId = Guid.NewGuid();
        var chunk = AiDocumentChunk.Create(
            documentId: documentId,
            chunkLevel: "child",
            content: "why did the system fail explanation",
            chunkIndex: 0,
            tokenCount: 8,
            qdrantPointId: Guid.NewGuid());
        db.AiDocumentChunks.Add(chunk);
        await db.SaveChangesAsync();

        var fakeVs = new FakeVectorStore
        {
            HitsToReturn =
            [
                new VectorSearchHit(chunk.QdrantPointId, 0.85m),
            ]
        };

        var classifier = new FakeQuestionClassifier(QuestionType.Reasoning);
        var reranker = new CapturingReranker();

        var svc = BuildService(db, vs: fakeVs, classifier: classifier, reranker: reranker);

        await svc.RetrieveForQueryAsync(
            tenantId: Guid.NewGuid(),
            queryText: "why did the system fail",
            documentFilter: null,
            topK: 5,
            minScore: null,
            includeParents: false,
            ct: CancellationToken.None);

        reranker.CapturedContext.Should().NotBeNull();
        reranker.CapturedContext!.QuestionType.Should().Be(QuestionType.Reasoning);
    }

    [Fact]
    public async Task Retrieve_populates_Siblings_when_neighbor_window_positive()
    {
        await using var db = CreateDb();

        var documentId = Guid.NewGuid();
        var chunk = AiDocumentChunk.Create(
            documentId: documentId,
            chunkLevel: "child",
            content: "anchor chunk content",
            chunkIndex: 5,
            tokenCount: 5,
            qdrantPointId: Guid.NewGuid());
        db.AiDocumentChunks.Add(chunk);
        await db.SaveChangesAsync();

        var fakeVs = new FakeVectorStore
        {
            HitsToReturn = [new VectorSearchHit(chunk.QdrantPointId, 0.9m)]
        };

        var sibling1 = new RetrievedChunk(Guid.NewGuid(), documentId, "doc-x", "sib content A", null, null, "child", 0, 0, 0, null, 4);
        var sibling2 = new RetrievedChunk(Guid.NewGuid(), documentId, "doc-x", "sib content B", null, null, "child", 0, 0, 0, null, 6);
        var fakeExpander = new FakeNeighborExpander([sibling1, sibling2]);

        var settings = new AiRagSettings
        {
            TopK = 5,
            RetrievalTopK = 20,
            VectorWeight = 1.0m,
            KeywordWeight = 1.0m,
            MaxContextTokens = 4000,
            IncludeParentContext = false,
            MinHybridScore = 0.0m,
            NeighborWindowSize = 2
        };

        var svc = BuildService(db, vs: fakeVs, settings: settings, neighborExpander: fakeExpander);

        var ctx = await svc.RetrieveForQueryAsync(
            tenantId: Guid.NewGuid(),
            queryText: "test query",
            documentFilter: null,
            topK: 5,
            minScore: null,
            includeParents: false,
            ct: CancellationToken.None);

        ctx.Siblings.Should().HaveCount(2);
        fakeExpander.WasCalled.Should().BeTrue();
        fakeExpander.CapturedAnchorCount.Should().Be(1);
    }

    [Fact]
    public async Task Retrieve_skips_neighbor_expansion_when_window_zero()
    {
        await using var db = CreateDb();

        var documentId = Guid.NewGuid();
        var chunk = AiDocumentChunk.Create(
            documentId: documentId,
            chunkLevel: "child",
            content: "anchor chunk content",
            chunkIndex: 5,
            tokenCount: 5,
            qdrantPointId: Guid.NewGuid());
        db.AiDocumentChunks.Add(chunk);
        await db.SaveChangesAsync();

        var fakeVs = new FakeVectorStore
        {
            HitsToReturn = [new VectorSearchHit(chunk.QdrantPointId, 0.9m)]
        };

        var throwingExpander = new ThrowingNeighborExpander();

        var settings = new AiRagSettings
        {
            TopK = 5,
            RetrievalTopK = 20,
            VectorWeight = 1.0m,
            KeywordWeight = 1.0m,
            MaxContextTokens = 4000,
            IncludeParentContext = false,
            MinHybridScore = 0.0m,
            NeighborWindowSize = 0
        };

        var svc = BuildService(db, vs: fakeVs, settings: settings, neighborExpander: throwingExpander);

        var ctx = await svc.RetrieveForQueryAsync(
            tenantId: Guid.NewGuid(),
            queryText: "test query",
            documentFilter: null,
            topK: 5,
            minScore: null,
            includeParents: false,
            ct: CancellationToken.None);

        ctx.Siblings.Should().BeEmpty();
        throwingExpander.WasCalled.Should().BeFalse();
    }
}
