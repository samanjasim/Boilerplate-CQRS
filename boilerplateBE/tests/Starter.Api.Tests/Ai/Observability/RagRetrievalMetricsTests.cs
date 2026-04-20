using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Api.Tests.Ai.Retrieval;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Observability;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Reranking;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Observability;

[Collection(ObservabilityTestCollection.Name)]
public class RagRetrievalMetricsTests
{
    [Fact]
    public async Task WithTimeoutAsync_records_duration_and_success_on_happy_path()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var degraded = new List<string>();
        var result = await RagRetrievalServiceTestHarness.RunWithTimeoutAsync(
            op: async ct => { await Task.Delay(5, ct); return "ok"; },
            timeoutMs: 500,
            stageName: "vector-search",
            degraded: degraded);

        result.Should().Be("ok");
        degraded.Should().BeEmpty();

        var snapshot = listener.Snapshot();
        snapshot.Should().Contain(m => m.InstrumentName == "rag.stage.duration"
                                       && (string?)m.Tags["rag.stage"] == "vector-search");
        snapshot.Should().Contain(m => m.InstrumentName == "rag.stage.outcome"
                                       && (string?)m.Tags["rag.stage"] == "vector-search"
                                       && (string?)m.Tags["rag.outcome"] == "success"
                                       && m.Value == 1);
    }

    [Fact]
    public async Task WithTimeoutAsync_records_timeout_outcome_when_op_exceeds_budget()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var degraded = new List<string>();
        var result = await RagRetrievalServiceTestHarness.RunWithTimeoutAsync<string>(
            op: async ct => { await Task.Delay(200, ct); return "too-slow"; },
            timeoutMs: 20,
            stageName: "rerank",
            degraded: degraded);

        result.Should().BeNull();
        degraded.Should().ContainSingle().Which.Should().Be("rerank");

        var outcomes = listener.Snapshot()
            .Where(m => m.InstrumentName == "rag.stage.outcome")
            .ToList();
        outcomes.Should().ContainSingle(m => (string?)m.Tags["rag.outcome"] == "timeout");
    }

    [Fact]
    public async Task WithTimeoutAsync_records_error_outcome_on_exception()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var degraded = new List<string>();
        var result = await RagRetrievalServiceTestHarness.RunWithTimeoutAsync<string>(
            op: _ => throw new InvalidOperationException("boom"),
            timeoutMs: 500,
            stageName: "rewrite",
            degraded: degraded);

        result.Should().BeNull();
        degraded.Should().ContainSingle().Which.Should().Be("rewrite");

        var outcomes = listener.Snapshot()
            .Where(m => m.InstrumentName == "rag.stage.outcome")
            .ToList();
        outcomes.Should().ContainSingle(m => (string?)m.Tags["rag.outcome"] == "error");
    }

    [Fact]
    public async Task Classify_stage_records_duration_and_outcome_via_direct_path()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-classify-metrics-{Guid.NewGuid():N}").Options;
        await using var db = new AiDbContext(options, currentUserService: null);

        var settings = new AiRagSettings();
        var svc = new RagRetrievalService(
            db,
            new FakeVectorStore(),
            new FakeKeywordSearchService(),
            new Fakes.FakeEmbeddingService(),
            new NoOpQueryRewriter(),
            new NoOpQuestionClassifier(),
            new NoOpReranker(),
            new RerankStrategySelector(settings),
            new NoOpNeighborExpander(),
            new TokenCounter(),
            Options.Create(settings),
            NullLogger<RagRetrievalService>.Instance);

        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "A", null, "p");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        _ = await svc.RetrieveForTurnAsync(assistant, "hello world", CancellationToken.None);

        var outcomes = listener.Snapshot()
            .Where(m => m.InstrumentName == "rag.stage.outcome"
                        && (string?)m.Tags["rag.stage"] == RagStages.Classify)
            .ToList();
        outcomes.Should().ContainSingle(m => (string?)m.Tags["rag.outcome"] == "success");

        var durations = listener.Snapshot()
            .Where(m => m.InstrumentName == "rag.stage.duration"
                        && (string?)m.Tags["rag.stage"] == RagStages.Classify)
            .ToList();
        durations.Should().HaveCount(1);
    }
}
