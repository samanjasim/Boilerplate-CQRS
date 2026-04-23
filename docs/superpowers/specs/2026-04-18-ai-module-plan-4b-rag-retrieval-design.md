# AI Module — Plan 4b: RAG Retrieval + Chat Injection (Design)

**Status:** Design approved, pending implementation plan.
**Supersedes:** Nothing. **Builds on:** Plan 4a (RAG Document Ingestion — merged on branch `feature/ai-integration`, commits `50a8fc6`, `0664d49`, `4fc5f72` at time of writing).
**Next step:** `writing-plans` skill produces `docs/superpowers/plans/2026-04-18-ai-module-plan-4b-rag-retrieval.md`.

## Goal

Wire retrieved document chunks into the live chat loop so a `RagScope`-enabled assistant grounds its answers in tenant knowledge-base content and emits source citations. Also expose `POST /ai/search` so operators and QA can probe retrieval quality without booting a chat. This plan does not implement query expansion or LLM re-ranking — those are deferred to Plan 4b-2 and the flags remain in `appsettings` but default off.

After this plan, the full RAG loop works end-to-end for a tenant admin:

1. Upload document (Plan 4a, done).
2. Create an assistant with `RagScope = SelectedDocuments` pointing at those doc ids.
3. Chat with the assistant; replies cite sources inline as `[1]`, `[2]`; the message stream emits a structured `citations` event; `AiMessage.Citations` JSONB is populated.

## Scope decisions made during brainstorming

| Decision | Value | Reason |
|---|---|---|
| **4b-1 vs 4b-all** | Ship hybrid retrieval + injection + `/ai/search` + citations only. Defer QE + re-ranker. | Hybrid alone is a complete demonstrable RAG loop. QE/re-ranker each add a provider round-trip per turn with unclear value until we have real content to benchmark. |
| **Retrieval gate** | `AiAssistant.RagScope` enum: `None` / `SelectedDocuments` / `AllTenantDocuments`. | Three explicit states, no ambiguous "enabled but empty doc list" combination. An assistant that must not see tenant-wide content simply can't be set to `AllTenantDocuments`. |
| **Citations UX** | Inline `[n]` markers + parsed into structured `Citations` list. Fallback to full retrieved-chunk set when parser finds nothing. | Richer UX than side-channel-only while staying robust to model misbehaviour. |
| **Parent-chunk inclusion** | Children + parents, deduped, token-budget capped at `MaxContextTokens = 4000`. | Parents meaningfully improve answer quality on paragraph-spanning questions; dedup + budget keep cost bounded. |
| **`/ai/search` permission** | New `Ai.SearchKnowledgeBase` permission. Seeded for `SuperAdmin` + `TenantAdmin` only. | Raw chunks can be sensitive; QA/content teams get scoped access without full doc-management rights. |

## Architecture

One new seam: `IRagRetrievalService`, called from `ChatExecutionService` right before building the provider request. Rest of the chat loop (tools, streaming, usage logging, persistence) unchanged.

```
ChatExecutionService.ExecuteAsync
  ├─ Resolve assistant + conversation history          (unchanged)
  ├─ Resolve tools                                     (unchanged)
  ├─ ▼ NEW: RagRetrievalService.RetrieveForTurnAsync   (only if RagScope != None)
  │     ├─ EmbeddingService.EmbedAsync(latestUserMessage)    (reuses Plan 4a)
  │     ├─ IVectorStore.SearchAsync(tenantId, vector, filter, limit = RetrievalTopK)
  │     ├─ IKeywordSearchService.SearchAsync(tenantId, text, filter, limit = RetrievalTopK)
  │     ├─ Hybrid merge: α·semantic_norm + (1-α)·keyword_norm
  │     ├─ Dedup, top-K children
  │     ├─ Union parents, dedup
  │     └─ Trim to MaxContextTokens (SharpToken; children first, then parents)
  ├─ ContextPromptBuilder.Build(assistant.SystemPrompt, retrievedContext)
  ├─ BuildChatOptions (system prompt now includes <context>[1]..[N]</context>)
  ├─ Provider.StreamChatAsync                          (unchanged)
  ├─ CitationParser.Parse(finalText, retrievedContext.Children)
  │    └─ fallback: full chunk set if no markers parseable
  ├─ Persist AiMessage with Citations JSON             (new column)
  └─ SSE: existing events + one new `citations` event before `done`
```

New files concentrate in three folders:

- `Application/Services/Retrieval/` — `IRagRetrievalService`, `IKeywordSearchService`, DTOs (`RetrievedChunk`, `RetrievedContext`, `AiMessageCitation`).
- `Infrastructure/Retrieval/` — `RagRetrievalService`, `PostgresKeywordSearchService`, `CitationParser`, `ContextPromptBuilder`, `HybridScoreCalculator`.
- `Application/Features/Search/` — `SearchKnowledgeBaseQuery` + handler + `SearchKnowledgeBaseResultDto`.

`IVectorStore` gets one method added (`SearchAsync`); Plan 4a's `QdrantVectorStore` implementation grows one method. No new NuGet packages.

## Data model changes

Per the repository rule, no migrations are committed to the boilerplate. The test app (generated via `scripts/rename.ps1`) creates its own migrations.

### 1. `AiAssistant.RagScope` (enum)

```csharp
public enum AiRagScope
{
    None = 0,
    SelectedDocuments = 1,
    AllTenantDocuments = 2
}
```

- Domain: new property on `AiAssistant`, `SetRagScope(AiRagScope)` method.
- Validation in `CreateAssistantCommand` / `UpdateAssistantCommand`:
  - `SelectedDocuments` requires `KnowledgeBaseDocIds.Count > 0` → `AiErrors.RagScopeRequiresDocuments`.
  - `AllTenantDocuments` does not read `KnowledgeBaseDocIds`.
- EF config: stored as `int`. Default `None`.
- Added to `AiAssistantDto`, both commands.

### 2. `AiMessage.Citations` (JSONB)

```csharp
public sealed record AiMessageCitation(
    int Marker,                         // corresponds to [n] in the reply text
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    string? SectionTitle,
    int? PageNumber,
    decimal Score);                     // hybrid score at injection time

// On AiMessage:
public IReadOnlyList<AiMessageCitation> Citations { get; private set; } = [];
```

- EF config: `HasColumnType("jsonb")` with JSON converter, matching the existing pattern on `AiAssistant.KnowledgeBaseDocIds` / `AiAssistant.ToolIds`.
- Populated only on assistant-turn saves where retrieval fired AND returned ≥1 chunk.
- `AddAssistantMessage` (or equivalent factory) gains a `Citations` parameter; other call sites pass `[]`.

### 3. Postgres FTS column on `AiDocumentChunk`

Added in the test-app migration via raw SQL:

```sql
ALTER TABLE ai_document_chunks
  ADD COLUMN content_tsv tsvector
  GENERATED ALWAYS AS (to_tsvector('english', content)) STORED;

CREATE INDEX ix_ai_document_chunks_content_tsv
  ON ai_document_chunks USING GIN (content_tsv);
```

- `PostgresKeywordSearchService` queries this via `EF.Functions.ToTsQuery(...)` or raw SQL with `plainto_tsquery(@q)` and `ts_rank_cd`.
- `english` config hard-coded for v1; multi-language support listed in deferred work.
- Scoped to `ChunkLevel == Child` (parents retrieved separately in step 6 of the pipeline).

Nothing else changes — `AiDocumentChunk` (Plan 4a) already has `ParentChunkId`, `PageNumber`, `SectionTitle`, `Content`, `QdrantPointId`.

## Retrieval pipeline

`RagRetrievalService.RetrieveForTurnAsync(assistant, latestUserMessage, ct)` — single entry point, pure orchestration.

### Step 1 — doc filter from `RagScope`

```csharp
var docFilter = assistant.RagScope switch
{
    AiRagScope.None => throw new InvalidOperationException(),    // caller gates
    AiRagScope.SelectedDocuments => assistant.KnowledgeBaseDocIds,
    AiRagScope.AllTenantDocuments => null,                       // no doc filter, tenant filter only
};
```

### Step 2 — embed query

`embeddingService.EmbedAsync(latestUserMessage.Content)` reuses Plan 4a's `EmbeddingService`. It logs `AiUsageLog` with a new `RequestType.QueryEmbedding` sub-type so cost can be separated from ingestion embeddings.

### Step 3 — parallel fan-out

`Task.WhenAll`:

- `IVectorStore.SearchAsync(tenantId, vector, docFilter, limit: RetrievalTopK)` — new method on existing seam. Qdrant filter `tenant_id == tenantId AND chunk_level == "child" [AND document_id IN docFilter]`. Returns `(ChunkId, SemanticScore)` list.
- `IKeywordSearchService.SearchAsync(tenantId, latestUserMessage.Content, docFilter, limit: RetrievalTopK)` — pg FTS `plainto_tsquery` + `ts_rank_cd`. Scoped to `ChunkLevel == Child`. Returns `(ChunkId, KeywordScore)` list.

Both return chunk ids only; bodies are fetched once in step 6.

### Step 4 — hybrid merge

Min-max normalize each side to [0,1] over its own result set, then:

```
hybrid = α · semantic_norm + (1 - α) · keyword_norm        // α = HybridSearchWeight = 0.7
```

Chunks present in only one list receive `0` on the missing axis before normalization — a chunk that is strong on one axis still competes, but does not dominate chunks solid on both.

### Step 5 — top-K children

Take `TopK = 5` by hybrid score descending. Stable tie-breaking by chunk id. Drop anything below `MinHybridScore` (default `0.0`, i.e. no floor in v1; config exists to enable tuning later).

### Step 6 — fetch bodies

Two round-trips (both `AsNoTracking`, both tenant-filter guarded by EF global filter):

```csharp
var childIds = topK.Select(r => r.ChunkId).ToList();

// Pass 1: fetch the children (includes each child's ParentChunkId + Document for DocumentName).
var children = await db.AiDocumentChunks
    .AsNoTracking()
    .Include(c => c.Document)
    .Where(c => childIds.Contains(c.Id))
    .ToListAsync(ct);

// Pass 2: fetch the distinct parents referenced by those children.
var parentIds = children
    .Where(c => c.ParentChunkId.HasValue)
    .Select(c => c.ParentChunkId!.Value)
    .Distinct()
    .ToList();

var parents = parentIds.Count == 0
    ? []
    : await db.AiDocumentChunks
        .AsNoTracking()
        .Include(c => c.Document)
        .Where(c => parentIds.Contains(c.Id))
        .ToListAsync(ct);
```

Two small queries rather than a single UNION-style query because both `IN`-lists are small (`TopK = 5` and ≤ 5 distinct parents) and two `Contains` queries are clearer than a combined one.

### Step 7 — assemble `RetrievedContext`

Children ordered by hybrid score descending; each child is associated with its parent (if any) via `ParentChunkId`. Parents are deduped (multiple children can share one parent).

### Step 8 — token-budget trim

`MaxContextTokens = 4000`, measured via Plan 4a's `TokenCounter` (SharpToken).

- Priority: children kept first (they passed Step 5).
- Parents kept in descending child-score order until budget exhausted.
- If even child-only content exceeds budget, drop lowest-scored children until it fits; log `warn` with `TopK`, `MaxContextTokens`, retained count; set `TruncatedByBudget = true`.

### Step 9 — return

```csharp
public sealed record RetrievedContext(
    IReadOnlyList<RetrievedChunk> Children,
    IReadOnlyList<RetrievedChunk> Parents,
    int TotalTokens,
    bool TruncatedByBudget);

public sealed record RetrievedChunk(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    string Content,
    string? SectionTitle,
    int? PageNumber,
    ChunkLevel ChunkLevel,
    decimal SemanticScore,
    decimal KeywordScore,
    decimal HybridScore,
    Guid? ParentChunkId);
```

### Empty-result policy

Both sides return zero hits → empty `RetrievedContext`. `ChatExecutionService` detects this and skips the `<context>` block entirely; the system prompt stays as the assistant's original. Citations column stays empty for the turn. A warning log (`"Assistant {AssistantId} turn retrieved 0 chunks"`) surfaces misconfigured / unused RAG assistants.

## Chat injection + citation flow

### System-prompt template

When `RetrievedContext.Children` is non-empty, `ContextPromptBuilder` prepends:

```
You have access to the following knowledge base excerpts, numbered [1]..[N].
Ground your answer in these excerpts when they are relevant.
When you use information from an excerpt, cite it inline as [n] (or [n, m] for multiple).
If the excerpts do not contain the answer, say so plainly and do not fabricate citations.

<context>
[1] Document: "<DocumentName>" · Section: "<SectionTitle>" · Page <N>
<child content>
(context continues)
<parent content>

[2] ...
</context>

<assistant_instructions>
<original assistant system prompt>
</assistant_instructions>
```

- Numbering is by hybrid-score order (same as `RetrievedContext.Children`).
- Parents appear immediately beneath their child, prefixed `(context continues)`, no separate marker.
- Empty retrieved context → no `<context>` block; only the `<assistant_instructions>` wrapper renders.

### Citation parser

After the stream ends, before persisting:

- Regex: `\[(\d+(?:\s*,\s*\d+)*)\]` — matches `[1]`, `[3]`, `[2, 4]`, `[1,2,3]`, tolerant of whitespace.
- Valid indices: `1..Children.Count`. Out-of-range markers are dropped with a warning log.
- Duplicates deduped across the whole message (a chunk cited twice appears once in `Citations`).

**Fallback:** parser yields zero valid markers AND `Children.Count > 0` → attach full `Children` list as `Citations` with `Marker = index + 1`. Diagnostic log: `"Assistant turn retrieved N chunks but emitted 0 parseable citation markers"`. This protects UX on models that ignore citation instructions and lets us tune the prompt per provider later.

### Stream events

Existing ordering preserved; one new event added:

```
message_start → content (delta, delta, ...) → [tool_use if any] → citations → done
```

`citations` event payload:

```json
{
  "type": "citations",
  "items": [
    {
      "marker": 1,
      "chunkId": "...",
      "documentId": "...",
      "documentName": "Biology Textbook",
      "sectionTitle": "Light-Dependent Reactions",
      "pageNumber": 45,
      "score": 0.87
    }
  ]
}
```

Emitted exactly once per turn, right before `done`, only when citations are non-empty. Not emitted when retrieval fired but produced nothing; not emitted when `RagScope == None`.

### Conversation-history handling

On later turns, prior assistant messages are sent to the provider **with their inline `[n]` markers intact but without their original `<context>` block** — previously-retrieved excerpts are not re-injected. The new turn gets its own freshly retrieved `<context>` numbered starting from `[1]` again. This prevents the context window from accumulating across a long conversation.

## `POST /ai/search` endpoint

Thin MediatR-backed passthrough to `RagRetrievalService` for QA and admin tuning.

### Request

```
POST /api/v1/ai/search
{
  "query": "photosynthesis Calvin cycle",
  "documentIds": ["..."],        // optional; null → all tenant docs
  "topK": 10,                    // optional; default AI:Rag:TopK, max AI:Rag:RetrievalTopK
  "minScore": 0.3,               // optional; filters post-merge
  "includeParents": true         // optional; default true
}
```

### Response

```json
{
  "data": {
    "items": [
      {
        "chunkId": "...",
        "documentId": "...",
        "documentName": "Biology Textbook",
        "content": "...",
        "sectionTitle": "Light-Dependent Reactions",
        "pageNumber": 45,
        "chunkLevel": "child",
        "hybridScore": 0.87,
        "semanticScore": 0.91,
        "keywordScore": 0.62,
        "parentChunkId": "..."
      },
      {
        "chunkId": "...",
        "chunkLevel": "parent",
        "content": "...",
        "hybridScore": null
      }
    ],
    "totalHits": 12,
    "truncatedByBudget": false
  }
}
```

Parent chunks (when `includeParents = true`) appear immediately after their child with `hybridScore = null` and `chunkLevel = "parent"` — same shape as chat retrieval.

### Auth + permissions

- `[Authorize(Policy = AiPermissions.SearchKnowledgeBase)]`.
- `Ai.SearchKnowledgeBase` added to `Starter.Module.AI/Constants/AiPermissions.cs` **and** mirrored in `boilerplateFE/src/constants/permissions.ts` for FE/BE parity.
- Default role seeding (in `DataSeeder` — lives in source but only runs in generated test apps):
  - `SuperAdmin`: granted.
  - `TenantAdmin`: granted.
  - `User`: not granted.

### Rate limiting

- Falls under existing global rules (10/s, 100/min).
- Adds a per-endpoint rule in `IpRateLimiting.GeneralRules`: `*/ai/search` → 30/min per IP, to protect against scripted KB enumeration via small-`topK` probing.

### Pagination

No offset pagination in v1. `topK` caps the result set; larger needs are met by raising `topK` up to `RetrievalTopK`. Offset pagination over a hybrid-ranked set is deferred.

### Cost side-effect

The endpoint embeds the query → writes `AiUsageLog` with `RequestType.QueryEmbedding` and the caller's user id, same as a chat turn. Admins using `/ai/search` for QA will generate billable embedding calls; documented in the OpenAPI description.

## Configuration

Added / changed under the existing `AI:Rag` block (most keys were declared but dormant in Plan 4a):

```json
"AI": {
  "Rag": {
    "TopK": 5,                          // ACTIVE in 4b-1
    "RetrievalTopK": 20,                // ACTIVE in 4b-1
    "HybridSearchWeight": 0.7,          // ACTIVE in 4b-1 (α)
    "MaxContextTokens": 4000,           // NEW in 4b-1
    "IncludeParentContext": true,       // NEW in 4b-1 (for A/B tuning)
    "MinHybridScore": 0.0,              // NEW in 4b-1 (floor before top-K cut)
    "FtsLanguage": "english",           // NEW; hard-coded usage in 4b-1
    "EnableQueryExpansion": false,      // flipped to false; wired in 4b-2
    "EnableReranking": false            // flipped to false; wired in 4b-2
  }
}
```

Plan 4a declared `EnableQueryExpansion: true` / `EnableReranking: true` as forward-looking placeholders; flipped to `false` here so nothing silently short-circuits when those code paths don't exist yet.

## Error codes

In `AiErrors.cs`:

```csharp
public static readonly Error SearchQueryRequired =
    new("Ai.SearchQueryRequired", "Search query is required.");

public static Error SearchTopKOutOfRange(int max) =>
    new("Ai.SearchTopKOutOfRange", $"topK must be between 1 and {max}.");

public static readonly Error RagScopeRequiresDocuments =
    new("Ai.RagScopeRequiresDocuments",
        "RagScope SelectedDocuments requires at least one knowledge base document id.");
```

## OpenTelemetry

New spans, attached to the existing `TracingBehavior` pipeline:

- `ai.rag.retrieve` — parent span around `RagRetrievalService.RetrieveForTurnAsync`. Attributes: `tenant_id`, `assistant_id`, `rag_scope`, `chunk_count`, `hybrid_top_score`, `truncated_by_budget`, `retrieval_latency_ms`.
- `ai.rag.vector_search` — child span around `IVectorStore.SearchAsync`. Attributes: `hit_count`, `top_score`.
- `ai.rag.keyword_search` — child span around `IKeywordSearchService.SearchAsync`. Attributes: `hit_count`, `top_score`.

## Testing strategy

### Unit tests (no infrastructure)

- `CitationParserTests` — canonical markers, multi-index `[2, 4]`, whitespace tolerance, out-of-range dropped with warning, no-marker fallback returns full chunk set, duplicates deduped.
- `ContextPromptBuilderTests` — empty context omits `<context>` block, non-empty renders `1..N`, parents appear adjacent to their child without own marker, original system prompt preserved inside `<assistant_instructions>`.
- `HybridScoreCalculatorTests` — min-max normalization, α-blend math, single-side hits score correctly, stable tie-breaking, `MinHybridScore` filter.
- `RagRetrievalServiceTests` — with mocked `IVectorStore`, `IKeywordSearchService`, `IEmbeddingService`, in-memory `AiDbContext`:
  - `RagScope.None` throws (defensive; caller gates).
  - `SelectedDocuments` with empty `KnowledgeBaseDocIds` throws `AiErrors.RagScopeRequiresDocuments` (belt-and-braces; validator should have caught).
  - Token-budget trim drops lowest-scored children when over cap, sets `TruncatedByBudget = true`.
  - Empty results on both sides return empty context without exception.
  - Parent dedup: two children sharing one parent → parent appears once.
- `AiAssistantTests` — `SetRagScope(SelectedDocuments)` with empty ids throws; `SetRagScope(AllTenantDocuments)` does not require ids; DTO round-trip includes `RagScope`.

### Integration tests (Testcontainers)

- `PostgresKeywordSearchServiceTests` — real Postgres fixture, seeds tsvector-indexed chunks, verifies `plainto_tsquery` scoring, tenant filter, doc-id filter, child-only constraint, empty-query handling.
- `AiSearchControllerTests` — upload doc via Plan 4a ingestion path → `POST /ai/search` → assert response shape, permission gating (401 unauth, 403 without permission, 200 with it), rate-limit 429 after 30 requests/min.
- `ChatExecutionRagInjectionTests` — one full turn end-to-end against `FakeAiProvider` that echoes the received system prompt back as the assistant message. Asserts:
  - `RagScope = None` → system prompt is the assistant's original, no citations.
  - `RagScope = SelectedDocuments` → system prompt contains `<context>[1]..[N]</context>`, citations populated on the returned message.
  - Parser fallback fires when stub "forgets" markers → `Citations = Children` in hybrid order.
  - `citations` SSE event emitted before `done` when non-empty, skipped otherwise.

### Fakes and fixtures

- `FakeVectorStore` implements `IVectorStore` over an in-memory list + cosine math; used instead of real Qdrant for unit tests. Real Qdrant covered by end-to-end verification.
- `FakeAiProvider` implements `IAiProvider` and returns a deterministic response for unit testing stream events.

### End-to-end verification (post-feature-testing skill)

After build + unit/integration tests pass, run the `.claude/skills/post-feature-testing.md` flow against a fresh `_testAiRag2/` test app (port 5102 / 3102 — 5100 range may still be held by other tests). Verification sequence:

1. Upload two small documents via Plan 4a ingestion.
2. Create assistant A with `RagScope = SelectedDocuments` targeting doc 1 only.
3. Send chat turn referencing doc 1 content → assert response cites `[1]`, `AiMessage.Citations` populated, `ai_usage_logs` has one `QueryEmbedding` row plus a normal chat row.
4. Flip assistant A to `RagScope = None`, resend same question → assert citations empty, answer is generic.
5. Flip assistant A to `RagScope = AllTenantDocuments`, ask a question answerable by doc 2 only → assert citations include doc 2.
6. Hit `POST /ai/search` directly with a query matching doc 1 → assert results ordered by hybrid score, include parents.
7. Try `POST /ai/search` as a regular `User` (without `SearchKnowledgeBase`) → assert 403.
8. Flip `RagScope = SelectedDocuments` with empty doc id list → assert validator rejects with `AiErrors.RagScopeRequiresDocuments`.

## Deferred work

Everything not in 4b-1, grouped by target plan. Each item gets a "why deferred" note so future sessions (different agents, after reviews, after interruptions) can resume without re-deriving context. Picked up one at a time.

### Plan 4b-2 — Retrieval quality upgrades

1. **LLM query expansion.** Hook exists (`AI:Rag:EnableQueryExpansion` flag, default `false` in 4b-1). Add `IQueryExpansionService`; expand user query into 3 paraphrases, run hybrid on each, merge/dedupe. **Deferred because:** adds one provider round-trip per turn; value unclear until real content exists to benchmark against.
2. **LLM re-ranking.** Hook exists (`AI:Rag:EnableReranking` flag). Add `IRerankerService`; after top-20 hybrid, score each vs. the *original* query with a cheap model (Haiku / GPT-4o-mini), take top-5 by rerank score. **Deferred because:** same cost concern as QE; we also want real data before committing to a reranker model.
3. **Contextual query rewrite for multi-turn.** Resolve "what about X?" follow-ups by rewriting the user query against the last N messages before retrieval. **Deferred because:** distinct design axis from QE; decide in 4b-2 whether to fold into `IQueryExpansionService` or give its own seam.
4. **Cross-encoder reranker (alternative to LLM).** Requires new NuGet (ML.NET ONNX or sentence-transformers wrapper). **Deferred because:** need a benchmark against LLM reranker first; avoid adding a dependency we may not keep.

### Plan 4b-3 — Quotas, cost controls, admin UX

5. **Quota gating on `/ai/search`.** Call `IQuotaChecker.CheckAsync` before embedding the query; refuse with 429 when over. **Deferred because:** billing module integration is its own testable surface; admins are currently the only callers.
6. **Embedding cache.** SHA-256 the query text, short-TTL Redis cache of `(hash → vector)`. **Deferred because:** premature until we can see repeat-query rates in logs.
7. **Per-document ACLs.** Document-level permissions beyond tenant scope (e.g. "HR can see HR docs only"). Schema: `AiDocumentAccess` join table; retrieval filter adds `document_id IN (user's visible set)`. **Deferred because:** separate product-level decision; tenant-wide access is sufficient for v1.
8. **Offset pagination on `/ai/search`.** **Deferred because:** no UI consumes it yet.

### Plan 4b-4 — Advanced retrieval

9. **Multi-language FTS.** `AI:Rag:FtsLanguage` currently hard-coded `english`. Support per-document language detection or per-tenant default. **Deferred because:** requires tsvector migration changes and a language-detection strategy; v1 is English-document-optimised.
10. **Hypothetical Document Embeddings (HyDE).** LLM generates a fake "ideal answer", embed that, retrieve against the hypothetical embedding. **Deferred because:** adds one provider call per turn; benchmark first.
11. **MMR (Max Marginal Relevance) diversification.** Current top-K by pure hybrid can return 5 near-duplicate chunks. **Deferred because:** symptom not observed yet; fix if/when near-dup problems surface in QA.
12. **Streaming retrieval UX.** Emit `retrieval_started` / `retrieval_complete` SSE events with retrieved chunks so the UI can show "Searched N sources..." before tokens start streaming. **Deferred because:** orthogonal to correctness; polish that belongs with the frontend plan.

### Plan 4b-5 — Frontend (belongs to Plan 6/7 domain)

13. **Citations UI.** Hover-to-preview on `[n]` markers, "Sources" accordion beneath replies, click-through to document detail page. **Deferred because:** Plan 6/7 owns the chat UI.
14. **`/ai/search` page.** Admin tool to tune retrieval: slider for α, topK, parent-inclusion toggle, side-by-side per-chunk scores. **Deferred because:** needs the BE endpoint (this plan), plus admin UI work.
15. **Assistant-config UI for `RagScope`.** Dropdown (None / Selected / AllTenant), doc picker shown only when `Selected`. **Deferred because:** Plan 7 owns the assistants admin UI.

### Plan 4b-6 — Observability & diagnostics

16. **Per-turn retrieval log table.** New `ai_retrieval_logs` table with `(message_id, query, chunks_retrieved, top_score, truncated_by_budget, latency_ms)` for offline quality analysis. **Deferred because:** OpenTelemetry spans (this plan) cover the 80% case; dedicated table is an optimisation once we have query volume.
17. **Retrieval hit-rate dashboard.** Grafana panel showing % of RAG-enabled turns that injected ≥1 chunk vs. returned empty context. **Deferred because:** needs #16 first or a derived metric from spans.
18. **"Answered from context" classifier.** Post-hoc label on assistant turns indicating whether the reply actually used injected chunks vs. answered from training. LLM-as-judge or n-gram overlap heuristic. **Deferred because:** research-grade signal; ship the happy path first.

### Cross-cutting

19. **Circuit breaker around Qdrant / Postgres FTS.** Polly resilience policy so one side failing degrades to the other instead of failing the whole turn. **Deferred because:** need to observe real failure modes before choosing fall-back behaviour.
20. **Large-result streaming.** `RetrievedContext` currently materialises fully in memory. **Deferred because:** current `RetrievalTopK = 20` is tiny; revisit only if `RetrievalTopK > 100` becomes a real config.

## File map (preview — writing-plans will flesh this out)

### New files in `Starter.Module.AI`

| File | Purpose |
|---|---|
| `Application/Services/Retrieval/IRagRetrievalService.cs` | Contract: `RetrieveForTurnAsync(assistant, userMessage, ct)` |
| `Application/Services/Retrieval/IKeywordSearchService.cs` | Contract: `SearchAsync(tenantId, query, docFilter?, limit, ct)` |
| `Application/Services/Retrieval/RetrievedContext.cs` | Record holding `Children`, `Parents`, `TotalTokens`, `TruncatedByBudget` |
| `Application/Services/Retrieval/RetrievedChunk.cs` | Record holding chunk metadata + scores |
| `Application/Services/Retrieval/AiMessageCitation.cs` | Record persisted in `AiMessage.Citations` JSON |
| `Infrastructure/Retrieval/RagRetrievalService.cs` | Orchestration (steps 1–9) |
| `Infrastructure/Retrieval/PostgresKeywordSearchService.cs` | pg FTS via `plainto_tsquery` + `ts_rank_cd` |
| `Infrastructure/Retrieval/HybridScoreCalculator.cs` | Min-max normalise + α-blend |
| `Infrastructure/Retrieval/CitationParser.cs` | Regex parser + fallback |
| `Infrastructure/Retrieval/ContextPromptBuilder.cs` | System-prompt template renderer |
| `Application/Features/Search/SearchKnowledgeBaseQuery.cs` | `(Query, DocumentIds?, TopK?, MinScore?, IncludeParents)` |
| `Application/Features/Search/SearchKnowledgeBaseQueryHandler.cs` | Calls `IRagRetrievalService` with a synthetic "turn" shape |
| `Application/Features/Search/SearchKnowledgeBaseResultDto.cs` | `Items`, `TotalHits`, `TruncatedByBudget` |
| `Controllers/AiSearchController.cs` | `POST /api/v1/ai/search` |

### Modified files

| File | Change |
|---|---|
| `Domain/Entities/AiAssistant.cs` | Add `RagScope`, `SetRagScope`; validation. |
| `Domain/Enums/AiRagScope.cs` | New enum. |
| `Infrastructure/Configurations/AiAssistantConfiguration.cs` | Configure `RagScope` as `int`. |
| `Application/Commands/CreateAssistant/CreateAssistantCommand.cs` + Handler + Validator | Accept `RagScope`, validate `SelectedDocuments` requires docs. |
| `Application/Commands/UpdateAssistant/UpdateAssistantCommand.cs` + Handler + Validator | Same. |
| `Application/DTOs/AiAssistantDto.cs` | Add `RagScope`. |
| `Application/DTOs/AiAssistantMappers.cs` | Map `RagScope`. |
| `Domain/Entities/AiMessage.cs` | Add `Citations` (IReadOnlyList\<AiMessageCitation\>), factory param. |
| `Infrastructure/Configurations/AiMessageConfiguration.cs` | Configure `Citations` as JSONB. |
| `Application/Services/Ingestion/IVectorStore.cs` | Add `SearchAsync(tenantId, vector, docFilter?, limit, ct)`. |
| `Infrastructure/Ingestion/QdrantVectorStore.cs` | Implement `SearchAsync`. |
| `Application/Services/ChatExecutionService.cs` | Call retrieval before building chat options; parse citations; emit `citations` event; persist `AiMessage.Citations`. |
| `Application/Services/ChatStreamEvent.cs` | New `CitationsEvent` variant. |
| `Domain/Errors/AiErrors.cs` | Add `SearchQueryRequired`, `SearchTopKOutOfRange`, `RagScopeRequiresDocuments`. |
| `Constants/AiPermissions.cs` | Add `SearchKnowledgeBase`. |
| `AIModule.cs` | Register `IRagRetrievalService`, `IKeywordSearchService`, `HybridScoreCalculator`, `CitationParser`, `ContextPromptBuilder` (all scoped/singleton as appropriate). |
| `Application/Domain/Enums/RequestType.cs` | Add `QueryEmbedding`. |
| `Infrastructure/Ingestion/EmbeddingService.cs` | Accept `RequestType` param so query embeds log as `QueryEmbedding`. |
| `Starter.Api/appsettings.Development.json` | Add `MaxContextTokens`, `IncludeParentContext`, `MinHybridScore`, `FtsLanguage`; flip QE+Reranker flags to false. |
| `Starter.Api/appsettings.Development.json` → `IpRateLimiting` | Add `*/ai/search` → 30/min rule. |
| `Starter.Shared/Constants/Permissions.cs` | Add `Ai.SearchKnowledgeBase`. |
| `boilerplateFE/src/constants/permissions.ts` | Mirror. |
| `DataSeeder.cs` (source; runs only in test apps) | Seed `Ai.SearchKnowledgeBase` to SuperAdmin + TenantAdmin roles. |

### Notably unchanged

- `Qdrant.Client` NuGet version, `SharpToken` version — no new packages.
- Plan 4a's `AiDocument` / `AiDocumentChunk` entities — no schema change beyond the FTS column (test-app migration only).
- Chat SSE event shape for `message_start`, `content`, `tool_use`, `done` — preserved byte-for-byte so existing frontends stay compatible.

## Non-goals for Plan 4b-1

- No migrations committed to the boilerplate. Test app generates them.
- No frontend changes. FE permission constant mirror is the only front-of-house touch.
- No quota enforcement on `/ai/search`.
- No multi-language FTS.
- No query expansion or re-ranking (flags exist, default off; 4b-2).
- No UI for `RagScope` (BE only; Plan 7 owns assistants UI).
- No changes to `AiDocument` / `AiDocumentChunk` beyond the FTS generated column (test-app migration only).

## Risks

| Risk | Mitigation |
|---|---|
| Model ignores citation instructions → every turn falls back to attaching full chunk set. | Parser fallback preserves UX. Diagnostic log + per-provider prompt tuning in 4b-2. |
| pg FTS + Qdrant scores normalise differently at low hit counts → top-K is unstable. | Min-max over the returned list plus unit tests covering single-side-hit scenarios. |
| Token budget over-aggressively drops parents, losing context. | `TruncatedByBudget` flag surfaces the truncation; `MaxContextTokens` and `IncludeParentContext` are configurable. |
| Embedding cost on `/ai/search` (admins probing) adds up fast. | Per-endpoint rate limit (30/min); `QueryEmbedding` `RequestType` isolates the cost in `ai_usage_logs`. Cache deferred to 4b-3. |
| Parent chunks shared across many children create N+1-style memory waste on retrieval. | Step 6 fetches parents in a single `Contains`-filtered query; step 7 dedups. |

## Acceptance criteria

1. An assistant with `RagScope = SelectedDocuments` and a populated `KnowledgeBaseDocIds` cites sources inline in replies; `AiMessage.Citations` persisted; `citations` SSE event emitted before `done`.
2. An assistant with `RagScope = None` behaves exactly as pre-4b (no retrieval, no citations, no extra provider calls).
3. `POST /ai/search` returns hybrid-ranked chunks with scores; enforced by `Ai.SearchKnowledgeBase`; rate-limited 30/min.
4. `ai_usage_logs` contains rows with `RequestType = QueryEmbedding` for every retrieval-firing turn AND every `/ai/search` call.
5. Full unit + integration test suite green; `FakeVectorStore`-driven retrieval tests pass; real-Postgres keyword-search integration test passes.
6. End-to-end post-feature-testing run (8 steps above) passes against a freshly generated test app.
7. All deferred items are enumerated in this spec with "why deferred" notes so future sessions can resume one-by-one.
