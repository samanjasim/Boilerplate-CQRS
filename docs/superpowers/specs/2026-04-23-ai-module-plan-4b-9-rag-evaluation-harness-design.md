# Plan 4b-9 — RAG Evaluation Harness

**Status:** Design (brainstormed) — awaiting user review before plan writing.
**Owner:** Saman
**Date:** 2026-04-23
**Preceded by:** Plan 4b-8 (Per-document ACLs + unified file/access primitive, shipped 2026-04-23 as PR #9)
**Succeeds:** Closes the Plan 4 family. Plan 5a (Agent Runtime Abstraction) comes next per the roadmap in `docs/superpowers/specs/2026-04-21-ai-module-vision-and-roadmap.md`.

---

## 1. Goal

Close Plan 4 with an **offline evaluation harness** that measures retrieval quality (`recall@k` / `precision@k` / `MRR` / `NDCG@k` / `hit_rate@k`) and per-stage latency against a version-controlled, bilingual ground-truth dataset — with **baseline-snapshot regression gating** so intentional drift is visible in diffs. Plus a superadmin-only **faithfulness endpoint** using an LLM judge to spot-check end-to-end grounding.

No production runtime cost; no DB schema changes; no UI beyond a thin admin invocation.

**Non-goals:** persisted eval campaigns in DB, historical comparison UI, per-tenant custom eval datasets, adversarial / jailbreak sets, automatic baseline updates on main merges.

## 2. Non-negotiable constraints

- **No migrations shipped** with the boilerplate — standing directive; harness touches zero schema.
- **No Co-Authored-By or Claude mentions in commits** — standing directive.
- **Bilingual from the start** — fixture carries both English and Arabic questions; metrics computed per-language and aggregate.
- **Deterministic in CI** — no live LLM calls on the xUnit path when `AI_EVAL_ENABLED` is unset. When enabled, the rerank stage is made deterministic via a pre-warmed cache blob (see §5.4).
- **Fail-closed on harness errors** — infrastructure error (Qdrant unavailable, fixture malformed), metric below baseline tolerance, or degraded-stage-count increase → test FAILs.
- **No live data leaks** — eval runs against an isolated tenant + disposable Qdrant collection; teardown on both pass and fail.

## 3. Decisions locked in brainstorm

| # | Decision | Rationale |
|---|---|---|
| D1 | Hybrid: xUnit for IR metrics + latency (CI); superadmin API endpoint for LLM-judged faithfulness (manual) | Deterministic path gates PRs; LLM judge is too expensive/flaky for CI but valuable as a spot-check |
| D2 | JSON fixture in repo is canonical; admin endpoint reads same format (disk or upload); no new DB tables | Avoids a full dataset-management CRUD surface that belongs in a later admin-UI plan |
| D3 | CI metrics: `recall@k` (k=5,10,20), `precision@k`, `MRR`, `NDCG@k`, `hit_rate@k`, plus stage-latency percentiles (p50/p95/p99) | Covers ranking quality and speed; matches standard IR literature |
| D4 | Baseline-snapshot gating via `rag-eval-baseline.json` + tolerance setting (default 5% metrics, 20% latency) | Catches drift; `UPDATE_EVAL_BASELINE=1` escape hatch for intentional regressions |
| D5 | Bilingual EN + AR fixture mandatory; metrics reported per-language and aggregate | Principle #6 in the AI vision doc; regressions are often language-specific |
| D6 | Disposable Qdrant collection per test run (`eval-{guid}`), cleaned up in `IAsyncLifetime.DisposeAsync` | Isolation from dev state; existing integration tests already use this pattern |
| D7 | Fixture includes both **documents** (with stable GUIDs) and **questions** — harness ingests docs before running questions | Self-contained; no external dependency; reproducible |
| D8 | Faithfulness judge model pinned via `AI:Rag:Eval:JudgeModel`, default = configured provider's most capable model | Judge model drift would invalidate comparisons |
| D9 | Document-granularity ground truth is default; chunk-granularity supported when author provides `relevant_chunk_ids` | Chunk IDs are unstable across chunking-strategy changes; docs are stable |
| D10 | Eval test is `[Collection("RagEval")]` and skipped unless `AI_EVAL_ENABLED=1` is set | Provider API calls are slow, cost money, and may flake — don't gate every PR on them |

## 4. Components

```
Starter.Module.AI/Application/Eval/
  ├── IRagEvalHarness.cs                        (interface; takes dataset + options, returns report)
  ├── Contracts/
  │   ├── EvalDataset.cs                        (record: Name, Language, Documents[], Questions[])
  │   ├── EvalDocument.cs                       (record: Id, FileName, Content, Language)
  │   ├── EvalQuestion.cs                       (record: Id, Query, RelevantDocumentIds[], RelevantChunkIds?, ExpectedAnswerSnippet?, Tags[])
  │   ├── EvalRunOptions.cs                     (record: KValues, IncludeFaithfulness, JudgeModelOverride, WarmupQueries)
  │   ├── EvalReport.cs                         (record: RunAt, DatasetName, Metrics, Latency, PerQuestion[], Faithfulness?)
  │   ├── EvalMetrics.cs                        (record: aggregate + per-language + per-k buckets)
  │   ├── LatencyMetrics.cs                     (record: per-stage p50/p95/p99)
  │   └── PerQuestionResult.cs                  (record: QuestionId, Retrieved[], Metrics, LatencyMs, DegradedStages[])
  └── Faithfulness/
      ├── IFaithfulnessJudge.cs
      ├── FaithfulnessReport.cs                 (record: aggregate score + per-question)
      └── FaithfulnessQuestionResult.cs         (record: QuestionId, Score, Claims[])

Starter.Module.AI/Infrastructure/Eval/
  ├── RagEvalHarness.cs                         (orchestrator: ingest → run → score → compare baseline)
  ├── Metrics/
  │   ├── RecallAtKCalculator.cs
  │   ├── PrecisionAtKCalculator.cs
  │   ├── MrrCalculator.cs
  │   ├── NdcgCalculator.cs
  │   └── HitRateCalculator.cs
  ├── Latency/
  │   └── StageLatencyAggregator.cs             (collects per-stage durations; computes percentiles)
  ├── Fixtures/
  │   ├── EvalFixtureLoader.cs                  (JSON parse + schema validation)
  │   └── EvalFixtureIngester.cs                (ingests docs into disposable tenant + Qdrant collection via UploadDocumentCommandHandler)
  ├── Baseline/
  │   ├── BaselineLoader.cs
  │   ├── BaselineWriter.cs
  │   └── BaselineComparator.cs                 (tolerance-based diff; returns pass/fail + failing-metric list)
  └── Faithfulness/
      ├── LlmJudgeFaithfulness.cs               (calls IAiService with judge prompt; parses structured output)
      └── FaithfulnessJudgePrompts.cs           (EN + AR prompt templates)

Starter.Module.AI/Infrastructure/Settings/
  └── AiRagEvalSettings.cs                      (new settings section)

Starter.Api/Controllers/Ai/
  └── AiEvalController.cs                       (POST /api/v1/ai/eval/faithfulness — superadmin only)

tools/EvalCacheWarmup/                            (one-time console app — generates eval-rerank-cache-*.bin blobs)
  ├── EvalCacheWarmup.csproj
  └── Program.cs                                  (loads fixture, calls IReranker with each (query, chunk_id) pair, writes MessagePack cache blob)

Starter.Api.Tests/Ai/Eval/
  ├── RagEvalHarnessTests.cs                    (the CI regression test — one [Fact] per fixture)
  ├── Metrics/
  │   ├── RecallAtKCalculatorTests.cs
  │   ├── PrecisionAtKCalculatorTests.cs
  │   ├── MrrCalculatorTests.cs
  │   ├── NdcgCalculatorTests.cs
  │   └── HitRateCalculatorTests.cs
  ├── BaselineComparatorTests.cs
  ├── EvalFixtureLoaderTests.cs
  ├── StageLatencyAggregatorTests.cs
  ├── fixtures/
  │   ├── rag-eval-dataset-en.json              (seed dataset; ≥15 questions, ~8 docs)
  │   ├── rag-eval-dataset-ar.json              (seed dataset; ≥15 questions, ~8 docs)
  │   ├── rag-eval-baseline.json                (checked-in baseline snapshot)
  │   ├── eval-rerank-cache-en.bin              (opaque blob; pre-warmed rerank responses)
  │   └── eval-rerank-cache-ar.bin
  └── RagEvalCollection.cs                      (xUnit collection fixture — shared Postgres + Qdrant lifecycle)
```

**One file = one responsibility.** Every metric is its own calculator file so new metrics drop in without editing existing ones. Fixture loading, ingestion, scoring, and baseline comparison are all separated.

## 5. Component details

### 5.1 `IRagEvalHarness` contract

```csharp
public interface IRagEvalHarness
{
    Task<EvalReport> RunAsync(EvalDataset dataset, EvalRunOptions options, CancellationToken ct);
}

public sealed record EvalRunOptions(
    int[] KValues,                      // default [5, 10, 20]
    bool IncludeFaithfulness = false,
    string? JudgeModelOverride = null,
    int WarmupQueries = 2);             // discarded from latency stats
```

Harness flow:

1. Create a disposable tenant + Qdrant collection (`eval-{guid}`).
2. Ingest every `EvalDocument` via the existing `UploadDocumentCommandHandler` path so chunking, embedding, and payload-stamping behave as they do in production. Build a `Dictionary<Guid, Guid>` mapping `fixture_doc_id → ingested_ai_document_id` — the upload handler generates its own `AiDocument.Id` and `FileMetadata.Id`, so fixture IDs don't survive ingestion as-is. This map is the only place fixture IDs cross the ingest boundary.
3. Run the harness as an admin-bypass user (injected `ICurrentUserService` with `IsInRole("Admin") = true`). This ensures `ResourceAccessService.ResolveAccessibleResourcesAsync` returns `IsAdminBypass = true` and no ACL filter is applied — the eval measures retrieval quality over the full corpus, not ACL behaviour (ACL is covered by Plan 4b-8's integration tests).
4. Run `WarmupQueries` queries (discarded from stats) to avoid cold-start skew.
5. For each `EvalQuestion`:
   - Call `IRagRetrievalService.RetrieveForQueryAsync`.
   - Translate retrieved `DocumentId` values back to fixture IDs via the map from step 2 before passing to metric calculators.
   - Record per-stage latency (see §5.1.1 for collection mechanism) and `DegradedStages`.
6. Compute metrics per-question, then aggregate overall and per-language.
7. If `IncludeFaithfulness`, call `IAiService.GenerateAnswerAsync` for each question using the assistant's system prompt + retrieved context, then score via `IFaithfulnessJudge`.
8. Return the `EvalReport`.
9. Teardown: drop the Qdrant collection, delete the disposable tenant's data.

The harness has no xUnit or HTTP dependency. Both the test and the admin controller call `RunAsync` on the same instance.

### 5.1.1 Stage-latency collection mechanism

`RagRetrievalService.WithTimeoutAsyncCore` records durations into the `Starter.Module.AI.Rag` OTel meter's `StageDuration` histogram, tagged with `rag.stage`. The harness captures these via an in-process `MeterListener` registered in `StageLatencyAggregator.BeginCapture()`:

```csharp
using var capture = StageLatencyAggregator.BeginCapture();   // registers MeterListener scoped to this block
var context = await retrieval.RetrieveForQueryAsync(...);
var stageDurations = capture.Stop();                          // IReadOnlyDictionary<string, double[]>
```

The listener filters to the `Starter.Module.AI.Rag` meter + `StageDuration` instrument, accumulates `(stage_name, duration_ms)` tuples per question, and disposes cleanly on scope exit. After all questions run, per-stage arrays feed the percentile calculator.

### 5.2 Fixture format — `rag-eval-dataset-en.json`

```json
{
  "name": "boilerplate-core-en-v1",
  "language": "en",
  "description": "Seed eval set covering common factual Q&A patterns on synthetic documentation.",
  "documents": [
    {
      "id": "11111111-1111-4111-8111-111111111111",
      "file_name": "invoicing-policy.md",
      "language": "en",
      "content": "# Invoicing Policy\n\nCustomers are billed monthly on the 1st...\n\n## Late Fees\nA 2% fee applies after 15 days..."
    }
  ],
  "questions": [
    {
      "id": "q001",
      "query": "When are customers billed?",
      "relevant_document_ids": ["11111111-1111-4111-8111-111111111111"],
      "relevant_chunk_ids": null,
      "expected_answer_snippet": "monthly on the 1st",
      "tags": ["factual", "single-doc"]
    }
  ]
}
```

`relevant_chunk_ids` is optional. When present, metrics are computed at chunk granularity. When absent, metrics fall back to document granularity — "any retrieved chunk from the right doc counts as a hit". Document-granularity is the default because chunk IDs are not stable across chunking-strategy changes (Plan 4b-3 proved this).

Tags (`factual`, `multi-doc`, `comparative`, `negation`, `long-context`, etc.) allow breakdowns — if recall drops overall, tag aggregates show whether the regression clusters on one question type.

### 5.3 Baseline snapshot — `rag-eval-baseline.json`

```json
{
  "generated_at": "2026-04-23T14:00:00Z",
  "git_sha": "c4f278f",
  "datasets": {
    "boilerplate-core-en-v1": {
      "aggregate": {
        "recall_at_5": 0.85, "recall_at_10": 0.92, "recall_at_20": 0.98,
        "precision_at_5": 0.41, "precision_at_10": 0.28,
        "mrr": 0.71,
        "ndcg_at_10": 0.82,
        "hit_rate_at_5": 0.90,
        "latency_ms": {
          "total":            { "p50": 120, "p95": 340, "p99": 510 },
          "acl-resolve":      { "p50":   2, "p95":   4, "p99":  12 },
          "vector-search-0":  { "p50":  40, "p95":  90, "p99": 130 },
          "rerank":           { "p50":  60, "p95": 180, "p99": 260 }
        },
        "degraded_stage_count": 0
      }
    },
    "boilerplate-core-ar-v1": { "aggregate": { "recall_at_5": 0.0, "...": "..." } }
  }
}
```

`BaselineComparator` rules:

- Any IR metric drop greater than `MetricTolerance` (default 5%): **FAIL**, with failing metric + before/after values in the assertion message.
- Any per-stage p95 latency increase greater than `LatencyTolerance` (default 20%): **FAIL**, with stage name and delta.
- Any increase in `degraded_stage_count` vs baseline: **FAIL**.
- Metric *improvements* past tolerance: pass, but print a one-line warning: "baseline is stale on these metrics — consider `UPDATE_EVAL_BASELINE=1`".

**Updating the baseline:**

```bash
AI_EVAL_ENABLED=1 UPDATE_EVAL_BASELINE=1 dotnet test --filter RagEvalHarnessTests
```

Writes a fresh baseline JSON and passes. Developer commits the updated baseline alongside the change that caused the drift, so reviewers see the regression (or improvement) explicitly in the diff.

### 5.4 Determinism strategy

Retrieval calls real services: `IAiService.GenerateEmbeddingsAsync`, `IVectorStore.SearchAsync`, `IKeywordSearchService.SearchAsync`, `IReranker.RerankAsync`. Real embedding and rerank model calls are **non-deterministic across provider versions** and **require network plus API keys**.

Approach:

- The eval test is marked `[Collection("RagEval")]` and **skipped in CI unless `AI_EVAL_ENABLED=1` is set**. PR CI on every push does not run it. A nightly Jenkins job (or local invocation) sets the flag.
- When enabled, the test needs live `OPENAI_API_KEY` or `ANTHROPIC_API_KEY` (or `OLLAMA_URL`) as the test harness configures in `appsettings.Test.json`.
- **Rerank cache is pre-warmed.** For every `(query, chunk_id)` pair in the fixture, a one-time `dotnet run --project tools/EvalCacheWarmup -- --fixture=rag-eval-dataset-en.json` pass writes a blob (`eval-rerank-cache-en.bin`). The blob is a MessagePack-serialized `Dictionary<string, decimal>` keyed by the same cache key the reranker already uses (`SHA256(query || "|" || chunk_id)` hex-encoded) — reusing the production cache key means zero divergence risk. Test setup loads that blob into `ICacheService` before running the suite. The rerank stage is deterministic from the cache; embeddings and vector-search stay live.
- Only the rerank cache is warmed. Embedding drift across provider updates shows up in metrics — that is a real regression signal, not a harness defect.
- Ollama-backed local runs use pinned local models (`nomic-embed-text` + a fixed rerank prompt) — stable across machines without needing cache blobs, but slower; the cache-blob path is the default.

### 5.5 Admin faithfulness endpoint

```
POST /api/v1/ai/eval/faithfulness
  Authorization: Bearer <superadmin token>
  Content-Type: multipart/form-data
    fixture:       <file>          (optional — if absent, load dataset_name from server disk)
    dataset_name:  string          (required if fixture absent)
    assistant_id:  Guid            (which assistant's model + settings drive answer generation)
    judge_model:   string?         (overrides AI:Rag:Eval:JudgeModel for this run)
```

Flow:

1. Controller authorizes via `[Authorize(Policy = Permissions.Ai.RunEval)]` (new permission — superadmin-only by default).
2. Calls `IRagEvalHarness.RunAsync(..., IncludeFaithfulness: true)`.
3. Report streamed back as JSON over SSE for datasets with more than 20 questions (reuses the existing SSE infrastructure from the chat endpoint). Smaller datasets return a single JSON payload.
4. Report not persisted. The caller saves results externally if they want history. Persistence defers to a later plan (§12).

Judge prompt template (RAGAS-style claim decomposition):

```
You are an impartial judge. Given a QUESTION, a CONTEXT, and an ANSWER,
extract each atomic claim in the ANSWER and classify each as:
  SUPPORTED   — directly stated or clearly inferable from CONTEXT.
  UNSUPPORTED — not stated in CONTEXT.

Output strict JSON with no prose:
  { "claims": [ { "text": "<claim>", "verdict": "SUPPORTED" | "UNSUPPORTED" } ] }

faithfulness_score = supported_count / total_claims  (1.0 if total_claims == 0)
```

Stored in `FaithfulnessJudgePrompts.cs` as two constants (`EnglishPrompt`, `ArabicPrompt`) — Arabic version phrased in Arabic so the judge stays in-language on Arabic fixtures.

## 6. Metrics math

For each question `q`, let `R_q` = set of relevant doc (or chunk) IDs, `H_q(k)` = top-k retrieved IDs in rank order.

| Metric | Formula |
|---|---|
| `recall@k` | `|R_q ∩ H_q(k)| / |R_q|`, averaged across questions |
| `precision@k` | `|R_q ∩ H_q(k)| / k`, averaged |
| `MRR` | `mean(1 / rank_of_first_relevant_in_H_q)`; contribution is 0 if no relevant ID appears in the full result list |
| `NDCG@k` | binary-gain DCG / ideal-DCG; `DCG = Σ rel_i / log₂(i + 1)` for `i ∈ [1..k]` |
| `hit_rate@k` | fraction of questions where `|R_q ∩ H_q(k)| > 0` |

All calculators take `(IReadOnlyList<Guid> retrieved, ISet<Guid> relevant, int k)` and return a `double`. Pure functions; zero external state; trivially unit-testable.

Per-stage latency: `StageLatencyAggregator` uses the `MeterListener` pattern in §5.1.1 to collect stage durations per question into per-stage arrays, then computes percentiles from a sorted-index lookup (`sorted[(int)Math.Ceiling(p * n) - 1]`). ~20 questions makes HdrHistogram overkill.

## 7. Settings

New section under `AI:Rag:Eval`:

```json
"AI": {
  "Rag": {
    "Eval": {
      "Enabled": false,
      "FixtureDirectory": "ai-eval-fixtures",
      "BaselineFile": "ai-eval-fixtures/rag-eval-baseline.json",
      "MetricTolerance": 0.05,
      "LatencyTolerance": 0.20,
      "JudgeModel": null,
      "JudgeTimeoutMs": 30000,
      "WarmupQueries": 2,
      "KValues": [5, 10, 20]
    }
  }
}
```

`AiRagEvalSettings` class in `Infrastructure/Settings/`, bound via `services.Configure<AiRagEvalSettings>(config.GetSection(...))`.

## 8. Permissions

- `Ai.RunEval` — new permission added to `Starter.Shared/Constants/Permissions.cs`. Superadmin-only by default in `Constants/Roles.cs`. Frontend mirror (`boilerplateFE/src/constants/permissions.ts`) also updated — no UI consumer yet, but the mirror stays in lockstep per CLAUDE.md.

## 9. Error codes

| Code | HTTP | When |
|---|---|---|
| `Ai.Eval.FixtureNotFound` | 404 | Fixture path or dataset_name doesn't resolve |
| `Ai.Eval.FixtureInvalid` | 400 | JSON schema validation failed (missing required fields, duplicate IDs) |
| `Ai.Eval.BaselineMissing` | 500 | Baseline file not present when comparison requested |
| `Ai.Eval.DatasetLanguageMismatch` | 400 | Fixture language ∉ {`en`, `ar`} |
| `Ai.Eval.AssistantNotFound` | 404 | Admin endpoint: `assistant_id` doesn't exist or caller can't access |
| `Ai.Eval.JudgeModelUnavailable` | 503 | Configured judge model refuses requests / no API key |

All returned via existing `Result<T>` + `HandleResult` pipeline.

## 10. Testing strategy

Meta-tests ensure the harness itself is correct:

### 10.1 Unit tests (`Starter.Api.Tests/Ai/Eval/Metrics/`)

- `RecallAtKCalculatorTests` — table-driven:
  - `(R={A,B}, H=[A,C,B,D], k=2) → 0.5`
  - `(R={A}, H=[B,C,D], k=3) → 0.0`
  - `(R=∅, H=[A], k=1) → 0.0` (degenerate: no relevant → score 0)
  - `(R={A,B,C}, H=[A,B,C,D], k=3) → 1.0`
- Equivalent table coverage for `PrecisionAtKCalculator`, `MrrCalculator`, `NdcgCalculator`, `HitRateCalculator`.
- `BaselineComparatorTests`:
  - Metric drop within tolerance → pass.
  - Metric drop exceeding tolerance → fail with metric name in message.
  - Metric improvement past tolerance → pass with warning.
  - Degraded-stage-count increase → fail.
  - Baseline missing → `Ai.Eval.BaselineMissing`.
- `EvalFixtureLoaderTests`:
  - Malformed JSON → `Ai.Eval.FixtureInvalid`.
  - Duplicate document IDs → `Ai.Eval.FixtureInvalid`.
  - Valid fixture → parses into `EvalDataset` with expected shape.
- `StageLatencyAggregatorTests`:
  - Empty input → zero percentiles, no exception.
  - Known values → correct p50/p95/p99 (sorted-index lookup).

### 10.2 Integration test — the CI regression gate

`RagEvalHarnessTests` — one `[Fact]` per fixture:

- `EvalHarness_EnglishDataset_PassesBaseline`
- `EvalHarness_ArabicDataset_PassesBaseline`

Each test:
1. `[Collection("RagEval")]` — gated on `AI_EVAL_ENABLED=1` (skipped otherwise with a clear skip reason).
2. Loads fixture + rerank cache blob.
3. Invokes `IRagEvalHarness.RunAsync`.
4. Asserts report against baseline via `BaselineComparator`.
5. On `UPDATE_EVAL_BASELINE=1`, writes new baseline and passes.

### 10.3 Admin endpoint test

- `FaithfulnessEndpoint_SuperadminCaller_ReturnsReport` — supplies a small fixture, asserts `FaithfulnessReport.PerQuestion.Length == fixture.Questions.Length` and `Score ∈ [0.0, 1.0]`. Judge model is a fake `IFaithfulnessJudge` that returns canned structured output.
- `FaithfulnessEndpoint_NonSuperadmin_403` — auth gate.
- `FaithfulnessEndpoint_MissingFixture_404` — `Ai.Eval.FixtureNotFound`.

### 10.4 Orphan-collection cleanup

- `RagEvalCollection.InitializeAsync` runs a one-time sweep of Qdrant collections whose names start with `eval-` and `created_at` older than 24h. Prevents leaks from prior crashed runs.

## 11. Risks & mitigations

| Risk | Mitigation |
|---|---|
| Provider API calls make the CI test flaky or slow | Gated by `AI_EVAL_ENABLED=1`; default-off on PR CI; nightly Jenkins job carries the flag |
| Rerank non-determinism across provider versions skews results | Rerank cache pre-warmed into an opaque blob per fixture; loaded at test setup |
| Baseline drift from unrelated model upgrades | Explicit `UPDATE_EVAL_BASELINE=1` flow; updated baseline reviewed in the diff |
| Faithfulness judge itself is a language model with biases | Pin `JudgeModel`; document that scores are comparative across runs, not absolute truth |
| Fixture documents rot as chunking strategy evolves | Default to document-granularity ground truth; chunk-level IDs only when explicitly authored |
| Arabic fixture author availability | Synthetic AR docs and queries authored during the plan; native-speaker review deferred to post-ship |
| Disposable Qdrant collection leaks on crashed test | `IAsyncLifetime.DisposeAsync` + startup sweep of orphan `eval-*` collections older than 24h |
| Faithfulness endpoint returns invalid JSON from judge | Retry once with "your last response wasn't valid JSON, try again" suffix; after second failure, mark that question `judge_parse_failed: true` and continue |
| Admin endpoint runs on large fixture and times out HTTP | SSE streaming for >20 questions; caller sees per-question progress |

## 12. Out of scope (deferred)

- Persisted eval runs in DB + historical comparison UI. Defers to a later admin-UI plan (candidate: Plan 7 or 8d).
- CSV / TSV fixture formats. JSON only.
- Answer-relevance metric. Correlated with faithfulness; YAGNI for v1.
- RAGAS `context_relevance` metric. Can be derived later from the same retrieved-chunk data.
- Adversarial / jailbreak evaluation sets. Belongs in a safety plan, not a retrieval plan.
- Automatic baseline updates on main merges. Explicit developer action preserves review quality.
- Per-tenant custom eval datasets runnable from the tenant admin UI. Belongs in Plan 8e or 11.
- Cross-run variance tracking / statistical significance tests. Single-run snapshot compare is sufficient at this scale.

## 13. Acceptance criteria

- `AI_EVAL_ENABLED=1 dotnet test --filter RagEvalHarnessTests` runs end-to-end in under 10 minutes on a developer machine against both EN and AR fixtures.
- `rag-eval-baseline.json` is checked in and reflects a clean baseline run.
- Introducing an intentional regression (e.g., `RetrievalTopK=1` in settings) causes the eval test to FAIL with a diff message naming the regressed metrics and/or stages.
- `POST /api/v1/ai/eval/faithfulness` returns a `FaithfulnessReport` with per-question supported / unsupported claim counts when called by a superadmin.
- All §10.1 unit tests pass.
- Seed EN and AR fixtures exist with ≥ 15 questions each and cover at minimum the `factual`, `multi-doc`, `negation`, and `comparative` tags.
- `Ai.RunEval` permission is registered and enforced on the faithfulness endpoint; non-superadmin returns 403.
- `CLAUDE.md` gains a short "Running the RAG eval harness" paragraph documenting `AI_EVAL_ENABLED`, `UPDATE_EVAL_BASELINE`, and the fixture location.
- An orphan `eval-*` Qdrant collection older than 24h is cleaned up by `RagEvalCollection` setup (verified by a dedicated integration test).

## 14. Post-implementation verification

Standard rename-app flow does not apply — this is a test harness, not a user-facing feature. Verification instead:

1. `AI_EVAL_ENABLED=1 dotnet test --filter RagEvalHarnessTests` on the committed branch passes with zero baseline regressions.
2. Flipping `AiRagSettings.RetrievalTopK` from 20 → 3 in `appsettings.Test.json` and re-running the test produces a `FAIL` message explicitly naming `recall_at_10` (or similar) with the drop value.
3. `UPDATE_EVAL_BASELINE=1` produces a diff in `rag-eval-baseline.json`; the regressed metrics are visibly lower.
4. `curl -H "Authorization: Bearer <sa>" -F dataset_name=boilerplate-core-en-v1 -F assistant_id=<a> http://localhost:5000/api/v1/ai/eval/faithfulness` returns a valid `FaithfulnessReport` with per-question `claims[]` arrays.
5. Non-superadmin credential on the same endpoint → HTTP 403.
