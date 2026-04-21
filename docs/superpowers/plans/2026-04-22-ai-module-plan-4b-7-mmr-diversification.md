# Plan 4b-7 — MMR Diversification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce near-duplicate chunks in retrieved top-K by adding an opt-in MMR (Maximal Marginal Relevance) diversification stage between rerank and `.Take(topK)` in the RAG pipeline.

**Architecture:** Pure-algorithm `MmrDiversifier` + new `IVectorStore.GetVectorsByIdsAsync` for fetching chunk embeddings from Qdrant. Stage is wrapped by existing `WithTimeoutAsync` (inherits circuit-breaker + degradation from 4b-1 / 4b-4 / 4b-6). Off by default (`EnableMmr=false`).

**Tech Stack:** .NET 10, Polly v8 (reused from 4b-6), Qdrant.Client, xUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-04-22-ai-module-plan-4b-7-mmr-diversification-design.md`

---

## Task 1: Extend `IVectorStore` with `GetVectorsByIdsAsync` (contract test)

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/IVectorStore.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Diversification/VectorStoreContractTests.cs` (new)

- [ ] **Step 1: Write failing contract test on a fake**

```csharp
// boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Diversification/VectorStoreContractTests.cs
using FluentAssertions;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Diversification;

public class VectorStoreContractTests
{
    [Fact]
    public async Task GetVectorsByIdsAsync_is_part_of_the_interface()
    {
        IVectorStore store = new StubVectorStore();
        var tenant = Guid.NewGuid();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var result = await store.GetVectorsByIdsAsync(tenant, new[] { id1, id2 }, CancellationToken.None);

        result.Should().ContainKey(id1).WhoseValue.Should().BeEquivalentTo(new[] { 1f, 0f });
        result.Should().ContainKey(id2).WhoseValue.Should().BeEquivalentTo(new[] { 0f, 1f });
    }

    private sealed class StubVectorStore : IVectorStore
    {
        public Task EnsureCollectionAsync(Guid t, int s, CancellationToken ct) => Task.CompletedTask;
        public Task UpsertAsync(Guid t, IReadOnlyList<VectorPoint> p, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteByDocumentAsync(Guid t, Guid d, CancellationToken ct) => Task.CompletedTask;
        public Task DropCollectionAsync(Guid t, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<VectorSearchHit>> SearchAsync(Guid t, float[] v, IReadOnlyCollection<Guid>? d, int limit, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<VectorSearchHit>>(Array.Empty<VectorSearchHit>());

        public Task<IReadOnlyDictionary<Guid, float[]>> GetVectorsByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> pointIds, CancellationToken ct)
        {
            var ids = pointIds.ToList();
            IReadOnlyDictionary<Guid, float[]> dict = new Dictionary<Guid, float[]>
            {
                [ids[0]] = new[] { 1f, 0f },
                [ids[1]] = new[] { 0f, 1f },
            };
            return Task.FromResult(dict);
        }
    }
}
```

- [ ] **Step 2: Run test — expect compile error (method does not exist on interface)**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~VectorStoreContractTests`
Expected: CS0535 / CS1061 — `IVectorStore` does not contain `GetVectorsByIdsAsync`.

- [ ] **Step 3: Add method to interface**

```csharp
// Application/Services/Ingestion/IVectorStore.cs
public interface IVectorStore
{
    Task EnsureCollectionAsync(Guid tenantId, int vectorSize, CancellationToken ct);
    Task UpsertAsync(Guid tenantId, IReadOnlyList<VectorPoint> points, CancellationToken ct);
    Task DeleteByDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct);
    Task DropCollectionAsync(Guid tenantId, CancellationToken ct);
    Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        Guid tenantId,
        float[] queryVector,
        IReadOnlyCollection<Guid>? documentFilter,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// Retrieve stored embedding vectors for a batch of point-ids. Missing ids are
    /// silently omitted from the result dictionary (eventual consistency between
    /// Qdrant and the relational DB can leave orphan ids).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, float[]>> GetVectorsByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> pointIds,
        CancellationToken ct);
}
```

- [ ] **Step 4: Re-run test — still failing because `QdrantVectorStore` and `CircuitBreakingVectorStore` now fail to compile**

Run: `cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj`
Expected: `CS0535` — `QdrantVectorStore` / `CircuitBreakingVectorStore` does not implement `GetVectorsByIdsAsync`.

This is expected. Tasks 2 and 3 implement the two concrete types; once both are done the build goes green and this contract test passes.

- [ ] **Step 5: Skip commit until Tasks 2+3 finish** — the module will not compile in isolation.

---

## Task 2: Implement `GetVectorsByIdsAsync` on `QdrantVectorStore`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/QdrantVectorStore.cs`

- [ ] **Step 1: Add method implementation**

```csharp
// Append to QdrantVectorStore (keep other methods unchanged)
public async Task<IReadOnlyDictionary<Guid, float[]>> GetVectorsByIdsAsync(
    Guid tenantId,
    IReadOnlyCollection<Guid> pointIds,
    CancellationToken ct)
{
    if (pointIds.Count == 0)
        return new Dictionary<Guid, float[]>();

    var name = CollectionName(tenantId);
    var ids = pointIds
        .Select(id => new PointId { Uuid = id.ToString() })
        .ToList();

    var points = await _client.RetrieveAsync(
        collectionName: name,
        ids: ids,
        withPayload: false,
        withVectors: true,
        cancellationToken: ct);

    var map = new Dictionary<Guid, float[]>(points.Count);
    foreach (var p in points)
    {
        if (p.Vectors?.Vector?.Data is not { Count: > 0 } data) continue;
        map[Guid.Parse(p.Id.Uuid)] = data.ToArray();
    }

    return map;
}
```

- [ ] **Step 2: Compile the module**

Run: `cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj`
Expected: still fails — `CircuitBreakingVectorStore` does not implement the method yet.

- [ ] **Step 3: Continue to Task 3 (no commit yet).**

---

## Task 3: Forward `GetVectorsByIdsAsync` through `CircuitBreakingVectorStore`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/CircuitBreakingVectorStore.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/CircuitBreakingVectorStoreGetVectorsTests.cs` (new)

- [ ] **Step 1: Write a failing test for the decorator passthrough**

```csharp
// boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/CircuitBreakingVectorStoreGetVectorsTests.cs
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Resilience;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Resilience;

public class CircuitBreakingVectorStoreGetVectorsTests
{
    [Fact]
    public async Task Forwards_GetVectorsByIds_to_inner_store_when_circuit_closed()
    {
        var inner = new RecordingVectorStore();
        var settings = new AiRagSettings
        {
            CircuitBreakers = new RagCircuitBreakerSettings
            {
                Qdrant = new RagCircuitBreakerOptions { Enabled = true },
                PostgresFts = new RagCircuitBreakerOptions { Enabled = true },
            }
        };
        var registry = new RagCircuitBreakerRegistry(
            Options.Create(settings),
            NullLogger<RagCircuitBreakerRegistry>.Instance);
        var decorator = new CircuitBreakingVectorStore(inner, registry);

        var tenant = Guid.NewGuid();
        var id = Guid.NewGuid();
        inner.VectorsByIdResult = new Dictionary<Guid, float[]> { [id] = new[] { 1f, 2f, 3f } };

        var result = await decorator.GetVectorsByIdsAsync(tenant, new[] { id }, CancellationToken.None);

        inner.GetVectorsCalls.Should().Be(1);
        result.Should().ContainKey(id).WhoseValue.Should().Equal(1f, 2f, 3f);
    }

    private sealed class RecordingVectorStore : IVectorStore
    {
        public int GetVectorsCalls { get; private set; }
        public IReadOnlyDictionary<Guid, float[]> VectorsByIdResult { get; set; }
            = new Dictionary<Guid, float[]>();

        public Task EnsureCollectionAsync(Guid t, int s, CancellationToken ct) => Task.CompletedTask;
        public Task UpsertAsync(Guid t, IReadOnlyList<VectorPoint> p, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteByDocumentAsync(Guid t, Guid d, CancellationToken ct) => Task.CompletedTask;
        public Task DropCollectionAsync(Guid t, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<VectorSearchHit>> SearchAsync(Guid t, float[] v, IReadOnlyCollection<Guid>? d, int limit, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<VectorSearchHit>>(Array.Empty<VectorSearchHit>());
        public Task<IReadOnlyDictionary<Guid, float[]>> GetVectorsByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> pointIds, CancellationToken ct)
        {
            GetVectorsCalls++;
            return Task.FromResult(VectorsByIdResult);
        }
    }
}
```

- [ ] **Step 2: Run test — expect compile failure**

Run: `cd boilerplateBE && dotnet build tests/Starter.Api.Tests/Starter.Api.Tests.csproj`
Expected: still `CS0535` on `CircuitBreakingVectorStore`.

- [ ] **Step 3: Implement the passthrough**

```csharp
// Append to CircuitBreakingVectorStore after SearchAsync
public async Task<IReadOnlyDictionary<Guid, float[]>> GetVectorsByIdsAsync(
    Guid tenantId,
    IReadOnlyCollection<Guid> pointIds,
    CancellationToken ct)
{
    return await _registry.Qdrant.ExecuteAsync(
        async token => await _inner.GetVectorsByIdsAsync(tenantId, pointIds, token),
        ct);
}
```

Also update the XML-doc at the top of the class:

```csharp
/// <summary>
/// <see cref="IVectorStore"/> decorator that routes <see cref="SearchAsync"/> and
/// <see cref="GetVectorsByIdsAsync"/> through the Qdrant circuit-breaker pipeline.
/// Mutating operations (Ensure/Upsert/Delete/Drop) bypass the breaker because they
/// are invoked from the ingestion path (indexing retries are MassTransit's concern,
/// not the live-chat latency budget).
/// </summary>
```

- [ ] **Step 4: Build + run the decorator and contract tests**

Run:
```
cd boilerplateBE
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj \
  --filter "FullyQualifiedName~VectorStoreContractTests|FullyQualifiedName~CircuitBreakingVectorStoreGetVectorsTests"
```
Expected: both pass.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/IVectorStore.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/QdrantVectorStore.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Resilience/CircuitBreakingVectorStore.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Diversification/VectorStoreContractTests.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Resilience/CircuitBreakingVectorStoreGetVectorsTests.cs
git commit -m "feat(ai): add IVectorStore.GetVectorsByIdsAsync with Qdrant impl and breaker passthrough"
```

---

## Task 4: Add MMR settings to `AiRagSettings`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs`

- [ ] **Step 1: Append the three settings to the end of `AiRagSettings`**

```csharp
// ---- New in Plan 4b-7 — MMR diversification ----
/// <summary>
/// When true, a Maximal Marginal Relevance pass runs after rerank to reduce
/// near-duplicate chunks in the final top-K. Off by default; flip on per-environment
/// when QA confirms near-duplicate symptoms in retrieved context.
/// </summary>
public bool EnableMmr { get; init; } = false;

/// <summary>
/// MMR λ trade-off: 1.0 = pure relevance (no-op), 0.0 = pure diversity.
/// Clamped to [0,1] at runtime. Literature-recommended range 0.5–0.8; default 0.7
/// mildly favours relevance.
/// </summary>
public double MmrLambda { get; init; } = 0.7;

/// <summary>
/// Per-stage timeout for the MMR diversification pass. Guards the Qdrant
/// vector-fetch round-trip that MMR needs; the algorithm itself runs in microseconds.
/// </summary>
public int StageTimeoutMmrMs { get; init; } = 2_000;
```

- [ ] **Step 2: Build**

Run: `cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs
git commit -m "feat(ai): add MMR settings (EnableMmr, MmrLambda, StageTimeoutMmrMs) to AiRagSettings"
```

---

## Task 5: Add `MmrDiversify` stage constant

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagStages.cs`

- [ ] **Step 1: Add constant**

```csharp
internal static class RagStages
{
    public const string Contextualize = "contextualize";
    public const string Classify = "classify";
    public const string QueryRewrite = "query-rewrite";
    public const string EmbedQuery = "embed-query";
    public const string Rerank = "rerank";
    public const string MmrDiversify = "mmr-diversify";
    public const string NeighborExpand = "neighbor-expand";

    public static string VectorSearch(int variantIndex) => $"vector-search[{variantIndex}]";
    public static string KeywordSearch(int variantIndex) => $"keyword-search[{variantIndex}]";
}
```

- [ ] **Step 2: Build**

Run: `cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj`
Expected: succeeds.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagStages.cs
git commit -m "feat(ai): add MmrDiversify stage name constant"
```

---

## Task 6: Implement `MmrDiversifier` pure algorithm

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Diversification/MmrDiversifier.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Diversification/MmrDiversifierTests.cs` (new)

- [ ] **Step 1: Write failing tests for the algorithm**

```csharp
// boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Diversification/MmrDiversifierTests.cs
using FluentAssertions;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Diversification;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Diversification;

public class MmrDiversifierTests
{
    [Fact]
    public void Lambda_one_is_pure_relevance_and_short_circuits()
    {
        var id1 = Guid.NewGuid(); var id2 = Guid.NewGuid(); var id3 = Guid.NewGuid();
        var hits = new[]
        {
            new HybridHit(id1, 0m, 0m, 0.9m),
            new HybridHit(id2, 0m, 0m, 0.8m),
            new HybridHit(id3, 0m, 0m, 0.7m),
        };
        // Deliberately degenerate embeddings — with lambda=1 they should never be consulted.
        var embeddings = new Dictionary<Guid, float[]>
        {
            [id1] = new[] { 1f, 0f }, [id2] = new[] { 1f, 0f }, [id3] = new[] { 1f, 0f }
        };

        var result = MmrDiversifier.Diversify(hits, embeddings, lambda: 1.0, topK: 2);

        result.Select(h => h.ChunkId).Should().ContainInOrder(id1, id2);
    }

    [Fact]
    public void Suppresses_near_duplicates_at_moderate_lambda()
    {
        // A, B, C all have the same embedding (duplicates). D has a distinct embedding.
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid(); var d = Guid.NewGuid();
        var hits = new[]
        {
            new HybridHit(a, 0m, 0m, 0.99m),
            new HybridHit(b, 0m, 0m, 0.97m),
            new HybridHit(c, 0m, 0m, 0.95m),
            new HybridHit(d, 0m, 0m, 0.50m),
        };
        var embeddings = new Dictionary<Guid, float[]>
        {
            [a] = new[] { 1f, 0f },
            [b] = new[] { 1f, 0f },
            [c] = new[] { 1f, 0f },
            [d] = new[] { 0f, 1f },
        };

        var result = MmrDiversifier.Diversify(hits, embeddings, lambda: 0.5, topK: 2);

        result.Select(h => h.ChunkId).Should().Contain(a).And.Contain(d);
    }

    [Fact]
    public void Empty_input_returns_empty()
    {
        var result = MmrDiversifier.Diversify(
            Array.Empty<HybridHit>(),
            new Dictionary<Guid, float[]>(),
            lambda: 0.5,
            topK: 5);
        result.Should().BeEmpty();
    }

    [Fact]
    public void TopK_greater_than_pool_returns_pool()
    {
        var id1 = Guid.NewGuid(); var id2 = Guid.NewGuid();
        var hits = new[]
        {
            new HybridHit(id1, 0m, 0m, 0.9m),
            new HybridHit(id2, 0m, 0m, 0.8m),
        };
        var embeddings = new Dictionary<Guid, float[]>
        {
            [id1] = new[] { 1f, 0f }, [id2] = new[] { 0f, 1f }
        };

        var result = MmrDiversifier.Diversify(hits, embeddings, lambda: 0.5, topK: 5);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Drops_hits_with_missing_embedding()
    {
        var id1 = Guid.NewGuid(); var id2 = Guid.NewGuid(); var id3 = Guid.NewGuid();
        var hits = new[]
        {
            new HybridHit(id1, 0m, 0m, 0.9m),
            new HybridHit(id2, 0m, 0m, 0.8m),  // missing embedding
            new HybridHit(id3, 0m, 0m, 0.7m),
        };
        var embeddings = new Dictionary<Guid, float[]>
        {
            [id1] = new[] { 1f, 0f },
            // id2 omitted
            [id3] = new[] { 0f, 1f },
        };

        var result = MmrDiversifier.Diversify(hits, embeddings, lambda: 0.5, topK: 5);

        result.Select(h => h.ChunkId).Should().BeEquivalentTo(new[] { id1, id3 });
    }

    [Fact]
    public void Lambda_zero_prefers_diverse_pick_over_second_most_relevant()
    {
        // Two clusters: (a, b) near each other, c distinct. With lambda=0, after picking a
        // (highest relevance), the next pick must be c (most diverse), not b (near duplicate).
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var hits = new[]
        {
            new HybridHit(a, 0m, 0m, 0.9m),
            new HybridHit(b, 0m, 0m, 0.8m),
            new HybridHit(c, 0m, 0m, 0.3m),
        };
        var embeddings = new Dictionary<Guid, float[]>
        {
            [a] = new[] { 1f, 0f },
            [b] = new[] { 0.99f, 0.01f },  // near-duplicate of a
            [c] = new[] { 0f, 1f },
        };

        var result = MmrDiversifier.Diversify(hits, embeddings, lambda: 0.0, topK: 2);

        result.Select(h => h.ChunkId).Should().ContainInOrder(a, c);
    }

    [Fact]
    public void Clamps_lambda_to_valid_range()
    {
        var id1 = Guid.NewGuid();
        var hits = new[] { new HybridHit(id1, 0m, 0m, 0.9m) };
        var embeddings = new Dictionary<Guid, float[]> { [id1] = new[] { 1f, 0f } };

        var r1 = MmrDiversifier.Diversify(hits, embeddings, lambda: 5.0, topK: 1);
        var r2 = MmrDiversifier.Diversify(hits, embeddings, lambda: -1.0, topK: 1);

        r1.Should().ContainSingle();
        r2.Should().ContainSingle();
    }
}
```

- [ ] **Step 2: Run tests — expect failure**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~MmrDiversifierTests`
Expected: compile failure — `MmrDiversifier` not found.

- [ ] **Step 3: Implement the algorithm**

Check the current `HybridHit` definition first to match the constructor signature:

Run: `cd boilerplateBE && grep -n "public record HybridHit\|public sealed record HybridHit\|record HybridHit" -r src/modules/Starter.Module.AI/`

Then create the file. **If `HybridHit`'s actual shape differs from the test's 4-arg constructor, update the tests to match before implementing.**

```csharp
// boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Diversification/MmrDiversifier.cs
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval.Diversification;

/// <summary>
/// Pure-function implementation of Maximal Marginal Relevance (Carbonell &amp; Goldstein, 1998).
/// Given a relevance-ranked pool of hybrid hits and their chunk embeddings, iteratively picks
/// the candidate that maximises <c>λ · rel(d) − (1−λ) · max sim(d, s)</c> where <c>s</c> ranges
/// over the already-selected set. <c>rel</c> is min-max normalised across the pool so λ is not
/// dominated by the absolute magnitude of rerank scores.
/// </summary>
internal static class MmrDiversifier
{
    public static IReadOnlyList<HybridHit> Diversify(
        IReadOnlyList<HybridHit> hits,
        IReadOnlyDictionary<Guid, float[]> embeddings,
        double lambda,
        int topK)
    {
        if (hits.Count == 0 || topK <= 0)
            return Array.Empty<HybridHit>();

        var clampedLambda = Math.Clamp(lambda, 0.0, 1.0);

        // Filter hits whose embeddings are missing (eventual consistency).
        var usableHits = hits.Where(h => embeddings.ContainsKey(h.ChunkId)).ToList();
        if (usableHits.Count == 0)
            return Array.Empty<HybridHit>();

        // Short-circuit: lambda == 1 (pure relevance) or pool already <= topK.
        if (clampedLambda >= 1.0 - 1e-9 || usableHits.Count <= topK)
            return usableHits.Take(topK).ToList();

        // Min-max normalise relevance into [0, 1].
        var rels = new Dictionary<Guid, double>(usableHits.Count);
        var scores = usableHits.Select(h => (double)h.HybridScore).ToList();
        var min = scores.Min();
        var max = scores.Max();
        var span = max - min;
        foreach (var h in usableHits)
        {
            rels[h.ChunkId] = span > 1e-12
                ? ((double)h.HybridScore - min) / span
                : 0.5; // all equal → neutral
        }

        var selected = new List<HybridHit>(topK);
        var remaining = new List<HybridHit>(usableHits);
        var selectedEmbeddings = new List<float[]>(topK);

        while (selected.Count < topK && remaining.Count > 0)
        {
            HybridHit? best = null;
            double bestScore = double.NegativeInfinity;

            foreach (var candidate in remaining)
            {
                var candidateVec = embeddings[candidate.ChunkId];
                double maxSim = 0.0;
                foreach (var sVec in selectedEmbeddings)
                {
                    var sim = CosineSimilarity(candidateVec, sVec);
                    if (sim > maxSim) maxSim = sim;
                }

                var mmrScore = clampedLambda * rels[candidate.ChunkId]
                              - (1.0 - clampedLambda) * maxSim;

                if (mmrScore > bestScore)
                {
                    bestScore = mmrScore;
                    best = candidate;
                }
            }

            if (best is null) break;
            selected.Add(best);
            selectedEmbeddings.Add(embeddings[best.ChunkId]);
            remaining.Remove(best);
        }

        return selected;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0.0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        if (na < 1e-12 || nb < 1e-12) return 0.0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }
}
```

- [ ] **Step 4: Run tests**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~MmrDiversifierTests`
Expected: all 7 tests pass.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/Diversification/MmrDiversifier.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Diversification/MmrDiversifierTests.cs
git commit -m "feat(ai): add MmrDiversifier pure algorithm with min-max relevance normalisation"
```

---

## Task 7: Wire MMR into `RagRetrievalService`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs`

- [ ] **Step 1: Add the MMR branch between rerank and `.Take(topK)`**

Locate the section around `rerankedHits = rerankResult?.Ordered ?? alignedPool;` followed by `var topKHits = rerankedHits.Take(topK).ToList();`. Insert the MMR stage between them:

```csharp
IReadOnlyList<HybridHit> rerankedHits = rerankResult?.Ordered ?? alignedPool;

// ...existing rerank-result logging/tracing...

// 7b. (Plan 4b-7) Optional MMR diversification. Off by default; when enabled it
// replaces rerank's top-K order with a diversity-aware ordering using chunk embeddings
// fetched from the vector store. Any transient failure / timeout / empty embedding
// map degrades to the rerank order with "mmr-diversify" recorded in DegradedStages.
IReadOnlyList<HybridHit> orderedForTopK = rerankedHits;
if (_settings.EnableMmr && rerankedHits.Count > topK)
{
    var mmrResult = await WithTimeoutAsync(
        async innerCt =>
        {
            var pointIds = rerankedHits.Select(h => h.ChunkId).ToList();
            var embeddings = await _vectorStore.GetVectorsByIdsAsync(tenantId, pointIds, innerCt);
            return MmrDiversifier.Diversify(rerankedHits, embeddings, _settings.MmrLambda, topK);
        },
        _settings.StageTimeoutMmrMs,
        RagStages.MmrDiversify,
        degraded,
        ct);

    if (mmrResult is { Count: > 0 })
        orderedForTopK = mmrResult;
}

var topKHits = orderedForTopK.Take(topK).ToList();
```

Add the using at the top of the file:

```csharp
using Starter.Module.AI.Infrastructure.Retrieval.Diversification;
```

- [ ] **Step 2: Build**

Run: `cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj`
Expected: succeeds.

- [ ] **Step 3: Run existing retrieval tests to confirm no regression (MMR defaults off)**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~Ai.Retrieval`
Expected: all pre-4b-7 retrieval tests still pass (EnableMmr=false means the new branch is skipped).

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs
git commit -m "feat(ai): wire optional MMR diversification stage between rerank and topK"
```

---

## Task 8: Integration test — `EnableMmr=false` preserves existing behaviour

**Files:**
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Diversification/RagRetrievalMmrDisabledTests.cs` (new)

- [ ] **Step 1: Write the test**

```csharp
// boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Diversification/RagRetrievalMmrDisabledTests.cs
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

public class RagRetrievalMmrDisabledTests
{
    [Fact]
    public async Task When_EnableMmr_false_vector_store_GetVectorsByIds_is_not_called()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var pointId = Guid.NewGuid();

        var dbOptions = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-mmr-disabled-{Guid.NewGuid():N}").Options;
        await using var db = new AiDbContext(dbOptions, currentUserService: null);

        var chunk = AiDocumentChunk.Create(
            documentId: documentId, chunkLevel: "child",
            content: "body", chunkIndex: 0, tokenCount: 10,
            qdrantPointId: pointId);
        db.AiDocumentChunks.Add(chunk);
        await db.SaveChangesAsync();

        var vectorStore = new CountingVectorStore
        {
            SearchHits = { new VectorSearchHit(pointId, 0.9m) }
        };
        var keyword = new FakeKeywordSearchService();

        var settings = new AiRagSettings
        {
            TopK = 3,
            RetrievalTopK = 5,
            RerankStrategy = RerankStrategy.Off,
            EnableMmr = false,
        };

        var svc = new RagRetrievalService(
            db, vectorStore, keyword,
            new FakeEmbeddingService(),
            new NoOpQueryRewriter(), new NoOpContextualQueryResolver(),
            new NoOpQuestionClassifier(), new NoOpReranker(),
            new RerankStrategySelector(settings),
            new NoOpNeighborExpander(),
            new TokenCounter(),
            Options.Create(settings),
            NullLogger<RagRetrievalService>.Instance);

        var assistant = AiAssistant.Create(tenantId, "t", null, "x");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var ctx = await svc.RetrieveForTurnAsync(
            assistant, "q", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        vectorStore.GetVectorsCalls.Should().Be(0,
            "MMR is off — the pipeline must not fetch embeddings");
        ctx.DegradedStages.Should().NotContain("mmr-diversify");
    }

    private sealed class CountingVectorStore : IVectorStore
    {
        public List<VectorSearchHit> SearchHits { get; } = new();
        public int GetVectorsCalls { get; private set; }

        public Task EnsureCollectionAsync(Guid t, int s, CancellationToken ct) => Task.CompletedTask;
        public Task UpsertAsync(Guid t, IReadOnlyList<VectorPoint> p, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteByDocumentAsync(Guid t, Guid d, CancellationToken ct) => Task.CompletedTask;
        public Task DropCollectionAsync(Guid t, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
            Guid t, float[] v, IReadOnlyCollection<Guid>? d, int limit, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<VectorSearchHit>>(SearchHits);
        public Task<IReadOnlyDictionary<Guid, float[]>> GetVectorsByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> pointIds, CancellationToken ct)
        {
            GetVectorsCalls++;
            return Task.FromResult<IReadOnlyDictionary<Guid, float[]>>(new Dictionary<Guid, float[]>());
        }
    }
}
```

- [ ] **Step 2: Run test**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~RagRetrievalMmrDisabledTests`
Expected: passes — vector-store fetch counter remains 0.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Diversification/RagRetrievalMmrDisabledTests.cs
git commit -m "test(ai): EnableMmr=false skips vector fetch in RAG pipeline"
```

---

## Task 9: Integration test — `EnableMmr=true` diversifies top-K

**Files:**
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Diversification/RagRetrievalMmrEnabledTests.cs` (new)

- [ ] **Step 1: Write the test**

Goal: seed 4 chunks where 3 have near-duplicate embeddings (cluster A) and 1 is distinct (cluster B). With `TopK=2` and MMR on, the returned children must span both clusters.

```csharp
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

public class RagRetrievalMmrEnabledTests
{
    [Fact]
    public async Task Top_K_spans_distinct_clusters_when_MMR_enabled()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var dbOptions = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-mmr-enabled-{Guid.NewGuid():N}").Options;
        await using var db = new AiDbContext(dbOptions, currentUserService: null);

        var clusterA1 = Guid.NewGuid();
        var clusterA2 = Guid.NewGuid();
        var clusterA3 = Guid.NewGuid();
        var clusterB  = Guid.NewGuid();

        var chunkA1 = AiDocumentChunk.Create(documentId, "child", "A1", 0, 10, qdrantPointId: clusterA1);
        var chunkA2 = AiDocumentChunk.Create(documentId, "child", "A2", 1, 10, qdrantPointId: clusterA2);
        var chunkA3 = AiDocumentChunk.Create(documentId, "child", "A3", 2, 10, qdrantPointId: clusterA3);
        var chunkB  = AiDocumentChunk.Create(documentId, "child", "B",  3, 10, qdrantPointId: clusterB);

        foreach (var c in new[] { chunkA1, chunkA2, chunkA3, chunkB }) db.AiDocumentChunks.Add(c);
        await db.SaveChangesAsync();

        var vectorStore = new FakeVectorStoreWithEmbeddings
        {
            SearchHits =
            {
                new VectorSearchHit(clusterA1, 0.99m),
                new VectorSearchHit(clusterA2, 0.97m),
                new VectorSearchHit(clusterA3, 0.95m),
                new VectorSearchHit(clusterB,  0.50m),
            },
            Embeddings =
            {
                [clusterA1] = new[] { 1f, 0f },
                [clusterA2] = new[] { 1f, 0f },
                [clusterA3] = new[] { 1f, 0f },
                [clusterB]  = new[] { 0f, 1f },
            }
        };

        var settings = new AiRagSettings
        {
            TopK = 2,
            RetrievalTopK = 10,
            RerankStrategy = RerankStrategy.Off,
            EnableMmr = true,
            MmrLambda = 0.5,
        };

        var svc = new RagRetrievalService(
            db, vectorStore, new FakeKeywordSearchService(),
            new FakeEmbeddingService(),
            new NoOpQueryRewriter(), new NoOpContextualQueryResolver(),
            new NoOpQuestionClassifier(), new NoOpReranker(),
            new RerankStrategySelector(settings),
            new NoOpNeighborExpander(),
            new TokenCounter(),
            Options.Create(settings),
            NullLogger<RagRetrievalService>.Instance);

        var assistant = AiAssistant.Create(tenantId, "t", null, "x");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var ctx = await svc.RetrieveForTurnAsync(
            assistant, "q", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        ctx.Children.Should().HaveCount(2);
        var returnedChunkIds = ctx.Children.Select(c => c.ChunkId).ToList();
        returnedChunkIds.Should().Contain(chunkB.Id,
            "MMR must break up the three near-duplicate A-cluster hits and include B");
    }

    private sealed class FakeVectorStoreWithEmbeddings : IVectorStore
    {
        public List<VectorSearchHit> SearchHits { get; } = new();
        public Dictionary<Guid, float[]> Embeddings { get; } = new();

        public Task EnsureCollectionAsync(Guid t, int s, CancellationToken ct) => Task.CompletedTask;
        public Task UpsertAsync(Guid t, IReadOnlyList<VectorPoint> p, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteByDocumentAsync(Guid t, Guid d, CancellationToken ct) => Task.CompletedTask;
        public Task DropCollectionAsync(Guid t, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
            Guid t, float[] v, IReadOnlyCollection<Guid>? d, int limit, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<VectorSearchHit>>(SearchHits);
        public Task<IReadOnlyDictionary<Guid, float[]>> GetVectorsByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> pointIds, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<Guid, float[]>>(Embeddings);
    }
}
```

- [ ] **Step 2: Run test**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~RagRetrievalMmrEnabledTests`
Expected: passes — cluster B's chunk is included in the 2-chunk result.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Diversification/RagRetrievalMmrEnabledTests.cs
git commit -m "test(ai): EnableMmr=true diversifies top-K across clusters"
```

---

## Task 10: Integration test — MMR degradation on Qdrant failure

**Files:**
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Diversification/RagRetrievalMmrDegradationTests.cs` (new)

- [ ] **Step 1: Write the test**

```csharp
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

public class RagRetrievalMmrDegradationTests
{
    [Fact]
    public async Task When_GetVectorsByIds_throws_MMR_stage_degrades_and_rerank_order_survives()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var dbOptions = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-mmr-degrade-{Guid.NewGuid():N}").Options;
        await using var db = new AiDbContext(dbOptions, currentUserService: null);

        var p1 = Guid.NewGuid(); var p2 = Guid.NewGuid(); var p3 = Guid.NewGuid();
        foreach (var id in new[] { p1, p2, p3 })
            db.AiDocumentChunks.Add(AiDocumentChunk.Create(
                documentId, "child", "body", 0, 10, qdrantPointId: id));
        await db.SaveChangesAsync();

        var vectorStore = new ThrowingGetVectorsStore
        {
            SearchHits =
            {
                new VectorSearchHit(p1, 0.9m),
                new VectorSearchHit(p2, 0.8m),
                new VectorSearchHit(p3, 0.7m),
            }
        };

        var settings = new AiRagSettings
        {
            TopK = 2,
            RetrievalTopK = 5,
            RerankStrategy = RerankStrategy.Off,
            EnableMmr = true,
            StageTimeoutMmrMs = 500,
        };

        var svc = new RagRetrievalService(
            db, vectorStore, new FakeKeywordSearchService(),
            new FakeEmbeddingService(),
            new NoOpQueryRewriter(), new NoOpContextualQueryResolver(),
            new NoOpQuestionClassifier(), new NoOpReranker(),
            new RerankStrategySelector(settings),
            new NoOpNeighborExpander(),
            new TokenCounter(),
            Options.Create(settings),
            NullLogger<RagRetrievalService>.Instance);

        var assistant = AiAssistant.Create(tenantId, "t", null, "x");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var ctx = await svc.RetrieveForTurnAsync(
            assistant, "q", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        ctx.DegradedStages.Should().Contain("mmr-diversify");
        ctx.Children.Should().HaveCount(2,
            "rerank order should survive even when MMR fails");
    }

    private sealed class ThrowingGetVectorsStore : IVectorStore
    {
        public List<VectorSearchHit> SearchHits { get; } = new();
        public Task EnsureCollectionAsync(Guid t, int s, CancellationToken ct) => Task.CompletedTask;
        public Task UpsertAsync(Guid t, IReadOnlyList<VectorPoint> p, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteByDocumentAsync(Guid t, Guid d, CancellationToken ct) => Task.CompletedTask;
        public Task DropCollectionAsync(Guid t, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
            Guid t, float[] v, IReadOnlyCollection<Guid>? d, int limit, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<VectorSearchHit>>(SearchHits);
        public Task<IReadOnlyDictionary<Guid, float[]>> GetVectorsByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> pointIds, CancellationToken ct)
            => throw new TimeoutException("simulated qdrant retrieve outage");
    }
}
```

- [ ] **Step 2: Run test**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~RagRetrievalMmrDegradationTests`
Expected: passes — `mmr-diversify` appears in degraded stages, 2 children still returned.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/Diversification/RagRetrievalMmrDegradationTests.cs
git commit -m "test(ai): MMR stage degrades gracefully on vector-store failure"
```

---

## Task 11: Document MMR knobs in `appsettings.json` / `appsettings.Development.json`

**Files:**
- Modify: `boilerplateBE/src/Starter.Api/appsettings.json`
- Modify: `boilerplateBE/src/Starter.Api/appsettings.Development.json`

- [ ] **Step 1: Add to `appsettings.json` inside `AI:Rag`, adjacent to the other stage-level flags**

Insert near the other `StageTimeout*` keys:

```json
"EnableMmr": false,
"MmrLambda": 0.7,
"StageTimeoutMmrMs": 2000,
```

- [ ] **Step 2: Mirror into `appsettings.Development.json`** — same three keys.

- [ ] **Step 3: Build**

Run: `cd boilerplateBE && dotnet build src/Starter.Api/Starter.Api.csproj`
Expected: succeeds.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/Starter.Api/appsettings.json boilerplateBE/src/Starter.Api/appsettings.Development.json
git commit -m "chore(ai): document MMR knobs (EnableMmr, MmrLambda, StageTimeoutMmrMs) in appsettings"
```

---

## Task 12: Full test suite run

- [ ] **Step 1: Run the whole AI test suite**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~Starter.Api.Tests.Ai`
Expected: all green.

- [ ] **Step 2: Run the whole boilerplate test suite**

Run: `cd boilerplateBE && dotnet test`
Expected: 356 baseline + 4b-7 new tests (≈10 new) all pass.

- [ ] **Step 3: No commit** — if anything fails, fix in the relevant task and commit separately.
