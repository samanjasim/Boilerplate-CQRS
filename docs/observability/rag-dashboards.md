# RAG Observability — Dashboards & Alerts

This page describes the metrics emitted by the AI RAG pipeline (meter
`Starter.Module.AI.Rag`), a Grafana import skeleton, and Prometheus alert
starters. Import and adapt to your environment — the skeleton is not a hard
dependency.

## Metric reference

| Name | Kind | Unit | Tags | What to watch |
|---|---|---|---|---|
| rag.retrieval.requests | Counter | count | rag.scope | Traffic per scope |
| rag.stage.duration | Histogram | ms | rag.stage | P50/P95/P99 per stage |
| rag.stage.outcome | Counter | count | rag.stage, rag.outcome | Success vs timeout vs error ratio per stage |
| rag.cache.requests | Counter | count | rag.cache, rag.hit | Hit rate per cache |
| rag.fusion.candidates | Histogram | count | — | Fused list size distribution before top-K |
| rag.context.tokens | Histogram | tokens | — | Final context size |
| rag.context.truncated | Counter | count | rag.reason | How often context was trimmed |
| rag.degraded.stages | Counter | count | rag.stage | Degrade frequency per stage |
| rag.rerank.reordered | Counter | count | rag.changed | How often rerank changed top-K |
| rag.keyword.hits | Histogram | count | rag.lang | Keyword recall distribution per language |

**Important:** no per-tenant tags — avoids Prometheus cardinality explosions.
Per-tenant drill-down comes from logs (`req=` correlates to webhook payload
`RequestId`) and webhooks (`ai.retrieval.completed|degraded|failed`).

## Tag value domains

Keep tag cardinality finite. Current domains:

- `rag.scope` — `AllTenantDocuments`, `SelectedDocuments`.
- `rag.stage` — `classify`, `query-rewrite`, `embed-query`, `vector-search-<i>`,
  `keyword-search-<i>`, `rerank`, `neighbor-expand`, `webhook-publish`. The
  `<i>` suffix is the per-variant index and is bounded by
  `AiRagSettings.MaxQueryVariants` (typically ≤ 4).
- `rag.outcome` — `success`, `timeout`, `error`.
- `rag.cache` — `embed`, `rewrite`, `classify`, `rerank`.
- `rag.hit` — `true`, `false`.
- `rag.reason` — `budget` (future: `provider-limit`).
- `rag.changed` — `true`, `false`.
- `rag.lang` — `ar`, `en`, `mixed`, `unknown`.

## Grafana panel skeleton

Six panels cover the important signals. Replace `${DS_PROMETHEUS}` with your
datasource UID when importing.

| # | Panel | PromQL |
|---|---|---|
| 1 | Stage P95 latency | `histogram_quantile(0.95, sum by (le, rag_stage) (rate(rag_stage_duration_bucket[5m])))` |
| 2 | Cache hit rate (all caches) | `sum by (rag_cache) (rate(rag_cache_requests_total{rag_hit="true"}[5m])) / sum by (rag_cache) (rate(rag_cache_requests_total[5m]))` |
| 3 | Degraded-stage rate | `sum by (rag_stage) (rate(rag_degraded_stages_total[5m]))` |
| 4 | Arabic vs English keyword recall | `histogram_quantile(0.5, sum by (le, rag_lang) (rate(rag_keyword_hits_bucket[5m])))` |
| 5 | Fusion size distribution | `histogram_quantile(0.9, sum by (le) (rate(rag_fusion_candidates_bucket[15m])))` |
| 6 | Rerank change rate | `sum(rate(rag_rerank_reordered_total{rag_changed="true"}[5m])) / sum(rate(rag_rerank_reordered_total[5m]))` |

A ready-to-import Grafana JSON can live alongside this file as
`rag-dashboards.grafana.json`. It is not committed yet — build from the table
above when your environment needs it.

## Prometheus alert starters

```yaml
# Alert when more than 10% of retrievals degrade over 5 minutes.
- alert: RagDegradedStageRateHigh
  expr: |
    sum(rate(rag_degraded_stages_total[5m])) /
    sum(rate(rag_retrieval_requests_total[5m])) > 0.1
  for: 5m
  labels: { severity: warning }
  annotations:
    summary: "RAG pipeline degraded-stage rate over 10%"

# Alert when any vector-search shard's P95 exceeds 2s.
- alert: RagVectorSearchSlow
  expr: |
    histogram_quantile(0.95,
      sum by (le, rag_stage) (rate(rag_stage_duration_bucket{rag_stage=~"vector-search.*"}[5m]))) > 2000
  for: 5m
  labels: { severity: warning }

# Alert when cache hit rate drops below 50% for any cache.
- alert: RagCacheHitRateLow
  expr: |
    sum by (rag_cache) (rate(rag_cache_requests_total{rag_hit="true"}[10m])) /
    sum by (rag_cache) (rate(rag_cache_requests_total[10m])) < 0.5
  for: 10m
  labels: { severity: info }

# Alert when webhook-publish timeouts spike.
- alert: RagWebhookPublishTimeouts
  expr: |
    sum(rate(rag_stage_outcome_total{rag_stage="webhook-publish",rag_outcome="timeout"}[5m])) > 0.05
  for: 5m
  labels: { severity: warning }
```

## Webhook events

Subscribers to the `ai.retrieval.*` namespace receive per-turn lifecycle
events. Payload schema:

```json
{
  "requestId": "uuid",
  "assistantId": "uuid",
  "tenantId": "uuid|null",
  "keptChildren": 5,
  "keptParents": 3,
  "siblingsCount": 2,
  "fusedCandidates": 20,
  "totalTokens": 3200,
  "truncated": false,
  "degradedStages": ["rerank"],
  "detectedLanguage": "ar",
  "stages": []
}
```

Three event types:

- `ai.retrieval.completed` — retrieval ran with zero degraded stages.
- `ai.retrieval.degraded` — retrieval ran, but ≥ 1 stage degraded.
- `ai.retrieval.failed` — retrieval threw; payload includes `error` field.

The `requestId` field in the payload equals the `req=` value in the structured
log line emitted by `ChatExecutionService`, so subscribers can cross-reference
logs and webhooks without additional correlation work.

Use the existing webhook-subscription admin UI to filter by event name.

## Operational notes

- **Cardinality** — `rag.lang` is capped to 4 values (`ar|en|mixed|unknown`).
  Do not add per-tenant tags to any instrument; investigation flows through
  logs and webhooks instead.
- **Clock drift** — `rag.stage.duration` is a local wall-clock histogram.
  Cross-machine comparisons should bucket by host.
- **Weak signal: `rag.rerank.reordered`** — this is a proxy, not ground truth.
  Use as a health indicator of whether reranking is mattering, not as a
  quality score.
- **Webhook publish failures never block the turn** — a timeout or exception
  from the webhook subscriber is swallowed inside the RAG pipeline and
  recorded as `rag.stage.outcome{rag.stage="webhook-publish",rag.outcome="timeout|error"}`.
