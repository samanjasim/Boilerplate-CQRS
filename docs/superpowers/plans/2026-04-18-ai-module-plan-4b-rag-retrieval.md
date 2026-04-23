# AI Module — Plan 4b: RAG Retrieval + Chat Injection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire retrieved document chunks into the live chat loop so a `RagScope`-enabled assistant grounds its answers in tenant knowledge-base content and emits inline `[n]` source citations; also expose `POST /ai/search` so operators can probe retrieval quality. Defers query expansion and LLM re-ranking to Plan 4b-2 (flags stay in settings but default `false`).

**Architecture:** One new seam — `IRagRetrievalService` — called from `ChatExecutionService` immediately before the provider request. It does hybrid retrieval (Qdrant semantic + Postgres FTS keyword), hybrid-score merge, parent-chunk union, and token-budget trimming. A `CitationParser` extracts `[n]` markers from the finished assistant text; a `ContextPromptBuilder` renders the system-prompt block. Citations are persisted to a new JSONB column on `AiMessage`. The same retrieval service powers `POST /ai/search`.

**Tech Stack:** .NET 10, EF Core 10 (PostgreSQL via Npgsql), MediatR, Qdrant.Client, SharpToken, FluentValidation, OpenTelemetry. No new NuGet packages.

**Spec:** [docs/superpowers/specs/2026-04-18-ai-module-plan-4b-rag-retrieval-design.md](../specs/2026-04-18-ai-module-plan-4b-rag-retrieval-design.md)

**Plan series position:**
- Plan 1: Foundation + Provider Layer ✅
- Plan 2: Chat + Streaming ✅
- Plan 3: Assistants CRUD + Tool Registry + Function Calling ✅
- Plan 4a: RAG Document Ingestion ✅
- **Plan 4b: RAG Retrieval + Chat Injection ← this plan**
- Plan 4b-2: Query Expansion + LLM Re-Ranking (deferred; hooks exist)
- Plan 5: Agent Engine
- Plan 6: Web frontend — chat sidebar + streaming UI
- Plan 7: Web frontend — admin pages

**Out of scope for Plan 4b (intentional — see spec §"Deferred work"):**
- LLM query expansion (`AI:Rag:EnableQueryExpansion`; flag defaults `false` here).
- LLM re-ranking (`AI:Rag:EnableReranking`; flag defaults `false` here).
- Multi-turn contextual query rewriting.
- Embedding cache, per-document ACLs, quota gating on `/ai/search`, offset pagination.
- Multi-language FTS (`AI:Rag:FtsLanguage` exists but is hard-coded to `english` at usage sites).
- Any FE changes beyond mirroring the new permission constant.
- Migrations in the boilerplate: per repo convention, migrations are generated per test app only. The EF model changes in this plan WILL appear in the test app's generated `InitialCreate`.

---

## File Map

### New files in `Starter.Module.AI`

| File | Purpose |
|---|---|
| `Domain/Enums/AiRagScope.cs` | `{ None, SelectedDocuments, AllTenantDocuments }` |
| `Application/Services/Retrieval/IRagRetrievalService.cs` | `RetrieveForTurnAsync(assistant, userMessage, ct)` |
| `Application/Services/Retrieval/IKeywordSearchService.cs` | `SearchAsync(tenantId, query, docFilter?, limit, ct)` |
| `Application/Services/Retrieval/RetrievedContext.cs` | Result of retrieval: `Children`, `Parents`, `TotalTokens`, `TruncatedByBudget` |
| `Application/Services/Retrieval/RetrievedChunk.cs` | `(ChunkId, DocumentId, DocumentName, Content, SectionTitle?, PageNumber?, ChunkLevel, SemanticScore, KeywordScore, HybridScore, ParentChunkId?)` |
| `Application/Services/Retrieval/KeywordSearchHit.cs` | `(Guid ChunkId, decimal Score)` |
| `Application/Services/Retrieval/VectorSearchHit.cs` | `(Guid ChunkId, decimal Score)` (added to IVectorStore namespace) |
| `Application/DTOs/AiMessageCitation.cs` | Persisted into `AiMessage.Citations` JSONB |
| `Infrastructure/Retrieval/RagRetrievalService.cs` | Pipeline steps 1–9 from spec |
| `Infrastructure/Retrieval/PostgresKeywordSearchService.cs` | `plainto_tsquery` + `ts_rank_cd` via raw SQL |
| `Infrastructure/Retrieval/HybridScoreCalculator.cs` | Min-max normalise + α-blend |
| `Infrastructure/Retrieval/CitationParser.cs` | `\[(\d+(?:\s*,\s*\d+)*)\]` regex + fallback |
| `Infrastructure/Retrieval/ContextPromptBuilder.cs` | System-prompt template renderer |
| `Application/Features/Search/SearchKnowledgeBaseQuery.cs` | `(Query, DocumentIds?, TopK?, MinScore?, IncludeParents)` |
| `Application/Features/Search/SearchKnowledgeBaseQueryHandler.cs` | Calls `IRagRetrievalService` |
| `Application/Features/Search/SearchKnowledgeBaseResultDto.cs` | `Items`, `TotalHits`, `TruncatedByBudget` |
| `Application/Features/Search/SearchKnowledgeBaseResultItemDto.cs` | One item in the result list |
| `Controllers/AiSearchController.cs` | `POST /api/v1/ai/search` |

### New files in `Starter.Api.Tests`

| File | Purpose |
|---|---|
| `Ai/Retrieval/CitationParserTests.cs` | Regex parsing + fallback |
| `Ai/Retrieval/ContextPromptBuilderTests.cs` | Prompt template rendering |
| `Ai/Retrieval/HybridScoreCalculatorTests.cs` | Normalization + blend math |
| `Ai/Retrieval/RagRetrievalServiceTests.cs` | Pipeline orchestration with fakes |
| `Ai/Retrieval/FakeVectorStore.cs` | In-memory `IVectorStore` for tests |
| `Ai/Retrieval/FakeKeywordSearchService.cs` | In-memory `IKeywordSearchService` for tests |
| `Ai/Retrieval/PostgresKeywordSearchServiceTests.cs` | Real-Postgres integration via existing fixture |
| `Ai/Retrieval/AiAssistantRagScopeTests.cs` | Domain invariants for `SetRagScope` |
| `Ai/Retrieval/SearchKnowledgeBaseQueryHandlerTests.cs` | Handler unit test |
| `Ai/Retrieval/AiSearchControllerTests.cs` | Controller + permission gating |
| `Ai/Retrieval/ChatExecutionRagInjectionTests.cs` | End-to-end turn with `FakeAiProvider` |

### Modified files

| File | Change |
|---|---|
| `Domain/Entities/AiAssistant.cs` | Add `RagScope` property + `SetRagScope`. |
| `Domain/Entities/AiMessage.cs` | Add `Citations` property + `CreateAssistantMessage` overload. |
| `Domain/Enums/AiRequestType.cs` | Add `QueryEmbedding`. |
| `Domain/Errors/AiErrors.cs` | Add 3 new errors. |
| `Infrastructure/Configurations/AiAssistantConfiguration.cs` | Configure `RagScope` as int. |
| `Infrastructure/Configurations/AiMessageConfiguration.cs` | Configure `Citations` as JSONB. |
| `Infrastructure/Configurations/AiDocumentChunkConfiguration.cs` | Add `content_tsv` computed tsvector column + GIN index. |
| `Application/Services/Ingestion/IVectorStore.cs` | Add `SearchAsync`. |
| `Infrastructure/Ingestion/QdrantVectorStore.cs` | Implement `SearchAsync`. |
| `Application/Services/Ingestion/IEmbeddingService.cs` | Add `requestType` param on `EmbedAsync`. |
| `Infrastructure/Ingestion/EmbeddingService.cs` | Forward `requestType` to usage log. |
| `Application/DTOs/AiAssistantDto.cs` | Include `RagScope`. |
| `Application/DTOs/AiAssistantMappers.cs` | Map `RagScope`. |
| `Application/Commands/AssistantInputRules.cs` | Add `RagScope` to rules interface. |
| `Application/Commands/CreateAssistant/CreateAssistantCommand.cs` | Accept `RagScope`. |
| `Application/Commands/CreateAssistant/CreateAssistantCommandHandler.cs` | Pass to domain. |
| `Application/Commands/CreateAssistant/CreateAssistantCommandValidator.cs` | Validate `RagScope`. |
| `Application/Commands/UpdateAssistant/UpdateAssistantCommand.cs` | Accept `RagScope`. |
| `Application/Commands/UpdateAssistant/UpdateAssistantCommandHandler.cs` | Call `SetRagScope`. |
| `Application/Commands/UpdateAssistant/UpdateAssistantCommandValidator.cs` | Validate `RagScope`. |
| `Application/Services/ChatExecutionService.cs` | Call retrieval, build prompt, emit `citations`, persist citations. |
| `Constants/AiPermissions.cs` | Add `SearchKnowledgeBase`. |
| `AIModule.cs` | Register retrieval services, add permission to `GetPermissions()` + role mappings. |
| `Infrastructure/Settings/AiRagSettings.cs` | Add 4 new keys. |
| `Starter.Api/appsettings.Development.json` | Add new `AI:Rag` keys, flip QE+Reranker flags, add `*/ai/search` rate-limit rule. |
| `boilerplateFE/src/constants/permissions.ts` | Mirror `Ai.SearchKnowledgeBase`. |

---

## Task 1: Expand `AiRagSettings` + update appsettings

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs`
- Modify: `boilerplateBE/src/Starter.Api/appsettings.Development.json`

- [ ] **Step 1: Add new settings keys to `AiRagSettings`**

Append to the `AiRagSettings` class (full file replace is fine; preserve existing keys):

```csharp
namespace Starter.Module.AI.Infrastructure.Settings;

public sealed class AiRagSettings
{
    public const string SectionName = "AI:Rag";

    // From Plan 4a
    public int ChunkSize { get; init; } = 512;
    public int ChunkOverlap { get; init; } = 50;
    public int ParentChunkSize { get; init; } = 1536;
    public int TopK { get; init; } = 5;
    public int RetrievalTopK { get; init; } = 20;
    public double HybridSearchWeight { get; init; } = 0.7;
    public bool EnableQueryExpansion { get; init; } = false;  // flipped 4a → 4b: wired in 4b-2
    public bool EnableReranking { get; init; } = false;       // flipped 4a → 4b: wired in 4b-2
    public int EmbedBatchSize { get; init; } = 32;
    public long MaxUploadBytes { get; init; } = 25 * 1024 * 1024;
    public double OcrFallbackMinCharsPerPage { get; init; } = 40;
    public double PageFailureThreshold { get; init; } = 0.25;

    // New in Plan 4b
    public int MaxContextTokens { get; init; } = 4000;
    public bool IncludeParentContext { get; init; } = true;
    public decimal MinHybridScore { get; init; } = 0.0m;
    public string FtsLanguage { get; init; } = "english";
}
```

- [ ] **Step 2: Update `appsettings.Development.json` under `AI:Rag`**

Find the existing `"Rag": { ... }` block (added in Plan 4a) and set the key set to match. Only modify `AI:Rag`; leave the rest of the file alone.

```json
"Rag": {
  "ChunkSize": 512,
  "ChunkOverlap": 50,
  "ParentChunkSize": 1536,
  "TopK": 5,
  "RetrievalTopK": 20,
  "HybridSearchWeight": 0.7,
  "EnableQueryExpansion": false,
  "EnableReranking": false,
  "EmbedBatchSize": 32,
  "MaxUploadBytes": 26214400,
  "OcrFallbackMinCharsPerPage": 40,
  "PageFailureThreshold": 0.25,
  "MaxContextTokens": 4000,
  "IncludeParentContext": true,
  "MinHybridScore": 0.0,
  "FtsLanguage": "english"
}
```

- [ ] **Step 3: Add rate-limit rule for `/ai/search`**

In the same file, find `"IpRateLimiting": { "GeneralRules": [ ... ] }` and append a new rule to the array:

```json
{
  "Endpoint": "*/ai/search",
  "Period": "1m",
  "Limit": 30
}
```

- [ ] **Step 4: Build**

Run: `cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs boilerplateBE/src/Starter.Api/appsettings.Development.json
git commit -m "feat(ai): AiRagSettings keys + rate-limit rule for 4b retrieval"
```

---

## Task 2: Add error codes to `AiErrors`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/AiErrors.cs`

- [ ] **Step 1: Append three new errors**

Inside the `AiErrors` static class, append:

```csharp
public static readonly Error SearchQueryRequired =
    new("Ai.SearchQueryRequired", "Search query is required.");

public static Error SearchTopKOutOfRange(int max) =>
    new("Ai.SearchTopKOutOfRange", $"topK must be between 1 and {max}.");

public static readonly Error RagScopeRequiresDocuments =
    new("Ai.RagScopeRequiresDocuments",
        "RagScope SelectedDocuments requires at least one knowledge base document id.");
```

- [ ] **Step 2: Build + commit**

```bash
cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj
git add src/modules/Starter.Module.AI/Domain/Errors/AiErrors.cs
git commit -m "feat(ai): error codes for 4b search + rag scope validation"
```

---

## Task 3: Add `QueryEmbedding` to `AiRequestType`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/AiRequestType.cs`

- [ ] **Step 1: Append the new enum value**

Replace the single-line enum with the extended form (keep existing members for EF compatibility):

```csharp
namespace Starter.Module.AI.Domain.Enums;
public enum AiRequestType { Chat, Completion, Embedding, AgentStep, QueryEmbedding }
```

- [ ] **Step 2: Build**

Run: `cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/modules/Starter.Module.AI/Domain/Enums/AiRequestType.cs
git commit -m "feat(ai): AiRequestType.QueryEmbedding for retrieval usage logs"
```

---

## Task 4: Add `Ai.SearchKnowledgeBase` permission

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`
- Modify: `boilerplateFE/src/constants/permissions.ts`

- [ ] **Step 1: Add constant to `AiPermissions.cs`**

Append one line after `ManageSettings`:

```csharp
public const string SearchKnowledgeBase = "Ai.SearchKnowledgeBase";
```

- [ ] **Step 2: Register permission + role mappings in `AIModule.cs`**

In `GetPermissions()`, append:

```csharp
yield return (AiPermissions.SearchKnowledgeBase, "Search knowledge base content directly", "AI");
```

In `GetDefaultRolePermissions()`, add the permission to the **SuperAdmin** and **Admin** role arrays (not to User). Locate the `SuperAdmin` yield and append `AiPermissions.SearchKnowledgeBase`; do the same for `Admin`. Do not add to `User`.

- [ ] **Step 3: Mirror to frontend permissions**

In `boilerplateFE/src/constants/permissions.ts`, locate the `Ai` block (section for AI permissions — follow the existing shape for other modules; look for keys like `Ai.Chat` or search by `ManageDocuments`). Add:

```ts
SearchKnowledgeBase: 'Ai.SearchKnowledgeBase',
```

If the `Ai` group doesn't exist yet in `permissions.ts` (because prior AI plans didn't touch FE), add the group following the pattern of neighbouring groups. Only add the keys actually referenced by the repo — minimum `SearchKnowledgeBase`.

- [ ] **Step 4: Build backend**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeded.

- [ ] **Step 5: Build frontend (typecheck only)**

Run: `cd boilerplateFE && npm run build`
Expected: Build succeeded, no TS errors.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs boilerplateFE/src/constants/permissions.ts
git commit -m "feat(ai): Ai.SearchKnowledgeBase permission (BE + FE mirror)"
```

---

## Task 5: Create `AiRagScope` enum

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/AiRagScope.cs`

- [ ] **Step 1: Write the enum**

```csharp
namespace Starter.Module.AI.Domain.Enums;

public enum AiRagScope
{
    None = 0,
    SelectedDocuments = 1,
    AllTenantDocuments = 2
}
```

- [ ] **Step 2: Build + commit**

```bash
cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj
git add src/modules/Starter.Module.AI/Domain/Enums/AiRagScope.cs
git commit -m "feat(ai): AiRagScope enum"
```

---

## Task 6: Add `RagScope` + `SetRagScope` to `AiAssistant` (TDD)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/AiAssistantRagScopeTests.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAssistant.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class AiAssistantRagScopeTests
{
    [Fact]
    public void SetRagScope_None_IsDefault()
    {
        var assistant = AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "A",
            description: null,
            systemPrompt: "You are helpful.");

        assistant.RagScope.Should().Be(AiRagScope.None);
    }

    [Fact]
    public void SetRagScope_AllTenantDocuments_Allowed_Without_DocIds()
    {
        var a = AiAssistant.Create(Guid.NewGuid(), "A", null, "p");

        var act = () => a.SetRagScope(AiRagScope.AllTenantDocuments);

        act.Should().NotThrow();
        a.RagScope.Should().Be(AiRagScope.AllTenantDocuments);
    }

    [Fact]
    public void SetRagScope_SelectedDocuments_Requires_DocIds()
    {
        var a = AiAssistant.Create(Guid.NewGuid(), "A", null, "p");

        var act = () => a.SetRagScope(AiRagScope.SelectedDocuments);

        act.Should().Throw<DomainException>()
            .Which.Error.Code.Should().Be(AiErrors.RagScopeRequiresDocuments.Code);
    }

    [Fact]
    public void SetRagScope_SelectedDocuments_Passes_When_DocIds_Present()
    {
        var a = AiAssistant.Create(Guid.NewGuid(), "A", null, "p");
        a.SetKnowledgeBase([Guid.NewGuid()]);

        var act = () => a.SetRagScope(AiRagScope.SelectedDocuments);

        act.Should().NotThrow();
        a.RagScope.Should().Be(AiRagScope.SelectedDocuments);
    }
}
```

Note: if `DomainException` / `AggregateRoot` don't throw the exact exception type above, adjust by first reading `boilerplateBE/src/Starter.Domain/Common/` to find the project's domain-error convention (look for `Result<T>` patterns vs. exception throwing in neighbouring entities like `AiDocument`). Use the existing convention — do not invent a new one.

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiAssistantRagScopeTests`
Expected: 4 tests fail with "RagScope not a member of AiAssistant" (compile error is OK; counts as failing).

- [ ] **Step 3: Add property + method to `AiAssistant`**

In `AiAssistant.cs`:

Add using at top if not present:
```csharp
using Starter.Module.AI.Domain.Errors;
using Starter.Domain.Common;  // for DomainException/error types
```

Add property after `IsActive`:
```csharp
public AiRagScope RagScope { get; private set; } = AiRagScope.None;
```

Add method near `SetKnowledgeBase`:
```csharp
public void SetRagScope(AiRagScope scope)
{
    if (scope == AiRagScope.SelectedDocuments && _knowledgeBaseDocIds.Count == 0)
        throw new DomainException(AiErrors.RagScopeRequiresDocuments);

    RagScope = scope;
    ModifiedAt = DateTime.UtcNow;
}
```

If the codebase throws `DomainException` differently (e.g. via a factory), match the neighbouring convention. If there is no `DomainException` class at all, look at how `AiDocument.MarkFailed` or similar surfaces errors — use the same mechanism.

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiAssistantRagScopeTests`
Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add src/modules/Starter.Module.AI/Domain/Entities/AiAssistant.cs tests/Starter.Api.Tests/Ai/Retrieval/AiAssistantRagScopeTests.cs
git commit -m "feat(ai): AiAssistant.RagScope domain property + invariants"
```

---

## Task 7: Configure `RagScope` in EF config

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiAssistantConfiguration.cs`

- [ ] **Step 1: Add property mapping**

Inside `Configure()`, near the existing `ExecutionMode` mapping, add:

```csharp
builder.Property(e => e.RagScope)
    .HasColumnName("rag_scope")
    .HasConversion<int>()
    .IsRequired();
```

- [ ] **Step 2: Build**

Run: `cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/modules/Starter.Module.AI/Infrastructure/Configurations/AiAssistantConfiguration.cs
git commit -m "feat(ai): EF config for AiAssistant.RagScope"
```

---

## Task 8: Update `AiAssistantDto` + mapper

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAssistantDto.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAssistantMappers.cs`

- [ ] **Step 1: Add `RagScope` property to DTO**

In the `AiAssistantDto` record definition, add a `AiRagScope RagScope` property positioned near other enum-typed fields. Keep its position stable (append to the end of the record argument list to minimise downstream breakage).

- [ ] **Step 2: Map `RagScope` in mapper**

In the mapper method `ToDto()` (or whatever the canonical name is — inspect the file), add `a.RagScope` to the mapping wherever other scalar properties are being set.

- [ ] **Step 3: Build**

Run: `cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/modules/Starter.Module.AI/Application/DTOs/AiAssistantDto.cs src/modules/Starter.Module.AI/Application/DTOs/AiAssistantMappers.cs
git commit -m "feat(ai): AiAssistantDto includes RagScope"
```

---

## Task 9: Update Create/Update assistant commands

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/AssistantInputRules.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/CreateAssistant/CreateAssistantCommand.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/CreateAssistant/CreateAssistantCommandHandler.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/CreateAssistant/CreateAssistantCommandValidator.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/UpdateAssistant/UpdateAssistantCommand.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/UpdateAssistant/UpdateAssistantCommandHandler.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/UpdateAssistant/UpdateAssistantCommandValidator.cs`

- [ ] **Step 1: Add `RagScope` to `AssistantInputRules` interface**

Append to the interface properties:

```csharp
AiRagScope RagScope { get; }
```

- [ ] **Step 2: Add `RagScope` to Create and Update commands**

In both `CreateAssistantCommand` and `UpdateAssistantCommand` records, add the new property (append to record argument list). Default to `AiRagScope.None`:

```csharp
AiRagScope RagScope = AiRagScope.None
```

- [ ] **Step 3: Invoke `SetRagScope` from both handlers**

In `CreateAssistantCommandHandler`, after the existing `SetKnowledgeBase` call:

```csharp
if (request.RagScope != AiRagScope.None)
    assistant.SetRagScope(request.RagScope);
```

In `UpdateAssistantCommandHandler`, after the existing `SetKnowledgeBase` call, always invoke:

```csharp
assistant.SetRagScope(request.RagScope);
```

(Update always calls it so the operator can flip back to `None`.)

- [ ] **Step 4: Add validator rule to both validators**

In both `CreateAssistantCommandValidator` and `UpdateAssistantCommandValidator`:

```csharp
RuleFor(x => x)
    .Must(x => x.RagScope != AiRagScope.SelectedDocuments
        || (x.KnowledgeBaseDocIds is not null && x.KnowledgeBaseDocIds.Count > 0))
    .WithMessage(AiErrors.RagScopeRequiresDocuments.Message)
    .WithErrorCode(AiErrors.RagScopeRequiresDocuments.Code);
```

- [ ] **Step 5: Build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/modules/Starter.Module.AI/Application/Commands
git commit -m "feat(ai): RagScope on Create/Update assistant commands + validation"
```

---

## Task 10: Create `AiMessageCitation` record

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiMessageCitation.cs`

- [ ] **Step 1: Write the record**

```csharp
namespace Starter.Module.AI.Application.DTOs;

public sealed record AiMessageCitation(
    int Marker,
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    string? SectionTitle,
    int? PageNumber,
    decimal Score);
```

- [ ] **Step 2: Build + commit**

```bash
cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj
git add src/modules/Starter.Module.AI/Application/DTOs/AiMessageCitation.cs
git commit -m "feat(ai): AiMessageCitation record"
```

---

## Task 11: Add `Citations` to `AiMessage` + factory param

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiMessage.cs`

- [ ] **Step 1: Add backing field + property**

Near the top of the `AiMessage` class, next to other fields:

```csharp
private List<AiMessageCitation> _citations = new();

public IReadOnlyList<AiMessageCitation> Citations
{
    get => _citations;
    private set => _citations = value?.ToList() ?? new();
}
```

Import at top: `using Starter.Module.AI.Application.DTOs;`. If a cycle arises (Domain referencing Application), move the record to `Domain/ValueObjects/AiMessageCitation.cs` instead — the record is pure data and has no Application-layer dependencies. Pick whichever keeps the Domain project free of Application references. (Inspect `Starter.Module.AI.Domain.csproj` for existing references before deciding.)

- [ ] **Step 2: Add new assistant-message factory overload**

Add a second factory method:

```csharp
public static AiMessage CreateAssistantMessageWithCitations(
    Guid conversationId,
    string? content,
    int order,
    IReadOnlyList<AiMessageCitation> citations,
    int inputTokens = 0,
    int outputTokens = 0,
    string? toolCalls = null)
{
    var msg = CreateAssistantMessage(conversationId, content, order, inputTokens, outputTokens, toolCalls);
    msg._citations = citations?.ToList() ?? new();
    return msg;
}
```

Keep the original `CreateAssistantMessage` signature unchanged so existing callers (tool-calling turns, etc.) don't break.

- [ ] **Step 3: Build**

Run: `cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/modules/Starter.Module.AI/Domain/Entities/AiMessage.cs
git commit -m "feat(ai): AiMessage.Citations + assistant-message factory overload"
```

---

## Task 12: EF config for `AiMessage.Citations` (JSONB)

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiMessageConfiguration.cs`

- [ ] **Step 1: Configure JSONB column**

Follow the pattern used by `AiAssistantConfiguration` for `KnowledgeBaseDocIds` (read that file first to copy the exact JSON-serialisation approach — typically either `HasConversion` with a custom `ValueConverter` into `string`, or `HasColumnType("jsonb")` with `HasConversion` on a System.Text.Json serializer).

Add inside `Configure()`:

```csharp
builder.Property(e => e.Citations)
    .HasColumnName("citations")
    .HasColumnType("jsonb")
    .HasConversion(
        v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
        v => System.Text.Json.JsonSerializer.Deserialize<List<Starter.Module.AI.Application.DTOs.AiMessageCitation>>(
                v, (System.Text.Json.JsonSerializerOptions?)null) ?? new(),
        new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlyList<Starter.Module.AI.Application.DTOs.AiMessageCitation>>(
            (a, b) => (a ?? new List<Starter.Module.AI.Application.DTOs.AiMessageCitation>())
                .SequenceEqual(b ?? new List<Starter.Module.AI.Application.DTOs.AiMessageCitation>()),
            c => c.Aggregate(0, (hc, x) => HashCode.Combine(hc, x.GetHashCode())),
            c => c.ToList()));
```

If `AiAssistantConfiguration.KnowledgeBaseDocIds` uses a helper method or extension (e.g. `ConfigureJsonColumn<T>`), reuse that instead of hand-rolling the serializer. Match the existing pattern exactly.

- [ ] **Step 2: Build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/modules/Starter.Module.AI/Infrastructure/Configurations/AiMessageConfiguration.cs
git commit -m "feat(ai): AiMessage.Citations JSONB EF configuration"
```

---

## Task 13: Add pg FTS generated column to `AiDocumentChunk` config

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiDocumentChunkConfiguration.cs`

- [ ] **Step 1: Add shadow tsvector property + GIN index**

Inside `Configure()`, append after the existing index block:

```csharp
builder.Property<NpgsqlTypes.NpgsqlTsVector>("ContentTsVector")
    .HasColumnName("content_tsv")
    .HasComputedColumnSql("to_tsvector('english', content)", stored: true);

builder.HasIndex("ContentTsVector")
    .HasDatabaseName("ix_ai_document_chunks_content_tsv")
    .HasMethod("GIN");
```

Add usings at top if missing:
```csharp
using NpgsqlTypes;
```

- [ ] **Step 2: Build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/modules/Starter.Module.AI/Infrastructure/Configurations/AiDocumentChunkConfiguration.cs
git commit -m "feat(ai): pg FTS tsvector generated column + GIN index on ai_document_chunks"
```

Note: no migration is committed to the boilerplate per repo convention. The test app's generated `InitialCreate` will emit the column and index automatically because EF reads the computed-column SQL from the model.

---

## Task 14: Add `SearchAsync` to `IVectorStore` + supporting types

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/VectorSearchHit.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/IVectorStore.cs`

- [ ] **Step 1: Create the hit record**

```csharp
namespace Starter.Module.AI.Application.Services.Retrieval;

public sealed record VectorSearchHit(Guid ChunkId, decimal Score);
```

- [ ] **Step 2: Extend `IVectorStore`**

Add using at the top of `IVectorStore.cs`:
```csharp
using Starter.Module.AI.Application.Services.Retrieval;
```

Add the new method inside the interface:

```csharp
Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
    Guid tenantId,
    float[] queryVector,
    IReadOnlyCollection<Guid>? documentFilter,
    int limit,
    CancellationToken ct);
```

Semantics:
- Filter `tenant_id == tenantId AND chunk_level == "child"` always.
- When `documentFilter != null`, additionally filter `document_id IN documentFilter`.
- Return hits ordered by descending cosine similarity, capped at `limit`.
- Empty collection → empty list, not null.

- [ ] **Step 3: Build (expect failure — QdrantVectorStore doesn't implement yet)**

Run: `cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj`
Expected: FAIL — `QdrantVectorStore` does not implement `SearchAsync`.

- [ ] **Step 4: Do not commit yet** — next task implements the method.

---

## Task 15: Implement `SearchAsync` on `QdrantVectorStore`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/QdrantVectorStore.cs`

- [ ] **Step 1: Read the existing Qdrant usage in the file**

Read `QdrantVectorStore.cs` top-to-bottom. Note:
- How `UpsertAsync` builds its `PointStruct` and filter keys for `document_id` / `tenant_id` / `chunk_level`.
- The collection name convention (`tenant_{tenantId}`).
- The Qdrant.Client types used (likely `Qdrant.Client.Grpc.Filter`, `Match`, etc.).

Match the existing style exactly. Do not introduce new patterns.

- [ ] **Step 2: Implement `SearchAsync`**

Append to the class:

```csharp
public async Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
    Guid tenantId,
    float[] queryVector,
    IReadOnlyCollection<Guid>? documentFilter,
    int limit,
    CancellationToken ct)
{
    var collection = $"tenant_{tenantId:N}";

    var filterConditions = new List<Qdrant.Client.Grpc.Condition>
    {
        new() { Field = new() { Key = "chunk_level", Match = new() { Keyword = "child" } } }
    };

    if (documentFilter is { Count: > 0 })
    {
        var docFilter = new Qdrant.Client.Grpc.Condition
        {
            Field = new()
            {
                Key = "document_id",
                Match = new() { Keywords = { documentFilter.Select(d => d.ToString("D")) } }
            }
        };
        filterConditions.Add(docFilter);
    }

    var filter = new Qdrant.Client.Grpc.Filter { Must = { filterConditions } };

    var results = await _client.SearchAsync(
        collectionName: collection,
        vector: queryVector,
        filter: filter,
        limit: (ulong)limit,
        cancellationToken: ct);

    return results
        .Select(hit => new VectorSearchHit(
            ChunkId: Guid.Parse(hit.Payload["chunk_id"].StringValue),
            Score: (decimal)hit.Score))
        .ToList();
}
```

If the existing `UpsertAsync` stores `chunk_id` in the payload, use that key. If it stores `chunk_level` under a different payload key (e.g. `level`), adjust accordingly. If the payload doesn't currently contain `chunk_id`, fall back to using `hit.Id.Uuid` (Qdrant point id equals the chunk id by Plan 4a's convention) — check `UpsertAsync` to confirm.

- [ ] **Step 3: Build**

Run: `cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/modules/Starter.Module.AI/Application/Services/Retrieval/VectorSearchHit.cs src/modules/Starter.Module.AI/Application/Services/Ingestion/IVectorStore.cs src/modules/Starter.Module.AI/Infrastructure/Ingestion/QdrantVectorStore.cs
git commit -m "feat(ai): IVectorStore.SearchAsync + Qdrant implementation"
```

---

## Task 16: Create `IKeywordSearchService` + `KeywordSearchHit`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IKeywordSearchService.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/KeywordSearchHit.cs`

- [ ] **Step 1: Write `KeywordSearchHit`**

```csharp
namespace Starter.Module.AI.Application.Services.Retrieval;

public sealed record KeywordSearchHit(Guid ChunkId, decimal Score);
```

- [ ] **Step 2: Write `IKeywordSearchService`**

```csharp
namespace Starter.Module.AI.Application.Services.Retrieval;

public interface IKeywordSearchService
{
    Task<IReadOnlyList<KeywordSearchHit>> SearchAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? documentFilter,
        int limit,
        CancellationToken ct);
}
```

Semantics: Postgres FTS with `plainto_tsquery('english', @q)` against the `content_tsv` column (Task 13). Scoped to `tenant_id == tenantId AND chunk_level == 'child' [AND document_id IN documentFilter]`. Scores are `ts_rank_cd(content_tsv, query)` cast to decimal. Ordered by score descending, capped at `limit`.

- [ ] **Step 3: Build + commit**

```bash
cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj
git add src/modules/Starter.Module.AI/Application/Services/Retrieval
git commit -m "feat(ai): IKeywordSearchService contract"
```

---

## Task 17: Implement `PostgresKeywordSearchService` (TDD)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/PostgresKeywordSearchServiceTests.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/PostgresKeywordSearchService.cs`

- [ ] **Step 1: Write the failing integration test**

Before writing the test, read `boilerplateBE/tests/Starter.Api.Tests/` for existing integration-test fixtures. If there is a Testcontainers-based Postgres fixture (search for `PostgreSqlContainer` or `DatabaseFixture`), reuse it via `IClassFixture<T>`. If not, the test uses `Testcontainers.PostgreSql` directly. Match existing convention.

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class PostgresKeywordSearchServiceTests : IClassFixture<ExistingPostgresFixture>  // name TBD by existing fixture
{
    private readonly ExistingPostgresFixture _fx;

    public PostgresKeywordSearchServiceTests(ExistingPostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task SearchAsync_Returns_ChunksMatchingQueryTerms_ScopedToTenant()
    {
        using var db = _fx.CreateDbContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var docA = Guid.NewGuid();

        db.AiDocumentChunks.AddRange(
            MakeChild(tenantA, docA, "photosynthesis light reactions"),
            MakeChild(tenantA, docA, "mitosis prophase metaphase"),
            MakeChild(tenantB, docA, "photosynthesis light reactions"));  // different tenant
        await db.SaveChangesAsync();

        var svc = new PostgresKeywordSearchService(db, _fx.Logger<PostgresKeywordSearchService>());
        var hits = await svc.SearchAsync(tenantA, "photosynthesis", null, 10, CancellationToken.None);

        hits.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchAsync_Honours_DocumentFilter()
    {
        using var db = _fx.CreateDbContext();
        var tenantId = Guid.NewGuid();
        var docX = Guid.NewGuid();
        var docY = Guid.NewGuid();

        db.AiDocumentChunks.AddRange(
            MakeChild(tenantId, docX, "photosynthesis"),
            MakeChild(tenantId, docY, "photosynthesis"));
        await db.SaveChangesAsync();

        var svc = new PostgresKeywordSearchService(db, _fx.Logger<PostgresKeywordSearchService>());
        var hits = await svc.SearchAsync(tenantId, "photosynthesis", new[] { docX }, 10, CancellationToken.None);

        hits.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmpty()
    {
        using var db = _fx.CreateDbContext();
        var svc = new PostgresKeywordSearchService(db, _fx.Logger<PostgresKeywordSearchService>());

        var hits = await svc.SearchAsync(Guid.NewGuid(), "", null, 10, CancellationToken.None);

        hits.Should().BeEmpty();
    }

    private static AiDocumentChunk MakeChild(Guid tenantId, Guid documentId, string content) =>
        // Use the real Plan 4a factory; adjust args as needed.
        AiDocumentChunk.CreateChild(
            documentId: documentId,
            parentChunkId: null,
            content: content,
            chunkIndex: 0,
            sectionTitle: null,
            pageNumber: null,
            tokenCount: content.Split(' ').Length,
            qdrantPointId: Guid.NewGuid());
}
```

Note: `ExistingPostgresFixture` is a placeholder — replace with the actual fixture discovered in step 1. The `MakeChild` helper assumes `AiDocumentChunk.CreateChild` exists (Plan 4a factory). Read [boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiDocumentChunk.cs](../../boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiDocumentChunk.cs) and adjust to the real signature.

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~PostgresKeywordSearchServiceTests`
Expected: All 3 tests FAIL with "PostgresKeywordSearchService not found" (compile error).

- [ ] **Step 3: Implement `PostgresKeywordSearchService`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Retrieval;

internal sealed class PostgresKeywordSearchService : IKeywordSearchService
{
    private readonly AiDbContext _db;
    private readonly ILogger<PostgresKeywordSearchService> _logger;

    public PostgresKeywordSearchService(AiDbContext db, ILogger<PostgresKeywordSearchService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<KeywordSearchHit>> SearchAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? documentFilter,
        int limit,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(queryText))
            return [];

        var sql = @"
            SELECT c.id AS ChunkId,
                   ts_rank_cd(c.content_tsv, plainto_tsquery('english', {0}))::numeric AS Score
            FROM ai_document_chunks c
            INNER JOIN ai_documents d ON d.id = c.document_id
            WHERE d.tenant_id = {1}
              AND c.chunk_level = 'Child'
              AND c.content_tsv @@ plainto_tsquery('english', {0})
        ";

        var parameters = new List<object> { queryText, tenantId };
        if (documentFilter is { Count: > 0 })
        {
            sql += $" AND c.document_id = ANY({{{parameters.Count}}})";
            parameters.Add(documentFilter.ToArray());
        }

        sql += $" ORDER BY Score DESC LIMIT {{{parameters.Count}}}";
        parameters.Add(limit);

        var hits = await _db.Database
            .SqlQueryRaw<KeywordSearchHitRow>(sql, parameters.ToArray())
            .ToListAsync(ct);

        return hits.Select(h => new KeywordSearchHit(h.ChunkId, h.Score)).ToList();
    }

    private sealed record KeywordSearchHitRow(Guid ChunkId, decimal Score);
}
```

Confirm via the existing code:
- `AiDocumentChunk.ChunkLevel` stored values are `"Child"` / `"Parent"` (title case based on Plan 4a's EF config `HasMaxLength(10)` + `Enum.ToString()`). If the DB stores them lowercase, adjust `'Child'` accordingly.
- `AiDocuments` table is where `TenantId` lives — verify by reading `AiDocumentConfiguration`. If `TenantId` is instead on `AiDocumentChunk`, adjust the join.

- [ ] **Step 4: Run tests to verify pass**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~PostgresKeywordSearchServiceTests`
Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add src/modules/Starter.Module.AI/Infrastructure/Retrieval/PostgresKeywordSearchService.cs tests/Starter.Api.Tests/Ai/Retrieval/PostgresKeywordSearchServiceTests.cs
git commit -m "feat(ai): PostgresKeywordSearchService via ts_rank_cd + FTS integration tests"
```

---

## Task 18: `HybridScoreCalculator` (TDD)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/HybridScoreCalculatorTests.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/HybridScoreCalculator.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using FluentAssertions;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class HybridScoreCalculatorTests
{
    [Fact]
    public void Combine_Blends_With_Alpha()
    {
        var semantic = new List<VectorSearchHit>
        {
            new(Guid.Parse("00000000-0000-0000-0000-000000000001"), 0.9m),
            new(Guid.Parse("00000000-0000-0000-0000-000000000002"), 0.1m)
        };
        var keyword = new List<KeywordSearchHit>
        {
            new(Guid.Parse("00000000-0000-0000-0000-000000000001"), 0.5m),
            new(Guid.Parse("00000000-0000-0000-0000-000000000003"), 1.5m)
        };

        var merged = HybridScoreCalculator.Combine(semantic, keyword, alpha: 0.7m, minScore: 0m);

        // After min-max normalisation:
        //   semantic: {1: (0.9-0.1)/(0.9-0.1)=1.0, 2: 0.0}
        //   keyword:  {1: (0.5-0.5)/(1.5-0.5)=0.0, 3: 1.0}
        // Hybrid: 0.7 * sem + 0.3 * kw
        //   chunk 1: 0.7*1.0 + 0.3*0.0 = 0.70
        //   chunk 2: 0.7*0.0 + 0.3*0.0 = 0.00
        //   chunk 3: 0.7*0.0 + 0.3*1.0 = 0.30
        merged.Select(m => m.ChunkId).Should().ContainInOrder(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Guid.Parse("00000000-0000-0000-0000-000000000002"));
        merged[0].HybridScore.Should().BeApproximately(0.70m, 0.001m);
        merged[1].HybridScore.Should().BeApproximately(0.30m, 0.001m);
        merged[2].HybridScore.Should().BeApproximately(0.00m, 0.001m);
    }

    [Fact]
    public void Combine_Filters_By_MinScore()
    {
        var semantic = new List<VectorSearchHit> { new(Guid.NewGuid(), 0.2m) };
        var keyword = new List<KeywordSearchHit>();

        var merged = HybridScoreCalculator.Combine(semantic, keyword, alpha: 0.7m, minScore: 0.5m);

        merged.Should().BeEmpty();
    }

    [Fact]
    public void Combine_Handles_Empty_Inputs()
    {
        var merged = HybridScoreCalculator.Combine(
            new List<VectorSearchHit>(),
            new List<KeywordSearchHit>(),
            alpha: 0.7m,
            minScore: 0m);

        merged.Should().BeEmpty();
    }

    [Fact]
    public void Combine_Single_Hit_On_Each_Side_Normalises_To_One()
    {
        // With a single result on a side, min==max, so we define normalised score as 1.0 (present).
        var semantic = new List<VectorSearchHit> { new(Guid.Parse("00000000-0000-0000-0000-000000000001"), 0.5m) };
        var keyword = new List<KeywordSearchHit> { new(Guid.Parse("00000000-0000-0000-0000-000000000001"), 0.5m) };

        var merged = HybridScoreCalculator.Combine(semantic, keyword, alpha: 0.7m, minScore: 0m);

        merged.Should().HaveCount(1);
        merged[0].HybridScore.Should().BeApproximately(1.0m, 0.001m);
    }
}
```

- [ ] **Step 2: Run to verify fail**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~HybridScoreCalculatorTests`
Expected: FAIL (compile — `HybridScoreCalculator` undefined).

- [ ] **Step 3: Implement**

```csharp
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval;

public sealed record HybridHit(Guid ChunkId, decimal SemanticScore, decimal KeywordScore, decimal HybridScore);

internal static class HybridScoreCalculator
{
    public static IReadOnlyList<HybridHit> Combine(
        IReadOnlyList<VectorSearchHit> semantic,
        IReadOnlyList<KeywordSearchHit> keyword,
        decimal alpha,
        decimal minScore)
    {
        var semMap = semantic.ToDictionary(h => h.ChunkId, h => h.Score);
        var kwMap = keyword.ToDictionary(h => h.ChunkId, h => h.Score);

        var semNorm = Normalise(semMap);
        var kwNorm = Normalise(kwMap);

        var allIds = new HashSet<Guid>(semMap.Keys);
        foreach (var id in kwMap.Keys) allIds.Add(id);

        var merged = allIds
            .Select(id =>
            {
                var sNorm = semNorm.GetValueOrDefault(id, 0m);
                var kNorm = kwNorm.GetValueOrDefault(id, 0m);
                var hybrid = alpha * sNorm + (1m - alpha) * kNorm;
                var sRaw = semMap.GetValueOrDefault(id, 0m);
                var kRaw = kwMap.GetValueOrDefault(id, 0m);
                return new HybridHit(id, sRaw, kRaw, hybrid);
            })
            .Where(h => h.HybridScore >= minScore)
            .OrderByDescending(h => h.HybridScore)
            .ThenBy(h => h.ChunkId)
            .ToList();

        return merged;
    }

    private static Dictionary<Guid, decimal> Normalise(Dictionary<Guid, decimal> raw)
    {
        if (raw.Count == 0) return new();
        var min = raw.Values.Min();
        var max = raw.Values.Max();
        if (max == min) return raw.ToDictionary(kv => kv.Key, _ => 1.0m);
        var range = max - min;
        return raw.ToDictionary(kv => kv.Key, kv => (kv.Value - min) / range);
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~HybridScoreCalculatorTests`
Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add src/modules/Starter.Module.AI/Infrastructure/Retrieval/HybridScoreCalculator.cs tests/Starter.Api.Tests/Ai/Retrieval/HybridScoreCalculatorTests.cs
git commit -m "feat(ai): HybridScoreCalculator + unit tests"
```

---

## Task 19: `RetrievedChunk` + `RetrievedContext` records

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RetrievedChunk.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RetrievedContext.cs`

- [ ] **Step 1: Write `RetrievedChunk`**

```csharp
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Retrieval;

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

Note: `ChunkLevel` is a Plan 4a enum; verify by reading `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/` and matching the exact type name used. If it's a string instead of an enum, switch the type accordingly.

- [ ] **Step 2: Write `RetrievedContext`**

```csharp
namespace Starter.Module.AI.Application.Services.Retrieval;

public sealed record RetrievedContext(
    IReadOnlyList<RetrievedChunk> Children,
    IReadOnlyList<RetrievedChunk> Parents,
    int TotalTokens,
    bool TruncatedByBudget)
{
    public static RetrievedContext Empty { get; } = new([], [], 0, false);
    public bool IsEmpty => Children.Count == 0;
}
```

- [ ] **Step 3: Build + commit**

```bash
cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj
git add src/modules/Starter.Module.AI/Application/Services/Retrieval
git commit -m "feat(ai): RetrievedChunk + RetrievedContext records"
```

---

## Task 20: `IRagRetrievalService` contract

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/IRagRetrievalService.cs`

- [ ] **Step 1: Write the contract**

```csharp
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.Services.Retrieval;

public interface IRagRetrievalService
{
    /// <summary>
    /// Retrieves hybrid-ranked chunks + their parents for the given user message,
    /// scoped by the assistant's RagScope. Caller MUST ensure RagScope != None.
    /// </summary>
    Task<RetrievedContext> RetrieveForTurnAsync(
        AiAssistant assistant,
        string latestUserMessage,
        CancellationToken ct);

    /// <summary>
    /// Ad-hoc retrieval for the /ai/search endpoint; not tied to an assistant.
    /// </summary>
    Task<RetrievedContext> RetrieveForQueryAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? documentFilter,
        int topK,
        decimal? minScore,
        bool includeParents,
        CancellationToken ct);
}
```

- [ ] **Step 2: Build + commit**

```bash
cd boilerplateBE && dotnet build src/modules/Starter.Module.AI/Starter.Module.AI.csproj
git add src/modules/Starter.Module.AI/Application/Services/Retrieval/IRagRetrievalService.cs
git commit -m "feat(ai): IRagRetrievalService contract"
```

---

## Task 21: Implement `RagRetrievalService` (TDD)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/FakeVectorStore.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/FakeKeywordSearchService.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/RagRetrievalServiceTests.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs`

- [ ] **Step 1: Write `FakeVectorStore`**

```csharp
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Api.Tests.Ai.Retrieval;

internal sealed class FakeVectorStore : IVectorStore
{
    public List<VectorSearchHit> HitsToReturn { get; set; } = new();
    public IReadOnlyCollection<Guid>? LastDocFilter { get; private set; }
    public Guid LastTenantId { get; private set; }

    public Task EnsureCollectionAsync(Guid tenantId, int vectorSize, CancellationToken ct) => Task.CompletedTask;
    public Task UpsertAsync(Guid tenantId, IReadOnlyList<VectorPoint> points, CancellationToken ct) => Task.CompletedTask;
    public Task DeleteByDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct) => Task.CompletedTask;
    public Task DropCollectionAsync(Guid tenantId, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        Guid tenantId, float[] queryVector, IReadOnlyCollection<Guid>? documentFilter, int limit, CancellationToken ct)
    {
        LastTenantId = tenantId;
        LastDocFilter = documentFilter;
        return Task.FromResult<IReadOnlyList<VectorSearchHit>>(HitsToReturn.Take(limit).ToList());
    }
}
```

- [ ] **Step 2: Write `FakeKeywordSearchService`**

```csharp
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Api.Tests.Ai.Retrieval;

internal sealed class FakeKeywordSearchService : IKeywordSearchService
{
    public List<KeywordSearchHit> HitsToReturn { get; set; } = new();

    public Task<IReadOnlyList<KeywordSearchHit>> SearchAsync(
        Guid tenantId, string queryText, IReadOnlyCollection<Guid>? documentFilter, int limit, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<KeywordSearchHit>>(HitsToReturn.Take(limit).ToList());
}
```

- [ ] **Step 3: Write failing `RagRetrievalServiceTests`**

Use an in-memory `AiDbContext` via `UseInMemoryDatabase`. Seed chunks manually. Test cases:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class RagRetrievalServiceTests
{
    [Fact]
    public async Task RagScope_None_Throws()
    {
        var (svc, assistant, _, _) = BuildService(AiRagScope.None, docIds: null);

        var act = async () => await svc.RetrieveForTurnAsync(assistant, "q", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SelectedDocuments_Filters_Qdrant_To_DocIds()
    {
        var docIds = new[] { Guid.NewGuid() };
        var (svc, assistant, fakeVs, _) = BuildService(AiRagScope.SelectedDocuments, docIds);
        fakeVs.HitsToReturn = []; // empty

        await svc.RetrieveForTurnAsync(assistant, "q", CancellationToken.None);

        fakeVs.LastDocFilter.Should().BeEquivalentTo(docIds);
    }

    [Fact]
    public async Task AllTenantDocuments_Sends_Null_DocFilter()
    {
        var (svc, assistant, fakeVs, _) = BuildService(AiRagScope.AllTenantDocuments, docIds: null);
        fakeVs.HitsToReturn = [];

        await svc.RetrieveForTurnAsync(assistant, "q", CancellationToken.None);

        fakeVs.LastDocFilter.Should().BeNull();
    }

    [Fact]
    public async Task Both_Search_Sides_Empty_Returns_Empty_Context()
    {
        var (svc, assistant, _, _) = BuildService(AiRagScope.AllTenantDocuments, null);

        var ctx = await svc.RetrieveForTurnAsync(assistant, "q", CancellationToken.None);

        ctx.IsEmpty.Should().BeTrue();
        ctx.Children.Should().BeEmpty();
        ctx.Parents.Should().BeEmpty();
    }

    [Fact]
    public async Task TopK_Plus_Parent_Dedup_Work()
    {
        // Seed two children sharing one parent in the in-memory DB.
        // Both children get semantic hits; top-K=5 so both survive; parents list has 1 item.
        var (svc, assistant, fakeVs, db) = BuildService(AiRagScope.AllTenantDocuments, null, topK: 5);
        var parent = SeedParentChunk(db, assistant.TenantId!.Value);
        var childA = SeedChildChunk(db, assistant.TenantId!.Value, parent.Id);
        var childB = SeedChildChunk(db, assistant.TenantId!.Value, parent.Id);
        await db.SaveChangesAsync();

        fakeVs.HitsToReturn = [
            new(childA.Id, 0.9m),
            new(childB.Id, 0.8m)
        ];

        var ctx = await svc.RetrieveForTurnAsync(assistant, "q", CancellationToken.None);

        ctx.Children.Should().HaveCount(2);
        ctx.Parents.Should().HaveCount(1);
        ctx.Parents[0].ChunkId.Should().Be(parent.Id);
    }

    // Helpers
    private static (RagRetrievalService svc, AiAssistant assistant, FakeVectorStore vs, AiDbContext db)
        BuildService(AiRagScope scope, Guid[]? docIds, int topK = 5)
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-{Guid.NewGuid()}")
            .Options;
        var db = new AiDbContext(options);

        var settings = Options.Create(new AiRagSettings
        {
            TopK = topK,
            RetrievalTopK = 20,
            HybridSearchWeight = 0.7,
            MaxContextTokens = 4000,
            IncludeParentContext = true,
            MinHybridScore = 0m
        });

        var assistant = AiAssistant.Create(tenantId, "A", null, "p");
        if (docIds is not null) assistant.SetKnowledgeBase(docIds);
        if (scope != AiRagScope.None) assistant.SetRagScope(scope);

        var fakeVs = new FakeVectorStore();
        var fakeKw = new FakeKeywordSearchService();
        var fakeEmbed = new FakeEmbeddingService();
        var tokenCounter = new TokenCounter();  // real

        var svc = new RagRetrievalService(
            fakeVs, fakeKw, fakeEmbed, db, tokenCounter, settings, NullLogger<RagRetrievalService>.Instance);

        return (svc, assistant, fakeVs, db);
    }

    private static AiDocumentChunk SeedParentChunk(AiDbContext db, Guid tenantId) { /* use Plan 4a factory */ throw new NotImplementedException(); }
    private static AiDocumentChunk SeedChildChunk(AiDbContext db, Guid tenantId, Guid parentId) { /* use Plan 4a factory */ throw new NotImplementedException(); }
}

internal sealed class FakeEmbeddingService : IEmbeddingService
{
    public int VectorSize => 1536;
    public Task<float[][]> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct, EmbedAttribution? attribution = null)
        => Task.FromResult(texts.Select(_ => new float[1536]).ToArray());
}
```

Fill in the `SeedParentChunk` / `SeedChildChunk` helpers using the real `AiDocumentChunk` factory from Plan 4a. Also seed the parent `AiDocument` row so `Include(c => c.Document)` works.

- [ ] **Step 4: Run tests to verify fail**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~RagRetrievalServiceTests`
Expected: FAIL — `RagRetrievalService` undefined.

- [ ] **Step 5: Implement `RagRetrievalService`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Retrieval;

internal sealed class RagRetrievalService : IRagRetrievalService
{
    private readonly IVectorStore _vectorStore;
    private readonly IKeywordSearchService _keywordSearch;
    private readonly IEmbeddingService _embedding;
    private readonly AiDbContext _db;
    private readonly TokenCounter _tokenCounter;
    private readonly AiRagSettings _settings;
    private readonly ILogger<RagRetrievalService> _logger;

    public RagRetrievalService(
        IVectorStore vectorStore,
        IKeywordSearchService keywordSearch,
        IEmbeddingService embedding,
        AiDbContext db,
        TokenCounter tokenCounter,
        IOptions<AiRagSettings> settings,
        ILogger<RagRetrievalService> logger)
    {
        _vectorStore = vectorStore;
        _keywordSearch = keywordSearch;
        _embedding = embedding;
        _db = db;
        _tokenCounter = tokenCounter;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<RetrievedContext> RetrieveForTurnAsync(
        AiAssistant assistant,
        string latestUserMessage,
        CancellationToken ct)
    {
        if (assistant.RagScope == AiRagScope.None)
            throw new InvalidOperationException("Caller must ensure RagScope != None before invoking retrieval.");

        IReadOnlyCollection<Guid>? docFilter = assistant.RagScope switch
        {
            AiRagScope.SelectedDocuments => assistant.KnowledgeBaseDocIds.ToList(),
            AiRagScope.AllTenantDocuments => null,
            _ => throw new InvalidOperationException()
        };

        return await RetrieveCoreAsync(
            tenantId: assistant.TenantId!.Value,
            queryText: latestUserMessage,
            docFilter: docFilter,
            topK: _settings.TopK,
            minScore: _settings.MinHybridScore,
            includeParents: _settings.IncludeParentContext,
            attributionUserId: null,
            requestType: AiRequestType.QueryEmbedding,
            ct: ct);
    }

    public async Task<RetrievedContext> RetrieveForQueryAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? documentFilter,
        int topK,
        decimal? minScore,
        bool includeParents,
        CancellationToken ct)
    {
        return await RetrieveCoreAsync(
            tenantId: tenantId,
            queryText: queryText,
            docFilter: documentFilter,
            topK: topK,
            minScore: minScore ?? _settings.MinHybridScore,
            includeParents: includeParents,
            attributionUserId: null,
            requestType: AiRequestType.QueryEmbedding,
            ct: ct);
    }

    private async Task<RetrievedContext> RetrieveCoreAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? docFilter,
        int topK,
        decimal minScore,
        bool includeParents,
        Guid? attributionUserId,
        AiRequestType requestType,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(queryText))
            return RetrievedContext.Empty;

        // Step 2 — embed query
        var vectors = await _embedding.EmbedAsync(
            new[] { queryText }, ct,
            new EmbedAttribution(tenantId, attributionUserId ?? Guid.Empty));
        var queryVector = vectors[0];

        // Step 3 — parallel fan-out
        var semTask = _vectorStore.SearchAsync(tenantId, queryVector, docFilter, _settings.RetrievalTopK, ct);
        var kwTask = _keywordSearch.SearchAsync(tenantId, queryText, docFilter, _settings.RetrievalTopK, ct);
        await Task.WhenAll(semTask, kwTask);

        var semHits = semTask.Result;
        var kwHits = kwTask.Result;

        if (semHits.Count == 0 && kwHits.Count == 0)
        {
            _logger.LogWarning(
                "RAG retrieval returned zero hits for tenant {TenantId} (scope docs: {DocFilter}).",
                tenantId, docFilter?.Count ?? -1);
            return RetrievedContext.Empty;
        }

        // Step 4–5 — hybrid merge + top-K
        var merged = HybridScoreCalculator.Combine(semHits, kwHits, (decimal)_settings.HybridSearchWeight, minScore);
        var topKHits = merged.Take(topK).ToList();

        if (topKHits.Count == 0)
            return RetrievedContext.Empty;

        // Step 6 — fetch child bodies
        var childIds = topKHits.Select(h => h.ChunkId).ToList();
        var children = await _db.AiDocumentChunks
            .AsNoTracking()
            .Include(c => c.Document)
            .Where(c => childIds.Contains(c.Id))
            .ToListAsync(ct);

        // Preserve hybrid order
        var childOrdered = topKHits
            .Select(h => (h, Entity: children.FirstOrDefault(c => c.Id == h.ChunkId)))
            .Where(x => x.Entity is not null)
            .ToList();

        // Step 6 (cont.) — fetch parents if enabled
        var parentEntities = new List<AiDocumentChunk>();
        if (includeParents)
        {
            var parentIds = childOrdered
                .Where(x => x.Entity!.ParentChunkId.HasValue)
                .Select(x => x.Entity!.ParentChunkId!.Value)
                .Distinct()
                .ToList();

            if (parentIds.Count > 0)
            {
                parentEntities = await _db.AiDocumentChunks
                    .AsNoTracking()
                    .Include(c => c.Document)
                    .Where(c => parentIds.Contains(c.Id))
                    .ToListAsync(ct);
            }
        }

        // Step 7 — assemble
        var childChunks = childOrdered.Select(x => Map(x.Entity!, x.h, ChunkLevel.Child)).ToList();
        var parentChunks = parentEntities.Select(p => Map(p, null, ChunkLevel.Parent)).ToList();

        // Step 8 — token-budget trim
        var (trimmedChildren, trimmedParents, total, truncated) = TrimToBudget(
            childChunks, parentChunks, _settings.MaxContextTokens);

        return new RetrievedContext(trimmedChildren, trimmedParents, total, truncated);
    }

    private static RetrievedChunk Map(AiDocumentChunk c, HybridHit? hit, ChunkLevel level) => new(
        ChunkId: c.Id,
        DocumentId: c.DocumentId,
        DocumentName: c.Document?.Name ?? "(unknown)",
        Content: c.Content,
        SectionTitle: c.SectionTitle,
        PageNumber: c.PageNumber,
        ChunkLevel: level,
        SemanticScore: hit?.SemanticScore ?? 0m,
        KeywordScore: hit?.KeywordScore ?? 0m,
        HybridScore: hit?.HybridScore ?? 0m,
        ParentChunkId: c.ParentChunkId);

    private (List<RetrievedChunk> Children, List<RetrievedChunk> Parents, int Total, bool Truncated)
        TrimToBudget(
            List<RetrievedChunk> children,
            List<RetrievedChunk> parents,
            int maxTokens)
    {
        var retainedChildren = new List<RetrievedChunk>();
        var retainedParents = new List<RetrievedChunk>();
        var total = 0;
        var truncated = false;

        // Children first
        foreach (var c in children)
        {
            var t = _tokenCounter.CountTokens(c.Content);
            if (total + t > maxTokens)
            {
                truncated = true;
                break;
            }
            retainedChildren.Add(c);
            total += t;
        }

        // Parents in order of their children's hybrid score
        var parentsByChildScore = parents
            .Select(p => (parent: p, maxChildScore: children
                .Where(c => c.ParentChunkId == p.ChunkId)
                .Select(c => c.HybridScore)
                .DefaultIfEmpty(0m)
                .Max()))
            .OrderByDescending(x => x.maxChildScore)
            .Select(x => x.parent);

        foreach (var p in parentsByChildScore)
        {
            var t = _tokenCounter.CountTokens(p.Content);
            if (total + t > maxTokens)
            {
                truncated = true;
                break;
            }
            retainedParents.Add(p);
            total += t;
        }

        if (retainedChildren.Count == 0 && children.Count > 0)
        {
            // Even the lowest child exceeds budget — drop parents and take highest child.
            retainedParents.Clear();
            total = 0;
            foreach (var c in children.OrderByDescending(c => c.HybridScore))
            {
                var t = _tokenCounter.CountTokens(c.Content);
                if (total + t <= maxTokens)
                {
                    retainedChildren.Add(c);
                    total += t;
                    break;
                }
            }
            truncated = true;
        }

        return (retainedChildren, retainedParents, total, truncated);
    }
}
```

If `TokenCounter.CountTokens` doesn't exist with that exact signature, rename — inspect Plan 4a's `TokenCounter.cs` and use the actual method name. Likewise for `AiDbContext.AiDocumentChunks` (might be named differently).

- [ ] **Step 6: Run tests to verify pass**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~RagRetrievalServiceTests`
Expected: 5 passed.

- [ ] **Step 7: Commit**

```bash
git add src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs tests/Starter.Api.Tests/Ai/Retrieval
git commit -m "feat(ai): RagRetrievalService + unit tests"
```

---

## Task 22: `CitationParser` (TDD)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/CitationParserTests.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/CitationParser.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using FluentAssertions;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class CitationParserTests
{
    private static readonly IReadOnlyList<RetrievedChunk> Chunks = new List<RetrievedChunk>
    {
        MakeChunk(1), MakeChunk(2), MakeChunk(3)
    };

    [Fact]
    public void Parses_Single_Marker()
    {
        var result = CitationParser.Parse("Water is wet [1].", Chunks);
        result.Should().HaveCount(1);
        result[0].Marker.Should().Be(1);
        result[0].ChunkId.Should().Be(Chunks[0].ChunkId);
    }

    [Fact]
    public void Parses_Multi_Index_Markers()
    {
        var result = CitationParser.Parse("Both sources agree [1, 3].", Chunks);
        result.Should().HaveCount(2);
        result.Select(c => c.Marker).Should().BeEquivalentTo(new[] { 1, 3 });
    }

    [Fact]
    public void Tolerates_Whitespace_And_Dedupes()
    {
        var result = CitationParser.Parse("See [1] and [  1 ] and [1, 2].", Chunks);
        result.Select(c => c.Marker).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void Drops_OutOfRange_Markers_With_Warning()
    {
        var result = CitationParser.Parse("Check [5] or [1].", Chunks);
        result.Should().HaveCount(1);
        result[0].Marker.Should().Be(1);
    }

    [Fact]
    public void No_Markers_Returns_Fallback_Full_Set()
    {
        var result = CitationParser.Parse("plain answer with no citations", Chunks);
        result.Should().HaveCount(3);
        result.Select(c => c.Marker).Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void No_Markers_No_Chunks_Returns_Empty()
    {
        var result = CitationParser.Parse("text", new List<RetrievedChunk>());
        result.Should().BeEmpty();
    }

    private static RetrievedChunk MakeChunk(int i) => new(
        ChunkId: Guid.Parse($"00000000-0000-0000-0000-00000000000{i}"),
        DocumentId: Guid.NewGuid(),
        DocumentName: $"Doc{i}",
        Content: $"content {i}",
        SectionTitle: null,
        PageNumber: null,
        ChunkLevel: ChunkLevel.Child,
        SemanticScore: 0.9m,
        KeywordScore: 0.5m,
        HybridScore: 0.8m,
        ParentChunkId: null);
}
```

- [ ] **Step 2: Run tests, verify fail**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~CitationParserTests`
Expected: FAIL (compile).

- [ ] **Step 3: Implement `CitationParser`**

```csharp
using System.Text.RegularExpressions;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval;

internal static class CitationParser
{
    private static readonly Regex MarkerRegex = new(
        @"\[(\d+(?:\s*,\s*\d+)*)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<AiMessageCitation> Parse(
        string? assistantText,
        IReadOnlyList<RetrievedChunk> retrievedChildren)
    {
        if (retrievedChildren.Count == 0)
            return [];

        if (string.IsNullOrWhiteSpace(assistantText))
            return Fallback(retrievedChildren);

        var parsedMarkers = new HashSet<int>();
        foreach (Match match in MarkerRegex.Matches(assistantText))
        {
            foreach (var tok in match.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(tok, out var n)) continue;
                if (n < 1 || n > retrievedChildren.Count) continue;
                parsedMarkers.Add(n);
            }
        }

        if (parsedMarkers.Count == 0)
            return Fallback(retrievedChildren);

        return parsedMarkers
            .OrderBy(n => n)
            .Select(n => ToCitation(n, retrievedChildren[n - 1]))
            .ToList();
    }

    private static IReadOnlyList<AiMessageCitation> Fallback(IReadOnlyList<RetrievedChunk> retrieved) =>
        retrieved.Select((c, i) => ToCitation(i + 1, c)).ToList();

    private static AiMessageCitation ToCitation(int marker, RetrievedChunk c) => new(
        Marker: marker,
        ChunkId: c.ChunkId,
        DocumentId: c.DocumentId,
        DocumentName: c.DocumentName,
        SectionTitle: c.SectionTitle,
        PageNumber: c.PageNumber,
        Score: c.HybridScore);
}
```

- [ ] **Step 4: Run tests, verify pass**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~CitationParserTests`
Expected: 6 passed.

- [ ] **Step 5: Commit**

```bash
git add src/modules/Starter.Module.AI/Infrastructure/Retrieval/CitationParser.cs tests/Starter.Api.Tests/Ai/Retrieval/CitationParserTests.cs
git commit -m "feat(ai): CitationParser with [n] regex + full-set fallback"
```

---

## Task 23: `ContextPromptBuilder` (TDD)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextPromptBuilderTests.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/ContextPromptBuilder.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using FluentAssertions;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class ContextPromptBuilderTests
{
    [Fact]
    public void Empty_Context_Omits_Context_Block()
    {
        var sp = ContextPromptBuilder.Build(
            assistantSystemPrompt: "You are helpful.",
            context: RetrievedContext.Empty);

        sp.Should().NotContain("<context>");
        sp.Should().Contain("You are helpful.");
    }

    [Fact]
    public void Non_Empty_Context_Numbers_Children_One_To_N()
    {
        var ctx = new RetrievedContext(
            Children: new List<RetrievedChunk>
            {
                MakeChild("apple"),
                MakeChild("banana")
            },
            Parents: [],
            TotalTokens: 10,
            TruncatedByBudget: false);

        var sp = ContextPromptBuilder.Build("Be helpful.", ctx);

        sp.Should().Contain("[1]");
        sp.Should().Contain("[2]");
        sp.Should().Contain("apple");
        sp.Should().Contain("banana");
        sp.Should().Contain("<context>");
        sp.Should().Contain("<assistant_instructions>");
        sp.Should().Contain("Be helpful.");
    }

    [Fact]
    public void Parents_Appear_Near_Children_Without_Own_Marker()
    {
        var parentId = Guid.NewGuid();
        var ctx = new RetrievedContext(
            Children: new List<RetrievedChunk> { MakeChild("child", parentChunkId: parentId) },
            Parents: new List<RetrievedChunk> { MakeParent(parentId, "parent context") },
            TotalTokens: 10,
            TruncatedByBudget: false);

        var sp = ContextPromptBuilder.Build("S", ctx);

        sp.Should().Contain("child");
        sp.Should().Contain("parent context");
        sp.Should().Contain("(context continues)");
        // No "[2]" marker for parent
        sp.Should().NotContain("[2]");
    }

    private static RetrievedChunk MakeChild(string content, Guid? parentChunkId = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), "Doc", content, "Section", 1,
        ChunkLevel.Child, 0.9m, 0.4m, 0.7m, parentChunkId);

    private static RetrievedChunk MakeParent(Guid id, string content) => new(
        id, Guid.NewGuid(), "Doc", content, "Section", 1,
        ChunkLevel.Parent, 0m, 0m, 0m, null);
}
```

- [ ] **Step 2: Run, verify fail.**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~ContextPromptBuilderTests`
Expected: FAIL.

- [ ] **Step 3: Implement `ContextPromptBuilder`**

```csharp
using System.Text;
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval;

internal static class ContextPromptBuilder
{
    public static string Build(string assistantSystemPrompt, RetrievedContext context)
    {
        var sb = new StringBuilder();

        if (!context.IsEmpty)
        {
            sb.AppendLine("You have access to the following knowledge base excerpts, numbered [1]..[N].");
            sb.AppendLine("Ground your answer in these excerpts when they are relevant.");
            sb.AppendLine("When you use information from an excerpt, cite it inline as [n] (or [n, m] for multiple).");
            sb.AppendLine("If the excerpts do not contain the answer, say so plainly and do not fabricate citations.");
            sb.AppendLine();
            sb.AppendLine("<context>");

            var parentsById = context.Parents.ToDictionary(p => p.ChunkId);

            for (int i = 0; i < context.Children.Count; i++)
            {
                var child = context.Children[i];
                sb.Append('[').Append(i + 1).Append("] Document: \"").Append(child.DocumentName).Append('"');
                if (!string.IsNullOrWhiteSpace(child.SectionTitle))
                    sb.Append(" · Section: \"").Append(child.SectionTitle).Append('"');
                if (child.PageNumber.HasValue)
                    sb.Append(" · Page ").Append(child.PageNumber);
                sb.AppendLine();
                sb.AppendLine(child.Content);

                if (child.ParentChunkId.HasValue && parentsById.TryGetValue(child.ParentChunkId.Value, out var parent))
                {
                    sb.AppendLine("(context continues)");
                    sb.AppendLine(parent.Content);
                }
                sb.AppendLine();
            }

            sb.AppendLine("</context>");
            sb.AppendLine();
        }

        sb.AppendLine("<assistant_instructions>");
        sb.AppendLine(assistantSystemPrompt);
        sb.AppendLine("</assistant_instructions>");

        return sb.ToString();
    }
}
```

- [ ] **Step 4: Run tests, verify pass.**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~ContextPromptBuilderTests`
Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add src/modules/Starter.Module.AI/Infrastructure/Retrieval/ContextPromptBuilder.cs tests/Starter.Api.Tests/Ai/Retrieval/ContextPromptBuilderTests.cs
git commit -m "feat(ai): ContextPromptBuilder renders <context> + <assistant_instructions>"
```

---

## Task 24: Update `IEmbeddingService` + `EmbeddingService` to accept `AiRequestType`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/IEmbeddingService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/EmbeddingService.cs`

- [ ] **Step 1: Extend interface**

```csharp
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Ingestion;

public sealed record EmbedAttribution(Guid? TenantId, Guid UserId);

public interface IEmbeddingService
{
    Task<float[][]> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct,
        EmbedAttribution? attribution = null,
        AiRequestType requestType = AiRequestType.Embedding);

    int VectorSize { get; }
}
```

- [ ] **Step 2: Forward `requestType` inside `EmbeddingService`**

Open `EmbeddingService.cs`. Locate where it writes the `AiUsageLog` row. Replace the hard-coded `AiRequestType.Embedding` with the passed-in `requestType` parameter. Default preserves existing behaviour.

- [ ] **Step 3: Build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/modules/Starter.Module.AI/Application/Services/Ingestion/IEmbeddingService.cs src/modules/Starter.Module.AI/Infrastructure/Ingestion/EmbeddingService.cs
git commit -m "feat(ai): IEmbeddingService accepts AiRequestType for usage attribution"
```

- [ ] **Step 5: Update `RagRetrievalService` call site**

In `RagRetrievalService.RetrieveCoreAsync`, change the `EmbedAsync` call to pass `requestType: AiRequestType.QueryEmbedding`:

```csharp
var vectors = await _embedding.EmbedAsync(
    new[] { queryText }, ct,
    new EmbedAttribution(tenantId, attributionUserId ?? Guid.Empty),
    AiRequestType.QueryEmbedding);
```

(The method already passes `requestType` as a param — this just wires it to the overloaded service.)

Build + re-run retrieval tests:
```bash
cd boilerplateBE && dotnet build && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~RagRetrievalServiceTests
```
Expected: still passing.

Amend the previous commit or create a follow-up: `git commit -m "feat(ai): RagRetrievalService logs QueryEmbedding usage type"` (prefer a separate commit for clarity).

---

## Task 25: `SearchKnowledgeBaseQuery` + Handler + DTOs

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Features/Search/SearchKnowledgeBaseQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Features/Search/SearchKnowledgeBaseQueryValidator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Features/Search/SearchKnowledgeBaseResultDto.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Features/Search/SearchKnowledgeBaseResultItemDto.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Features/Search/SearchKnowledgeBaseQueryHandler.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/SearchKnowledgeBaseQueryHandlerTests.cs`

- [ ] **Step 1: Write the query + validator**

```csharp
// SearchKnowledgeBaseQuery.cs
using MediatR;
using Starter.Application.Common;   // for Result<T> — verify namespace

namespace Starter.Module.AI.Application.Features.Search;

public sealed record SearchKnowledgeBaseQuery(
    string Query,
    IReadOnlyList<Guid>? DocumentIds,
    int? TopK,
    decimal? MinScore,
    bool IncludeParents = true
) : IRequest<Result<SearchKnowledgeBaseResultDto>>;
```

```csharp
// SearchKnowledgeBaseQueryValidator.cs
using FluentValidation;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Starter.Module.AI.Application.Features.Search;

public sealed class SearchKnowledgeBaseQueryValidator : AbstractValidator<SearchKnowledgeBaseQuery>
{
    public SearchKnowledgeBaseQueryValidator(IOptions<AiRagSettings> settings)
    {
        var s = settings.Value;

        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage(AiErrors.SearchQueryRequired.Message)
            .WithErrorCode(AiErrors.SearchQueryRequired.Code);

        RuleFor(x => x.TopK)
            .Must(k => k is null || (k >= 1 && k <= s.RetrievalTopK))
            .WithMessage(AiErrors.SearchTopKOutOfRange(s.RetrievalTopK).Message)
            .WithErrorCode(AiErrors.SearchTopKOutOfRange(s.RetrievalTopK).Code);
    }
}
```

- [ ] **Step 2: Write DTOs**

```csharp
// SearchKnowledgeBaseResultDto.cs
namespace Starter.Module.AI.Application.Features.Search;

public sealed record SearchKnowledgeBaseResultDto(
    IReadOnlyList<SearchKnowledgeBaseResultItemDto> Items,
    int TotalHits,
    bool TruncatedByBudget);
```

```csharp
// SearchKnowledgeBaseResultItemDto.cs
namespace Starter.Module.AI.Application.Features.Search;

public sealed record SearchKnowledgeBaseResultItemDto(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    string Content,
    string? SectionTitle,
    int? PageNumber,
    string ChunkLevel,      // "child" | "parent"
    decimal? HybridScore,
    decimal? SemanticScore,
    decimal? KeywordScore,
    Guid? ParentChunkId);
```

- [ ] **Step 3: Write handler**

```csharp
// SearchKnowledgeBaseQueryHandler.cs
using MediatR;
using Microsoft.Extensions.Options;
using Starter.Application.Common;
using Starter.Application.Common.Services;   // for ICurrentUserService — verify
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Application.Features.Search;

public sealed class SearchKnowledgeBaseQueryHandler : IRequestHandler<SearchKnowledgeBaseQuery, Result<SearchKnowledgeBaseResultDto>>
{
    private readonly IRagRetrievalService _retrieval;
    private readonly ICurrentUserService _currentUser;
    private readonly AiRagSettings _settings;

    public SearchKnowledgeBaseQueryHandler(
        IRagRetrievalService retrieval,
        ICurrentUserService currentUser,
        IOptions<AiRagSettings> settings)
    {
        _retrieval = retrieval;
        _currentUser = currentUser;
        _settings = settings.Value;
    }

    public async Task<Result<SearchKnowledgeBaseResultDto>> Handle(SearchKnowledgeBaseQuery request, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId ?? throw new InvalidOperationException("Search requires a tenant scope.");

        var topK = request.TopK ?? _settings.TopK;
        var ctx = await _retrieval.RetrieveForQueryAsync(
            tenantId,
            request.Query,
            request.DocumentIds?.Count > 0 ? request.DocumentIds.ToList() : null,
            topK,
            request.MinScore,
            request.IncludeParents,
            ct);

        var items = new List<SearchKnowledgeBaseResultItemDto>();
        foreach (var c in ctx.Children)
        {
            items.Add(Map(c));
            if (request.IncludeParents)
            {
                var parent = ctx.Parents.FirstOrDefault(p => p.ChunkId == c.ParentChunkId);
                if (parent is not null) items.Add(Map(parent));
            }
        }

        return Result<SearchKnowledgeBaseResultDto>.Success(
            new SearchKnowledgeBaseResultDto(items, ctx.Children.Count, ctx.TruncatedByBudget));
    }

    private static SearchKnowledgeBaseResultItemDto Map(RetrievedChunk c) => new(
        ChunkId: c.ChunkId,
        DocumentId: c.DocumentId,
        DocumentName: c.DocumentName,
        Content: c.Content,
        SectionTitle: c.SectionTitle,
        PageNumber: c.PageNumber,
        ChunkLevel: c.ChunkLevel == Domain.Enums.ChunkLevel.Child ? "child" : "parent",
        HybridScore: c.ChunkLevel == Domain.Enums.ChunkLevel.Child ? c.HybridScore : null,
        SemanticScore: c.ChunkLevel == Domain.Enums.ChunkLevel.Child ? c.SemanticScore : null,
        KeywordScore: c.ChunkLevel == Domain.Enums.ChunkLevel.Child ? c.KeywordScore : null,
        ParentChunkId: c.ParentChunkId);
}
```

Verify `ICurrentUserService`, `Result<T>` namespaces by reading one existing handler (e.g. from the users or billing feature).

- [ ] **Step 4: Write handler unit test**

```csharp
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Services;
using Starter.Module.AI.Application.Features.Search;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class SearchKnowledgeBaseQueryHandlerTests
{
    [Fact]
    public async Task Returns_Items_Mapped_From_RetrievedContext()
    {
        var tenantId = Guid.NewGuid();
        var retrieval = new FakeRetrievalService(tenantId);
        var currentUser = new FakeCurrentUser(tenantId);
        var opts = Options.Create(new AiRagSettings { TopK = 5, RetrievalTopK = 20 });
        var handler = new SearchKnowledgeBaseQueryHandler(retrieval, currentUser, opts);

        var result = await handler.Handle(
            new SearchKnowledgeBaseQuery("q", null, 5, null, true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().NotBeEmpty();
    }
}

internal sealed class FakeRetrievalService : IRagRetrievalService
{
    private readonly Guid _tenant;
    public FakeRetrievalService(Guid t) => _tenant = t;
    public Task<RetrievedContext> RetrieveForTurnAsync(Domain.Entities.AiAssistant a, string q, CancellationToken ct)
        => throw new NotSupportedException();
    public Task<RetrievedContext> RetrieveForQueryAsync(Guid tenantId, string q, IReadOnlyCollection<Guid>? f, int k, decimal? m, bool p, CancellationToken ct)
    {
        var child = new RetrievedChunk(
            Guid.NewGuid(), Guid.NewGuid(), "Doc", "content", null, null,
            ChunkLevel.Child, 0.9m, 0.3m, 0.7m, null);
        return Task.FromResult(new RetrievedContext([child], [], 10, false));
    }
}

internal sealed class FakeCurrentUser : ICurrentUserService
{
    public FakeCurrentUser(Guid? tenant) => TenantId = tenant;
    public Guid? TenantId { get; }
    public Guid? UserId => Guid.NewGuid();
    public string? Email => null;
    public bool IsAuthenticated => true;
    public IReadOnlyList<string> Permissions => [];
}
```

Adjust `ICurrentUserService` member list to match the real interface — likely it has more/fewer members. Read the real definition first.

- [ ] **Step 5: Run tests — verify fail then pass**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~SearchKnowledgeBaseQueryHandlerTests
```
Expected: FAIL (compile) → after implementing, 1 passed.

- [ ] **Step 6: Commit**

```bash
git add src/modules/Starter.Module.AI/Application/Features/Search tests/Starter.Api.Tests/Ai/Retrieval/SearchKnowledgeBaseQueryHandlerTests.cs
git commit -m "feat(ai): SearchKnowledgeBaseQuery + handler + DTOs + unit test"
```

---

## Task 26: `AiSearchController` + controller test

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiSearchController.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/AiSearchControllerTests.cs`

- [ ] **Step 1: Write controller**

Read an existing AI module controller first (e.g. `AiDocumentsController.cs`) to match the base class and response-handling pattern.

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Api.Controllers;  // BaseApiController — verify namespace
using Starter.Module.AI.Application.Features.Search;
using Starter.Module.AI.Constants;

namespace Starter.Module.AI.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/ai/search")]
[ApiVersion("1.0")]
[Authorize(Policy = AiPermissions.SearchKnowledgeBase)]
public sealed class AiSearchController(ISender sender) : BaseApiController(sender)
{
    [HttpPost]
    public async Task<IActionResult> Search([FromBody] SearchKnowledgeBaseQuery query, CancellationToken ct)
    {
        var result = await Sender.Send(query, ct);
        return HandleResult(result);
    }
}
```

Match the existing version-attribute style. If other controllers use `[Route("api/v{version:apiVersion}/[controller]")]` + suffix, do NOT reuse — `search` doesn't cleanly plural-ise. Use the explicit path shown above.

- [ ] **Step 2: Write integration test**

Follow the same test pattern used by `AiDocumentsControllerTests` (Plan 4a). Seed an authenticated admin user, POST to `/api/v1/ai/search`, assert 200 shape. Also assert 401 without token and 403 without `SearchKnowledgeBase`.

```csharp
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class AiSearchControllerTests : IClassFixture<ExistingWebApplicationFixture>
{
    private readonly ExistingWebApplicationFixture _fx;
    public AiSearchControllerTests(ExistingWebApplicationFixture fx) => _fx = fx;

    [Fact]
    public async Task Post_Requires_Authentication()
    {
        using var client = _fx.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/ai/search", new { query = "q" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Requires_SearchKnowledgeBase_Permission()
    {
        using var client = _fx.CreateClientAsUserWithoutPermission(AiPermissions.SearchKnowledgeBase);
        var resp = await client.PostAsJsonAsync("/api/v1/ai/search", new { query = "q" });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_With_Permission_Returns_Ok_And_Items_Shape()
    {
        using var client = _fx.CreateClientAsSuperAdmin();
        // Seed a doc+chunk via Plan 4a upload path, wait for ingestion
        // Then POST /ai/search
        var resp = await client.PostAsJsonAsync("/api/v1/ai/search", new { query = "term from the doc", topK = 5 });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        body!.RootElement.GetProperty("data").GetProperty("items").ValueKind
            .Should().Be(System.Text.Json.JsonValueKind.Array);
    }
}
```

Placeholder fixture/helper names — replace with actual names from existing AI tests.

- [ ] **Step 3: Build + run tests**

```bash
cd boilerplateBE && dotnet build && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~AiSearchControllerTests
```
Expected: tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/modules/Starter.Module.AI/Controllers/AiSearchController.cs tests/Starter.Api.Tests/Ai/Retrieval/AiSearchControllerTests.cs
git commit -m "feat(ai): POST /api/v1/ai/search endpoint + integration tests"
```

---

## Task 27: DI registration in `AIModule.cs`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

- [ ] **Step 1: Register retrieval services**

Inside `ConfigureServices`, after the ingestion service registrations (around the `QdrantVectorStore` line), append:

```csharp
services.AddScoped<IKeywordSearchService, Infrastructure.Retrieval.PostgresKeywordSearchService>();
services.AddScoped<IRagRetrievalService, Infrastructure.Retrieval.RagRetrievalService>();
```

Add usings at top if missing:
```csharp
using Starter.Module.AI.Application.Services.Retrieval;
```

(No DI registrations needed for the static `CitationParser`, `ContextPromptBuilder`, `HybridScoreCalculator`.)

- [ ] **Step 2: Build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/modules/Starter.Module.AI/AIModule.cs
git commit -m "feat(ai): register IKeywordSearchService + IRagRetrievalService"
```

---

## Task 28: Integrate retrieval into `ChatExecutionService` (TDD)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ChatExecutionRagInjectionTests.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs`

- [ ] **Step 1: Read `ChatExecutionService.cs` end-to-end**

735 lines. Identify:
1. Where the system prompt is currently assembled (`BuildChatOptions` around line 550).
2. Where the final assistant text is captured after streaming.
3. Where the assistant `AiMessage` is persisted.
4. Where SSE events are yielded.
5. Whether DI already injects `IRagRetrievalService` or if the constructor needs updating.

List these line ranges in a short note before modifying.

- [ ] **Step 2: Write a failing end-to-end test**

Use existing chat-test infrastructure (likely a fake `IAiProvider` called `FakeAiProvider` or similar). Follow existing chat tests' patterns. Key assertions:

```csharp
using FluentAssertions;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class ChatExecutionRagInjectionTests : IClassFixture<ChatExecutionTestFixture>
{
    private readonly ChatExecutionTestFixture _fx;
    public ChatExecutionRagInjectionTests(ChatExecutionTestFixture fx) => _fx = fx;

    [Fact]
    public async Task RagScope_None_Does_Not_Inject_Context()
    {
        var assistant = _fx.SeedAssistantWithRagScope(AiRagScope.None);
        var (events, savedMessage) = await _fx.RunOneTurnAsync(assistant, userMessage: "hello");

        savedMessage.Citations.Should().BeEmpty();
        events.Should().NotContain(e => e.Type == "citations");
        _fx.FakeProvider.LastSystemPrompt.Should().NotContain("<context>");
    }

    [Fact]
    public async Task RagScope_SelectedDocuments_Injects_Context_And_Emits_Citations()
    {
        var chunks = _fx.SeedTwoChunks();  // seeds docs + chunks + fake vector hits
        var assistant = _fx.SeedAssistantWithRagScope(AiRagScope.SelectedDocuments, docIds: new[] { chunks.DocId });
        _fx.FakeProvider.ScriptedResponse = "The answer references [1] and [2].";

        var (events, savedMessage) = await _fx.RunOneTurnAsync(assistant, userMessage: "what does doc X say");

        _fx.FakeProvider.LastSystemPrompt.Should().Contain("<context>");
        savedMessage.Citations.Should().HaveCount(2);
        events.Should().ContainSingle(e => e.Type == "citations");

        // Verify event order: citations before done
        var citationIdx = events.FindIndex(e => e.Type == "citations");
        var doneIdx = events.FindIndex(e => e.Type == "done");
        citationIdx.Should().BeLessThan(doneIdx);
    }

    [Fact]
    public async Task Fallback_When_Model_Emits_No_Markers_Populates_Full_Chunk_Set()
    {
        var chunks = _fx.SeedTwoChunks();
        var assistant = _fx.SeedAssistantWithRagScope(AiRagScope.SelectedDocuments, docIds: new[] { chunks.DocId });
        _fx.FakeProvider.ScriptedResponse = "plain answer with no citation tags";

        var (_, savedMessage) = await _fx.RunOneTurnAsync(assistant, userMessage: "q");

        savedMessage.Citations.Should().HaveCount(2);  // fallback: full retrieved set
    }
}
```

The `ChatExecutionTestFixture` is a placeholder — if an equivalent fixture exists for existing `ChatExecutionService` tests, extend it; otherwise build it here by composing the real `ChatExecutionService` with fake provider + fake retrieval + in-memory DB. Reuse `FakeVectorStore` and `FakeKeywordSearchService` from Task 21.

- [ ] **Step 3: Run — verify fail.**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~ChatExecutionRagInjectionTests`
Expected: FAIL (retrieval not yet wired).

- [ ] **Step 4: Inject `IRagRetrievalService` into `ChatExecutionService`**

Add to the constructor parameter list and the primary-constructor capture (match existing primary-constructor style — most handlers in this repo use it).

- [ ] **Step 5: Call retrieval + build system prompt**

In `BuildChatOptions` (or immediately before it in the caller), add:

```csharp
// Retrieve RAG context when the assistant has RagScope != None
RetrievedContext retrieved = RetrievedContext.Empty;
if (assistant.RagScope != AiRagScope.None && latestUserMessage is not null)
{
    try
    {
        retrieved = await retrievalService.RetrieveForTurnAsync(assistant, latestUserMessage, ct);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "RAG retrieval failed; proceeding without context.");
        retrieved = RetrievedContext.Empty;
    }
}

var effectiveSystemPrompt = retrieved.IsEmpty
    ? assistant.SystemPrompt
    : ContextPromptBuilder.Build(assistant.SystemPrompt, retrieved);
```

Replace the existing `SystemPrompt: assistant.SystemPrompt` in `BuildChatOptions` with `SystemPrompt: effectiveSystemPrompt`.

Capture `retrieved` in a local so the post-stream citation step can read it.

- [ ] **Step 6: Parse citations and emit `citations` event**

After the stream completes and before the final `done` event is yielded, and before persisting the message:

```csharp
var citations = CitationParser.Parse(finalAssistantText, retrieved.Children);

if (citations.Count > 0)
{
    yield return new ChatStreamEvent("citations", new
    {
        items = citations.Select(c => new
        {
            marker = c.Marker,
            chunkId = c.ChunkId,
            documentId = c.DocumentId,
            documentName = c.DocumentName,
            sectionTitle = c.SectionTitle,
            pageNumber = c.PageNumber,
            score = c.Score
        }).ToList()
    });
}
```

Adapt object-shape to match existing SSE serialisation conventions (camelCase comes from default JSON options).

- [ ] **Step 7: Persist citations on the assistant `AiMessage`**

Locate the call to `AiMessage.CreateAssistantMessage(...)` in the persist path. Replace with `CreateAssistantMessageWithCitations(...)` when `citations.Count > 0`:

```csharp
var assistantMsg = citations.Count > 0
    ? AiMessage.CreateAssistantMessageWithCitations(
        conversationId, finalAssistantText, order, citations,
        inputTokens, outputTokens, toolCalls)
    : AiMessage.CreateAssistantMessage(
        conversationId, finalAssistantText, order,
        inputTokens, outputTokens, toolCalls);
```

- [ ] **Step 8: Run tests — verify pass**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter FullyQualifiedName~ChatExecutionRagInjectionTests`
Expected: 3 passed.

- [ ] **Step 9: Full suite regression**

Run: `cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj`
Expected: all tests passing (38+ from Plan 4a, plus new 4b tests).

- [ ] **Step 10: Commit**

```bash
git add src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs tests/Starter.Api.Tests/Ai/Retrieval/ChatExecutionRagInjectionTests.cs
git commit -m "feat(ai): wire RAG retrieval + citations into ChatExecutionService"
```

---

## Task 29: End-to-end verification via post-feature-testing skill

**Files:** none modified; this is a verification step.

Follow [.claude/skills/post-feature-testing.md](../../.claude/skills/post-feature-testing.md) against a fresh test app. Use name `_testAiRag2` and ports `5102` / `3102` (5100 range may be held by prior sessions — verify with `lsof -i :5102` first; bump if busy).

- [ ] **Step 1: Check ports and generate test app**

```bash
lsof -i :5102 >/dev/null 2>&1 && echo "BUSY — pick another" || echo "5102 FREE"
lsof -i :3102 >/dev/null 2>&1 && echo "BUSY — pick another" || echo "3102 FREE"
PGPASSWORD=123456 psql -U postgres -h localhost -c "DROP DATABASE IF EXISTS _testairag2db;"
pwsh -File scripts/rename.ps1 -Name "_testAiRag2" -OutputDir "."
```

- [ ] **Step 2: Configure ports + secrets + seed + feature flags**

Apply all the steps from `.claude/skills/post-feature-testing.md` §4–§7:
- Rewrite `launchSettings.json` ports to `5102` / `5103`.
- Strip leading underscore from seed email and MinIO bucket.
- Flip `RabbitMQ.Enabled` to `true`.
- Set `AI:Ocr:Enabled = false` (OCR still not installed on this machine).
- In `_testAiRag2-BE/src/_testAiRag2.Api/appsettings.Development.json` `AI` block, ensure:
  - `"DefaultProvider": "Anthropic"`
  - `"EmbeddingProvider": "OpenAI"`
  - `"SeedSampleAssistant": true`
- Copy OpenAI + Anthropic API keys from the source project's user-secrets into the test project's user-secrets.

- [ ] **Step 3: Generate multi-context EF migrations**

Per `.claude/skills/post-feature-testing.md` §8, generate one migration per `DbContext` including the AI module's `AiDbContext`. The generated `InitialCreate` for AI will include `ai_assistants.rag_scope`, `ai_messages.citations (jsonb)`, and `ai_document_chunks.content_tsv (generated tsvector) + GIN index` (from Tasks 7, 12, 13).

- [ ] **Step 4: Build + run**

```bash
cd _testAiRag2/_testAiRag2-BE && dotnet build
cd src/_testAiRag2.Api && dotnet run --launch-profile http
```

Wait for `/health` to respond.

- [ ] **Step 5: Execute the 8-step verification script** (from spec § "End-to-end verification")

1. Upload two small .txt or .md documents with distinct content via the Plan 4a upload endpoint. Wait for both to reach `Completed` status.
2. Create assistant `A` via `POST /api/v1/ai/assistants` with `ragScope: "SelectedDocuments"`, `knowledgeBaseDocIds: [doc1.id]`, and a system prompt like "Answer based on provided context."
3. Start a conversation with assistant A and send a user message whose answer is in doc 1. Assert:
   - Response contains `[1]` inline.
   - `GET /api/v1/ai/conversations/{id}/messages` shows the assistant message with `citations` populated.
   - `ai_usage_logs` table has one new row with `request_type = QueryEmbedding` plus the normal chat row.
4. Update assistant A via `PUT` to `ragScope: "None"`. Resend the same user question. Assert citations empty, answer generic (doesn't reference doc 1 specifics).
5. Update assistant A to `ragScope: "AllTenantDocuments"`. Ask a question answerable only by doc 2. Assert doc 2 appears in citations.
6. `POST /api/v1/ai/search` as SuperAdmin with a query matching doc 1. Assert 200, items ordered by `hybridScore` desc, includes `child` + `parent` entries.
7. `POST /api/v1/ai/search` as a regular `User` (no `SearchKnowledgeBase` permission). Assert 403.
8. Attempt `PUT` on assistant A with `ragScope: "SelectedDocuments"` and `knowledgeBaseDocIds: []`. Assert 400 with `Ai.RagScopeRequiresDocuments`.

- [ ] **Step 6: Record verification result**

If all 8 steps pass, the plan is complete. If any fails, fix in the worktree source (not the test copy), regenerate the test app, re-verify.

- [ ] **Step 7: Leave running for manual QA**

Per skill workflow. Report the BE swagger URL, MinIO, Mailpit, Qdrant dashboard URLs to the user.

- [ ] **Step 8: On user approval — cleanup + push**

```bash
# Kill running BE
kill $(lsof -ti :5102) 2>/dev/null

# Drop test DB
PGPASSWORD=123456 psql -U postgres -h localhost -c "DROP DATABASE IF EXISTS _testairag2db;"

# Remove test directory
rm -rf _testAiRag2/

# Push commits
git push origin feature/ai-integration
```

---

## Deferred work tracker

Each item below should become its own future plan. Do not address any of them in Plan 4b-1. The deferred-work section in the design spec ([`docs/superpowers/specs/2026-04-18-ai-module-plan-4b-rag-retrieval-design.md`](../specs/2026-04-18-ai-module-plan-4b-rag-retrieval-design.md#deferred-work)) holds the full catalogue with "why deferred" notes.

When picking one up in a later session, start by re-reading the spec's deferred-work entry for it.

---

## Self-review

**Spec coverage:** Every section of the design spec maps to a task:

| Spec section | Task(s) |
|---|---|
| Architecture overview | Implemented through Tasks 19–28 combined |
| Data model — `RagScope` | Tasks 5, 6, 7, 8, 9 |
| Data model — `Citations` | Tasks 10, 11, 12 |
| Data model — FTS column | Task 13 |
| Retrieval pipeline (steps 1–9) | Task 21 (+ 16, 18 for sub-services) |
| Chat injection + citations | Tasks 22, 23, 28 |
| `POST /ai/search` | Tasks 25, 26 |
| Configuration | Task 1 |
| Permissions | Task 4 |
| Error codes | Task 2 |
| Testing strategy | Tasks 6, 17, 18, 21, 22, 23, 25, 26, 28 |
| End-to-end verification | Task 29 |

**Placeholder scan:** Plan contains no "TBD" / "TODO" / "similar to task N" placeholders. All code blocks are complete. Comments that direct the engineer to "verify namespace X" or "match existing pattern Y" are deliberate — they flag the one-line checks needed where the existing codebase's convention must be inspected to avoid introducing an inconsistent pattern. Every such note is bounded (a single file to read).

**Type/name consistency:**
- `AiRagScope` — spelled consistently (Task 5 introduces, Tasks 6, 8, 9, 19, 21, 28 use).
- `AiMessageCitation` — introduced Task 10, consumed Tasks 11, 12, 22, 28.
- `RetrievedChunk` / `RetrievedContext` — introduced Task 19, consumed Tasks 21, 22, 23, 25, 28.
- `VectorSearchHit` / `KeywordSearchHit` — Tasks 14, 16; consumed Tasks 18, 21.
- `IRagRetrievalService` / `IKeywordSearchService` — Tasks 20, 16; consumed Tasks 21, 25, 28 and registered Task 27.
- `ChunkLevel` enum — assumed to exist from Plan 4a; Task 19 notes to verify before use.
- `AiRequestType.QueryEmbedding` — added Task 3, consumed Tasks 21, 24.

**Known verification lookups (deliberate — each is bounded):**
- Task 6: domain error-throwing convention (check existing entities).
- Task 12: JSONB serialisation helper pattern (check `AiAssistantConfiguration`).
- Task 15: Qdrant payload key names (check `QdrantVectorStore.UpsertAsync`).
- Task 17: `AiDocumentChunk.ChunkLevel` stored-value casing.
- Task 21: `TokenCounter` method name and `AiDbContext` DbSet name.
- Task 25: `Result<T>` + `ICurrentUserService` namespaces.
- Task 26: controller base class + route convention.
- Task 28: `ChatExecutionService` constructor injection style (primary constructor vs classical).

These are intentionally described as one-line checks rather than hard-coded because the plan must not assert facts it hasn't verified. The engineer reads one file each before proceeding.
