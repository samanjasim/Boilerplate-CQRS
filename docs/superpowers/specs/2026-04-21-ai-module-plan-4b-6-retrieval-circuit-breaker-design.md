# AI Module — Plan 4b-6: Retrieval Circuit Breaker (Design)

**Date:** 2026-04-21
**Status:** Design approved, pending implementation plan.
**Supersedes:** Nothing. **Builds on:** Plan 4b (retrieval baseline), Plan 4b-1 (timeouts + degraded-stage telemetry), Plan 4b-4 (stage observability).
**Next step:** `writing-plans` skill produces `docs/superpowers/plans/2026-04-21-ai-module-plan-4b-6-retrieval-circuit-breaker.md`.

## Goal

Protect RAG-turn latency and recoverability when `IVectorStore` (Qdrant) or `IKeywordSearchService` (Postgres FTS) is unhealthy. Today, each failing stage pays its full per-stage timeout before falling through to `WithTimeoutAsync`'s degrade-and-continue path; if Qdrant is down for several minutes, every RAG turn in that window waits the vector-search timeout once per query variant (up to `QueryRewriteMaxVariants * StageTimeoutVectorMs`). A circuit breaker collapses that wait to near-zero after a few observed failures, while preserving the existing graceful-degradation contract: the turn still completes, `DegradedStages` still carries the affected stage, and hybrid retrieval continues using whichever side is healthy.

## Scope decisions

| Decision | Value | Reason |
|---|---|---|
| **Breaker library** | Polly v8 `ResiliencePipeline` (`AddResilienceEnricher` not required). | De facto standard in .NET 10; first-party integration with `Microsoft.Extensions.Http`; stable API surface. |
| **Targets** | Exactly two breakers: one for `IVectorStore.SearchAsync` (Qdrant), one for `IKeywordSearchService.SearchAsync` (Postgres FTS). | User's stated scope. Other RAG stages (embed, rerank, classify) already have timeouts plus provider-side retry; adding breakers there is out of scope for 4b-6 and can be revisited if telemetry shows a need. |
| **Breaker scope** | Global per-service (not per-tenant). | Qdrant/FTS failures are infrastructure-level — a bad backend affects every tenant equally. Per-tenant breakers would need parallel instances and hide shared failures. |
| **Failure signal** | Any exception already classified as transient by `RagRetrievalService.IsTransientStageException` (`HttpRequestException`, `TimeoutException`, `DbException`, `RpcException`, `TaskCanceledException`) plus `OperationCanceledException` from the stage timeout when the caller's token is *not* cancelled. | Reuses the existing transient-exception taxonomy so the breaker counts exactly what `WithTimeoutAsync` already degrades on. Programmer bugs (ArgumentException etc.) still throw through to fail the turn loudly. |
| **Trip policy** | Polly v8 defaults: ≥10 samples in a 30-second sampling window, failure ratio ≥ 0.5. Overridable via configuration. | Conservative defaults. Tuning requires real-world failure data; 10 samples avoids trips from a single bad burst. |
| **Break duration** | 30 seconds, no jitter, no exponential backoff on re-open (v1). | Simple + observable. If telemetry shows breakers oscillating, add backoff in 4b-6.1. |
| **Half-open behaviour** | One probe per recovery attempt (Polly v8 default). | Standard; lets the next real request test the dependency rather than burning a synthetic probe. |
| **Open-circuit outcome** | `BrokenCircuitException` propagates to `WithTimeoutAsync`, which is extended to treat it as transient → stage added to `DegradedStages`, null returned, pipeline continues. | Preserves the existing degrade-and-continue contract; no new control flow through retrieval. |
| **Telemetry** | New counter `ai.rag.circuit.state_changes{service, state}` emitted on every `OnOpened` / `OnClosed` / `OnHalfOpened`. Structured log at `Warning` on open with counts + failure ratio, `Information` on close. `rag.outcome` on the affected stage becomes `"circuit_open"` (new outcome value) so dashboards can distinguish fail-fast from slow-fail. | Observability is half the value of a breaker; dashboards need to answer "is the breaker tripping right now?" and "how often does it trip?" at a glance. |
| **Configuration** | Three config keys per service under `AI:Rag:CircuitBreakers:Qdrant` / `AI:Rag:CircuitBreakers:PostgresFts`: `MinimumThroughput` (int, default 10), `FailureRatio` (double 0..1, default 0.5), `BreakDurationMs` (int, default 30000). Plus a single `Enabled` bool (default `true`) per service. | Few knobs; sensible defaults; tenants that need to disable breakers (e.g., offline dev boxes) can flip `Enabled`. |
| **Injection sites** | Wrap the `IVectorStore` and `IKeywordSearchService` calls *inside* `RagRetrievalService.WithTimeoutAsync`. Two thin wrapper classes `CircuitBreakingVectorStore` / `CircuitBreakingKeywordSearch` implement the interfaces and delegate to the real implementation through their Polly pipelines, registered in DI with `TryDecorate` or a manual registration. | Decorator pattern keeps `RagRetrievalService` unchanged; the timeout still fires first and produces `OperationCanceledException`, which the breaker observes as a failure and counts toward its trip window. Matches existing DI patterns in `Starter.Module.AI`. |

## Architecture

```
RagRetrievalService.RetrieveForQueryInternalAsync
  └─ WithTimeoutAsync (stage=VectorSearch:i)
        ├─ linked CTS with StageTimeoutVectorMs
        └─ await CircuitBreakingVectorStore.SearchAsync(...)
              └─ Polly pipeline (named "rag.qdrant")
                    ├─ CircuitBreaker (closed) → forwards to QdrantVectorStore.SearchAsync
                    ├─ CircuitBreaker (open)   → throws BrokenCircuitException immediately
                    └─ CircuitBreaker (half-open) → allows one probe
```

`WithTimeoutAsyncCore`'s `isTransient` filter is extended once to include `BrokenCircuitException` so an open circuit degrades like any other transient failure.

### New files

- `Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagCircuitBreakerOptions.cs` — strongly-typed options for one service.
- `Starter.Module.AI/Infrastructure/Retrieval/Resilience/RagCircuitBreakerRegistry.cs` — factory that builds the two named pipelines from `AiRagSettings` (or a new `AiRagResilienceSettings` sub-section; decision during writing-plans) using Polly v8.
- `Starter.Module.AI/Infrastructure/Retrieval/Resilience/CircuitBreakingVectorStore.cs` — decorator over `IVectorStore`.
- `Starter.Module.AI/Infrastructure/Retrieval/Resilience/CircuitBreakingKeywordSearch.cs` — decorator over `IKeywordSearchService`.
- `Starter.Module.AI/Infrastructure/Observability/AiRagCircuitMetrics.cs` — meter for state changes.

### Modified files

- `Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs` — add `CircuitBreakers` sub-settings block.
- `Starter.Module.AI/Infrastructure/DependencyInjection.cs` — register `RagCircuitBreakerRegistry` singleton and decorate `IVectorStore` + `IKeywordSearchService` with the circuit-breaking wrappers.
- `Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs` — extend `IsTransientStageException` to recognise `Polly.CircuitBreaker.BrokenCircuitException`.
- `Starter.Module.AI/Infrastructure/Retrieval/RagStageOutcome.cs` — add `CircuitOpen = "circuit_open"` constant.
- `Starter.Module.AI/Infrastructure/Retrieval/RagStages.cs` — no change expected; VectorSearch/KeywordSearch stage names unchanged.
- `appsettings.json` / `appsettings.Development.json` — document the new keys under `AI:Rag:CircuitBreakers`.
- `boilerplateBE/src/Starter.Api/Starter.Api.csproj` or the AI module's csproj — add `Polly` package reference (latest v8.x).

### Tests

- `Starter.Module.AI.Tests/Infrastructure/Retrieval/Resilience/CircuitBreakingVectorStoreTests.cs`
  - Trips after configured failure ratio across minimum throughput.
  - Fails fast with `BrokenCircuitException` while open.
  - Recovers through half-open probe after break duration.
  - Emits `StateChanges` metric on each transition.
- `Starter.Module.AI.Tests/Infrastructure/Retrieval/Resilience/CircuitBreakingKeywordSearchTests.cs` — symmetrical.
- `Starter.Module.AI.Tests/Infrastructure/Retrieval/RagRetrievalServiceTests.cs` — extend existing degradation test: when the circuit is open, `VectorSearch:0` appears in `DegradedStages` and keyword hits are still merged.

## Deferred (out of scope for 4b-6)

- Breakers around embed/classify/rerank/contextualize. These stages already have timeouts and provider-side retries; add breakers only if observed failure modes justify it.
- Exponential backoff on repeated opens. Add in a follow-up if telemetry shows breaker oscillation.
- Per-tenant isolation. Irrelevant until a tenant-specific upstream failure mode emerges.
- An admin-facing UI for breaker state. Metrics to Grafana are enough for v1; an admin UI belongs with Plan 7 / Plan 8d.

## Success criteria

1. With Qdrant stopped, the first RAG turns take the stage timeout; after ~10 failures within the sampling window, subsequent turns return in < 50 ms with `VectorSearch:*` in `DegradedStages`, and keyword-only retrieval proceeds normally.
2. Starting Qdrant after the break duration causes the next RAG turn to succeed on the first try (half-open probe passes → breaker closes). Metric emits `Closed` transition.
3. Symmetrical behaviour for Postgres FTS outage.
4. With both services healthy, breaker overhead is negligible (< 1 ms p99) and no state-change metrics emit.
5. Existing 328-test suite plus new resilience tests all pass.

## Change control

Contradictions with the roadmap in `docs/superpowers/specs/2026-04-21-ai-module-vision-and-roadmap.md` must be flagged explicitly in the implementation plan rather than silently applied.
