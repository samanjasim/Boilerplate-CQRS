# Plan 4b-2 — Query Intelligence Design

**Status:** Draft — follow-on to Plan 4b-1. Pending user approval before implementation plan.

**Depends on:** Plan 4b-1 (needs RRF's multi-list `Combine`, `ArabicTextNormalizer`, per-stage timeouts + `DegradedStages`).

## Goal

Raise retrieval precision and recall by (a) rewriting the user query into multiple variants before searching, (b) re-ranking the fused hit set with an LLM, (c) widening each kept hit with its adjacent chunks, and (d) letting a lightweight classifier decide when each of (a)/(b) should run. All four stages are gated by flags (`EnableQueryExpansion`, `EnableReranking`) already present in `AiRagSettings`.

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

### 2. `IReranker` — LLM-based final ordering

**Contract:**

```csharp
public interface IReranker
{
    Task<IReadOnlyList<HybridHit>> RerankAsync(
        string query,
        IReadOnlyList<HybridHit> candidates,
        IReadOnlyList<AiDocumentChunk> candidateChunks,
        CancellationToken ct);
}
```

Runs **after RRF fusion, before token-budget trim**. Reorders the top `TopK × RerankPoolMultiplier` (default 3×). If the reranker fails, returns candidates unchanged (deterministic fallback to RRF order).

**Prompt shape:**

System message: "You rank document excerpts by relevance to a query. You may see queries and excerpts in Arabic or English. Respond with a JSON array of integer indices, most relevant first. Include every input index exactly once."

User message: query + a numbered list of excerpts (first 500 chars each, preceded by `(page N, doc "X")`). Indices are 0-based.

Output: JSON array → map indices back to `HybridHit`s, drop any missing (shouldn't happen; LLM is instructed to include all), append anything missed in the original RRF order for robustness.

**Caching:** key `ai:rerank:{providerType}:{model}:{sha256(query|sortedChunkIds)}`, TTL `RerankCacheTtlSeconds = 1800`.

**Settings:**

```csharp
public int RerankPoolMultiplier { get; init; } = 3;
public int RerankCacheTtlSeconds { get; init; } = 1800;
public string? RerankerModel { get; init; } = null;   // null → use chat model from tenant config
public int StageTimeoutRerankMs { get; init; } = 8_000;
```

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

**Classifier output:** `QuestionType` enum with 4 values:
- `Greeting` — skip retrieval entirely (chat handles directly)
- `Definition` / `Factoid` — enable rewrites, **skip** rerank (keyword match is usually exact)
- `Reasoning` — enable rewrites + rerank (hardest case; maximum help)
- `Listing` — enable rewrites, skip rerank (completeness matters more than reorder)

Default when classifier fails / disabled: `Reasoning` (safest — invokes everything).

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
[classify] (optional, gates downstream)
  ↓
[rewrite] → [original, variant1, variant2]  (each ArabicNormalized)
  ↓
[embed each]  ← cache hits most embeddings
  ↓
[vectorSearch×N]  [keywordSearch×N]
  ↓                 ↓
  └── RRF multi-list fuse ──┐
                            ↓
                     [rerank top K×3]
                            ↓
                        top K hits
                            ↓
              [parent + sibling expand]
                            ↓
                    TrimToBudget → RetrievedContext
```

Every stage wrapped in `WithTimeoutAsync`; failure = record in `DegradedStages`, proceed with the best we have.

## Risks & mitigations

| Risk | Mitigation |
|---|---|
| LLM rewriter hallucinates irrelevant queries that drag precision down | Cap to 3 variants, keep RRF (caps any single variant's influence), track a `retrieval.rewritten_hits_unused_ratio` metric in 4b-4 to detect runaway rewrites |
| LLM reranker occasionally refuses / outputs malformed JSON | `JsonArrayExtractor` tolerates markdown-wrapped output; any missed indices append in RRF order (never drop candidates) |
| Neighbor expansion blows the token budget for short queries | Neighbors trim first when budget tight; `NeighborWindow=0` default |
| Classifier trained on English patterns misclassifies Arabic greetings | Regex layer has explicit Arabic branches; LLM layer receives Arabic input directly |
| Rewriter cache returns stale variants when the embedding model changes | Cache key includes provider+model; model change invalidates automatically |
| Combined stage latency blows chat turn | Per-stage timeouts from 4b-1; worst case all stages time out → falls back to 4b-1 hybrid behavior |

## Estimated effort

| Item | Effort |
|---|---|
| 1. Query rewriter (rule + LLM + cache + Arabic prompt) | ~1.5d |
| 2. Reranker (prompt + JSON extractor + cache + fallback) | ~1.5d |
| 3. Neighbor expansion | ~0.5d |
| 4. Question classification (regex + LLM, optional) | ~1d |
| Arabic test corpus + integration tests | ~1d |
| **Total** | **~5.5 days** |

## Open questions

- **Classifier default off?** On first roll-out, disabled — rewrites + rerank run on everything. Turn on later once we have telemetry showing where they shouldn't run.
- **Rerank pool size** (`TopK × 3`) — tutor-AI uses 3-5×. Start at 3, tune by metric once 4b-4 ships.
- **Single-message reranker call vs per-pair scoring.** Using single message (JSON indices) — much cheaper, slightly less accurate. Revisit if quality proves insufficient.
