using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Api.Tests.Ai.Retrieval;
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

namespace Starter.Api.Tests.Ai.Retrieval.Diversification;

public sealed class MmrIntegrationTests
{
    private static AiDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-mmr-{Guid.NewGuid():N}").Options;
        return new AiDbContext(options, currentUserService: null);
    }

    private static RagRetrievalService BuildService(
        AiDbContext db,
        FakeVectorStore vs,
        AiRagSettings settings)
    {
        return new RagRetrievalService(
            db,
            vs,
            new FakeKeywordSearchService(),
            new FakeEmbeddingService(),
            new NoOpQueryRewriter(),
            new NoOpContextualQueryResolver(),
            new NoOpQuestionClassifier(),
            new NoOpReranker(),
            new RerankStrategySelector(settings),
            new NoOpNeighborExpander(),
            new TokenCounter(),
            new FakeResourceAccessService(),
            new FakeCurrentUserService(),
            Options.Create(settings),
            NullLogger<RagRetrievalService>.Instance);
    }

    private static async Task<List<AiDocumentChunk>> SeedChunksAsync(AiDbContext db, int count)
    {
        var documentId = Guid.NewGuid();
        var chunks = new List<AiDocumentChunk>();
        for (var i = 0; i < count; i++)
        {
            var chunk = AiDocumentChunk.Create(
                documentId: documentId,
                chunkLevel: "child",
                content: $"excerpt {i}",
                chunkIndex: i,
                tokenCount: 5,
                qdrantPointId: Guid.NewGuid());
            db.AiDocumentChunks.Add(chunk);
            chunks.Add(chunk);
        }
        await db.SaveChangesAsync();
        return chunks;
    }

    [Fact]
    public async Task EnableMmr_false_does_not_call_GetVectorsByIds()
    {
        await using var db = CreateDb();
        var chunks = await SeedChunksAsync(db, 6);

        var vs = new FakeVectorStore
        {
            HitsToReturn =
            [
                new VectorSearchHit(chunks[0].QdrantPointId, 0.9m),
                new VectorSearchHit(chunks[1].QdrantPointId, 0.85m),
                new VectorSearchHit(chunks[2].QdrantPointId, 0.8m),
                new VectorSearchHit(chunks[3].QdrantPointId, 0.75m),
                new VectorSearchHit(chunks[4].QdrantPointId, 0.7m),
                new VectorSearchHit(chunks[5].QdrantPointId, 0.65m),
            ]
        };

        var settings = new AiRagSettings
        {
            TopK = 3,
            RetrievalTopK = 20,
            EnableMmr = false,
            IncludeParentContext = false,
        };

        var svc = BuildService(db, vs, settings);

        var assistant = AiAssistant.Create(Guid.NewGuid(), "A", null, "p", createdByUserId: Guid.NewGuid());
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var ctx = await svc.RetrieveForTurnAsync(
            assistant, "query", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        ctx.Children.Should().HaveCount(3);
        vs.GetVectorsByIdsCallCount.Should().Be(0);
        ctx.DegradedStages.Should().NotContain(RagStages.MmrDiversify);
    }

    [Fact]
    public async Task EnableMmr_true_diversifies_topK_across_clusters()
    {
        await using var db = CreateDb();
        var chunks = await SeedChunksAsync(db, 4);

        // Three near-duplicates plus one distinct chunk.
        var vs = new FakeVectorStore
        {
            HitsToReturn =
            [
                new VectorSearchHit(chunks[0].QdrantPointId, 0.99m),
                new VectorSearchHit(chunks[1].QdrantPointId, 0.98m),
                new VectorSearchHit(chunks[2].QdrantPointId, 0.97m),
                new VectorSearchHit(chunks[3].QdrantPointId, 0.50m),
            ]
        };
        vs.VectorsById[chunks[0].QdrantPointId] = [1f, 0f];
        vs.VectorsById[chunks[1].QdrantPointId] = [1f, 0f];
        vs.VectorsById[chunks[2].QdrantPointId] = [1f, 0f];
        vs.VectorsById[chunks[3].QdrantPointId] = [0f, 1f];

        var settings = new AiRagSettings
        {
            TopK = 2,
            RetrievalTopK = 20,
            EnableMmr = true,
            MmrLambda = 0.5,
            IncludeParentContext = false,
        };

        var svc = BuildService(db, vs, settings);

        var assistant = AiAssistant.Create(Guid.NewGuid(), "A", null, "p", createdByUserId: Guid.NewGuid());
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var ctx = await svc.RetrieveForTurnAsync(
            assistant, "query", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        ctx.Children.Should().HaveCount(2);
        var selected = ctx.Children.Select(c => c.ChunkId).ToHashSet();
        selected.Should().Contain(chunks[0].Id, "highest relevance anchor must be picked first");
        selected.Should().Contain(chunks[3].Id, "diverse outlier must beat the near-duplicates at λ=0.5");
        vs.GetVectorsByIdsCallCount.Should().Be(1);
        ctx.DegradedStages.Should().NotContain(RagStages.MmrDiversify);
    }

    [Fact]
    public async Task EnableMmr_true_degrades_when_all_embeddings_missing()
    {
        await using var db = CreateDb();
        var chunks = await SeedChunksAsync(db, 4);

        // Search returns 4 reranked hits, but VectorsById is empty — simulates
        // eventual consistency between DB and Qdrant (point ids exist in the chunk
        // table but aren't yet in the vector store). MMR must surface this as a
        // degraded stage even though GetVectorsByIdsAsync did not throw.
        var vs = new FakeVectorStore
        {
            HitsToReturn =
            [
                new VectorSearchHit(chunks[0].QdrantPointId, 0.99m),
                new VectorSearchHit(chunks[1].QdrantPointId, 0.98m),
                new VectorSearchHit(chunks[2].QdrantPointId, 0.97m),
                new VectorSearchHit(chunks[3].QdrantPointId, 0.96m),
            ]
        };

        var settings = new AiRagSettings
        {
            TopK = 2,
            RetrievalTopK = 20,
            EnableMmr = true,
            MmrLambda = 0.5,
            IncludeParentContext = false,
        };

        var svc = BuildService(db, vs, settings);

        var assistant = AiAssistant.Create(Guid.NewGuid(), "A", null, "p", createdByUserId: Guid.NewGuid());
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var ctx = await svc.RetrieveForTurnAsync(
            assistant, "query", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        ctx.DegradedStages.Should().Contain(RagStages.MmrDiversify);
        ctx.Children.Should().HaveCount(2);
        ctx.Children[0].ChunkId.Should().Be(chunks[0].Id);
        ctx.Children[1].ChunkId.Should().Be(chunks[1].Id);
    }

    private sealed class FailingVectorStore : IVectorStore
    {
        private readonly FakeVectorStore _inner;
        public FailingVectorStore(FakeVectorStore inner) => _inner = inner;

        public Task EnsureCollectionAsync(Guid t, int vs, CancellationToken ct) => Task.CompletedTask;
        public Task UpsertAsync(Guid t, IReadOnlyList<VectorPoint> p, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteByDocumentAsync(Guid t, Guid d, CancellationToken ct) => Task.CompletedTask;
        public Task DropCollectionAsync(Guid t, CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
            Guid t, float[] q, IReadOnlyCollection<Guid>? f, AclPayloadFilter? acl, int l, CancellationToken ct)
            => _inner.SearchAsync(t, q, f, acl, l, ct);

        public Task<IReadOnlyDictionary<Guid, float[]>> GetVectorsByIdsAsync(
            Guid t, IReadOnlyCollection<Guid> ids, CancellationToken ct)
            => throw new HttpRequestException("Qdrant unreachable");
    }

    [Fact]
    public async Task EnableMmr_true_degrades_to_rerank_order_when_GetVectorsByIds_throws()
    {
        await using var db = CreateDb();
        var chunks = await SeedChunksAsync(db, 4);

        var innerVs = new FakeVectorStore
        {
            HitsToReturn =
            [
                new VectorSearchHit(chunks[0].QdrantPointId, 0.99m),
                new VectorSearchHit(chunks[1].QdrantPointId, 0.98m),
                new VectorSearchHit(chunks[2].QdrantPointId, 0.97m),
                new VectorSearchHit(chunks[3].QdrantPointId, 0.96m),
            ]
        };
        var vs = new FailingVectorStore(innerVs);

        var settings = new AiRagSettings
        {
            TopK = 2,
            RetrievalTopK = 20,
            EnableMmr = true,
            MmrLambda = 0.5,
            IncludeParentContext = false,
        };

        // FailingVectorStore delegates SearchAsync to the inner fake but throws on
        // GetVectorsByIdsAsync, isolating the MMR stage as the failure source.
        var svc = new RagRetrievalService(
            db,
            vs,
            new FakeKeywordSearchService(),
            new FakeEmbeddingService(),
            new NoOpQueryRewriter(),
            new NoOpContextualQueryResolver(),
            new NoOpQuestionClassifier(),
            new NoOpReranker(),
            new RerankStrategySelector(settings),
            new NoOpNeighborExpander(),
            new TokenCounter(),
            new FakeResourceAccessService(),
            new FakeCurrentUserService(),
            Options.Create(settings),
            NullLogger<RagRetrievalService>.Instance);

        var assistant = AiAssistant.Create(Guid.NewGuid(), "A", null, "p", createdByUserId: Guid.NewGuid());
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var ctx = await svc.RetrieveForTurnAsync(
            assistant, "query", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        // MMR degraded → fall back to rerank order (RRF order since NoOpReranker).
        ctx.DegradedStages.Should().Contain(RagStages.MmrDiversify);
        ctx.Children.Should().HaveCount(2);
        ctx.Children[0].ChunkId.Should().Be(chunks[0].Id);
        ctx.Children[1].ChunkId.Should().Be(chunks[1].Id);
    }
}
