# Plan 4b-2 — Query Intelligence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship query expansion, hybrid (listwise + pointwise) LLM reranking with auto strategy selection, neighbor chunk expansion, and a lightweight question classifier on top of the Plan 4b-1 RAG pipeline — all configurable per-tenant and observable via telemetry.

**Architecture:** Extend `RagRetrievalService` to call four new stages behind `WithTimeoutAsync` — classify → rewrite → (existing RRF fusion) → rerank → neighbor-expand. Each stage is its own interface + implementation in `Infrastructure/Retrieval/`, registered in `AIModule.ConfigureServices`. Hybrid reranker dispatches to a listwise or pointwise sub-strategy picked by a `RerankStrategySelector`; the selector reads `AiRagSettings.RerankStrategy` and the classifier's `QuestionType?` output.

**Tech Stack:** .NET 10, xUnit + FluentAssertions, Redis via `ICacheService`, Qdrant, PostgreSQL FTS, Anthropic/OpenAI chat completions via `IAiProvider`, OpenTelemetry.

**Spec:** [docs/superpowers/specs/2026-04-19-ai-module-plan-4b-2-query-intelligence-design.md](../specs/2026-04-19-ai-module-plan-4b-2-query-intelligence-design.md)

---

## File Structure

**New files (Application layer — public contracts):**
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/QuestionType.cs` — enum
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RerankStrategy.cs` — enum
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RerankResult.cs` — record
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RerankContext.cs` — record
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IQueryRewriter.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IReranker.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IQuestionClassifier.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/INeighborExpander.cs`

**New files (Infrastructure layer — internal implementations):**
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Json/JsonArrayExtractor.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/RuleBasedQueryRewriter.cs` — static helper
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/QueryRewriter.cs` — `IQueryRewriter` impl
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/ListwiseReranker.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/PointwiseReranker.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/RerankStrategySelector.cs`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/Reranker.cs` — `IReranker` impl
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Classification/RegexQuestionClassifier.cs` — static helper
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Classification/QuestionClassifier.cs` — `IQuestionClassifier` impl
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/NeighborExpander.cs` — `INeighborExpander` impl

**Modified files:**
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs` — add new fields, remove `EnableReranking`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RetrievedContext.cs` — add `Siblings`, `RerankMetrics`
- `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RetrievedChunk.cs` — add `ChunkIndex`
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs` — wire new stages
- `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/ContextPromptBuilder.cs` — render siblings
- `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs` — DI registrations
- `boilerplateBE/src/Starter.Api/appsettings.Development.json` — example values for new settings

**New test files:**
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/JsonArrayExtractorTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RuleBasedQueryRewriterTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/QueryRewriterTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ListwiseRerankerTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/PointwiseRerankerTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RerankStrategySelectorTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RerankerTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RegexQuestionClassifierTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/QuestionClassifierTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/NeighborExpanderTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RagRetrievalServicePipelineTests.cs`
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Fakes/FakeAiProvider.cs` — shared test double
- `boilerplateBE/tests/Starter.Api.Tests/Ai/Fakes/FakeCacheService.cs` — shared test double
- `boilerplateBE/tests/Starter.Api.Tests/Ai/fixtures/arabic_queries.json`

---

## Type references (used across tasks)

Use this section when writing tests/impls to confirm existing signatures without re-reading source:

- `HybridHit` (existing): `record HybridHit(Guid ChunkId, decimal SemanticScore, decimal KeywordScore, decimal HybridScore)`. `ChunkId` here is the **Qdrant point id**, not the chunk's primary key.
- `HybridScoreCalculator.Combine` (existing): `static IReadOnlyList<HybridHit> Combine(IReadOnlyList<IReadOnlyList<VectorSearchHit>> semanticLists, IReadOnlyList<IReadOnlyList<KeywordSearchHit>> keywordLists, decimal vectorWeight, decimal keywordWeight, int rrfK, decimal minScore)` — already accepts multi-list input.
- `AiDocumentChunk` (existing, primary-constructor-less, private setters): fields we touch — `Id`, `DocumentId`, `ParentChunkId`, `ChunkIndex`, `ChunkLevel`, `Content`, `PageNumber`, `SectionTitle`, `QdrantPointId`, `TokenCount`.
- `IAiProvider.ChatAsync` (existing, internal): `Task<AiChatCompletion> ChatAsync(IReadOnlyList<AiChatMessage> messages, AiChatOptions options, CancellationToken ct)` where `AiChatCompletion(string? Content, IReadOnlyList<AiToolCall>? ToolCalls, int InputTokens, int OutputTokens, string FinishReason)`.
- `IAiProviderFactory` (internal): `CreateDefault()`, `GetDefaultProviderType()`.
- `ICacheService`: `GetAsync<T>`, `SetAsync<T>(key, value, TimeSpan?)`, plus other methods we won't use here.
- `ArabicTextNormalizer.Normalize(string, ArabicNormalizationOptions)` (existing).

---

## Phase A — Foundations (Tasks 1–3)

### Task 1: Add 4b-2 settings and enums to AiRagSettings

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/QuestionType.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RerankStrategy.cs`

- [ ] **Step 1: Create `RerankStrategy` enum**

`boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RerankStrategy.cs`:

```csharp
namespace Starter.Module.AI.Application.Services.Retrieval;

public enum RerankStrategy
{
    Off,
    Listwise,
    Pointwise,
    Auto,
    FallbackRrf
}
```

- [ ] **Step 2: Create `QuestionType` enum**

`boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/QuestionType.cs`:

```csharp
namespace Starter.Module.AI.Application.Services.Retrieval;

public enum QuestionType
{
    Greeting,
    Definition,
    Factoid,
    Reasoning,
    Listing
}
```

- [ ] **Step 3: Modify `AiRagSettings` — remove `EnableReranking`, add new fields**

Replace the line `public bool EnableReranking { get; init; } = false;       // 4b-2` with nothing (remove it). Then, at the end of the class (before closing brace), append:

```csharp
    // ---- New in Plan 4b-2 — Query rewriter ----
    public int QueryRewriteMaxVariants { get; init; } = 3;
    public int QueryRewriteCacheTtlSeconds { get; init; } = 1800;
    public int StageTimeoutQueryRewriteMs { get; init; } = 4_000;

    // ---- New in Plan 4b-2 — Reranker (hybrid) ----
    // Replaces the legacy EnableReranking bool (which was a no-op in 4b-1).
    // Mapping when migrating appsettings: true → Auto, false → Off.
    public RerankStrategy RerankStrategy { get; init; } = RerankStrategy.Auto;
    public int ListwiseRerankPoolMultiplier { get; init; } = 3;
    public int PointwiseRerankPoolMultiplier { get; init; } = 2;
    public int PointwiseMaxParallelism { get; init; } = 5;
    public decimal MinPointwiseScore { get; init; } = 0.3m;
    public decimal PointwiseMaxFailureRatio { get; init; } = 0.25m;
    public int RerankCacheTtlSeconds { get; init; } = 1800;
    public string? RerankerModel { get; init; } = null;
    public int StageTimeoutRerankMs { get; init; } = 8_000;

    // ---- New in Plan 4b-2 — Question classifier ----
    public bool EnableQuestionClassification { get; init; } = false;
    public int QuestionClassifierCacheTtlSeconds { get; init; } = 1800;
    public int StageTimeoutClassifyMs { get; init; } = 2_000;

    // ---- New in Plan 4b-2 — Neighbor expansion ----
    public int NeighborWindow { get; init; } = 0;
    public decimal NeighborScoreWeight { get; init; } = 0.5m;
```

Also add the `using` at the top of the file (after the existing `namespace` line is fine but keep the convention — put it before `namespace`):

```csharp
using Starter.Module.AI.Application.Services.Retrieval;
```

- [ ] **Step 4: Update `ai-integration` `appsettings.Development.json` to show new keys (if the file references the `AI:Rag` section)**

Open `boilerplateBE/src/Starter.Api/appsettings.Development.json` and, inside the `AI:Rag` object (if present; otherwise skip this step and it will use defaults), add the new entries. If there is no `AI:Rag` section in the file today, do not create one — defaults are fine.

- [ ] **Step 5: Build**

```bash
cd boilerplateBE && dotnet build
```

Expected: SUCCESS with 0 errors. If any file references the removed `EnableReranking`, the build error points you to it; there should be none (4b-1 memo says it was declared as a no-op).

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/QuestionType.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RerankStrategy.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs \
        boilerplateBE/src/Starter.Api/appsettings.Development.json
git commit -m "feat(ai): 4b-2 settings and enums — RerankStrategy, QuestionType, new AiRagSettings fields"
```

---

### Task 2: Create `RerankResult` and `RerankContext` records

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RerankResult.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RerankContext.cs`

- [ ] **Step 1: Create `RerankResult`**

`boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RerankResult.cs`:

```csharp
namespace Starter.Module.AI.Application.Services.Retrieval;

public sealed record RerankResult(
    IReadOnlyList<HybridHit> Ordered,
    RerankStrategy StrategyRequested,
    RerankStrategy StrategyUsed,
    int CandidatesIn,
    int CandidatesScored,
    int CacheHits,
    long LatencyMs,
    int TokensIn,
    int TokensOut,
    double UnusedRatio);
```

(If the `HybridHit` type is in a different namespace, add `using Starter.Module.AI.Infrastructure.Retrieval;` — confirm by grepping. If `HybridHit` is `internal`, promote it to `public` in its own file and keep the record shape identical.)

- [ ] **Step 2: Make `HybridHit` public if it is currently `internal`**

Open `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/HybridScoreCalculator.cs`. The record `HybridHit` is declared at line 5. If the declaration is `internal sealed record HybridHit(...)` change it to `public sealed record HybridHit(...)`. If it is already `public`, leave it. This is required because `RerankResult` lives in `Application` and references it.

- [ ] **Step 3: Create `RerankContext`**

`boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RerankContext.cs`:

```csharp
namespace Starter.Module.AI.Application.Services.Retrieval;

public sealed record RerankContext(
    QuestionType? QuestionType,
    RerankStrategy? StrategyOverride);
```

`StrategyOverride` is `null` when the caller wants the selector to decide from settings + question type.

- [ ] **Step 4: Build**

```bash
cd boilerplateBE && dotnet build
```

Expected: SUCCESS.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RerankResult.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RerankContext.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/HybridScoreCalculator.cs
git commit -m "feat(ai): 4b-2 reranker result/context records + promote HybridHit to public"
```

---

### Task 3: `JsonArrayExtractor` helper + tests

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Json/JsonArrayExtractor.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/JsonArrayExtractorTests.cs`

- [ ] **Step 1: Write the failing test**

`boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/JsonArrayExtractorTests.cs`:

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Retrieval.Json;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class JsonArrayExtractorTests
{
    [Fact]
    public void BareArray_ReturnsElements()
    {
        JsonArrayExtractor.TryExtractStrings("[\"a\", \"b\"]", out var items).Should().BeTrue();
        items.Should().Equal("a", "b");
    }

    [Fact]
    public void MarkdownFenced_ReturnsElements()
    {
        var input = "Sure, here you go:\n```json\n[\"x\", \"y\", \"z\"]\n```\n";
        JsonArrayExtractor.TryExtractStrings(input, out var items).Should().BeTrue();
        items.Should().Equal("x", "y", "z");
    }

    [Fact]
    public void LeadingPreamble_ReturnsElements()
    {
        JsonArrayExtractor.TryExtractStrings("Here: [\"foo\"]", out var items).Should().BeTrue();
        items.Should().Equal("foo");
    }

    [Fact]
    public void NoArray_ReturnsFalse()
    {
        JsonArrayExtractor.TryExtractStrings("nope", out var items).Should().BeFalse();
        items.Should().BeEmpty();
    }

    [Fact]
    public void IntegerArray_UsesTryExtractInts()
    {
        JsonArrayExtractor.TryExtractInts("[2, 0, 1]", out var items).Should().BeTrue();
        items.Should().Equal(2, 0, 1);
    }

    [Fact]
    public void IntegerArrayWithFence_UsesTryExtractInts()
    {
        JsonArrayExtractor.TryExtractInts("```\n[1,2,3]\n```", out var items).Should().BeTrue();
        items.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void ArabicStringsPreserved()
    {
        JsonArrayExtractor.TryExtractStrings("[\"ما هو\", \"التعريف\"]", out var items).Should().BeTrue();
        items.Should().Equal("ما هو", "التعريف");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~JsonArrayExtractorTests"
```

Expected: FAIL with compile error (type `JsonArrayExtractor` not found).

- [ ] **Step 3: Implement `JsonArrayExtractor`**

`boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Json/JsonArrayExtractor.cs`:

```csharp
using System.Text.Json;

namespace Starter.Module.AI.Infrastructure.Retrieval.Json;

internal static class JsonArrayExtractor
{
    public static bool TryExtractStrings(string input, out IReadOnlyList<string> items)
    {
        if (TryExtractArrayElement(input, out var array))
        {
            var list = new List<string>(array.GetArrayLength());
            foreach (var el in array.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String)
                    list.Add(el.GetString() ?? string.Empty);
            }
            items = list;
            return list.Count > 0;
        }
        items = Array.Empty<string>();
        return false;
    }

    public static bool TryExtractInts(string input, out IReadOnlyList<int> items)
    {
        if (TryExtractArrayElement(input, out var array))
        {
            var list = new List<int>(array.GetArrayLength());
            foreach (var el in array.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i))
                    list.Add(i);
            }
            items = list;
            return list.Count > 0;
        }
        items = Array.Empty<int>();
        return false;
    }

    private static bool TryExtractArrayElement(string input, out JsonElement array)
    {
        array = default;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var start = input.IndexOf('[');
        var end = input.LastIndexOf(']');
        if (start < 0 || end <= start) return false;

        var slice = input.AsSpan(start, end - start + 1).ToString();
        try
        {
            using var doc = JsonDocument.Parse(slice);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return false;
            array = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~JsonArrayExtractorTests"
```

Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Json/JsonArrayExtractor.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/JsonArrayExtractorTests.cs
git commit -m "feat(ai): JsonArrayExtractor tolerates markdown-fenced + preamble-wrapped LLM output"
```

---

### Task 4: Shared test fakes — `FakeAiProvider` and `FakeCacheService`

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Fakes/FakeAiProvider.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Fakes/FakeCacheService.cs`

Rationale: reranker, rewriter, classifier tests all need a scripted `IAiProvider` and an in-memory `ICacheService`. Centralising them avoids duplication across 8+ test files.

- [ ] **Step 1: Make `IAiProvider`, `IAiProviderFactory`, and chat record types `public` where needed for tests**

Open `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/AiProviderTypes.cs`. Change all four records (`AiChatMessage`, `AiChatOptions`, `AiChatCompletion`, `AiChatChunk`, `AiToolCall`, `AiToolDefinitionDto`) from `internal` to `public`.

Open the file defining `IAiProvider` (search with `grep -r "interface IAiProvider" boilerplateBE/src/modules/Starter.Module.AI`). Change `internal interface IAiProvider` to `public interface IAiProvider`.

Open `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/AiProviderFactory.cs`. Change `internal interface IAiProviderFactory` to `public interface IAiProviderFactory`.

Also make `AiProviderType` enum public if not already (it's in the same module under `Domain/Enums`).

Build to verify no regression:

```bash
cd boilerplateBE && dotnet build
```

Expected: SUCCESS.

- [ ] **Step 2: Create `FakeAiProvider`**

`boilerplateBE/tests/Starter.Api.Tests/Ai/Fakes/FakeAiProvider.cs`:

```csharp
using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Api.Tests.Ai.Fakes;

/// <summary>
/// Scripted IAiProvider for unit tests. Each enqueued response is returned in order
/// on successive ChatAsync calls. Calls is incremented for every invocation.
/// </summary>
public sealed class FakeAiProvider : IAiProvider
{
    private readonly Queue<Func<IReadOnlyList<AiChatMessage>, AiChatOptions, AiChatCompletion>> _responses = new();
    public int Calls { get; private set; }
    public List<(IReadOnlyList<AiChatMessage> Messages, AiChatOptions Options)> CallLog { get; } = new();

    public void EnqueueContent(string content, int inputTokens = 10, int outputTokens = 5)
    {
        _responses.Enqueue((_, _) => new AiChatCompletion(content, null, inputTokens, outputTokens, "stop"));
    }

    public void EnqueueThrow(Exception ex)
    {
        _responses.Enqueue((_, _) => throw ex);
    }

    public Task<AiChatCompletion> ChatAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        CancellationToken ct)
    {
        Calls++;
        CallLog.Add((messages, options));
        if (_responses.Count == 0)
            throw new InvalidOperationException("FakeAiProvider: no scripted response available.");
        var factory = _responses.Dequeue();
        return Task.FromResult(factory(messages, options));
    }

    // Streaming not used in 4b-2 tests. Leave unimplemented.
    public IAsyncEnumerable<AiChatChunk> ChatStreamAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        CancellationToken ct)
        => throw new NotImplementedException();
}

public sealed class FakeAiProviderFactory : IAiProviderFactory
{
    public IAiProvider Provider { get; }
    public string EmbeddingModelId { get; set; } = "OpenAI:text-embedding-3-small";
    public string DefaultChatModelId { get; set; } = "OpenAI:gpt-4o-mini";

    public FakeAiProviderFactory(IAiProvider provider) { Provider = provider; }

    public IAiProvider Create(Starter.Module.AI.Domain.Enums.AiProviderType providerType) => Provider;
    public Starter.Module.AI.Domain.Enums.AiProviderType GetDefaultProviderType()
        => Starter.Module.AI.Domain.Enums.AiProviderType.OpenAI;
    public Starter.Module.AI.Domain.Enums.AiProviderType GetEmbeddingProviderType()
        => Starter.Module.AI.Domain.Enums.AiProviderType.OpenAI;
    public IAiProvider CreateDefault() => Provider;
    public IAiProvider CreateForEmbeddings() => Provider;
    public string GetEmbeddingModelId() => EmbeddingModelId;
    public string GetDefaultChatModelId() => DefaultChatModelId;
}
```

Note: `IAiProviderFactory` may need `GetDefaultChatModelId()` added (Plan 4b-2 introduces this — the classifier and rewriter need a chat model id separate from the embedding model id). Add it to the interface if missing. If the real interface has additional methods, mirror them in the fake exactly.

- [ ] **Step 3: Create `FakeCacheService`**

`boilerplateBE/tests/Starter.Api.Tests/Ai/Fakes/FakeCacheService.cs`:

```csharp
using Starter.Application.Common.Interfaces;

namespace Starter.Api.Tests.Ai.Fakes;

public sealed class FakeCacheService : ICacheService
{
    private readonly Dictionary<string, object?> _store = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        => Task.FromResult((T?)(_store.TryGetValue(key, out var v) ? v : default));

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }

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
```

- [ ] **Step 4: Create `TestChunkFactory`**

`boilerplateBE/tests/Starter.Api.Tests/Ai/Fakes/TestChunkFactory.cs`:

```csharp
using System.Reflection;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Api.Tests.Ai.Fakes;

/// <summary>
/// AiDocumentChunk has private setters (aggregate root); tests use reflection to
/// construct instances with controlled values. Only the fields reranker/rewriter
/// tests care about are settable here — extend as new tests need more fields.
/// </summary>
public static class TestChunkFactory
{
    public static AiDocumentChunk Build(
        Guid? pointId = null,
        Guid? documentId = null,
        int chunkIndex = 0,
        string? content = null,
        string chunkLevel = "child",
        int? pageNumber = null,
        string? sectionTitle = null,
        Guid? parentChunkId = null,
        Guid? tenantId = null)
    {
        var chunk = (AiDocumentChunk)Activator.CreateInstance(typeof(AiDocumentChunk), nonPublic: true)!;
        SetProp(chunk, nameof(AiDocumentChunk.Id), Guid.NewGuid());
        SetProp(chunk, nameof(AiDocumentChunk.QdrantPointId), pointId ?? Guid.NewGuid());
        SetProp(chunk, nameof(AiDocumentChunk.DocumentId), documentId ?? Guid.NewGuid());
        SetProp(chunk, nameof(AiDocumentChunk.ChunkIndex), chunkIndex);
        SetProp(chunk, nameof(AiDocumentChunk.Content), content ?? $"content-{chunkIndex}");
        SetProp(chunk, nameof(AiDocumentChunk.ChunkLevel), chunkLevel);
        SetProp(chunk, nameof(AiDocumentChunk.PageNumber), pageNumber);
        SetProp(chunk, nameof(AiDocumentChunk.SectionTitle), sectionTitle);
        SetProp(chunk, nameof(AiDocumentChunk.ParentChunkId), parentChunkId);
        SetProp(chunk, nameof(AiDocumentChunk.TenantId), tenantId ?? Guid.NewGuid());
        return chunk;
    }

    private static void SetProp(object target, string name, object? value)
    {
        var p = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Property {name} not found on {target.GetType()}");
        var setter = p.GetSetMethod(nonPublic: true)
            ?? throw new InvalidOperationException($"Property {name} has no setter");
        setter.Invoke(target, new[] { value });
    }
}
```

Also add an `EnqueueAllFail` extension to `FakeAiProvider`:

```csharp
// Inside FakeAiProvider class, alongside EnqueueThrow:
public void EnqueueAllFail(string reason)
{
    // Replace current behaviour so every ChatAsync call throws.
    _responses.Clear();
    _responses.Enqueue((_, _) => throw new InvalidOperationException(reason));
    // Also set a flag so we re-enqueue on each dequeue (simple way: override ChatAsync).
    AlwaysFail = new InvalidOperationException(reason);
}

public Exception? AlwaysFail { get; private set; }
```

Update `ChatAsync` body:

```csharp
public Task<AiChatCompletion> ChatAsync(...)
{
    Calls++;
    CallLog.Add((messages, options));
    if (AlwaysFail is not null) throw AlwaysFail;
    if (_responses.Count == 0)
        throw new InvalidOperationException("FakeAiProvider: no scripted response available.");
    var factory = _responses.Dequeue();
    return Task.FromResult(factory(messages, options));
}
```

- [ ] **Step 5: Build**

```bash
cd boilerplateBE && dotnet build
```

Expected: SUCCESS.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/Fakes/ \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/
git commit -m "test(ai): FakeAiProvider, FakeCacheService, TestChunkFactory + promote IAiProvider to public"
```

---

## Phase B — Query Rewriter (Tasks 5–8)

### Task 5: `IQueryRewriter` interface

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IQueryRewriter.cs`

- [ ] **Step 1: Create the interface**

```csharp
namespace Starter.Module.AI.Application.Services.Retrieval;

public interface IQueryRewriter
{
    /// <summary>
    /// Returns the original query at index 0 plus up to N-1 rewrites.
    /// Never throws — falls back to [originalQuery] on any failure.
    /// </summary>
    Task<IReadOnlyList<string>> RewriteAsync(
        string originalQuery,
        string? language,
        CancellationToken ct);
}
```

- [ ] **Step 2: Build**

```bash
cd boilerplateBE && dotnet build
```

Expected: SUCCESS.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IQueryRewriter.cs
git commit -m "feat(ai): IQueryRewriter contract"
```

---

### Task 6: `RuleBasedQueryRewriter` — rule layer + tests

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/RuleBasedQueryRewriter.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RuleBasedQueryRewriterTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class RuleBasedQueryRewriterTests
{
    [Fact]
    public void EnglishQuestionWord_IsStripped()
    {
        var result = RuleBasedQueryRewriter.Rewrite("what is photosynthesis?");
        result.Should().Contain("photosynthesis");
    }

    [Fact]
    public void ArabicQuestionWord_IsStripped()
    {
        var result = RuleBasedQueryRewriter.Rewrite("ما هو التمثيل الضوئي؟");
        result.Should().Contain("التمثيل الضوئي");
    }

    [Fact]
    public void TrailingPoliteTokens_AreStripped_English()
    {
        var result = RuleBasedQueryRewriter.Rewrite("tell me about oxygen please");
        result.Should().Contain("tell me about oxygen");
    }

    [Fact]
    public void TrailingPoliteTokens_AreStripped_Arabic()
    {
        var result = RuleBasedQueryRewriter.Rewrite("اذكر عناصر الهواء من فضلك");
        result.Should().Contain("اذكر عناصر الهواء");
    }

    [Fact]
    public void WhenVariantEqualsOriginal_NotDuplicated()
    {
        var result = RuleBasedQueryRewriter.Rewrite("photosynthesis");
        result.Should().HaveCount(1);
        result[0].Should().Be("photosynthesis");
    }

    [Fact]
    public void WhitespaceCollapsed()
    {
        var result = RuleBasedQueryRewriter.Rewrite("what   is    photosynthesis");
        result.Should().NotContain(s => s.Contains("  "));
    }

    [Fact]
    public void EmptyInput_ReturnsEmptyList()
    {
        RuleBasedQueryRewriter.Rewrite("").Should().BeEmpty();
        RuleBasedQueryRewriter.Rewrite("   ").Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RuleBasedQueryRewriterTests"
```

Expected: FAIL (compile error — type not found).

- [ ] **Step 3: Implement `RuleBasedQueryRewriter`**

```csharp
using System.Text.RegularExpressions;

namespace Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;

internal static class RuleBasedQueryRewriter
{
    private static readonly Regex LeadingEnglishQuestionWord = new(
        @"^\s*(what|how|when|where|why|who|which|is|are|can|does|do)\b[\s]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LeadingArabicQuestionWord = new(
        @"^\s*(هل|ماذا|ما\s+هو|ما\s+هي|ما|كيف|متى|أين|لماذا|لم|من|أي|كم)\s+",
        RegexOptions.Compiled);

    private static readonly Regex TrailingPoliteEnglish = new(
        @"\s+(please|thanks|thank you)\s*[?.!]?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TrailingPoliteArabic = new(
        @"\s+(من\s+فضلك|لو\s+سمحت|شكرا)\s*[؟?.!]?\s*$",
        RegexOptions.Compiled);

    private static readonly Regex TrailingQuestionMark = new(@"[?؟]\s*$", RegexOptions.Compiled);
    private static readonly Regex MultiWhitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Returns [original] or [original, content-only-variant] depending on whether
    /// stripping question-words / polite tokens produces a materially different string.
    /// Empty / whitespace input returns [].
    /// </summary>
    public static IReadOnlyList<string> Rewrite(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Array.Empty<string>();

        var original = MultiWhitespace.Replace(input.Trim(), " ");
        var reduced = original;

        reduced = LeadingEnglishQuestionWord.Replace(reduced, string.Empty);
        reduced = LeadingArabicQuestionWord.Replace(reduced, string.Empty);
        reduced = TrailingPoliteEnglish.Replace(reduced, string.Empty);
        reduced = TrailingPoliteArabic.Replace(reduced, string.Empty);
        reduced = TrailingQuestionMark.Replace(reduced, string.Empty);
        reduced = MultiWhitespace.Replace(reduced, " ").Trim();

        if (string.Equals(original, reduced, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(reduced))
            return new[] { original };
        return new[] { original, reduced };
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RuleBasedQueryRewriterTests"
```

Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/RuleBasedQueryRewriter.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RuleBasedQueryRewriterTests.cs
git commit -m "feat(ai): RuleBasedQueryRewriter strips question-words and polite tokens (EN + AR)"
```

---

### Task 7: `QueryRewriter` — LLM + cache + composition

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/QueryRewriter.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/QueryRewriterTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class QueryRewriterTests
{
    private static QueryRewriter Build(
        FakeAiProvider provider,
        FakeCacheService cache,
        AiRagSettings? settings = null)
    {
        var factory = new FakeAiProviderFactory(provider);
        return new QueryRewriter(
            factory,
            cache,
            Options.Create(settings ?? new AiRagSettings { EnableQueryExpansion = true }),
            NullLogger<QueryRewriter>.Instance);
    }

    [Fact]
    public async Task Disabled_ReturnsRuleLayerOutputOnly()
    {
        var provider = new FakeAiProvider();
        var cache = new FakeCacheService();
        var svc = Build(provider, cache, new AiRagSettings { EnableQueryExpansion = false });

        var result = await svc.RewriteAsync("what is photosynthesis?", "en", CancellationToken.None);

        result[0].Should().Be("what is photosynthesis?");
        provider.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Enabled_AppendsLlmVariants()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("[\"define photosynthesis\", \"photosynthesis explanation\"]");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.RewriteAsync("what is photosynthesis?", "en", CancellationToken.None);

        result.Should().Contain("what is photosynthesis?");
        result.Should().Contain("define photosynthesis");
        provider.Calls.Should().Be(1);
    }

    [Fact]
    public async Task LlmFailure_FallsBackToRuleLayer()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueThrow(new InvalidOperationException("provider down"));
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.RewriteAsync("what is photosynthesis?", "en", CancellationToken.None);

        result[0].Should().Be("what is photosynthesis?");
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LlmMalformedJson_FallsBackToRuleLayer()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("sorry, I cannot produce an array");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.RewriteAsync("what is photosynthesis?", "en", CancellationToken.None);

        result.Should().NotBeEmpty();
        result[0].Should().Be("what is photosynthesis?");
    }

    [Fact]
    public async Task SecondCallSameQuery_HitsCache_NoProviderCall()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("[\"v1\", \"v2\"]");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        _ = await svc.RewriteAsync("الضوء", "ar", CancellationToken.None);
        _ = await svc.RewriteAsync("الضوء", "ar", CancellationToken.None);

        provider.Calls.Should().Be(1);
    }

    [Fact]
    public async Task CapsAtMaxVariants()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("[\"a\",\"b\",\"c\",\"d\",\"e\"]");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache, new AiRagSettings
        {
            EnableQueryExpansion = true,
            QueryRewriteMaxVariants = 3
        });

        var result = await svc.RewriteAsync("root", "en", CancellationToken.None);

        result.Should().HaveCount(3);  // original + 2 variants
    }

    [Fact]
    public async Task ArabicVariants_AreNormalized_NotDuplicated()
    {
        var provider = new FakeAiProvider();
        // "إضاءة" and "اضاءة" differ only by alef-hamza normalization → should dedupe
        provider.EnqueueContent("[\"إضاءة\", \"اضاءة\"]");
        var cache = new FakeCacheService();
        var svc = Build(provider, cache);

        var result = await svc.RewriteAsync("الضوء", "ar", CancellationToken.None);

        result.Should().HaveCount(c => c <= 2);  // original + 1 (not 3)
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~QueryRewriterTests"
```

Expected: FAIL — type `QueryRewriter` not found.

- [ ] **Step 3: Implement `QueryRewriter`**

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Retrieval.Json;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;

internal sealed class QueryRewriter : IQueryRewriter
{
    private readonly IAiProviderFactory _factory;
    private readonly ICacheService _cache;
    private readonly AiRagSettings _settings;
    private readonly ILogger<QueryRewriter> _logger;

    public QueryRewriter(
        IAiProviderFactory factory,
        ICacheService cache,
        IOptions<AiRagSettings> settings,
        ILogger<QueryRewriter> logger)
    {
        _factory = factory;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> RewriteAsync(
        string originalQuery, string? language, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(originalQuery))
            return Array.Empty<string>();

        var ruleVariants = RuleBasedQueryRewriter.Rewrite(originalQuery);

        if (!_settings.EnableQueryExpansion)
            return ruleVariants;

        var cacheKey = BuildCacheKey(originalQuery, language);
        var cached = await _cache.GetAsync<List<string>>(cacheKey, ct);
        IReadOnlyList<string> llmVariants;
        if (cached is not null)
        {
            llmVariants = cached;
        }
        else
        {
            llmVariants = await TryCallLlmAsync(originalQuery, language, ct);
            if (llmVariants.Count > 0 && _settings.QueryRewriteCacheTtlSeconds > 0)
            {
                await _cache.SetAsync(
                    cacheKey, llmVariants.ToList(),
                    TimeSpan.FromSeconds(_settings.QueryRewriteCacheTtlSeconds), ct);
            }
        }

        return Merge(ruleVariants, llmVariants, _settings.QueryRewriteMaxVariants);
    }

    private async Task<IReadOnlyList<string>> TryCallLlmAsync(
        string query, string? language, CancellationToken ct)
    {
        try
        {
            var provider = _factory.CreateDefault();
            var langHint = language switch
            {
                "ar" => "Arabic",
                "en" => "English",
                _ => "the same language as the input"
            };

            var systemPrompt =
                "You rewrite a user's question into 2 alternative phrasings that preserve the information need. " +
                "You may receive questions in Arabic or English. Reply in the same language as the input. Do NOT translate. " +
                "Respond with a JSON array of exactly 2 alternative phrasings as strings. No commentary.";

            var userPrompt = $"Language hint: {langHint}\nOriginal question: {query}";

            var model = _settings.RerankerModel ?? GetDefaultChatModel();
            var opts = new AiChatOptions(
                Model: model,
                Temperature: 0.2,
                MaxTokens: 256,
                SystemPrompt: systemPrompt);

            var messages = new List<AiChatMessage> { new("user", userPrompt) };
            var completion = await provider.ChatAsync(messages, opts, ct);

            if (completion.Content is null || !JsonArrayExtractor.TryExtractStrings(completion.Content, out var variants))
            {
                _logger.LogWarning("QueryRewriter: LLM output did not contain a JSON array");
                return Array.Empty<string>();
            }
            return variants;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "QueryRewriter: LLM call failed; falling back to rule variants only");
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> Merge(
        IReadOnlyList<string> ruleVariants,
        IReadOnlyList<string> llmVariants,
        int cap)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>(cap);

        foreach (var v in ruleVariants)
        {
            var norm = Normalize(v);
            if (seen.Add(norm)) result.Add(v);
            if (result.Count >= cap) return result;
        }
        foreach (var v in llmVariants)
        {
            var norm = Normalize(v);
            if (seen.Add(norm)) result.Add(v);
            if (result.Count >= cap) return result;
        }
        return result;
    }

    private static string Normalize(string s) =>
        ArabicTextNormalizer.Normalize(
            s.Trim(),
            new ArabicNormalizationOptions(NormalizeTaMarbuta: true, NormalizeArabicDigits: true));

    private string BuildCacheKey(string query, string? language)
    {
        var provider = _factory.GetDefaultProviderType().ToString();
        var model = _settings.RerankerModel ?? GetDefaultChatModel();
        var hash = Sha256Hex(Normalize(query));
        var lang = string.IsNullOrWhiteSpace(language) ? "-" : language;
        return $"ai:qrw:{provider}:{model}:{lang}:{hash}";
    }

    private string GetDefaultChatModel()
    {
        // Falls back to embedding-model-id if no chat-model method exists on the factory.
        // If IAiProviderFactory gains a chat-model-id method later, swap in that call.
        // For caching purposes, any stable string works.
        return _factory.GetEmbeddingModelId();
    }

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~QueryRewriterTests"
```

Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/QueryRewriter.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/QueryRewriterTests.cs
git commit -m "feat(ai): QueryRewriter composes rule-layer + LLM-layer with Redis cache and Arabic-aware dedup"
```

---

### Task 8: Wire `QueryRewriter` into `RagRetrievalService`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

- [ ] **Step 1: Inject `IQueryRewriter` into `RagRetrievalService`**

Edit the constructor (currently at lines 24–40). Add the field, parameter, and assignment:

```csharp
    private readonly IQueryRewriter _queryRewriter;
```

Change the constructor signature to accept `IQueryRewriter queryRewriter` (insert after `IEmbeddingService embeddingService,`). Assign `_queryRewriter = queryRewriter;`.

Add `using Starter.Module.AI.Application.Services.Retrieval;` at the top of the file if not already present (it should be — double-check).

- [ ] **Step 2: Replace the single-query embed/keyword pipeline with multi-variant pipeline**

In `RetrieveForQueryAsync`, replace the block currently spanning lines 78–124 (from `var degraded = new List<string>();` to the `Combine(...)` call) with:

```csharp
        var degraded = new List<string>();

        // 1. Query rewrite (original + variants). Never throws.
        var variants = await WithTimeoutAsync(
            innerCt => _queryRewriter.RewriteAsync(queryText, language: null, innerCt),
            _settings.StageTimeoutQueryRewriteMs,
            "query-rewrite",
            degraded,
            ct);
        IReadOnlyList<string> effectiveVariants = variants is { Count: > 0 } ? variants : new[] { queryText };

        // 2. Embed all variants in one batched call — duplicates absorbed by embedding cache.
        var vectors = await WithTimeoutAsync(
            innerCt => _embeddingService.EmbedAsync(
                effectiveVariants.ToList(), innerCt, attribution: null, requestType: AiRequestType.QueryEmbedding),
            _settings.StageTimeoutEmbedMs,
            "embed-query",
            degraded,
            ct);

        if (vectors is null || vectors.Length == 0)
        {
            return new RetrievedContext([], [], 0, false, degraded);
        }

        var retrievalTopK = _settings.RetrievalTopK;
        var minHybrid = minScore ?? _settings.MinHybridScore;

        // 3. Vector search per variant.
        var vectorLists = new List<IReadOnlyList<VectorSearchHit>>(vectors.Length);
        for (var i = 0; i < vectors.Length; i++)
        {
            var v = vectors[i];
            var hits = await WithTimeoutAsync(
                innerCt => _vectorStore.SearchAsync(tenantId, v, documentFilter, retrievalTopK, innerCt),
                _settings.StageTimeoutVectorMs,
                $"vector-search[{i}]",
                degraded,
                ct);
            vectorLists.Add(hits ?? (IReadOnlyList<VectorSearchHit>)Array.Empty<VectorSearchHit>());
        }

        // 4. Keyword search per variant (keyword search already uses query text, not vectors).
        var keywordLists = new List<IReadOnlyList<KeywordSearchHit>>(effectiveVariants.Count);
        for (var i = 0; i < effectiveVariants.Count; i++)
        {
            var q = effectiveVariants[i];
            var hits = await WithTimeoutAsync(
                innerCt => _keywordSearch.SearchAsync(tenantId, q, documentFilter, retrievalTopK, innerCt),
                _settings.StageTimeoutKeywordMs,
                $"keyword-search[{i}]",
                degraded,
                ct);
            keywordLists.Add(hits ?? (IReadOnlyList<KeywordSearchHit>)Array.Empty<KeywordSearchHit>());
        }

        // 5. RRF multi-list fuse.
        var mergedHits = HybridScoreCalculator.Combine(
            vectorLists,
            keywordLists,
            _settings.VectorWeight,
            _settings.KeywordWeight,
            _settings.RrfK,
            minHybrid);
        var topKHits = mergedHits.Take(topK).ToList();
```

Note: the above replaces the older `Combine([vectorHitsEffective], [keywordHitsEffective], …)` call, so `HybridScoreCalculator.Combine`'s multi-list signature is used for real now.

- [ ] **Step 3: Register `QueryRewriter` in DI**

In `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`, after line 79 (`services.AddScoped<IRagRetrievalService, Infrastructure.Retrieval.RagRetrievalService>();`), add:

```csharp
        services.AddScoped<IQueryRewriter, Infrastructure.Retrieval.QueryRewriting.QueryRewriter>();
```

- [ ] **Step 4: Build + test**

```bash
cd boilerplateBE && dotnet build && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~Ai"
```

Expected: SUCCESS, existing retrieval tests still pass (they either don't assert on call counts or the rewriter is a no-op when `EnableQueryExpansion=false` which is the default).

If any existing test breaks because it constructs `RagRetrievalService` directly, update the construction to pass a stub rewriter:

```csharp
private sealed class NoOpQueryRewriter : IQueryRewriter
{
    public Task<IReadOnlyList<string>> RewriteAsync(string q, string? lang, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<string>>(new[] { q });
}
```

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs \
        boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/
git commit -m "feat(ai): RagRetrievalService fans out over rewritten query variants (RRF multi-list fuse)"
```

---

## Phase C — Reranker (Tasks 9–13)

### Task 9: `IReranker` interface

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IReranker.cs`

- [ ] **Step 1: Create the interface**

```csharp
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.Services.Retrieval;

public interface IReranker
{
    /// <summary>
    /// Reorders candidates by relevance. Never throws — falls back to RRF order
    /// (returns RerankResult with StrategyUsed = FallbackRrf) on any failure.
    /// </summary>
    Task<RerankResult> RerankAsync(
        string query,
        IReadOnlyList<HybridHit> candidates,
        IReadOnlyList<AiDocumentChunk> candidateChunks,
        RerankContext context,
        CancellationToken ct);
}
```

- [ ] **Step 2: Build + commit**

```bash
cd boilerplateBE && dotnet build
```

Expected: SUCCESS.

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IReranker.cs
git commit -m "feat(ai): IReranker contract with RerankResult and RerankContext"
```

---

### Task 10: `ListwiseReranker` — single-call, JSON indices + tests

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/ListwiseReranker.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ListwiseRerankerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Reranking;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class ListwiseRerankerTests
{
    private static (ListwiseReranker svc, FakeAiProvider provider, FakeCacheService cache) Build(
        AiRagSettings? settings = null)
    {
        var provider = new FakeAiProvider();
        var factory = new FakeAiProviderFactory(provider);
        var cache = new FakeCacheService();
        var svc = new ListwiseReranker(
            factory, cache,
            Options.Create(settings ?? new AiRagSettings()),
            NullLogger<ListwiseReranker>.Instance);
        return (svc, provider, cache);
    }

    private static (IReadOnlyList<HybridHit>, IReadOnlyList<AiDocumentChunk>) Build3Candidates()
    {
        var id0 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var id1 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var id2 = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var hits = new List<HybridHit>
        {
            new(id0, 0.9m, 0.8m, 0.0164m),
            new(id1, 0.7m, 0.6m, 0.0142m),
            new(id2, 0.5m, 0.4m, 0.0125m),
        };
        var chunks = new List<AiDocumentChunk>
        {
            FakeChunk(id0, "Photosynthesis uses sunlight to convert CO2 to glucose."),
            FakeChunk(id1, "Plants are green organisms."),
            FakeChunk(id2, "Water is H2O."),
        };
        return (hits, chunks);
    }

    private static AiDocumentChunk FakeChunk(Guid qdrantPointId, string content)
    {
        // Use reflection to set private properties since AiDocumentChunk has no public ctor.
        // If the entity has a factory or internal ctor, prefer that.
        var chunk = (AiDocumentChunk)Activator.CreateInstance(typeof(AiDocumentChunk), nonPublic: true)!;
        typeof(AiDocumentChunk).GetProperty("QdrantPointId")!.SetValue(chunk, qdrantPointId);
        typeof(AiDocumentChunk).GetProperty("Content")!.SetValue(chunk, content);
        typeof(AiDocumentChunk).GetProperty("DocumentId")!.SetValue(chunk, Guid.NewGuid());
        typeof(AiDocumentChunk).GetProperty("ChunkLevel")!.SetValue(chunk, "child");
        typeof(AiDocumentChunk).GetProperty("ChunkIndex")!.SetValue(chunk, 0);
        typeof(AiDocumentChunk).GetProperty("PageNumber")!.SetValue(chunk, 1);
        return chunk;
    }

    [Fact]
    public async Task HappyPath_ReordersByLlmOutput()
    {
        var (svc, provider, _) = Build();
        provider.EnqueueContent("[2, 0, 1]");
        var (hits, chunks) = Build3Candidates();

        var result = await svc.RerankAsync("what is photosynthesis", hits, chunks, CancellationToken.None);

        result.Ordered[0].ChunkId.Should().Be(hits[2].ChunkId);
        result.Ordered[1].ChunkId.Should().Be(hits[0].ChunkId);
        result.Ordered[2].ChunkId.Should().Be(hits[1].ChunkId);
        result.StrategyUsed.Should().Be(RerankStrategy.Listwise);
        result.CandidatesIn.Should().Be(3);
        result.CandidatesScored.Should().Be(3);
    }

    [Fact]
    public async Task MissingIndices_AreAppendedInRrfOrder()
    {
        var (svc, provider, _) = Build();
        provider.EnqueueContent("[2]");  // LLM only returned one index
        var (hits, chunks) = Build3Candidates();

        var result = await svc.RerankAsync("q", hits, chunks, CancellationToken.None);

        result.Ordered.Should().HaveCount(3);
        result.Ordered[0].ChunkId.Should().Be(hits[2].ChunkId);
        // remaining in RRF order (0, 1)
        result.Ordered[1].ChunkId.Should().Be(hits[0].ChunkId);
        result.Ordered[2].ChunkId.Should().Be(hits[1].ChunkId);
    }

    [Fact]
    public async Task MalformedJson_FallsBackToRrf()
    {
        var (svc, provider, _) = Build();
        provider.EnqueueContent("I don't know how to rank");
        var (hits, chunks) = Build3Candidates();

        var result = await svc.RerankAsync("q", hits, chunks, CancellationToken.None);

        result.StrategyUsed.Should().Be(RerankStrategy.FallbackRrf);
        result.Ordered.Should().Equal(hits);
    }

    [Fact]
    public async Task ProviderThrows_FallsBackToRrf()
    {
        var (svc, provider, _) = Build();
        provider.EnqueueThrow(new InvalidOperationException("boom"));
        var (hits, chunks) = Build3Candidates();

        var result = await svc.RerankAsync("q", hits, chunks, CancellationToken.None);

        result.StrategyUsed.Should().Be(RerankStrategy.FallbackRrf);
        result.Ordered.Should().Equal(hits);
    }

    [Fact]
    public async Task CacheHit_AvoidsProviderCall()
    {
        var (svc, provider, _) = Build();
        provider.EnqueueContent("[0, 1, 2]");
        var (hits, chunks) = Build3Candidates();

        _ = await svc.RerankAsync("same query", hits, chunks, CancellationToken.None);
        _ = await svc.RerankAsync("same query", hits, chunks, CancellationToken.None);

        provider.Calls.Should().Be(1);
    }

    [Fact]
    public async Task EmptyCandidates_ReturnsEmptyImmediately()
    {
        var (svc, provider, _) = Build();
        var result = await svc.RerankAsync("q", Array.Empty<HybridHit>(), Array.Empty<AiDocumentChunk>(), CancellationToken.None);
        result.Ordered.Should().BeEmpty();
        provider.Calls.Should().Be(0);
    }
}
```

Note: the `FakeChunk` helper uses reflection because `AiDocumentChunk` has private setters. If the entity exposes a public factory method, prefer it.

Also note — `RerankAsync` in the test calls a 4-arg overload `(query, hits, chunks, ct)`. The interface requires `RerankContext context`; add the overload (or always pass a default context) in the `ListwiseReranker` class. The interface `IReranker` is implemented at the composite layer; `ListwiseReranker` itself is an internal class and can have its own signature. The tests above use the internal signature.

- [ ] **Step 2: Run tests — verify fail**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~ListwiseRerankerTests"
```

Expected: FAIL (compile error).

- [ ] **Step 3: Implement `ListwiseReranker`**

```csharp
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Retrieval.Json;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.Reranking;

internal sealed class ListwiseReranker
{
    private readonly IAiProviderFactory _factory;
    private readonly ICacheService _cache;
    private readonly AiRagSettings _settings;
    private readonly ILogger<ListwiseReranker> _logger;

    public ListwiseReranker(
        IAiProviderFactory factory,
        ICacheService cache,
        IOptions<AiRagSettings> settings,
        ILogger<ListwiseReranker> logger)
    {
        _factory = factory;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<RerankResult> RerankAsync(
        string query,
        IReadOnlyList<HybridHit> candidates,
        IReadOnlyList<AiDocumentChunk> candidateChunks,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        if (candidates.Count == 0)
            return new RerankResult(candidates, RerankStrategy.Listwise, RerankStrategy.Listwise, 0, 0, 0, 0, 0, 0, 0.0);

        var key = BuildCacheKey(query, candidates);
        var cached = await _cache.GetAsync<List<int>>(key, ct);
        IReadOnlyList<int>? indices = cached;
        int tokensIn = 0, tokensOut = 0;
        int cacheHits = cached is null ? 0 : 1;

        if (indices is null)
        {
            try
            {
                var provider = _factory.CreateDefault();
                var (messages, opts) = BuildPrompt(query, candidates, candidateChunks);
                var completion = await provider.ChatAsync(messages, opts, ct);
                tokensIn = completion.InputTokens;
                tokensOut = completion.OutputTokens;
                if (completion.Content is null || !JsonArrayExtractor.TryExtractInts(completion.Content, out var parsed))
                {
                    _logger.LogWarning("ListwiseReranker: output did not contain a JSON int array");
                    return Fallback(candidates, sw);
                }
                indices = parsed;
                if (_settings.RerankCacheTtlSeconds > 0)
                    await _cache.SetAsync(key, parsed.ToList(),
                        TimeSpan.FromSeconds(_settings.RerankCacheTtlSeconds), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "ListwiseReranker: provider call failed; falling back to RRF order");
                return Fallback(candidates, sw);
            }
        }

        var ordered = ApplyIndices(candidates, indices);
        return new RerankResult(
            Ordered: ordered,
            StrategyRequested: RerankStrategy.Listwise,
            StrategyUsed: RerankStrategy.Listwise,
            CandidatesIn: candidates.Count,
            CandidatesScored: candidates.Count,
            CacheHits: cacheHits,
            LatencyMs: sw.ElapsedMilliseconds,
            TokensIn: tokensIn,
            TokensOut: tokensOut,
            UnusedRatio: 0.0);
    }

    private (List<AiChatMessage> messages, AiChatOptions opts) BuildPrompt(
        string query, IReadOnlyList<HybridHit> candidates, IReadOnlyList<AiDocumentChunk> chunks)
    {
        var system =
            "You rank document excerpts by relevance to a query. " +
            "You may see queries and excerpts in Arabic or English. " +
            "Respond with a JSON array of integer indices, most relevant first. " +
            "Include every input index exactly once. No commentary.";

        var sb = new StringBuilder();
        sb.AppendLine($"Query: {query}");
        sb.AppendLine();
        sb.AppendLine("Excerpts:");
        var byPointId = chunks.ToDictionary(c => c.QdrantPointId);
        for (var i = 0; i < candidates.Count; i++)
        {
            var c = byPointId[candidates[i].ChunkId];
            var excerpt = c.Content.Length > 500 ? c.Content[..500] : c.Content;
            sb.AppendLine($"[{i}] (page {c.PageNumber ?? 0}) {excerpt}");
        }

        var model = _settings.RerankerModel ?? _factory.GetEmbeddingModelId();
        var opts = new AiChatOptions(
            Model: model,
            Temperature: 0.0,
            MaxTokens: 128,
            SystemPrompt: system);

        return (new List<AiChatMessage> { new("user", sb.ToString()) }, opts);
    }

    private static IReadOnlyList<HybridHit> ApplyIndices(
        IReadOnlyList<HybridHit> candidates, IReadOnlyList<int> indices)
    {
        var ordered = new List<HybridHit>(candidates.Count);
        var seen = new HashSet<int>();
        foreach (var idx in indices)
        {
            if (idx < 0 || idx >= candidates.Count) continue;
            if (!seen.Add(idx)) continue;
            ordered.Add(candidates[idx]);
        }
        for (var i = 0; i < candidates.Count; i++)
        {
            if (!seen.Contains(i)) ordered.Add(candidates[i]);
        }
        return ordered;
    }

    private static RerankResult Fallback(IReadOnlyList<HybridHit> candidates, Stopwatch sw) =>
        new(candidates, RerankStrategy.Listwise, RerankStrategy.FallbackRrf, candidates.Count, 0, 0, sw.ElapsedMilliseconds, 0, 0, 0.0);

    private string BuildCacheKey(string query, IReadOnlyList<HybridHit> candidates)
    {
        var provider = _factory.GetDefaultProviderType().ToString();
        var model = _settings.RerankerModel ?? _factory.GetEmbeddingModelId();
        var ids = string.Join("|", candidates.Select(c => c.ChunkId.ToString("N")).OrderBy(s => s));
        var hash = Sha256Hex($"{query}|{ids}");
        return $"ai:rerank:lw:{provider}:{model}:{hash}";
    }

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~ListwiseRerankerTests"
```

Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/ListwiseReranker.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ListwiseRerankerTests.cs
git commit -m "feat(ai): ListwiseReranker — single LLM call returns JSON indices, RRF fallback, cache"
```

---

### Task 11: `PointwiseReranker` — parallel per-pair scoring + tests

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/PointwiseReranker.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/PointwiseRerankerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Reranking;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class PointwiseRerankerTests
{
    private static AiDocumentChunk FakeChunk(Guid qdrantPointId, string content, int index = 0)
    {
        var chunk = (AiDocumentChunk)Activator.CreateInstance(typeof(AiDocumentChunk), nonPublic: true)!;
        typeof(AiDocumentChunk).GetProperty("QdrantPointId")!.SetValue(chunk, qdrantPointId);
        typeof(AiDocumentChunk).GetProperty("Content")!.SetValue(chunk, content);
        typeof(AiDocumentChunk).GetProperty("DocumentId")!.SetValue(chunk, Guid.NewGuid());
        typeof(AiDocumentChunk).GetProperty("ChunkLevel")!.SetValue(chunk, "child");
        typeof(AiDocumentChunk).GetProperty("ChunkIndex")!.SetValue(chunk, index);
        typeof(AiDocumentChunk).GetProperty("PageNumber")!.SetValue(chunk, 1);
        return chunk;
    }

    private static (List<HybridHit> hits, List<AiDocumentChunk> chunks) Build(int n)
    {
        var hits = new List<HybridHit>();
        var chunks = new List<AiDocumentChunk>();
        for (var i = 0; i < n; i++)
        {
            var id = Guid.Parse($"00000000-0000-0000-0000-0000000000{i:D2}");
            hits.Add(new HybridHit(id, 0.5m, 0.5m, 0.02m - 0.001m * i));
            chunks.Add(FakeChunk(id, $"excerpt {i}", i));
        }
        return (hits, chunks);
    }

    private static PointwiseReranker BuildSvc(FakeAiProvider provider, FakeCacheService cache, AiRagSettings? s = null)
    {
        var factory = new FakeAiProviderFactory(provider);
        return new PointwiseReranker(
            factory, cache,
            Options.Create(s ?? new AiRagSettings { PointwiseMaxParallelism = 2, MinPointwiseScore = 0.3m }),
            NullLogger<PointwiseReranker>.Instance);
    }

    [Fact]
    public async Task Scores_ByDescending_Drops_BelowCutoff()
    {
        var provider = new FakeAiProvider();
        // 3 candidates; scores 0.9, 0.2, 0.7. 0.2 should be dropped (< MinPointwiseScore=0.3).
        provider.EnqueueContent("{\"score\": 0.9, \"reason\": \"very relevant\"}");
        provider.EnqueueContent("{\"score\": 0.2, \"reason\": \"off topic\"}");
        provider.EnqueueContent("{\"score\": 0.7, \"reason\": \"related\"}");
        var cache = new FakeCacheService();
        var (hits, chunks) = Build(3);
        var svc = BuildSvc(provider, cache);

        var result = await svc.RerankAsync("q", hits, chunks, CancellationToken.None);

        result.Ordered.Should().HaveCount(2);
        result.Ordered[0].ChunkId.Should().Be(hits[0].ChunkId);  // 0.9
        result.Ordered[1].ChunkId.Should().Be(hits[2].ChunkId);  // 0.7
        result.StrategyUsed.Should().Be(RerankStrategy.Pointwise);
        result.CandidatesIn.Should().Be(3);
        result.CandidatesScored.Should().Be(3);
    }

    [Fact]
    public async Task PairCache_ReusesAcrossCalls()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("{\"score\": 0.8, \"reason\": \"ok\"}");
        provider.EnqueueContent("{\"score\": 0.6, \"reason\": \"ok\"}");
        var cache = new FakeCacheService();
        var (hits, chunks) = Build(2);
        var svc = BuildSvc(provider, cache);

        _ = await svc.RerankAsync("same q", hits, chunks, CancellationToken.None);
        _ = await svc.RerankAsync("same q", hits, chunks, CancellationToken.None);

        provider.Calls.Should().Be(2);  // only the first run populated cache
    }

    [Fact]
    public async Task TooManyFailures_FallsBackToRrf()
    {
        var provider = new FakeAiProvider();
        // 4 candidates; 3 of 4 calls fail (75% > 25% threshold) → fallback.
        provider.EnqueueThrow(new InvalidOperationException("x"));
        provider.EnqueueThrow(new InvalidOperationException("x"));
        provider.EnqueueThrow(new InvalidOperationException("x"));
        provider.EnqueueContent("{\"score\": 0.9, \"reason\": \"ok\"}");
        var cache = new FakeCacheService();
        var (hits, chunks) = Build(4);
        var svc = BuildSvc(provider, cache, new AiRagSettings
        {
            PointwiseMaxParallelism = 2,
            MinPointwiseScore = 0.3m,
            PointwiseMaxFailureRatio = 0.25m
        });

        var result = await svc.RerankAsync("q", hits, chunks, CancellationToken.None);

        result.StrategyUsed.Should().Be(RerankStrategy.FallbackRrf);
        result.Ordered.Should().Equal(hits);
    }

    [Fact]
    public async Task EmptyCandidates_ReturnsEmpty()
    {
        var provider = new FakeAiProvider();
        var svc = BuildSvc(provider, new FakeCacheService());
        var result = await svc.RerankAsync("q", Array.Empty<HybridHit>(), Array.Empty<AiDocumentChunk>(), CancellationToken.None);
        result.Ordered.Should().BeEmpty();
        result.StrategyUsed.Should().Be(RerankStrategy.Pointwise);
    }

    [Fact]
    public async Task MalformedScore_UsesRrfFallbackScore_ForThatPair()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("{\"score\": 0.9, \"reason\": \"ok\"}");
        provider.EnqueueContent("not a json");           // this pair falls back to RRF-derived score
        provider.EnqueueContent("{\"score\": 0.5, \"reason\": \"ok\"}");
        var cache = new FakeCacheService();
        var (hits, chunks) = Build(3);
        var svc = BuildSvc(provider, cache);

        var result = await svc.RerankAsync("q", hits, chunks, CancellationToken.None);

        // Hit 0 (score 0.9) should be first. Hit 2 (score 0.5) should beat the fallback score for hit 1.
        result.Ordered.Should().NotBeEmpty();
        result.Ordered[0].ChunkId.Should().Be(hits[0].ChunkId);
        result.StrategyUsed.Should().Be(RerankStrategy.Pointwise);
    }
}
```

- [ ] **Step 2: Run tests — verify fail**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~PointwiseRerankerTests"
```

Expected: FAIL (compile error).

- [ ] **Step 3: Implement `PointwiseReranker`**

```csharp
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.Reranking;

internal sealed class PointwiseReranker
{
    private readonly IAiProviderFactory _factory;
    private readonly ICacheService _cache;
    private readonly AiRagSettings _settings;
    private readonly ILogger<PointwiseReranker> _logger;

    public PointwiseReranker(
        IAiProviderFactory factory,
        ICacheService cache,
        IOptions<AiRagSettings> settings,
        ILogger<PointwiseReranker> logger)
    {
        _factory = factory;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<RerankResult> RerankAsync(
        string query,
        IReadOnlyList<HybridHit> candidates,
        IReadOnlyList<AiDocumentChunk> candidateChunks,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        if (candidates.Count == 0)
            return new RerankResult(candidates, RerankStrategy.Pointwise, RerankStrategy.Pointwise, 0, 0, 0, 0, 0, 0, 0.0);

        var byPointId = candidateChunks.ToDictionary(c => c.QdrantPointId);
        var semaphore = new SemaphoreSlim(Math.Max(1, _settings.PointwiseMaxParallelism));
        var scores = new decimal[candidates.Count];
        var failures = 0;
        var cacheHits = 0;
        var tokensIn = 0;
        var tokensOut = 0;

        var tasks = new List<Task>(candidates.Count);
        for (var i = 0; i < candidates.Count; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var result = await ScoreOneAsync(query, candidates[idx], byPointId[candidates[idx].ChunkId], idx, ct);
                    scores[idx] = result.Score;
                    if (result.CacheHit) Interlocked.Increment(ref cacheHits);
                    if (result.Failed) Interlocked.Increment(ref failures);
                    Interlocked.Add(ref tokensIn, result.TokensIn);
                    Interlocked.Add(ref tokensOut, result.TokensOut);
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks);

        var failureRatio = (double)failures / candidates.Count;
        if (failureRatio > _settings.PointwiseMaxFailureRatio)
        {
            _logger.LogWarning(
                "PointwiseReranker: {Failed}/{Total} pairs failed (ratio {Ratio:P0}); falling back to RRF",
                failures, candidates.Count, failureRatio);
            return new RerankResult(
                candidates, RerankStrategy.Pointwise, RerankStrategy.FallbackRrf,
                candidates.Count, candidates.Count - failures, cacheHits,
                sw.ElapsedMilliseconds, tokensIn, tokensOut, 0.0);
        }

        var ordered = Enumerable.Range(0, candidates.Count)
            .OrderByDescending(i => scores[i])
            .ThenBy(i => i)
            .Where(i => scores[i] >= _settings.MinPointwiseScore)
            .Select(i => candidates[i])
            .ToList();

        var unused = candidates.Count == 0 ? 0.0 : 1.0 - (double)ordered.Count / candidates.Count;

        return new RerankResult(
            Ordered: ordered,
            StrategyRequested: RerankStrategy.Pointwise,
            StrategyUsed: RerankStrategy.Pointwise,
            CandidatesIn: candidates.Count,
            CandidatesScored: candidates.Count - failures,
            CacheHits: cacheHits,
            LatencyMs: sw.ElapsedMilliseconds,
            TokensIn: tokensIn,
            TokensOut: tokensOut,
            UnusedRatio: unused);
    }

    private async Task<PairResult> ScoreOneAsync(
        string query, HybridHit hit, AiDocumentChunk chunk, int rank, CancellationToken ct)
    {
        var key = BuildPairKey(query, hit.ChunkId);
        var cached = await _cache.GetAsync<decimal?>(key, ct);
        if (cached.HasValue)
            return new PairResult(cached.Value, CacheHit: true, Failed: false, 0, 0);

        try
        {
            var provider = _factory.CreateDefault();
            var (messages, opts) = BuildPrompt(query, chunk);
            var completion = await provider.ChatAsync(messages, opts, ct);
            if (completion.Content is null || !TryParseScore(completion.Content, out var score))
            {
                // Couldn't parse — use RRF-derived fallback score but mark as failed for ratio calc.
                return new PairResult(RrfFallbackScore(rank), CacheHit: false, Failed: true, completion.InputTokens, completion.OutputTokens);
            }
            if (_settings.RerankCacheTtlSeconds > 0)
                await _cache.SetAsync<decimal?>(key, score,
                    TimeSpan.FromSeconds(_settings.RerankCacheTtlSeconds), ct);
            return new PairResult(score, CacheHit: false, Failed: false, completion.InputTokens, completion.OutputTokens);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "PointwiseReranker: pair scoring failed for chunk {ChunkId}", hit.ChunkId);
            return new PairResult(RrfFallbackScore(rank), CacheHit: false, Failed: true, 0, 0);
        }
    }

    private decimal RrfFallbackScore(int rank) => 1m / (_settings.RrfK + rank + 1);

    private static bool TryParseScore(string content, out decimal score)
    {
        score = 0m;
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start) return false;
        var slice = content[start..(end + 1)];
        try
        {
            using var doc = JsonDocument.Parse(slice);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            if (!doc.RootElement.TryGetProperty("score", out var s)) return false;
            if (s.ValueKind != JsonValueKind.Number) return false;
            if (!s.TryGetDecimal(out var dec)) return false;
            if (dec < 0m) dec = 0m;
            if (dec > 1m) dec = 1m;
            score = dec;
            return true;
        }
        catch (JsonException) { return false; }
    }

    private (List<AiChatMessage> messages, AiChatOptions opts) BuildPrompt(string query, AiDocumentChunk chunk)
    {
        var system =
            "You score how relevant a document excerpt is to a user query. " +
            "You may see queries and excerpts in Arabic or English. " +
            "Respond with a single JSON object: {\"score\": <float 0.0-1.0>, \"reason\": \"<max 60 chars>\"}. " +
            "No commentary.";
        var excerpt = chunk.Content.Length > 500 ? chunk.Content[..500] : chunk.Content;
        var user = $"Query: {query}\nExcerpt (page {chunk.PageNumber ?? 0}): {excerpt}";

        var model = _settings.RerankerModel ?? _factory.GetEmbeddingModelId();
        var opts = new AiChatOptions(
            Model: model,
            Temperature: 0.0,
            MaxTokens: 64,
            SystemPrompt: system);

        return (new List<AiChatMessage> { new("user", user) }, opts);
    }

    private string BuildPairKey(string query, Guid chunkId)
    {
        var provider = _factory.GetDefaultProviderType().ToString();
        var model = _settings.RerankerModel ?? _factory.GetEmbeddingModelId();
        var qhash = Sha256Hex(query);
        return $"ai:rerank:pw:{provider}:{model}:{qhash}:{chunkId:N}";
    }

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record PairResult(decimal Score, bool CacheHit, bool Failed, int TokensIn, int TokensOut);
}
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~PointwiseRerankerTests"
```

Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/PointwiseReranker.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/PointwiseRerankerTests.cs
git commit -m "feat(ai): PointwiseReranker — parallel per-pair scoring, score cutoff, RRF fallback on too-many-failures"
```

---

### Task 12: RerankStrategySelector

Selector maps `(QuestionType?, RerankStrategy setting, RerankContext override)` to a concrete strategy. Pure code, no I/O — trivial to test exhaustively.

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/RerankStrategySelector.cs`
- Create test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RerankStrategySelectorTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
// RerankStrategySelectorTests.cs
using FluentAssertions;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Reranking;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public class RerankStrategySelectorTests
{
    private static RerankStrategySelector Selector(RerankStrategy cfg) =>
        new(new AiRagSettings { RerankStrategy = cfg });

    [Fact]
    public void Override_takes_precedence_over_settings()
    {
        var s = Selector(RerankStrategy.Listwise);
        var ctx = new RerankContext(null, RerankStrategy.Off);

        s.Resolve(ctx).Should().Be(RerankStrategy.Off);
    }

    [Fact]
    public void Off_setting_returns_off()
    {
        Selector(RerankStrategy.Off).Resolve(new RerankContext(QuestionType.Reasoning, null))
            .Should().Be(RerankStrategy.Off);
    }

    [Fact]
    public void Listwise_setting_returns_listwise()
    {
        Selector(RerankStrategy.Listwise).Resolve(new RerankContext(QuestionType.Reasoning, null))
            .Should().Be(RerankStrategy.Listwise);
    }

    [Fact]
    public void Pointwise_setting_returns_pointwise()
    {
        Selector(RerankStrategy.Pointwise).Resolve(new RerankContext(QuestionType.Greeting, null))
            .Should().Be(RerankStrategy.Pointwise);
    }

    [Theory]
    [InlineData(QuestionType.Greeting, RerankStrategy.Off)]
    [InlineData(QuestionType.Reasoning, RerankStrategy.Pointwise)]
    [InlineData(QuestionType.Definition, RerankStrategy.Listwise)]
    [InlineData(QuestionType.Listing, RerankStrategy.Listwise)]
    [InlineData(QuestionType.Other, RerankStrategy.Listwise)]
    public void Auto_routes_on_question_type(QuestionType qt, RerankStrategy expected)
    {
        Selector(RerankStrategy.Auto).Resolve(new RerankContext(qt, null))
            .Should().Be(expected);
    }

    [Fact]
    public void Auto_with_null_question_type_defaults_to_listwise()
    {
        Selector(RerankStrategy.Auto).Resolve(new RerankContext(null, null))
            .Should().Be(RerankStrategy.Listwise);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RerankStrategySelectorTests"
```

Expected: FAIL — `RerankStrategySelector` not defined.

- [ ] **Step 3: Implement**

```csharp
// RerankStrategySelector.cs
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.Reranking;

internal sealed class RerankStrategySelector
{
    private readonly AiRagSettings _settings;

    public RerankStrategySelector(AiRagSettings settings)
    {
        _settings = settings;
    }

    public RerankStrategy Resolve(RerankContext ctx)
    {
        if (ctx.StrategyOverride is { } o)
            return o;

        var cfg = _settings.RerankStrategy;
        if (cfg != RerankStrategy.Auto)
            return cfg;

        return ctx.QuestionType switch
        {
            QuestionType.Greeting => RerankStrategy.Off,
            QuestionType.Reasoning => RerankStrategy.Pointwise,
            QuestionType.Definition => RerankStrategy.Listwise,
            QuestionType.Listing => RerankStrategy.Listwise,
            QuestionType.Other => RerankStrategy.Listwise,
            null => RerankStrategy.Listwise,
            _ => RerankStrategy.Listwise
        };
    }
}
```

Note the constructor takes `AiRagSettings` directly (not `IOptions<>`). The composite `Reranker` (Task 13) resolves `IOptions<AiRagSettings>.Value` once and hands the snapshot down — keeps selector trivially testable.

- [ ] **Step 4: Run tests — expect PASS**

Expected: PASS (9 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/RerankStrategySelector.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RerankStrategySelectorTests.cs
git commit -m "feat(ai): RerankStrategySelector — routes strategy based on settings + question type"
```

---

### Task 13: Reranker composite (IReranker implementation) + DI registration

The `Reranker` orchestrates: selector → strategy → fallback chain. Listwise and Pointwise are internal strategies; only `Reranker` implements `IReranker`.

**Fallback chain:**
- Pointwise → Listwise → FallbackRrf (passthrough)
- Listwise → FallbackRrf (passthrough)
- Off → passthrough (no fallback needed)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/Reranker.cs`
- Create test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RerankerTests.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs` (add DI registrations)

- [ ] **Step 1: Write failing test**

```csharp
// RerankerTests.cs
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Services.Providers;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Reranking;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public class RerankerTests
{
    private static (IReadOnlyList<HybridHit> hits, IReadOnlyList<AiDocumentChunk> chunks) FakeBatch(int n)
    {
        var hits = new List<HybridHit>(n);
        var chunks = new List<AiDocumentChunk>(n);
        for (var i = 0; i < n; i++)
        {
            var pointId = Guid.NewGuid();
            hits.Add(new HybridHit(pointId, 1.0m - i * 0.01m, i, vectorRank: i, keywordRank: null));
            chunks.Add(TestChunkFactory.Build(pointId: pointId, chunkIndex: i, content: $"chunk {i}"));
        }
        return (hits, chunks);
    }

    [Fact]
    public async Task Off_strategy_passes_through_unchanged()
    {
        var settings = new AiRagSettings { RerankStrategy = RerankStrategy.Off };
        var r = BuildReranker(settings, scriptedChat: null);

        var (hits, chunks) = FakeBatch(5);
        var result = await r.RerankAsync("q", hits, chunks, new RerankContext(null, null), CancellationToken.None);

        result.Ordered.Select(h => h.ChunkId).Should().Equal(hits.Select(h => h.ChunkId));
        result.StrategyUsed.Should().Be(RerankStrategy.Off);
        result.CandidatesScored.Should().Be(0);
    }

    [Fact]
    public async Task Listwise_failure_falls_back_to_rrf()
    {
        var settings = new AiRagSettings { RerankStrategy = RerankStrategy.Listwise };
        var provider = new FakeAiProvider();
        provider.EnqueueThrow(new InvalidOperationException("LLM blew up"));
        var r = BuildReranker(settings, scriptedChat: provider);

        var (hits, chunks) = FakeBatch(5);
        var result = await r.RerankAsync("q", hits, chunks, new RerankContext(null, null), CancellationToken.None);

        result.StrategyUsed.Should().Be(RerankStrategy.FallbackRrf);
        result.Ordered.Select(h => h.ChunkId).Should().Equal(hits.Select(h => h.ChunkId));
    }

    [Fact]
    public async Task Pointwise_failure_falls_back_to_listwise_then_rrf()
    {
        // Pointwise fails → aborts to RRF (returns StrategyUsed=FallbackRrf) → composite
        // treats that as fall-through → Listwise also fails → composite returns FallbackRrf.
        var settings = new AiRagSettings
        {
            RerankStrategy = RerankStrategy.Pointwise,
            PointwiseMaxFailureRatio = 0.0 // abort on any failure
        };
        var provider = new FakeAiProvider();
        provider.EnqueueAllFail("boom"); // queues repeated throws for every ChatAsync call
        var r = BuildReranker(settings, scriptedChat: provider);

        var (hits, chunks) = FakeBatch(3);
        var result = await r.RerankAsync("q", hits, chunks, new RerankContext(null, null), CancellationToken.None);

        result.StrategyUsed.Should().Be(RerankStrategy.FallbackRrf);
    }

    [Fact]
    public async Task Context_override_wins()
    {
        var settings = new AiRagSettings { RerankStrategy = RerankStrategy.Listwise };
        var r = BuildReranker(settings, scriptedChat: null);

        var (hits, chunks) = FakeBatch(3);
        var result = await r.RerankAsync("q", hits, chunks,
            new RerankContext(null, RerankStrategy.Off), CancellationToken.None);

        result.StrategyUsed.Should().Be(RerankStrategy.Off);
    }

    private static IReranker BuildReranker(AiRagSettings settings, FakeAiProvider? scriptedChat)
    {
        var factory = new FakeAiProviderFactory(scriptedChat ?? new FakeAiProvider());
        var cache = new FakeCacheService();
        var opts = Options.Create(settings);
        var listwise = new ListwiseReranker(factory, cache, opts, NullLogger<ListwiseReranker>.Instance);
        var pointwise = new PointwiseReranker(factory, cache, opts, NullLogger<PointwiseReranker>.Instance);
        var selector = new RerankStrategySelector(settings);
        return new Reranker(selector, listwise, pointwise, opts, NullLogger<Reranker>.Instance);
    }
}
```

`EnqueueAllFail(string reason)` is an extension on `FakeAiProvider` that makes every subsequent `ChatAsync` call throw `new InvalidOperationException(reason)`. Extend the `FakeAiProvider` test helper from Task 4 with this one-line method. `TestChunkFactory.Build(pointId, chunkIndex, content)` is a new helper in the shared test fakes (Task 4) that uses reflection to construct `AiDocumentChunk` with the minimum fields needed for rerank tests. Add it to the shared fakes file alongside `FakeAiProvider` and `FakeCacheService`.

- [ ] **Step 2: Run test to verify it fails**

Expected: FAIL — `Reranker` type missing.

- [ ] **Step 3: Implement**

```csharp
// Reranker.cs
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.Reranking;

internal sealed class Reranker : IReranker
{
    private readonly RerankStrategySelector _selector;
    private readonly ListwiseReranker _listwise;
    private readonly PointwiseReranker _pointwise;
    private readonly AiRagSettings _settings;
    private readonly ILogger<Reranker> _logger;

    public Reranker(
        RerankStrategySelector selector,
        ListwiseReranker listwise,
        PointwiseReranker pointwise,
        IOptions<AiRagSettings> settings,
        ILogger<Reranker> logger)
    {
        _selector = selector;
        _listwise = listwise;
        _pointwise = pointwise;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<RerankResult> RerankAsync(
        string query,
        IReadOnlyList<HybridHit> candidates,
        IReadOnlyList<AiDocumentChunk> candidateChunks,
        RerankContext ctx,
        CancellationToken ct)
    {
        var requested = _selector.Resolve(ctx);

        if (candidates.Count == 0 || requested == RerankStrategy.Off)
            return Passthrough(candidates, requested);

        if (requested == RerankStrategy.Pointwise)
        {
            RerankResult? pw = null;
            try
            {
                pw = await _pointwise.RerankAsync(query, candidates, candidateChunks, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Pointwise threw; falling back to Listwise");
            }

            // Pointwise aborts by returning StrategyUsed=FallbackRrf — treat that as fall-through to Listwise.
            if (pw is not null && pw.StrategyUsed != RerankStrategy.FallbackRrf)
                return pw with { StrategyRequested = requested };

            _logger.LogInformation("Pointwise unavailable or aborted; falling through to Listwise");
        }

        if (requested == RerankStrategy.Listwise || requested == RerankStrategy.Pointwise)
        {
            try
            {
                var lw = await _listwise.RerankAsync(query, candidates, candidateChunks, ct);
                return lw with { StrategyRequested = requested };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Listwise threw; falling back to RRF order");
            }
        }

        return Passthrough(candidates, requested, used: RerankStrategy.FallbackRrf);
    }

    private static RerankResult Passthrough(
        IReadOnlyList<HybridHit> candidates,
        RerankStrategy requested,
        RerankStrategy? used = null) =>
        new(
            Ordered: candidates,
            StrategyRequested: requested,
            StrategyUsed: used ?? requested,
            CandidatesIn: candidates.Count,
            CandidatesScored: 0,
            CacheHits: 0,
            LatencyMs: 0,
            TokensIn: 0,
            TokensOut: 0,
            UnusedRatio: 0.0);
}
```

Note: `Listwise`/`Pointwise` both return a `RerankResult` whose `StrategyRequested` is set to their own strategy (Task 10/11). The composite overwrites `StrategyRequested` with the selector's requested strategy using `with` so downstream telemetry reflects the user-facing request. The `StrategyUsed` field preserves what actually ran (which may be `FallbackRrf` when Pointwise aborts).

- [ ] **Step 4: DI registration**

Modify `AIModule.cs` in the AI module registrations:

```csharp
// Reranking
services.AddSingleton<RerankStrategySelector>();
services.AddScoped<ListwiseReranker>();
services.AddScoped<PointwiseReranker>();
services.AddScoped<IReranker, Reranker>();
```

Note: `RerankStrategySelector` can be singleton because `AiRagSettings` is pulled via snapshot — but if options are hot-reloaded it needs to be scoped. Go with **scoped** to match the existing AI settings lifetime pattern.

```csharp
services.AddScoped<RerankStrategySelector>(sp =>
    new RerankStrategySelector(sp.GetRequiredService<IOptions<AiRagSettings>>().Value));
```

- [ ] **Step 5: Run tests — expect PASS**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RerankerTests"
```

Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/Reranker.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/ListwiseReranker.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Reranking/PointwiseReranker.cs \
        boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RerankerTests.cs
git commit -m "feat(ai): Reranker composite — selector + fallback chain (Pointwise → Listwise → RRF)"
```

---

### Task 14: Wire Reranker into RagRetrievalService

Insert the reranker stage after RRF fusion, before `TrimToBudget`. Pool size comes from the active strategy:
- Listwise: `TopK × ListwisePoolMultiplier` (default 3)
- Pointwise: `TopK × PointwisePoolMultiplier` (default 2)
- Off: pool == `TopK` (no oversampling)

Trim candidate list to pool size before calling reranker. `RerankContext` is threaded through from the classifier (added in Task 17) — for now Task 14 passes a default `RerankContext(null, null)`.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs`
- Modify test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RagRetrievalServiceTests.cs`

- [ ] **Step 1: Write failing integration test**

```csharp
[Fact]
public async Task Retrieve_reranker_reorders_fused_candidates()
{
    // Set RerankStrategy=Listwise, script AI provider to return "[1, 0]" indices
    // (swap first two). Assert returned order reflects rerank, not RRF.
    var settings = new AiRagSettings
    {
        TopK = 2,
        RerankStrategy = RerankStrategy.Listwise,
        ListwisePoolMultiplier = 2 // pool=4
    };

    // Build a service with: 2 vector hits, 2 keyword hits → 4 fused
    // AI provider scripted to return "[1, 0, 2, 3]" as the rerank JSON
    // Seed 4 chunks with distinguishable content
    // Call RetrieveAsync("query", ...)
    // Assert result order == [hit1, hit0]
    // Assert telemetry includes "rerank.listwise"
    // ... (full setup mirrors existing RagRetrievalServiceTests fixtures)
}
```

Adapt the existing `RagRetrievalServiceTests` fixture patterns (builds `RagRetrievalService` with fakes for vector store, keyword search, embedding provider). Add a scripted AI provider for the rerank call.

- [ ] **Step 2: Run test to verify it fails**

Expected: FAIL — rerank stage not yet wired.

- [ ] **Step 3: Modify RagRetrievalService**

Add constructor dependency:

```csharp
public RagRetrievalService(
    // ...existing deps...
    IReranker reranker,
    // ...
)
```

After RRF fusion but before trimming:

```csharp
// Existing: var fused = _hybridScore.Combine(...);
// Existing: var candidates = fused.Take(retrievalTopK).ToList();

// NEW: determine pool size based on resolved strategy
var rerankCtx = new RerankContext(questionType: null, strategyOverride: null); // Task 17 will fill questionType
var plannedStrategy = _rerankSelector.Resolve(rerankCtx);
var poolMultiplier = plannedStrategy switch
{
    RerankStrategy.Listwise => _settings.ListwisePoolMultiplier,
    RerankStrategy.Pointwise => _settings.PointwisePoolMultiplier,
    _ => 1
};
var poolSize = Math.Max(_settings.TopK, _settings.TopK * poolMultiplier);
var pool = fused.Take(poolSize).ToList();

// Fetch chunk entities once so the reranker can access content + metadata without
// its own DB round trip. Retrieval already loads AiDocumentChunk rows keyed by
// QdrantPointId elsewhere in the pipeline — reuse that map here.
var poolChunks = pool
    .Select(h => chunkByPointId[h.ChunkId])
    .ToList();

// NEW: rerank
var rerankResult = await WithTimeoutAsync(
    () => _reranker.RerankAsync(query, pool, poolChunks, rerankCtx, ct),
    _settings.StageTimeoutRerankMs, // NEW setting; default 8000
    "rerank",
    degradedStages,
    ct);

// Telemetry: emit rerank.* metrics
EmitRerankMetrics(rerankResult);

var ordered = rerankResult.Ordered;
var topK = ordered.Take(_settings.TopK).ToList();
// continue to TrimToBudget with topK
```

**Inject `RerankStrategySelector`** too (the retrieval service needs to know planned strategy to size the pool). Both are injected.

**Add new setting** `StageTimeoutRerankMs` to `AiRagSettings` (default 8000).

**EmitRerankMetrics** writes to `ILogger` structured fields and `RetrievalMetrics` (the existing metrics class used for embedding/search latency). Add these fields to `RetrievalMetrics`:
- `RerankStrategyRequested` (string)
- `RerankStrategyUsed` (string)
- `RerankLatencyMs` (long)
- `RerankTokensIn` / `RerankTokensOut` (int)
- `RerankCacheHits` (int)
- `RerankUnusedRatio` (double)

- [ ] **Step 4: Run tests — expect PASS**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RagRetrievalServiceTests"
```

Expected: PASS including the new rerank test.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RetrievalMetrics.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RagRetrievalServiceTests.cs
git commit -m "feat(ai): wire Reranker into RagRetrievalService with pool sizing + telemetry"
```

---

## Phase D — Question Classifier

Classifier tags the incoming query with a `QuestionType` (Greeting, Definition, Listing, Reasoning, Other). Greetings skip retrieval entirely; the other tags feed the `RerankStrategySelector`.

Classifier is a composite: regex fast path → LLM slow path (only when regex can't decide) → Redis cache. Arabic and English patterns are both first-class.

### Task 15: IQuestionClassifier + RegexQuestionClassifier

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IQuestionClassifier.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Classification/RegexQuestionClassifier.cs`
- Create test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RegexQuestionClassifierTests.cs`

- [ ] **Step 1: Write IQuestionClassifier interface**

```csharp
// Application/Services/Retrieval/IQuestionClassifier.cs
namespace Starter.Module.AI.Application.Services.Retrieval;

public enum QuestionType
{
    Greeting,
    Definition,
    Listing,
    Reasoning,
    Other
}

public interface IQuestionClassifier
{
    Task<QuestionType?> ClassifyAsync(string query, CancellationToken ct);
}
```

(Task 1 already introduces `QuestionType` — keep this definition identical. If Task 1 put it in `Infrastructure.Retrieval`, move it here instead; application-layer types must live in Application.)

- [ ] **Step 2: Write failing test for regex classifier**

```csharp
// RegexQuestionClassifierTests.cs
using FluentAssertions;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Classification;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public class RegexQuestionClassifierTests
{
    [Theory]
    [InlineData("hi", QuestionType.Greeting)]
    [InlineData("hello there!", QuestionType.Greeting)]
    [InlineData("مرحبا", QuestionType.Greeting)]
    [InlineData("السلام عليكم", QuestionType.Greeting)]
    [InlineData("what is refund policy?", QuestionType.Definition)]
    [InlineData("ما هي سياسة الإرجاع", QuestionType.Definition)]
    [InlineData("list all products", QuestionType.Listing)]
    [InlineData("show me the customers", QuestionType.Listing)]
    [InlineData("اعرض لي العملاء", QuestionType.Listing)]
    [InlineData("why did the order fail?", QuestionType.Reasoning)]
    [InlineData("لماذا فشل الطلب", QuestionType.Reasoning)]
    public void Classifies_query(string input, QuestionType expected)
    {
        RegexQuestionClassifier.TryClassify(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("the forecast for Q3 is ambiguous based on these factors")]
    [InlineData("some unrelated narrative content")]
    public void Returns_null_when_no_pattern_matches(string input)
    {
        RegexQuestionClassifier.TryClassify(input).Should().BeNull();
    }
}
```

- [ ] **Step 3: Run test — expect FAIL**

- [ ] **Step 4: Implement**

```csharp
// RegexQuestionClassifier.cs
using System.Text.RegularExpressions;
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval.Classification;

internal static partial class RegexQuestionClassifier
{
    [GeneratedRegex(@"^\s*(hi|hello|hey|greetings|good\s+(morning|afternoon|evening))\b", RegexOptions.IgnoreCase)]
    private static partial Regex GreetingEn();

    [GeneratedRegex(@"^\s*(مرحب[اًا]|السلام\s+عليكم|أهلا|اهلا|صباح\s+الخير|مساء\s+الخير)", RegexOptions.IgnoreCase)]
    private static partial Regex GreetingAr();

    [GeneratedRegex(@"\b(what\s+is|define|definition\s+of|meaning\s+of)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DefinitionEn();

    [GeneratedRegex(@"(ما\s+هي|ما\s+هو|تعريف|معنى)", RegexOptions.IgnoreCase)]
    private static partial Regex DefinitionAr();

    [GeneratedRegex(@"\b(list|show|display|give\s+me|enumerate)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ListingEn();

    [GeneratedRegex(@"(اعرض|اسرد|قائمة|اذكر|اعطني)", RegexOptions.IgnoreCase)]
    private static partial Regex ListingAr();

    [GeneratedRegex(@"\b(why|how\s+come|explain|compare)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ReasoningEn();

    [GeneratedRegex(@"(لماذا|كيف|فسر|اشرح|قارن)", RegexOptions.IgnoreCase)]
    private static partial Regex ReasoningAr();

    public static QuestionType? TryClassify(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        if (GreetingEn().IsMatch(query) || GreetingAr().IsMatch(query)) return QuestionType.Greeting;
        if (DefinitionEn().IsMatch(query) || DefinitionAr().IsMatch(query)) return QuestionType.Definition;
        if (ListingEn().IsMatch(query) || ListingAr().IsMatch(query)) return QuestionType.Listing;
        if (ReasoningEn().IsMatch(query) || ReasoningAr().IsMatch(query)) return QuestionType.Reasoning;

        return null;
    }
}
```

- [ ] **Step 5: Run tests — expect PASS (13 tests)**

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IQuestionClassifier.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Classification/RegexQuestionClassifier.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RegexQuestionClassifierTests.cs
git commit -m "feat(ai): IQuestionClassifier + RegexQuestionClassifier (EN + AR)"
```

---

### Task 16: QuestionClassifier (composite) + tests

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Classification/QuestionClassifier.cs`
- Create test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/QuestionClassifierTests.cs`

Composite flow:
1. If regex matches → return its answer (no LLM call, no cache write).
2. Compute cache key `ai:classify:{provider}:{model}:{sha256(normalized-query)}`. If hit → return cached.
3. LLM call with a 64-token, temperature=0 prompt returning a single label word.
4. Parse label → `QuestionType`. Unknown → `QuestionType.Other`.
5. Cache the result for `QuestionCacheTtlSeconds` (default 3600).
6. On LLM failure → log warning, return `null` (caller defaults to `Other`).

- [ ] **Step 1: Write failing test**

```csharp
// QuestionClassifierTests.cs
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Classification;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public class QuestionClassifierTests
{
    private static QuestionClassifier Build(FakeAiProvider provider, FakeCacheService cache) =>
        new(new FakeAiProviderFactory(provider), cache,
            Options.Create(new AiRagSettings()), NullLogger<QuestionClassifier>.Instance);

    [Fact]
    public async Task Regex_match_skips_llm()
    {
        var provider = new FakeAiProvider();
        var cache = new FakeCacheService();
        var c = Build(provider, cache);

        var type = await c.ClassifyAsync("hello", CancellationToken.None);

        type.Should().Be(QuestionType.Greeting);
        provider.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Llm_called_when_regex_does_not_match()
    {
        var provider = new FakeAiProvider();
        provider.Enqueue(new AiChatCompletion("Reasoning", null, 10, 1, "stop"));
        var cache = new FakeCacheService();
        var c = Build(provider, cache);

        var type = await c.ClassifyAsync("the forecast for Q3 is ambiguous", CancellationToken.None);

        type.Should().Be(QuestionType.Reasoning);
        provider.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Llm_result_is_cached()
    {
        var provider = new FakeAiProvider();
        provider.Enqueue(new AiChatCompletion("Definition", null, 10, 1, "stop"));
        var cache = new FakeCacheService();
        var c = Build(provider, cache);

        await c.ClassifyAsync("what about concurrent queues in .NET", CancellationToken.None);
        await c.ClassifyAsync("what about concurrent queues in .NET", CancellationToken.None);

        provider.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Llm_failure_returns_null()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueThrow(new InvalidOperationException("boom"));
        var cache = new FakeCacheService();
        var c = Build(provider, cache);

        var type = await c.ClassifyAsync("ambiguous prose input", CancellationToken.None);

        type.Should().BeNull();
    }

    [Fact]
    public async Task Unknown_llm_label_maps_to_Other()
    {
        var provider = new FakeAiProvider();
        provider.Enqueue(new AiChatCompletion("Nonsense", null, 10, 1, "stop"));
        var cache = new FakeCacheService();
        var c = Build(provider, cache);

        var type = await c.ClassifyAsync("ambiguous prose input", CancellationToken.None);

        type.Should().Be(QuestionType.Other);
    }
}
```

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement**

```csharp
// QuestionClassifier.cs
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Services.Caching;
using Starter.Module.AI.Application.Services.Providers;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval.Classification;

internal sealed class QuestionClassifier : IQuestionClassifier
{
    private readonly IAiProviderFactory _factory;
    private readonly ICacheService _cache;
    private readonly AiRagSettings _settings;
    private readonly ILogger<QuestionClassifier> _logger;

    public QuestionClassifier(
        IAiProviderFactory factory,
        ICacheService cache,
        IOptions<AiRagSettings> settings,
        ILogger<QuestionClassifier> logger)
    {
        _factory = factory;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<QuestionType?> ClassifyAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return QuestionType.Other;

        var regexHit = RegexQuestionClassifier.TryClassify(query);
        if (regexHit is not null)
            return regexHit;

        var normalized = _settings.ApplyArabicNormalization
            ? ArabicTextNormalizer.Normalize(query, _settings)
            : query;

        var key = BuildCacheKey(normalized);
        var cached = await _cache.GetAsync<string>(key, ct);
        if (!string.IsNullOrEmpty(cached))
            return ParseLabel(cached);

        try
        {
            var provider = _factory.CreateDefault();
            var label = await CallLlmAsync(provider, query, ct);
            await _cache.SetAsync(key, label, TimeSpan.FromSeconds(_settings.QuestionCacheTtlSeconds), ct);
            return ParseLabel(label);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Question classification LLM failed; returning null");
            return null;
        }
    }

    private async Task<string> CallLlmAsync(IAiProvider provider, string query, CancellationToken ct)
    {
        const string system =
            "Classify the user question into EXACTLY one label: Greeting, Definition, Listing, Reasoning, Other. " +
            "Output ONLY the label word, no punctuation, no explanation.";

        var opts = new AiChatOptions(
            Provider: _factory.GetDefaultProviderType(),
            Model: _settings.ClassifierModel ?? _factory.GetDefaultChatModelId(),
            Temperature: 0.0,
            MaxTokens: 8,
            SystemPrompt: system);

        var msgs = new List<AiChatMessage> { new("user", query) };
        var resp = await provider.ChatAsync(msgs, opts, ct);
        return (resp.Content ?? string.Empty).Trim();
    }

    private static QuestionType ParseLabel(string label) =>
        label.Trim().ToLowerInvariant() switch
        {
            "greeting" => QuestionType.Greeting,
            "definition" => QuestionType.Definition,
            "listing" => QuestionType.Listing,
            "reasoning" => QuestionType.Reasoning,
            _ => QuestionType.Other
        };

    private string BuildCacheKey(string normalized)
    {
        var provider = _factory.GetDefaultProviderType().ToString();
        var model = _settings.ClassifierModel ?? _factory.GetDefaultChatModelId();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        return $"ai:classify:{provider}:{model}:{hash}";
    }
}
```

Two new settings on `AiRagSettings`:
- `QuestionCacheTtlSeconds` (int, default 3600)
- `ClassifierModel` (string?, default null → use default chat model)

Add them to Task 1's settings update.

- [ ] **Step 4: Run tests — PASS (5 tests)**

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Classification/QuestionClassifier.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/QuestionClassifierTests.cs
git commit -m "feat(ai): QuestionClassifier — regex fast path + LLM fallback + Redis cache"
```

---

### Task 17: Wire QuestionClassifier into RagRetrievalService

At the top of the retrieval pipeline, classify the query. Greetings short-circuit retrieval and return an empty `RetrievedContext` (chat injection layer already handles empty gracefully). All other types pass through and feed `RerankContext(questionType, null)` into the reranker (Task 14).

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs`
- Modify test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RagRetrievalServiceTests.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs` (register classifier)

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public async Task Greeting_short_circuits_and_returns_empty_context()
{
    var settings = new AiRagSettings { EnableQueryExpansion = true };
    var classifier = new FakeQuestionClassifier(QuestionType.Greeting);
    var svc = BuildRetrievalService(settings, classifier: classifier);

    var result = await svc.RetrieveAsync("hi there", tenantId, null, CancellationToken.None);

    result.Chunks.Should().BeEmpty();
    result.Telemetry.DegradedStages.Should().NotContain("embed");
    result.Telemetry.QuestionType.Should().Be(QuestionType.Greeting);
}

[Fact]
public async Task Reasoning_feeds_rerank_context()
{
    var settings = new AiRagSettings { RerankStrategy = RerankStrategy.Auto };
    var classifier = new FakeQuestionClassifier(QuestionType.Reasoning);
    // ... arrange AI provider to return pointwise-style score JSON
    // Assert reranker saw ctx.QuestionType == Reasoning and selected Pointwise
}
```

Add `FakeQuestionClassifier` as a local test helper (simple always-returns).

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Modify RagRetrievalService**

Inject `IQuestionClassifier`. At the top of `RetrieveAsync`:

```csharp
QuestionType? questionType = null;
try
{
    questionType = await WithTimeoutAsync(
        () => _classifier.ClassifyAsync(query, ct),
        _settings.StageTimeoutClassifyMs, // new setting, default 3000
        "classify",
        degradedStages,
        ct);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    _logger.LogWarning(ex, "Classifier threw; continuing with null question type");
}

// Record on telemetry
telemetry.QuestionType = questionType;

// Short-circuit greetings
if (questionType == QuestionType.Greeting)
{
    telemetry.ShortCircuit = "greeting";
    return new RetrievedContext(
        Chunks: Array.Empty<RetrievedChunk>(),
        Telemetry: telemetry);
}
```

Where `RetrievedContext.Telemetry` exists — if not, add a `QuestionType?` field to the existing telemetry record and a `ShortCircuit` string. Check `RetrievalMetrics`/`Telemetry` for the exact type in the codebase. (The plan assumes it's the existing metrics class — wire it onto that.)

Thread `questionType` into the reranker call in Task 14's spot:

```csharp
var rerankCtx = new RerankContext(questionType, null);
```

**Add settings** to `AiRagSettings`:
- `StageTimeoutClassifyMs` (int, default 3000)

- [ ] **Step 4: Register classifier in DI**

In `AIModule.cs`:

```csharp
services.AddScoped<IQuestionClassifier, QuestionClassifier>();
```

- [ ] **Step 5: Run tests — PASS**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~RagRetrievalServiceTests"
```

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs \
        boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RagRetrievalServiceTests.cs
git commit -m "feat(ai): wire QuestionClassifier into retrieval — short-circuit greetings + rerank context"
```

---

## Phase E — Neighbor Expansion

After TrimToBudget selects the final top-K chunks, expand each with its immediate siblings in the same document (chunks at `ChunkIndex - W` through `ChunkIndex + W`, excluding the anchor itself). Siblings help the model reason about surrounding context without polluting the top-K itself.

Siblings are rendered separately in the prompt (labelled "Nearby context") so the LLM can distinguish anchor chunks from context.

### Task 18: RetrievedContext extensions (Siblings + ChunkIndex)

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RetrievedContext.cs`
- Modify test: tests that construct `RetrievedChunk` (update constructor calls)

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public void RetrievedChunk_carries_chunk_index()
{
    var chunk = new RetrievedChunk(
        ChunkId: Guid.NewGuid(),
        DocumentId: Guid.NewGuid(),
        DocumentName: "doc.pdf",
        Content: "body",
        Score: 0.8m,
        ChunkIndex: 5,
        PageNumber: 2,
        SectionTitle: null,
        ParentChunkId: null);

    chunk.ChunkIndex.Should().Be(5);
}

[Fact]
public void RetrievedContext_has_siblings_collection()
{
    var ctx = new RetrievedContext(
        Chunks: Array.Empty<RetrievedChunk>(),
        Siblings: Array.Empty<RetrievedChunk>(),
        Telemetry: new RetrievalTelemetry());

    ctx.Siblings.Should().NotBeNull().And.BeEmpty();
}
```

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Update records**

```csharp
public sealed record RetrievedChunk(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    string Content,
    decimal Score,
    int ChunkIndex,
    int? PageNumber,
    string? SectionTitle,
    Guid? ParentChunkId);

public sealed record RetrievedContext(
    IReadOnlyList<RetrievedChunk> Chunks,
    IReadOnlyList<RetrievedChunk> Siblings,
    RetrievalTelemetry Telemetry);
```

Update every constructor call of `RetrievedContext` (currently two args) to pass the siblings arg. Search the codebase for `new RetrievedContext(` and update each.

Update every `RetrievedChunk` construction site to include `ChunkIndex`. `RagRetrievalService` builds them from `AiDocumentChunk` entities, which have `ChunkIndex` already — just pipe it through.

- [ ] **Step 4: Run — PASS**

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RetrievedContext.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/
git commit -m "feat(ai): add Siblings + ChunkIndex to RetrievedContext/RetrievedChunk"
```

---

### Task 19: INeighborExpander + NeighborExpander

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/INeighborExpander.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/NeighborExpander.cs`
- Create test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/NeighborExpanderTests.cs`

Neighbor expander queries the DB for sibling chunks in a single batched call:
- For each anchor, compute `(DocumentId, range [ChunkIndex-W, ChunkIndex+W])`.
- Merge overlapping ranges per document.
- One `WHERE DocumentId IN (...) AND ChunkIndex BETWEEN ? AND ?` style query (or a UNION of per-doc ranges — use EF `Where(c => (c.DocumentId == d1 && c.ChunkIndex >= a1 && c.ChunkIndex <= b1) || (...)`).
- Exclude anchors from siblings (return only new chunks).
- Keep child chunks only (filter `ChunkLevel == "child"`).
- Order by `(DocumentId, ChunkIndex)` stable for downstream prompt assembly.

- [ ] **Step 1: Write interface**

```csharp
// INeighborExpander.cs
namespace Starter.Module.AI.Application.Services.Retrieval;

public interface INeighborExpander
{
    Task<IReadOnlyList<RetrievedChunk>> ExpandAsync(
        Guid tenantId,
        IReadOnlyList<RetrievedChunk> anchors,
        int windowSize,
        CancellationToken ct);
}
```

- [ ] **Step 2: Write failing test**

```csharp
// NeighborExpanderTests.cs
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public class NeighborExpanderTests
{
    [Fact]
    public async Task Expands_window_excluding_anchor()
    {
        await using var db = TestDbFactory.CreateAiDb();
        var tenantId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        for (int i = 0; i < 10; i++)
            await SeedChunk(db, tenantId, docId, index: i, level: "child");
        await db.SaveChangesAsync();

        var anchor = BuildAnchor(docId, chunkIndex: 5);
        var expander = new NeighborExpander(db, NullLogger<NeighborExpander>.Instance);

        var siblings = await expander.ExpandAsync(tenantId, new[] { anchor }, windowSize: 2, CancellationToken.None);

        siblings.Select(s => s.ChunkIndex).Should().Equal(3, 4, 6, 7);
    }

    [Fact]
    public async Task Merges_overlapping_windows_same_document()
    {
        await using var db = TestDbFactory.CreateAiDb();
        var tenantId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        for (int i = 0; i < 10; i++) await SeedChunk(db, tenantId, docId, i, "child");
        await db.SaveChangesAsync();

        var anchors = new[] { BuildAnchor(docId, 3), BuildAnchor(docId, 5) };
        var siblings = await new NeighborExpander(db, NullLogger<NeighborExpander>.Instance)
            .ExpandAsync(tenantId, anchors, 2, CancellationToken.None);

        siblings.Select(s => s.ChunkIndex).Should().Equal(1, 2, 4, 6, 7);
    }

    [Fact]
    public async Task Cross_document_expansion_handled_separately()
    {
        await using var db = TestDbFactory.CreateAiDb();
        var tenantId = Guid.NewGuid();
        var docA = Guid.NewGuid();
        var docB = Guid.NewGuid();
        for (int i = 0; i < 5; i++) await SeedChunk(db, tenantId, docA, i, "child");
        for (int i = 0; i < 5; i++) await SeedChunk(db, tenantId, docB, i, "child");
        await db.SaveChangesAsync();

        var anchors = new[] { BuildAnchor(docA, 2), BuildAnchor(docB, 2) };
        var siblings = await new NeighborExpander(db, NullLogger<NeighborExpander>.Instance)
            .ExpandAsync(tenantId, anchors, 1, CancellationToken.None);

        siblings.Should().HaveCount(4);
        siblings.Where(s => s.DocumentId == docA).Select(s => s.ChunkIndex).Should().Equal(1, 3);
        siblings.Where(s => s.DocumentId == docB).Select(s => s.ChunkIndex).Should().Equal(1, 3);
    }

    [Fact]
    public async Task Excludes_parent_chunks()
    {
        await using var db = TestDbFactory.CreateAiDb();
        var tenantId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        await SeedChunk(db, tenantId, docId, 0, "child");
        await SeedChunk(db, tenantId, docId, 1, "parent");
        await SeedChunk(db, tenantId, docId, 2, "child");
        await db.SaveChangesAsync();

        var siblings = await new NeighborExpander(db, NullLogger<NeighborExpander>.Instance)
            .ExpandAsync(tenantId, new[] { BuildAnchor(docId, 0) }, 2, CancellationToken.None);

        siblings.Select(s => s.ChunkIndex).Should().Equal(2);
    }

    [Fact]
    public async Task Empty_anchors_returns_empty()
    {
        await using var db = TestDbFactory.CreateAiDb();
        var siblings = await new NeighborExpander(db, NullLogger<NeighborExpander>.Instance)
            .ExpandAsync(Guid.NewGuid(), Array.Empty<RetrievedChunk>(), 2, CancellationToken.None);

        siblings.Should().BeEmpty();
    }

    private static RetrievedChunk BuildAnchor(Guid docId, int chunkIndex) =>
        new(Guid.NewGuid(), docId, "doc", "body", 0.8m, chunkIndex, 1, null, null);

    private static async Task SeedChunk(AppDb db, Guid tenantId, Guid docId, int index, string level)
    {
        // Ensure parent AiDocument exists once per docId. AiDocument has private setters —
        // use reflection (Activator + property setter invocation) the same way TestChunkFactory
        // does for AiDocumentChunk. Fields to set: Id=docId, TenantId=tenantId, Name="doc-{N}",
        // Status="Completed" (or the enum value the retrieval query filters by). Then add it.
        if (!await db.AiDocuments.AnyAsync(d => d.Id == docId))
        {
            var doc = (AiDocument)Activator.CreateInstance(typeof(AiDocument), nonPublic: true)!;
            typeof(AiDocument).GetProperty(nameof(AiDocument.Id))!.GetSetMethod(true)!.Invoke(doc, new object[] { docId });
            typeof(AiDocument).GetProperty(nameof(AiDocument.TenantId))!.GetSetMethod(true)!.Invoke(doc, new object?[] { tenantId });
            typeof(AiDocument).GetProperty(nameof(AiDocument.Name))!.GetSetMethod(true)!.Invoke(doc, new object?[] { $"doc-{docId:N}" });
            db.AiDocuments.Add(doc);
        }

        db.AiDocumentChunks.Add(TestChunkFactory.Build(
            pointId: Guid.NewGuid(),
            documentId: docId,
            chunkIndex: index,
            content: $"chunk {index}",
            chunkLevel: level,
            tenantId: tenantId));
    }
}
```

`TestDbFactory` is an existing test helper in `tests/Starter.Api.Tests/` that wires an in-memory or SQLite AI DB. If it doesn't exist in that form, use the closest equivalent — check `Ai/` test folder for a `TestDbFactory` or `SharedDbFixture`. If absent, add a minimal one that wires `ApplicationDbContext` via Sqlite in-memory with just `AiDocumentChunk` and `AiDocument` configured.

- [ ] **Step 3: Run — FAIL**

- [ ] **Step 4: Implement**

```csharp
// NeighborExpander.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Retrieval;

internal sealed class NeighborExpander : INeighborExpander
{
    private readonly IAiDbContext _db;
    private readonly ILogger<NeighborExpander> _logger;

    public NeighborExpander(IAiDbContext db, ILogger<NeighborExpander> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> ExpandAsync(
        Guid tenantId,
        IReadOnlyList<RetrievedChunk> anchors,
        int windowSize,
        CancellationToken ct)
    {
        if (anchors.Count == 0 || windowSize <= 0)
            return Array.Empty<RetrievedChunk>();

        var anchorIds = anchors.Select(a => a.ChunkId).ToHashSet();

        // Build per-document merged ranges
        var ranges = anchors
            .GroupBy(a => a.DocumentId)
            .Select(g =>
            {
                var merged = MergeRanges(
                    g.Select(a => (Math.Max(0, a.ChunkIndex - windowSize), a.ChunkIndex + windowSize)));
                return (DocumentId: g.Key, Ranges: merged);
            })
            .ToList();

        // Fetch chunks for all doc+range combos in a single round trip.
        // EF predicate: (doc == d1 && idx in [r1a,r1b]) || (doc == d2 && idx in [r2a,r2b]) || ...
        var predicate = BuildPredicate(ranges);

        var chunks = await _db.AiDocumentChunks
            .Where(c => c.TenantId == tenantId && c.ChunkLevel == "child")
            .Where(predicate)
            .OrderBy(c => c.DocumentId).ThenBy(c => c.ChunkIndex)
            .AsNoTracking()
            .ToListAsync(ct);

        return chunks
            .Where(c => !anchorIds.Contains(c.QdrantPointId))
            .Select(c => new RetrievedChunk(
                ChunkId: c.QdrantPointId,
                DocumentId: c.DocumentId,
                DocumentName: c.Document?.Name ?? string.Empty,
                Content: c.Content,
                Score: 0m,
                ChunkIndex: c.ChunkIndex,
                PageNumber: c.PageNumber,
                SectionTitle: c.SectionTitle,
                ParentChunkId: c.ParentChunkId))
            .ToList();
    }

    private static IEnumerable<(int Start, int End)> MergeRanges(IEnumerable<(int, int)> input)
    {
        var sorted = input.OrderBy(r => r.Item1).ToList();
        var merged = new List<(int, int)>();
        foreach (var r in sorted)
        {
            if (merged.Count > 0 && merged[^1].Item2 >= r.Item1 - 1)
                merged[^1] = (merged[^1].Item1, Math.Max(merged[^1].Item2, r.Item2));
            else
                merged.Add(r);
        }
        return merged;
    }

    private static System.Linq.Expressions.Expression<Func<AiDocumentChunk, bool>> BuildPredicate(
        IReadOnlyList<(Guid DocumentId, IEnumerable<(int Start, int End)> Ranges)> ranges)
    {
        var p = System.Linq.Expressions.Expression.Parameter(typeof(AiDocumentChunk), "c");
        System.Linq.Expressions.Expression? body = null;

        foreach (var (docId, rs) in ranges)
        {
            foreach (var (start, end) in rs)
            {
                var docEq = System.Linq.Expressions.Expression.Equal(
                    System.Linq.Expressions.Expression.Property(p, nameof(AiDocumentChunk.DocumentId)),
                    System.Linq.Expressions.Expression.Constant(docId));
                var idxProp = System.Linq.Expressions.Expression.Property(p, nameof(AiDocumentChunk.ChunkIndex));
                var gte = System.Linq.Expressions.Expression.GreaterThanOrEqual(idxProp,
                    System.Linq.Expressions.Expression.Constant(start));
                var lte = System.Linq.Expressions.Expression.LessThanOrEqual(idxProp,
                    System.Linq.Expressions.Expression.Constant(end));
                var term = System.Linq.Expressions.Expression.AndAlso(docEq,
                    System.Linq.Expressions.Expression.AndAlso(gte, lte));
                body = body is null ? term : System.Linq.Expressions.Expression.OrElse(body, term);
            }
        }

        return System.Linq.Expressions.Expression.Lambda<Func<AiDocumentChunk, bool>>(
            body ?? System.Linq.Expressions.Expression.Constant(false), p);
    }
}
```

Note: `ChunkId` in `RetrievedChunk` follows the existing convention — it holds `QdrantPointId`, not the raw EF `Id`. Anchor exclusion uses the same key.

- [ ] **Step 5: Run tests — PASS**

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/INeighborExpander.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/NeighborExpander.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/NeighborExpanderTests.cs
git commit -m "feat(ai): INeighborExpander + NeighborExpander — batched sibling lookup per document"
```

---

### Task 20: Wire NeighborExpander into RagRetrievalService + ContextPromptBuilder rendering

Post-TrimToBudget, expand anchors with siblings (respecting `NeighborWindowSize` setting). Render siblings in the prompt as a labelled "Nearby context" block — not mixed into the anchor chunks.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Chat/ContextPromptBuilder.cs` (or wherever anchor chunks are currently rendered)
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs` (DI registration)

- [ ] **Step 1: Write failing integration test**

```csharp
[Fact]
public async Task Retrieve_populates_Siblings_when_neighbor_window_positive()
{
    var settings = new AiRagSettings { NeighborWindowSize = 1, TopK = 1 };
    // Seed 5 chunks in one doc, index 0..4
    // Retrieve with query that anchors on chunk index 2
    // Assert result.Chunks has 1, result.Siblings has 2 (index 1 and 3)
}

[Fact]
public async Task Retrieve_neighbor_window_zero_skips_expansion()
{
    var settings = new AiRagSettings { NeighborWindowSize = 0 };
    var result = await svc.RetrieveAsync(...);
    result.Siblings.Should().BeEmpty();
}
```

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Modify RagRetrievalService**

```csharp
// after TrimToBudget produces topK anchors
IReadOnlyList<RetrievedChunk> siblings = Array.Empty<RetrievedChunk>();
if (_settings.NeighborWindowSize > 0)
{
    try
    {
        siblings = await WithTimeoutAsync(
            () => _neighborExpander.ExpandAsync(tenantId, topK, _settings.NeighborWindowSize, ct),
            _settings.StageTimeoutNeighborMs, // new setting, default 3000
            "neighbor",
            degradedStages,
            ct);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        _logger.LogWarning(ex, "Neighbor expansion threw; continuing with no siblings");
    }
}

return new RetrievedContext(
    Chunks: topK,
    Siblings: siblings,
    Telemetry: telemetry);
```

Inject `INeighborExpander` via constructor. Add `StageTimeoutNeighborMs` and `NeighborWindowSize` (default 1) to `AiRagSettings`.

- [ ] **Step 4: Modify ContextPromptBuilder**

Render anchor chunks first, then a `--- Nearby context ---` separator, then siblings in `(DocumentName, PageNumber, ChunkIndex)` order. Token budget still applies — siblings truncate first if we'd exceed `MaxContextTokens`.

Rough render:

```
[Document: invoices.pdf, page 2]
<anchor chunk content>

[Document: invoices.pdf, page 1]
<anchor chunk content>

--- Nearby context ---
[Document: invoices.pdf, page 2]
<sibling chunk content>
...
```

- [ ] **Step 5: DI registration**

In `AIModule.cs`:

```csharp
services.AddScoped<INeighborExpander, NeighborExpander>();
```

- [ ] **Step 6: Run all retrieval tests — PASS**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~Ai.Retrieval"
```

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Chat/ContextPromptBuilder.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs \
        boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/
git commit -m "feat(ai): wire NeighborExpander — siblings rendered as 'Nearby context' block"
```

---

## Phase F — Arabic + Integration

### Task 21: Arabic fixture + end-to-end integration test

Exercise the full pipeline: classify → rewrite → embed → fuse → rerank → neighbor-expand, with Arabic queries.

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/fixtures/arabic_queries.json`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/QueryIntelligencePipelineTests.cs`

- [ ] **Step 1: Create Arabic fixtures**

```json
{
  "queries": [
    {
      "name": "greeting_ar",
      "input": "مرحبا",
      "expectedClassification": "Greeting",
      "expectedRewrite": null,
      "expectShortCircuit": true
    },
    {
      "name": "definition_ar_with_taa_marbuta",
      "input": "ما هي سياسة الإرجاع؟",
      "normalizedForm": "ما هي سياسه الارجاع",
      "expectedClassification": "Definition",
      "expectedRewrite": "سياسة الإرجاع"
    },
    {
      "name": "listing_ar",
      "input": "اعرض لي جميع العملاء المسجلين",
      "expectedClassification": "Listing",
      "expectedRewrite": "العملاء المسجلين"
    },
    {
      "name": "reasoning_ar",
      "input": "لماذا فشل الطلب رقم 12345؟",
      "expectedClassification": "Reasoning",
      "expectedRewrite": "فشل الطلب 12345"
    },
    {
      "name": "mixed_ar_digits",
      "input": "ما هو رصيد الحساب ١٢٣٤٥",
      "normalizedForm": "ما هو رصيد الحساب 12345",
      "expectedClassification": "Definition"
    }
  ]
}
```

- [ ] **Step 2: Write integration test**

```csharp
// QueryIntelligencePipelineTests.cs
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public class QueryIntelligencePipelineTests : IClassFixture<AiTestFixture>
{
    private readonly AiTestFixture _fx;

    public QueryIntelligencePipelineTests(AiTestFixture fx) => _fx = fx;

    public static IEnumerable<object[]> ArabicQueries()
    {
        var json = File.ReadAllText("Ai/fixtures/arabic_queries.json");
        using var doc = JsonDocument.Parse(json);
        foreach (var q in doc.RootElement.GetProperty("queries").EnumerateArray())
            yield return new object[] { q.GetProperty("name").GetString()!, q.Clone() };
    }

    [Theory]
    [MemberData(nameof(ArabicQueries))]
    public async Task Arabic_pipeline_end_to_end(string name, JsonElement q)
    {
        var input = q.GetProperty("input").GetString()!;
        var expectedClass = Enum.Parse<QuestionType>(q.GetProperty("expectedClassification").GetString()!);
        var shortCircuit = q.TryGetProperty("expectShortCircuit", out var sc) && sc.GetBoolean();

        // Arrange: seed AI DB + Qdrant fake with a single Arabic doc matching the expected rewrite
        await _fx.SeedArabicDocumentAsync();

        // Scripted AI provider:
        //   Call 1 (classifier): returns expectedClass as string
        //   Call 2 (rewriter): returns expectedRewrite or input
        //   Call 3 (reranker): returns "[0]" (single-hit listwise)
        _fx.Ai.EnqueueClassification(expectedClass);
        if (!shortCircuit)
        {
            _fx.Ai.EnqueueRewrite(q.TryGetProperty("expectedRewrite", out var rw) && rw.ValueKind != JsonValueKind.Null
                ? rw.GetString()! : input);
            _fx.Ai.EnqueueRerankIndices("[0]");
        }

        var svc = _fx.Services.GetRequiredService<IRagRetrievalService>();
        var result = await svc.RetrieveAsync(input, _fx.TenantId, null, CancellationToken.None);

        if (shortCircuit)
        {
            result.Chunks.Should().BeEmpty();
            result.Telemetry.QuestionType.Should().Be(QuestionType.Greeting);
            return;
        }

        result.Chunks.Should().NotBeEmpty();
        result.Telemetry.QuestionType.Should().Be(expectedClass);
        result.Telemetry.DegradedStages.Should().BeEmpty();
    }
}
```

`AiTestFixture` is a new fixture that stands up: in-memory SQLite with `ApplicationDbContext`, a fake vector store (in-memory with the documents seeded), a fake keyword search, a scripted `FakeAiProvider`, and the real `RagRetrievalService` wired through real DI. If a similar fixture already exists for retrieval tests, extend it — don't duplicate.

- [ ] **Step 3: Run — FAIL then PASS after fixture wired**

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/fixtures/arabic_queries.json \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/QueryIntelligencePipelineTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/
git commit -m "test(ai): Arabic query intelligence pipeline end-to-end fixture + integration tests"
```

---

## Phase G — Config + Migration

### Task 22: appsettings + AIModule DI sweep + migration note

Final task: make sure every new DI registration is wired, defaults land in `appsettings.Development.json` with operator-facing comments, and a short migration note is added.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`
- Modify: `boilerplateBE/src/Starter.Api/appsettings.Development.json`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/README.md` (if present; else create under docs)

- [ ] **Step 1: Verify DI registrations**

Open `AIModule.cs` and confirm these live in the AI services block:

```csharp
// Query intelligence (Plan 4b-2)
services.AddScoped<IQueryRewriter, QueryRewriter>();
services.AddScoped<IQuestionClassifier, QuestionClassifier>();
services.AddScoped<INeighborExpander, NeighborExpander>();

services.AddScoped<RerankStrategySelector>(sp =>
    new RerankStrategySelector(sp.GetRequiredService<IOptions<AiRagSettings>>().Value));
services.AddScoped<ListwiseReranker>();
services.AddScoped<PointwiseReranker>();
services.AddScoped<IReranker, Reranker>();
```

- [ ] **Step 2: Update appsettings.Development.json**

Add under `AI:Rag`:

```json
{
  "AI": {
    "Rag": {
      "EnableQueryExpansion": true,
      "QueryExpansionMaxVariants": 3,
      "QueryRewriteCacheTtlSeconds": 3600,
      "RewriterModel": null,

      "RerankStrategy": "Listwise",
      "RerankerModel": null,
      "ListwisePoolMultiplier": 3,
      "PointwisePoolMultiplier": 2,
      "PointwiseMaxParallelism": 4,
      "PointwiseMinScore": 0.3,
      "PointwiseMaxFailureRatio": 0.25,
      "RerankCacheTtlSeconds": 3600,
      "StageTimeoutRerankMs": 8000,

      "ClassifierModel": null,
      "QuestionCacheTtlSeconds": 3600,
      "StageTimeoutClassifyMs": 3000,

      "NeighborWindowSize": 1,
      "StageTimeoutNeighborMs": 3000
    }
  }
}
```

- [ ] **Step 3: Add migration note**

Create or append `boilerplateBE/src/modules/Starter.Module.AI/docs/plan-4b-2-upgrade-notes.md`:

```markdown
# Plan 4b-2 Upgrade Notes

## Setting rename
- `AI:Rag:EnableReranking` (bool) is REMOVED.
- Replacement: `AI:Rag:RerankStrategy` enum — `Off | Listwise | Pointwise | Auto | FallbackRrf`.
- Migration: existing `EnableReranking=false` → set `RerankStrategy=Off`.
  `EnableReranking=true` → set `RerankStrategy=Listwise` (safe default).

## New settings
| Setting | Default | Purpose |
| --- | --- | --- |
| `QueryExpansionMaxVariants` | 3 | Max rewrite variants fed to retrieval |
| `ListwisePoolMultiplier` | 3 | Rerank pool size = TopK × multiplier |
| `PointwisePoolMultiplier` | 2 | Same, for pointwise strategy |
| `PointwiseMinScore` | 0.3 | Scores below this are dropped |
| `PointwiseMaxFailureRatio` | 0.25 | Abort pointwise if this fraction of pairs fails; fall back to Listwise |
| `NeighborWindowSize` | 1 | Sibling chunks either side of each anchor |
| `*ModelOverride` | null | Per-feature model override; null = default chat model |
| `StageTimeout*Ms` | various | Per-stage budgets; exceeding adds to DegradedStages telemetry |

## Automation signals
See spec §5 Monitoring — emit `rerank.strategy_used`, `rerank.unused_ratio`, `rerank.fell_back`,
`rerank.latency_ms`, `rerank.tokens_*`, `classify.type`, `rewrite.variants_used`. Use these in
Grafana to decide between Listwise and Pointwise — or flip to `Auto` and let the selector route.
```

- [ ] **Step 4: Final smoke test — full AI test suite**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~Ai"
```

Expected: all tests PASS. Investigate any failures — they represent regressions.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs \
        boilerplateBE/src/Starter.Api/appsettings.Development.json \
        boilerplateBE/src/modules/Starter.Module.AI/docs/plan-4b-2-upgrade-notes.md
git commit -m "chore(ai): 4b-2 — DI sweep, appsettings defaults, upgrade notes"
```

---

## Done Checklist

After Task 22:
- [ ] All new settings have defaults in `appsettings.Development.json`.
- [ ] `EnableReranking` bool is fully removed (no residual references).
- [ ] Every new service is DI-registered in `AIModule.cs`.
- [ ] Full AI test suite passes (`dotnet test --filter "FullyQualifiedName~Ai"`).
- [ ] Spec's Monitoring table (§5) is reflected in emitted metrics.
- [ ] Arabic fixture exercised end-to-end.
- [ ] No EF Core migrations committed (per repo rule — `AiDocumentChunk.ChunkIndex` already exists).
- [ ] Manual sanity check: flip `RerankStrategy=Auto` in dev, ask a greeting / a list-me-X / a why-X, watch logs.

## Future / Deferred

Out of scope for 4b-2 (tracked for later plans):
- Cross-encoder model adapter (local bge-reranker-base or similar) — plug behind `IReranker` with a new strategy value.
- Adaptive strategy selector driven by live telemetry (learning to route Pointwise only when unused-ratio is high).
- Rewriter variant diversity scoring (deduplicate variants by semantic similarity before fan-out).
- Multi-tenant rerank cache warmup.

