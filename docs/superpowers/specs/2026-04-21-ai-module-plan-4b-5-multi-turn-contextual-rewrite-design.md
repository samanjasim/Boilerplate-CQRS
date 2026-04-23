# Plan 4b-5 — Multi-turn contextual query rewrite

**Parent plan:** AI module RAG (Plan 4 family). Depends on 4b-2 (existing `IQueryRewriter` seam, `WithTimeoutAsync` wrapper) and 4b-4 (metric + stage-tagging conventions).

**Position in the Plan 4 family:** picks up item #3 from the 4b spec's Deferred list ("Contextual query rewrite for multi-turn"). Closes the last user-visible gap in RAG retrieval: follow-up questions that reference earlier turns ("how do we configure *it*?") currently hit retrieval verbatim and miss.

---

## 1. Goal

When a user asks a follow-up in an existing conversation, rewrite their latest message into a self-contained query *before* the existing RAG pipeline runs. The user sees no change in the UI — the improvement shows up as better retrieval on turn 2+.

## 2. Non-goals

See §10 (Deferred work) for the full catalogue. In short: we do **not** touch the citation path, the provider request, the message that gets stored, or any frontend surface.

## 3. Motivation

Today's flow (as of 4b-4):

```
ChatExecutionService.ExecuteAsync(conversationId, assistantId, userMessage)
  → PrepareTurnAsync (loads priorMessages, builds providerMessages)
  → RetrieveContextSafelyAsync(assistant, userMessage, ct)  ← only userMessage; no history
      → retrievalService.RetrieveForTurnAsync(assistant, userMessage, ct)
        → classify → query-rewrite → embed → vector+keyword → fuse → rerank → neighbor → budget
```

The retrieval seam never sees conversation history. "How do we configure it?" goes to the embedder verbatim — the embedder has no idea what "it" refers to.

Query expansion (4b-2) paraphrases within a single turn and cannot recover inter-turn references. Reranking (4b-2) only re-orders what retrieval surfaced. The fix has to land before embedding.

## 4. Architecture

One new pre-rewrite stage in `RagRetrievalService`:

```
classify → [NEW] contextualize → query-rewrite → embed → vector + keyword → fuse → rerank → neighbor-expand → budget
```

New pieces:

- `IContextualQueryResolver` (interface) — one method: `Task<string> ResolveAsync(string latestUserMessage, IReadOnlyList<AiChatMessage> history, string? language, CancellationToken ct)`. Never throws; falls back to `latestUserMessage` on any failure.
- `ContextualQueryResolver` (implementation) — rule-based heuristic gate + LLM fallback + short-TTL cache.
- `RagStages.Contextualize` constant for metric tagging and degradation.
- `RagCacheKeys.Contextualize(provider, model, language, historyHash)` factory.
- Four new `AiRagSettings` keys (all with defaults; no appsettings change required for existing deployments).

Modified seams:

- `RagRetrievalService.RetrieveForTurnAsync` gains `IReadOnlyList<AiChatMessage> history` parameter (not nullable — empty list is the "first turn / no history" signal).
- `ChatExecutionService.RetrieveContextSafelyAsync` maps the last `ContextualRewriteHistoryTurns` (default 3) turns from `state.ProviderMessages` and passes them through.

### Activation (heuristic-first)

Before paying for an LLM call, a rule-based check decides whether the latest message *looks like* a follow-up. Mirrors the `RuleBasedQueryRewriter` → `QueryRewriter` pattern from 4b-2.

Rules (any ONE triggers the LLM step):
- Message length ≤ 25 characters.
- Message contains a pronoun/reference token from a small bilingual list: English — `it, this, that, they, them, these, those, one, ones, which`; Arabic — `هو, هي, هذا, هذه, ذلك, تلك, هؤلاء, الذي, التي`.
- Message starts with a question-continuation token: English — `and, or, but, also, what about, how about, why, when`; Arabic — `و, أو, لكن, ماذا عن, كيف, لماذا, متى`.

If none of the rules fire AND history is non-empty, the stage records `outcome=success, rag.skipped=true` and returns the raw message. No LLM call.

If history is empty (first turn), the stage is skipped entirely with `outcome=success, rag.skipped=true`.

If `EnableContextualRewrite=false`, the stage is skipped entirely with no metric emission (consistent with how `EnableQueryExpansion=false` collapses inside the existing rewriter).

### LLM call

- System prompt: `"Given the recent conversation and the user's latest message, rewrite the latest message into a single self-contained question that preserves the user's intent. Reply in the same language as the user. Do NOT translate. If the message is already self-contained, return it unchanged."`
- User prompt: `"Language hint: {en|ar|auto}\nConversation (oldest first):\n{history}\nLatest message: {latestMessage}\nSelf-contained rewrite:"`
- `Temperature: 0.2`, `MaxTokens: 200`.
- Response parsed as plain string; surrounding quotes/whitespace stripped; empty/whitespace treated as failure.
- Model: `AiRagSettings.ContextualRewriterModel ?? IAiProviderFactory.GetDefaultChatModelId()` — matches the rewriter's model-selection pattern.
- Timeout: wrapped in the existing `WithTimeoutAsync` helper with `StageTimeoutContextualizeMs` (default `3000`).

### Cache

- Key: `RagCacheKeys.Contextualize(provider, model, lang, sha256(history_normalized || '\n' || latestMessage_normalized))`.
- `history_normalized` = `history.Select(m => m.Role + ":" + ArabicTextNormalizer.Normalize(m.Content.Trim())).Join("\n")`.
- TTL: `ContextualRewriteCacheTtlSeconds` (default `600` = 10 min).
- Redis unavailable → proceed without cache, run LLM, skip `cache.SetAsync`. Cache counter not emitted. Stage still records `outcome=success`.

### Degradation matrix

| Failure mode | Behavior | Metrics |
|---|---|---|
| LLM timeout (>StageTimeoutContextualizeMs) | Return raw user message | `rag.stage.outcome stage=contextualize outcome=timeout` + `rag.degraded.stages stage=contextualize` |
| LLM HTTP/transport error | Return raw user message | `outcome=error` + `degraded` |
| LLM returns empty / whitespace | Return raw user message | `outcome=error` + `degraded` (treat as bad output) |
| LLM returns unquoted string | Use as-is | `outcome=success, rag.skipped=false` |
| LLM returns quoted string | Strip surrounding quotes | `outcome=success, rag.skipped=false` |
| Request cancellation (caller ct) | Propagate | none (matches existing `WithTimeoutAsync` behavior) |

### Concurrency

`RetrieveForTurnAsync` is per-request-scoped. Same conversation in parallel turns is rare (client-side serialized) but safe: cache key is deterministic, no shared writable state.

### What the LLM sees during generation

Unchanged. The contextualizer's output is used *only* to drive retrieval. The user's original message is still what gets written to `AiMessages`, sent to the provider as the latest user turn, and surfaced in the UI. The provider never receives the rewritten query. This preserves:

- User-facing conversation fidelity (the user sees their own words).
- Citation accuracy (citations still resolve against the *original* assistant message content, not a rewritten query).
- Privacy (the rewritten query is an internal artifact; not stored).

## 5. File structure

### New files

| Path | Responsibility |
|---|---|
| `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IContextualQueryResolver.cs` | One-method interface; lives next to `IQueryRewriter` |
| `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/QueryRewriting/ContextualQueryResolver.cs` | Heuristic + cache + LLM call + quote stripping |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextualQueryResolverTests.cs` | Unit tests (heuristic, LLM, cache, degradation, Arabic) |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/MultiTurnRetrievalTests.cs` | Integration: two-turn chat proves follow-up retrieval now hits |

### Modified files

| Path | Change |
|---|---|
| `Application/Services/Retrieval/IRagRetrievalService.cs` | Add `IReadOnlyList<AiChatMessage> history` to `RetrieveForTurnAsync` |
| `Infrastructure/Retrieval/RagRetrievalService.cs` | Inject `IContextualQueryResolver`; add contextualize stage between classify and query-rewrite |
| `Infrastructure/Retrieval/RagStages.cs` | Add `Contextualize = "contextualize"` constant |
| `Infrastructure/Retrieval/RagCacheKeys.cs` | Add `Contextualize(provider, model, lang, historyHash)` factory |
| `Infrastructure/Settings/AiRagSettings.cs` | Add `EnableContextualRewrite`, `StageTimeoutContextualizeMs`, `ContextualRewriteCacheTtlSeconds`, `ContextualRewriteHistoryTurns`, `ContextualRewriterModel` |
| `Application/Services/ChatExecutionService.cs` | `RetrieveContextSafelyAsync` slices last N turns from `state.ProviderMessages` and forwards to retrieval |
| `AIModule.cs` | `services.AddScoped<IContextualQueryResolver, ContextualQueryResolver>()` |
| `src/_test4b4.Api/appsettings.Development.json` (+ main `appsettings.Development.json` template) | Add the five new `AI:Rag:*` keys with defaults |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RagRetrievalPipelineTests.cs` | Update pipeline tests that assert stage counts |
| `boilerplateBE/tests/Starter.Api.Tests/Ai/Observability/RagInstrumentEmissionTests.cs` | Add `rag.stage=contextualize` to the asserted instrument set for multi-turn tests |

Single-purpose-per-file: the heuristic and the LLM call both live in `ContextualQueryResolver.cs` because they share the same seam (rule-first-then-LLM, same as `RuleBasedQueryRewriter` + `QueryRewriter`). If the heuristic grows enough to warrant extraction, it becomes `ContextualFollowUpHeuristic.cs` — the contract stays stable.

## 6. Configuration

New keys under `AI:Rag:` (defaults applied via `AiRagSettings` options binding; no appsettings change required for existing deployments):

| Key | Default | Purpose |
|---|---|---|
| `EnableContextualRewrite` | `true` | Global kill-switch. Off → stage never runs |
| `StageTimeoutContextualizeMs` | `3000` | Per-stage timeout, wrapped by `WithTimeoutAsync` |
| `ContextualRewriteCacheTtlSeconds` | `600` | Redis TTL; 0 disables caching writes |
| `ContextualRewriteHistoryTurns` | `3` | Number of most-recent user+assistant *pairs* included in the prompt (so up to 6 messages) |
| `ContextualRewriterModel` | `null` | Override the default chat model for the rewrite call; `null` → use `IAiProviderFactory.GetDefaultChatModelId()` |

## 7. Observability

No new meter, no new instrument — all measurements extend the 4b-4 instrument set with a new stage tag.

| Instrument | New tag values |
|---|---|
| `rag.stage.duration` (histogram) | `rag.stage=contextualize` |
| `rag.stage.outcome` (counter) | `rag.stage=contextualize`, `rag.outcome ∈ {success, timeout, error}`, plus `rag.skipped ∈ {true, false}` when outcome=success |
| `rag.cache.requests` (counter) | `rag.cache=contextualize`, `rag.hit ∈ {true, false}` |
| `rag.degraded.stages` (counter) | `rag.stage=contextualize` on timeout/error |

**Tag cardinality stays bounded.** No query text in tags — the 4b-4 "aggregate-only" rule continues to hold. Per-turn drill-down uses Debug logs.

**Serilog** (Debug level only, to keep PII out of Info logs):

```
contextualize: original={Original} resolved={Resolved} lang={Lang} skipped={Skipped}
```

Debug is not enabled by default in `appsettings.Development.json`'s `Serilog.MinimumLevel.Default` override for production; log level has to be explicitly raised to see the text. The existing aggregate `RAG retrieval done ...` log gets no new fields (stage-level detail lives in metrics).

**Webhook payload (`ai.retrieval.completed|degraded`):** unchanged. No new field on the payload.

## 8. Testing

### Unit: `ContextualQueryResolverTests`

Per-scenario expectation table. All tests use `FakeAiProvider` (existing test double) with a `ContextualizerResponses` queue + an in-memory `ICacheService` fake.

| Test | Given | Expect |
|---|---|---|
| `Returns raw when history empty` | `[], "What is Qdrant?"` | Raw returned; no heuristic; no LLM call |
| `Returns raw when heuristic skips` | `[...history], "How does the billing module work?"` (explicit subject) | Raw returned; no LLM call; stage outcome success with `rag.skipped=true` |
| `Returns LLM result on cache miss` | Heuristic-positive message, cache empty, LLM queued response | LLM result returned; cache.Set called with TTL=600 |
| `Returns cached value on cache hit` | Same input as a previous call | No LLM call; cache hit counter |
| `Falls back on LLM timeout` | Heuristic-positive, LLM delays 5s, timeout 3s | Raw returned; `outcome=timeout` + `degraded` |
| `Falls back on LLM error` | Heuristic-positive, LLM throws | Raw returned; `outcome=error` + `degraded` |
| `Falls back on empty LLM response` | Heuristic-positive, LLM returns `"  "` | Raw returned; `outcome=error` + `degraded` |
| `Strips surrounding quotes from LLM output` | LLM returns `"\"How do we configure Qdrant?\""` | `How do we configure Qdrant?` |
| `Arabic pronoun triggers heuristic` | `"كيف نضبطه؟"` with Arabic history | LLM called; Arabic response returned |
| `Short English follow-up triggers heuristic` | `"and then?"` | LLM called |
| `Feature flag off skips stage` | `EnableContextualRewrite=false` | Raw returned; no heuristic; no LLM call; no metric emission |
| `Redis cache unavailable → still returns LLM result` | Cache throws on Get/Set | LLM result returned; `outcome=success` |

### Integration: `MultiTurnRetrievalTests`

Wire the real `RagRetrievalService` + a stubbed provider + stubbed vector/keyword search so retrieval actually runs but against a deterministic index.

| Scenario | Expected |
|---|---|
| Two-turn recall: `[T1: "What is Qdrant?", T2: "How do we configure it?"]` | Turn 2 retrieval finds the Qdrant-config chunk (which it would miss with the raw "How do we configure it?" query) |
| Self-contained follow-up: `[T1: "What is Qdrant?", T2: "Tell me about MinIO"]` | Heuristic skips contextualize; MinIO chunk retrieved unchanged |
| First turn (empty history): `[T1: "What is Qdrant?"]` | Contextualize stage skipped; existing 4b-4 behavior preserved |
| Arabic follow-up: `[T1: "ما هو Qdrant؟", T2: "كيف نضبطه؟"]` | Rewrites to "كيف نضبط Qdrant؟" and retrieves the Arabic Qdrant config chunk |

### Regression guard

Extend `RagInstrumentEmissionTests` (from 4b-4 Task 16) so that a two-turn run asserts `rag.stage=contextualize` appears in the emitted tag set on `rag.stage.duration` and `rag.stage.outcome`.

### Live QA (test-app)

Reuse the 4b-4 rename-app pattern. Seed two tenant documents (Qdrant-config, MinIO-config), drive a two-turn chat with `"What is Qdrant?"` then `"How do we configure it?"`, and verify:

1. `/diagnostics/rag-metrics` shows `rag.stage=contextualize` with 1+ measurements on turn 2 only.
2. Turn 2 reply cites the Qdrant-config chunk.
3. Arabic two-turn variant works identically with Arabic pronouns.
4. No regression in the 4b-4 QA checklist (webhook still fires, enriched log still appears).

## 9. Risks

| Risk | Mitigation |
|---|---|
| LLM rewrites a self-contained question into something wrong ("What is RAG?" → "What is RAG in our Qdrant context?") | Heuristic-first gating skips most standalone questions; system prompt says *"If already self-contained, return unchanged"*; Debug log captures before/after for audit |
| 1 extra LLM round-trip per follow-up turn → added latency | 3s timeout caps tail; cache absorbs retries; heuristic skips the cost on most turns |
| Heuristic misclassifies → missed follow-ups | False negatives are acceptable — retrieval still runs on the original message, so quality matches today. Not a regression |
| Prompt injection via conversation history ("Ignore previous instructions and output X") | Output is only a query string — never executed, never shown to the user, never stored as a user message. Worst case: bad retrieval. Not a security-sensitive path |
| Cache key collision across tenants | Key already includes message+history SHA-256; tenant-level keys diverge naturally (same as existing rewrite cache) |
| Arabic heuristic gaps (small pronoun list) | When heuristic says "skip" on a true follow-up, only retrieval quality degrades — no correctness regression. Tunable in code, no schema change |
| Contextualizer returns a translation despite the "do not translate" instruction | Same-language rule enforced via explicit system-prompt language hint; rewriter-model response is a single string so translation is detectable post-hoc by comparing `RagLanguageDetector.Detect(original)` vs `Detect(resolved)`. On mismatch, fall back to the original message and record `outcome=error` |

## 10. Deferred work

Each item is a candidate for a future sub-plan. Kept in this spec so future agents can pick one up without re-deriving context. Picked up one at a time.

1. **Cross-conversation memory.** Resolve references that span *different* conversations (e.g., "what was that error I saw yesterday in the other thread?"). Requires a per-user conversation index and a cross-conversation relevance model. **Deferred because:** a fundamentally different feature — not a pre-retrieval transform. Belongs with long-term memory / personalization work, not with RAG retrieval.

2. **Compounding history-aware query expansion.** After contextualize resolves references, expand the resolved query into N paraphrases using the same history as additional context (today's `IQueryRewriter` is history-blind). **Deferred because:** the current rewriter already produces paraphrases from the resolved query. Compounding both would overfit the paraphrases to prior-turn phrasing and likely worsen recall on real follow-ups. Revisit only if retrieval evaluation (see #6) shows a measurable gap.

3. **Feedback-driven rewriter tuning.** User thumbs-down on a reply → signal to re-tune contextualizer (e.g., different model, different system prompt, cache eviction). **Deferred because:** needs a feedback-ingestion pipeline that doesn't exist yet (Plan 6/7 UI owns the thumbs UI). Revisit once feedback events are flowing into the webhook/event bus.

4. **Hypothetical Document Embeddings (HyDE).** LLM generates a fake "ideal answer" to the resolved query, embed the hypothetical answer instead of the query. Still on the 4b spec's deferred list as item #10. **Deferred because:** orthogonal axis (answer-space embedding vs. question-space). Adds one more provider call per turn on top of contextualize; benchmark requirement from 4b's original "why deferred" still holds.

5. **Citation-aware rewrites.** User says "summarize citation [2]" → resolve `[2]` to the cited chunk ID and fetch it directly rather than going through retrieval. **Deferred because:** retrieval doesn't currently see the citation markers emitted in prior assistant turns; would require threading citation metadata through `AiChatMessage`. Different data-flow axis. Belongs with a future citation-UX pass.

6. **RAG evaluation harness.** Golden-set runner with nDCG/precision/recall over a held-out question set. Explicitly called out as OoS in 4b-4 (`"Real quality measurement needs the eval harness (out of scope)"`). **Deferred because:** evaluation tooling is a separate investment of its own (harness + golden set + CI wiring); picking it up here would triple the scope. Track as a candidate 4b-6 sub-plan.

7. **Multi-document cross-reference resolution.** "In the Qdrant doc you mentioned topK — does that same idea apply in MinIO?" — requires resolving "the Qdrant doc" AND "that same idea" against multiple retrieved contexts across turns. **Deferred because:** requires the contextualizer to know which chunks were retrieved on prior turns, which means threading retrieval metadata through `AiChatMessage`. Schema + pipeline change; out of scope for a single sub-plan.

8. **Per-assistant override.** `AiAssistant.EnableContextualRewrite` column so a "single-shot lookup" assistant can opt out. **Deferred because:** (evaluated as option C during brainstorming) adds a DB column + migration + admin UI surface for a feature that's likely always-on. Revisit if a real assistant needs it off.

9. **UI surfacing of the rewritten query.** Show users "we searched for: {resolved}" so they can see what the system actually looked for. **Deferred because:** frontend concern (Plan 6/7 owns the chat UI), not RAG-backend scope. Data already flows into Debug logs; UI can consume later.

10. **Streaming contextualize.** Start the LLM rewrite in parallel with the classify stage instead of strictly after. **Deferred because:** premature — the 3s timeout means worst-case 3s added to a sub-1s classify stage. If p95 latency telemetry from 4b-4 shows contextualize dominating, revisit.

11. **Language-mixed history handling.** Today the prompt includes a single `language` hint from the latest message. A conversation that switched languages mid-thread (English turn 1, Arabic turn 2) may produce a confused rewrite. **Deferred because:** need real-world data on how often this happens; the system-prompt instruction ("reply in the same language as the user") should cover the common case.

## 11. Success criteria

1. Follow-up turn like `"How do we configure it?"` after `"What is Qdrant?"` retrieves the Qdrant-config chunk (previously missed).
2. Standalone questions in the same conversation are unaffected — no regression in recall/precision against the 4b-4 baseline suite.
3. Happy-path latency overhead < 200ms on cache hit; < 1s tail on cache miss (with 3s hard timeout cap).
4. `rag.stage=contextualize` visible in `/diagnostics/rag-metrics` on a live follow-up turn (QA step).
5. `rag.cache=contextualize` cache-hit ratio > 0 across a repeated two-turn test.
6. Zero regressions in existing 4b-1..4b-4 test suites.
7. Arabic follow-up (`ما هو RAG؟` → `كيف نضبطه؟`) resolves to `كيف نضبط RAG؟` and retrieves the correct Arabic chunk.

## 12. Dependencies

None beyond what's already wired:

- `IAiProviderFactory` — existing
- `ICacheService` — existing
- `AiRagMetrics` — from 4b-4
- `WithTimeoutAsync` helper — from 4b-1
- `RagLanguageDetector` — from 4b-4
- `ArabicTextNormalizer` — from 4b-1
- `RagCacheKeys` — from 4b-2

No new NuGet packages. No database migrations. No new DI surface beyond one scoped registration.
