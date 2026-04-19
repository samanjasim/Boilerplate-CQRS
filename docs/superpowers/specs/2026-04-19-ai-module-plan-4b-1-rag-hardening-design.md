# Plan 4b-1 — RAG Hardening Design

**Status:** Draft — pending user approval before writing the implementation plan.
**Scope:** A focused hardening pass on the RAG pipeline landed in Plan 4b. Lifts items that are high-leverage and low-risk from the tutor-AI comparison. Explicitly excludes query expansion + reranker (already scoped in Plan 4b-2) and structure-aware chunking (separate plan).

## Goal

Improve retrieval quality, cost, and observability without changing the module's public surface. Every change is internal to `Starter.Module.AI.Infrastructure` + settings + tests.

## Why now

Plan 4b shipped a minimal working hybrid RAG pipeline. Comparison with the tutor-AI service surfaced five concrete gaps that ship with quiet failure modes:

1. **Min-max hybrid fusion is fragile.** When one of the two hit-lists has a single entry or all entries tied, normalized scores collapse to `1.0` for every item — the other list then dominates. RRF is scale-free and is the de-facto industry standard for hybrid fusion.
2. **Embedding is re-run every chat turn** for the same user query, doubling latency and provider cost.
3. **One blanket try/catch** wraps retrieval. A slow FTS query or embedding call will stall the whole chat turn until the outer cancellation token fires; we can't tell which stage failed.
4. **Re-ingesting a document orphans the old vectors** in Qdrant (chunks are upserted; the previous set is never deleted), and we re-process identical bytes unconditionally — wasting OCR, chunking, and embedding cost.
5. **No unit tests cover fusion or retrieval math.** Any regression to the fusion algorithm (or the upcoming RRF swap) ships silently.

## Non-goals

- Query expansion / LLM query rewriting → Plan 4b-2.
- LLM reranker → Plan 4b-2.
- Structure-aware markdown chunker, `ChunkType` enum → separate plan.
- Neighbor / sibling chunk expansion → Plan 4b-2 or later.
- Prometheus/webhook RAG metrics → tracked separately; this plan adds per-stage logs only.
- ES migration, index versioning / alias swap, circuit breakers → not porting.

## Architecture

No new public interfaces, no DI shape changes for consumers. All work is in:

- `Infrastructure/Retrieval/HybridScoreCalculator.cs` — algorithm swap
- `Infrastructure/Retrieval/RagRetrievalService.cs` — per-stage timeouts, degraded telemetry
- `Infrastructure/Ingestion/CachingEmbeddingService.cs` (new) — transparent decorator
- `Infrastructure/Consumers/ProcessDocumentConsumer.cs` — fingerprint skip + delete-before-upsert
- `Infrastructure/Persistence/Configurations/AiDocumentConfiguration.cs` + `AiDocument` entity — new `ContentHash` column
- `Application/Services/Retrieval/RetrievedContext.cs` — add `DegradedStages`
- `Infrastructure/Settings/AiRagSettings.cs` — new knobs
- `Application/Services/Ingestion/IVectorStore.cs` — add `DeleteByDocumentAsync` (implementation: Qdrant filter-delete)
- `Infrastructure/Persistence/QdrantVectorStore.cs` — implement new method
- `Infrastructure/DependencyInjection.cs` — register embedding decorator
- `tests/Starter.Api.Tests/Ai/Retrieval/*` — new test files

## Components

### 1. Reciprocal Rank Fusion (RRF)

**Rationale:** RRF is scale-free, unaffected by raw score distribution, robust when one list is empty/tiny, and extends naturally to multi-query fusion (which 4b-2 needs). The min-max approach has to be replaced before we add query rewriting.

**New signature:**

```csharp
public static IReadOnlyList<HybridHit> Combine(
    IReadOnlyList<IReadOnlyList<VectorSearchHit>> semanticLists,
    IReadOnlyList<IReadOnlyList<KeywordSearchHit>> keywordLists,
    decimal vectorWeight,
    decimal keywordWeight,
    int rrfK,
    decimal minScore);
```

- Each hit-list represents one query variant (Plan 4b always passes one list per side; 4b-2 can pass N).
- For each `(listSource, chunkId)`, contribution = `weight / (rrfK + rank + 1)` where `rank` is 0-indexed inside that list.
- Sum contributions across all lists per `chunkId`. Filter by `minScore`, sort by score desc, break ties by `chunkId` (deterministic).
- `HybridHit.SemanticScore` / `KeywordScore` = max raw score seen across lists (kept for downstream display/logging).
- `HybridHit.HybridScore` = total RRF score.

**Settings:**

```csharp
public int RrfK { get; init; } = 60;          // industry standard
public decimal VectorWeight { get; init; } = 1.0m;
public decimal KeywordWeight { get; init; } = 1.0m;
// HybridSearchWeight removed
```

`HybridSearchWeight` (the old `alpha`) is **replaced**, not kept. Rationale: semantically different meaning (weighted-sum vs RRF-contribution). Leaving both invites confusion; there is no backwards-compat constraint since we're pre-release.

**Callers:**

`RagRetrievalService.RetrieveForQueryAsync` wraps its single vector + single keyword list in one-element arrays:

```csharp
var mergedHits = HybridScoreCalculator.Combine(
    [vectorHits], [keywordHits],
    _settings.VectorWeight, _settings.KeywordWeight,
    _settings.RrfK, minHybrid);
```

### 2. Embedding cache (query path only)

A decorator on `IEmbeddingService`. Caches **single-text** embed calls (query path); passes multi-text calls straight through (document ingestion — low hit rate, large payloads, not worth the churn).

**Key:** `ai:embed:{providerType}:{model}:{sha256(text)}` — providerType + model prevent dimension/provider mixups; text hash prevents collisions.

**Storage:** `IDistributedCache` (already wired to Redis via `ICacheService`). Value = raw `float[]` serialized as a length-prefixed byte array (binary, not JSON — faster + smaller for 1536-float vectors).

**Bypass:** always when `texts.Count != 1`. Never cache failures.

**TTL:** `AiRagSettings.EmbeddingCacheTtlSeconds = 3600` (1h). Query-embedding outputs don't expire per-se, but capping TTL bounds Redis growth.

**VectorSize propagation:** the decorator must keep the underlying `IEmbeddingService.VectorSize` visible. Simplest: delegate to the inner service's property. If the first-ever call is a cache hit, `VectorSize` stays at its initial `-1` value. We'll work around this by eagerly setting `_vectorSize` from the cached vector's length on a cache hit.

**Registration:** replace the existing `services.AddScoped<IEmbeddingService, EmbeddingService>()` with:

```csharp
services.AddScoped<EmbeddingService>();          // concrete, for the decorator to resolve
services.AddScoped<IEmbeddingService>(sp =>
    new CachingEmbeddingService(
        sp.GetRequiredService<EmbeddingService>(),
        sp.GetRequiredService<ICacheService>(),
        sp.GetRequiredService<IOptions<AiRagSettings>>()));
```

Usage-log attribution is unaffected — the inner `EmbeddingService` still writes `AiUsageLog` rows for actual provider calls. Cache hits skip the provider entirely, so no log row is written, which is correct.

### 3. Per-stage timeouts + degraded telemetry

Goal: no single stage can block the chat turn for more than its configured budget, and the caller knows which stage(s) failed.

**Helper** (private in `RagRetrievalService`):

```csharp
private async Task<T?> WithTimeoutAsync<T>(
    Func<CancellationToken, Task<T>> op,
    TimeSpan timeout,
    string stageName,
    List<string> degraded,
    CancellationToken ct) where T : class
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(timeout);
    try { return await op(cts.Token); }
    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
    {
        degraded.Add(stageName);
        _logger.LogWarning("RAG stage '{Stage}' timed out after {Timeout}ms", stageName, timeout.TotalMilliseconds);
        return null;
    }
    catch (Exception ex)
    {
        degraded.Add(stageName);
        _logger.LogError(ex, "RAG stage '{Stage}' failed", stageName);
        return null;
    }
}
```

**Wrapped stages:** `embed-query`, `vector-search`, `keyword-search`. (Chunk/parent DB lookups stay unwrapped — they're fast and a failure here is a real bug, not a degradation.)

**Fallback semantics:** if a stage returns null,
- `embed-query` null → abort with `RetrievedContext.Empty(degradedStages: [..])`, since we can't do vector search at all.
- `vector-search` null → treat as empty list; keyword-only retrieval.
- `keyword-search` null → treat as empty list; vector-only retrieval.

**Settings:**

```csharp
public int StageTimeoutEmbedMs { get; init; } = 5_000;
public int StageTimeoutVectorMs { get; init; } = 5_000;
public int StageTimeoutKeywordMs { get; init; } = 3_000;
```

**Telemetry:** extend `RetrievedContext`:

```csharp
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

`ChatExecutionService.RetrieveContextSafelyAsync` already logs an aggregate line at source-line 608; extend it to include `DegradedStages` count + comma-joined names.

### 4. Fingerprint skip + delete-before-upsert

**Domain change:** add `string? ContentHash` to `AiDocument`.

Populated at upload time by `UploadDocumentCommandHandler` — compute `SHA-256` of the file stream after it's written to storage. Stored as lowercase hex (64 chars).

**On re-ingest** (reprocess or duplicate upload): in `ProcessDocumentConsumer` before extraction, check if there exists another `AiDocument` with:

- same `TenantId` (or both null),
- same `ContentHash`,
- `EmbeddingStatus == Completed`,
- `Id != doc.Id`.

If found, **copy the existing doc's chunks** (rewriting `DocumentId` to the new doc) and vector points (new `QdrantPointId` per child, since point IDs must be unique in Qdrant), mark the new doc `Completed(chunkCount)`, return. Skips extract/chunk/embed entirely.

**Vector cleanup on reprocess:** `ReprocessDocumentCommandHandler` currently calls `ResetForReprocessing`. Augment so that before re-queueing, it:

1. Deletes all existing `AiDocumentChunk` rows for the doc.
2. Calls `IVectorStore.DeleteByDocumentAsync(tenantId, doc.Id, ct)`.

**New vector store method:**

```csharp
// IVectorStore
Task DeleteByDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct);
```

Qdrant impl: uses the client's `DeleteAsync` with a filter on payload `DocumentId`.

**Edge case:** if the fingerprint-match query returns a doc whose chunks are gone (user hard-deleted something), fall back to re-processing normally. Don't block.

**Cross-tenant deduplication:** not done. Same bytes uploaded by tenant A and tenant B become two independent embedding sets. Cross-tenant cache would raise data-leak concerns and save negligible cost for a multi-tenant SaaS.

### 5. Retrieval unit tests

Cover the risky algorithm changes:

**`HybridScoreCalculatorTests`**
- `Combine_SingleVectorHit_SingleKeywordHit_DifferentIds_BothReturned`
- `Combine_SameId_InBothLists_ScoresSum`
- `Combine_EmptyKeyword_ReturnsVectorListOrdered`
- `Combine_EmptyVector_ReturnsKeywordListOrdered`
- `Combine_AllEmpty_ReturnsEmpty`
- `Combine_MultipleVectorLists_RanksAccumulate` (validates 4b-2 readiness)
- `Combine_MinScoreFilter_DropsLowScores`
- `Combine_TieBreakByChunkId_IsDeterministic`

**`RagRetrievalServiceTests`** (new cases on top of the existing file)
- `Embed_Timeout_ReturnsEmpty_WithEmbedStageDegraded`
- `VectorSearch_Fails_ReturnsKeywordOnly_WithVectorStageDegraded`
- `KeywordSearch_Fails_ReturnsVectorOnly_WithKeywordStageDegraded`
- `Normal_Path_DegradedStages_IsEmpty`

**`CachingEmbeddingServiceTests`**
- `Hit_ReturnsCachedVector_WithoutCallingInner`
- `Miss_CallsInner_AndStoresResult`
- `MultiText_BypassesCache`
- `SetsVectorSizeFromCacheHit`

**`ProcessDocumentConsumerTests`** (existing file; add cases)
- `Fingerprint_Match_Clones_Chunks_And_Skips_Embedding`
- `Fingerprint_Match_But_Source_Deleted_FallsBack_To_Processing`

All tests mock `IVectorStore`, `IKeywordSearchService`, `IEmbeddingService`, `ICacheService` using existing fakes or simple test doubles. No new fixtures required beyond what's already in `tests/Starter.Api.Tests/Ai/`.

## Data flow changes

Before:

```
embed(query) ──┬── vector.Search ──┐
               └── keyword.Search ─┴── minMaxFuse → topK → DB lookup by Id → parents by Id
```

After:

```
embed(query) [cached] ─┬── vector.Search [5s timeout] ──┐
                       └── keyword.Search [3s timeout] ─┴── RRF → topK → DB lookup by QdrantPointId → parents by Id
                          ↓ on failure
                       record degradedStages, proceed with the other side
```

Plus on ingest:

```
upload → hash + store → check duplicate-fingerprint → clone-or-process
reprocess → delete old chunks + vectors → re-queue
```

## Settings delta

```diff
 public sealed class AiRagSettings
 {
-    public double HybridSearchWeight { get; init; } = 0.7;
+    public decimal VectorWeight { get; init; } = 1.0m;
+    public decimal KeywordWeight { get; init; } = 1.0m;
+    public int RrfK { get; init; } = 60;
+
+    public int EmbeddingCacheTtlSeconds { get; init; } = 3600;
+    public int StageTimeoutEmbedMs { get; init; } = 5_000;
+    public int StageTimeoutVectorMs { get; init; } = 5_000;
+    public int StageTimeoutKeywordMs { get; init; } = 3_000;
 }
```

`appsettings.Development.json` (and `appsettings.json`) updates: replace `HybridSearchWeight` with the three new keys, add the four ints. Comments in the json (via `_comment` sibling keys if we already use that convention) explaining the RRF move.

## Migration & backfill

- **`AiDocument.ContentHash` column** — nullable string(64). The boilerplate does not ship EF migrations; consuming apps will regenerate theirs.
- Existing docs keep `ContentHash = null` → fingerprint lookup skips nulls, so nothing dedupes retroactively. That's fine.
- No Qdrant migration: point payloads stay identical.

## Risks & mitigations

| Risk | Mitigation |
|---|---|
| RRF produces different top-K than min-max for existing chats, "regressing" answers on demo queries | Land tests + manual chat smoke in `_testAiRag2` before committing. Tune `VectorWeight` / `KeywordWeight` defaults if a real regression shows up. |
| Cache returns a vector from an old model after the embedding model changes | Key includes the model name; a model switch invalidates the cache automatically. |
| Fingerprint skip copies chunks whose parent `AiDocumentChunk` rows have been soft-deleted | Chunk entity has no soft-delete; we delete hard on reprocess. Lookup predicate requires `Completed` status + chunks actually existing. Fall back to normal processing on any mismatch. |
| Delete-by-document fails silently in Qdrant | Log at `Error`, do not swallow. Reprocess handler surfaces as Failed if the delete throws; operator retries. |
| Decorator breaks `VectorSize` on cold cache hit | Decorator explicitly sets `_vectorSize` from the cached `float[].Length` on first hit; test covers this. |

## Estimated effort

| Item | Effort |
|---|---|
| 1. RRF fusion + settings migration | ~3h |
| 2. Embedding cache decorator | ~4h |
| 3. Per-stage timeouts + degraded telemetry | ~5h |
| 4. Fingerprint + delete-before-upsert | ~5h |
| 5. Unit tests | ~6h |
| **Total** | **~2.5 days** |

## Open questions

None blocking. Items I decided without asking, flagged in case you want them changed:

- **Drop `HybridSearchWeight` rather than deprecate.** Cleaner; pre-release so no compat burden.
- **Cache only query embeds (single-text).** Document embeds are one-shot; caching them wastes Redis RAM on payloads that'll never be requested again.
- **No cross-tenant fingerprint dedup.** Data isolation outweighs the marginal cost saving.
- **Chat turn continues on any single stage failure.** We don't ever refuse to answer due to retrieval; at worst the LLM gets no context (existing 4b behavior).
