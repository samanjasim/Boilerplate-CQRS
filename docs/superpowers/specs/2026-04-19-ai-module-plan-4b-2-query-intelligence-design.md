# Plan 4b-2 — Query Intelligence Design

**Status:** Draft — follow-on to Plan 4b-1. Pending user approval before implementation plan.

**Depends on:** Plan 4b-1 (needs RRF's multi-list `Combine`, `ArabicTextNormalizer`, per-stage timeouts + `DegradedStages`).

## Goal

Raise retrieval precision and recall by (a) rewriting the user query into multiple variants before searching, (b) re-ranking the fused hit set with an LLM, (c) widening each kept hit with its adjacent chunks, and (d) letting a lightweight classifier decide when each of (a)/(b) should run. Gating is via `AiRagSettings`:
- `EnableQueryExpansion` (bool, already present in 4b-1 as a no-op) now wires the rewriter.
- `EnableReranking` (bool, 4b-1 no-op) is **replaced by** `RerankStrategy` (enum). Migration: `true → Auto`, `false → Off`.
- `EnableQuestionClassification` (bool, new).
- `NeighborWindow` (int, new; `0` disables).

**Must work well in Arabic.** Arabic is first-class, not an afterthought — each sub-component below includes its Arabic story.

## Non-goals

- Cross-lingual query (Arabic query → English corpus retrieval). Possible with multilingual embeddings but out of scope. If a user writes an English question against Arabic docs, the vector side still answers.
- Dense retrieval reranker (cross-encoder). LLM reranker is simpler to ship, language-agnostic, and cacheable. A cross-encoder pass may come later.
- Hybrid re-weighting per question type. We tune per-type BM25 boost in tutor-AI; we'll instead gate rerank/rewrite per type, which delivers most of the gain without re-exposing the fusion weights.

## Components

### 1. `IQueryRewriter` — query variants generator

**Contract** (`Application/Services/Retrieval/IQueryRewriter.cs`):

```csharp
public interface IQueryRewriter
{
    Task<IReadOnlyList<string>> RewriteAsync(
        string originalQuery, string? language, CancellationToken ct);
}
```

Returns the **original plus 1-3 rewrites**, deduped, normalized via `ArabicTextNormalizer` to avoid near-identical outputs. Always includes the original as index 0.

**Two layers, composable:**

**Layer 1 — rule-based (fast, deterministic, free):**
Runs for every query. Cheap normalizations:
- Strip leading question words:
  - Arabic: `هل|ماذا|ما|كيف|متى|أين|لماذا|لم|من|أي|كم`
  - English: `what|how|when|where|why|who|which|is|are|can|does|do`
- Strip trailing polite / padding tokens (Arabic: `لو سمحت`, `من فضلك`; English: `please`, `thanks`).
- Strip trailing `?` or `؟`.
- Output the "content-only" variant if it materially differs from the original.

**Layer 2 — LLM rewriter (stronger, gated by `EnableQueryExpansion`):**
- Prompt: language-aware system prompt asking for 2 alternative phrasings that preserve the information need. Explicit Arabic support ("You may receive questions in Arabic or English. Reply in the same language as the input. Do NOT translate.").
- Uses `IAiProviderFactory.CreateForChat()` with the tenant's configured chat model. Temperature low (0.2).
- Output parsed as JSON array of strings via a small `JsonArrayExtractor` helper (accepts either bare array or array embedded in markdown code-block).
- **Fallback:** on parse failure / timeout / provider error → return the rule-based layer's output. Never fail the chat turn.

**Caching:** Redis via `ICacheService`, key `ai:qrw:{providerType}:{model}:{langHint}:{sha256(normalizedQuery)}`, TTL `AiRagSettings.QueryRewriteCacheTtlSeconds = 1800` (30m). Cache only the LLM layer's output (rule-based layer is already near-free).

**Settings:**

```csharp
public int QueryRewriteMaxVariants { get; init; } = 3;
public int QueryRewriteCacheTtlSeconds { get; init; } = 1800;
public int StageTimeoutQueryRewriteMs { get; init; } = 4_000;
```

**Wiring:** `RagRetrievalService.RetrieveForQueryAsync` calls `IQueryRewriter.RewriteAsync` behind `WithTimeoutAsync("query-rewrite", ...)`. Each variant is embedded (single provider call, batched — the embedding cache from 4b-1 absorbs duplicates) and feeds one `VectorSearchHit[]`. All variants also feed one `KeywordSearchHit[]`. All lists go into RRF's multi-list `Combine`.

### 2. `IReranker` — hybrid LLM-based final ordering

We ship **two reranker strategies behind the same contract** and a strategy selector that can run on manual config, classifier output, or both. Both strategies share caching, fallback, and telemetry so operators can swap without code changes.

**Contract:**

```csharp
public interface IReranker
{
    Task<RerankResult> RerankAsync(
        string query,
        IReadOnlyList<HybridHit> candidates,
        IReadOnlyList<AiDocumentChunk> candidateChunks,
        RerankContext context,    // QuestionType?, strategyHint, cancellationToken budget
        CancellationToken ct);
}

public sealed record RerankResult(
    IReadOnlyList<HybridHit> Ordered,
    RerankStrategy StrategyUsed,
    int CandidatesIn,
    int CandidatesScored,
    int CacheHits,
    long LatencyMs,
    int TokensIn,
    int TokensOut,
    decimal UnusedRatio);     // 1 - (kept_after_topK / scored)
```

Runs **after RRF fusion, before token-budget trim**. The caller (`RagRetrievalService`) picks the strategy via `RerankStrategySelector`; the selected strategy reorders its pool; the result is trimmed to `TopK`. On failure, returns `RerankResult` with `Ordered = candidates` and `StrategyUsed = RerankStrategy.FallbackRrf` (deterministic — never drops hits).

**Strategy enum:**

```csharp
public enum RerankStrategy
{
    Off,         // do not rerank; just truncate RRF to TopK
    Listwise,    // one LLM call, JSON array of indices (current 4b-2 plan)
    Pointwise,   // per-(query, excerpt) LLM scoring, parallel
    Auto,        // decision tree below
    FallbackRrf  // only appears in telemetry; set by selector when a concrete strategy errored
}
```

#### 2a. `ListwiseReranker` — single call, JSON indices (cheap path)

Pool size: `TopK × ListwiseRerankPoolMultiplier` (default **3×**).

Prompt:
- **System:** "You rank document excerpts by relevance to a query. You may see queries and excerpts in Arabic or English. Respond with a JSON array of integer indices, most relevant first. Include every input index exactly once."
- **User:** query + numbered list of excerpts (first 500 chars each, preceded by `(page N, doc "X")`), 0-based indices.

Output parsing via `JsonArrayExtractor` (reused from the query rewriter). Missing indices are appended in original RRF order (never drop).

Cache key: `ai:rerank:lw:{providerType}:{model}:{sha256(query|sortedChunkIds)}`, TTL `RerankCacheTtlSeconds`.

**Why default for most queries:** 1 call, ≤ 2k tokens, ~0.4–0.9s P50. Good enough for Definition/Factoid/Listing.

#### 2b. `PointwiseReranker` — parallel per-pair scoring (accurate path)

Pool size: `TopK × PointwiseRerankPoolMultiplier` (default **2×** — pointwise has higher per-item accuracy so smaller pool is fine).

For each `(query, excerpt)` pair, call the chat provider asking for a **numeric relevance score 0.0–1.0 as a single JSON object `{"score": <float>, "reason": "<≤60 chars>"}`**. Excerpt content identical to listwise (first 500 chars + page/doc prefix).

Parallelism: `SemaphoreSlim(PointwiseMaxParallelism)` (default **5**) bounds concurrent provider calls. Higher risks rate limits; lower increases latency.

Score aggregation: sort by score desc. Any pair whose call failed/timed out is assigned the RRF-rank-derived fallback score `1 / (RrfK + origRank + 1)` so it stays in order rather than dropping.

Cutoff: drop hits with score `< MinPointwiseScore` (default **0.3**) before truncating to `TopK`. This is the opt-in "quality gate" pointwise unlocks over listwise.

Per-pair cache: `ai:rerank:pw:{providerType}:{model}:{sha256(query)}:{chunkId}`, TTL `RerankCacheTtlSeconds`. Cache entry is `{score, reason}`. **Cache-reuse property:** repeats of the same `(query, chunkId)` pair across conversations hit the cache; overlapping pools between variant queries in the same turn hit the cache after the first variant. We do not pre-score excerpts offline — the score is query-conditional, so cache keying includes the query.

Budget & safety:
- If more than `PointwiseMaxFailureRatio` (default **0.25**) of pair calls fail, abort pointwise and fall through to listwise.
- If the overall stage exceeds `StageTimeoutRerankMs`, cancel outstanding pair tokens and fall through to listwise with whatever arrived, else RRF.

**Why opt-in for reasoning queries:** ~8× tokens and ~5–15× calls vs listwise, but numeric scores, graceful partial failure, and per-excerpt cache reuse.

#### 2c. `RerankStrategySelector` — decision tree

Given `strategyHintFromSettings ∈ {Off, Listwise, Pointwise, Auto}` (`AiRagSettings.RerankStrategy`, default **Auto**) and `QuestionType?` from the classifier:

```
if settings == Off                        → Off
if settings == Listwise or Pointwise      → settings (respect the override)
if settings == Auto:
    if questionType == Greeting           → Off
    if questionType == Reasoning          → Pointwise
    if questionType == Definition|Factoid → Listwise
    if questionType == Listing            → Listwise
    if questionType == null (classifier off or failed)
                                          → Listwise  (safe, cheap default)
```

Per-tenant overrides: `AiRagSettings` is per-tenant via options binding (existing pattern), so a tenant can force a strategy via config without code change.

#### Shared settings (replaces the previous single-strategy block)

```csharp
public RerankStrategy RerankStrategy { get; init; } = RerankStrategy.Auto;   // was: EnableReranking bool
public int ListwiseRerankPoolMultiplier { get; init; } = 3;
public int PointwiseRerankPoolMultiplier { get; init; } = 2;
public int PointwiseMaxParallelism { get; init; } = 5;
public decimal MinPointwiseScore { get; init; } = 0.3m;
public decimal PointwiseMaxFailureRatio { get; init; } = 0.25m;
public int RerankCacheTtlSeconds { get; init; } = 1800;
public string? RerankerModel { get; init; } = null;   // null → use tenant chat model
public int StageTimeoutRerankMs { get; init; } = 8_000;
```

The legacy boolean `EnableReranking` (declared as a no-op in 4b-1) is **replaced** by `RerankStrategy` — a migration note in `appsettings.Example.json` documents the mapping (`true → Auto`, `false → Off`).

#### Monitoring surface (what 4b-4 will chart)

Every rerank invocation emits telemetry fields (via OpenTelemetry activity tags + per-turn log fields):

| Field | Purpose |
|---|---|
| `rerank.strategy_requested` | What selector asked for (`Auto` / forced) |
| `rerank.strategy_used` | What actually ran (may differ after fallback) |
| `rerank.question_type` | Classifier output at decision time (or `null`) |
| `rerank.candidates_in` | Pool size before rerank |
| `rerank.candidates_scored` | Pointwise: pair calls completed; Listwise: always = in |
| `rerank.cache_hits` | Pointwise only (listwise is single-key) |
| `rerank.latency_ms` | Wall clock for the stage |
| `rerank.tokens_in` / `rerank.tokens_out` | Provider usage |
| `rerank.unused_ratio` | Pool kept after `TopK` trim — high = pool too large |
| `rerank.fell_back` | `true` if selector's choice errored and we used another strategy |

A degraded rerank call still appends `"rerank"` to `DegradedStages` (per 4b-1 telemetry).

#### Decision flow (operator guide)

```
start
  │
  ├─ Want deterministic / zero LLM cost? → set RerankStrategy = Off
  │
  ├─ Want cheap, always-on improvement?  → set RerankStrategy = Listwise
  │                                       (observes cache_hits, latency_ms)
  │
  ├─ Want best quality, per-question?    → set RerankStrategy = Auto
  │                                       + EnableQuestionClassification = true
  │                                       (auto routes Reasoning → Pointwise)
  │
  └─ Want max quality everywhere?        → set RerankStrategy = Pointwise
                                           (watch tokens_in, parallelism cap)
```

Operators flip a single enum. No code deploy, no prompt edit.

### 3. Neighbor / sibling chunk expansion

**Rationale:** Our parent-chunk retrieval gives the LLM the enclosing "section" but not the paragraph **immediately before and after** the hit. That's what ±N neighbor expansion gives the tutor-AI service. Complementary, not a replacement.

**Shape:**

Post-trim (after `TrimToBudget`, before returning), for each kept child chunk, pull the chunks at `(DocumentId, ChunkIndex-W..ChunkIndex+W)` where `W = AiRagSettings.NeighborWindow` (default `0` — off). Add them as a new `RetrievedContext.Siblings` collection with score = parent-hit × 0.5 (tutor-AI's pattern).

Budget enforcement: neighbors compete for the same `MaxContextTokens` budget. If the budget is exhausted, drop neighbors first (they're supplementary).

Deduplication: a neighbor already present as a child hit or a parent hit is skipped.

**Cross-page:** yes. If the previous chunk is on the previous page, include it. The page number travels with the chunk for citation.

**`ContextPromptBuilder` update:** emit siblings under a new `<context>` section or inline between their child hits in `(page, chunkIndex)` reading order. Prefer inline interleaving so the LLM sees natural reading order.

**Settings:**

```csharp
public int NeighborWindow { get; init; } = 0;     // 0 = disabled
public decimal NeighborScoreWeight { get; init; } = 0.5m;
```

### 4. Question classification (optional, lightweight)

**Rationale:** Not every question benefits from rewrites + rerank. "Hello", "thanks", "summarize this doc" don't need hybrid retrieval; a chit-chat question shouldn't trigger an LLM rerank. Tutor-AI's classifier has 9 types; we'll scope tighter to the ones that actually gate behavior in our pipeline.

**Classifier output:** `QuestionType?` — nullable enum with 5 values (grouped into 4 behavioral buckets):

| Value | Behavior |
|---|---|
| `Greeting` | Skip retrieval entirely (chat handles directly) |
| `Definition`, `Factoid` | Enable rewrites, rerank = **Listwise** (cheap, ordering already close) |
| `Reasoning` | Enable rewrites, rerank = **Pointwise** (hardest case; numeric scoring helps) |
| `Listing` | Enable rewrites, rerank = **Listwise** (completeness matters more than reorder) |

When the classifier is disabled or fails, it returns `null`. The `RerankStrategySelector` (Section 2c) treats `null` as `Listwise` — the cheap, safe default. Section 2c is the single source of truth for how `QuestionType?` maps to a strategy; this table is informational.

**Implementation:** two-layer like the rewriter. Layer 1 regex (cheap, language-aware) catches 80%+ of the common cases:
- Greeting regex (Arabic + English): `^\s*(hi|hello|hey|مرحبا|السلام|اهلا|صباح|مساء)`
- Definition: `^(what is|what are|ما هو|ما هي|ماذا يعني|تعريف)`
- Listing: `^(list|enumerate|اذكر|ما هي أنواع)`

Layer 2 LLM classifier behind `EnableQuestionClassification` flag (separate from rewriter toggle so you can have one without the other). Cached same as the rewriter. Low temperature, JSON output.

**Settings:**

```csharp
public bool EnableQuestionClassification { get; init; } = false;  // conservative default
public int QuestionClassifierCacheTtlSeconds { get; init; } = 1800;
public int StageTimeoutClassifyMs { get; init; } = 2_000;
```

## Arabic quality bar

Each feature above includes Arabic handling. The acceptance criteria for the plan as a whole:

- LLM rewriter **must not translate**. A rewrite of `ما هو التمثيل الضوئي؟` must return Arabic variants, not English. Verified by a test harness + sampled manual review.
- Reranker returns correct ordering when both query and excerpts are Arabic. Verified by a test with known-relevant and known-irrelevant Arabic excerpts.
- Rule-based classifier catches the greetings / listings that appear in the Arabic user test corpus (seed corpus maintained under `tests/Starter.Api.Tests/Ai/fixtures/arabic_queries.json`).
- Neighbor expansion preserves RTL reading order when rendering context to the LLM.

## Data flow

```
query
  ↓
[classify] (optional) ──────────────────┐
  ↓                                      │ QuestionType feeds
[rewrite] → [original, v1, v2]           │   RerankStrategySelector
  ↓                                      │
[embed each]  ← embedding cache          │
  ↓                                      │
[vectorSearch×N]   [keywordSearch×N]     │
  ↓                  ↓                   │
  └── RRF multi-list fuse ──┐            │
                            ↓            │
                 ┌──────────────────────┐│
                 │ RerankStrategySelector│<─── AiRagSettings.RerankStrategy
                 └──┬────┬────┬────┬────┘│      (Off|Listwise|Pointwise|Auto)
                    ↓    ↓    ↓    ↓     │
                   Off  LW    PW   Auto──┘
                    │    │    │    │ (routes to Off/LW/PW per QuestionType)
                    └────┴─┬──┴────┘
                           ↓
                      top K hits
                           ↓
             [parent + sibling expand]
                           ↓
                   TrimToBudget → RetrievedContext
```

Every stage wrapped in `WithTimeoutAsync`; failure = record in `DegradedStages`, proceed with the best we have. Strategy fallback chain on error: `Pointwise → Listwise → FallbackRrf (= RRF order unchanged)`.

## Risks & mitigations

| Risk | Mitigation |
|---|---|
| LLM rewriter hallucinates irrelevant queries that drag precision down | Cap to 3 variants, keep RRF (caps any single variant's influence), track a `retrieval.rewritten_hits_unused_ratio` metric in 4b-4 to detect runaway rewrites |
| Listwise reranker refuses / outputs malformed JSON | `JsonArrayExtractor` tolerates markdown-wrapped output; any missed indices append in RRF order (never drop candidates); selector falls through to `FallbackRrf` on persistent error |
| Pointwise rate-limits the provider when pool is large | `PointwiseMaxParallelism=5` default; `PointwiseMaxFailureRatio=0.25` aborts back to listwise; per-pair cache shaves repeated calls on overlapping queries |
| Pointwise cost runaway (8× tokens) for every query | `RerankStrategy.Auto` only routes `Reasoning` queries to pointwise; operators can force `Listwise` globally; tokens emitted to telemetry for 4b-4 cost alerts |
| Auto mode routes wrong strategy because classifier is miscalibrated | Classifier disabled by default; when enabled, selector defaults to `Listwise` on `null`/`Reasoning`-safe fallbacks; operators can override per-tenant via `RerankStrategy` |
| Neighbor expansion blows the token budget for short queries | Neighbors trim first when budget tight; `NeighborWindow=0` default |
| Classifier trained on English patterns misclassifies Arabic greetings | Regex layer has explicit Arabic branches; LLM layer receives Arabic input directly |
| Rewriter cache returns stale variants when the embedding model changes | Cache key includes provider+model; model change invalidates automatically |
| Combined stage latency blows chat turn | Per-stage timeouts from 4b-1; worst case all stages time out → falls back to 4b-1 hybrid behavior |

## Estimated effort

| Item | Effort |
|---|---|
| 1. Query rewriter (rule + LLM + cache + Arabic prompt) | ~1.5d |
| 2a. Listwise reranker (prompt + JSON extractor + cache + fallback) | ~1.5d |
| 2b. Pointwise reranker (parallel scoring + cache + partial-failure handling) | ~1d |
| 2c. Strategy selector + telemetry fields | ~0.5d |
| 3. Neighbor expansion | ~0.5d |
| 4. Question classification (regex + LLM, optional) | ~1d |
| Arabic test corpus + integration tests (incl. both rerank strategies) | ~1.5d |
| **Total** | **~7.5 days** |

## Open questions (resolved)

- **Classifier default off?** Yes. On first roll-out disabled → `RerankStrategy.Auto` treats `questionType == null` as `Listwise`. Enable once telemetry confirms the regex layer covers the corpus.
- **Rerank pool size.** Listwise **3×**, pointwise **2×** (pointwise is more accurate per-item so a smaller pool is fine). Tune from 4b-4 metrics.
- **Single-message vs per-pair reranker.** **Hybrid**, selectable via `RerankStrategy = Auto | Listwise | Pointwise | Off`. Auto mode routes `Reasoning` questions to pointwise and others to listwise; both strategies are always compiled in, always observable, and operator-switchable without deploy. Cost/latency trade-offs documented in the Decision Flow section.
- **`EnableReranking` (legacy flag from 4b-1).** Replaced by `RerankStrategy`. Mapping: `true → Auto`, `false → Off`. Example config will carry a comment noting the migration.

## Future work (not in 4b-2 scope)

- **Cross-encoder reranker** (e.g. `bge-reranker-v2`) as a third strategy — local, cheap, numeric. Would slot into the same `IReranker` contract. Defer until we see pointwise cost/latency data from production.
- **Query-side cache pre-warming** — if the same query is hot, pre-score all candidates asynchronously. Defer until telemetry justifies.
- **Per-tenant model overrides on pointwise** — currently all pointwise pairs use the tenant chat model; a smaller/faster model for pointwise (e.g. Haiku) could cut cost without much quality loss. Defer pending provider-routing work.
