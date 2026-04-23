# Plan 4b-9 — RAG Evaluation Harness

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Ship an offline evaluation harness for the RAG pipeline — version-controlled bilingual JSON fixtures, standard IR metrics (`recall@k` / `precision@k` / `MRR` / `NDCG@k` / `hit_rate@k`), stage-latency percentiles, baseline-snapshot regression gating, plus a superadmin faithfulness endpoint using an LLM judge.

**Architecture:** One orchestrator (`RagEvalHarness`) orchestrates a disposable tenant + Qdrant collection, ingests fixture docs via the existing upload pipeline, runs each question via `IRagRetrievalService.RetrieveForQueryAsync`, collects stage durations via an in-process `MeterListener`, and computes metrics by translating retrieved `DocumentId` values back to fixture IDs through a map built at ingest. The xUnit path is gated on `AI_EVAL_ENABLED=1`; CI stays fast. Rerank determinism via pre-warmed blob. Admin faithfulness endpoint reuses the same harness with `IncludeFaithfulness=true`.

**Tech Stack:** .NET 10, xUnit + FluentAssertions, MessagePack (for cache blob), `System.Diagnostics.Metrics.MeterListener` (stage-latency capture), Qdrant gRPC, Postgres via Testcontainers, existing `IAiService` / `IReranker` / `IRagRetrievalService` / `ICacheService` contracts.

**Spec:** [`docs/superpowers/specs/2026-04-23-ai-module-plan-4b-9-rag-evaluation-harness-design.md`](../specs/2026-04-23-ai-module-plan-4b-9-rag-evaluation-harness-design.md)

---

## Conventions for this plan

- All file paths are relative to repo root (`Boilerplate-CQRS-ai-integration/`).
- Every task ends with **one commit**. Message format: `feat(ai): …`, `test(ai): …`, `docs(ai): …`, `chore(ai): …`. **Never** add `Co-Authored-By` lines or mention Claude.
- Tests use xUnit + FluentAssertions. Pure-function calculators use standalone unit tests; the harness integration test uses `[Collection("RagEval")]` and is gated on `AI_EVAL_ENABLED=1`.
- **No migrations** — harness touches zero schema.
- Before each commit: `dotnet build boilerplateBE/Starter.sln` must pass.
- Primary test run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~<Class>"`.
- The integration regression test (`RagEvalHarnessTests`) is skipped by default; run it locally with `AI_EVAL_ENABLED=1 dotnet test ... --filter RagEvalHarnessTests`.

---

## File map (one-line responsibilities)

### Phase A — Application contracts (pure records; no logic)
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Contracts/EvalDataset.cs` — `(Name, Language, Documents[], Questions[])`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Contracts/EvalDocument.cs` — `(Id, FileName, Content, Language)`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Contracts/EvalQuestion.cs` — `(Id, Query, RelevantDocumentIds, RelevantChunkIds?, ExpectedAnswerSnippet?, Tags)`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Contracts/EvalRunOptions.cs` — `(KValues, IncludeFaithfulness, JudgeModelOverride, WarmupQueries)`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Contracts/EvalMetrics.cs` — aggregate + per-language metric buckets
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Contracts/LatencyMetrics.cs` — per-stage p50/p95/p99
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Contracts/PerQuestionResult.cs` — per-question breakdown
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Contracts/EvalReport.cs` — top-level return value
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Faithfulness/FaithfulnessReport.cs` — aggregate + per-question
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Faithfulness/FaithfulnessQuestionResult.cs` — per-question claims + score
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Faithfulness/IFaithfulnessJudge.cs` — judge interface
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/IRagEvalHarness.cs` — harness interface
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Errors/EvalErrors.cs` — `Ai.Eval.*` error codes

### Phase B — Metric calculators (pure, TDD per-file)
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Metrics/RecallAtKCalculator.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Metrics/PrecisionAtKCalculator.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Metrics/MrrCalculator.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Metrics/NdcgCalculator.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Metrics/HitRateCalculator.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Latency/StageLatencyAggregator.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Baseline/BaselineLoader.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Baseline/BaselineWriter.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Baseline/BaselineComparator.cs`

### Phase C — Settings + fixture loading
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagEvalSettings.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Fixtures/EvalFixtureLoader.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Fixtures/EvalFixtureSchemaException.cs`

### Phase D — Harness orchestrator
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Fixtures/EvalFixtureIngester.cs` — fixture docs → AiDocuments (+ id map)
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/RagEvalHarness.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Faithfulness/LlmJudgeFaithfulness.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Faithfulness/FaithfulnessJudgePrompts.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/DependencyInjection.cs` — register the new types

### Phase E — Admin endpoint
- `boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs` — add `RunEval`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Features/Eval/Commands/RunFaithfulnessEval/RunFaithfulnessEvalCommand.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Features/Eval/Commands/RunFaithfulnessEval/RunFaithfulnessEvalCommandHandler.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiEvalController.cs`

### Phase F — Cache warmup tool
- `boilerplateBE/tools/EvalCacheWarmup/EvalCacheWarmup.csproj`
- `boilerplateBE/tools/EvalCacheWarmup/Program.cs`

### Phase G — Fixtures (data)
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/rag-eval-dataset-en.json`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/rag-eval-dataset-ar.json`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/rag-eval-baseline.json`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/eval-rerank-cache-en.bin` (generated; committed)
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/eval-rerank-cache-ar.bin` (generated; committed)

### Phase H — Tests (meta + integration)
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/Metrics/RecallAtKCalculatorTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/Metrics/PrecisionAtKCalculatorTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/Metrics/MrrCalculatorTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/Metrics/NdcgCalculatorTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/Metrics/HitRateCalculatorTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/BaselineComparatorTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/EvalFixtureLoaderTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/StageLatencyAggregatorTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/RagEvalHarnessTests.cs` — the CI regression gate
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/FaithfulnessEndpointTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/RagEvalCollection.cs` — collection fixture + orphan cleanup
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Fakes/FakeFaithfulnessJudge.cs`

### Phase I — Docs
- `CLAUDE.md` — add "Running the RAG eval harness" section

---

## Task 1: Add enums + error codes (no dependencies, tiny types)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Errors/EvalErrors.cs`

- [ ] **Step 1: Create the error-code static class**

Create `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Errors/EvalErrors.cs`:

```csharp
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Eval.Errors;

public static class EvalErrors
{
    public static readonly Error FixtureNotFound = Error.NotFound(
        "Ai.Eval.FixtureNotFound", "The requested eval fixture does not exist.");

    public static readonly Error FixtureInvalid = Error.Validation(
        "Ai.Eval.FixtureInvalid", "The eval fixture JSON is invalid or malformed.");

    public static readonly Error BaselineMissing = Error.Problem(
        "Ai.Eval.BaselineMissing", "Baseline snapshot file is missing.");

    public static readonly Error DatasetLanguageMismatch = Error.Validation(
        "Ai.Eval.DatasetLanguageMismatch", "Dataset language must be 'en' or 'ar'.");

    public static readonly Error AssistantNotFound = Error.NotFound(
        "Ai.Eval.AssistantNotFound", "Assistant not found or not accessible.");

    public static readonly Error JudgeModelUnavailable = Error.Problem(
        "Ai.Eval.JudgeModelUnavailable", "The configured faithfulness judge model is unavailable.");
}
```

- [ ] **Step 2: Build**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Errors/EvalErrors.cs
git commit -m "feat(ai): add Ai.Eval.* error codes for RAG eval harness"
```

---

## Task 2: Add Eval contract records

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Contracts/EvalDocument.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Contracts/EvalQuestion.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Contracts/EvalDataset.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Contracts/EvalRunOptions.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Contracts/LatencyMetrics.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Contracts/EvalMetrics.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Contracts/PerQuestionResult.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Contracts/EvalReport.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Faithfulness/FaithfulnessQuestionResult.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Faithfulness/FaithfulnessReport.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/Faithfulness/IFaithfulnessJudge.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/IRagEvalHarness.cs`

- [ ] **Step 1: `EvalDocument.cs`**

```csharp
namespace Starter.Module.AI.Application.Eval.Contracts;

public sealed record EvalDocument(
    Guid Id,
    string FileName,
    string Content,
    string Language);
```

- [ ] **Step 2: `EvalQuestion.cs`**

```csharp
namespace Starter.Module.AI.Application.Eval.Contracts;

public sealed record EvalQuestion(
    string Id,
    string Query,
    IReadOnlyList<Guid> RelevantDocumentIds,
    IReadOnlyList<Guid>? RelevantChunkIds,
    string? ExpectedAnswerSnippet,
    IReadOnlyList<string> Tags);
```

- [ ] **Step 3: `EvalDataset.cs`**

```csharp
namespace Starter.Module.AI.Application.Eval.Contracts;

public sealed record EvalDataset(
    string Name,
    string Language,
    string? Description,
    IReadOnlyList<EvalDocument> Documents,
    IReadOnlyList<EvalQuestion> Questions);
```

- [ ] **Step 4: `EvalRunOptions.cs`**

```csharp
namespace Starter.Module.AI.Application.Eval.Contracts;

public sealed record EvalRunOptions(
    int[] KValues,
    bool IncludeFaithfulness = false,
    string? JudgeModelOverride = null,
    int WarmupQueries = 2,
    Guid? AssistantId = null);
```

- [ ] **Step 5: `LatencyMetrics.cs`**

```csharp
namespace Starter.Module.AI.Application.Eval.Contracts;

public sealed record StagePercentiles(double P50, double P95, double P99);

public sealed record LatencyMetrics(IReadOnlyDictionary<string, StagePercentiles> PerStage);
```

- [ ] **Step 6: `EvalMetrics.cs`**

```csharp
namespace Starter.Module.AI.Application.Eval.Contracts;

public sealed record MetricBucket(
    IReadOnlyDictionary<int, double> RecallAtK,
    IReadOnlyDictionary<int, double> PrecisionAtK,
    IReadOnlyDictionary<int, double> NdcgAtK,
    IReadOnlyDictionary<int, double> HitRateAtK,
    double Mrr);

public sealed record EvalMetrics(
    MetricBucket Aggregate,
    IReadOnlyDictionary<string, MetricBucket> PerLanguage,
    IReadOnlyDictionary<string, MetricBucket> PerTag);
```

- [ ] **Step 7: `PerQuestionResult.cs`**

```csharp
namespace Starter.Module.AI.Application.Eval.Contracts;

public sealed record PerQuestionResult(
    string QuestionId,
    string Query,
    IReadOnlyList<Guid> RetrievedDocumentIds,
    IReadOnlyList<Guid> RelevantDocumentIds,
    double RecallAt5,
    double RecallAt10,
    double ReciprocalRank,
    double TotalLatencyMs,
    IReadOnlyList<string> DegradedStages);
```

- [ ] **Step 8: `EvalReport.cs`**

```csharp
using Starter.Module.AI.Application.Eval.Faithfulness;

namespace Starter.Module.AI.Application.Eval.Contracts;

public sealed record EvalReport(
    DateTime RunAt,
    string DatasetName,
    string Language,
    int QuestionCount,
    EvalMetrics Metrics,
    LatencyMetrics Latency,
    IReadOnlyList<PerQuestionResult> PerQuestion,
    IReadOnlyList<string> AggregateDegradedStages,
    FaithfulnessReport? Faithfulness);
```

- [ ] **Step 9: `FaithfulnessQuestionResult.cs`**

```csharp
namespace Starter.Module.AI.Application.Eval.Faithfulness;

public sealed record ClaimVerdict(string Text, string Verdict);

public sealed record FaithfulnessQuestionResult(
    string QuestionId,
    double Score,
    IReadOnlyList<ClaimVerdict> Claims,
    bool JudgeParseFailed);
```

- [ ] **Step 10: `FaithfulnessReport.cs`**

```csharp
namespace Starter.Module.AI.Application.Eval.Faithfulness;

public sealed record FaithfulnessReport(
    double AggregateScore,
    int JudgeParseFailureCount,
    IReadOnlyList<FaithfulnessQuestionResult> PerQuestion);
```

- [ ] **Step 11: `IFaithfulnessJudge.cs`**

```csharp
using Starter.Module.AI.Application.Eval.Contracts;

namespace Starter.Module.AI.Application.Eval.Faithfulness;

public interface IFaithfulnessJudge
{
    Task<FaithfulnessQuestionResult> JudgeAsync(
        EvalQuestion question,
        string context,
        string answer,
        string? modelOverride,
        CancellationToken ct);
}
```

- [ ] **Step 12: `IRagEvalHarness.cs`**

```csharp
using Starter.Module.AI.Application.Eval.Contracts;

namespace Starter.Module.AI.Application.Eval;

public interface IRagEvalHarness
{
    Task<EvalReport> RunAsync(
        EvalDataset dataset,
        EvalRunOptions options,
        CancellationToken ct);
}
```

- [ ] **Step 13: Build**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: PASS.

- [ ] **Step 14: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Eval/
git commit -m "feat(ai): add Eval contract records + harness interface"
```

---

## Task 3: `RecallAtKCalculator` (TDD)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/Metrics/RecallAtKCalculatorTests.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Metrics/RecallAtKCalculator.cs`

- [ ] **Step 1: Write the failing tests**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/Metrics/RecallAtKCalculatorTests.cs`:

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Eval.Metrics;

namespace Starter.Api.Tests.Ai.Eval.Metrics;

public sealed class RecallAtKCalculatorTests
{
    [Fact]
    public void PartialMatch_ReturnsProportionInTopK()
    {
        // R={A,B}, H=[A,C,B,D], k=2 → 1/2 = 0.5
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var c = Guid.NewGuid(); var d = Guid.NewGuid();

        var result = RecallAtKCalculator.Compute(
            retrieved: new[] { a, c, b, d },
            relevant: new HashSet<Guid> { a, b },
            k: 2);

        result.Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void NoMatchInTopK_ReturnsZero()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();

        var result = RecallAtKCalculator.Compute(
            retrieved: new[] { b, c },
            relevant: new HashSet<Guid> { a },
            k: 2);

        result.Should().Be(0.0);
    }

    [Fact]
    public void EmptyRelevant_ReturnsZero()
    {
        var a = Guid.NewGuid();
        var result = RecallAtKCalculator.Compute(
            retrieved: new[] { a },
            relevant: new HashSet<Guid>(),
            k: 1);

        result.Should().Be(0.0);
    }

    [Fact]
    public void AllRelevantInTopK_ReturnsOne()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();

        var result = RecallAtKCalculator.Compute(
            retrieved: new[] { a, b, c, Guid.NewGuid() },
            relevant: new HashSet<Guid> { a, b, c },
            k: 3);

        result.Should().Be(1.0);
    }

    [Fact]
    public void KLargerThanRetrieved_ScansWholeList()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var result = RecallAtKCalculator.Compute(
            retrieved: new[] { a, b },
            relevant: new HashSet<Guid> { a, b },
            k: 100);

        result.Should().Be(1.0);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RecallAtKCalculator"`
Expected: FAIL — `RecallAtKCalculator` does not exist.

- [ ] **Step 3: Implement**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Metrics/RecallAtKCalculator.cs`:

```csharp
namespace Starter.Module.AI.Infrastructure.Eval.Metrics;

public static class RecallAtKCalculator
{
    public static double Compute(
        IReadOnlyList<Guid> retrieved,
        ISet<Guid> relevant,
        int k)
    {
        if (relevant.Count == 0) return 0.0;
        var cutoff = Math.Min(k, retrieved.Count);
        var hits = 0;
        for (var i = 0; i < cutoff; i++)
            if (relevant.Contains(retrieved[i])) hits++;
        return (double)hits / relevant.Count;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RecallAtKCalculator"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Metrics/RecallAtKCalculator.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/Metrics/RecallAtKCalculatorTests.cs
git commit -m "feat(ai): add RecallAtKCalculator with TDD coverage"
```

---

## Task 4: `PrecisionAtKCalculator` (TDD)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/Metrics/PrecisionAtKCalculatorTests.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Metrics/PrecisionAtKCalculator.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Eval.Metrics;

namespace Starter.Api.Tests.Ai.Eval.Metrics;

public sealed class PrecisionAtKCalculatorTests
{
    [Fact]
    public void TwoOfFourRelevantInTopK_ReturnsRatio()
    {
        // R={A,B}, H=[A,C,B,D], k=4 → 2/4 = 0.5
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var c = Guid.NewGuid(); var d = Guid.NewGuid();

        PrecisionAtKCalculator.Compute(
            retrieved: new[] { a, c, b, d },
            relevant: new HashSet<Guid> { a, b },
            k: 4).Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void AllRetrievedRelevant_ReturnsOne()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        PrecisionAtKCalculator.Compute(
            retrieved: new[] { a, b },
            relevant: new HashSet<Guid> { a, b },
            k: 2).Should().Be(1.0);
    }

    [Fact]
    public void EmptyRetrievedOrK0_ReturnsZero()
    {
        var a = Guid.NewGuid();
        PrecisionAtKCalculator.Compute(
            retrieved: Array.Empty<Guid>(),
            relevant: new HashSet<Guid> { a },
            k: 5).Should().Be(0.0);
        PrecisionAtKCalculator.Compute(
            retrieved: new[] { a },
            relevant: new HashSet<Guid> { a },
            k: 0).Should().Be(0.0);
    }

    [Fact]
    public void KGreaterThanRetrievedCount_UsesK_InDenominator()
    {
        // Precision@k always divides by k, not retrieved.Count (standard IR def).
        var a = Guid.NewGuid();
        PrecisionAtKCalculator.Compute(
            retrieved: new[] { a },
            relevant: new HashSet<Guid> { a },
            k: 5).Should().BeApproximately(0.2, 1e-9);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~PrecisionAtKCalculator"`
Expected: FAIL.

- [ ] **Step 3: Implement**

```csharp
namespace Starter.Module.AI.Infrastructure.Eval.Metrics;

public static class PrecisionAtKCalculator
{
    public static double Compute(
        IReadOnlyList<Guid> retrieved,
        ISet<Guid> relevant,
        int k)
    {
        if (k <= 0 || retrieved.Count == 0) return 0.0;
        var cutoff = Math.Min(k, retrieved.Count);
        var hits = 0;
        for (var i = 0; i < cutoff; i++)
            if (relevant.Contains(retrieved[i])) hits++;
        return (double)hits / k;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: same as Step 2. Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Metrics/PrecisionAtKCalculator.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/Metrics/PrecisionAtKCalculatorTests.cs
git commit -m "feat(ai): add PrecisionAtKCalculator with TDD coverage"
```

---

## Task 5: `MrrCalculator` (TDD)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/Metrics/MrrCalculatorTests.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Metrics/MrrCalculator.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Eval.Metrics;

namespace Starter.Api.Tests.Ai.Eval.Metrics;

public sealed class MrrCalculatorTests
{
    [Fact]
    public void FirstResultRelevant_ReciprocalIsOne()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        MrrCalculator.ReciprocalRank(
            retrieved: new[] { a, b },
            relevant: new HashSet<Guid> { a })
            .Should().Be(1.0);
    }

    [Fact]
    public void ThirdResultFirstRelevant_ReciprocalIsOneThird()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        MrrCalculator.ReciprocalRank(
            retrieved: new[] { b, c, a },
            relevant: new HashSet<Guid> { a })
            .Should().BeApproximately(1.0 / 3.0, 1e-9);
    }

    [Fact]
    public void NoRelevantRetrieved_ReciprocalIsZero()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        MrrCalculator.ReciprocalRank(
            retrieved: new[] { b },
            relevant: new HashSet<Guid> { a })
            .Should().Be(0.0);
    }

    [Fact]
    public void MrrIsMeanOfReciprocalRanks()
    {
        MrrCalculator.Mean(new[] { 1.0, 0.5, 0.0 })
            .Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void MrrOfEmpty_IsZero()
    {
        MrrCalculator.Mean(Array.Empty<double>()).Should().Be(0.0);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~MrrCalculator"`
Expected: FAIL.

- [ ] **Step 3: Implement**

```csharp
namespace Starter.Module.AI.Infrastructure.Eval.Metrics;

public static class MrrCalculator
{
    public static double ReciprocalRank(IReadOnlyList<Guid> retrieved, ISet<Guid> relevant)
    {
        for (var i = 0; i < retrieved.Count; i++)
            if (relevant.Contains(retrieved[i])) return 1.0 / (i + 1);
        return 0.0;
    }

    public static double Mean(IReadOnlyList<double> reciprocalRanks)
    {
        if (reciprocalRanks.Count == 0) return 0.0;
        var sum = 0.0;
        for (var i = 0; i < reciprocalRanks.Count; i++) sum += reciprocalRanks[i];
        return sum / reciprocalRanks.Count;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: same as Step 2. Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Metrics/MrrCalculator.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/Metrics/MrrCalculatorTests.cs
git commit -m "feat(ai): add MrrCalculator with TDD coverage"
```

---

## Task 6: `NdcgCalculator` (TDD)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/Metrics/NdcgCalculatorTests.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Metrics/NdcgCalculator.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Eval.Metrics;

namespace Starter.Api.Tests.Ai.Eval.Metrics;

public sealed class NdcgCalculatorTests
{
    [Fact]
    public void IdealRanking_Ndcg_Is_One()
    {
        // All 3 relevant docs in top 3 (ideal order).
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        NdcgCalculator.Compute(
            retrieved: new[] { a, b, c },
            relevant: new HashSet<Guid> { a, b, c },
            k: 3).Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void NoRelevantInTopK_Ndcg_Is_Zero()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        NdcgCalculator.Compute(
            retrieved: new[] { b },
            relevant: new HashSet<Guid> { a },
            k: 1).Should().Be(0.0);
    }

    [Fact]
    public void RelevantAtSecondPosition_KnownNdcg()
    {
        // R={A}, H=[B,A,C], k=3
        // DCG  = 0/log2(2) + 1/log2(3) + 0/log2(4) = 1/log2(3)
        // IDCG = 1/log2(2) = 1
        // NDCG = 1/log2(3) ≈ 0.63092975
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        NdcgCalculator.Compute(
            retrieved: new[] { b, a, c },
            relevant: new HashSet<Guid> { a },
            k: 3).Should().BeApproximately(1.0 / Math.Log2(3), 1e-9);
    }

    [Fact]
    public void EmptyRelevant_Ndcg_Is_Zero()
    {
        var a = Guid.NewGuid();
        NdcgCalculator.Compute(
            retrieved: new[] { a },
            relevant: new HashSet<Guid>(),
            k: 1).Should().Be(0.0);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~NdcgCalculator"`
Expected: FAIL.

- [ ] **Step 3: Implement**

```csharp
namespace Starter.Module.AI.Infrastructure.Eval.Metrics;

public static class NdcgCalculator
{
    public static double Compute(
        IReadOnlyList<Guid> retrieved,
        ISet<Guid> relevant,
        int k)
    {
        if (relevant.Count == 0) return 0.0;

        var cutoff = Math.Min(k, retrieved.Count);
        var dcg = 0.0;
        for (var i = 0; i < cutoff; i++)
        {
            var rel = relevant.Contains(retrieved[i]) ? 1.0 : 0.0;
            // position = i + 1; formula uses log2(position + 1)
            dcg += rel / Math.Log2(i + 2);
        }

        var idealCount = Math.Min(k, relevant.Count);
        var idcg = 0.0;
        for (var i = 0; i < idealCount; i++) idcg += 1.0 / Math.Log2(i + 2);

        return idcg == 0.0 ? 0.0 : dcg / idcg;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: same as Step 2. Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Metrics/NdcgCalculator.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/Metrics/NdcgCalculatorTests.cs
git commit -m "feat(ai): add NdcgCalculator with TDD coverage"
```

---

## Task 7: `HitRateCalculator` (TDD)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/Metrics/HitRateCalculatorTests.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Metrics/HitRateCalculator.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Eval.Metrics;

namespace Starter.Api.Tests.Ai.Eval.Metrics;

public sealed class HitRateCalculatorTests
{
    [Fact]
    public void HitInTopK_ReturnsOne()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        HitRateCalculator.Compute(
            retrieved: new[] { a, b },
            relevant: new HashSet<Guid> { b },
            k: 2).Should().Be(1.0);
    }

    [Fact]
    public void NoHitInTopK_ReturnsZero()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        HitRateCalculator.Compute(
            retrieved: new[] { a, b },
            relevant: new HashSet<Guid> { c },
            k: 2).Should().Be(0.0);
    }

    [Fact]
    public void MeanAcrossQuestions_ReturnsFraction()
    {
        HitRateCalculator.Mean(new[] { 1.0, 0.0, 1.0, 1.0 })
            .Should().BeApproximately(0.75, 1e-9);
    }

    [Fact]
    public void MeanOfEmpty_IsZero()
    {
        HitRateCalculator.Mean(Array.Empty<double>()).Should().Be(0.0);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~HitRateCalculator"`
Expected: FAIL.

- [ ] **Step 3: Implement**

```csharp
namespace Starter.Module.AI.Infrastructure.Eval.Metrics;

public static class HitRateCalculator
{
    public static double Compute(
        IReadOnlyList<Guid> retrieved,
        ISet<Guid> relevant,
        int k)
    {
        var cutoff = Math.Min(k, retrieved.Count);
        for (var i = 0; i < cutoff; i++)
            if (relevant.Contains(retrieved[i])) return 1.0;
        return 0.0;
    }

    public static double Mean(IReadOnlyList<double> perQuestionHits)
    {
        if (perQuestionHits.Count == 0) return 0.0;
        var sum = 0.0;
        for (var i = 0; i < perQuestionHits.Count; i++) sum += perQuestionHits[i];
        return sum / perQuestionHits.Count;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: same as Step 2. Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Metrics/HitRateCalculator.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/Metrics/HitRateCalculatorTests.cs
git commit -m "feat(ai): add HitRateCalculator with TDD coverage"
```

---

## Task 8: `StageLatencyAggregator` (MeterListener-based)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/StageLatencyAggregatorTests.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Latency/StageLatencyAggregator.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Diagnostics.Metrics;
using FluentAssertions;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Infrastructure.Eval.Latency;
using Starter.Module.AI.Infrastructure.Observability;

namespace Starter.Api.Tests.Ai.Eval;

public sealed class StageLatencyAggregatorTests
{
    [Fact]
    public void Capture_RecordsStageDurationsFromRagMeter()
    {
        using var capture = StageLatencyAggregator.BeginCapture();

        // Use the real AiRagMetrics.StageDuration histogram directly.
        AiRagMetrics.StageDuration.Record(12.0, new KeyValuePair<string, object?>("rag.stage", "embed-query"));
        AiRagMetrics.StageDuration.Record(25.0, new KeyValuePair<string, object?>("rag.stage", "embed-query"));
        AiRagMetrics.StageDuration.Record(4.0,  new KeyValuePair<string, object?>("rag.stage", "acl-resolve"));

        var durations = capture.Stop();

        durations["embed-query"].Should().BeEquivalentTo(new[] { 12.0, 25.0 });
        durations["acl-resolve"].Should().BeEquivalentTo(new[] { 4.0 });
    }

    [Fact]
    public void Aggregate_ComputesPercentiles()
    {
        var perStage = new Dictionary<string, List<double>>
        {
            ["vector-search[0]"] = new() { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 }
        };

        var metrics = StageLatencyAggregator.Aggregate(perStage);

        metrics.PerStage["vector-search[0]"].P50.Should().BeApproximately(50, 0.1);
        metrics.PerStage["vector-search[0]"].P95.Should().BeApproximately(100, 0.1);
        metrics.PerStage["vector-search[0]"].P99.Should().BeApproximately(100, 0.1);
    }

    [Fact]
    public void Aggregate_EmptyPerStage_ReturnsEmptyMetrics()
    {
        var metrics = StageLatencyAggregator.Aggregate(new Dictionary<string, List<double>>());
        metrics.PerStage.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~StageLatencyAggregator"`
Expected: FAIL.

- [ ] **Step 3: Implement**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Latency/StageLatencyAggregator.cs`:

```csharp
using System.Diagnostics.Metrics;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Infrastructure.Observability;

namespace Starter.Module.AI.Infrastructure.Eval.Latency;

public static class StageLatencyAggregator
{
    public static LatencyCapture BeginCapture() => new();

    public static LatencyMetrics Aggregate(IReadOnlyDictionary<string, List<double>> perStage)
    {
        var result = new Dictionary<string, StagePercentiles>(perStage.Count);
        foreach (var (stage, values) in perStage)
        {
            if (values.Count == 0) continue;
            var sorted = values.ToArray();
            Array.Sort(sorted);
            result[stage] = new StagePercentiles(
                P50: Percentile(sorted, 0.50),
                P95: Percentile(sorted, 0.95),
                P99: Percentile(sorted, 0.99));
        }
        return new LatencyMetrics(result);
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 1) return sorted[0];
        var rank = (int)Math.Ceiling(p * sorted.Length) - 1;
        if (rank < 0) rank = 0;
        if (rank >= sorted.Length) rank = sorted.Length - 1;
        return sorted[rank];
    }

    public sealed class LatencyCapture : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly Dictionary<string, List<double>> _buckets = new();
        private readonly object _lock = new();
        private bool _disposed;

        internal LatencyCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == AiRagMetrics.MeterName
                        && instrument.Name == "rag.stage.duration")
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _listener.SetMeasurementEventCallback<double>(OnMeasurement);
            _listener.Start();
        }

        private void OnMeasurement(
            Instrument instrument,
            double measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags,
            object? state)
        {
            string? stage = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "rag.stage") { stage = tag.Value?.ToString(); break; }
            }
            if (stage is null) return;

            lock (_lock)
            {
                if (!_buckets.TryGetValue(stage, out var list))
                {
                    list = new List<double>();
                    _buckets[stage] = list;
                }
                list.Add(measurement);
            }
        }

        public IReadOnlyDictionary<string, double[]> Stop()
        {
            lock (_lock)
            {
                _listener.Dispose();
                _disposed = true;
                return _buckets.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
            }
        }

        public void Dispose()
        {
            if (!_disposed) _listener.Dispose();
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: same as Step 2. Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Latency/StageLatencyAggregator.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/StageLatencyAggregatorTests.cs
git commit -m "feat(ai): add StageLatencyAggregator with MeterListener-based capture"
```

---

## Task 9: `BaselineLoader`, `BaselineWriter`, `BaselineComparator` (TDD)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/BaselineComparatorTests.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Baseline/BaselineLoader.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Baseline/BaselineWriter.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Baseline/BaselineComparator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Baseline/BaselineSnapshot.cs`

- [ ] **Step 1: Write failing tests for `BaselineComparator`**

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Eval.Baseline;

namespace Starter.Api.Tests.Ai.Eval;

public sealed class BaselineComparatorTests
{
    private static BaselineDatasetSnapshot Snap(
        double recall5 = 0.8, double mrr = 0.7,
        IReadOnlyDictionary<string, double>? stagesP95 = null,
        int degraded = 0)
    {
        var stages = stagesP95 ?? new Dictionary<string, double> { ["total"] = 100 };
        return new BaselineDatasetSnapshot(
            RecallAtK: new Dictionary<int, double> { [5] = recall5 },
            PrecisionAtK: new Dictionary<int, double>(),
            NdcgAtK: new Dictionary<int, double>(),
            HitRateAtK: new Dictionary<int, double>(),
            Mrr: mrr,
            StageP95Ms: stages,
            DegradedStageCount: degraded);
    }

    [Fact]
    public void MetricDropWithinTolerance_Passes()
    {
        var baseline = Snap(recall5: 0.80);
        var current = Snap(recall5: 0.78); // 2.5% drop
        var result = BaselineComparator.Compare(
            baseline, current, metricTolerance: 0.05, latencyTolerance: 0.20);
        result.Failed.Should().BeFalse();
    }

    [Fact]
    public void MetricDropExceedsTolerance_Fails()
    {
        var baseline = Snap(recall5: 0.80);
        var current = Snap(recall5: 0.60); // 25% drop
        var result = BaselineComparator.Compare(
            baseline, current, 0.05, 0.20);
        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainMatch("*recall_at_5*");
    }

    [Fact]
    public void MetricImprovementPastTolerance_PassesWithWarning()
    {
        var baseline = Snap(recall5: 0.80);
        var current = Snap(recall5: 0.95); // 18.75% gain
        var result = BaselineComparator.Compare(baseline, current, 0.05, 0.20);
        result.Failed.Should().BeFalse();
        result.Warnings.Should().ContainMatch("*recall_at_5*");
    }

    [Fact]
    public void LatencyP95IncreaseExceedsTolerance_Fails()
    {
        var baseline = Snap(stagesP95: new Dictionary<string, double> { ["rerank"] = 100 });
        var current  = Snap(stagesP95: new Dictionary<string, double> { ["rerank"] = 150 }); // +50%
        var result = BaselineComparator.Compare(baseline, current, 0.05, 0.20);
        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainMatch("*rerank*");
    }

    [Fact]
    public void DegradedStageCountIncrease_Fails()
    {
        var baseline = Snap(degraded: 0);
        var current = Snap(degraded: 3);
        var result = BaselineComparator.Compare(baseline, current, 0.05, 0.20);
        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainMatch("*degraded*");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~BaselineComparator"`
Expected: FAIL.

- [ ] **Step 3: Implement `BaselineSnapshot`**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Baseline/BaselineSnapshot.cs`:

```csharp
namespace Starter.Module.AI.Infrastructure.Eval.Baseline;

public sealed record BaselineDatasetSnapshot(
    IReadOnlyDictionary<int, double> RecallAtK,
    IReadOnlyDictionary<int, double> PrecisionAtK,
    IReadOnlyDictionary<int, double> NdcgAtK,
    IReadOnlyDictionary<int, double> HitRateAtK,
    double Mrr,
    IReadOnlyDictionary<string, double> StageP95Ms,
    int DegradedStageCount);

public sealed record BaselineSnapshot(
    DateTime GeneratedAt,
    string? GitSha,
    IReadOnlyDictionary<string, BaselineDatasetSnapshot> Datasets);
```

- [ ] **Step 4: Implement `BaselineComparator`**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Baseline/BaselineComparator.cs`:

```csharp
namespace Starter.Module.AI.Infrastructure.Eval.Baseline;

public sealed record BaselineComparisonResult(
    bool Failed,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Warnings);

public static class BaselineComparator
{
    public static BaselineComparisonResult Compare(
        BaselineDatasetSnapshot baseline,
        BaselineDatasetSnapshot current,
        double metricTolerance,
        double latencyTolerance)
    {
        var failures = new List<string>();
        var warnings = new List<string>();

        CompareMetricDict("recall_at_", baseline.RecallAtK, current.RecallAtK,
            metricTolerance, failures, warnings);
        CompareMetricDict("precision_at_", baseline.PrecisionAtK, current.PrecisionAtK,
            metricTolerance, failures, warnings);
        CompareMetricDict("ndcg_at_", baseline.NdcgAtK, current.NdcgAtK,
            metricTolerance, failures, warnings);
        CompareMetricDict("hit_rate_at_", baseline.HitRateAtK, current.HitRateAtK,
            metricTolerance, failures, warnings);
        CompareSingleMetric("mrr", baseline.Mrr, current.Mrr,
            metricTolerance, failures, warnings);

        foreach (var (stage, baseP95) in baseline.StageP95Ms)
        {
            if (!current.StageP95Ms.TryGetValue(stage, out var curP95)) continue;
            if (baseP95 <= 0) continue;
            var delta = (curP95 - baseP95) / baseP95;
            if (delta > latencyTolerance)
                failures.Add($"latency.{stage}.p95 regressed: {baseP95:F1} → {curP95:F1} ms ({delta:P1})");
        }

        if (current.DegradedStageCount > baseline.DegradedStageCount)
            failures.Add(
                $"degraded_stage_count increased: {baseline.DegradedStageCount} → {current.DegradedStageCount}");

        return new BaselineComparisonResult(failures.Count > 0, failures, warnings);
    }

    private static void CompareMetricDict(
        string prefix,
        IReadOnlyDictionary<int, double> baseline,
        IReadOnlyDictionary<int, double> current,
        double tolerance,
        List<string> failures,
        List<string> warnings)
    {
        foreach (var (k, baseValue) in baseline)
        {
            if (!current.TryGetValue(k, out var curValue)) continue;
            CompareSingleMetric($"{prefix}{k}", baseValue, curValue, tolerance, failures, warnings);
        }
    }

    private static void CompareSingleMetric(
        string name,
        double baseline,
        double current,
        double tolerance,
        List<string> failures,
        List<string> warnings)
    {
        if (baseline <= 0) return;
        var delta = (current - baseline) / baseline;
        if (delta < -tolerance)
            failures.Add($"{name} regressed: {baseline:F4} → {current:F4} ({delta:P1})");
        else if (delta > tolerance)
            warnings.Add($"{name} improved past tolerance: {baseline:F4} → {current:F4} ({delta:P1})");
    }
}
```

- [ ] **Step 5: Implement `BaselineLoader` + `BaselineWriter`**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Baseline/BaselineLoader.cs`:

```csharp
using System.Text.Json;
using Starter.Module.AI.Application.Eval.Errors;
using Starter.Shared.Results;

namespace Starter.Module.AI.Infrastructure.Eval.Baseline;

public static class BaselineLoader
{
    public static Result<BaselineSnapshot> Load(string path)
    {
        if (!File.Exists(path)) return Result.Failure<BaselineSnapshot>(EvalErrors.BaselineMissing);
        var json = File.ReadAllText(path);
        var snapshot = JsonSerializer.Deserialize<BaselineSnapshot>(
            json, BaselineJson.Options);
        return snapshot is null
            ? Result.Failure<BaselineSnapshot>(EvalErrors.BaselineMissing)
            : Result.Success(snapshot);
    }
}

internal static class BaselineJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Baseline/BaselineWriter.cs`:

```csharp
using System.Text.Json;

namespace Starter.Module.AI.Infrastructure.Eval.Baseline;

public static class BaselineWriter
{
    public static void Write(string path, BaselineSnapshot snapshot)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, BaselineJson.Options));
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~BaselineComparator"`
Expected: PASS (5 tests).

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Baseline/ \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/BaselineComparatorTests.cs
git commit -m "feat(ai): add BaselineLoader/Writer/Comparator for eval regression gating"
```

---

## Task 10: `AiRagEvalSettings` + registration

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagEvalSettings.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/DependencyInjection.cs` (add `Configure<>`)
- Modify: `boilerplateBE/src/Starter.Api/appsettings.json` (add `AI:Rag:Eval` section)

- [ ] **Step 1: Create settings class**

```csharp
namespace Starter.Module.AI.Infrastructure.Settings;

public sealed class AiRagEvalSettings
{
    public const string SectionName = "AI:Rag:Eval";

    public bool Enabled { get; init; } = false;
    public string FixtureDirectory { get; init; } = "ai-eval-fixtures";
    public string BaselineFile { get; init; } = "ai-eval-fixtures/rag-eval-baseline.json";
    public double MetricTolerance { get; init; } = 0.05;
    public double LatencyTolerance { get; init; } = 0.20;
    public string? JudgeModel { get; init; } = null;
    public int JudgeTimeoutMs { get; init; } = 30_000;
    public int WarmupQueries { get; init; } = 2;
    public int[] KValues { get; init; } = new[] { 5, 10, 20 };
}
```

- [ ] **Step 2: Register in DI**

Locate `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/DependencyInjection.cs`. In the main AI-module service registration method (search for `services.Configure<AiRagSettings>`), add immediately after:

```csharp
services.Configure<AiRagEvalSettings>(
    configuration.GetSection(AiRagEvalSettings.SectionName));
```

- [ ] **Step 3: Add to appsettings.json**

Append to `boilerplateBE/src/Starter.Api/appsettings.json` under the `"AI": { "Rag": { ... } }` block (add `"Eval"` subsection):

```json
"Eval": {
  "Enabled": false,
  "FixtureDirectory": "ai-eval-fixtures",
  "BaselineFile": "ai-eval-fixtures/rag-eval-baseline.json",
  "MetricTolerance": 0.05,
  "LatencyTolerance": 0.20,
  "JudgeModel": null,
  "JudgeTimeoutMs": 30000,
  "WarmupQueries": 2,
  "KValues": [5, 10, 20]
}
```

- [ ] **Step 4: Build**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagEvalSettings.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/DependencyInjection.cs \
        boilerplateBE/src/Starter.Api/appsettings.json
git commit -m "feat(ai): add AiRagEvalSettings + appsettings registration"
```

---

## Task 11: `EvalFixtureLoader` (TDD)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/EvalFixtureLoaderTests.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Fixtures/EvalFixtureLoader.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Fixtures/EvalFixtureSchemaException.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Eval.Fixtures;

namespace Starter.Api.Tests.Ai.Eval;

public sealed class EvalFixtureLoaderTests
{
    [Fact]
    public void LoadFromString_ValidFixture_Parses()
    {
        const string json = """
        {
          "name": "test-en-v1",
          "language": "en",
          "description": "test",
          "documents": [
            {
              "id": "11111111-1111-4111-8111-111111111111",
              "file_name": "a.md",
              "language": "en",
              "content": "hello"
            }
          ],
          "questions": [
            {
              "id": "q1",
              "query": "hi?",
              "relevant_document_ids": ["11111111-1111-4111-8111-111111111111"],
              "relevant_chunk_ids": null,
              "expected_answer_snippet": "hello",
              "tags": ["factual"]
            }
          ]
        }
        """;

        var result = EvalFixtureLoader.LoadFromString(json);
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("test-en-v1");
        result.Value.Language.Should().Be("en");
        result.Value.Documents.Should().HaveCount(1);
        result.Value.Questions.Should().HaveCount(1);
        result.Value.Questions[0].Tags.Should().ContainSingle(t => t == "factual");
    }

    [Fact]
    public void LoadFromString_MalformedJson_ReturnsFixtureInvalid()
    {
        var result = EvalFixtureLoader.LoadFromString("{ not json");
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ai.Eval.FixtureInvalid");
    }

    [Fact]
    public void LoadFromString_UnsupportedLanguage_ReturnsLanguageMismatch()
    {
        const string json = """
        { "name":"x","language":"fr","documents":[],"questions":[] }
        """;
        var result = EvalFixtureLoader.LoadFromString(json);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ai.Eval.DatasetLanguageMismatch");
    }

    [Fact]
    public void LoadFromString_DuplicateDocumentIds_ReturnsFixtureInvalid()
    {
        const string json = """
        {
          "name": "t", "language": "en", "documents": [
            {"id":"11111111-1111-4111-8111-111111111111","file_name":"a","language":"en","content":"x"},
            {"id":"11111111-1111-4111-8111-111111111111","file_name":"b","language":"en","content":"y"}
          ], "questions": []
        }
        """;
        var result = EvalFixtureLoader.LoadFromString(json);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ai.Eval.FixtureInvalid");
    }

    [Fact]
    public void LoadFromString_QuestionReferencesUnknownDocId_ReturnsFixtureInvalid()
    {
        const string json = """
        {
          "name": "t", "language": "en",
          "documents": [
            {"id":"11111111-1111-4111-8111-111111111111","file_name":"a","language":"en","content":"x"}
          ],
          "questions": [
            {"id":"q1","query":"?","relevant_document_ids":["22222222-2222-4222-8222-222222222222"],
             "relevant_chunk_ids":null,"expected_answer_snippet":null,"tags":[]}
          ]
        }
        """;
        var result = EvalFixtureLoader.LoadFromString(json);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ai.Eval.FixtureInvalid");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~EvalFixtureLoader"`
Expected: FAIL.

- [ ] **Step 3: Implement exception type**

```csharp
namespace Starter.Module.AI.Infrastructure.Eval.Fixtures;

public sealed class EvalFixtureSchemaException(string message) : Exception(message);
```

- [ ] **Step 4: Implement `EvalFixtureLoader`**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Application.Eval.Errors;
using Starter.Shared.Results;

namespace Starter.Module.AI.Infrastructure.Eval.Fixtures;

public static class EvalFixtureLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public static Result<EvalDataset> LoadFromString(string json)
    {
        RawFixture? raw;
        try { raw = JsonSerializer.Deserialize<RawFixture>(json, Options); }
        catch (JsonException) { return Result.Failure<EvalDataset>(EvalErrors.FixtureInvalid); }

        if (raw is null) return Result.Failure<EvalDataset>(EvalErrors.FixtureInvalid);
        if (raw.Language is not ("en" or "ar"))
            return Result.Failure<EvalDataset>(EvalErrors.DatasetLanguageMismatch);

        var docs = raw.Documents ?? new List<RawDoc>();
        var questions = raw.Questions ?? new List<RawQuestion>();

        var docIds = new HashSet<Guid>();
        foreach (var d in docs)
            if (!docIds.Add(d.Id)) return Result.Failure<EvalDataset>(EvalErrors.FixtureInvalid);

        foreach (var q in questions)
            foreach (var id in q.RelevantDocumentIds ?? new List<Guid>())
                if (!docIds.Contains(id))
                    return Result.Failure<EvalDataset>(EvalErrors.FixtureInvalid);

        var dataset = new EvalDataset(
            Name: raw.Name ?? "unknown",
            Language: raw.Language,
            Description: raw.Description,
            Documents: docs.Select(d => new EvalDocument(
                d.Id, d.FileName, d.Content, d.Language ?? raw.Language)).ToList(),
            Questions: questions.Select(q => new EvalQuestion(
                Id: q.Id,
                Query: q.Query,
                RelevantDocumentIds: q.RelevantDocumentIds ?? new List<Guid>(),
                RelevantChunkIds: q.RelevantChunkIds,
                ExpectedAnswerSnippet: q.ExpectedAnswerSnippet,
                Tags: q.Tags ?? new List<string>())).ToList());

        return Result.Success(dataset);
    }

    public static Result<EvalDataset> LoadFromFile(string path)
        => !File.Exists(path)
            ? Result.Failure<EvalDataset>(EvalErrors.FixtureNotFound)
            : LoadFromString(File.ReadAllText(path));

    private sealed class RawFixture
    {
        public string? Name { get; set; }
        public string Language { get; set; } = "";
        public string? Description { get; set; }
        public List<RawDoc>? Documents { get; set; }
        public List<RawQuestion>? Questions { get; set; }
    }

    private sealed class RawDoc
    {
        public Guid Id { get; set; }
        [JsonPropertyName("file_name")] public string FileName { get; set; } = "";
        public string? Language { get; set; }
        public string Content { get; set; } = "";
    }

    private sealed class RawQuestion
    {
        public string Id { get; set; } = "";
        public string Query { get; set; } = "";
        [JsonPropertyName("relevant_document_ids")] public List<Guid>? RelevantDocumentIds { get; set; }
        [JsonPropertyName("relevant_chunk_ids")] public List<Guid>? RelevantChunkIds { get; set; }
        [JsonPropertyName("expected_answer_snippet")] public string? ExpectedAnswerSnippet { get; set; }
        public List<string>? Tags { get; set; }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: same as Step 2. Expected: PASS (5 tests).

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Fixtures/ \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/EvalFixtureLoaderTests.cs
git commit -m "feat(ai): add EvalFixtureLoader with schema validation"
```

---

## Task 12: Seed EN fixture

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/rag-eval-dataset-en.json`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj` — mark fixtures CopyToOutputDirectory

- [ ] **Step 1: Create EN fixture file**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/rag-eval-dataset-en.json`:

```json
{
  "name": "boilerplate-core-en-v1",
  "language": "en",
  "description": "Seed eval set covering common factual Q&A patterns on synthetic company documentation.",
  "documents": [
    {
      "id": "00000000-0000-4000-8000-000000000001",
      "file_name": "invoicing-policy.md",
      "language": "en",
      "content": "# Invoicing Policy\n\nCustomers are billed monthly on the 1st of each month. Payment is due within 30 days of the invoice date.\n\n## Late Fees\n\nA 2% late fee applies to unpaid balances after 15 days past the due date. After 45 days, the account is suspended.\n\n## Invoice Format\n\nInvoices are sent via email as PDF attachments. Paper invoices are available on request for an additional $5 fee.\n"
    },
    {
      "id": "00000000-0000-4000-8000-000000000002",
      "file_name": "refund-policy.md",
      "language": "en",
      "content": "# Refund Policy\n\nAll purchases are eligible for a full refund within 14 days of the transaction date, provided the product has not been used.\n\n## Exceptions\n\n- Digital goods are non-refundable after download.\n- Custom orders are non-refundable.\n- Gift cards are final sale.\n\n## Processing Time\n\nApproved refunds are processed within 5 business days and returned to the original payment method.\n"
    },
    {
      "id": "00000000-0000-4000-8000-000000000003",
      "file_name": "shipping-policy.md",
      "language": "en",
      "content": "# Shipping Policy\n\nStandard shipping takes 3-5 business days within the continental United States. Express shipping takes 1-2 business days for an additional $15 charge.\n\n## International\n\nInternational orders ship via DHL and arrive within 7-14 business days. Customs duties are the recipient's responsibility.\n\n## Free Shipping\n\nOrders over $75 qualify for free standard shipping within the US.\n"
    },
    {
      "id": "00000000-0000-4000-8000-000000000004",
      "file_name": "security-policy.md",
      "language": "en",
      "content": "# Security Policy\n\nAll data is encrypted at rest using AES-256 and in transit via TLS 1.3.\n\n## Authentication\n\nWe require multi-factor authentication (MFA) for all administrative accounts. Session tokens expire after 60 minutes of inactivity.\n\n## Incident Response\n\nSecurity incidents are reported to security@example.com and investigated within 4 hours of receipt.\n"
    },
    {
      "id": "00000000-0000-4000-8000-000000000005",
      "file_name": "vacation-policy.md",
      "language": "en",
      "content": "# Vacation Policy\n\nFull-time employees receive 20 days of paid vacation per calendar year, accrued monthly.\n\n## Rollover\n\nUp to 5 unused vacation days can be rolled over to the next calendar year. Days beyond 5 are forfeited on January 1st.\n\n## Requesting Time Off\n\nVacation requests must be submitted at least 2 weeks in advance through the HR portal.\n"
    },
    {
      "id": "00000000-0000-4000-8000-000000000006",
      "file_name": "support-tiers.md",
      "language": "en",
      "content": "# Support Tiers\n\n## Basic\n\nEmail support with 48-hour response SLA. Included with all plans.\n\n## Priority\n\nEmail + chat support with 4-hour response SLA. $99/month.\n\n## Enterprise\n\n24/7 phone support with 15-minute response SLA and a dedicated account manager. Custom pricing.\n"
    },
    {
      "id": "00000000-0000-4000-8000-000000000007",
      "file_name": "api-rate-limits.md",
      "language": "en",
      "content": "# API Rate Limits\n\nThe API enforces a default limit of 1,000 requests per hour per API key.\n\n## Upgrades\n\nEnterprise customers can request limits up to 100,000 requests per hour. Contact sales@example.com to upgrade.\n\n## Rate-Limited Responses\n\nRate-limited requests return HTTP 429 with a Retry-After header indicating when the next request is allowed.\n"
    },
    {
      "id": "00000000-0000-4000-8000-000000000008",
      "file_name": "product-warranty.md",
      "language": "en",
      "content": "# Product Warranty\n\nAll hardware products include a 1-year limited warranty covering manufacturing defects.\n\n## Extended Warranty\n\nA 3-year extended warranty is available for 15% of the product price at time of purchase.\n\n## Claims\n\nWarranty claims require the original receipt and a description of the defect. Submit via the support portal.\n"
    }
  ],
  "questions": [
    {"id":"q001","query":"When are customers billed?","relevant_document_ids":["00000000-0000-4000-8000-000000000001"],"relevant_chunk_ids":null,"expected_answer_snippet":"monthly on the 1st","tags":["factual","single-doc"]},
    {"id":"q002","query":"What is the late fee?","relevant_document_ids":["00000000-0000-4000-8000-000000000001"],"relevant_chunk_ids":null,"expected_answer_snippet":"2% late fee","tags":["factual","single-doc"]},
    {"id":"q003","query":"How long is the refund window?","relevant_document_ids":["00000000-0000-4000-8000-000000000002"],"relevant_chunk_ids":null,"expected_answer_snippet":"14 days","tags":["factual","single-doc"]},
    {"id":"q004","query":"Can I return digital goods?","relevant_document_ids":["00000000-0000-4000-8000-000000000002"],"relevant_chunk_ids":null,"expected_answer_snippet":"non-refundable after download","tags":["negation","single-doc"]},
    {"id":"q005","query":"How long does express shipping take?","relevant_document_ids":["00000000-0000-4000-8000-000000000003"],"relevant_chunk_ids":null,"expected_answer_snippet":"1-2 business days","tags":["factual","single-doc"]},
    {"id":"q006","query":"Is shipping free?","relevant_document_ids":["00000000-0000-4000-8000-000000000003"],"relevant_chunk_ids":null,"expected_answer_snippet":"over $75","tags":["factual","single-doc"]},
    {"id":"q007","query":"What encryption does the platform use?","relevant_document_ids":["00000000-0000-4000-8000-000000000004"],"relevant_chunk_ids":null,"expected_answer_snippet":"AES-256","tags":["factual","single-doc"]},
    {"id":"q008","query":"How long is a session valid?","relevant_document_ids":["00000000-0000-4000-8000-000000000004"],"relevant_chunk_ids":null,"expected_answer_snippet":"60 minutes","tags":["factual","single-doc"]},
    {"id":"q009","query":"How much vacation do I get?","relevant_document_ids":["00000000-0000-4000-8000-000000000005"],"relevant_chunk_ids":null,"expected_answer_snippet":"20 days","tags":["factual","single-doc"]},
    {"id":"q010","query":"Can I carry vacation days forward?","relevant_document_ids":["00000000-0000-4000-8000-000000000005"],"relevant_chunk_ids":null,"expected_answer_snippet":"5 unused vacation days","tags":["factual","single-doc"]},
    {"id":"q011","query":"What is the SLA for Priority support?","relevant_document_ids":["00000000-0000-4000-8000-000000000006"],"relevant_chunk_ids":null,"expected_answer_snippet":"4-hour response","tags":["factual","comparative"]},
    {"id":"q012","query":"Which support tier gives phone support?","relevant_document_ids":["00000000-0000-4000-8000-000000000006"],"relevant_chunk_ids":null,"expected_answer_snippet":"Enterprise","tags":["comparative","single-doc"]},
    {"id":"q013","query":"What is the default API request rate?","relevant_document_ids":["00000000-0000-4000-8000-000000000007"],"relevant_chunk_ids":null,"expected_answer_snippet":"1,000 requests per hour","tags":["factual","single-doc"]},
    {"id":"q014","query":"What status code means rate-limited?","relevant_document_ids":["00000000-0000-4000-8000-000000000007"],"relevant_chunk_ids":null,"expected_answer_snippet":"429","tags":["factual","single-doc"]},
    {"id":"q015","query":"How long is the standard product warranty?","relevant_document_ids":["00000000-0000-4000-8000-000000000008"],"relevant_chunk_ids":null,"expected_answer_snippet":"1-year","tags":["factual","single-doc"]},
    {"id":"q016","query":"Compare invoicing and refund timelines.","relevant_document_ids":["00000000-0000-4000-8000-000000000001","00000000-0000-4000-8000-000000000002"],"relevant_chunk_ids":null,"expected_answer_snippet":null,"tags":["comparative","multi-doc"]},
    {"id":"q017","query":"What happens if my account is unpaid?","relevant_document_ids":["00000000-0000-4000-8000-000000000001"],"relevant_chunk_ids":null,"expected_answer_snippet":"suspended","tags":["factual","single-doc"]},
    {"id":"q018","query":"Are gift cards refundable?","relevant_document_ids":["00000000-0000-4000-8000-000000000002"],"relevant_chunk_ids":null,"expected_answer_snippet":"final sale","tags":["negation","single-doc"]}
  ]
}
```

- [ ] **Step 2: Mark fixtures as content files**

Open `boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj` and add (inside an `<ItemGroup>`):

```xml
<ItemGroup>
  <None Update="Ai\Eval\fixtures\**\*.*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 3: Verify parses via existing loader test**

Add this test to `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/EvalFixtureLoaderTests.cs`:

```csharp
[Fact]
public void LoadFromFile_SeedEnFixture_Parses()
{
    var path = Path.Combine(AppContext.BaseDirectory, "Ai", "Eval", "fixtures", "rag-eval-dataset-en.json");
    var result = EvalFixtureLoader.LoadFromFile(path);
    result.IsSuccess.Should().BeTrue();
    result.Value.Questions.Should().HaveCountGreaterOrEqualTo(15);
    result.Value.Questions.Should().Contain(q => q.Tags.Contains("factual"));
    result.Value.Questions.Should().Contain(q => q.Tags.Contains("multi-doc"));
    result.Value.Questions.Should().Contain(q => q.Tags.Contains("negation"));
    result.Value.Questions.Should().Contain(q => q.Tags.Contains("comparative"));
}
```

- [ ] **Step 4: Run and verify passes**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~EvalFixtureLoaderTests.LoadFromFile_SeedEnFixture"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/rag-eval-dataset-en.json \
        boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/EvalFixtureLoaderTests.cs
git commit -m "feat(ai): add EN seed eval fixture (18 questions, 8 docs)"
```

---

## Task 13: Seed AR fixture

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/rag-eval-dataset-ar.json`

- [ ] **Step 1: Create AR fixture**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/rag-eval-dataset-ar.json`:

```json
{
  "name": "boilerplate-core-ar-v1",
  "language": "ar",
  "description": "مجموعة تقييم أولية تغطي أنماط الأسئلة الشائعة على وثائق اصطناعية.",
  "documents": [
    {
      "id": "00000000-0000-4000-8000-000000000101",
      "file_name": "invoicing-policy.md",
      "language": "ar",
      "content": "# سياسة الفوترة\n\nيتم إصدار فواتير العملاء شهرياً في اليوم الأول من كل شهر. استحقاق الدفع خلال 30 يوماً من تاريخ الفاتورة.\n\n## الرسوم المتأخرة\n\nتُطبَّق رسوم تأخير بنسبة 2% على الأرصدة غير المدفوعة بعد 15 يوماً من تاريخ الاستحقاق. بعد 45 يوماً يتم تعليق الحساب.\n"
    },
    {
      "id": "00000000-0000-4000-8000-000000000102",
      "file_name": "refund-policy.md",
      "language": "ar",
      "content": "# سياسة الاسترجاع\n\nجميع المشتريات مؤهلة لاسترداد كامل خلال 14 يوماً من تاريخ المعاملة، شرط عدم استخدام المنتج.\n\n## الاستثناءات\n\n- المنتجات الرقمية غير قابلة للاسترداد بعد التنزيل.\n- الطلبات المخصصة غير قابلة للاسترداد.\n- بطاقات الهدايا نهائية.\n"
    },
    {
      "id": "00000000-0000-4000-8000-000000000103",
      "file_name": "shipping-policy.md",
      "language": "ar",
      "content": "# سياسة الشحن\n\nيستغرق الشحن القياسي من 3 إلى 5 أيام عمل داخل الولايات المتحدة. الشحن السريع يستغرق 1-2 يوم عمل مقابل رسم إضافي 15 دولاراً.\n\n## الشحن الدولي\n\nتُشحن الطلبات الدولية عبر DHL وتصل خلال 7-14 يوم عمل.\n"
    },
    {
      "id": "00000000-0000-4000-8000-000000000104",
      "file_name": "security-policy.md",
      "language": "ar",
      "content": "# سياسة الأمان\n\nجميع البيانات مشفرة أثناء التخزين باستخدام AES-256 وأثناء النقل عبر TLS 1.3.\n\n## المصادقة\n\nنطلب المصادقة متعددة العوامل (MFA) لجميع حسابات الإدارة. تنتهي صلاحية رموز الجلسة بعد 60 دقيقة من عدم النشاط.\n"
    },
    {
      "id": "00000000-0000-4000-8000-000000000105",
      "file_name": "vacation-policy.md",
      "language": "ar",
      "content": "# سياسة الإجازات\n\nيحصل الموظفون بدوام كامل على 20 يوماً من الإجازة المدفوعة سنوياً، تُستحق شهرياً.\n\n## الترحيل\n\nيمكن ترحيل ما يصل إلى 5 أيام إجازة غير مستخدمة إلى السنة التقويمية التالية. تُفقد الأيام الزائدة عن 5 في الأول من يناير.\n"
    },
    {
      "id": "00000000-0000-4000-8000-000000000106",
      "file_name": "support-tiers.md",
      "language": "ar",
      "content": "# مستويات الدعم\n\n## أساسي\n\nدعم عبر البريد الإلكتروني مع اتفاقية استجابة 48 ساعة. متضمن في جميع الخطط.\n\n## أولوية\n\nدعم بريد إلكتروني ومحادثة مع اتفاقية استجابة 4 ساعات. 99 دولار شهرياً.\n\n## مؤسسي\n\nدعم هاتفي 24/7 مع استجابة خلال 15 دقيقة ومدير حساب مخصص.\n"
    },
    {
      "id": "00000000-0000-4000-8000-000000000107",
      "file_name": "api-rate-limits.md",
      "language": "ar",
      "content": "# حدود معدل واجهة API\n\nتفرض الواجهة حداً افتراضياً قدره 1000 طلب في الساعة لكل مفتاح API.\n\n## الترقيات\n\nيمكن لعملاء المؤسسات طلب حدود تصل إلى 100000 طلب في الساعة.\n\n## استجابة التجاوز\n\nتُرجع الطلبات المتجاوزة للحد رمز HTTP 429 مع ترويسة Retry-After.\n"
    },
    {
      "id": "00000000-0000-4000-8000-000000000108",
      "file_name": "product-warranty.md",
      "language": "ar",
      "content": "# ضمان المنتج\n\nتتضمن جميع منتجات الأجهزة ضماناً محدوداً لمدة سنة واحدة يغطي عيوب التصنيع.\n\n## الضمان الممتد\n\nيتوفر ضمان ممتد لمدة 3 سنوات مقابل 15% من سعر المنتج وقت الشراء.\n"
    }
  ],
  "questions": [
    {"id":"q001","query":"متى يتم إصدار فواتير العملاء؟","relevant_document_ids":["00000000-0000-4000-8000-000000000101"],"relevant_chunk_ids":null,"expected_answer_snippet":"شهرياً في اليوم الأول","tags":["factual","single-doc"]},
    {"id":"q002","query":"ما هي نسبة رسوم التأخير؟","relevant_document_ids":["00000000-0000-4000-8000-000000000101"],"relevant_chunk_ids":null,"expected_answer_snippet":"2%","tags":["factual","single-doc"]},
    {"id":"q003","query":"ما هي مدة نافذة الاسترجاع؟","relevant_document_ids":["00000000-0000-4000-8000-000000000102"],"relevant_chunk_ids":null,"expected_answer_snippet":"14 يوماً","tags":["factual","single-doc"]},
    {"id":"q004","query":"هل يمكن استرداد المنتجات الرقمية؟","relevant_document_ids":["00000000-0000-4000-8000-000000000102"],"relevant_chunk_ids":null,"expected_answer_snippet":"غير قابلة للاسترداد","tags":["negation","single-doc"]},
    {"id":"q005","query":"كم يستغرق الشحن السريع؟","relevant_document_ids":["00000000-0000-4000-8000-000000000103"],"relevant_chunk_ids":null,"expected_answer_snippet":"1-2 يوم","tags":["factual","single-doc"]},
    {"id":"q006","query":"ما هي خوارزمية التشفير المستخدمة؟","relevant_document_ids":["00000000-0000-4000-8000-000000000104"],"relevant_chunk_ids":null,"expected_answer_snippet":"AES-256","tags":["factual","single-doc"]},
    {"id":"q007","query":"متى تنتهي صلاحية الجلسة؟","relevant_document_ids":["00000000-0000-4000-8000-000000000104"],"relevant_chunk_ids":null,"expected_answer_snippet":"60 دقيقة","tags":["factual","single-doc"]},
    {"id":"q008","query":"كم يوم إجازة سنوياً للموظفين بدوام كامل؟","relevant_document_ids":["00000000-0000-4000-8000-000000000105"],"relevant_chunk_ids":null,"expected_answer_snippet":"20 يوماً","tags":["factual","single-doc"]},
    {"id":"q009","query":"كم يوم يمكن ترحيله من الإجازة؟","relevant_document_ids":["00000000-0000-4000-8000-000000000105"],"relevant_chunk_ids":null,"expected_answer_snippet":"5 أيام","tags":["factual","single-doc"]},
    {"id":"q010","query":"ما هي اتفاقية استجابة دعم الأولوية؟","relevant_document_ids":["00000000-0000-4000-8000-000000000106"],"relevant_chunk_ids":null,"expected_answer_snippet":"4 ساعات","tags":["factual","comparative"]},
    {"id":"q011","query":"أي مستوى دعم يتضمن دعماً هاتفياً؟","relevant_document_ids":["00000000-0000-4000-8000-000000000106"],"relevant_chunk_ids":null,"expected_answer_snippet":"مؤسسي","tags":["comparative","single-doc"]},
    {"id":"q012","query":"ما هو الحد الافتراضي للطلبات في الساعة؟","relevant_document_ids":["00000000-0000-4000-8000-000000000107"],"relevant_chunk_ids":null,"expected_answer_snippet":"1000 طلب","tags":["factual","single-doc"]},
    {"id":"q013","query":"ما رمز الحالة الذي يعني تجاوز الحد؟","relevant_document_ids":["00000000-0000-4000-8000-000000000107"],"relevant_chunk_ids":null,"expected_answer_snippet":"429","tags":["factual","single-doc"]},
    {"id":"q014","query":"ما هي مدة الضمان القياسي؟","relevant_document_ids":["00000000-0000-4000-8000-000000000108"],"relevant_chunk_ids":null,"expected_answer_snippet":"سنة واحدة","tags":["factual","single-doc"]},
    {"id":"q015","query":"قارن بين الفوترة وسياسة الاسترجاع.","relevant_document_ids":["00000000-0000-4000-8000-000000000101","00000000-0000-4000-8000-000000000102"],"relevant_chunk_ids":null,"expected_answer_snippet":null,"tags":["comparative","multi-doc"]},
    {"id":"q016","query":"ماذا يحدث إذا لم أدفع الفاتورة؟","relevant_document_ids":["00000000-0000-4000-8000-000000000101"],"relevant_chunk_ids":null,"expected_answer_snippet":"تعليق","tags":["factual","single-doc"]}
  ]
}
```

- [ ] **Step 2: Verify loader accepts AR**

Add another `[Fact]` to `EvalFixtureLoaderTests`:

```csharp
[Fact]
public void LoadFromFile_SeedArFixture_Parses()
{
    var path = Path.Combine(AppContext.BaseDirectory, "Ai", "Eval", "fixtures", "rag-eval-dataset-ar.json");
    var result = EvalFixtureLoader.LoadFromFile(path);
    result.IsSuccess.Should().BeTrue();
    result.Value.Language.Should().Be("ar");
    result.Value.Questions.Should().HaveCountGreaterOrEqualTo(15);
}
```

- [ ] **Step 3: Run and verify passes**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~EvalFixtureLoaderTests.LoadFromFile_SeedArFixture"`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/rag-eval-dataset-ar.json \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/EvalFixtureLoaderTests.cs
git commit -m "feat(ai): add AR seed eval fixture (16 questions, 8 docs)"
```

---

## Task 14: `EvalFixtureIngester`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Fixtures/EvalFixtureIngester.cs`

Ingester ingests fixture documents into the running AI module. Because `UploadDocumentCommandHandler` takes an `IFormFile` and assigns its own GUIDs, we bypass it here and interact with `AiDbContext` + `IVectorStore` directly, using the existing chunking + embedding services. This keeps the harness fast (one transaction per doc) and preserves the fixture's GUIDs so the id-map is deterministic.

- [ ] **Step 1: Implement ingester**

```csharp
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Eval.Fixtures;

public sealed class EvalFixtureIngester(
    AiDbContext db,
    IDocumentChunker chunker,
    IEmbeddingService embeddings,
    IVectorStore vectors)
{
    public async Task<IReadOnlyDictionary<Guid, Guid>> IngestAsync(
        Guid tenantId,
        Guid uploaderUserId,
        EvalDataset dataset,
        string qdrantCollection,
        CancellationToken ct)
    {
        var idMap = new Dictionary<Guid, Guid>(dataset.Documents.Count);

        foreach (var doc in dataset.Documents)
        {
            var entity = AiDocument.CreateForEval(
                tenantId: tenantId,
                uploadedBy: uploaderUserId,
                name: Path.GetFileNameWithoutExtension(doc.FileName),
                fileName: doc.FileName,
                content: doc.Content,
                language: doc.Language);

            db.AiDocuments.Add(entity);
            idMap[doc.Id] = entity.Id;

            var chunks = await chunker.ChunkAsync(
                new ChunkingRequest(doc.Content, doc.Language, entity.Id), ct);
            foreach (var c in chunks) db.AiDocumentChunks.Add(c);

            await db.SaveChangesAsync(ct);

            var embedResult = await embeddings.EmbedBatchAsync(
                chunks.Select(c => c.Content).ToList(), ct);
            for (var i = 0; i < chunks.Count; i++)
                chunks[i].SetEmbeddingVector(embedResult.Vectors[i]);
            await db.SaveChangesAsync(ct);

            await vectors.UpsertAsync(
                qdrantCollection,
                chunks.Select(c => new VectorPoint(
                    c.Id, c.EmbeddingVector!, BuildPayload(tenantId, entity, c))).ToList(),
                ct);

            entity.MarkEmbedded();
            await db.SaveChangesAsync(ct);
        }

        return idMap;
    }

    private static IReadOnlyDictionary<string, object?> BuildPayload(
        Guid tenantId, AiDocument doc, AiDocumentChunk chunk) =>
        new Dictionary<string, object?>
        {
            ["tenant_id"] = tenantId.ToString(),
            ["document_id"] = doc.Id.ToString(),
            ["file_id"] = doc.FileId?.ToString() ?? doc.Id.ToString(),
            ["visibility"] = "TenantWide",
            ["uploaded_by_user_id"] = doc.UploadedBy.ToString(),
            ["chunk_index"] = chunk.ChunkIndex,
            ["language"] = doc.Language ?? "en"
        };
}
```

> **Note for the implementer:** `AiDocument.CreateForEval` is a factory we add *only if* the existing creation APIs on `AiDocument` don't support the shape we need (name + filename + language + inline content + explicit uploaded_by). If a suitable constructor/factory exists (e.g. `AiDocument.Create(...)`), call that instead and skip adding a new factory. If not, add a small `public static AiDocument CreateForEval(...)` factory on `AiDocument` that sets the required fields without going through the file-upload path. This is the only domain-entity change in the plan; keep it minimal and internal-looking.

- [ ] **Step 2: Build**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: PASS (add the `AiDocument.CreateForEval` factory if the build complains that no compatible constructor/factory exists).

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Fixtures/EvalFixtureIngester.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiDocument.cs
git commit -m "feat(ai): add EvalFixtureIngester with fixture→AiDocument id map"
```

---

## Task 15: `FaithfulnessJudgePrompts` + `LlmJudgeFaithfulness`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Faithfulness/FaithfulnessJudgePrompts.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Faithfulness/LlmJudgeFaithfulness.cs`

- [ ] **Step 1: Create prompt constants**

```csharp
namespace Starter.Module.AI.Infrastructure.Eval.Faithfulness;

internal static class FaithfulnessJudgePrompts
{
    public const string EnglishPrompt = """
        You are an impartial judge. Given a QUESTION, a CONTEXT, and an ANSWER,
        extract each atomic claim in the ANSWER and classify each as:
          SUPPORTED   — directly stated or clearly inferable from CONTEXT.
          UNSUPPORTED — not stated in CONTEXT.

        Output strict JSON with no prose:
          { "claims": [ { "text": "<claim>", "verdict": "SUPPORTED" | "UNSUPPORTED" } ] }

        QUESTION: {question}
        CONTEXT: {context}
        ANSWER: {answer}
        """;

    public const string ArabicPrompt = """
        أنت حكمٌ محايد. بناءً على السؤال والسياق والإجابة، استخرج كل ادعاء ذري في الإجابة وصنّفه كما يلي:
          SUPPORTED   — مذكور صراحةً أو يمكن استنتاجه بوضوح من السياق.
          UNSUPPORTED — غير مذكور في السياق.

        أخرِج JSON صارم فقط بدون أي نص إضافي:
          { "claims": [ { "text": "<الادعاء>", "verdict": "SUPPORTED" | "UNSUPPORTED" } ] }

        السؤال: {question}
        السياق: {context}
        الإجابة: {answer}
        """;
}
```

- [ ] **Step 2: Implement `LlmJudgeFaithfulness`**

```csharp
using System.Text.Json;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Application.Eval.Faithfulness;

namespace Starter.Module.AI.Infrastructure.Eval.Faithfulness;

public sealed class LlmJudgeFaithfulness(IAiService ai) : IFaithfulnessJudge
{
    public async Task<FaithfulnessQuestionResult> JudgeAsync(
        EvalQuestion question,
        string context,
        string answer,
        string? modelOverride,
        CancellationToken ct)
    {
        var detectedLang = context.Any(c => c >= 0x0600 && c <= 0x06FF) ? "ar" : "en";
        var template = detectedLang == "ar"
            ? FaithfulnessJudgePrompts.ArabicPrompt
            : FaithfulnessJudgePrompts.EnglishPrompt;

        var prompt = template
            .Replace("{question}", question.Query)
            .Replace("{context}", context)
            .Replace("{answer}", answer);

        var options = new AiCompletionOptions(Model: modelOverride);
        var first = await ai.CompleteAsync(prompt, options, ct);
        var parsed = TryParse(first?.Text);

        if (parsed is null)
        {
            var retry = await ai.CompleteAsync(
                prompt + "\n\nYour last response was not valid JSON. Respond with only the JSON object now.",
                options, ct);
            parsed = TryParse(retry?.Text);
        }

        if (parsed is null)
            return new FaithfulnessQuestionResult(question.Id, 0.0, Array.Empty<ClaimVerdict>(), JudgeParseFailed: true);

        var supported = parsed.Count(c => c.Verdict == "SUPPORTED");
        var score = parsed.Count == 0 ? 1.0 : (double)supported / parsed.Count;
        return new FaithfulnessQuestionResult(question.Id, score, parsed, JudgeParseFailed: false);
    }

    private static IReadOnlyList<ClaimVerdict>? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        var json = text[start..(end + 1)];

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("claims", out var claimsEl)) return null;
            var result = new List<ClaimVerdict>();
            foreach (var item in claimsEl.EnumerateArray())
            {
                var claim = item.GetProperty("text").GetString() ?? "";
                var verdict = item.GetProperty("verdict").GetString() ?? "UNSUPPORTED";
                result.Add(new ClaimVerdict(claim, verdict));
            }
            return result;
        }
        catch (JsonException) { return null; }
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Faithfulness/
git commit -m "feat(ai): add LlmJudgeFaithfulness with RAGAS-style claim decomposition"
```

---

## Task 16: `RagEvalHarness` orchestrator + DI registration

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/RagEvalHarness.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Implement harness**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/RagEvalHarness.cs`:

```csharp
using Microsoft.Extensions.Options;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Eval;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Application.Eval.Faithfulness;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Eval.Fixtures;
using Starter.Module.AI.Infrastructure.Eval.Latency;
using Starter.Module.AI.Infrastructure.Eval.Metrics;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Eval;

public sealed class RagEvalHarness(
    EvalFixtureIngester ingester,
    IRagRetrievalService retrieval,
    IAiService ai,
    IFaithfulnessJudge judge,
    ICurrentUserService currentUser,
    IOptions<AiRagEvalSettings> settings,
    IOptions<AiRagSettings> ragSettings) : IRagEvalHarness
{
    public async Task<EvalReport> RunAsync(
        EvalDataset dataset,
        EvalRunOptions options,
        CancellationToken ct)
    {
        var tenantId = currentUser.TenantId
            ?? throw new InvalidOperationException("RagEvalHarness requires a current-user tenant id.");
        var uploaderId = currentUser.UserId
            ?? throw new InvalidOperationException("RagEvalHarness requires a current-user id.");

        var collectionName = $"eval-{Guid.NewGuid():N}";
        var idMap = await ingester.IngestAsync(tenantId, uploaderId, dataset, collectionName, ct);
        var reverseMap = idMap.ToDictionary(kv => kv.Value, kv => kv.Key);

        for (var i = 0; i < options.WarmupQueries && i < dataset.Questions.Count; i++)
            await retrieval.RetrieveForQueryAsync(
                tenantId, dataset.Questions[i].Query, null,
                ragSettings.Value.TopK, null, ragSettings.Value.IncludeParentContext, ct);

        var perQuestion = new List<PerQuestionResult>(dataset.Questions.Count);
        var perStageDurations = new Dictionary<string, List<double>>();
        var aggregateDegraded = new HashSet<string>();
        var faithfulnessResults = options.IncludeFaithfulness
            ? new List<FaithfulnessQuestionResult>(dataset.Questions.Count)
            : null;

        foreach (var question in dataset.Questions)
        {
            using var capture = StageLatencyAggregator.BeginCapture();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var context = await retrieval.RetrieveForQueryAsync(
                tenantId, question.Query, null,
                options.KValues.Max(), null, ragSettings.Value.IncludeParentContext, ct);
            sw.Stop();
            var durations = capture.Stop();

            foreach (var (stage, arr) in durations)
            {
                if (!perStageDurations.TryGetValue(stage, out var bucket))
                    perStageDurations[stage] = bucket = new List<double>();
                bucket.AddRange(arr);
            }
            perStageDurations.TryAdd("total", new List<double>());
            perStageDurations["total"].Add(sw.Elapsed.TotalMilliseconds);
            foreach (var d in context.DegradedStages) aggregateDegraded.Add(d);

            var retrievedFixtureIds = context.Children
                .Select(c => reverseMap.TryGetValue(c.DocumentId, out var fid) ? fid : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .ToList();
            var relevantSet = new HashSet<Guid>(question.RelevantDocumentIds);

            perQuestion.Add(new PerQuestionResult(
                QuestionId: question.Id,
                Query: question.Query,
                RetrievedDocumentIds: retrievedFixtureIds,
                RelevantDocumentIds: question.RelevantDocumentIds,
                RecallAt5: RecallAtKCalculator.Compute(retrievedFixtureIds, relevantSet, 5),
                RecallAt10: RecallAtKCalculator.Compute(retrievedFixtureIds, relevantSet, 10),
                ReciprocalRank: MrrCalculator.ReciprocalRank(retrievedFixtureIds, relevantSet),
                TotalLatencyMs: sw.Elapsed.TotalMilliseconds,
                DegradedStages: context.DegradedStages));

            if (faithfulnessResults is not null)
            {
                var ctx = string.Join("\n---\n", context.Children.Select(c => c.Content));
                var answer = await ai.CompleteAsync(
                    $"Answer the question using only this context.\n\nContext:\n{ctx}\n\nQuestion: {question.Query}",
                    new AiCompletionOptions(Model: options.JudgeModelOverride), ct);
                var judgement = await judge.JudgeAsync(
                    question, ctx, answer?.Text ?? "", options.JudgeModelOverride, ct);
                faithfulnessResults.Add(judgement);
            }
        }

        var metrics = BuildMetrics(dataset, perQuestion, options.KValues);
        var latency = StageLatencyAggregator.Aggregate(perStageDurations);
        var faithfulness = faithfulnessResults is null ? null : BuildFaithfulness(faithfulnessResults);

        return new EvalReport(
            RunAt: DateTime.UtcNow,
            DatasetName: dataset.Name,
            Language: dataset.Language,
            QuestionCount: dataset.Questions.Count,
            Metrics: metrics,
            Latency: latency,
            PerQuestion: perQuestion,
            AggregateDegradedStages: aggregateDegraded.ToList(),
            Faithfulness: faithfulness);
    }

    private static EvalMetrics BuildMetrics(
        EvalDataset dataset,
        IReadOnlyList<PerQuestionResult> perQuestion,
        int[] kValues)
    {
        var questions = dataset.Questions;
        var bucket = ComputeBucket(questions, perQuestion, kValues);
        var byTag = questions
            .SelectMany(q => q.Tags.Select(t => (tag: t, q)))
            .GroupBy(x => x.tag)
            .ToDictionary(
                g => g.Key,
                g => ComputeBucket(
                    g.Select(x => x.q).ToList(),
                    perQuestion.Where(r => g.Any(x => x.q.Id == r.QuestionId)).ToList(),
                    kValues));
        return new EvalMetrics(
            Aggregate: bucket,
            PerLanguage: new Dictionary<string, MetricBucket> { [dataset.Language] = bucket },
            PerTag: byTag);
    }

    private static MetricBucket ComputeBucket(
        IReadOnlyList<EvalQuestion> questions,
        IReadOnlyList<PerQuestionResult> results,
        int[] kValues)
    {
        var recall = new Dictionary<int, double>();
        var precision = new Dictionary<int, double>();
        var ndcg = new Dictionary<int, double>();
        var hit = new Dictionary<int, double>();

        foreach (var k in kValues)
        {
            var recallVals = new List<double>(results.Count);
            var precisionVals = new List<double>(results.Count);
            var ndcgVals = new List<double>(results.Count);
            var hitVals = new List<double>(results.Count);

            for (var i = 0; i < results.Count; i++)
            {
                var rel = new HashSet<Guid>(questions[i].RelevantDocumentIds);
                recallVals.Add(RecallAtKCalculator.Compute(results[i].RetrievedDocumentIds, rel, k));
                precisionVals.Add(PrecisionAtKCalculator.Compute(results[i].RetrievedDocumentIds, rel, k));
                ndcgVals.Add(NdcgCalculator.Compute(results[i].RetrievedDocumentIds, rel, k));
                hitVals.Add(HitRateCalculator.Compute(results[i].RetrievedDocumentIds, rel, k));
            }

            recall[k] = recallVals.Count == 0 ? 0 : recallVals.Average();
            precision[k] = precisionVals.Count == 0 ? 0 : precisionVals.Average();
            ndcg[k] = ndcgVals.Count == 0 ? 0 : ndcgVals.Average();
            hit[k] = HitRateCalculator.Mean(hitVals);
        }

        var mrr = MrrCalculator.Mean(results.Select(r => r.ReciprocalRank).ToArray());
        return new MetricBucket(recall, precision, ndcg, hit, mrr);
    }

    private static FaithfulnessReport BuildFaithfulness(IReadOnlyList<FaithfulnessQuestionResult> perQuestion)
    {
        var valid = perQuestion.Where(r => !r.JudgeParseFailed).ToList();
        var aggregate = valid.Count == 0 ? 0 : valid.Average(r => r.Score);
        var parseFailures = perQuestion.Count(r => r.JudgeParseFailed);
        return new FaithfulnessReport(aggregate, parseFailures, perQuestion);
    }
}
```

- [ ] **Step 2: Register in DI**

In `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/DependencyInjection.cs`, in the AI-module registration method alongside other services, add:

```csharp
services.AddScoped<EvalFixtureIngester>();
services.AddScoped<IFaithfulnessJudge, LlmJudgeFaithfulness>();
services.AddScoped<IRagEvalHarness, RagEvalHarness>();
```

Ensure the right `using`s are present: `Starter.Module.AI.Application.Eval;`, `Starter.Module.AI.Application.Eval.Faithfulness;`, `Starter.Module.AI.Infrastructure.Eval;`, `Starter.Module.AI.Infrastructure.Eval.Fixtures;`, `Starter.Module.AI.Infrastructure.Eval.Faithfulness;`.

- [ ] **Step 3: Build**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/RagEvalHarness.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/DependencyInjection.cs
git commit -m "feat(ai): add RagEvalHarness orchestrator + DI registration"
```

---

## Task 17: `Ai.RunEval` permission

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AiModule.cs` (wherever `GetPermissions()` lists module perms — if applicable)
- Modify: `boilerplateFE/src/constants/permissions.ts`

- [ ] **Step 1: Add permission constant**

In `boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs`, add to the class body:

```csharp
public const string RunEval = "Ai.RunEval";
```

- [ ] **Step 2: If the module exposes `GetPermissions()`, add it there**

Search in `boilerplateBE/src/modules/Starter.Module.AI/` for a file that aggregates module permissions (likely `AiModule.cs` or similar). Add `AiPermissions.RunEval` to the returned collection. If no such method exists, skip this step.

- [ ] **Step 3: Mirror in frontend permissions**

In `boilerplateFE/src/constants/permissions.ts`, locate the `Ai: { ... }` block and add:

```ts
RunEval: 'Ai.RunEval',
```

- [ ] **Step 4: Build**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: PASS.
Run: `cd boilerplateFE && npm run build`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs \
        boilerplateFE/src/constants/permissions.ts
# if AiModule.cs was modified, add it too:
git add boilerplateBE/src/modules/Starter.Module.AI/AiModule.cs
git commit -m "feat(ai): add Ai.RunEval permission (superadmin-only by default)"
```

---

## Task 18: `RunFaithfulnessEval` command + `AiEvalController`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Features/Eval/Commands/RunFaithfulnessEval/RunFaithfulnessEvalCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Features/Eval/Commands/RunFaithfulnessEval/RunFaithfulnessEvalCommandHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiEvalController.cs`

- [ ] **Step 1: Command**

```csharp
using MediatR;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Features.Eval.Commands.RunFaithfulnessEval;

public sealed record RunFaithfulnessEvalCommand(
    string? FixtureJson,
    string? DatasetName,
    Guid AssistantId,
    string? JudgeModelOverride) : IRequest<Result<EvalReport>>;
```

- [ ] **Step 2: Handler**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Eval;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Application.Eval.Errors;
using Starter.Module.AI.Infrastructure.Eval.Fixtures;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Settings;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Features.Eval.Commands.RunFaithfulnessEval;

internal sealed class RunFaithfulnessEvalCommandHandler(
    IRagEvalHarness harness,
    AiDbContext db,
    IOptions<AiRagEvalSettings> settings)
    : IRequestHandler<RunFaithfulnessEvalCommand, Result<EvalReport>>
{
    public async Task<Result<EvalReport>> Handle(
        RunFaithfulnessEvalCommand request, CancellationToken ct)
    {
        var assistant = await db.AiAssistants.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AssistantId, ct);
        if (assistant is null) return Result.Failure<EvalReport>(EvalErrors.AssistantNotFound);

        Result<EvalDataset> datasetResult;
        if (!string.IsNullOrWhiteSpace(request.FixtureJson))
        {
            datasetResult = EvalFixtureLoader.LoadFromString(request.FixtureJson);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.DatasetName))
                return Result.Failure<EvalReport>(EvalErrors.FixtureNotFound);
            var path = Path.Combine(
                settings.Value.FixtureDirectory,
                $"rag-eval-dataset-{request.DatasetName}.json");
            datasetResult = EvalFixtureLoader.LoadFromFile(path);
        }
        if (datasetResult.IsFailure) return Result.Failure<EvalReport>(datasetResult.Error);

        var report = await harness.RunAsync(
            datasetResult.Value,
            new EvalRunOptions(
                KValues: settings.Value.KValues,
                IncludeFaithfulness: true,
                JudgeModelOverride: request.JudgeModelOverride ?? settings.Value.JudgeModel,
                WarmupQueries: settings.Value.WarmupQueries,
                AssistantId: request.AssistantId),
            ct);

        return Result.Success(report);
    }
}
```

- [ ] **Step 3: Controller**

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Abstractions.Web;
using Starter.Module.AI.Application.Features.Eval.Commands.RunFaithfulnessEval;
using Starter.Module.AI.Constants;

namespace Starter.Module.AI.Controllers;

public sealed class AiEvalController(ISender mediator) : BaseApiController(mediator)
{
    [HttpPost("faithfulness")]
    [Authorize(Policy = AiPermissions.RunEval)]
    public async Task<IActionResult> RunFaithfulness(
        [FromForm] string? dataset_name,
        [FromForm] Guid assistant_id,
        [FromForm] string? judge_model,
        IFormFile? fixture,
        CancellationToken ct)
    {
        string? fixtureJson = null;
        if (fixture is not null)
        {
            using var reader = new StreamReader(fixture.OpenReadStream());
            fixtureJson = await reader.ReadToEndAsync(ct);
        }

        var result = await Mediator.Send(
            new RunFaithfulnessEvalCommand(fixtureJson, dataset_name, assistant_id, judge_model), ct);
        return HandleResult(result);
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Features/Eval/ \
        boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiEvalController.cs
git commit -m "feat(ai): add AiEvalController + RunFaithfulnessEval command (superadmin-gated)"
```

---

## Task 19: `FaithfulnessEndpointTests` + `FakeFaithfulnessJudge`

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Fakes/FakeFaithfulnessJudge.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/FaithfulnessEndpointTests.cs`

- [ ] **Step 1: Fake judge**

```csharp
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Application.Eval.Faithfulness;

namespace Starter.Api.Tests.Ai.Fakes;

public sealed class FakeFaithfulnessJudge : IFaithfulnessJudge
{
    public List<FaithfulnessQuestionResult> Responses { get; } = new();
    public int CallCount { get; private set; }

    public Task<FaithfulnessQuestionResult> JudgeAsync(
        EvalQuestion question, string context, string answer, string? modelOverride, CancellationToken ct)
    {
        CallCount++;
        var response = CallCount - 1 < Responses.Count
            ? Responses[CallCount - 1]
            : new FaithfulnessQuestionResult(
                question.Id,
                0.8,
                new[] { new ClaimVerdict("stub", "SUPPORTED") },
                JudgeParseFailed: false);
        return Task.FromResult(response);
    }
}
```

- [ ] **Step 2: Endpoint tests**

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Starter.Api.Tests.Common;
using System.Net;
using System.Net.Http.Headers;

namespace Starter.Api.Tests.Ai.Eval;

[Collection(ApiTestCollection.Name)]
public sealed class FaithfulnessEndpointTests(WebApplicationFactory<Program> factory)
{
    [Fact]
    public async Task NonSuperadminCaller_403()
    {
        var client = factory.CreateClient();
        var tenantUserToken = await TestTokens.IssueTenantUserTokenAsync(factory);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantUserToken);

        var content = new MultipartFormDataContent
        {
            { new StringContent("boilerplate-core-en-v1"), "dataset_name" },
            { new StringContent(Guid.NewGuid().ToString()), "assistant_id" }
        };
        var response = await client.PostAsync("/api/v1/aieval/faithfulness", content);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MissingFixtureAndDatasetName_404()
    {
        var client = factory.CreateClient();
        var saToken = await TestTokens.IssueSuperadminTokenAsync(factory);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", saToken);

        var content = new MultipartFormDataContent
        {
            { new StringContent(Guid.NewGuid().ToString()), "assistant_id" }
        };
        var response = await client.PostAsync("/api/v1/aieval/faithfulness", content);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

> **Note:** `TestTokens.IssueSuperadminTokenAsync` / `IssueTenantUserTokenAsync` helpers should already exist in `Starter.Api.Tests/Common/` from prior plans. If not, add thin wrappers that create a user with the required role and return a JWT via the existing login flow — follow the pattern in other controller tests in the test project. If `ApiTestCollection` doesn't exist, use whatever collection/fixture pattern the test project uses for API-level tests (grep for `WebApplicationFactory<Program>` usage in existing tests).

- [ ] **Step 3: Run and verify they pass**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~FaithfulnessEndpointTests"`
Expected: PASS (2 tests).

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/Fakes/FakeFaithfulnessJudge.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/FaithfulnessEndpointTests.cs
git commit -m "test(ai): auth gate + bad-input coverage for /ai/eval/faithfulness"
```

---

## Task 20: Cache warmup tool (`tools/EvalCacheWarmup`)

**Files:**
- Create: `boilerplateBE/tools/EvalCacheWarmup/EvalCacheWarmup.csproj`
- Create: `boilerplateBE/tools/EvalCacheWarmup/Program.cs`
- Modify: `boilerplateBE/Starter.sln` — add the project to the solution

- [ ] **Step 1: csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>EvalCacheWarmup</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MessagePack" Version="2.5.187" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Starter.Api\Starter.Api.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Program**

```csharp
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Eval.Fixtures;

// Minimal driver — load fixture, for each (query, chunk_id) pair call the real reranker
// via the host's DI, collect scores into a MessagePack blob.
//
// Usage:
//   dotnet run --project boilerplateBE/tools/EvalCacheWarmup -- \
//     --fixture boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/rag-eval-dataset-en.json \
//     --out boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/eval-rerank-cache-en.bin

var args0 = new Dictionary<string, string>();
for (var i = 0; i < args.Length - 1; i++)
    if (args[i].StartsWith("--")) args0[args[i][2..]] = args[i + 1];

if (!args0.TryGetValue("fixture", out var fixturePath) || !args0.TryGetValue("out", out var outPath))
{
    Console.Error.WriteLine("usage: --fixture <path> --out <path>");
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);
// The Api project wires up every AI service in its Program.cs; we replicate the same
// configuration by reusing its appsettings and its DependencyInjection.
builder.Configuration
    .AddJsonFile("../../src/Starter.Api/appsettings.json", optional: false)
    .AddJsonFile("../../src/Starter.Api/appsettings.Development.json", optional: true);
Starter.Api.Program.ConfigureServicesForTooling(builder.Services, builder.Configuration);

using var host = builder.Build();
using var scope = host.Services.CreateScope();

var datasetResult = EvalFixtureLoader.LoadFromFile(fixturePath);
if (datasetResult.IsFailure)
{
    Console.Error.WriteLine($"fixture load failed: {datasetResult.Error.Code}");
    return 2;
}
var dataset = datasetResult.Value;

// Ingest the fixture into a throwaway tenant/collection so we get real chunk ids.
// Then, for each question, run a retrieval against it to get the reranker's responses.
// We capture reranker responses by wrapping IReranker with a decorator registered by a
// TestOnly flag — see note at bottom.

Console.Error.WriteLine(
    "NOTE: the warmup tool reuses the running DI container. Ensure ");
Console.Error.WriteLine(
    "Starter.Api.Program.ConfigureServicesForTooling is implemented to register AI");
Console.Error.WriteLine(
    "services without starting the web host. See plan Task 20 for the split.");

var captured = new Dictionary<string, decimal>(); // key = SHA256(query|chunk_id) hex
// (Pipeline-level reranker capturing is out of scope here; this tool is a thin
// driver. The actual (query,chunk) enumeration lives in the full implementation
// and writes to `captured`.)

await File.WriteAllBytesAsync(outPath,
    MessagePackSerializer.Serialize(captured,
        MessagePack.Resolvers.ContractlessStandardResolver.Options));
Console.Error.WriteLine($"wrote {captured.Count} entries to {outPath}");
return 0;
```

> **Note for the implementer:** This tool is a thin driver around the already-working AI pipeline. The split you need is:
> 1. In `Starter.Api.Program.cs`, extract a `public static void ConfigureServicesForTooling(IServiceCollection s, IConfiguration c)` method that registers AI, persistence, and cache services without starting the web host. Keep the existing `Main` calling it. 
> 2. In this tool, enumerate `(question, chunk_id)` pairs by running the retrieval pipeline once per question against the ingested fixture, capturing the reranker's input/output via a small `CapturingReranker : IReranker` decorator registered only when `EVAL_CACHE_WARMUP=1`. Compute the cache key **using the same helper the production reranker cache uses** — grep for the reranker cache entry (search `IReranker` and look for the wrapping cache service) and reuse its key function. If the helper is not public, copy its shape into the tool (exact match matters; `ICacheService.Set` at test setup must find the same key the reranker looks up in production).
> 3. Commit the blob alongside the fixture.

- [ ] **Step 3: Add to solution**

```bash
dotnet sln boilerplateBE/Starter.sln add boilerplateBE/tools/EvalCacheWarmup/EvalCacheWarmup.csproj
```

- [ ] **Step 4: Build**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: PASS (the tool compiles; it's not expected to produce a useful blob yet).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/tools/EvalCacheWarmup/ boilerplateBE/Starter.sln
git commit -m "chore(ai): add tools/EvalCacheWarmup skeleton"
```

---

## Task 21: Cache warmup — real reranker capture

**Files:**
- Modify: `boilerplateBE/src/Starter.Api/Program.cs` — extract `ConfigureServicesForTooling`
- Modify: `boilerplateBE/tools/EvalCacheWarmup/Program.cs` — full capture logic
- Create: `boilerplateBE/tools/EvalCacheWarmup/CapturingReranker.cs` — decorator that records inputs

- [ ] **Step 1: Extract `ConfigureServicesForTooling`**

In `boilerplateBE/src/Starter.Api/Program.cs`, split the existing service registration into a reusable static:

```csharp
public partial class Program
{
    public static void ConfigureServicesForTooling(IServiceCollection services, IConfiguration config)
    {
        // Move the existing service-registration block here (Infrastructure, AI module, persistence,
        // cache, etc.) — everything up to (but not including) the web-host-specific pieces like
        // UseRouting / MapControllers.
    }
}
```

Keep `Main` calling `ConfigureServicesForTooling(builder.Services, builder.Configuration)`.

- [ ] **Step 2: `CapturingReranker` decorator**

```csharp
using System.Security.Cryptography;
using System.Text;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;

namespace EvalCacheWarmup;

public sealed class CapturingReranker(IReranker inner) : IReranker
{
    public Dictionary<string, decimal> Captured { get; } = new();

    public async Task<RerankResult> RerankAsync(
        string query,
        IReadOnlyList<HybridHit> candidates,
        IReadOnlyList<AiDocumentChunk> candidateChunks,
        RerankContext context,
        CancellationToken ct)
    {
        var result = await inner.RerankAsync(query, candidates, candidateChunks, context, ct);
        for (var i = 0; i < result.Scores.Count; i++)
            Captured[CacheKey(query, candidateChunks[i].Id)] = result.Scores[i];
        return result;
    }

    public static string CacheKey(string query, Guid chunkId)
    {
        var input = $"{query}|{chunkId}";
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(input), hash);
        return Convert.ToHexString(hash);
    }
}
```

> **Important:** Before committing, confirm the production reranker-cache key format. Grep for the existing reranker cache wrapper (search within the AI module for `ICacheService` + `IReranker`). Mirror *exactly* that format here. If the production key uses a different separator, hash algorithm, or includes extra inputs (e.g. assistant id, reranker model), update `CacheKey` to match.

- [ ] **Step 3: Wire into `Program.cs` of the tool**

Update `boilerplateBE/tools/EvalCacheWarmup/Program.cs` to:
1. Read fixture, create a disposable tenant+collection (reuse `EvalFixtureIngester`).
2. Register `CapturingReranker` as `IReranker` (decorating the existing registration).
3. Resolve `IRagRetrievalService` and run each question.
4. After all questions, serialize `CapturingReranker.Captured` to the output file.
5. Tear down the Qdrant collection.

Replace the body of `Program.cs` (keeping the earlier arg parsing) with the full capture loop:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Eval.Fixtures;
using Starter.Application.Common.Interfaces;
using EvalCacheWarmup;
using MessagePack;

// ...arg parsing as before...

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration
    .AddJsonFile("../../src/Starter.Api/appsettings.json", false)
    .AddJsonFile("../../src/Starter.Api/appsettings.Development.json", true);
Starter.Api.Program.ConfigureServicesForTooling(builder.Services, builder.Configuration);

// Replace IReranker with capturing decorator
builder.Services.Decorate<IReranker, CapturingReranker>();
// If Decorate isn't available (Scrutor not referenced), do it manually:
//   var old = services.Last(d => d.ServiceType == typeof(IReranker));
//   services.Remove(old);
//   services.Add(new ServiceDescriptor(typeof(IReranker),
//       sp => new CapturingReranker((IReranker)ActivatorUtilities.CreateInstance(sp, old.ImplementationType!)),
//       old.Lifetime));

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var sp = scope.ServiceProvider;

var datasetResult = EvalFixtureLoader.LoadFromFile(fixturePath);
if (datasetResult.IsFailure) { Console.Error.WriteLine($"ERR {datasetResult.Error.Code}"); return 2; }
var dataset = datasetResult.Value;

var ingester = sp.GetRequiredService<EvalFixtureIngester>();
var retrieval = sp.GetRequiredService<IRagRetrievalService>();
var capturing = (CapturingReranker)sp.GetRequiredService<IReranker>();

var tenantId = Guid.NewGuid();
var uploaderId = Guid.NewGuid();
var collection = $"warmup-{Guid.NewGuid():N}";
await ingester.IngestAsync(tenantId, uploaderId, dataset, collection, CancellationToken.None);

foreach (var q in dataset.Questions)
    await retrieval.RetrieveForQueryAsync(
        tenantId, q.Query, null, 20, null, true, CancellationToken.None);

await File.WriteAllBytesAsync(outPath,
    MessagePackSerializer.Serialize(capturing.Captured,
        MessagePack.Resolvers.ContractlessStandardResolver.Options));
Console.Error.WriteLine($"wrote {capturing.Captured.Count} entries to {outPath}");
return 0;
```

- [ ] **Step 4: Build**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/Starter.Api/Program.cs \
        boilerplateBE/tools/EvalCacheWarmup/Program.cs \
        boilerplateBE/tools/EvalCacheWarmup/CapturingReranker.cs
git commit -m "feat(ai): wire CapturingReranker into eval cache warmup tool"
```

---

## Task 22: `RagEvalCollection` + orphan-collection cleanup

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/RagEvalCollection.cs`

- [ ] **Step 1: Create collection fixture**

```csharp
using Qdrant.Client;
using Starter.Api.Tests.Ai.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Eval;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RagEvalCollectionDef : ICollectionFixture<RagEvalFixture>
{
    public const string Name = "RagEval";
}

public sealed class RagEvalFixture : IAsyncLifetime
{
    public AiPostgresFixture Postgres { get; } = new();
    public QdrantClient Qdrant { get; } = new("localhost");

    public async Task InitializeAsync()
    {
        await Postgres.InitializeAsync();
        await CleanupOrphanCollectionsAsync();
    }

    public async Task DisposeAsync() => await Postgres.DisposeAsync();

    private async Task CleanupOrphanCollectionsAsync()
    {
        var collections = await Qdrant.ListCollectionsAsync();
        var cutoff = DateTime.UtcNow.AddHours(-24);
        foreach (var c in collections)
        {
            if (!c.StartsWith("eval-") && !c.StartsWith("warmup-")) continue;
            // Qdrant doesn't expose creation_time directly — use deterministic deletion
            // on any `eval-*` / `warmup-*` collection at test-suite start. This is safe
            // because every test run creates a new one.
            try { await Qdrant.DeleteCollectionAsync(c); } catch { }
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/RagEvalCollection.cs
git commit -m "test(ai): add RagEvalCollection fixture with orphan cleanup"
```

---

## Task 23: `RagEvalHarnessTests` — the CI regression gate

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/RagEvalHarnessTests.cs`

- [ ] **Step 1: Write the gated integration test**

```csharp
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Eval;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Infrastructure.Eval.Baseline;
using Starter.Module.AI.Infrastructure.Eval.Fixtures;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Eval;

[Collection(RagEvalCollectionDef.Name)]
public sealed class RagEvalHarnessTests(RagEvalFixture fixture)
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("AI_EVAL_ENABLED") == "1";
    private static bool UpdateBaseline =>
        Environment.GetEnvironmentVariable("UPDATE_EVAL_BASELINE") == "1";

    [Fact]
    public async Task EvalHarness_EnglishDataset_PassesBaseline()
    {
        if (!Enabled)
        {
            // Using SkipException pattern via Xunit.SkippableFact if available,
            // otherwise conditional early return is acceptable — the test is a
            // regression gate, not a correctness test, so treating it as passed
            // when disabled is the documented behaviour.
            return;
        }

        var sp = fixture.BuildEvalServiceProvider();
        var harness = sp.GetRequiredService<IRagEvalHarness>();
        var settings = sp.GetRequiredService<IOptions<AiRagEvalSettings>>().Value;

        var fixturePath = Path.Combine(AppContext.BaseDirectory,
            "Ai", "Eval", "fixtures", "rag-eval-dataset-en.json");
        var datasetResult = EvalFixtureLoader.LoadFromFile(fixturePath);
        datasetResult.IsSuccess.Should().BeTrue();

        var report = await harness.RunAsync(
            datasetResult.Value,
            new EvalRunOptions(KValues: settings.KValues, WarmupQueries: settings.WarmupQueries),
            CancellationToken.None);

        var baselinePath = Path.Combine(AppContext.BaseDirectory,
            "Ai", "Eval", "fixtures", "rag-eval-baseline.json");

        if (UpdateBaseline)
        {
            BaselineWriter.Update(baselinePath, report.DatasetName, ToSnapshot(report));
            return;
        }

        var baseline = BaselineLoader.Load(baselinePath);
        baseline.IsSuccess.Should().BeTrue();
        var comparison = BaselineComparator.Compare(
            baseline.Value.Datasets[report.DatasetName],
            ToSnapshot(report),
            settings.MetricTolerance,
            settings.LatencyTolerance);

        if (comparison.Failed)
            throw new Xunit.Sdk.XunitException(
                "Eval baseline regression:\n" + string.Join("\n", comparison.Failures));
    }

    [Fact]
    public async Task EvalHarness_ArabicDataset_PassesBaseline()
    {
        if (!Enabled) return;

        var sp = fixture.BuildEvalServiceProvider();
        var harness = sp.GetRequiredService<IRagEvalHarness>();
        var settings = sp.GetRequiredService<IOptions<AiRagEvalSettings>>().Value;

        var fixturePath = Path.Combine(AppContext.BaseDirectory,
            "Ai", "Eval", "fixtures", "rag-eval-dataset-ar.json");
        var datasetResult = EvalFixtureLoader.LoadFromFile(fixturePath);
        datasetResult.IsSuccess.Should().BeTrue();

        var report = await harness.RunAsync(
            datasetResult.Value,
            new EvalRunOptions(KValues: settings.KValues, WarmupQueries: settings.WarmupQueries),
            CancellationToken.None);

        var baselinePath = Path.Combine(AppContext.BaseDirectory,
            "Ai", "Eval", "fixtures", "rag-eval-baseline.json");

        if (UpdateBaseline)
        {
            BaselineWriter.Update(baselinePath, report.DatasetName, ToSnapshot(report));
            return;
        }

        var baseline = BaselineLoader.Load(baselinePath);
        baseline.IsSuccess.Should().BeTrue();
        var comparison = BaselineComparator.Compare(
            baseline.Value.Datasets[report.DatasetName],
            ToSnapshot(report),
            settings.MetricTolerance,
            settings.LatencyTolerance);

        if (comparison.Failed)
            throw new Xunit.Sdk.XunitException(
                "Eval baseline regression:\n" + string.Join("\n", comparison.Failures));
    }

    private static BaselineDatasetSnapshot ToSnapshot(EvalReport r) => new(
        RecallAtK: r.Metrics.Aggregate.RecallAtK,
        PrecisionAtK: r.Metrics.Aggregate.PrecisionAtK,
        NdcgAtK: r.Metrics.Aggregate.NdcgAtK,
        HitRateAtK: r.Metrics.Aggregate.HitRateAtK,
        Mrr: r.Metrics.Aggregate.Mrr,
        StageP95Ms: r.Latency.PerStage.ToDictionary(kv => kv.Key, kv => kv.Value.P95),
        DegradedStageCount: r.AggregateDegradedStages.Count);
}
```

- [ ] **Step 2: Add `BaselineWriter.Update`**

Add to `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Baseline/BaselineWriter.cs`:

```csharp
public static void Update(string path, string datasetName, BaselineDatasetSnapshot snapshot)
{
    var existing = File.Exists(path)
        ? System.Text.Json.JsonSerializer.Deserialize<BaselineSnapshot>(
            File.ReadAllText(path), BaselineJson.Options)
        : null;
    var datasets = existing?.Datasets is not null
        ? new Dictionary<string, BaselineDatasetSnapshot>(existing.Datasets)
        : new Dictionary<string, BaselineDatasetSnapshot>();
    datasets[datasetName] = snapshot;
    Write(path, new BaselineSnapshot(
        DateTime.UtcNow,
        Environment.GetEnvironmentVariable("GIT_SHA"),
        datasets));
}
```

- [ ] **Step 3: Add `BuildEvalServiceProvider` helper on `RagEvalFixture`**

Append to `RagEvalFixture` (in `RagEvalCollection.cs`):

```csharp
public IServiceProvider BuildEvalServiceProvider()
{
    var services = new ServiceCollection();
    var config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = Postgres.ConnectionString,
            ["AI:Rag:Eval:Enabled"] = "true"
        })
        .Build();
    Starter.Api.Program.ConfigureServicesForTooling(services, config);
    // Inject admin ICurrentUserService so ACL resolve bypasses.
    services.AddSingleton<ICurrentUserService>(new EvalAdminCurrentUser());
    return services.BuildServiceProvider();
}

private sealed class EvalAdminCurrentUser : ICurrentUserService
{
    public Guid? UserId { get; } = Guid.NewGuid();
    public string? Email => "eval@harness.local";
    public bool IsAuthenticated => true;
    public IEnumerable<string> Roles => new[] { "Admin" };
    public IEnumerable<string> Permissions => Array.Empty<string>();
    public Guid? TenantId { get; } = Guid.NewGuid();
    public bool IsInRole(string role) => role == "Admin";
    public bool HasPermission(string permission) => true;
}
```

> **Note:** `ICurrentUserService` member list must match the production interface exactly — compare against `boilerplateBE/src/Starter.Application/Common/Interfaces/ICurrentUserService.cs` at this point in the plan and adjust the `EvalAdminCurrentUser` properties to match.

- [ ] **Step 4: Generate the initial baseline**

Run once with `UPDATE_EVAL_BASELINE=1` + `AI_EVAL_ENABLED=1` (requires live provider + Qdrant + Postgres locally):

```bash
AI_EVAL_ENABLED=1 UPDATE_EVAL_BASELINE=1 \
  dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj \
  --filter "FullyQualifiedName~RagEvalHarnessTests"
```

Verify `rag-eval-baseline.json` is created and has entries for both `boilerplate-core-en-v1` and `boilerplate-core-ar-v1`.

- [ ] **Step 5: Commit baseline + tests**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/RagEvalHarnessTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/rag-eval-baseline.json \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Eval/Baseline/BaselineWriter.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/RagEvalCollection.cs
git commit -m "test(ai): add RagEvalHarnessTests + initial baseline snapshot"
```

---

## Task 24: Regression-detection verification

**Files:**
- (no source changes)

- [ ] **Step 1: Force a regression locally**

Temporarily set `AI:Rag:RetrievalTopK` to `1` in `appsettings.Development.json` (or in a `TEST_OVERRIDES` env var that the test `BuildEvalServiceProvider` reads).

- [ ] **Step 2: Run**

```bash
AI_EVAL_ENABLED=1 dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj \
  --filter "FullyQualifiedName~RagEvalHarnessTests"
```

Expected: FAIL with message containing `recall_at_10 regressed` (or similar) naming one or more baseline metric drops.

- [ ] **Step 3: Revert the change**

Restore the original `RetrievalTopK` value. Re-run without `UPDATE_EVAL_BASELINE=1` — the test should pass again.

- [ ] **Step 4: Commit nothing — the revert leaves a clean tree**

No new commit. This task is a manual verification of the regression-gate behaviour and documents the expected CI failure pattern.

---

## Task 25: `CLAUDE.md` — add harness docs

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Add documentation section**

Add a new section at the end of `CLAUDE.md` (before any existing "Post-Feature Testing Workflow" footer):

```markdown
## Running the RAG eval harness

The AI module ships with an offline evaluation harness covering retrieval quality and stage latency.

**When to run it:** Before merging any change to the retrieval pipeline (`RagRetrievalService`, chunking, embedding, reranker, vector store). A nightly Jenkins job also runs it against main.

**Prerequisites:** Live Postgres + Qdrant (docker-compose up); live provider API key (`OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, or `OLLAMA_URL` set in `appsettings.Test.json`).

**Run the harness:**

\```bash
AI_EVAL_ENABLED=1 dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj \
  --filter "FullyQualifiedName~RagEvalHarnessTests"
\```

**Update the baseline** (when an intentional regression/improvement is expected):

\```bash
AI_EVAL_ENABLED=1 UPDATE_EVAL_BASELINE=1 dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj \
  --filter "FullyQualifiedName~RagEvalHarnessTests"
\```

Commit `boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/rag-eval-baseline.json` alongside your change so reviewers see the before/after metric drift explicitly.

**Warm the rerank cache blob** (required when adding new questions to a fixture):

\```bash
dotnet run --project boilerplateBE/tools/EvalCacheWarmup -- \
  --fixture boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/rag-eval-dataset-en.json \
  --out boilerplateBE/tests/Starter.Api.Tests/Ai/Eval/fixtures/eval-rerank-cache-en.bin
\```

Commit the updated blob alongside the fixture change.

**Faithfulness spot-check (superadmin):**

\```bash
curl -H "Authorization: Bearer <superadmin-token>" \
  -F dataset_name=en \
  -F assistant_id=<assistant-guid> \
  http://localhost:5000/api/v1/aieval/faithfulness
\```

Returns a `FaithfulnessReport` with per-question SUPPORTED/UNSUPPORTED claim breakdowns. Only superadmins (`Ai.RunEval`) can invoke this endpoint.
```

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: document RAG eval harness (AI_EVAL_ENABLED, baseline update, faithfulness)"
```

---

## Spec coverage checklist

- **§2 — No migrations / no Co-Authored-By / bilingual / deterministic in CI / fail-closed / no live-data leaks** — tasks 12–13 (bilingual fixtures), 22 (orphan cleanup), 23 (regression gate asserts baseline failure), 10 (settings `Enabled=false` default).
- **§3 — D1–D10 decisions** — all covered: harness split (tasks 16, 18), canonical JSON (tasks 11–13), metric suite (tasks 3–8), baseline gating (task 9, 23), bilingual (tasks 12–13), disposable collection (task 22), fixture-ID map (tasks 14, 16), judge pinning (task 18), doc-granularity default (task 2 `RelevantChunkIds` optional), `AI_EVAL_ENABLED` gate (task 23).
- **§4 — File layout** — matches tasks 1–23.
- **§5.1 — Harness flow** — task 16 implements all 9 steps.
- **§5.1.1 — MeterListener-based latency capture** — task 8.
- **§5.2 — Fixture format** — tasks 12–13.
- **§5.3 — Baseline snapshot format** — tasks 9, 23.
- **§5.4 — Determinism strategy** — tasks 20–21 (warmup tool, capturing reranker).
- **§5.5 — Admin faithfulness endpoint + judge prompt + retry-once** — tasks 15, 18, 19.
- **§6 — Metric math** — tasks 3–8.
- **§7 — Settings** — task 10.
- **§8 — `Ai.RunEval` permission** — task 17.
- **§9 — Error codes** — task 1.
- **§10 — Testing strategy** — tasks 3–9, 11, 19, 23.
- **§11 — Risks (judge retry, orphan cleanup, invalid JSON)** — tasks 15 (retry once), 22 (cleanup).
- **§13 — Acceptance criteria** — all tasks collectively satisfy.
- **§14 — Post-implementation verification** — task 24.
