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
using Starter.Module.AI.Infrastructure.Retrieval.Resilience;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Resilience;

public class RagRetrievalDegradationWithBreakerTests
{
    [Fact]
    public async Task Open_qdrant_circuit_degrades_vector_stage_and_keyword_hits_still_flow()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var dbOptions = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-breaker-e2e-{Guid.NewGuid():N}").Options;
        await using var db = new AiDbContext(dbOptions, currentUserService: null);

        var chunkPointId = Guid.NewGuid();
        var chunk = AiDocumentChunk.Create(
            documentId: documentId,
            chunkLevel: "child",
            content: "Qdrant is a vector database",
            chunkIndex: 0,
            tokenCount: 10,
            qdrantPointId: chunkPointId);
        db.AiDocumentChunks.Add(chunk);
        await db.SaveChangesAsync();

        var vectorInner = new ThrowingVectorStoreForBreakerTest();
        var keywordInner = new FakeKeywordSearchService
        {
            HitsToReturn = { new KeywordSearchHit(chunkPointId, 0.9m) }
        };

        var settings = new AiRagSettings
        {
            TopK = 3,
            RetrievalTopK = 3,
            RerankStrategy = RerankStrategy.Off,
            StageTimeoutVectorMs = 500,
            StageTimeoutKeywordMs = 1000,
            MinHybridScore = 0m,
            CircuitBreakers = new RagCircuitBreakerSettings
            {
                Qdrant = new RagCircuitBreakerOptions
                {
                    Enabled = true,
                    MinimumThroughput = 2,
                    FailureRatio = 0.5,
                    BreakDurationMs = 60_000,
                },
                PostgresFts = new RagCircuitBreakerOptions { Enabled = true },
            }
        };
        var registry = new RagCircuitBreakerRegistry(
            Options.Create(settings), NullLogger<RagCircuitBreakerRegistry>.Instance);

        var breaker = new CircuitBreakingVectorStore(vectorInner, registry);

        // Trip the breaker: raw TimeoutExceptions from the inner store surface first;
        // once MinimumThroughput samples have failed, the breaker flips to Open and
        // subsequent calls short-circuit with BrokenCircuitException.
        for (var i = 0; i < 2; i++)
        {
            try
            {
                await breaker.SearchAsync(tenantId, Array.Empty<float>(), null, 1, CancellationToken.None);
            }
            catch (TimeoutException) { /* expected */ }
        }

        var svc = new RagRetrievalService(
            db,
            breaker,
            keywordInner,
            new FakeEmbeddingService(),
            new NoOpQueryRewriter(),
            new NoOpContextualQueryResolver(),
            new NoOpQuestionClassifier(),
            new NoOpReranker(),
            new RerankStrategySelector(settings),
            new NoOpNeighborExpander(),
            new TokenCounter(),
            Options.Create(settings),
            NullLogger<RagRetrievalService>.Instance);

        var assistant = AiAssistant.Create(tenantId, "t", null, "x");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var ctx = await svc.RetrieveForTurnAsync(
            assistant, "qdrant", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        ctx.DegradedStages.Should().Contain(s => s.StartsWith("vector-search"));
        ctx.Children.Should().NotBeEmpty(
            "keyword hits must still flow even while the vector circuit is open");
    }

    private sealed class ThrowingVectorStoreForBreakerTest : IVectorStore
    {
        public Task EnsureCollectionAsync(Guid t, int s, CancellationToken ct) => Task.CompletedTask;
        public Task UpsertAsync(Guid t, IReadOnlyList<VectorPoint> p, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteByDocumentAsync(Guid t, Guid d, CancellationToken ct) => Task.CompletedTask;
        public Task DropCollectionAsync(Guid t, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
            Guid t, float[] v, IReadOnlyCollection<Guid>? d, int limit, CancellationToken ct)
            => throw new TimeoutException("simulated qdrant outage");
        public Task<IReadOnlyDictionary<Guid, float[]>> GetVectorsByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> pointIds, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<Guid, float[]>>(new Dictionary<Guid, float[]>());
    }
}
