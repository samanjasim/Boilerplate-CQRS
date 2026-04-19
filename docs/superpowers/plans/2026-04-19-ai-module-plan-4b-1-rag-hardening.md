# Plan 4b-1 — RAG Hardening + Arabic Foundations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fragile min-max hybrid fusion with RRF; add a Redis-backed embedding cache; add per-stage timeouts with degraded-stage telemetry; prevent orphan Qdrant vectors and wasted re-processing via SHA-256 fingerprinting; and make keyword retrieval work correctly for Arabic content via a pre-normalized `NormalizedContent` column + `simple` FTS config.

**Architecture:** All changes internal to `Starter.Module.AI`. No public API changes — `RagRetrievalService`'s contract is preserved (except `RetrievedContext` gains a `DegradedStages` list). `AiDocument` gains `ContentHash`; `AiDocumentChunk` gains `NormalizedContent` and the `content_tsv` computed column is rebuilt over it. No migrations are committed — consuming apps regenerate their own.

**Tech Stack:** .NET 10, EF Core + Postgres (tsvector / `pg_trgm`), Qdrant, MassTransit + RabbitMQ, xUnit + FluentAssertions, `ICacheService` (Redis).

**Standing orders (do not violate):**
- Never add `Co-Authored-By` or mention Claude in any commit message.
- Never commit EF Core migrations; consuming apps regenerate theirs.
- Keep commits small, one per task.

**Repo layout:**
- Source module: `boilerplateBE/src/modules/Starter.Module.AI/`
- Tests: `boilerplateBE/tests/Starter.Api.Tests/Ai/`
- Test app for E2E: `_testAiRag2/_testAiRag2-BE/` (ports 5102/3102, already running)

---

## Task 1: Settings delta + `DegradedStages` on `RetrievedContext`

Foundational change with zero behavior impact. Adds the knobs every other task will consume.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RetrievedContext.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs` (construct with `DegradedStages: []`)
- Modify: `boilerplateBE/src/Starter.Api/appsettings.json` and `boilerplateBE/src/Starter.Api/appsettings.Development.json`
- Modify (failing test first): `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RagRetrievalServiceTests.cs`

- [ ] **Step 1: Failing test — RetrievedContext has DegradedStages field**

Add the test at the top of the existing test class body (`RagRetrievalServiceTests`), after the private helpers:

```csharp
[Fact]
public void RetrievedContext_Empty_HasEmptyDegradedStages()
{
    RetrievedContext.Empty.DegradedStages.Should().NotBeNull().And.BeEmpty();
}
```

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RetrievedContext_Empty_HasEmptyDegradedStages"`
Expected: FAIL with "does not contain a definition for 'DegradedStages'"

- [ ] **Step 3: Extend `RetrievedContext`**

Replace the full file content at `Application/Services/Retrieval/RetrievedContext.cs`:

```csharp
namespace Starter.Module.AI.Application.Services.Retrieval;

public sealed record RetrievedContext(
    IReadOnlyList<RetrievedChunk> Children,
    IReadOnlyList<RetrievedChunk> Parents,
    int TotalTokens,
    bool TruncatedByBudget,
    IReadOnlyList<string> DegradedStages)
{
    public static RetrievedContext Empty { get; } = new([], [], 0, false, []);
    public bool IsEmpty => Children.Count == 0;
}
```

- [ ] **Step 4: Update `RagRetrievalService` construction**

In `Infrastructure/Retrieval/RagRetrievalService.cs`, find every `new RetrievedContext(...)` call and every `return RetrievedContext.Empty`. The constructor now requires 5 args. For this task only, pass `DegradedStages: []` everywhere — per-stage wiring lands in Task 7.

Locations to update in `RagRetrievalService.cs`:
- The `new RetrievedContext(trimmedChildren, trimmedParents, totalTokens, truncated)` near the end of `RetrieveForQueryAsync`.

Change to:
```csharp
return new RetrievedContext(trimmedChildren, trimmedParents, totalTokens, truncated, []);
```

- [ ] **Step 5: Update `AiRagSettings`**

Replace the full file content at `Infrastructure/Settings/AiRagSettings.cs`:

```csharp
namespace Starter.Module.AI.Infrastructure.Settings;

public sealed class AiRagSettings
{
    public const string SectionName = "AI:Rag";

    // From Plan 4a
    public int ChunkSize { get; init; } = 512;
    public int ChunkOverlap { get; init; } = 50;
    public int ParentChunkSize { get; init; } = 1536;
    public int TopK { get; init; } = 5;
    public int RetrievalTopK { get; init; } = 20;
    public bool EnableQueryExpansion { get; init; } = false;  // 4b-2
    public bool EnableReranking { get; init; } = false;       // 4b-2
    public int EmbedBatchSize { get; init; } = 32;
    public long MaxUploadBytes { get; init; } = 25 * 1024 * 1024;
    public double OcrFallbackMinCharsPerPage { get; init; } = 40;
    public double PageFailureThreshold { get; init; } = 0.25;

    // From Plan 4b
    public int MaxContextTokens { get; init; } = 4000;
    public bool IncludeParentContext { get; init; } = true;
    public decimal MinHybridScore { get; init; } = 0.0m;

    // New in Plan 4b-1 — RRF fusion (replaces HybridSearchWeight)
    public decimal VectorWeight { get; init; } = 1.0m;
    public decimal KeywordWeight { get; init; } = 1.0m;
    public int RrfK { get; init; } = 60;

    // New in Plan 4b-1 — embedding cache
    public int EmbeddingCacheTtlSeconds { get; init; } = 3600;

    // New in Plan 4b-1 — per-stage timeouts
    public int StageTimeoutEmbedMs { get; init; } = 5_000;
    public int StageTimeoutVectorMs { get; init; } = 5_000;
    public int StageTimeoutKeywordMs { get; init; } = 3_000;

    // New in Plan 4b-1 — Arabic / FTS language
    public string FtsLanguage { get; init; } = "simple";
    public bool ApplyArabicNormalization { get; init; } = true;
    public bool NormalizeTaMarbuta { get; init; } = true;
    public bool NormalizeArabicDigits { get; init; } = true;
}
```

Note: `HybridSearchWeight` is removed. `FtsLanguage` default changes from `"english"` to `"simple"`.

- [ ] **Step 6: Update appsettings**

In both `appsettings.json` and `appsettings.Development.json`, under the `AI:Rag` section:
- Remove the `HybridSearchWeight` key if present.
- Add `"VectorWeight": 1.0, "KeywordWeight": 1.0, "RrfK": 60`.
- Add `"EmbeddingCacheTtlSeconds": 3600`.
- Add `"StageTimeoutEmbedMs": 5000, "StageTimeoutVectorMs": 5000, "StageTimeoutKeywordMs": 3000`.
- Change `FtsLanguage` (if present) to `"simple"`, else add it.
- Add `"ApplyArabicNormalization": true, "NormalizeTaMarbuta": true, "NormalizeArabicDigits": true`.

If `appsettings*.json` doesn't currently surface the `AI:Rag` section, leave it alone — `AiRagSettings` defaults are used.

- [ ] **Step 7: Verify test passes and full suite still builds**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: 0 errors.

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~Retrieval"`
Expected: all existing retrieval tests PASS plus the new one. (Any test that constructs `RetrievedContext` directly must be updated — adjust if any fail.)

- [ ] **Step 8: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RetrievedContext.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs \
        boilerplateBE/src/Starter.Api/appsettings.json \
        boilerplateBE/src/Starter.Api/appsettings.Development.json \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RagRetrievalServiceTests.cs
git commit -m "feat(ai): add 4b-1 settings + RetrievedContext.DegradedStages

Foundation commit for the RAG hardening pass. Adds RRF knobs
(VectorWeight/KeywordWeight/RrfK), embedding cache TTL, per-stage timeout
budgets, and Arabic normalization toggles. Removes HybridSearchWeight
(replaced by RRF in Task 2). Changes default FTS language to 'simple'
(pre-normalized text is indexed in Task 5)."
```

---

## Task 2: Reciprocal Rank Fusion in `HybridScoreCalculator` + unit tests

Replace min-max normalization with RRF. Signature changes to accept multi-list input so 4b-2 query rewriting drops in cleanly.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/HybridScoreCalculator.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs` (caller)
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/HybridScoreCalculatorTests.cs`

- [ ] **Step 1: Write failing tests**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/HybridScoreCalculatorTests.cs`:

```csharp
using FluentAssertions;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class HybridScoreCalculatorTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000A");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000B");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-00000000000C");
    private static readonly Guid D = Guid.Parse("00000000-0000-0000-0000-00000000000D");

    private static IReadOnlyList<IReadOnlyList<VectorSearchHit>> Vec(params (Guid id, decimal score)[][] lists)
        => lists.Select(l => (IReadOnlyList<VectorSearchHit>)l.Select(t => new VectorSearchHit(t.id, t.score)).ToList()).ToList();

    private static IReadOnlyList<IReadOnlyList<KeywordSearchHit>> Kw(params (Guid id, decimal score)[][] lists)
        => lists.Select(l => (IReadOnlyList<KeywordSearchHit>)l.Select(t => new KeywordSearchHit(t.id, t.score)).ToList()).ToList();

    [Fact]
    public void Combine_SingleVector_SingleKeyword_DifferentIds_BothReturned()
    {
        var result = HybridScoreCalculator.Combine(
            Vec(new[] { (A, 0.9m) }),
            Kw(new[] { (B, 0.5m) }),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 0m);

        result.Should().HaveCount(2);
        result.Select(h => h.ChunkId).Should().BeEquivalentTo(new[] { A, B });
    }

    [Fact]
    public void Combine_SameId_InBothLists_ScoresSum()
    {
        var result = HybridScoreCalculator.Combine(
            Vec(new[] { (A, 0.9m), (B, 0.5m) }),
            Kw(new[] { (A, 0.7m), (B, 0.4m) }),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 0m);

        // A appears rank-0 in both lists; B rank-1 in both. A's RRF > B's RRF.
        result.Should().HaveCount(2);
        result[0].ChunkId.Should().Be(A);
        result[1].ChunkId.Should().Be(B);
        result[0].HybridScore.Should().BeGreaterThan(result[1].HybridScore);
    }

    [Fact]
    public void Combine_EmptyKeyword_ReturnsVectorListOrdered()
    {
        var result = HybridScoreCalculator.Combine(
            Vec(new[] { (A, 0.9m), (B, 0.5m) }),
            Kw(Array.Empty<(Guid, decimal)>()),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 0m);

        result.Select(h => h.ChunkId).Should().ContainInOrder(A, B);
    }

    [Fact]
    public void Combine_EmptyVector_ReturnsKeywordListOrdered()
    {
        var result = HybridScoreCalculator.Combine(
            Vec(Array.Empty<(Guid, decimal)>()),
            Kw(new[] { (B, 0.5m), (A, 0.3m) }),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 0m);

        result.Select(h => h.ChunkId).Should().ContainInOrder(B, A);
    }

    [Fact]
    public void Combine_AllEmpty_ReturnsEmpty()
    {
        var result = HybridScoreCalculator.Combine(
            Vec(Array.Empty<(Guid, decimal)>()),
            Kw(Array.Empty<(Guid, decimal)>()),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 0m);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Combine_MultipleVectorLists_RanksAccumulate()
    {
        // A is rank-0 in both variant lists; B only in list #1 at rank-1.
        // A should win because it gets two high contributions.
        var result = HybridScoreCalculator.Combine(
            Vec(
                new[] { (A, 0.9m), (B, 0.8m) },
                new[] { (A, 0.9m), (C, 0.7m) }),
            Kw(Array.Empty<(Guid, decimal)>()),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 0m);

        result[0].ChunkId.Should().Be(A);
    }

    [Fact]
    public void Combine_MinScoreFilter_DropsLowScores()
    {
        // With only one hit, A's RRF = 1/61. Set a min above that.
        var result = HybridScoreCalculator.Combine(
            Vec(new[] { (A, 0.9m) }),
            Kw(Array.Empty<(Guid, decimal)>()),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 1m);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Combine_TieBreakByChunkId_IsDeterministic()
    {
        // A and B both appear at rank-0 in one of two symmetric lists → equal scores → tiebreak on id.
        var result = HybridScoreCalculator.Combine(
            Vec(
                new[] { (A, 0.9m) },
                new[] { (B, 0.9m) }),
            Kw(Array.Empty<(Guid, decimal)>()),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 0m);

        result[0].ChunkId.Should().Be(A);  // A < B by Guid ordering
        result[1].ChunkId.Should().Be(B);
    }

    [Fact]
    public void Combine_SemanticScore_ReportsMaxRawAcrossLists()
    {
        var result = HybridScoreCalculator.Combine(
            Vec(
                new[] { (A, 0.7m) },
                new[] { (A, 0.95m) }),
            Kw(Array.Empty<(Guid, decimal)>()),
            vectorWeight: 1m, keywordWeight: 1m, rrfK: 60, minScore: 0m);

        result[0].SemanticScore.Should().Be(0.95m);
    }

    [Fact]
    public void Combine_KeywordWeightZero_VectorOnly()
    {
        // B scores higher on keyword; A scores on vector only. With keywordWeight=0, A must win.
        var result = HybridScoreCalculator.Combine(
            Vec(new[] { (A, 0.9m) }),
            Kw(new[] { (B, 0.9m) }),
            vectorWeight: 1m, keywordWeight: 0m, rrfK: 60, minScore: 0m);

        result[0].ChunkId.Should().Be(A);
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~HybridScoreCalculatorTests"`
Expected: FAIL — the `Combine` signature doesn't match.

- [ ] **Step 3: Rewrite `HybridScoreCalculator`**

Replace the full file content at `Infrastructure/Retrieval/HybridScoreCalculator.cs`:

```csharp
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval;

public sealed record HybridHit(Guid ChunkId, decimal SemanticScore, decimal KeywordScore, decimal HybridScore);

internal static class HybridScoreCalculator
{
    public static IReadOnlyList<HybridHit> Combine(
        IReadOnlyList<IReadOnlyList<VectorSearchHit>> semanticLists,
        IReadOnlyList<IReadOnlyList<KeywordSearchHit>> keywordLists,
        decimal vectorWeight,
        decimal keywordWeight,
        int rrfK,
        decimal minScore)
    {
        var scores = new Dictionary<Guid, decimal>();
        var maxSem = new Dictionary<Guid, decimal>();
        var maxKw = new Dictionary<Guid, decimal>();

        foreach (var list in semanticLists)
        {
            for (var rank = 0; rank < list.Count; rank++)
            {
                var hit = list[rank];
                var contribution = vectorWeight / (rrfK + rank + 1);
                scores[hit.ChunkId] = scores.GetValueOrDefault(hit.ChunkId) + contribution;
                if (!maxSem.TryGetValue(hit.ChunkId, out var existing) || hit.Score > existing)
                    maxSem[hit.ChunkId] = hit.Score;
            }
        }

        foreach (var list in keywordLists)
        {
            for (var rank = 0; rank < list.Count; rank++)
            {
                var hit = list[rank];
                var contribution = keywordWeight / (rrfK + rank + 1);
                scores[hit.ChunkId] = scores.GetValueOrDefault(hit.ChunkId) + contribution;
                if (!maxKw.TryGetValue(hit.ChunkId, out var existing) || hit.Score > existing)
                    maxKw[hit.ChunkId] = hit.Score;
            }
        }

        return scores
            .Where(kv => kv.Value >= minScore)
            .Select(kv => new HybridHit(
                kv.Key,
                maxSem.GetValueOrDefault(kv.Key),
                maxKw.GetValueOrDefault(kv.Key),
                kv.Value))
            .OrderByDescending(h => h.HybridScore)
            .ThenBy(h => h.ChunkId)
            .ToList();
    }
}
```

- [ ] **Step 4: Update caller in `RagRetrievalService`**

In `Infrastructure/Retrieval/RagRetrievalService.cs`, find the block that builds `mergedHits`. It currently reads (around line 78-85):

```csharp
var alpha = (decimal)_settings.HybridSearchWeight;
var minHybrid = minScore ?? _settings.MinHybridScore;

var vectorHits = await _vectorStore.SearchAsync(tenantId, queryVector, documentFilter, retrievalTopK, ct);
var keywordHits = await _keywordSearch.SearchAsync(tenantId, queryText, documentFilter, retrievalTopK, ct);

var mergedHits = HybridScoreCalculator.Combine(vectorHits, keywordHits, alpha, minHybrid);
```

Replace with:

```csharp
var minHybrid = minScore ?? _settings.MinHybridScore;

var vectorHits = await _vectorStore.SearchAsync(tenantId, queryVector, documentFilter, retrievalTopK, ct);
var keywordHits = await _keywordSearch.SearchAsync(tenantId, queryText, documentFilter, retrievalTopK, ct);

var mergedHits = HybridScoreCalculator.Combine(
    [vectorHits],
    [keywordHits],
    _settings.VectorWeight,
    _settings.KeywordWeight,
    _settings.RrfK,
    minHybrid);
```

Remove the unused `alpha` variable. The `retrievalTopK` line above stays.

- [ ] **Step 5: Run tests**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~HybridScoreCalculatorTests"`
Expected: all 10 tests PASS.

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RagRetrievalService"`
Expected: existing tests PASS. If any fail because they assumed min-max semantics (e.g., a specific score ordering), adjust the test — RRF produces different absolute scores but the top-K order for the small test fixtures should hold.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/HybridScoreCalculator.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/HybridScoreCalculatorTests.cs
git commit -m "feat(ai): replace min-max hybrid fusion with Reciprocal Rank Fusion

RRF is scale-free, robust when one hit list is empty/tiny, and accepts
multi-list input so Plan 4b-2 query rewriting drops in cleanly. The new
Combine takes lists-of-lists for both sides, weights them with
VectorWeight/KeywordWeight, and fuses via weight/(k+rank+1)."
```

---

## Task 3: `ArabicTextNormalizer` utility + unit tests

Pure function; no DI, no DB. Shared between indexing path (Task 5) and query path (Task 5).

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/ArabicTextNormalizer.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ArabicTextNormalizerTests.cs`

- [ ] **Step 1: Failing tests**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ArabicTextNormalizerTests.cs`:

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class ArabicTextNormalizerTests
{
    private static readonly ArabicNormalizationOptions DefaultOpts = new(
        NormalizeTaMarbuta: true, NormalizeArabicDigits: true);

    [Fact]
    public void StripsDiacritics_KeepsBaseLetters()
    {
        var input = "مُؤَسَّسَة";   // with harakat
        var output = ArabicTextNormalizer.Normalize(input, DefaultOpts);
        output.Should().Be("مؤسسه");  // diacritics removed, ta-marbuta → ha
    }

    [Fact]
    public void NormalizesAlefVariants_ToBareAlef()
    {
        ArabicTextNormalizer.Normalize("أحمد", DefaultOpts).Should().Be("احمد");
        ArabicTextNormalizer.Normalize("إيمان", DefaultOpts).Should().Be("ايمان");
        ArabicTextNormalizer.Normalize("آمنة", DefaultOpts).Should().Be("امنه");
    }

    [Fact]
    public void NormalizesYa_FromAlefMaksura()
    {
        ArabicTextNormalizer.Normalize("على", DefaultOpts).Should().Be("علي");
    }

    [Fact]
    public void NormalizesTaMarbuta_ToHa_WhenEnabled()
    {
        ArabicTextNormalizer.Normalize("مدرسة", DefaultOpts).Should().Be("مدرسه");
    }

    [Fact]
    public void LeavesTaMarbuta_WhenDisabled()
    {
        var opts = DefaultOpts with { NormalizeTaMarbuta = false };
        ArabicTextNormalizer.Normalize("مدرسة", opts).Should().Be("مدرسة");
    }

    [Fact]
    public void StripsTatweel()
    {
        ArabicTextNormalizer.Normalize("ســـــلام", DefaultOpts).Should().Be("سلام");
    }

    [Fact]
    public void LeavesAsciiUnchanged()
    {
        ArabicTextNormalizer.Normalize("Hello World 2024", DefaultOpts).Should().Be("Hello World 2024");
    }

    [Fact]
    public void NormalizesMixedArabicEnglish_TouchesOnlyArabicRanges()
    {
        ArabicTextNormalizer.Normalize("مرحبا Hello مُدرسة", DefaultOpts).Should().Be("مرحبا Hello مدرسه");
    }

    [Fact]
    public void NormalizesArabicIndicDigits_WhenEnabled()
    {
        ArabicTextNormalizer.Normalize("سنة ٢٠٢٥", DefaultOpts).Should().Be("سنه 2025");
    }

    [Fact]
    public void LeavesArabicDigits_WhenDisabled()
    {
        var opts = DefaultOpts with { NormalizeArabicDigits = false };
        ArabicTextNormalizer.Normalize("٢٠٢٥", opts).Should().Be("٢٠٢٥");
    }

    [Fact]
    public void NormalizesHamzaOnYaAndWaw()
    {
        ArabicTextNormalizer.Normalize("سؤال", DefaultOpts).Should().Be("سوال");
        ArabicTextNormalizer.Normalize("شيء", DefaultOpts).Should().Be("شيء"); // hamza-on-bare stays (it's \u0621)
        ArabicTextNormalizer.Normalize("رئيس", DefaultOpts).Should().Be("رييس");
    }

    [Fact]
    public void CollapsesWhitespaceAndTrims()
    {
        ArabicTextNormalizer.Normalize("  مرحبا   بك  ", DefaultOpts).Should().Be("مرحبا بك");
    }

    [Fact]
    public void EmptyString_ReturnsEmpty()
    {
        ArabicTextNormalizer.Normalize("", DefaultOpts).Should().Be("");
    }

    [Fact]
    public void NullInput_ReturnsEmpty()
    {
        ArabicTextNormalizer.Normalize(null!, DefaultOpts).Should().Be("");
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~ArabicTextNormalizerTests"`
Expected: FAIL — class doesn't exist.

- [ ] **Step 3: Implement `ArabicTextNormalizer`**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/ArabicTextNormalizer.cs`:

```csharp
using System.Text;

namespace Starter.Module.AI.Infrastructure.Retrieval;

public readonly record struct ArabicNormalizationOptions(
    bool NormalizeTaMarbuta,
    bool NormalizeArabicDigits);

public static class ArabicTextNormalizer
{
    public static string Normalize(string input, ArabicNormalizationOptions options)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var sb = new StringBuilder(input.Length);
        var lastWasWhitespace = false;

        foreach (var ch in input)
        {
            // Diacritics / harakat (including tatweel) — strip.
            if (IsArabicDiacritic(ch) || ch == '\u0640') continue;

            char mapped = ch;

            // Alef variants → bare alef
            if (ch is '\u0623' or '\u0625' or '\u0622' or '\u0671') mapped = '\u0627';
            // Alef maksura → ya
            else if (ch == '\u0649') mapped = '\u064A';
            // Ta marbuta → ha (gated)
            else if (ch == '\u0629' && options.NormalizeTaMarbuta) mapped = '\u0647';
            // Hamza on ya → ya; hamza on waw → waw
            else if (ch == '\u0626') mapped = '\u064A';
            else if (ch == '\u0624') mapped = '\u0648';
            // Arabic-Indic digits → ASCII (gated)
            else if (options.NormalizeArabicDigits)
            {
                if (ch is >= '\u0660' and <= '\u0669')
                    mapped = (char)('0' + (ch - '\u0660'));
                else if (ch is >= '\u06F0' and <= '\u06F9')
                    mapped = (char)('0' + (ch - '\u06F0'));
            }

            // Collapse whitespace runs.
            if (char.IsWhiteSpace(mapped))
            {
                if (!lastWasWhitespace && sb.Length > 0) sb.Append(' ');
                lastWasWhitespace = true;
            }
            else
            {
                sb.Append(mapped);
                lastWasWhitespace = false;
            }
        }

        // Trim trailing space from collapse.
        if (sb.Length > 0 && sb[^1] == ' ') sb.Length--;
        return sb.ToString();
    }

    private static bool IsArabicDiacritic(char ch) =>
        ch is >= '\u064B' and <= '\u065F'   // harakat + tanween + shadda + sukun + quranic marks
        || ch == '\u0670'                    // superscript alef
        || ch is >= '\u0610' and <= '\u061A';
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~ArabicTextNormalizerTests"`
Expected: all PASS.

Note on `NormalizesHamzaOnYaAndWaw`: the third assertion (`شيء` → `شيء`) — hamza on bare line `\u0621` is not one of our mapped code points, so it passes through untouched. Confirm this in the output.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/ArabicTextNormalizer.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ArabicTextNormalizerTests.cs
git commit -m "feat(ai): add ArabicTextNormalizer utility

Shared normalizer for Arabic FTS indexing and query paths. Strips
diacritics and tatweel, normalizes alef/ya/ta-marbuta/hamza variants,
optionally maps Arabic-Indic digits to ASCII. ASCII input passes
through unchanged; touches only the U+0600-U+06FF range."
```

---

## Task 4: Schema — `AiDocument.ContentHash` + `AiDocumentChunk.NormalizedContent` + rebuild `content_tsv`

Schema changes only; behavior change lands in Task 5/8.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiDocument.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiDocumentChunk.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiDocumentConfiguration.cs` (find via Glob)
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiDocumentChunkConfiguration.cs`

- [ ] **Step 1: Read entity + config files**

Read:
- `Domain/Entities/AiDocument.cs` — note the constructor signature and public `Create`.
- `Domain/Entities/AiDocumentChunk.cs` — note `Create` signature.
- `Infrastructure/Configurations/AiDocumentConfiguration.cs` and `AiDocumentChunkConfiguration.cs` — note the HasColumnName patterns.

- [ ] **Step 2: Extend `AiDocument`**

Add a `string? ContentHash { get; private set; }` property and a method:

```csharp
public void SetContentHash(string hash)
{
    if (string.IsNullOrWhiteSpace(hash) || hash.Length != 64)
        throw new ArgumentException("ContentHash must be a 64-char hex SHA-256 string.", nameof(hash));
    ContentHash = hash.ToLowerInvariant();
    ModifiedAt = DateTime.UtcNow;
}
```

Do NOT change the `Create` factory — `ContentHash` is populated after upload succeeds.

- [ ] **Step 3: Extend `AiDocumentChunk`**

Add a `string? NormalizedContent { get; private set; }` property and a method:

```csharp
public void SetNormalizedContent(string normalized)
{
    NormalizedContent = normalized ?? string.Empty;
    ModifiedAt = DateTime.UtcNow;
}
```

Plus an overload on `Create` that accepts an optional `string? normalizedContent = null` parameter and sets it on the returned instance — or add a convenience init path. Prefer keeping `Create` signature unchanged and letting the consumer call `SetNormalizedContent` after.

- [ ] **Step 4: Update `AiDocumentConfiguration`**

Add inside `Configure`:

```csharp
builder.Property(e => e.ContentHash)
    .HasColumnName("content_hash")
    .HasMaxLength(64);

builder.HasIndex(e => new { e.TenantId, e.ContentHash })
    .HasDatabaseName("ix_ai_documents_tenant_content_hash");
```

- [ ] **Step 5: Update `AiDocumentChunkConfiguration`**

Before the `if (IsRelationalProvider(...))` block:

```csharp
builder.Property(e => e.NormalizedContent)
    .HasColumnName("normalized_content");
```

Inside the `if (IsRelationalProvider(...))` block, replace the existing `content_tsv` definition with:

```csharp
builder.Property<NpgsqlTsVector>("ContentTsVector")
    .HasColumnName("content_tsv")
    .HasComputedColumnSql(
        "to_tsvector('simple', coalesce(normalized_content, content))",
        stored: true);
```

- [ ] **Step 6: Verify build**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: 0 errors.

Run the existing test suite to confirm no regressions from the config change:
Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj`
Expected: existing AiPostgres-integration tests may fail if they run against a DB without the column — in that case, note the failure but proceed; Task 5 populates `NormalizedContent` and the test fixture re-creates the DB schema on fresh spin-up.

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiDocument.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiDocumentChunk.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiDocumentConfiguration.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiDocumentChunkConfiguration.cs
git commit -m "feat(ai): add ContentHash + NormalizedContent columns for 4b-1

AiDocument.ContentHash (SHA-256 hex) enables fingerprint-based reindex
skip in Task 8. AiDocumentChunk.NormalizedContent holds Arabic-normalized
text, indexed by the regenerated content_tsv generated column using the
Postgres 'simple' FTS config. Existing rows keep NormalizedContent null
and the COALESCE in the computed column falls back to content."
```

---

## Task 5: Arabic FTS wiring — normalize on index, normalize on query, integration tests

Wires `ArabicTextNormalizer` into both the ingestion path (populate `NormalizedContent`) and the retrieval path (normalize the query text before `plainto_tsquery`). Adds Arabic FTS tests.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Consumers/ProcessDocumentConsumer.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/PostgresKeywordSearchService.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/PostgresKeywordSearchServiceTests.cs`

- [ ] **Step 1: Write failing integration tests (Arabic)**

Append to `PostgresKeywordSearchServiceTests.cs` (inside the existing `PostgresKeywordSearchServiceTests` class):

```csharp
[Fact]
public async Task Arabic_Query_Matches_Chunk_With_Different_Alef_Spelling()
{
    var tenant = Guid.NewGuid();
    var uploader = Guid.NewGuid();

    await using var db = _fixture.CreateDbContext();

    var doc = AiDocument.Create(tenant, "Doc AR", "doc-ar.pdf", "ref-ar", "application/pdf", 512, uploader);
    db.AiDocuments.Add(doc);

    // Chunk body uses أ; query will use ا. After normalization both become ا.
    var chunk = AiDocumentChunk.Create(doc.Id, "child", "أكاديمي العلوم", 0, 5, Guid.NewGuid());
    chunk.SetNormalizedContent(Starter.Module.AI.Infrastructure.Retrieval.ArabicTextNormalizer.Normalize(
        chunk.Content,
        new Starter.Module.AI.Infrastructure.Retrieval.ArabicNormalizationOptions(true, true)));
    db.AiDocumentChunks.Add(chunk);
    await db.SaveChangesAsync();

    var svc = new PostgresKeywordSearchService(db, _fixture.Logger<PostgresKeywordSearchService>());
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

    var doc = AiDocument.Create(tenant, "Doc AR2", "doc-ar2.pdf", "ref-ar2", "application/pdf", 512, uploader);
    db.AiDocuments.Add(doc);

    var chunk = AiDocumentChunk.Create(doc.Id, "child", "مُؤَسَّسَة تعليمية", 0, 5, Guid.NewGuid());
    chunk.SetNormalizedContent(Starter.Module.AI.Infrastructure.Retrieval.ArabicTextNormalizer.Normalize(
        chunk.Content,
        new Starter.Module.AI.Infrastructure.Retrieval.ArabicNormalizationOptions(true, true)));
    db.AiDocumentChunks.Add(chunk);
    await db.SaveChangesAsync();

    var svc = new PostgresKeywordSearchService(db, _fixture.Logger<PostgresKeywordSearchService>());
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

    var doc = AiDocument.Create(tenant, "Doc Mixed", "mix.pdf", "ref-mix", "application/pdf", 512, uploader);
    db.AiDocuments.Add(doc);

    var chunk = AiDocumentChunk.Create(doc.Id, "child", "photosynthesis التمثيل الضوئي", 0, 5, Guid.NewGuid());
    chunk.SetNormalizedContent(Starter.Module.AI.Infrastructure.Retrieval.ArabicTextNormalizer.Normalize(
        chunk.Content,
        new Starter.Module.AI.Infrastructure.Retrieval.ArabicNormalizationOptions(true, true)));
    db.AiDocumentChunks.Add(chunk);
    await db.SaveChangesAsync();

    var svc = new PostgresKeywordSearchService(db, _fixture.Logger<PostgresKeywordSearchService>());
    var results = await svc.SearchAsync(tenant, "photosynthesis", null, 10, CancellationToken.None);

    results.Should().HaveCount(1);
    results[0].ChunkId.Should().Be(chunk.QdrantPointId);
}
```

- [ ] **Step 2: Run integration tests to confirm they fail**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~PostgresKeywordSearchServiceTests&FullyQualifiedName~Arabic"`
Expected: tests run but `Arabic_Query_Matches_Through_Diacritics` fails because the keyword service still uses raw query text without normalization (even though `NormalizedContent` is indexed, the query isn't normalized).

Note: the existing tests in this file should also be verified to still pass — they use English text and `NormalizedContent` is null, so COALESCE falls back to `content` and the `simple` language config still tokenizes English correctly.

- [ ] **Step 3: Normalize queries in `PostgresKeywordSearchService`**

In `Infrastructure/Retrieval/PostgresKeywordSearchService.cs`, the service needs access to `AiRagSettings` to pick up the Arabic options and the FTS language. Update the constructor:

```csharp
internal sealed class PostgresKeywordSearchService : IKeywordSearchService
{
    private readonly AiDbContext _db;
    private readonly ILogger<PostgresKeywordSearchService> _logger;
    private readonly AiRagSettings _settings;

    public PostgresKeywordSearchService(
        AiDbContext db,
        ILogger<PostgresKeywordSearchService> logger,
        IOptions<AiRagSettings> settings)
    {
        _db = db;
        _logger = logger;
        _settings = settings.Value;
    }
    // ...
}
```

Add the required using: `using Microsoft.Extensions.Options;` and `using Starter.Module.AI.Infrastructure.Settings;`.

Update `SearchAsync` to normalize the query and use `_settings.FtsLanguage`:

```csharp
public async Task<IReadOnlyList<KeywordSearchHit>> SearchAsync(
    Guid tenantId,
    string queryText,
    IReadOnlyCollection<Guid>? documentFilter,
    int limit,
    CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(queryText))
        return [];

    var normalized = _settings.ApplyArabicNormalization
        ? ArabicTextNormalizer.Normalize(queryText, new ArabicNormalizationOptions(
            _settings.NormalizeTaMarbuta,
            _settings.NormalizeArabicDigits))
        : queryText;

    if (string.IsNullOrWhiteSpace(normalized))
        return [];

    var ftsLang = _settings.FtsLanguage;

    var sql = @"
        SELECT c.qdrant_point_id AS ""ChunkId"",
               ts_rank_cd(c.content_tsv, plainto_tsquery('" + ftsLang + @"', {0}))::numeric AS ""Score""
        FROM ai_document_chunks c
        INNER JOIN ai_documents d ON d.id = c.document_id
        WHERE (d.tenant_id = {1} OR (d.tenant_id IS NULL AND {1} = '00000000-0000-0000-0000-000000000000'::uuid))
          AND c.chunk_level = 'child'
          AND c.content_tsv @@ plainto_tsquery('" + ftsLang + @"', {0})
    ";

    var parameters = new List<object> { normalized, tenantId };
    if (documentFilter is { Count: > 0 })
    {
        sql += $" AND c.document_id = ANY({{{parameters.Count}}})";
        parameters.Add(documentFilter.ToArray());
    }

    sql += $" ORDER BY \"Score\" DESC LIMIT {{{parameters.Count}}}";
    parameters.Add(limit);

    var hits = await _db.Database
        .SqlQueryRaw<KeywordSearchHitRow>(sql, parameters.ToArray())
        .ToListAsync(ct);

    return hits.Select(h => new KeywordSearchHit(h.ChunkId, h.Score)).ToList();
}
```

**Security note:** `ftsLang` is a string interpolated into SQL. This is safe because the value comes from `AiRagSettings` (app-controlled, not user input). Do not let a user-supplied string reach this path.

- [ ] **Step 4: Populate `NormalizedContent` in the ingest consumer**

In `Infrastructure/Consumers/ProcessDocumentConsumer.cs`, import the normalizer usings at the top of the file:

```csharp
using Starter.Module.AI.Infrastructure.Retrieval;
```

Inside `Consume`, right after `var points = new List<VectorPoint>(chunks.Children.Count);`, build the Arabic opts once:

```csharp
var arOpts = new ArabicNormalizationOptions(
    ragOptions.NormalizeTaMarbuta,
    ragOptions.NormalizeArabicDigits);
```

Then inside the `for (var i = 0; i < chunks.Children.Count; i++)` loop, after the `childEntities.Add(...)` call:

```csharp
if (ragOptions.ApplyArabicNormalization)
{
    var last = childEntities[^1];
    last.SetNormalizedContent(ArabicTextNormalizer.Normalize(last.Content, arOpts));
}
```

Do the same for the parents block — before `db.AiDocumentChunks.AddRange(parentEntities)`:

```csharp
if (ragOptions.ApplyArabicNormalization)
{
    foreach (var p in parentEntities)
        p.SetNormalizedContent(ArabicTextNormalizer.Normalize(p.Content, arOpts));
}
```

- [ ] **Step 5: Update DI if needed + check callers of `PostgresKeywordSearchService`**

Search for `new PostgresKeywordSearchService(` callers in tests.

Run: `grep -rn "new PostgresKeywordSearchService" boilerplateBE/`

For each test caller, update the construction to pass `IOptions<AiRagSettings>`. The `AiPostgresFixture` pattern is to use `Options.Create(new AiRagSettings())`. Example test update:

```csharp
using Microsoft.Extensions.Options;
using Starter.Module.AI.Infrastructure.Settings;
// ...
var settings = Options.Create(new AiRagSettings());
var svc = new PostgresKeywordSearchService(db, _fixture.Logger<PostgresKeywordSearchService>(), settings);
```

DI container registration (in `AIModule.cs` or wherever `IKeywordSearchService` is registered) likely already resolves `IOptions<AiRagSettings>` — verify the registration is fine.

- [ ] **Step 6: Run tests**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~PostgresKeywordSearchServiceTests"`
Expected: all PASS (existing English + new Arabic + mixed).

If the PostgreSQL `pg_trgm` / `ispell` configs are missing, `plainto_tsquery('simple', ...)` should still work (the `simple` config ships with base Postgres).

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Consumers/ProcessDocumentConsumer.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/PostgresKeywordSearchService.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/PostgresKeywordSearchServiceTests.cs
git commit -m "feat(ai): wire ArabicTextNormalizer into FTS index + query paths

Consumer populates NormalizedContent on every chunk; keyword search
normalizes the query text before plainto_tsquery and switches to the
'simple' FTS config. Resolves the case where Arabic-alef-variant or
diacritic differences between query and corpus produced zero keyword
hits and the hybrid search silently collapsed to vector-only."
```

---

## Task 6: Per-stage timeouts + degraded-stage propagation

Wraps each retrieval I/O stage so a single slow dependency can't stall the whole chat turn; surfaces failed stages to callers.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs` (telemetry log)
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RagRetrievalServiceTimeoutTests.cs`

- [ ] **Step 1: Failing tests**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RagRetrievalServiceTimeoutTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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

public sealed class RagRetrievalServiceTimeoutTests
{
    private static AiDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-timeout-{Guid.NewGuid():N}").Options;
        return new AiDbContext(options, currentUserService: null);
    }

    private sealed class SlowVectorStore : IVectorStore
    {
        public Task EnsureCollectionAsync(Guid t, int vs, CancellationToken ct) => Task.CompletedTask;
        public Task UpsertAsync(Guid t, IReadOnlyList<VectorPoint> p, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteByDocumentAsync(Guid t, Guid d, CancellationToken ct) => Task.CompletedTask;
        public Task DropCollectionAsync(Guid t, CancellationToken ct) => Task.CompletedTask;
        public async Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
            Guid t, float[] q, IReadOnlyCollection<Guid>? filter, int limit, CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return [];
        }
    }

    private sealed class FakeKw : IKeywordSearchService
    {
        public Task<IReadOnlyList<KeywordSearchHit>> SearchAsync(
            Guid t, string q, IReadOnlyCollection<Guid>? f, int l, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<KeywordSearchHit>>([]);
    }

    private sealed class FakeEmbed : IEmbeddingService
    {
        public int VectorSize => 1536;
        public Task<float[][]> EmbedAsync(
            IReadOnlyList<string> texts, CancellationToken ct,
            EmbedAttribution? a = null, AiRequestType r = AiRequestType.Embedding)
            => Task.FromResult(texts.Select(_ => new float[1536]).ToArray());
    }

    [Fact]
    public async Task VectorSearch_Timeout_ReturnsKeywordOnly_WithVectorStageDegraded()
    {
        await using var db = CreateDb();
        var settings = new AiRagSettings { StageTimeoutVectorMs = 50 };
        var svc = new RagRetrievalService(
            db,
            new SlowVectorStore(),
            new FakeKw(),
            new FakeEmbed(),
            new TokenCounter(),
            Options.Create(settings));

        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "A", null, "p");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var ctx = await svc.RetrieveForTurnAsync(assistant, "query", CancellationToken.None);

        ctx.DegradedStages.Should().Contain("vector-search");
    }

    // NOTE: We do not assert total latency here — CI variance makes that flaky.
}
```

- [ ] **Step 2: Run tests to confirm failure**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RagRetrievalServiceTimeoutTests"`
Expected: FAIL — either times out itself or asserts DegradedStages doesn't contain "vector-search".

- [ ] **Step 3: Add the WithTimeoutAsync helper + wiring to `RagRetrievalService`**

In `Infrastructure/Retrieval/RagRetrievalService.cs`:

Add the field (near the other readonly fields):
```csharp
private readonly ILogger<RagRetrievalService>? _logger;  // optional — ILogger may not exist yet; if missing, inject from ILoggerFactory
```

**If `RagRetrievalService` already has `ILogger<RagRetrievalService>` injected, skip this add.** Otherwise: inject `ILogger<RagRetrievalService> logger` in the primary constructor and assign. Also add `using Microsoft.Extensions.Logging;`.

Add the helper as a private method at the bottom of the class:

```csharp
private async Task<T?> WithTimeoutAsync<T>(
    Func<CancellationToken, Task<T>> op,
    int timeoutMs,
    string stageName,
    List<string> degraded,
    CancellationToken ct) where T : class
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(timeoutMs);
    try
    {
        return await op(cts.Token);
    }
    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
    {
        degraded.Add(stageName);
        _logger?.LogWarning("RAG stage '{Stage}' timed out after {TimeoutMs}ms", stageName, timeoutMs);
        return null;
    }
    catch (Exception ex)
    {
        degraded.Add(stageName);
        _logger?.LogError(ex, "RAG stage '{Stage}' failed", stageName);
        return null;
    }
}
```

Update `RetrieveForQueryAsync`: introduce a `var degraded = new List<string>();` early, wrap the three I/O stages, handle null returns:

```csharp
var degraded = new List<string>();

var vectors = await WithTimeoutAsync(
    inner => _embeddingService.EmbedAsync([queryText], inner, attribution: null, requestType: AiRequestType.QueryEmbedding),
    _settings.StageTimeoutEmbedMs, "embed-query", degraded, ct);

if (vectors is null)
    return new RetrievedContext([], [], 0, false, degraded);

var queryVector = vectors[0];

var retrievalTopK = _settings.RetrievalTopK;
var minHybrid = minScore ?? _settings.MinHybridScore;

var vectorHits = await WithTimeoutAsync(
    inner => _vectorStore.SearchAsync(tenantId, queryVector, documentFilter, retrievalTopK, inner),
    _settings.StageTimeoutVectorMs, "vector-search", degraded, ct)
    ?? (IReadOnlyList<VectorSearchHit>)[];

var keywordHits = await WithTimeoutAsync(
    inner => _keywordSearch.SearchAsync(tenantId, queryText, documentFilter, retrievalTopK, inner),
    _settings.StageTimeoutKeywordMs, "keyword-search", degraded, ct)
    ?? (IReadOnlyList<KeywordSearchHit>)[];
```

At the end, where the existing `return new RetrievedContext(trimmedChildren, trimmedParents, totalTokens, truncated, []);` lives, change the last arg to `degraded`.

Empty-path early return (`if (topKHits.Count == 0)`): also return `degraded` instead of `[]`.

- [ ] **Step 4: Wire DegradedStages into chat telemetry**

In `Application/Services/ChatExecutionService.cs`, find the existing diagnostic log near line 608:

```csharp
logger.LogInformation(
    "RAG retrieval for assistant {AssistantId}: children={Children} parents={Parents} tokens={Tokens} truncated={Truncated}",
    assistant.Id, retrieved.Children.Count, retrieved.Parents.Count, retrieved.TotalTokens, retrieved.TruncatedByBudget);
```

Change to:

```csharp
logger.LogInformation(
    "RAG retrieval for assistant {AssistantId}: children={Children} parents={Parents} tokens={Tokens} truncated={Truncated} degraded={Degraded}",
    assistant.Id,
    retrieved.Children.Count,
    retrieved.Parents.Count,
    retrieved.TotalTokens,
    retrieved.TruncatedByBudget,
    retrieved.DegradedStages.Count == 0 ? "none" : string.Join(",", retrieved.DegradedStages));
```

- [ ] **Step 5: Run tests**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RagRetrievalService"`
Expected: all PASS (existing tests + new timeout test). Existing RagRetrievalServiceTests may have created the service with 6 ctor args; if an `ILogger` arg was added, update those constructions to pass `NullLogger<RagRetrievalService>.Instance` (add `using Microsoft.Extensions.Logging.Abstractions;`).

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RagRetrievalServiceTimeoutTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RagRetrievalServiceTests.cs
git commit -m "feat(ai): per-stage timeouts + DegradedStages telemetry

Wrap embed-query, vector-search, keyword-search in a linked-cts timeout
helper. On timeout or exception the stage name is appended to
RetrievedContext.DegradedStages; the pipeline continues with whatever
the other stages returned. Chat telemetry log line now surfaces the
degraded-stage list so vector-only / keyword-only fallbacks are visible."
```

---

## Task 7: `DeleteByDocumentAsync` on `IVectorStore` + Qdrant impl + reprocess handler

Prevents orphan vectors when a document is reprocessed.

**Files:**
- Already modified: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/IVectorStore.cs` (already has `DeleteByDocumentAsync` per Read above)
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/QdrantVectorStore.cs` (find via Glob — probably `Infrastructure/Retrieval/` or `Infrastructure/Ingestion/`)
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/ReprocessDocument/ReprocessDocumentCommandHandler.cs` (find via Glob)
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/QdrantVectorStoreTests.cs` (if exists) or existing consumer tests

- [ ] **Step 1: Find file locations**

Run: `find boilerplateBE -name "QdrantVectorStore.cs"`
Run: `find boilerplateBE -name "ReprocessDocumentCommandHandler.cs"`

Read both files to learn the Qdrant client API and reprocess flow.

- [ ] **Step 2: Implement `DeleteByDocumentAsync` on `QdrantVectorStore`**

The Qdrant client exposes `DeleteAsync(collection, filter, ct)`. Build a filter on payload `DocumentId`. Example (adjust to the `Qdrant.Client` version actually referenced):

```csharp
public async Task DeleteByDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct)
{
    var collection = CollectionName(tenantId);

    var filter = new Filter
    {
        Must =
        {
            new Condition
            {
                Field = new FieldCondition
                {
                    Key = "DocumentId",
                    Match = new Match { Keyword = documentId.ToString() }
                }
            }
        }
    };

    try
    {
        await _client.DeleteAsync(collection, filter, cancellationToken: ct);
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
    {
        _logger.LogWarning("Qdrant collection '{Collection}' not found; nothing to delete for doc {DocId}", collection, documentId);
    }
}
```

The exact builder syntax depends on the installed version of `Qdrant.Client`. Match the style used elsewhere in the same file (e.g., how `SearchAsync` builds its filters).

- [ ] **Step 3: Update `ReprocessDocumentCommandHandler`**

Before re-queueing the document for processing, delete existing chunks + vectors:

Read the handler first. The flow typically is:
1. Load `doc`.
2. Call `doc.ResetForReprocessing()`.
3. Publish `ProcessDocumentMessage`.

Insert between (1) and (2):

```csharp
// Remove existing chunks before re-ingest so Qdrant doesn't accumulate orphans.
var existingChunks = await _db.AiDocumentChunks
    .Where(c => c.DocumentId == doc.Id)
    .ToListAsync(ct);
_db.AiDocumentChunks.RemoveRange(existingChunks);

var tenantId = doc.TenantId ?? Guid.Empty;
await _vectorStore.DeleteByDocumentAsync(tenantId, doc.Id, ct);
```

Add `IVectorStore _vectorStore` to the handler constructor if not already present.

- [ ] **Step 4: Tests**

Search for existing Qdrant vector store tests:
Run: `find boilerplateBE -name "QdrantVectorStore*Tests.cs"`

If tests exist, add a case that upserts two points, calls `DeleteByDocumentAsync` for one document, re-searches, and verifies only the other doc's points remain.

If no tests exist yet, do NOT create a new Qdrant integration test fixture in this task — the Qdrant fixture is heavy and belongs in its own integration test setup. Instead, add a consumer-level assertion: a unit test that the reprocess handler calls `IVectorStore.DeleteByDocumentAsync` with the right args, using a mocked `IVectorStore`.

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Commands/ReprocessDocumentCommandHandlerTests.cs` if it doesn't exist, using existing in-memory DB patterns — or extend the existing file. The test verifies:
- Chunks for the target doc are removed before re-queue.
- `IVectorStore.DeleteByDocumentAsync` is invoked with `(tenantId, docId)`.

- [ ] **Step 5: Run tests**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~Reprocess"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QdrantVectorStore.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/ReprocessDocument/ReprocessDocumentCommandHandler.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Commands/ReprocessDocumentCommandHandlerTests.cs
git commit -m "feat(ai): delete chunks + vectors before reprocessing a document

Reprocess was upserting new vectors on top of the old ones and leaving
the previous chunks in the DB, causing orphan points in Qdrant and
duplicate rows in ai_document_chunks. Now clears both before re-queueing
the ProcessDocumentMessage. Qdrant client filter-delete uses the payload
DocumentId field."
```

(Adjust the path for QdrantVectorStore.cs to match where Step 1's find located it.)

---

## Task 8: Fingerprint skip — compute `ContentHash` on upload + skip-and-clone in consumer

Skips re-embedding when the same bytes have already been processed for the tenant.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/UploadDocument/UploadDocumentCommandHandler.cs` (find via Glob)
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Consumers/ProcessDocumentConsumer.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Consumers/ProcessDocumentConsumerTests.cs`

- [ ] **Step 1: Compute ContentHash at upload time**

Find and read `UploadDocumentCommandHandler.cs`. After the file is saved to storage and the `AiDocument` entity is created + persisted, compute the SHA-256 of the uploaded bytes and set it via `doc.SetContentHash(hash)`.

Outline (adjust to actual handler shape):

```csharp
using System.Security.Cryptography;

// After saving to storage, before persisting the entity:
string contentHash;
using (var sha = SHA256.Create())
{
    file.OpenReadStream().Position = 0; // or rewind as needed
    var hashBytes = await sha.ComputeHashAsync(fileStream, ct);
    contentHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
}

var doc = AiDocument.Create(...);
doc.SetContentHash(contentHash);
_db.AiDocuments.Add(doc);
await _db.SaveChangesAsync(ct);
```

Exact integration depends on the handler's current flow. The key invariant: `ContentHash` is populated before the `ProcessDocumentMessage` is published.

- [ ] **Step 2: Fingerprint skip in `ProcessDocumentConsumer`**

At the top of the try block (just after `doc.MarkProcessing()` + `SaveChangesAsync`), insert:

```csharp
if (!string.IsNullOrEmpty(doc.ContentHash))
{
    var match = await db.AiDocuments
        .IgnoreQueryFilters()
        .Where(d => d.Id != doc.Id
                 && d.ContentHash == doc.ContentHash
                 && (d.TenantId == doc.TenantId || (d.TenantId == null && doc.TenantId == null))
                 && d.EmbeddingStatus == EmbeddingStatus.Completed)
        .OrderByDescending(d => d.ProcessedAt)
        .FirstOrDefaultAsync(ct);

    if (match is not null)
    {
        var existingChunks = await db.AiDocumentChunks
            .AsNoTracking()
            .Where(c => c.DocumentId == match.Id)
            .ToListAsync(ct);

        if (existingChunks.Count > 0)
        {
            logger.LogInformation(
                "Document {Id} matches fingerprint of {MatchId}; cloning {Count} chunks and skipping embedding.",
                doc.Id, match.Id, existingChunks.Count);

            // Clone parents first so we can remap ParentChunkId.
            var oldToNewParentId = new Dictionary<Guid, Guid>();
            var newParents = new List<AiDocumentChunk>();
            foreach (var parent in existingChunks.Where(c => c.ChunkLevel == "parent"))
            {
                var clone = AiDocumentChunk.Create(
                    documentId: doc.Id,
                    chunkLevel: "parent",
                    content: parent.Content,
                    chunkIndex: parent.ChunkIndex,
                    tokenCount: parent.TokenCount,
                    qdrantPointId: Guid.NewGuid(),
                    parentChunkId: null,
                    sectionTitle: parent.SectionTitle,
                    pageNumber: parent.PageNumber);
                if (!string.IsNullOrEmpty(parent.NormalizedContent))
                    clone.SetNormalizedContent(parent.NormalizedContent);
                newParents.Add(clone);
                oldToNewParentId[parent.Id] = clone.Id;
            }
            db.AiDocumentChunks.AddRange(newParents);

            var tenantId = doc.TenantId ?? Guid.Empty;
            await vectorStore.EnsureCollectionAsync(tenantId, embedder.VectorSize, ct);

            var points = new List<VectorPoint>();
            var newChildren = new List<AiDocumentChunk>();
            foreach (var child in existingChunks.Where(c => c.ChunkLevel == "child"))
            {
                var parentDbId = child.ParentChunkId is Guid pid && oldToNewParentId.TryGetValue(pid, out var np)
                    ? np : (Guid?)null;
                var newPointId = Guid.NewGuid();
                var cloneChild = AiDocumentChunk.Create(
                    documentId: doc.Id,
                    chunkLevel: "child",
                    content: child.Content,
                    chunkIndex: child.ChunkIndex,
                    tokenCount: child.TokenCount,
                    qdrantPointId: newPointId,
                    parentChunkId: parentDbId,
                    sectionTitle: child.SectionTitle,
                    pageNumber: child.PageNumber);
                if (!string.IsNullOrEmpty(child.NormalizedContent))
                    cloneChild.SetNormalizedContent(child.NormalizedContent);
                newChildren.Add(cloneChild);

                // Re-embed isn't needed because we clone vectors from existing qdrant points.
                // Simpler: fetch the vector via Qdrant retrieve-by-id, or re-embed. To avoid
                // another Qdrant roundtrip, re-embed the single content. Acceptable cost: same
                // bytes = identical embedding; this is the fallback if vector copy isn't trivial.
                // For now, re-embed:
                var vec = (await embedder.EmbedAsync([child.Content], ct)).Single();
                points.Add(new VectorPoint(
                    Id: newPointId,
                    Vector: vec,
                    Payload: new VectorPayload(
                        DocumentId: doc.Id,
                        DocumentName: doc.Name,
                        ChunkLevel: "child",
                        ChunkIndex: child.ChunkIndex,
                        SectionTitle: child.SectionTitle,
                        PageNumber: child.PageNumber,
                        ParentChunkId: parentDbId,
                        TenantId: tenantId)));
            }

            db.AiDocumentChunks.AddRange(newChildren);
            await vectorStore.UpsertAsync(tenantId, points, ct);

            doc.MarkCompleted(chunkCount: newChildren.Count);
            await db.SaveChangesAsync(ct);
            return;
        }
    }
}
```

**Note:** we re-embed in the clone path for simplicity. A future optimization can retrieve the existing vectors from Qdrant by point id and copy them; not worth the Qdrant-API surface area in this iteration.

- [ ] **Step 3: Tests for the fingerprint-match path**

Find and read `boilerplateBE/tests/Starter.Api.Tests/Ai/Consumers/ProcessDocumentConsumerTests.cs`. Add:

```csharp
[Fact]
public async Task Fingerprint_Match_Clones_Chunks_And_Skips_Extraction()
{
    // Arrange: seed an existing completed doc with chunks, and a new doc with same hash + same tenant.
    // Act: run consumer on the new doc.
    // Assert: extractor is NOT called; new doc is Completed with same chunk count as original.
}

[Fact]
public async Task Fingerprint_Match_But_No_Chunks_Falls_Back_To_Processing()
{
    // Arrange: existing doc with same hash, but no chunks in DB.
    // Act: run consumer.
    // Assert: normal extractor path runs.
}
```

The existing test file's fakes + fixtures should be reused. If the consumer uses `IServiceScopeFactory`, the test setup for the new cases can mirror the existing ones.

- [ ] **Step 4: Run tests**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~ProcessDocumentConsumer"`
Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/UploadDocument/UploadDocumentCommandHandler.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Consumers/ProcessDocumentConsumer.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Consumers/ProcessDocumentConsumerTests.cs
git commit -m "feat(ai): SHA-256 fingerprint skip on re-ingest

Upload handler now computes content_hash on the uploaded bytes. The
consumer looks for an earlier Completed document with the same
(tenant_id, content_hash); if found with chunks intact, it clones the
chunk rows under the new document id and re-upserts the vectors,
skipping extraction and OCR. New qdrant_point_ids are generated so
vector-store uniqueness is preserved."
```

---

## Task 9: `CachingEmbeddingService` decorator + DI registration + tests

Caches single-text query embeddings; passes document ingestion batches through.

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/CachingEmbeddingService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs` (DI)
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/CachingEmbeddingServiceTests.cs`

- [ ] **Step 1: Failing tests**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/CachingEmbeddingServiceTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion;

public sealed class CachingEmbeddingServiceTests
{
    private sealed class InnerCountingEmbedder : IEmbeddingService
    {
        public int VectorSize => 4;
        public int CallCount { get; private set; }
        public Task<float[][]> EmbedAsync(
            IReadOnlyList<string> texts, CancellationToken ct,
            EmbedAttribution? a = null, AiRequestType r = AiRequestType.Embedding)
        {
            CallCount++;
            return Task.FromResult(texts.Select((_, i) => new float[] { 0.1f * i, 0.2f, 0.3f, 0.4f }).ToArray());
        }
    }

    private sealed class MemoryCache : ICacheService
    {
        private readonly Dictionary<string, object?> _store = new();
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
            => Task.FromResult((T?)(_store.TryGetValue(key, out var v) ? v : default));
        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
        { _store[key] = value; return Task.CompletedTask; }
        public Task RemoveAsync(string key, CancellationToken ct = default)
        { _store.Remove(key); return Task.CompletedTask; }
        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
        {
            foreach (var k in _store.Keys.Where(k => k.StartsWith(prefix)).ToList()) _store.Remove(k);
            return Task.CompletedTask;
        }
        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default)
        {
            if (_store.TryGetValue(key, out var v) && v is T existing) return existing;
            var created = await factory();
            _store[key] = created;
            return created;
        }
        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(_store.ContainsKey(key));
    }

    private static CachingEmbeddingService Build(InnerCountingEmbedder inner, ICacheService cache) =>
        new(inner, cache, Options.Create(new AiRagSettings { EmbeddingCacheTtlSeconds = 60 }));

    [Fact]
    public async Task Hit_ReturnsCachedVector_WithoutCallingInner()
    {
        var inner = new InnerCountingEmbedder();
        var cache = new MemoryCache();
        var svc = Build(inner, cache);

        var first = await svc.EmbedAsync(["hello"], CancellationToken.None);
        var second = await svc.EmbedAsync(["hello"], CancellationToken.None);

        inner.CallCount.Should().Be(1);
        second[0].Should().BeEquivalentTo(first[0]);
    }

    [Fact]
    public async Task Miss_CallsInner_AndStoresResult()
    {
        var inner = new InnerCountingEmbedder();
        var cache = new MemoryCache();
        var svc = Build(inner, cache);

        var first = await svc.EmbedAsync(["a"], CancellationToken.None);
        var second = await svc.EmbedAsync(["b"], CancellationToken.None);

        inner.CallCount.Should().Be(2);
        first[0].Should().NotBeEquivalentTo(second[0]);
    }

    [Fact]
    public async Task MultiText_BypassesCache()
    {
        var inner = new InnerCountingEmbedder();
        var cache = new MemoryCache();
        var svc = Build(inner, cache);

        _ = await svc.EmbedAsync(["a", "b"], CancellationToken.None);
        _ = await svc.EmbedAsync(["a", "b"], CancellationToken.None);

        inner.CallCount.Should().Be(2);  // cache never engaged for multi-text
    }

    [Fact]
    public async Task SetsVectorSizeFromCacheHit()
    {
        var inner = new InnerCountingEmbedder();
        var cache = new MemoryCache();
        var svc = Build(inner, cache);

        _ = await svc.EmbedAsync(["warmup"], CancellationToken.None);

        var svc2 = Build(new InnerCountingEmbedder(), cache);
        _ = await svc2.EmbedAsync(["warmup"], CancellationToken.None);

        svc2.VectorSize.Should().Be(4);
    }
}
```

- [ ] **Step 2: Run tests to confirm failure**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~CachingEmbeddingServiceTests"`
Expected: FAIL — class doesn't exist.

- [ ] **Step 3: Implement `CachingEmbeddingService`**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/CachingEmbeddingService.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Ingestion;

internal sealed class CachingEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingService _inner;
    private readonly ICacheService _cache;
    private readonly AiRagSettings _settings;
    private int _vectorSize = -1;

    public CachingEmbeddingService(
        IEmbeddingService inner,
        ICacheService cache,
        IOptions<AiRagSettings> settings)
    {
        _inner = inner;
        _cache = cache;
        _settings = settings.Value;
    }

    public int VectorSize => _vectorSize > 0 ? _vectorSize : _inner.VectorSize;

    public async Task<float[][]> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct,
        EmbedAttribution? attribution = null,
        AiRequestType requestType = AiRequestType.Embedding)
    {
        if (texts.Count != 1)
            return await _inner.EmbedAsync(texts, ct, attribution, requestType);

        var key = BuildKey(texts[0]);
        var cached = await _cache.GetAsync<float[]>(key, ct);
        if (cached is not null && cached.Length > 0)
        {
            if (_vectorSize < 0) _vectorSize = cached.Length;
            return [cached];
        }

        var result = await _inner.EmbedAsync(texts, ct, attribution, requestType);
        if (result.Length == 1 && result[0].Length > 0)
        {
            _vectorSize = result[0].Length;
            await _cache.SetAsync(key, result[0], TimeSpan.FromSeconds(_settings.EmbeddingCacheTtlSeconds), ct);
        }
        return result;
    }

    private static string BuildKey(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return "ai:embed:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
```

- [ ] **Step 4: Register the decorator in DI**

In `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`, find the line:

```csharp
services.AddScoped<IEmbeddingService, Infrastructure.Ingestion.EmbeddingService>();
```

Replace with:

```csharp
services.AddScoped<Infrastructure.Ingestion.EmbeddingService>();
services.AddScoped<IEmbeddingService>(sp => new Infrastructure.Ingestion.CachingEmbeddingService(
    sp.GetRequiredService<Infrastructure.Ingestion.EmbeddingService>(),
    sp.GetRequiredService<Starter.Application.Common.Interfaces.ICacheService>(),
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Infrastructure.Settings.AiRagSettings>>()));
```

- [ ] **Step 5: Run tests**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~CachingEmbeddingServiceTests"`
Expected: all 4 PASS.

Full build: `dotnet build boilerplateBE/Starter.sln` — expect 0 errors.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/CachingEmbeddingService.cs \
        boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/CachingEmbeddingServiceTests.cs
git commit -m "feat(ai): cache single-text (query) embeddings via ICacheService

Transparent decorator on IEmbeddingService. Caches query-path embeds
(single-text calls) keyed by SHA-256(text); multi-text document-ingest
calls bypass the cache unchanged. Cache hits set VectorSize from the
cached vector length so downstream callers behave correctly on cold-cache
reuse."
```

---

## Task 10: Full regression + E2E verification in `_testAiRag2`

Final check: build, run the full test suite, regenerate the test app, and confirm the existing chat verify script still passes.

- [ ] **Step 1: Full build**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: 0 errors, warnings acceptable.

- [ ] **Step 2: Full test suite**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj`
Expected: all PASS.

Record the before/after counts. Notable new tests: ArabicTextNormalizer (14+), HybridScoreCalculator (10), RagRetrievalServiceTimeout (1), CachingEmbeddingService (4), Arabic FTS integration (3), fingerprint (2). Expect 30+ net-new tests.

- [ ] **Step 3: Regenerate `_testAiRag2`**

The test app is already running; drop its DB so the new schema (ContentHash, NormalizedContent, regenerated content_tsv) is applied on next start:

```bash
pkill -f "_testAiRag2.Api" 2>/dev/null
psql -U postgres -c "DROP DATABASE IF EXISTS _testairag2db;"
cd _testAiRag2/_testAiRag2-BE
dotnet ef database update --project src/_testAiRag2.Infrastructure --startup-project src/_testAiRag2.Api || dotnet run --project src/_testAiRag2.Api --launch-profile http &
```

Wait for BE up:
```bash
until curl -sf http://localhost:5102/health > /dev/null; do sleep 2; done
```

- [ ] **Step 4: Re-run the Blueforge RAG verify script**

Run: `bash /tmp/_testAiRag2-verify.sh`
Expected: chat answers cite Blueforge content with `[1]` marker; no degraded stages in the log.

- [ ] **Step 5: Push**

If all green:
```bash
git push origin feature/ai-integration
```

- [ ] **Step 6: Final commit (if anything fixed during regression)**

If Step 2 or 4 surfaced any fix, commit it with a focused message. Otherwise skip — the prior tasks are the shippable increment.

---

## Completion criteria

All tasks green. Full build passes, full test suite passes (including Arabic FTS), E2E chat smoke still grounds answers in Blueforge content, `DegradedStages` is empty on the happy path. Push authorized.
