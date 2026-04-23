# Plan 4b-1 — RAG Hardening + Arabic Foundations Design

**Status:** Draft — pending user approval before writing the implementation plan.
**Scope:** A focused hardening pass on the RAG pipeline landed in Plan 4b, plus the Arabic foundations the rest of the follow-on plans depend on.

**Follow-on plans (each independently shippable, ordered):**
- **4b-2** — Query intelligence (LLM + Arabic rule-based rewriter, LLM reranker, neighbor expansion, question classifier)
- **4b-3** — Structure-aware markdown chunker + `ChunkType` (Arabic-aware)
- **4b-4** — Observability (OTel stage metrics, RAG lifecycle events)

## Audience assumption — Arabic first-class

A large portion of users will write Arabic queries and upload Arabic documents. Every item in this spec is evaluated against that assumption:

- **Vector side** handles Arabic fine with multilingual embedding models (OpenAI `text-embedding-3-small/large`, Cohere multilingual). No work needed beyond model choice, which is already a provider setting.
- **Keyword side** does NOT handle Arabic well by default: Postgres FTS with `'english'` config ignores Arabic stemming, diacritics split tokens incorrectly, and alef/ya variants are treated as distinct words. This is the biggest correctness gap and is fixed in this plan.
- **Tokenizer** (`cl100k_base` via SharpToken) under-counts Arabic characters per token — our token budget over-fills for Arabic-heavy content. Tracked as a known limitation in 4b-3; not blocking here.
- **Prompts** (reranker, rewriter) arrive in 4b-2 and must be language-neutral instructions with explicit Arabic support.

## Goal

Improve retrieval quality, cost, and observability without changing the module's public surface. Every change is internal to `Starter.Module.AI.Infrastructure` + settings + tests.

## Why now

Plan 4b shipped a minimal working hybrid RAG pipeline. Comparison with the tutor-AI service surfaced six concrete gaps that ship with quiet failure modes:

1. **Min-max hybrid fusion is fragile.** When one of the two hit-lists has a single entry or all entries tied, normalized scores collapse to `1.0` for every item — the other list then dominates. RRF is scale-free, is the de-facto industry standard for hybrid fusion, and extends cleanly to multi-query fusion (4b-2 needs that).
2. **Embedding is re-run every chat turn** for the same user query, doubling latency and provider cost.
3. **One blanket try/catch** wraps retrieval. A slow FTS query or embedding call will stall the whole chat turn until the outer cancellation token fires; we can't tell which stage failed.
4. **Re-ingesting a document orphans the old vectors** in Qdrant (chunks are upserted; the previous set is never deleted), and we re-process identical bytes unconditionally — wasting OCR, chunking, and embedding cost.
5. **Arabic keyword search is effectively broken.** The FTS index uses `to_tsvector('english', content)`. For Arabic rows this produces near-useless tokens — alef/hamza variants become distinct words, diacritics split tokens, no stemming. Hybrid retrieval collapses to vector-only for Arabic queries without any signal that it's degraded.
6. **No unit tests cover fusion or retrieval math.** Any regression to the fusion algorithm (or the upcoming RRF swap) ships silently.

## Non-goals (moved to dedicated successor plans, not dropped)

- **Query expansion / LLM query rewriting + Arabic rule-based normalization** → **Plan 4b-2**.
- **LLM reranker (language-neutral prompt, Arabic-capable)** → **Plan 4b-2**.
- **Neighbor / sibling chunk expansion** → **Plan 4b-2**.
- **Question classification** → **Plan 4b-2**.
- **Structure-aware markdown chunker + `ChunkType` enum + heading-breadcrumbs + Arabic heading/punctuation awareness** → **Plan 4b-3**.
- **OpenTelemetry stage metrics + RAG lifecycle events (webhook bus)** → **Plan 4b-4**.
- **ES migration, index versioning / alias swap, circuit breakers** → not planned. Postgres FTS + Arabic normalization gives us enough keyword quality without the ops cost of Elasticsearch, per the tutor-AI comparison.

## Architecture

No new public interfaces, no DI shape changes for consumers. All work is in:

- `Infrastructure/Retrieval/HybridScoreCalculator.cs` — algorithm swap (min-max → RRF)
- `Infrastructure/Retrieval/RagRetrievalService.cs` — per-stage timeouts, degraded telemetry, Arabic-aware FTS query building
- `Infrastructure/Retrieval/PostgresKeywordSearchService.cs` — Arabic-normalized FTS
- `Infrastructure/Retrieval/ArabicTextNormalizer.cs` (new) — shared normalizer used on both index + query paths
- `Infrastructure/Ingestion/CachingEmbeddingService.cs` (new) — transparent decorator
- `Infrastructure/Ingestion/HierarchicalDocumentChunker.cs` — apply normalizer to the searchable text before computing the FTS key (preserve original `Content` for display/citation)
- `Infrastructure/Consumers/ProcessDocumentConsumer.cs` — fingerprint skip + delete-before-upsert, populate `NormalizedContent`
- `Infrastructure/Persistence/Configurations/AiDocumentConfiguration.cs` + `AiDocument` entity — new `ContentHash` column
- `Infrastructure/Persistence/Configurations/AiDocumentChunkConfiguration.cs` + `AiDocumentChunk` entity — new `NormalizedContent` column + regenerated `content_tsv` using it
- `Application/Services/Retrieval/RetrievedContext.cs` — add `DegradedStages`
- `Infrastructure/Settings/AiRagSettings.cs` — new knobs (RRF, cache, timeouts, Arabic normalization toggle, FTS language)
- `Application/Services/Ingestion/IVectorStore.cs` — add `DeleteByDocumentAsync` (implementation: Qdrant filter-delete)
- `Infrastructure/Persistence/QdrantVectorStore.cs` — implement new method
- `Infrastructure/DependencyInjection.cs` — register embedding decorator, normalizer
- `tests/Starter.Api.Tests/Ai/Retrieval/*` — new test files including Arabic corpus

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

### 5. Arabic text normalization + FTS language

**Rationale:** Hybrid retrieval's keyword side is effectively broken for Arabic today. `to_tsvector('english', content)` treats `أكاديمي` and `اكاديمي` as distinct tokens, splits diacritics, and does no stemming. Users typing the same question with slightly different spelling get zero keyword hits, and the RRF fusion collapses to vector-only silently.

**Approach: pre-normalize in the application layer, store normalized text, FTS over `'simple'`.**

Why not `to_tsvector('arabic', ...)`:
- Postgres `'arabic'` config exists but depends on ispell dictionaries that are not always installed, not tuned for our content, and stem too aggressively for proper nouns / technical terms. It also breaks when chunks mix Arabic with English code/math (common in our target documents).
- Doing the normalization in C# keeps the logic one-source-of-truth, language-stack independent, and makes the rules testable in xUnit rather than via Postgres round-trips.

Why not switch to Elasticsearch with an Arabic analyzer:
- ES is an extra service + extra ops burden. The tutor-AI comparison showed their Arabic quality gain came primarily from **normalization**, which Postgres can apply equally well once we do it ourselves. Keeping Postgres FTS + `pg_trgm` covers >95% of the quality gap at a fraction of the cost.

**`ArabicTextNormalizer` (new):**

Single static method `Normalize(string input) → string` applying (in order):

1. Strip diacritics (harakat): `\u064B-\u065F`, `\u0670`, `\u0610-\u061A` (tanween, fatha, damma, kasra, shadda, sukun, dagger alef, quranic marks)
2. Strip tatweel (kashida): `\u0640`
3. Normalize alef variants to bare alef: `أ إ آ ٱ` (`\u0623 \u0625 \u0622 \u0671`) → `ا` (`\u0627`)
4. Normalize ya variants: `ى` (`\u0649`) → `ي` (`\u064A`)
5. Normalize ta marbuta → ha: `ة` (`\u0629`) → `ه` (`\u0647`). Controversial — some pipelines keep `ة` distinct. We normalize because query typists frequently elide it, and the gain in recall outweighs the precision loss for Arabic FAQs / documents. Behind a `AiRagSettings.NormalizeTaMarbuta = true` flag so a tenant can disable it.
6. Normalize hamza variants on ya/wa: `ئ ؤ` → `ي و`
7. Normalize Arabic-Indic digits to ASCII (optional, behind `NormalizeArabicDigits = true`).
8. Collapse runs of whitespace to single space; trim.

ASCII / English text passes through unchanged — the rules only affect code points in the Arabic block `\u0600-\u06FF`.

**Schema change:** add a `NormalizedContent` column on `AiDocumentChunk` (nullable string, longer than `Content` since normalization doesn't expand). Regenerate the `content_tsv` generated column to index `to_tsvector('simple', COALESCE(NormalizedContent, Content))`. Rationale: `simple` skips stemming (no wrong stems on proper nouns), and the pre-normalized input has already collapsed alef/ya variants so recall is restored without needing language-specific stemmers.

**Keyword query path:** `PostgresKeywordSearchService.SearchAsync` applies the same `ArabicTextNormalizer.Normalize(queryText)` before calling `plainto_tsquery('simple', ...)`. Both sides now see the same normalized token space.

**Document-ingest path:** in `ProcessDocumentConsumer`, populate `child.NormalizedContent = ArabicTextNormalizer.Normalize(child.Content)` before inserting. `Content` stays the original text — that's what the LLM sees and what citations quote.

**Settings:**

```csharp
public string FtsLanguage { get; init; } = "simple";    // changed from "english"
public bool ApplyArabicNormalization { get; init; } = true;
public bool NormalizeTaMarbuta { get; init; } = true;
public bool NormalizeArabicDigits { get; init; } = true;
```

**Embedding side — no change.** `text-embedding-3-*` and `text-embedding-ada-002` handle Arabic natively; we pass the original `Content` through. Normalizing before embedding would hurt (the embedding model has learned the distinction between `ه` and `ة`, and we'd blur it).

**Migration burden on consuming apps:** the boilerplate does not ship migrations; a generated migration will add `NormalizedContent` (nullable) and regenerate `content_tsv`. Existing rows: `NormalizedContent` stays null → `COALESCE` falls back to raw `Content`, so old rows keep the old (English-default) behavior until backfilled. A backfill command could be added, but we're pre-release so acceptable to require re-ingest.

### 6. Retrieval unit tests

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

**`ArabicTextNormalizerTests`** (new)
- `StripsDiacritics_KeepsBaseLetters`
- `NormalizesAlefVariants_ToBareAlef`
- `NormalizesYa_FromAlefMaksura`
- `NormalizesTaMarbuta_ToHa_WhenEnabled`
- `LeavesTaMarbuta_WhenDisabled`
- `StripsTatweel`
- `LeavesAsciiUnchanged`
- `NormalizesMixedArabicEnglish_TouchesOnlyArabicRanges`
- `NormalizesArabicIndicDigits_WhenEnabled`

**`PostgresKeywordSearchServiceTests`** (existing file; add Arabic-corpus tests — `[Collection("AiPostgres")]`)
- `Arabic_Query_Matches_Chunk_With_Different_Alef_Spelling`
- `Arabic_Query_Matches_Through_Diacritics` (e.g., query `مؤسسة` matches chunk `مُؤَسَّسَة`)
- `Arabic_Query_Respects_Document_Filter`
- `Mixed_Content_Chunk_Keeps_English_Matching` (regression guard: Arabic normalization must not break English FTS)

All tests mock `IVectorStore`, `IKeywordSearchService`, `IEmbeddingService`, `ICacheService` using existing fakes or simple test doubles. The Arabic FTS tests reuse the existing `AiPostgresFixture`; no new fixtures required.

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
+
-    public string FtsLanguage { get; init; } = "english";
+    public string FtsLanguage { get; init; } = "simple";
+    public bool ApplyArabicNormalization { get; init; } = true;
+    public bool NormalizeTaMarbuta { get; init; } = true;
+    public bool NormalizeArabicDigits { get; init; } = true;
 }
```

`appsettings.Development.json` (and `appsettings.json`) updates: replace `HybridSearchWeight` with the three new keys, add the four timeout ints, flip `FtsLanguage` to `simple`, add the three Arabic toggles defaulting to `true`. Comments in the json (via `_comment` sibling keys where we already use that convention) explaining the RRF move and why the Arabic toggles default on.

## Migration & backfill

- **`AiDocument.ContentHash` column** — nullable string(64). The boilerplate does not ship EF migrations; consuming apps will regenerate theirs.
- **`AiDocumentChunk.NormalizedContent` column** — nullable text. Also triggers regeneration of the `content_tsv` generated column: `GENERATED ALWAYS AS (to_tsvector('simple', coalesce(normalized_content, content))) STORED`. Consuming apps re-ingest or run a manual backfill (`UPDATE ai_document_chunks SET normalized_content = ar_normalize(content)` — but we do it in C# at write time, so the backfill is just "re-run the consumer for all completed docs" if they want retroactive coverage).
- Existing docs keep `ContentHash = null` → fingerprint lookup skips nulls, so nothing dedupes retroactively. That's fine.
- Existing chunks keep `NormalizedContent = null` → `content_tsv` falls back to `content` via `COALESCE`, preserving current behavior until re-ingest.
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
| 5. Arabic normalizer + FTS language + schema | ~6h |
| 6. Unit tests (including Arabic FTS corpus) | ~8h |
| **Total** | **~4 days** |

## Open questions

None blocking. Items I decided without asking, flagged in case you want them changed:

- **Drop `HybridSearchWeight` rather than deprecate.** Cleaner; pre-release so no compat burden.
- **Cache only query embeds (single-text).** Document embeds are one-shot; caching them wastes Redis RAM on payloads that'll never be requested again.
- **No cross-tenant fingerprint dedup.** Data isolation outweighs the marginal cost saving.
- **Chat turn continues on any single stage failure.** We don't ever refuse to answer due to retrieval; at worst the LLM gets no context (existing 4b behavior).
- **Arabic normalization in C#, FTS on `'simple'`.** Alternative was `to_tsvector('arabic', ...)` — rejected because the Arabic Postgres config depends on optional ispell dictionaries, stems aggressively (breaks proper nouns), and fails on mixed AR/EN chunks. C# normalization is testable, portable, and recovers >95% of the Arabic recall gain at much lower risk.
- **Ta marbuta normalization defaults to on.** Arguable — some Arabic NLP practitioners prefer keeping `ة` distinct. We default on because users routinely elide it in queries; the recall gain outweighs the precision loss for typical FAQ/document retrieval. Controllable via settings.
- **`NormalizedContent` as a separate column, not a trigger/function.** Keeps the logic in one place (C#), testable without a DB, and doesn't couple the schema to a specific Postgres function. Cost: one extra column + small duplication of text; acceptable.
