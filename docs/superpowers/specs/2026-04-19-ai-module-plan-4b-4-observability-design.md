# Plan 4b-4 — RAG Observability Design

**Status:** Draft — follow-on to Plan 4b-1/2/3. Ships last because it piggybacks on hooks the prior plans add.

**Depends on:** Plan 4b-1 (per-stage wrappers, `DegradedStages`). 4b-2 / 4b-3 add stages this plan will instrument if they're merged first, but each stage's telemetry is independent — 4b-4 doesn't block on them.

## Goal

Make the RAG pipeline **measurably** good or bad in production: per-stage latencies, cache hit rates, degraded-mode counts, fusion/rerank quality proxies, and lifecycle events emitted to the same webhook / event bus we already use for tenants, users, billing, etc. Everything flows through the existing OpenTelemetry + Serilog + Webhooks infrastructure — no new services.

## Why now

We currently log one aggregate line per chat turn:

```
RAG retrieval for assistant {AssistantId}: children={Children} parents={Parents} tokens={Tokens} truncated={Truncated}
```

That's enough to know retrieval ran, nothing more. We can't answer:

- What's the P50/P95 latency of vector search vs keyword search?
- How often is the embedding cache hitting?
- How often does the keyword search time out and we fall back to vector-only?
- Are reranks measurably improving the ordering (cheap proxy: how often does rerank's top-1 differ from RRF's top-1)?
- How many chat turns hit the token budget (truncated=true) vs fit cleanly?
- Is our Arabic corpus getting keyword hits at the expected rate compared to English?

All of those are cheap to instrument via `System.Diagnostics.Metrics` counters/histograms we already export to Prometheus via OpenTelemetry. Webhook events add a second layer for operational alerting.

## Non-goals

- Retrieval quality evaluation harness (recall@k against a labeled test set). Worth building, but separate — needs a curated test corpus.
- User-facing "why did I get this answer?" UI. The data to build it will be present; the UI is not in this plan.
- Replacement of OpenTelemetry with Prometheus-native. OTel is already wired.
- Per-tenant metric labels (cardinality explosion). Metrics stay tenant-aggregate. Tenant-specific dashboards come from logs/webhooks.

## Components

### 1. `AiRagMetrics` — OTel instruments

New class in `Infrastructure/Observability/AiRagMetrics.cs`. Holds a single static `Meter` named `Starter.Module.AI.Rag`, registered in the existing OTel pipeline (add meter name to `OpenTelemetryConfiguration.cs`).

**Instruments:**

| Name | Kind | Unit | Tags |
|---|---|---|---|
| `rag.retrieval.requests` | Counter | count | `rag.scope` (SelectedDocuments\|AllTenantDocuments) |
| `rag.stage.duration` | Histogram | ms | `rag.stage` (embed-query, vector-search, keyword-search, rewrite, rerank, classify) |
| `rag.stage.outcome` | Counter | count | `rag.stage`, `rag.outcome` (success, timeout, error) |
| `rag.cache.requests` | Counter | count | `rag.cache` (embed, rewrite, rerank, classify), `rag.hit` (true\|false) |
| `rag.fusion.candidates` | Histogram | count | (no tag) — size of fused list before top-K |
| `rag.context.tokens` | Histogram | tokens | (no tag) — final context size |
| `rag.context.truncated` | Counter | count | `rag.reason` (budget, too-many-chunks) |
| `rag.degraded.stages` | Counter | count | `rag.stage` — one increment per degraded stage per request |
| `rag.rerank.reordered` | Counter | count | `rag.changed` (true\|false) — did rerank's top-K differ from RRF's? |
| `rag.keyword.hits` | Histogram | count | `rag.lang` (ar, en, mixed, unknown) — detect Arabic recall regressions |

**No per-tenant / per-user labels.** The Prometheus cardinality explosion caveat. Per-tenant investigation uses logs + webhooks.

**`rag.lang` detection:** lightweight heuristic — count ratio of code points in Arabic block `\u0600-\u06FF` vs ASCII letters in the query. `>0.5` = ar, `<0.1` = en, else mixed. Arabic-aware but cheap.

### 2. Instrumentation points

Wrapping already exists from 4b-1 (`WithTimeoutAsync`). Augment each wrapper to emit:

- `rag.stage.duration` observed at completion (success, timeout, or error).
- `rag.stage.outcome` incremented with the corresponding tag.

`CachingEmbeddingService` (from 4b-1), `LlmQueryRewriter`, `LlmReranker`, `QuestionClassifier` each increment `rag.cache.requests`.

`HybridScoreCalculator.Combine` is pure — not instrumented directly. Caller records `rag.fusion.candidates`.

`RagRetrievalService` records `rag.context.tokens`, `rag.context.truncated`, `rag.degraded.stages`, `rag.retrieval.requests`, `rag.keyword.hits` (before RRF) at the appropriate stages.

`LlmReranker` records `rag.rerank.reordered` (compare pre- and post-rerank top-K chunk-id lists — set bool).

### 3. Lifecycle webhook events

Use the existing `IWebhookPublisher` (used by `ChatExecutionService.cs:522` already for chat events). Three new event names under the `ai.retrieval.*` namespace:

| Event | Trigger | Payload |
|---|---|---|
| `ai.retrieval.completed` | Every successful retrieval | `requestId, assistantId, tenantId, stages[{name,durationMs,outcome}], fusedCandidates, keptChildren, keptParents, siblingsCount, truncated, degradedStages[], totalTokens, detectedLang` |
| `ai.retrieval.degraded` | Any stage degraded | Same as above, emitted instead of `completed` when `degradedStages.Count > 0`. Enables webhook subscribers to alert on degradation specifically. |
| `ai.retrieval.failed` | Pipeline caught an unhandled exception (rare since 4b-1 covers most) | `requestId, assistantId, tenantId, stage, errorMessage` |

Subscribers can filter by event name via the existing webhook-subscription API.

Emission is **fire-and-forget** (await the publisher but don't block the chat turn on a publish failure — `try/catch` + log).

### 4. Serilog enrichment

Add structured properties to the aggregate log line (keeping it for human-readable tailing):

```
RAG retrieval done assistant={AssistantId} req={RequestId} children={Children} parents={Parents} siblings={Siblings} tokens={Tokens} truncated={Truncated} stages={StagesSummary} degraded={DegradedStages} lang={DetectedLang}
```

Adopts the conventions used elsewhere in the codebase (`assistant=`, not `AssistantId=`).

### 5. Dashboards (documentation only)

Ship a markdown doc under `docs/observability/rag-dashboards.md` containing:

- A list of the metric names above
- A Grafana JSON skeleton with the 6 most useful panels pre-wired (stage P95, cache hit %, degraded rate, Arabic recall, fusion size distribution, reranker change rate)
- Prometheus alert rule starters (e.g. `rag.degraded.stages increase > 10% over 5m`)

The skeleton is an import target, not a hard dependency. Consuming teams import and adjust.

## Data flow additions

Same pipeline as before; every arrow now also emits a `rag.stage.duration` + `rag.stage.outcome` pair. At pipeline end, one webhook publish + one structured log. Nothing in the retrieval path blocks on observability.

## Risks & mitigations

| Risk | Mitigation |
|---|---|
| Metric cardinality explosion from a leaky label | Tag enums defined up-front; no dynamic strings. Language tag capped to 4 values. |
| Webhook publishing adds latency to the chat turn | Await with a tight timeout (500ms) and continue on failure; the timeout is itself recorded on `rag.stage.outcome` with stage=`webhook-publish`. |
| `rag.keyword.hits` histogram by `rag.lang` becomes noisy for mixed corpora | Acceptable; we're looking for trend drift, not per-query detail. |
| Reranker `reordered` proxy is a weak quality signal | Acknowledged — it's a proxy, not ground truth. Real quality measurement needs the eval harness (out of scope). |

## Estimated effort

| Item | Effort |
|---|---|
| 1. `AiRagMetrics` + meter registration | ~0.5d |
| 2. Instrumentation points across stages | ~0.5d |
| 3. Webhook events + publisher integration | ~0.5d |
| 4. Enriched log line + language detector | ~0.5d |
| 5. Dashboard + alerts doc | ~0.5d |
| Tests (unit for metrics emission, fixture for webhook payloads) | ~0.5d |
| **Total** | **~3 days** |

## Open questions

- **Should `rag.stage.duration` be a real histogram or a UpDownCounter of sum/count?** Prometheus OTel export handles histograms cleanly; go with Histogram.
- **Webhook payload size for large contexts** — chunks themselves are NOT in the payload (too large). Only counts + ids. A separate `ai.retrieval.debug.snapshot` event with the full payload could come later behind a feature flag.
- **Opt-in per tenant?** Events emit unconditionally; webhook subscribers already filter server-side. A per-tenant metrics opt-out is not planned.
