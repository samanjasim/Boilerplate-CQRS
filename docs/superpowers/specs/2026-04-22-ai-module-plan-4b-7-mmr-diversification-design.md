# AI Module — Plan 4b-7: MMR Diversification (Design)

**Date:** 2026-04-22
**Status:** Design approved, pending implementation plan.
**Supersedes:** Nothing. **Builds on:** Plan 4b (retrieval baseline), Plan 4b-1 (timeouts), Plan 4b-4 (stage observability), Plan 4b-6 (circuit breaker).
**Next step:** `writing-plans` skill produces `docs/superpowers/plans/2026-04-22-ai-module-plan-4b-7-mmr-diversification.md`.

## Goal

Reduce near-duplicate chunks in the retrieved top-K by inserting a Maximal Marginal Relevance (MMR) diversification stage between rerank and the final `.Take(topK)`. Current behavior: when a document contains multiple paraphrased passages on the same topic, hybrid retrieval + rerank can return 5 near-identical chunks, crowding out complementary context from other passages/documents. MMR trades a configurable amount of relevance for diversity by penalising each candidate against the already-selected set.

The stage is **opt-in** (off by default) because the symptom is deferred — we want the mechanism in place and observable so we can flip it on when QA surfaces the problem, without another design cycle.

## Scope decisions

| Decision | Value | Reason |
|---|---|---|
| **Placement** | Post-rerank, pre-TopK. MMR runs on `rerankedHits` and outputs the final ordered top-K. | Cheapest: chunk-chunk similarities computed only on the reranked pool (≤ `PointwisePoolMultiplier * topK`, typically ≤ 20). Keeps rerank as the relevance authority; MMR is a pure diversity filter. Alternative placements (pre-rerank on RRF'd pool, or replacing rerank) wasted rerank work or required invasive strategy-selector changes. |
| **Algorithm** | Standard MMR: iteratively pick the argmax of `λ · rel(d) − (1−λ) · max_{s ∈ selected} sim(d, s)`. | De facto reference implementation (Carbonell & Goldstein, 1998). Linear in pool size × top-K. |
| **`rel(d)`** | Rerank score from `HybridHit.Score`, normalised to `[0,1]` via min-max over the pool. | Rerank score is already the best per-candidate relevance signal; normalising prevents λ from being dominated by absolute-magnitude differences between rerank strategies (listwise logits vs pointwise probabilities). |
| **`sim(d, s)`** | Cosine similarity between chunk embeddings fetched from Qdrant by point-id. | Embeddings already exist in Qdrant; no second-vector-store. Bulk `Retrieve(WithVectors=true)` for the reranked pool is one round-trip. |
| **Similarity fetch** | New `IVectorStore.GetVectorsByIdsAsync(tenantId, pointIds, ct)` → `IReadOnlyDictionary<Guid, float[]>`. | Matches existing interface conventions (tenant-scoped, batch API). Only one call site; tight contract. |
| **Trip knob (λ)** | `MmrLambda` (double, default `0.7`, clamped `[0,1]`). λ=1 → pure relevance (no-op), λ=0 → pure diversity. | Literature consensus 0.5–0.8 for QA-style RAG; 0.7 mildly favours relevance, safe default if ops flip `EnableMmr=true` without further tuning. |
| **Enable flag** | `EnableMmr` (bool, default `false`). | Opt-in matches "fix if/when observed" deferral from the 4b-roadmap. Also lets us ship the observability + algorithm and turn on per-environment only where QA has confirmed near-dup problems. |
| **Pool size** | Reuse existing rerank pool (`poolMultiplier * topK`). No new setting. | The reranked pool IS the MMR input. Adding another knob adds config surface without clear value. |
| **Timeout** | `StageTimeoutMmrMs` (int, default `2000`). | MMR itself is microseconds; the timeout guards the Qdrant vector-fetch roundtrip. |
| **Degradation** | On any transient exception, timeout, or empty embedding fetch → degrade to `rerankedHits.Take(topK)` and add `"mmr-diversify"` to `DegradedStages`. | Same contract as every other RAG stage since Plan 4b-1; inherits circuit-breaker-aware `WithTimeoutAsync` from 4b-6. |
| **Metrics** | Reuse existing `rag.stage.latency` + `rag.stage.outcome` (from Plan 4b-4) with `rag.stage = "mmr-diversify"`. No new meter. | One stage, standard observability. A dedicated counter for "chunks dropped by MMR" is deferred (we can compute `pool_size - topK_unique_documents` from traces if needed). |
| **Empty-vector handling** | If `GetVectorsByIdsAsync` returns fewer vectors than requested (eventual consistency between Qdrant and the DB), drop the missing ids from the MMR pool and continue. Log at `Warning`. | Same defensive pattern as existing `chunkByPointId` alignment loop in `RetrieveForQueryInternalAsync`. |
| **Circuit breaker** | The existing `CircuitBreakingVectorStore` decorator wraps `GetVectorsByIdsAsync` too (added to the interface → decorator must forward it; same Qdrant breaker). | Single breaker per Qdrant instance — a search outage and a retrieve outage share the same infrastructure. Reuses the registry + metrics from 4b-6. |

## Architecture

```
RagRetrievalService.RetrieveForQueryInternalAsync
  ├─ ...existing pipeline...
  ├─ Rerank → rerankedHits: IReadOnlyList<HybridHit>
  ├─ NEW: MMR Diversify stage (only if EnableMmr=true)
  │    └─ WithTimeoutAsync (stage="mmr-diversify")
  │          ├─ CircuitBreakingVectorStore.GetVectorsByIdsAsync(tenantId, pointIds)
  │          │    └─ Polly pipeline (named "rag.qdrant") → QdrantVectorStore.GetVectorsByIdsAsync
  │          └─ MmrDiversifier.Diversify(rerankedHits, embeddings, λ, topK)
  │               returns IReadOnlyList<HybridHit>
  ├─ topKHits = (mmrHits ?? rerankedHits).Take(topK)
  └─ ...existing pipeline (child/parent expansion, neighbor, trim)...
```

### New files

- `Starter.Module.AI/Infrastructure/Retrieval/Diversification/MmrDiversifier.cs` — pure algorithm (static helper or injectable service). Takes rerankedHits, embedding map, λ, topK; returns diversified ordered list.
- `Starter.Module.AI/Infrastructure/Retrieval/Diversification/MmrStage.cs` *(optional — decision during writing-plans)* — thin orchestration around vector-fetch + diversifier, so `RagRetrievalService` stays slim.

### Modified files

- `Starter.Module.AI/Application/Services/Ingestion/IVectorStore.cs` — add `GetVectorsByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> pointIds, CancellationToken ct)`.
- `Starter.Module.AI/Infrastructure/Ingestion/QdrantVectorStore.cs` — implement via `QdrantClient.RetrieveAsync(name, ids, withVectors: true)`.
- `Starter.Module.AI/Infrastructure/Retrieval/Resilience/CircuitBreakingVectorStore.cs` — forward `GetVectorsByIdsAsync` through the Qdrant pipeline.
- `Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs` — insert MMR stage between rerank and `.Take(topK)`. Only invoke when `_settings.EnableMmr` is true.
- `Starter.Module.AI/Infrastructure/Retrieval/RagStages.cs` — add `MmrDiversify = "mmr-diversify"` constant.
- `Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs` — add `EnableMmr`, `MmrLambda`, `StageTimeoutMmrMs`.
- `Starter.Module.AI/AIModule.cs` — register `MmrDiversifier` (scoped or singleton; pure algorithm → singleton).
- `appsettings.json` / `appsettings.Development.json` — document the new keys under `AI:Rag`.

### Tests

- `Starter.Api.Tests/Ai/Retrieval/Diversification/MmrDiversifierTests.cs`
  - Near-duplicate suppression: 3 identical embeddings + 2 distinct → top-3 contains 1 of each cluster (λ=0.5).
  - λ=1 produces pure relevance order (MMR output == input order truncated).
  - λ=0 maximises diversity (successive picks are the farthest from selected set).
  - Top-K > pool size → returns whole pool in some order, no exception.
  - Missing embedding for a hit → that hit is dropped, remaining hits still diversified.
  - Empty rerankedHits → empty output.
- `Starter.Api.Tests/Ai/Retrieval/Diversification/QdrantGetVectorsByIdsTests.cs` (integration, skippable when Qdrant absent)
  - Round-trip: upsert 3 points, fetch 2 by id, verify vectors match.
  - Partial-hit: request 3 ids, 1 is missing → returns dict with 2 entries (no throw).
- `Starter.Api.Tests/Ai/Retrieval/RagRetrievalServiceMmrIntegrationTests.cs`
  - With `EnableMmr=false`: pipeline runs unchanged (no Qdrant fetch call).
  - With `EnableMmr=true`: near-duplicate rerankedHits → final top-K has reduced duplicates.
  - MMR stage timeout / Qdrant error → `"mmr-diversify"` in `DegradedStages`, top-K falls back to rerank order.
- `Starter.Api.Tests/Ai/Retrieval/Resilience/CircuitBreakingVectorStoreTests.cs` — extend to cover `GetVectorsByIdsAsync` passing through the breaker.

## Deferred (out of scope for 4b-7)

- Tenant- or assistant-level MMR tuning. A single global λ until we have evidence diversity needs vary across workloads.
- Cross-document clustering that forces document-level diversity (e.g., "at most 2 chunks from any single document"). MMR handles this implicitly via cosine; a hard document cap belongs in 4b-8 (per-document ACLs) or later if MMR alone is insufficient.
- Learned diversity weights or contextual λ selection. Out of scope; λ stays static.
- Sub-linear MMR variants. N² over ≤20 chunks is microseconds; no need.
- Exposing MMR pool size as a separate knob. Pool is the reranked pool.

## Success criteria

1. With `EnableMmr=false` (default): zero behavior change. All 4b-6 tests still pass. No Qdrant `Retrieve` calls added to the pipeline.
2. With `EnableMmr=true` on a synthetic corpus of 3 near-duplicate chunks + 2 distinct chunks at rerank-output, final top-3 contains at least 2 distinct clusters (vs 1 cluster without MMR).
3. Qdrant outage during MMR: `"mmr-diversify"` appears in `DegradedStages`, top-K is returned from rerank order, RAG turn completes with keyword-side hits still intact.
4. `rag.stage.latency` and `rag.stage.outcome` metrics carry `rag.stage = "mmr-diversify"` tag when MMR runs.
5. Polly circuit breaker for Qdrant (from 4b-6) also protects `GetVectorsByIdsAsync`; when open, MMR degrades without additional latency.

## Notes

- Minmax normalisation of rerank scores happens inside `MmrDiversifier` so the caller doesn't need to know rerank strategy specifics. If min == max in the pool (all scores equal), normalise to `0.5` for every candidate to keep the λ tradeoff well-defined.
- For λ=1 the algorithm is provably equivalent to `rerankedHits.Take(topK)`, so the fast path short-circuits without computing any cosines.
- For `rerankedHits.Count ≤ topK` the algorithm is also a no-op (everything is selected regardless of λ); short-circuit before fetching vectors.
