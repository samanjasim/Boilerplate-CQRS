using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Classification;
using Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;
using Starter.Module.AI.Infrastructure.Retrieval.Reranking;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

/// <summary>
/// End-to-end integration test for the Arabic query intelligence pipeline:
/// classify → (rule+LLM) rewrite → embed → hybrid fuse → rerank → neighbor-expand.
///
/// The regex classifier handles all five Arabic queries without an LLM call, so
/// provider.Calls per non-greeting case = rewriter (1) + listwise reranker (1) = 2.
/// For the greeting case the pipeline short-circuits after classify, so Calls = 0.
/// </summary>
public sealed class QueryIntelligencePipelineTests
{
    private static AiRagSettings BuildSettings() => new()
    {
        EnableQueryExpansion = true,
        ApplyArabicNormalization = true,
        NormalizeTaMarbuta = true,
        NormalizeArabicDigits = true,
        TopK = 3,
        RetrievalTopK = 10,
        NeighborWindowSize = 0,
        RerankStrategy = RerankStrategy.Listwise,
        ListwisePoolMultiplier = 1,
        MinHybridScore = 0.0m,
        VectorWeight = 1.0m,
        KeywordWeight = 1.0m,
        MaxContextTokens = 4000,
        IncludeParentContext = false,
    };

    private static (RagRetrievalService svc, FakeAiProvider provider, AiDbContext db) BuildPipeline(
        Guid tenantId,
        AiDocument doc,
        List<AiDocumentChunk> chunks,
        AiRagSettings settings)
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"qip-{Guid.NewGuid():N}")
            .Options;
        var db = new AiDbContext(options, currentUserService: null);
        db.AiDocuments.Add(doc);
        db.AiDocumentChunks.AddRange(chunks);
        db.SaveChanges();

        var provider = new FakeAiProvider();
        var factory = new FakeAiProviderFactory(provider);
        var cache = new FakeCacheService();
        var opts = Options.Create(settings);

        var classifier = new QuestionClassifier(factory, cache, opts, NullLogger<QuestionClassifier>.Instance);
        var rewriter = new QueryRewriter(factory, cache, opts, NullLogger<QueryRewriter>.Instance);

        var listwise = new ListwiseReranker(factory, cache, opts, NullLogger<ListwiseReranker>.Instance);
        var pointwise = new PointwiseReranker(factory, cache, opts, NullLogger<PointwiseReranker>.Instance);
        var selector = new RerankStrategySelector(settings);
        var reranker = new Reranker(selector, listwise, pointwise, opts, NullLogger<Reranker>.Instance);

        var neighbor = new NeighborExpander(db, NullLogger<NeighborExpander>.Instance);

        var vs = new FakeVectorStore
        {
            HitsToReturn = chunks
                .Where(c => c.ChunkLevel == "child")
                .Select((c, i) => new VectorSearchHit(c.QdrantPointId, 0.9m - (0.05m * i)))
                .Take(3)
                .ToList()
        };
        var kw = new FakeKeywordSearchService();

        var svc = new RagRetrievalService(
            db,
            vs,
            kw,
            new FakeEmbeddingService(),
            rewriter,
            classifier,
            reranker,
            selector,
            neighbor,
            new TokenCounter(),
            opts,
            NullLogger<RagRetrievalService>.Instance);

        return (svc, provider, db);
    }

    [Theory]
    [MemberData(nameof(ArabicQueries))]
    public async Task Arabic_pipeline_end_to_end(string name, JsonElement q)
    {
        var input = q.GetProperty("input").GetString()!;
        var expectedClass = Enum.Parse<QuestionType>(q.GetProperty("expectedClassification").GetString()!);
        var shortCircuit = q.TryGetProperty("expectShortCircuit", out var sc) && sc.GetBoolean();

        var tenantId = Guid.NewGuid();
        var doc = AiDocument.Create(
            tenantId,
            $"doc-{name}",
            "d.pdf",
            $"refs/{name}.pdf",
            "application/pdf",
            1024,
            Guid.NewGuid());
        var chunks = Enumerable.Range(0, 3)
            .Select(i => AiDocumentChunk.Create(
                doc.Id,
                "child",
                $"محتوى المقطع رقم {i}",
                i,
                10,
                Guid.NewGuid()))
            .ToList();

        var settings = BuildSettings();
        var (svc, provider, db) = BuildPipeline(tenantId, doc, chunks, settings);

        // The Arabic regex classifier handles all five queries without an LLM call,
        // so we do NOT enqueue a classifier response. LLM calls start at the rewriter.
        if (!shortCircuit)
        {
            var rewrite = q.TryGetProperty("expectedRewrite", out var rw) && rw.ValueKind != JsonValueKind.Null
                ? rw.GetString()!
                : input;
            provider.EnqueueContent($"[\"{rewrite}\", \"alt phrasing\"]"); // rewriter
            provider.EnqueueContent("[0,1,2]");                             // listwise reranker
        }

        var result = await svc.RetrieveForQueryAsync(
            tenantId,
            input,
            documentFilter: null,
            topK: 3,
            minScore: null,
            includeParents: false,
            ct: CancellationToken.None);

        if (shortCircuit)
        {
            // Greeting: regex short-circuits before any LLM call — classifier, rewriter,
            // and reranker are all skipped. Pipeline returns empty context.
            result.Children.Should().BeEmpty($"greeting '{name}' should short-circuit");
            result.Parents.Should().BeEmpty();
            provider.Calls.Should().Be(0,
                "regex classifier handles Arabic greetings without an LLM call");
        }
        else
        {
            result.Children.Should().NotBeEmpty($"non-greeting '{name}' should return results");
            result.DegradedStages.Should().BeEmpty($"no stages should degrade for '{name}'");
            // classifier=0 (regex) + rewriter=1 + listwise reranker=1 = 2 calls
            provider.Calls.Should().Be(2,
                $"rewriter + listwise reranker = 2 LLM calls for '{name}'");
        }

        await db.DisposeAsync();
    }

    public static IEnumerable<object[]> ArabicQueries()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Ai", "fixtures", "arabic_queries.json");
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        foreach (var q in doc.RootElement.GetProperty("queries").EnumerateArray())
            yield return new object[] { q.GetProperty("name").GetString()!, q.Clone() };
    }
}
