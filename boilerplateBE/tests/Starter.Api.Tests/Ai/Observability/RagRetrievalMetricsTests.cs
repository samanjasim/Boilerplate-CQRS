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
            new NoOpContextualQueryResolver(),
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

        _ = await svc.RetrieveForTurnAsync(assistant, "hello world", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

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

    [Fact]
    public async Task Retrieval_emits_requests_counter_tagged_by_scope()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-requests-scope-{Guid.NewGuid():N}").Options;
        await using var db = new AiDbContext(options, currentUserService: null);

        var settings = new AiRagSettings();
        var svc = new RagRetrievalService(
            db,
            new FakeVectorStore(),
            new FakeKeywordSearchService(),
            new Fakes.FakeEmbeddingService(),
            new NoOpQueryRewriter(),
            new NoOpContextualQueryResolver(),
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

        _ = await svc.RetrieveForTurnAsync(assistant, "centrifugal pumps move fluid through impeller rotation", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        listener.Snapshot()
            .Should().Contain(m =>
                m.InstrumentName == "rag.retrieval.requests"
                && (string?)m.Tags["rag.scope"] == "AllTenantDocuments"
                && m.Value == 1);
    }

    [Fact]
    public async Task Retrieval_records_fusion_candidates_histogram_even_when_zero()
    {
        // With all no-op fakes, vector + keyword return empty, so fusion count is 0.
        // The histogram MUST still be recorded — assertion is that the instrument fires.
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-fusion-{Guid.NewGuid():N}").Options;
        await using var db = new AiDbContext(options, currentUserService: null);

        var settings = new AiRagSettings();
        var svc = new RagRetrievalService(
            db,
            new FakeVectorStore(),
            new FakeKeywordSearchService(),
            new Fakes.FakeEmbeddingService(),
            new NoOpQueryRewriter(),
            new NoOpContextualQueryResolver(),
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

        _ = await svc.RetrieveForTurnAsync(assistant, "centrifugal pumps move fluid through impeller rotation", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        listener.Snapshot()
            .Should().Contain(m => m.InstrumentName == "rag.fusion.candidates");
    }

    [Fact]
    public async Task Retrieval_records_keyword_hits_tagged_by_detected_language()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-keyword-lang-{Guid.NewGuid():N}").Options;
        await using var db = new AiDbContext(options, currentUserService: null);

        var settings = new AiRagSettings();
        var svc = new RagRetrievalService(
            db,
            new FakeVectorStore(),
            new FakeKeywordSearchService(),
            new Fakes.FakeEmbeddingService(),
            new NoOpQueryRewriter(),
            new NoOpContextualQueryResolver(),
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

        _ = await svc.RetrieveForTurnAsync(assistant, "ما هي المضخة الطاردة المركزية وكيف تعمل", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        listener.Snapshot()
            .Should().Contain(m =>
                m.InstrumentName == "rag.keyword.hits"
                && (string?)m.Tags["rag.lang"] == "ar");
    }

    [Fact]
    public async Task Chat_turn_records_context_tokens_histogram()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var fx = new Starter.Api.Tests.Ai.Retrieval.ChatExecutionTestFixture();
        var docId = fx.SeedTwoRetrievedChunks(); // populates fake with 2 chunks, TotalTokens=20
        var assistant = fx.SeedAssistantWithRagScope(
            Starter.Module.AI.Domain.Enums.AiRagScope.SelectedDocuments, docIds: new[] { docId });

        fx.FakeProvider.ScriptedResponse = "reply";

        var reply = await fx.RunOneTurnAsync(assistant, userMessage: "q");
        reply.IsSuccess.Should().BeTrue();

        listener.Snapshot()
            .Should().Contain(m =>
                m.InstrumentName == "rag.context.tokens" && m.Value > 0);
    }

    [Fact]
    public async Task Chat_turn_records_context_truncated_counter_with_budget_reason()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var fx = new Starter.Api.Tests.Ai.Retrieval.ChatExecutionTestFixture();
        var docId = fx.SeedTwoRetrievedChunks();
        // Overwrite the fixture's context with a truncated version.
        fx.OverrideRetrievalContext(
            children: fx.CurrentRetrievalContext.Children,
            truncated: true,
            degradedStages: Array.Empty<string>());

        var assistant = fx.SeedAssistantWithRagScope(
            Starter.Module.AI.Domain.Enums.AiRagScope.SelectedDocuments, docIds: new[] { docId });

        fx.FakeProvider.ScriptedResponse = "reply";

        await fx.RunOneTurnAsync(assistant, userMessage: "q");

        listener.Snapshot()
            .Should().Contain(m =>
                m.InstrumentName == "rag.context.truncated"
                && (string?)m.Tags["rag.reason"] == "budget"
                && m.Value == 1);
    }

    [Fact]
    public async Task Chat_turn_records_degraded_stages_counter_per_stage()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var fx = new Starter.Api.Tests.Ai.Retrieval.ChatExecutionTestFixture();
        var docId = fx.SeedTwoRetrievedChunks();
        fx.OverrideRetrievalContext(
            children: fx.CurrentRetrievalContext.Children,
            truncated: false,
            degradedStages: new[] { "rerank", "embed" });

        var assistant = fx.SeedAssistantWithRagScope(
            Starter.Module.AI.Domain.Enums.AiRagScope.SelectedDocuments, docIds: new[] { docId });

        fx.FakeProvider.ScriptedResponse = "reply";

        await fx.RunOneTurnAsync(assistant, userMessage: "q");

        var rows = listener.Snapshot()
            .Where(m => m.InstrumentName == "rag.degraded.stages")
            .ToList();
        rows.Should().ContainSingle(m => (string?)m.Tags["rag.stage"] == "rerank");
        rows.Should().ContainSingle(m => (string?)m.Tags["rag.stage"] == "embed");
    }

    [Fact]
    public async Task End_to_end_pipeline_emits_core_observability_instruments()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        // Part 1 — direct RagRetrievalService path
        // Exercises: rag.retrieval.requests, rag.stage.duration (classify), rag.stage.outcome (classify),
        //            rag.fusion.candidates, rag.keyword.hits
        {
            var options = new DbContextOptionsBuilder<AiDbContext>()
                .UseInMemoryDatabase($"rag-e2e-{Guid.NewGuid():N}").Options;
            await using var db = new AiDbContext(options, currentUserService: null);

            var settings = new AiRagSettings();
            var svc = new RagRetrievalService(
                db,
                new FakeVectorStore(),
                new FakeKeywordSearchService(),
                new Fakes.FakeEmbeddingService(),
                new NoOpQueryRewriter(),
                new NoOpContextualQueryResolver(),
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

            // Mixed Arabic+English query ensures language detector fires and tags keyword hits
            _ = await svc.RetrieveForTurnAsync(
                assistant,
                "What is المضخة and how does cavitation affect it?",
                Array.Empty<RagHistoryMessage>(),
                CancellationToken.None);

            // Second turn with history exercises the contextualize stage instruments
            _ = await svc.RetrieveForTurnAsync(
                assistant,
                "and how do we configure it?",
                new[]
                {
                    new RagHistoryMessage("user", "what is المضخة?"),
                    new RagHistoryMessage("assistant", "المضخة is a pump.")
                },
                CancellationToken.None);
        }

        // Part 2 — ChatExecutionService path
        // Exercises: rag.context.tokens, rag.context.truncated, rag.degraded.stages
        {
            var fx = new Starter.Api.Tests.Ai.Retrieval.ChatExecutionTestFixture();
            var docId = fx.SeedTwoRetrievedChunks();
            fx.OverrideRetrievalContext(
                children: fx.CurrentRetrievalContext.Children,
                truncated: true,
                degradedStages: new[] { "rerank" });

            var assistant = fx.SeedAssistantWithRagScope(
                Starter.Module.AI.Domain.Enums.AiRagScope.SelectedDocuments, docIds: new[] { docId });
            fx.FakeProvider.ScriptedResponse = "reply";
            await fx.RunOneTurnAsync(assistant, userMessage: "query");
        }

        var names = listener.Snapshot()
            .Select(m => m.InstrumentName)
            .Distinct()
            .ToHashSet();

        // Instruments proven alive by this regression guard:
        names.Should().Contain("rag.retrieval.requests");
        names.Should().Contain("rag.stage.duration");
        names.Should().Contain("rag.stage.outcome");
        names.Should().Contain("rag.fusion.candidates");
        names.Should().Contain("rag.keyword.hits");
        names.Should().Contain("rag.context.tokens");
        names.Should().Contain("rag.context.truncated");
        names.Should().Contain("rag.degraded.stages");

        // New in 4b-5: contextualize stage tag visible on the stage instruments when history is present.
        var snapshot = listener.Snapshot();
        snapshot.Should().Contain(m => m.InstrumentName == "rag.stage.duration"
                                       && (string?)m.Tags["rag.stage"] == RagStages.Contextualize);
    }

    [Fact]
    public async Task Contextualize_stage_emits_duration_and_outcome_when_history_present()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-contextualize-{Guid.NewGuid():N}").Options;
        await using var db = new AiDbContext(options, currentUserService: null);

        var settings = new AiRagSettings();
        var svc = new RagRetrievalService(
            db,
            new FakeVectorStore(),
            new FakeKeywordSearchService(),
            new Fakes.FakeEmbeddingService(),
            new NoOpQueryRewriter(),
            new NoOpContextualQueryResolver(),
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

        var history = new[]
        {
            new RagHistoryMessage("user", "what is the pump model?"),
            new RagHistoryMessage("assistant", "It is an XR-200 centrifugal pump."),
        };

        _ = await svc.RetrieveForTurnAsync(assistant, "how do we configure it?", history, CancellationToken.None);

        var snapshot = listener.Snapshot();

        snapshot.Should().Contain(m =>
            m.InstrumentName == "rag.stage.duration"
            && (string?)m.Tags["rag.stage"] == RagStages.Contextualize);

        snapshot.Should().Contain(m =>
            m.InstrumentName == "rag.stage.outcome"
            && (string?)m.Tags["rag.stage"] == RagStages.Contextualize
            && (string?)m.Tags["rag.outcome"] == "success");
    }

    [Fact]
    public async Task Contextualize_stage_absent_when_history_empty()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-contextualize-nohist-{Guid.NewGuid():N}").Options;
        await using var db = new AiDbContext(options, currentUserService: null);

        var settings = new AiRagSettings();
        var svc = new RagRetrievalService(
            db,
            new FakeVectorStore(),
            new FakeKeywordSearchService(),
            new Fakes.FakeEmbeddingService(),
            new NoOpQueryRewriter(),
            new NoOpContextualQueryResolver(),
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

        _ = await svc.RetrieveForTurnAsync(assistant, "what is qdrant?", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        var snapshot = listener.Snapshot();

        snapshot.Should().NotContain(m =>
            m.InstrumentName == "rag.stage.outcome"
            && (string?)m.Tags["rag.stage"] == RagStages.Contextualize);
    }
}
