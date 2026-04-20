# Plan 4b-2 Upgrade Notes — Query Intelligence

These notes cover operator-facing changes when upgrading from 4b-1 to 4b-2. No schema migrations were required — `AiDocumentChunk.ChunkIndex` already exists from 4a.

## Setting rename

- `AI:Rag:EnableReranking` (bool) is REMOVED from `AiRagSettings`.
- Replacement: `AI:Rag:RerankStrategy` enum — `Off | Listwise | Pointwise | Auto | FallbackRrf`.
- Migration for existing appsettings:
  - `EnableReranking: false` → `"RerankStrategy": "Off"`
  - `EnableReranking: true`  → `"RerankStrategy": "Auto"` (recommended) or `"Listwise"` (safer default)
- `FallbackRrf` is rejected at startup — it is a runtime-only outcome reported via `RerankResult.StrategyUsed`, not a valid configuration value.

## New settings (AI:Rag)

| Setting | Default | Purpose |
| --- | --- | --- |
| `EnableQueryExpansion` | `true` | When `true`, `QueryRewriter` calls the LLM for 2 alternate phrasings in addition to rule-based variants. When `false`, rule-based only. |
| `QueryRewriteMaxVariants` | `3` | Hard cap on the number of variants fanned out to vector + keyword search. |
| `QueryRewriteCacheTtlSeconds` | `1800` | Redis TTL for LLM-generated rewrite variants, keyed on the normalized query. |
| `StageTimeoutQueryRewriteMs` | `4000` | Per-stage budget. Exceeding adds `query-rewrite` to `DegradedStages`. |
| `RewriterModel` | `null` | Override the rewriter model; `null` uses `IAiProviderFactory.GetDefaultChatModelId()`. |
| `RerankStrategy` | `Auto` | Router between Listwise and Pointwise based on `QuestionType`. Use `Off` to disable. |
| `RerankerModel` | `null` | Override the reranker model; `null` uses the default chat model. |
| `ListwisePoolMultiplier` | `3` | Rerank candidate pool size = `TopK × multiplier`. Listwise reorders the pool, then trims to TopK. |
| `PointwisePoolMultiplier` | `2` | Same, for pointwise strategy. Pointwise scores each candidate in parallel. |
| `PointwiseMaxParallelism` | `5` | Concurrent LLM scoring calls for pointwise. |
| `MinPointwiseScore` | `0.3` | Pointwise scores below this are dropped. |
| `PointwiseMaxFailureRatio` | `0.25` | If more than this fraction of pointwise scoring calls fail, abort pointwise and fall back to Listwise. |
| `RerankCacheTtlSeconds` | `1800` | Redis TTL for rerank output, keyed on query + ordered candidate ids. |
| `StageTimeoutRerankMs` | `8000` | Per-stage budget. Exceeding adds `rerank` to `DegradedStages`; pipeline falls back to RRF order. |
| `ClassifierModel` | `null` | Override the classifier model; `null` uses the default chat model. |
| `QuestionCacheTtlSeconds` | `1800` | Redis TTL for classifier labels, keyed on the normalized query. |
| `StageTimeoutClassifyMs` | `2000` | Per-stage budget. Exceeding adds `classify` to `DegradedStages`; pipeline continues without a question type. |
| `NeighborWindowSize` | `1` | Sibling chunks each side of an anchor. `0` disables expansion. |
| `NeighborScoreWeight` | `0.5` | Scalar applied to an anchor's `HybridScore` when attributing a score to its siblings; range `[0, 1]`. |
| `StageTimeoutNeighborMs` | `3000` | Per-stage budget. Exceeding adds `neighbor-expand` to `DegradedStages`; pipeline returns the anchors without siblings. |

## Pipeline order (4b-2)

1. **Classify** — regex fast path (Greeting / Definition / Listing / Reasoning in EN + AR) then LLM fallback. Greetings short-circuit the whole pipeline with an empty `RetrievedContext`.
2. **Rewrite** — rule-based strips question particles (`ما هي`, `what is`, etc.) and, if `EnableQueryExpansion=true`, merges 2 LLM-generated paraphrases. Output capped at `QueryRewriteMaxVariants`.
3. **Embed** — one batched call for all variants.
4. **Vector + Keyword search** — per variant, in parallel lists.
5. **RRF fusion** — reciprocal-rank-fusion merges vector and keyword hits per variant and across variants, weighted by `VectorWeight` / `KeywordWeight`.
6. **Rerank** — strategy selected by `RerankStrategySelector` from `RerankStrategy` + `QuestionType`. Listwise reorders the whole pool in one LLM call; Pointwise scores each candidate in parallel with `MinPointwiseScore` gate. On timeout / LLM failure the pipeline falls back to RRF order and records `FallbackRrf` in `RerankResult.StrategyUsed`.
7. **Trim to budget** — children are dropped first to fit `MaxContextTokens`. `TruncatedByBudget` flags truncation.
8. **Neighbor-expand** — if `NeighborWindowSize > 0`, siblings adjacent to each anchor are loaded in a single batched DB query and returned in `RetrievedContext.Siblings`. Siblings are rendered in the prompt as a separate `--- Nearby context ---` block; they are not citation targets.

## Arabic (all stages)

- `ApplyArabicNormalization` / `NormalizeTaMarbuta` / `NormalizeArabicDigits` (from 4b-1) flow through classify, rewrite, and cache keys.
- `RegexQuestionClassifier` has Arabic patterns for all four question types; most well-formed Arabic queries never reach the LLM classifier (see `QueryIntelligencePipelineTests.Arabic_pipeline_end_to_end`).
- `RuleBasedQueryRewriter` strips the common Arabic interrogatives (`ما هي`, `لماذا`, `اعرض لي`, etc.) so the downstream searches hit noun-phrase content.
- `QueryRewriter` LLM prompt instructs the model to reply in the same language as the input — do not translate.

## Observability / monitoring signals

`RagRetrievalService` emits structured logs at Information level for:

- `RAG rerank: Requested=... Used=... Latency=... TokensIn=... TokensOut=... CacheHits=... Unused=...`
- `RAG classify: QuestionType=...` (Debug)
- `RAG short-circuit: greeting` (Information)

`DegradedStages` on the returned `RetrievedContext` is the source of truth for stage-level failures — surface this via telemetry if you want Grafana alerts on e.g. rerank falling back.

The pipeline also emits OpenTelemetry spans on `ActivitySource` `Starter.Module.AI` (wired into the global OTEL exporters in `OpenTelemetryConfiguration`). The root span for retrieval is named `rag.retrieve` and tagged with:

- `rag.retrieve.top_k`, `rag.retrieve.pool_size`, `rag.retrieve.variants_used`, `rag.retrieve.truncated`, `rag.retrieve.degraded_stages`
- `rag.classify.type`, `rag.rewrite.variants_used`
- `rag.rerank.strategy_requested`, `rag.rerank.strategy_used`, `rag.rerank.fell_back`, `rag.rerank.cache_hits`, `rag.rerank.latency_ms`, `rag.rerank.unused_ratio`
- `rag.neighbor.siblings_returned`

## Done checklist (plan 4b-2)

- [x] `EnableReranking` fully removed from settings; `RerankStrategy` is the new router.
- [x] All new services registered in `AIModule.ConfigureServices` (`IQueryRewriter`, `IQuestionClassifier`, `INeighborExpander`, `IReranker`, `ListwiseReranker`, `PointwiseReranker`, `RerankStrategySelector`).
- [x] Defaults written to `appsettings.Development.json`.
- [x] Arabic fixture + end-to-end integration test (`QueryIntelligencePipelineTests`).
- [x] No EF Core migrations committed (per repo rule — apps generate their own).

## Deferred to future plans

- Cross-encoder model adapter (local `bge-reranker-base` or similar) behind a new `RerankStrategy` value.
- Adaptive strategy selector driven by live telemetry (route to Pointwise only when `UnusedRatio` is high).
- Rewriter variant diversity scoring (semantic dedup before fan-out).
- Multi-tenant rerank cache warmup.
