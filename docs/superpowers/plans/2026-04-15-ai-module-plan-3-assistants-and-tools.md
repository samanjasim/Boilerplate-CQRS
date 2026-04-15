# AI Module — Plan 3: Assistants CRUD + Tool Registry + Function Calling

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the AI a first-class actor. Tenant admins can now create/edit/delete assistants through the API; any module can register its MediatR commands as AI-callable tools via `IAiToolDefinition`; the chat loop in `ChatExecutionService` resolves an assistant's enabled tools, calls the LLM with them, dispatches any returned tool calls back through MediatR (running as the current user, so the existing permission + validation + audit pipeline is enforced), feeds the results back to the LLM, and loops until the assistant produces a terminal text response. Streaming emits dedicated `tool_call` and `tool_result` SSE frames so the UI can render inline tool execution.

**Architecture:** Three new concerns layered on top of Plan 2's infrastructure:

1. **Assistants CRUD** — new MediatR commands/queries in `Application/Commands/{Create,Update,Delete}Assistant` + `Application/Queries/{GetAssistants,GetAssistantById}`, DTOs, validators, and a new `AiAssistantsController` gated by `Ai.ManageAssistants` (writes) and `Ai.ViewConversations` (reads — same audience as chat). Uses the existing `AiAssistant.Create/Update/SetEnabledTools/Activate/Deactivate` domain methods added in Plan 1.
2. **Tool registry** — new `IAiToolRegistry` interface (module-internal) backed by `AiToolRegistryService`. The service collects all `IAiToolDefinition` registrations at startup, upserts them as `AiTool` rows (code-defined tools are the source of truth — a code-side tool gone means the DB row is orphaned but harmless; new tools from modules appear automatically). An `AiToolRegistrySyncHostedService` runs the sync once on application startup, after the module's `MigrateAsync`. `AiToolsController` exposes `GET /tools` (list with filters) and `PUT /tools/{name}/toggle` (admin global enable/disable, `Ai.ManageTools`). The registry also provides a `ResolveForAssistantAsync(assistant, currentUser)` helper that returns the intersection of `assistant.EnabledToolNames ∩ registry.IsEnabled ∩ currentUser.HasPermission(tool.RequiredPermission)` — this is what the chat loop feeds to the provider.
3. **Function calling in the chat loop** — `ChatExecutionService.PrepareTurnAsync` now also resolves tools. After the provider returns (non-streaming) or the stream completes (streaming), if the assistant response includes `ToolCalls` rather than a finish reason of `stop`, the service:
   - persists an assistant message carrying the `ToolCalls` JSON;
   - for each tool call: re-checks permission, deserializes arguments to the command type, sends via `ISender.Send(cmd, ct)`, serializes the `Result<T>` back to a `ToolResult` message;
   - re-invokes the provider with the new message list;
   - iterates up to `assistant.MaxAgentSteps` steps (default 10, capped at 20 for chat mode) before emitting a terminal message noting the step limit.

   In streaming mode each tool-call frame is surfaced as `event: tool_call` with `{ callId, name, argumentsJson }`, each result as `event: tool_result` with `{ callId, isError, content }`, and the usual `delta`/`done` frames continue to carry content/end-of-stream.

**Tech Stack:** .NET 10, MediatR (`ISender` for tool invocation — already wired globally), EF Core (PostgreSQL, jsonb for `EnabledToolNames`), FluentValidation, the module's existing `AiProviderFactory`/`IAiProvider`, `System.Text.Json` for schema + args serialization. No new NuGet packages.

**Spec:** `docs/superpowers/specs/2026-04-13-ai-integration-module-design.md` — sections "Data Model → AiAssistant / AiTool / AiMessage", "Execution Engine → Function Calling → MediatR Bridge", "API Endpoints → Assistants (admin) / Tools (admin)", "Permissions".

**Plan series:** This is Plan 3 of ~8.
- Plan 1: Foundation + Provider Layer ✅
- Plan 2: Chat + Streaming ✅
- **Plan 3: Assistants CRUD + Tool Registry + Function Calling ← this plan**
- Plan 4: RAG Pipeline (document upload, chunking, embeddings, Qdrant, semantic search)
- Plan 5: Agent Engine (autonomous multi-step tasks, triggers)
- Plan 6: Web frontend — chat sidebar + streaming UI
- Plan 7: Web frontend — admin pages (Assistants/KB/Tools/Triggers/Usage)
- Plan 8: Mobile chat

**Out of scope for Plan 3 (intentional):**
- RAG context injection (Plan 4 — the assistant can have `KnowledgeBaseDocIds` set but it is not consumed by the chat loop yet)
- Multi-step autonomous agents (Plan 5 — chat loop tops out at `MaxAgentSteps` tool-call iterations; no background task engine)
- Triggers / scheduled execution (Plan 5)
- Frontend (Plans 6/7)
- Tool argument auto-generation from C# command types — each module hand-writes the `ParameterSchema` JSON. A schema generator is future work.
- Per-tenant tool enable/disable — global toggle only. Tenant-level overrides arrive with the admin UI in Plan 7 if needed.
- Semantic Kernel / AutoGen frameworks — we stay on the lightweight native provider SDK loop to keep dependencies minimal.
- Re-emitting text deltas interleaved with tool calls inside a single turn — Anthropic and OpenAI both allow text before a tool call; we currently capture final text after the last tool iteration and ignore the partial text that may precede a tool call inside one LLM round. Acceptable for v1; revisit if UX needs "assistant thinks out loud → calls tool → continues" flow.

---

## File Map

### New files in `Starter.Module.AI`

| File | Purpose |
|------|---------|
| `Application/DTOs/AiAssistantDto.cs` | Read model for list + detail endpoints |
| `Application/DTOs/AiAssistantMappers.cs` | `AiAssistant` → `AiAssistantDto` |
| `Application/DTOs/AiToolDto.cs` | Read model for tool list |
| `Application/DTOs/AiToolMappers.cs` | `AiTool` + `IAiToolDefinition` → `AiToolDto` |
| `Application/Commands/CreateAssistant/CreateAssistantCommand.cs` | Create request |
| `Application/Commands/CreateAssistant/CreateAssistantCommandValidator.cs` | FluentValidation |
| `Application/Commands/CreateAssistant/CreateAssistantCommandHandler.cs` | Handler |
| `Application/Commands/UpdateAssistant/UpdateAssistantCommand.cs` | Update request |
| `Application/Commands/UpdateAssistant/UpdateAssistantCommandValidator.cs` | FluentValidation |
| `Application/Commands/UpdateAssistant/UpdateAssistantCommandHandler.cs` | Handler |
| `Application/Commands/DeleteAssistant/DeleteAssistantCommand.cs` | Delete request |
| `Application/Commands/DeleteAssistant/DeleteAssistantCommandHandler.cs` | Handler (blocks when conversations exist) |
| `Application/Commands/ToggleTool/ToggleToolCommand.cs` | Global enable/disable |
| `Application/Commands/ToggleTool/ToggleToolCommandValidator.cs` | FluentValidation |
| `Application/Commands/ToggleTool/ToggleToolCommandHandler.cs` | Handler |
| `Application/Queries/GetAssistants/GetAssistantsQuery.cs` | Paged list |
| `Application/Queries/GetAssistants/GetAssistantsQueryHandler.cs` | Handler |
| `Application/Queries/GetAssistantById/GetAssistantByIdQuery.cs` | Single fetch |
| `Application/Queries/GetAssistantById/GetAssistantByIdQueryHandler.cs` | Handler |
| `Application/Queries/GetTools/GetToolsQuery.cs` | Paged list |
| `Application/Queries/GetTools/GetToolsQueryHandler.cs` | Handler |
| `Application/Services/IAiToolRegistry.cs` | Registry abstraction |
| `Application/Services/ToolResolutionResult.cs` | Record returned by `ResolveForAssistantAsync` |
| `Infrastructure/Services/AiToolRegistryService.cs` | Implementation — holds definitions + queries DB for enable state |
| `Infrastructure/Services/AiToolRegistrySyncHostedService.cs` | Runs DB upsert on startup |
| `Infrastructure/Tools/ListMyConversationsAiTool.cs` | Built-in demo tool (read-only, maps to `GetConversationsQuery`) |
| `Controllers/AiAssistantsController.cs` | CRUD endpoints |
| `Controllers/AiToolsController.cs` | Tool list + toggle |

### Modified files

| File | Change |
|------|--------|
| `Starter.Module.AI/AIModule.cs` | Register `IAiToolRegistry` → `AiToolRegistryService` (singleton), `AddHostedService<AiToolRegistrySyncHostedService>`, register the one built-in `IAiToolDefinition`. Seed a sample assistant in `MigrateAsync` when `AI:SeedSampleAssistant=true`. |
| `Starter.Module.AI/Application/Services/ChatExecutionService.cs` | `PrepareTurnAsync` resolves tools via `IAiToolRegistry`, passes them into `BuildChatOptions`. `ExecuteAsync` + `ExecuteStreamAsync` loop over `provider.ChatAsync` until `FinishReason != "tool_use"` or step limit reached. Persist an assistant message with `ToolCalls` JSON per round, a `ToolResult` message per call, emit `tool_call` / `tool_result` SSE frames. `FinalizeTurnAsync` writes the final assistant text message. |
| `Starter.Module.AI/Application/Services/ChatStreamEvent.cs` | Doc-comment the two new frame types (`tool_call`, `tool_result`). |
| `Starter.Module.AI/Application/Services/IChatExecutionService.cs` | No signature change — the tool loop lives behind the existing interface. |
| `Starter.Module.AI/Domain/Errors/AiErrors.cs` | Add `ToolArgumentsInvalid(name, detail)` and `ToolExecutionFailed(name, detail)` + reuse existing `ToolPermissionDenied`. |
| `Starter.Module.AI/Domain/Entities/AiAssistant.cs` | Add `SetActive(bool)` convenience method used by update flow. (No new state.) |
| `Starter.Module.AI/Infrastructure/Providers/AiProviderTypes.cs` | Confirm `AiToolDefinitionDto` exists (already does); no change needed. |
| `docs/API.md` (if present) | Append section "AI — Assistants & Tools" summarising the 7 new endpoints. (If no API.md exists, skip.) |

### Integration notes (no code changes required)

- `Starter.Api/Program.cs` already calls `ModuleLoader.DiscoverModules()` → `module.ConfigureServices(...)` so any new registrations inside `AIModule` are picked up.
- Handlers and validators in this module are scanned globally by `AddApplication(moduleAssemblies)` — no per-module MediatR/FluentValidation registration.
- `ISender` (MediatR) is registered globally and injectable into `ChatExecutionService` directly.
- `ICurrentUserService.HasPermission(string)` already exists on the interface (`boilerplateBE/src/Starter.Application/Common/Interfaces/ICurrentUserService.cs:12`), so the tool-permission check is a trivial in-memory call.

---

### Task 1: Assistant DTOs + mappers

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAssistantDto.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAssistantMappers.cs`

- [ ] **Step 1: Write the DTO**

Create `AiAssistantDto.cs`:

```csharp
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.DTOs;

public sealed record AiAssistantDto(
    Guid Id,
    string Name,
    string? Description,
    string SystemPrompt,
    AiProviderType? Provider,
    string? Model,
    double Temperature,
    int MaxTokens,
    IReadOnlyList<string> EnabledToolNames,
    IReadOnlyList<Guid> KnowledgeBaseDocIds,
    AssistantExecutionMode ExecutionMode,
    int MaxAgentSteps,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
```

- [ ] **Step 2: Write the mapper**

Create `AiAssistantMappers.cs`:

```csharp
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.DTOs;

internal static class AiAssistantMappers
{
    public static AiAssistantDto ToDto(this AiAssistant a) =>
        new(
            a.Id,
            a.Name,
            a.Description,
            a.SystemPrompt,
            a.Provider,
            a.Model,
            a.Temperature,
            a.MaxTokens,
            a.EnabledToolNames,
            a.KnowledgeBaseDocIds,
            a.ExecutionMode,
            a.MaxAgentSteps,
            a.IsActive,
            a.CreatedAt,
            a.ModifiedAt);
}
```

- [ ] **Step 3: Build**

Run: `dotnet build boilerplateBE/boilerplateBE.sln`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAssistantDto.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiAssistantMappers.cs
git commit -m "feat(ai): add AiAssistant DTO and mapper"
```

---

### Task 2: CreateAssistant command + handler + validator

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/CreateAssistant/CreateAssistantCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/CreateAssistant/CreateAssistantCommandValidator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/CreateAssistant/CreateAssistantCommandHandler.cs`

- [ ] **Step 1: Write the command**

```csharp
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.CreateAssistant;

public sealed record CreateAssistantCommand(
    string Name,
    string? Description,
    string SystemPrompt,
    AiProviderType? Provider,
    string? Model,
    double Temperature,
    int MaxTokens,
    AssistantExecutionMode ExecutionMode,
    int MaxAgentSteps,
    IReadOnlyList<string>? EnabledToolNames,
    IReadOnlyList<Guid>? KnowledgeBaseDocIds) : IRequest<Result<AiAssistantDto>>;
```

- [ ] **Step 2: Write the validator**

```csharp
using FluentValidation;

namespace Starter.Module.AI.Application.Commands.CreateAssistant;

public sealed class CreateAssistantCommandValidator : AbstractValidator<CreateAssistantCommand>
{
    public CreateAssistantCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.SystemPrompt).NotEmpty().MaximumLength(20_000);
        RuleFor(x => x.Model).MaximumLength(120);
        RuleFor(x => x.Temperature).InclusiveBetween(0.0, 2.0);
        RuleFor(x => x.MaxTokens).InclusiveBetween(1, 64_000);
        RuleFor(x => x.MaxAgentSteps).InclusiveBetween(1, 50);
        RuleForEach(x => x.EnabledToolNames!)
            .NotEmpty().MaximumLength(120)
            .When(x => x.EnabledToolNames is not null);
    }
}
```

- [ ] **Step 3: Write the handler**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.CreateAssistant;

internal sealed class CreateAssistantCommandHandler(
    AiDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateAssistantCommand, Result<AiAssistantDto>>
{
    public async Task<Result<AiAssistantDto>> Handle(
        CreateAssistantCommand request,
        CancellationToken cancellationToken)
    {
        // Name uniqueness is scoped per tenant. Platform admins (TenantId=null) share the
        // "global" namespace; tenant users collide only within their own tenant.
        var tenantId = currentUser.TenantId;
        var normalized = request.Name.Trim();

        var nameTaken = await context.AiAssistants
            .AnyAsync(a => a.Name == normalized, cancellationToken);
        if (nameTaken)
            return Result.Failure<AiAssistantDto>(AiErrors.AssistantNameAlreadyExists);

        var assistant = AiAssistant.Create(
            tenantId: tenantId,
            name: normalized,
            description: request.Description,
            systemPrompt: request.SystemPrompt,
            provider: request.Provider,
            model: request.Model,
            temperature: request.Temperature,
            maxTokens: request.MaxTokens,
            executionMode: request.ExecutionMode,
            maxAgentSteps: request.MaxAgentSteps,
            isActive: true);

        if (request.EnabledToolNames is { Count: > 0 })
            assistant.SetEnabledTools(request.EnabledToolNames);

        if (request.KnowledgeBaseDocIds is { Count: > 0 })
            assistant.SetKnowledgeBase(request.KnowledgeBaseDocIds);

        context.AiAssistants.Add(assistant);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(assistant.ToDto());
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build boilerplateBE/boilerplateBE.sln`
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/CreateAssistant/
git commit -m "feat(ai): add CreateAssistant command"
```

---

### Task 3: UpdateAssistant command + handler + validator

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/UpdateAssistant/UpdateAssistantCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/UpdateAssistant/UpdateAssistantCommandValidator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/UpdateAssistant/UpdateAssistantCommandHandler.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAssistant.cs`

- [ ] **Step 1: Add `SetActive(bool)` to `AiAssistant`**

In `AiAssistant.cs`, replace the two methods `Activate`/`Deactivate` with a single method (keep the originals — they stay as convenience wrappers):

```csharp
public void SetActive(bool isActive)
{
    IsActive = isActive;
    ModifiedAt = DateTime.UtcNow;
}

public void Deactivate() => SetActive(false);
public void Activate() => SetActive(true);
```

- [ ] **Step 2: Write the command**

```csharp
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.UpdateAssistant;

public sealed record UpdateAssistantCommand(
    Guid Id,
    string Name,
    string? Description,
    string SystemPrompt,
    AiProviderType? Provider,
    string? Model,
    double Temperature,
    int MaxTokens,
    AssistantExecutionMode ExecutionMode,
    int MaxAgentSteps,
    IReadOnlyList<string>? EnabledToolNames,
    IReadOnlyList<Guid>? KnowledgeBaseDocIds,
    bool IsActive) : IRequest<Result<AiAssistantDto>>;
```

- [ ] **Step 3: Write the validator**

```csharp
using FluentValidation;

namespace Starter.Module.AI.Application.Commands.UpdateAssistant;

public sealed class UpdateAssistantCommandValidator : AbstractValidator<UpdateAssistantCommand>
{
    public UpdateAssistantCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.SystemPrompt).NotEmpty().MaximumLength(20_000);
        RuleFor(x => x.Model).MaximumLength(120);
        RuleFor(x => x.Temperature).InclusiveBetween(0.0, 2.0);
        RuleFor(x => x.MaxTokens).InclusiveBetween(1, 64_000);
        RuleFor(x => x.MaxAgentSteps).InclusiveBetween(1, 50);
        RuleForEach(x => x.EnabledToolNames!)
            .NotEmpty().MaximumLength(120)
            .When(x => x.EnabledToolNames is not null);
    }
}
```

- [ ] **Step 4: Write the handler**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.UpdateAssistant;

internal sealed class UpdateAssistantCommandHandler(AiDbContext context)
    : IRequestHandler<UpdateAssistantCommand, Result<AiAssistantDto>>
{
    public async Task<Result<AiAssistantDto>> Handle(
        UpdateAssistantCommand request,
        CancellationToken cancellationToken)
    {
        var assistant = await context.AiAssistants
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (assistant is null)
            return Result.Failure<AiAssistantDto>(AiErrors.AssistantNotFound);

        // Uniqueness scoped within what the tenant can see (global filter already applied).
        var normalized = request.Name.Trim();
        if (normalized != assistant.Name)
        {
            var nameTaken = await context.AiAssistants
                .AnyAsync(a => a.Id != assistant.Id && a.Name == normalized, cancellationToken);
            if (nameTaken)
                return Result.Failure<AiAssistantDto>(AiErrors.AssistantNameAlreadyExists);
        }

        assistant.Update(
            name: normalized,
            description: request.Description,
            systemPrompt: request.SystemPrompt,
            provider: request.Provider,
            model: request.Model,
            temperature: request.Temperature,
            maxTokens: request.MaxTokens,
            executionMode: request.ExecutionMode,
            maxAgentSteps: request.MaxAgentSteps);

        assistant.SetEnabledTools(request.EnabledToolNames ?? Array.Empty<string>());
        assistant.SetKnowledgeBase(request.KnowledgeBaseDocIds ?? Array.Empty<Guid>());
        assistant.SetActive(request.IsActive);

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(assistant.ToDto());
    }
}
```

- [ ] **Step 5: Build**

Run: `dotnet build boilerplateBE/boilerplateBE.sln`
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/UpdateAssistant/ \
        boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAssistant.cs
git commit -m "feat(ai): add UpdateAssistant command"
```

---

### Task 4: DeleteAssistant command + handler

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/DeleteAssistant/DeleteAssistantCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/DeleteAssistant/DeleteAssistantCommandHandler.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/AiErrors.cs`

- [ ] **Step 1: Add a guard error**

Append to `AiErrors.cs`:

```csharp
public static Error AssistantInUse => Error.Conflict(
    "Ai.AssistantInUse",
    "This assistant has conversations and cannot be deleted. Deactivate it instead.");
```

- [ ] **Step 2: Write the command**

```csharp
using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.DeleteAssistant;

public sealed record DeleteAssistantCommand(Guid Id) : IRequest<Result>;
```

- [ ] **Step 3: Write the handler**

The handler refuses deletion whenever a conversation references the assistant — conversations carry token usage + audit trail that must not vanish. Admins can `IsActive=false` via `UpdateAssistant` to hide an assistant without destroying history.

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.DeleteAssistant;

internal sealed class DeleteAssistantCommandHandler(AiDbContext context)
    : IRequestHandler<DeleteAssistantCommand, Result>
{
    public async Task<Result> Handle(
        DeleteAssistantCommand request,
        CancellationToken cancellationToken)
    {
        var assistant = await context.AiAssistants
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (assistant is null)
            return Result.Failure(AiErrors.AssistantNotFound);

        var inUse = await context.AiConversations
            .AnyAsync(c => c.AssistantId == assistant.Id, cancellationToken);
        if (inUse)
            return Result.Failure(AiErrors.AssistantInUse);

        context.AiAssistants.Remove(assistant);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build boilerplateBE/boilerplateBE.sln`
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/DeleteAssistant/ \
        boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/AiErrors.cs
git commit -m "feat(ai): add DeleteAssistant command with in-use guard"
```

---

### Task 5: Assistant queries (list + by id)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetAssistants/GetAssistantsQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetAssistants/GetAssistantsQueryHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetAssistantById/GetAssistantByIdQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetAssistantById/GetAssistantByIdQueryHandler.cs`

- [ ] **Step 1: Write the list query**

```csharp
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Models;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetAssistants;

public sealed record GetAssistantsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    bool? IsActive = null) : IRequest<PagedResult<AiAssistantDto>>;
```

- [ ] **Step 2: Write the list handler**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Models;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetAssistants;

internal sealed class GetAssistantsQueryHandler(AiDbContext context)
    : IRequestHandler<GetAssistantsQuery, PagedResult<AiAssistantDto>>
{
    public async Task<PagedResult<AiAssistantDto>> Handle(
        GetAssistantsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.AiAssistants.AsNoTracking().AsQueryable();

        if (request.IsActive is bool active)
            query = query.Where(a => a.IsActive == active);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(a =>
                a.Name.ToLower().Contains(term) ||
                (a.Description != null && a.Description.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(a => a.ToDto()).ToList();
        return PagedResult<AiAssistantDto>.Success(dtos, total, request.PageNumber, request.PageSize);
    }
}
```

- [ ] **Step 3: Write the single-fetch query**

```csharp
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetAssistantById;

public sealed record GetAssistantByIdQuery(Guid Id) : IRequest<Result<AiAssistantDto>>;
```

- [ ] **Step 4: Write the single-fetch handler**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetAssistantById;

internal sealed class GetAssistantByIdQueryHandler(AiDbContext context)
    : IRequestHandler<GetAssistantByIdQuery, Result<AiAssistantDto>>
{
    public async Task<Result<AiAssistantDto>> Handle(
        GetAssistantByIdQuery request,
        CancellationToken cancellationToken)
    {
        var assistant = await context.AiAssistants.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (assistant is null)
            return Result.Failure<AiAssistantDto>(AiErrors.AssistantNotFound);

        return Result.Success(assistant.ToDto());
    }
}
```

- [ ] **Step 5: Build**

Run: `dotnet build boilerplateBE/boilerplateBE.sln`
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetAssistants/ \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetAssistantById/
git commit -m "feat(ai): add assistant list + get-by-id queries"
```

---

### Task 6: Assistants controller

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiAssistantsController.cs`

- [ ] **Step 1: Write the controller**

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.AI.Application.Commands.CreateAssistant;
using Starter.Module.AI.Application.Commands.DeleteAssistant;
using Starter.Module.AI.Application.Commands.UpdateAssistant;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.GetAssistantById;
using Starter.Module.AI.Application.Queries.GetAssistants;
using Starter.Module.AI.Constants;
using Starter.Shared.Models;
using Starter.Shared.Results;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/assistants")]
public sealed class AiAssistantsController(ISender mediator)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.ViewConversations)]
    [ProducesResponseType(typeof(PagedApiResponse<AiAssistantDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new GetAssistantsQuery(pageNumber, pageSize, searchTerm, isActive), ct);
        return HandlePagedResult(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AiPermissions.ViewConversations)]
    [ProducesResponseType(typeof(ApiResponse<AiAssistantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetAssistantByIdQuery(id), ct);
        return HandleResult(result);
    }

    [HttpPost]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(typeof(ApiResponse<AiAssistantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAssistantCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(typeof(ApiResponse<AiAssistantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAssistantCommand command,
        CancellationToken ct = default)
    {
        if (id != command.Id) return BadRequest();
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AiPermissions.ManageAssistants)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteAssistantCommand(id), ct);
        return HandleResult(result);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build boilerplateBE/boilerplateBE.sln`
Expected: 0 errors.

- [ ] **Step 3: Smoke test**

Start the API (`cd boilerplateBE/src/Starter.Api && dotnet run --launch-profile http`). Obtain a SuperAdmin JWT, then:

```bash
# Create
curl -s -X POST http://localhost:5000/api/v1/ai/assistants \
  -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" \
  -d '{"name":"HR","description":"HR helper","systemPrompt":"You help with HR.","temperature":0.5,"maxTokens":1024,"executionMode":0,"maxAgentSteps":10,"enabledToolNames":[],"knowledgeBaseDocIds":[]}'
# Expected: ApiResponse<AiAssistantDto> with IsActive=true, CreatedAt populated.

# List
curl -s "http://localhost:5000/api/v1/ai/assistants?pageNumber=1&pageSize=10" \
  -H "Authorization: Bearer $JWT"
# Expected: PagedApiResponse with the "HR" assistant + any previously-seeded rows.
```

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiAssistantsController.cs
git commit -m "feat(ai): add AiAssistantsController with CRUD endpoints"
```

---

### Task 7: Tool DTO + mapper

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiToolDto.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiToolMappers.cs`

- [ ] **Step 1: Write the DTO**

The DTO merges two concerns: the code-defined definition (from `IAiToolDefinition`) and the DB-tracked enable state (from `AiTool`). The Name is the join key. `ParameterSchema` is emitted as raw JSON so clients can render it directly.

```csharp
using System.Text.Json;

namespace Starter.Module.AI.Application.DTOs;

public sealed record AiToolDto(
    string Name,
    string Description,
    string Category,
    string RequiredPermission,
    bool IsReadOnly,
    bool IsEnabled,
    JsonElement ParameterSchema);
```

- [ ] **Step 2: Write the mapper**

```csharp
using System.Text.Json;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.DTOs;

internal static class AiToolMappers
{
    public static AiToolDto ToDto(this IAiToolDefinition definition, AiTool? dbRow) =>
        new(
            definition.Name,
            definition.Description,
            definition.Category,
            definition.RequiredPermission,
            definition.IsReadOnly,
            // New tools default to enabled; admin can disable.
            IsEnabled: dbRow?.IsEnabled ?? true,
            definition.ParameterSchema);

    public static AiToolDto ToDto(this AiTool row, IAiToolDefinition definition) =>
        new(
            row.Name,
            row.Description,
            row.Category,
            row.RequiredPermission,
            row.IsReadOnly,
            row.IsEnabled,
            definition.ParameterSchema);
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build boilerplateBE/boilerplateBE.sln
git add boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiToolDto.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiToolMappers.cs
git commit -m "feat(ai): add AiTool DTO and mappers"
```

---

### Task 8: IAiToolRegistry + resolution result

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/IAiToolRegistry.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ToolResolutionResult.cs`

- [ ] **Step 1: Write the registry interface**

```csharp
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.Services;

/// <summary>
/// Abstraction over the set of AI tools available at runtime. The code-side registrations
/// (IAiToolDefinition) are the source of truth for schemas; the DB (AiTool) tracks admin
/// enable/disable state. The registry joins the two.
/// </summary>
internal interface IAiToolRegistry
{
    /// <summary>All tools known to the system, joined with their DB enable state. Used by the
    /// admin Tools controller — no permission filtering.</summary>
    Task<IReadOnlyList<AiToolDto>> ListAllAsync(CancellationToken ct);

    /// <summary>Look up a single tool by its code-defined name.</summary>
    IAiToolDefinition? FindByName(string name);

    /// <summary>
    /// Resolve the tools available to a single chat turn: intersection of
    /// (assistant.EnabledToolNames) × (registry.IsEnabled in DB) × (user.HasPermission).
    /// Returns a ToolResolutionResult with the provider-facing definitions plus the
    /// per-name lookup needed to dispatch results.
    /// </summary>
    Task<ToolResolutionResult> ResolveForAssistantAsync(
        AiAssistant assistant,
        CancellationToken ct);
}
```

- [ ] **Step 2: Write the resolution result**

The record carries both the provider-facing list and a fast lookup the chat loop uses when dispatching tool calls (avoids re-joining on every call).

```csharp
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Module.AI.Application.Services;

internal sealed record ToolResolutionResult(
    IReadOnlyList<AiToolDefinitionDto> ProviderTools,
    IReadOnlyDictionary<string, IAiToolDefinition> DefinitionsByName);
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build boilerplateBE/boilerplateBE.sln
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/IAiToolRegistry.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ToolResolutionResult.cs
git commit -m "feat(ai): add IAiToolRegistry abstraction"
```

---

### Task 9: AiToolRegistryService implementation

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/AiToolRegistryService.cs`

- [ ] **Step 1: Write the service**

Behaviour notes in-line in the file:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Module.AI.Infrastructure.Services;

internal sealed class AiToolRegistryService(
    IEnumerable<IAiToolDefinition> definitions,
    IServiceScopeFactory scopeFactory,
    ICurrentUserService currentUser)
    : IAiToolRegistry
{
    // Definitions are singleton DI registrations — snapshot them once.
    private readonly IReadOnlyDictionary<string, IAiToolDefinition> _byName =
        definitions
            .GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            // First-wins when a duplicate name is registered — the sync service will log
            // the duplicate so the developer can rename.
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    public IAiToolDefinition? FindByName(string name) =>
        _byName.TryGetValue(name, out var d) ? d : null;

    public async Task<IReadOnlyList<AiToolDto>> ListAllAsync(CancellationToken ct)
    {
        // Use a short-lived scope so the registry (singleton) can safely resolve a scoped
        // DbContext for the sync query.
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();

        var rows = await db.AiTools.AsNoTracking().ToListAsync(ct);
        var rowByName = rows.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

        return _byName.Values
            .OrderBy(d => d.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => d.ToDto(rowByName.GetValueOrDefault(d.Name)))
            .ToList();
    }

    public async Task<ToolResolutionResult> ResolveForAssistantAsync(
        AiAssistant assistant,
        CancellationToken ct)
    {
        if (assistant.EnabledToolNames.Count == 0)
            return EmptyResolution;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();

        // Load the enable-state rows in one round trip.
        var enabledRowNames = new HashSet<string>(
            await db.AiTools.AsNoTracking()
                .Where(t => t.IsEnabled)
                .Select(t => t.Name)
                .ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        var resolved = new List<(AiToolDefinitionDto dto, IAiToolDefinition def)>();

        foreach (var name in assistant.EnabledToolNames)
        {
            if (!_byName.TryGetValue(name, out var def)) continue;       // stale assistant config
            if (!enabledRowNames.Contains(def.Name)) continue;           // globally disabled
            if (!currentUser.HasPermission(def.RequiredPermission)) continue; // user not allowed

            resolved.Add((
                new AiToolDefinitionDto(def.Name, def.Description, def.ParameterSchema),
                def));
        }

        if (resolved.Count == 0)
            return EmptyResolution;

        return new ToolResolutionResult(
            resolved.Select(r => r.dto).ToList(),
            resolved.ToDictionary(r => r.def.Name, r => r.def, StringComparer.OrdinalIgnoreCase));
    }

    private static readonly ToolResolutionResult EmptyResolution = new(
        Array.Empty<AiToolDefinitionDto>(),
        new Dictionary<string, IAiToolDefinition>(StringComparer.OrdinalIgnoreCase));
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build boilerplateBE/boilerplateBE.sln
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/AiToolRegistryService.cs
git commit -m "feat(ai): add AiToolRegistryService"
```

---

### Task 10: Startup sync hosted service

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/AiToolRegistrySyncHostedService.cs`

The hosted service upserts every registered `IAiToolDefinition` into the `AiTool` table once on boot. New tools land as enabled; existing rows keep their admin-set `IsEnabled` state but get their description/schema refreshed. Tools removed from code stay in the DB as orphans (they simply never appear in `ListAllAsync` because `IAiToolDefinition` is gone — a follow-up cleanup job can purge them if the list grows). Duplicate names are logged as warnings.

- [ ] **Step 1: Write the hosted service**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Services;

internal sealed class AiToolRegistrySyncHostedService(
    IServiceScopeFactory scopeFactory,
    IEnumerable<IAiToolDefinition> definitions,
    ILogger<AiToolRegistrySyncHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var distinct = new Dictionary<string, IAiToolDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in definitions)
        {
            if (!distinct.TryAdd(def.Name, def))
                logger.LogWarning(
                    "Duplicate IAiToolDefinition registered for '{Name}'. Keeping first.", def.Name);
        }

        if (distinct.Count == 0)
        {
            logger.LogInformation("No IAiToolDefinition registrations to sync.");
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();

        var existingRows = await db.AiTools.ToDictionaryAsync(
            t => t.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var added = 0;
        var updated = 0;

        foreach (var def in distinct.Values)
        {
            var schemaJson = def.ParameterSchema.GetRawText();
            if (existingRows.TryGetValue(def.Name, out var row))
            {
                // Rehydrate schema/description/permission while preserving IsEnabled.
                // The domain entity exposes only Toggle/Create, so we use a dedicated
                // method added in this task.
                row.RefreshFromDefinition(
                    description: def.Description,
                    commandType: def.CommandType.AssemblyQualifiedName ?? def.CommandType.FullName!,
                    requiredPermission: def.RequiredPermission,
                    category: def.Category,
                    parameterSchema: schemaJson,
                    isReadOnly: def.IsReadOnly);
                updated++;
            }
            else
            {
                var tool = AiTool.Create(
                    name: def.Name,
                    description: def.Description,
                    commandType: def.CommandType.AssemblyQualifiedName ?? def.CommandType.FullName!,
                    requiredPermission: def.RequiredPermission,
                    category: def.Category,
                    parameterSchema: schemaJson,
                    isEnabled: true,
                    isReadOnly: def.IsReadOnly);
                db.AiTools.Add(tool);
                added++;
            }
        }

        if (added + updated > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "AI tool registry synced. Added={Added} Updated={Updated}", added, updated);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

- [ ] **Step 2: Add `RefreshFromDefinition` to `AiTool`**

Modify `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiTool.cs` — append:

```csharp
public void RefreshFromDefinition(
    string description,
    string commandType,
    string requiredPermission,
    string category,
    string parameterSchema,
    bool isReadOnly)
{
    Description = description.Trim();
    CommandType = commandType.Trim();
    RequiredPermission = requiredPermission.Trim();
    Category = category.Trim();
    ParameterSchema = parameterSchema;
    IsReadOnly = isReadOnly;
    ModifiedAt = DateTime.UtcNow;
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build boilerplateBE/boilerplateBE.sln
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/AiToolRegistrySyncHostedService.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiTool.cs
git commit -m "feat(ai): add AI tool registry startup sync service"
```

---

### Task 11: GetTools query + ToggleTool command

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetTools/GetToolsQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetTools/GetToolsQueryHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/ToggleTool/ToggleToolCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/ToggleTool/ToggleToolCommandValidator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/ToggleTool/ToggleToolCommandHandler.cs`

- [ ] **Step 1: Write the list query**

```csharp
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetTools;

public sealed record GetToolsQuery(
    string? Category = null,
    bool? IsEnabled = null,
    string? SearchTerm = null) : IRequest<Result<IReadOnlyList<AiToolDto>>>;
```

- [ ] **Step 2: Write the list handler**

```csharp
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetTools;

internal sealed class GetToolsQueryHandler(IAiToolRegistry registry)
    : IRequestHandler<GetToolsQuery, Result<IReadOnlyList<AiToolDto>>>
{
    public async Task<Result<IReadOnlyList<AiToolDto>>> Handle(
        GetToolsQuery request,
        CancellationToken cancellationToken)
    {
        var all = await registry.ListAllAsync(cancellationToken);

        IEnumerable<AiToolDto> q = all;

        if (!string.IsNullOrWhiteSpace(request.Category))
            q = q.Where(t => string.Equals(t.Category, request.Category, StringComparison.OrdinalIgnoreCase));

        if (request.IsEnabled is bool enabled)
            q = q.Where(t => t.IsEnabled == enabled);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            q = q.Where(t =>
                t.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return Result.Success<IReadOnlyList<AiToolDto>>(q.ToList());
    }
}
```

- [ ] **Step 3: Write the toggle command**

```csharp
using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.ToggleTool;

public sealed record ToggleToolCommand(string Name, bool IsEnabled) : IRequest<Result>;
```

- [ ] **Step 4: Write the validator**

```csharp
using FluentValidation;

namespace Starter.Module.AI.Application.Commands.ToggleTool;

public sealed class ToggleToolCommandValidator : AbstractValidator<ToggleToolCommand>
{
    public ToggleToolCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
    }
}
```

- [ ] **Step 5: Write the toggle handler**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.ToggleTool;

internal sealed class ToggleToolCommandHandler(AiDbContext context)
    : IRequestHandler<ToggleToolCommand, Result>
{
    public async Task<Result> Handle(
        ToggleToolCommand request,
        CancellationToken cancellationToken)
    {
        var tool = await context.AiTools
            .FirstOrDefaultAsync(t => t.Name == request.Name, cancellationToken);
        if (tool is null)
            return Result.Failure(AiErrors.ToolNotFound);

        tool.Toggle(request.IsEnabled);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 6: Build + commit**

```bash
dotnet build boilerplateBE/boilerplateBE.sln
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetTools/ \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/ToggleTool/
git commit -m "feat(ai): add GetTools query and ToggleTool command"
```

---

### Task 12: Tools controller

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiToolsController.cs`

- [ ] **Step 1: Write the controller**

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.AI.Application.Commands.ToggleTool;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.GetTools;
using Starter.Module.AI.Constants;
using Starter.Shared.Results;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/tools")]
public sealed class AiToolsController(ISender mediator)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.ManageTools)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AiToolDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? category = null,
        [FromQuery] bool? isEnabled = null,
        [FromQuery] string? searchTerm = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new GetToolsQuery(category, isEnabled, searchTerm), ct);
        return HandleResult(result);
    }

    [HttpPut("{name}/toggle")]
    [Authorize(Policy = AiPermissions.ManageTools)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Toggle(
        string name,
        [FromBody] ToggleToolBody body,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ToggleToolCommand(name, body.IsEnabled), ct);
        return HandleResult(result);
    }

    public sealed record ToggleToolBody(bool IsEnabled);
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build boilerplateBE/boilerplateBE.sln
git add boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiToolsController.cs
git commit -m "feat(ai): add AiToolsController with list and toggle"
```

---

### Task 13: Built-in demo tool — `list_my_conversations`

The AI module itself ships one small read-only tool so function calling is end-to-end verifiable without requiring another feature module to register tools. The tool maps to the existing `GetConversationsQuery` (Plan 2) and returns the caller's 10 most recent conversations.

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Tools/ListMyConversationsAiTool.cs`

- [ ] **Step 1: Write the tool definition**

```csharp
using System.Text.Json;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Application.Queries.GetConversations;
using Starter.Module.AI.Constants;

namespace Starter.Module.AI.Infrastructure.Tools;

internal sealed class ListMyConversationsAiTool : IAiToolDefinition
{
    private static readonly JsonElement Schema = JsonDocument.Parse("""
    {
      "type": "object",
      "properties": {
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many recent conversations to return (default 10)."
        }
      },
      "additionalProperties": false
    }
    """).RootElement;

    public string Name => "list_my_conversations";
    public string Description =>
        "List the current user's recent AI conversations with title, message count, and last-message timestamp.";
    public JsonElement ParameterSchema => Schema;
    public Type CommandType => typeof(GetConversationsQuery);
    public string RequiredPermission => AiPermissions.ViewConversations;
    public string Category => "AI";
    public bool IsReadOnly => true;
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build boilerplateBE/boilerplateBE.sln
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Tools/ListMyConversationsAiTool.cs
git commit -m "feat(ai): add built-in list_my_conversations AI tool"
```

---

### Task 14: Wire everything into `AIModule.cs`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

- [ ] **Step 1: Register registry, hosted service, built-in tool**

In `AIModule.ConfigureServices`, after the existing registrations, add:

```csharp
services.AddSingleton<IAiToolRegistry, AiToolRegistryService>();
services.AddSingleton<IAiToolDefinition, Infrastructure.Tools.ListMyConversationsAiTool>();
services.AddHostedService<Infrastructure.Services.AiToolRegistrySyncHostedService>();
```

Required `using` additions: `Starter.Abstractions.Capabilities;`, `Starter.Module.AI.Application.Services;`.

- [ ] **Step 2: Build + run + verify sync**

```bash
dotnet build boilerplateBE/boilerplateBE.sln
cd boilerplateBE/src/Starter.Api && dotnet run --launch-profile http &
sleep 6
psql -U postgres -d starterdb -c "SELECT name, category, is_enabled, is_read_only FROM ai_tools;"
```

Expected: a single row `list_my_conversations | AI | t | t`. Startup log line: `AI tool registry synced. Added=1 Updated=0`. Stop the API.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs
git commit -m "feat(ai): register tool registry, sync service, and built-in tool"
```

---

### Task 15: Chat loop — wire tool resolution into `PrepareTurnAsync`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs`

The `ChatTurnState` record gains a `ToolResolutionResult Tools` field. `PrepareTurnAsync` calls `toolRegistry.ResolveForAssistantAsync(assistant, ct)` after the quota check and stores it in state. `BuildChatOptions` takes the provider tools list and plugs it into `AiChatOptions.Tools`.

- [ ] **Step 1: Inject the registry + ISender**

Change the `ChatExecutionService` primary constructor to also take `IAiToolRegistry toolRegistry` and `ISender sender`. Add the `using`:

```csharp
using MediatR;
using Starter.Module.AI.Infrastructure.Providers;
```

- [ ] **Step 2: Extend `ChatTurnState`**

Replace the existing `ChatTurnState` record with:

```csharp
private sealed record ChatTurnState(
    AiConversation Conversation,
    AiAssistant Assistant,
    AiMessage UserMessage,
    List<AiChatMessage> ProviderMessages,
    int NextOrder,
    ToolResolutionResult Tools);
```

- [ ] **Step 3: Resolve tools in `PrepareTurnAsync`**

At the end of `PrepareTurnAsync`, right before the `return Result.Success(new ChatTurnState(...))` line, insert:

```csharp
var toolResolution = await toolRegistry.ResolveForAssistantAsync(assistant, ct);
```

Pass `toolResolution` as the new `Tools` argument of `ChatTurnState`.

- [ ] **Step 4: Pass tools into `BuildChatOptions`**

Replace the existing `BuildChatOptions` helper:

```csharp
private static AiChatOptions BuildChatOptions(
    AiAssistant assistant,
    IReadOnlyList<AiToolDefinitionDto> tools) =>
    new(
        Model: assistant.Model ?? "",
        Temperature: assistant.Temperature,
        MaxTokens: assistant.MaxTokens,
        SystemPrompt: assistant.SystemPrompt,
        Tools: tools.Count == 0 ? null : tools);
```

Update the two callers (one in `ExecuteAsync`, one in `ExecuteStreamAsync`):

```csharp
var chatOptions = BuildChatOptions(state.Assistant, state.Tools.ProviderTools);
```

- [ ] **Step 5: Build**

Run: `dotnet build boilerplateBE/boilerplateBE.sln`
Expected: 0 errors. No behavioural changes yet (tools are passed to provider but providers already ignore null/empty tool lists).

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs
git commit -m "refactor(ai): plumb tool resolution into chat turn state"
```

---

### Task 16: Non-streaming tool-call loop in `ExecuteAsync`

`ChatExecutionService.ExecuteAsync` currently does a single provider call. It now loops while the provider returns tool calls:

```
loop up to assistant.MaxAgentSteps:
    completion = provider.ChatAsync(messages, opts, ct)
    if completion.ToolCalls is null or empty:
        break                               # normal text termination
    persist assistant turn with ToolCalls JSON (Content may be null or partial text)
    for each call:
        lookup definition in state.Tools.DefinitionsByName
        re-check permission
        deserialize arguments JSON into definition.CommandType
        send via ISender.Send(cmd, ct) → Result<T>
        serialize the Result back to JSON
        append tool-result message + feed to providerMessages as role="tool"
    # next loop iteration re-asks the provider with the new messages
FinalizeTurnAsync(state, completion.Content, cumulative tokens, ct)
```

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/AiErrors.cs`

- [ ] **Step 1: Add two specific errors**

Append to `AiErrors.cs`:

```csharp
public static Error ToolArgumentsInvalid(string toolName, string detail) => Error.Validation(
    "Ai.ToolArgumentsInvalid",
    $"Invalid arguments for tool '{toolName}': {detail}");

public static Error ToolExecutionFailed(string toolName, string detail) => Error.Failure(
    "Ai.ToolExecutionFailed",
    $"Tool '{toolName}' execution failed: {detail}");
```

- [ ] **Step 2: Introduce a private dispatcher**

In `ChatExecutionService.cs`, add a new private method. Error semantics: any exception/failure results in a tool-result message whose JSON carries `{ "ok": false, "error": "<detail>" }` so the LLM can recover; we never throw out of a single tool call.

```csharp
private async Task<string> DispatchToolAsync(
    AiToolCall call,
    ToolResolutionResult tools,
    CancellationToken ct)
{
    if (!tools.DefinitionsByName.TryGetValue(call.Name, out var def))
        return SerializeError($"Unknown tool '{call.Name}'.");

    if (!currentUser.HasPermission(def.RequiredPermission))
        return SerializeError($"Permission '{def.RequiredPermission}' required.");

    object? command;
    try
    {
        command = System.Text.Json.JsonSerializer.Deserialize(
            call.ArgumentsJson,
            def.CommandType,
            SerializerOptions);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to deserialize args for tool {Tool}.", call.Name);
        return SerializeError($"Arguments could not be deserialized: {ex.Message}");
    }

    if (command is null)
        return SerializeError("Deserialized arguments were null.");

    try
    {
        var result = await sender.Send(command, ct);
        // Serialize the whole Result<T> (or bool from non-generic Result) — the LLM gets to
        // read `isSuccess`, `error`, and `value` and decide what to do next.
        return System.Text.Json.JsonSerializer.Serialize(result, SerializerOptions);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Tool {Tool} threw during dispatch.", call.Name);
        return SerializeError(ex.Message);
    }

    static string SerializeError(string message) =>
        System.Text.Json.JsonSerializer.Serialize(new { ok = false, error = message });
}

private static readonly System.Text.Json.JsonSerializerOptions SerializerOptions =
    new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
```

- [ ] **Step 3: Rewrite `ExecuteAsync` to loop**

Replace the existing body:

```csharp
public async Task<Result<AiChatReplyDto>> ExecuteAsync(
    Guid? conversationId,
    Guid? assistantId,
    string userMessage,
    CancellationToken ct = default)
{
    var stateResult = await PrepareTurnAsync(conversationId, assistantId, userMessage, ct);
    if (stateResult.IsFailure)
        return Result.Failure<AiChatReplyDto>(stateResult.Error);

    var state = stateResult.Value;
    var provider = providerFactory.Create(ResolveProvider(state.Assistant));
    var chatOptions = BuildChatOptions(state.Assistant, state.Tools.ProviderTools);

    var messages = new List<AiChatMessage>(state.ProviderMessages);
    var totalInput = 0;
    var totalOutput = 0;
    var stepBudget = Math.Clamp(state.Assistant.MaxAgentSteps, 1, 20);
    var nextOrder = state.NextOrder;

    AiChatCompletion completion;
    try
    {
        for (var step = 0; step < stepBudget; step++)
        {
            completion = await provider.ChatAsync(messages, chatOptions, ct);
            totalInput += completion.InputTokens;
            totalOutput += completion.OutputTokens;

            if (completion.ToolCalls is null || completion.ToolCalls.Count == 0)
            {
                var finalMessage = await FinalizeTurnAsync(
                    state, completion.Content, totalInput, totalOutput, nextOrder, ct);
                return Result.Success(new AiChatReplyDto(
                    state.Conversation.Id,
                    state.UserMessage.ToDto(),
                    finalMessage.ToDto()));
            }

            // Tool-call turn — persist the assistant's request + feed it back to the provider.
            var toolCallsJson = System.Text.Json.JsonSerializer.Serialize(
                completion.ToolCalls, SerializerOptions);

            var assistantCallMsg = AiMessage.CreateAssistantMessage(
                state.Conversation.Id,
                completion.Content ?? "",
                nextOrder++,
                completion.InputTokens,
                completion.OutputTokens,
                toolCalls: toolCallsJson);
            context.AiMessages.Add(assistantCallMsg);
            messages.Add(new AiChatMessage(
                "assistant", completion.Content, ToolCalls: completion.ToolCalls));

            foreach (var call in completion.ToolCalls)
            {
                var resultJson = await DispatchToolAsync(call, state.Tools, ct);

                var toolResultMsg = AiMessage.CreateToolResultMessage(
                    state.Conversation.Id, call.Id, resultJson, nextOrder++);
                context.AiMessages.Add(toolResultMsg);
                messages.Add(new AiChatMessage("tool", resultJson, ToolCallId: call.Id));
            }

            await context.SaveChangesAsync(ct);
        }

        // Hit step budget — finalize with a note.
        var hitLimitMsg = await FinalizeTurnAsync(
            state,
            "I couldn't fully complete the task within my step budget. Please narrow the request.",
            totalInput, totalOutput, nextOrder, ct);
        return Result.Success(new AiChatReplyDto(
            state.Conversation.Id,
            state.UserMessage.ToDto(),
            hitLimitMsg.ToDto()));
    }
    catch (Exception ex)
    {
        await FailTurnAsync(state);
        return Result.Failure<AiChatReplyDto>(AiErrors.ProviderError(ex.Message));
    }
}
```

- [ ] **Step 4: Update `FinalizeTurnAsync` signature to accept `nextOrder`**

Current signature skips `nextOrder` (implied `state.NextOrder`). The loop advances `nextOrder` each round, so `FinalizeTurnAsync` must accept it explicitly.

```csharp
private async Task<AiMessage> FinalizeTurnAsync(
    ChatTurnState state,
    string? content,
    int inputTokens,
    int outputTokens,
    int order,
    CancellationToken ct)
{
    var assistantMessage = AiMessage.CreateAssistantMessage(
        state.Conversation.Id,
        content ?? "",
        order,          // <-- was state.NextOrder
        inputTokens,
        outputTokens);

    // ... rest of method unchanged
}
```

Update the streaming caller in step 8 later accordingly — for now the streaming path in `ExecuteStreamAsync` still passes `state.NextOrder`, which compiles fine.

- [ ] **Step 5: Build**

Run: `dotnet build boilerplateBE/boilerplateBE.sln`
Expected: 0 errors.

- [ ] **Step 6: Smoke test — create assistant + enable tool + call chat**

```bash
# Create an assistant with the demo tool enabled
curl -s -X POST http://localhost:5000/api/v1/ai/assistants \
  -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" \
  -d '{"name":"Tool Tester","description":"Test","systemPrompt":"When the user asks for their conversations, call list_my_conversations and summarise.","temperature":0.2,"maxTokens":1024,"executionMode":0,"maxAgentSteps":5,"enabledToolNames":["list_my_conversations"],"knowledgeBaseDocIds":[]}' | tee /tmp/assistant.json

ASST_ID=$(jq -r '.data.id' /tmp/assistant.json)

# Trigger a tool call
curl -s -X POST http://localhost:5000/api/v1/ai/chat \
  -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" \
  -d "{\"assistantId\":\"$ASST_ID\",\"message\":\"Show me my recent conversations.\"}"
```

Expected: a final assistant message whose content references the conversations. Check `SELECT role, "order", length(content), tool_calls IS NOT NULL AS has_tools, tool_call_id FROM ai_messages ORDER BY "order";` — should show `User`, `Assistant` with `tool_calls`, `ToolResult`, and `Assistant` (final text) rows.

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/AiErrors.cs
git commit -m "feat(ai): add tool-call loop to non-streaming chat"
```

---

### Task 17: Streaming tool-call loop in `ExecuteStreamAsync`

Streaming adds two new SSE frame types:

- `tool_call` — emitted once per tool invocation with `{ callId, name, argumentsJson }` so the UI can render "Calling X…".
- `tool_result` — emitted after dispatch with `{ callId, isError, content }` so the UI can render the outcome.

Architecture: each provider round is still consumed via `StreamChatAsync`, but now we collect tool-call deltas as they accumulate. When the round ends with tool calls, we emit `tool_call` + dispatch + emit `tool_result`, then re-invoke the provider (non-streaming for subsequent rounds to avoid a second SSE contract — final text is buffered and flushed as `delta` frames at the end of the last round). This keeps the loop shape identical to non-streaming while still giving the UI live updates on tool usage.

> **Note — providers that emit partial JSON args mid-stream:** Anthropic emits `input_json_delta` frames assembling the arguments JSON; OpenAI emits `arguments` deltas similarly. `AnthropicAiProvider.StreamChatAsync` already surfaces these as separate `AiToolCall` fragments carrying partial `ArgumentsJson`. We accumulate the fragments per `Id` inside `ExecuteStreamAsync` before dispatching.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatStreamEvent.cs` (doc only)

- [ ] **Step 1: Document the two new frames in `ChatStreamEvent.cs`**

Append to the existing XML summary:

```
/// - "tool_call" — { callId, name, argumentsJson } emitted once per tool invocation.
/// - "tool_result" — { callId, isError, content } emitted after the tool returns.
```

- [ ] **Step 2: Replace `ExecuteStreamAsync` body**

Streaming logic:

```csharp
public async IAsyncEnumerable<ChatStreamEvent> ExecuteStreamAsync(
    Guid? conversationId,
    Guid? assistantId,
    string userMessage,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    var stateResult = await PrepareTurnAsync(conversationId, assistantId, userMessage, ct);
    if (stateResult.IsFailure)
    {
        yield return new ChatStreamEvent("error", new
        {
            Code = stateResult.Error.Code,
            Message = stateResult.Error.Description
        });
        yield break;
    }

    var state = stateResult.Value;

    yield return new ChatStreamEvent("start", new
    {
        ConversationId = state.Conversation.Id,
        UserMessageId = state.UserMessage.Id
    });

    var provider = providerFactory.Create(ResolveProvider(state.Assistant));
    var chatOptions = BuildChatOptions(state.Assistant, state.Tools.ProviderTools);

    var messages = new List<AiChatMessage>(state.ProviderMessages);
    var totalInput = 0;
    var totalOutput = 0;
    var stepBudget = Math.Clamp(state.Assistant.MaxAgentSteps, 1, 20);
    var nextOrder = state.NextOrder;

    var finalContentBuilder = new StringBuilder();
    var finishReason = "stop";

    for (var step = 0; step < stepBudget; step++)
    {
        // Per-round accumulators
        var roundContent = new StringBuilder();
        var toolCallBuilders = new Dictionary<string, ToolCallBuilder>(StringComparer.Ordinal);
        int? roundInput = null;
        int? roundOutput = null;
        string? roundFinish = null;

        await foreach (var chunkOrError in EnumerateSafelyAsync(
            provider.StreamChatAsync(messages, chatOptions, ct), ct))
        {
            if (chunkOrError.Error is not null)
            {
                await FailTurnAsync(state);
                yield return new ChatStreamEvent("error", new
                {
                    Code = "Ai.ProviderError",
                    Message = chunkOrError.Error
                });
                yield break;
            }

            var chunk = chunkOrError.Chunk!;

            if (chunk.FinishReason is not null) roundFinish = chunk.FinishReason;
            if (chunk.InputTokens is int ci && ci > 0) roundInput = ci;
            if (chunk.OutputTokens is int co && co > 0) roundOutput = co;

            if (chunk.ContentDelta is { Length: > 0 } delta)
            {
                roundContent.Append(delta);
                yield return new ChatStreamEvent("delta", new { Content = delta });
            }

            if (chunk.ToolCallDelta is { } tc)
            {
                if (!toolCallBuilders.TryGetValue(tc.Id, out var builder))
                {
                    builder = new ToolCallBuilder(tc.Id, tc.Name);
                    toolCallBuilders[tc.Id] = builder;
                }
                builder.AppendArguments(tc.ArgumentsJson);
            }
        }

        totalInput += roundInput ?? EstimateTokens(messages.Sum(m => m.Content?.Length ?? 0));
        totalOutput += roundOutput ?? EstimateTokens(roundContent.Length);
        if (roundFinish is not null) finishReason = roundFinish;

        if (toolCallBuilders.Count == 0)
        {
            finalContentBuilder.Append(roundContent);
            break; // final text round — leave the loop and finalize
        }

        // Tool-call round: persist assistant request, dispatch, emit frames, continue.
        var assembledCalls = toolCallBuilders.Values.Select(b => b.Build()).ToList();
        var toolCallsJson = System.Text.Json.JsonSerializer.Serialize(
            assembledCalls, SerializerOptions);

        var assistantCallMsg = AiMessage.CreateAssistantMessage(
            state.Conversation.Id,
            roundContent.ToString(),
            nextOrder++,
            roundInput ?? 0,
            roundOutput ?? 0,
            toolCalls: toolCallsJson);
        context.AiMessages.Add(assistantCallMsg);
        messages.Add(new AiChatMessage(
            "assistant",
            roundContent.Length == 0 ? null : roundContent.ToString(),
            ToolCalls: assembledCalls));

        foreach (var call in assembledCalls)
        {
            yield return new ChatStreamEvent("tool_call", new
            {
                CallId = call.Id,
                Name = call.Name,
                ArgumentsJson = call.ArgumentsJson
            });

            var resultJson = await DispatchToolAsync(call, state.Tools, ct);

            var toolResultMsg = AiMessage.CreateToolResultMessage(
                state.Conversation.Id, call.Id, resultJson, nextOrder++);
            context.AiMessages.Add(toolResultMsg);
            messages.Add(new AiChatMessage("tool", resultJson, ToolCallId: call.Id));

            yield return new ChatStreamEvent("tool_result", new
            {
                CallId = call.Id,
                IsError = resultJson.Contains("\"ok\":false", StringComparison.Ordinal),
                Content = resultJson
            });
        }

        await context.SaveChangesAsync(ct);
    }

    var finalContent = finalContentBuilder.ToString();
    var assistantMessage = await FinalizeTurnAsync(
        state, finalContent, totalInput, totalOutput, nextOrder, ct);

    yield return new ChatStreamEvent("done", new
    {
        MessageId = assistantMessage.Id,
        InputTokens = totalInput,
        OutputTokens = totalOutput,
        FinishReason = finishReason
    });
}

// Helper used by the streaming loop only
private sealed class ToolCallBuilder(string id, string name)
{
    private readonly StringBuilder _args = new();

    public string Id { get; } = id;
    public string Name { get; } = name;

    public void AppendArguments(string fragment)
    {
        if (!string.IsNullOrEmpty(fragment)) _args.Append(fragment);
    }

    public AiToolCall Build()
    {
        var json = _args.Length == 0 ? "{}" : _args.ToString();
        return new AiToolCall(Id, Name, json);
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build boilerplateBE/boilerplateBE.sln`
Expected: 0 errors.

- [ ] **Step 4: Smoke test — stream with tool call**

Using the same assistant created in Task 16:

```bash
curl -N -s -X POST http://localhost:5000/api/v1/ai/chat/stream \
  -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" \
  -d "{\"assistantId\":\"$ASST_ID\",\"message\":\"Show me my recent conversations.\"}"
```

Expected SSE sequence:
```
event: start
event: delta (some content about to call the tool — or none, depends on the model)
event: tool_call
event: tool_result
event: delta … delta … (summary text)
event: done
```

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatStreamEvent.cs
git commit -m "feat(ai): emit tool_call and tool_result frames during SSE streaming"
```

---

### Task 18: Optional seed — sample assistant with demo tool enabled

So manual testing and any future frontend demo lands on a working starting point.

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

- [ ] **Step 1: Add a seed step inside `MigrateAsync`**

Append after `await context.Database.MigrateAsync(cancellationToken);`:

```csharp
var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
if (configuration.GetValue<bool>("AI:SeedSampleAssistant"))
{
    const string SampleName = "AI Tools Demo";
    var exists = await context.AiAssistants
        .AnyAsync(a => a.Name == SampleName, cancellationToken);

    if (!exists)
    {
        var sample = AiAssistant.Create(
            tenantId: null,
            name: SampleName,
            description: "Demonstrates AI function calling. Ask about your conversations.",
            systemPrompt:
                "You are a friendly assistant. When the user asks about their own " +
                "conversations, call the list_my_conversations tool and summarise the results.",
            provider: null,
            model: null,
            temperature: 0.2,
            maxTokens: 1024,
            executionMode: AssistantExecutionMode.Chat,
            maxAgentSteps: 5,
            isActive: true);
        sample.SetEnabledTools(new[] { "list_my_conversations" });
        context.AiAssistants.Add(sample);
        await context.SaveChangesAsync(cancellationToken);
    }
}
```

Required `using` additions: `Microsoft.Extensions.Configuration;`, `Microsoft.EntityFrameworkCore;`, `Starter.Module.AI.Domain.Entities;`, `Starter.Module.AI.Domain.Enums;`.

- [ ] **Step 2: Default the config key**

Open `boilerplateBE/src/Starter.Api/appsettings.Development.json` and under the `AI` section add:

```json
"SeedSampleAssistant": true
```

(If the `AI` section doesn't exist in appsettings.Development.json, skip — the default in `GetValue<bool>` is `false`, and the setting can be toggled per-app.)

- [ ] **Step 3: Build + run + verify**

```bash
dotnet build boilerplateBE/boilerplateBE.sln
cd boilerplateBE/src/Starter.Api && dotnet run --launch-profile http &
sleep 6
psql -U postgres -d starterdb -c "SELECT name, enabled_tool_names FROM ai_assistants;"
```

Expected: an `AI Tools Demo` row with `["list_my_conversations"]` in `enabled_tool_names`. Stop the API.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs \
        boilerplateBE/src/Starter.Api/appsettings.Development.json
git commit -m "feat(ai): seed AI Tools Demo assistant with list_my_conversations enabled"
```

---

### Task 19: End-to-end verification pass

No new code — just verify the whole plan works in one sitting. This replicates the Plan 2 smoke-test discipline.

- [ ] **Step 1: Clean rebuild**

```bash
dotnet build boilerplateBE/boilerplateBE.sln
```
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 2: Run the API**

```bash
cd boilerplateBE/src/Starter.Api && dotnet run --launch-profile http
```

- [ ] **Step 3: Log in as SuperAdmin and capture token**

```bash
JWT=$(curl -s -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"superadmin@starter.com","password":"Admin@123456"}' \
  | jq -r '.data.token')
echo "$JWT" | head -c 40
```

- [ ] **Step 4: Exercise every new endpoint**

```bash
# 1. List assistants — the seeded demo must appear
curl -s "http://localhost:5000/api/v1/ai/assistants?pageSize=5" -H "Authorization: Bearer $JWT"

# 2. Create a second assistant
curl -s -X POST http://localhost:5000/api/v1/ai/assistants \
  -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" \
  -d '{"name":"Plan3 Test","description":"Smoke","systemPrompt":"Be terse.","temperature":0.2,"maxTokens":512,"executionMode":0,"maxAgentSteps":5,"enabledToolNames":[],"knowledgeBaseDocIds":[]}'

# 3. Update the new assistant to enable the demo tool
ID=$(curl -s "http://localhost:5000/api/v1/ai/assistants?searchTerm=Plan3" -H "Authorization: Bearer $JWT" | jq -r '.data[0].id')
curl -s -X PUT "http://localhost:5000/api/v1/ai/assistants/$ID" \
  -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" \
  -d "{\"id\":\"$ID\",\"name\":\"Plan3 Test\",\"description\":\"Smoke\",\"systemPrompt\":\"When the user asks for conversations, call list_my_conversations.\",\"temperature\":0.2,\"maxTokens\":512,\"executionMode\":0,\"maxAgentSteps\":5,\"enabledToolNames\":[\"list_my_conversations\"],\"knowledgeBaseDocIds\":[],\"isActive\":true}"

# 4. List tools — the demo tool appears
curl -s "http://localhost:5000/api/v1/ai/tools" -H "Authorization: Bearer $JWT" | jq '.data[].name'

# 5. Toggle the tool off → expect 200 OK, IsEnabled=false in DB
curl -s -X PUT "http://localhost:5000/api/v1/ai/tools/list_my_conversations/toggle" \
  -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" \
  -d '{"isEnabled":false}'
psql -U postgres -d starterdb -c "SELECT name, is_enabled FROM ai_tools;"
# 5b. Toggle back on
curl -s -X PUT "http://localhost:5000/api/v1/ai/tools/list_my_conversations/toggle" \
  -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" \
  -d '{"isEnabled":true}'

# 6. Non-streaming tool call
curl -s -X POST http://localhost:5000/api/v1/ai/chat \
  -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" \
  -d "{\"assistantId\":\"$ID\",\"message\":\"What conversations have I had recently?\"}" | jq

# 7. Streaming tool call
curl -N -s -X POST http://localhost:5000/api/v1/ai/chat/stream \
  -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" \
  -d "{\"assistantId\":\"$ID\",\"message\":\"What conversations have I had recently?\"}"

# 8. Delete attempt — should be blocked (AssistantInUse)
curl -s -X DELETE "http://localhost:5000/api/v1/ai/assistants/$ID" -H "Authorization: Bearer $JWT"
```

- [ ] **Step 5: Verify DB state**

```bash
psql -U postgres -d starterdb -c "SELECT role, \"order\", (length(content) > 0) AS has_content, tool_calls IS NOT NULL AS has_tool_calls, tool_call_id FROM ai_messages ORDER BY \"order\" LIMIT 20;"
```

Expected: user/assistant(with tool_calls)/tool_result/assistant(final) rows in order.

- [ ] **Step 6: Stop the API and commit a closing note to the changelog (if present) or tag the verification**

No code change — just confirm the green state.

---

## Self-Review Checklist

- **Spec coverage:** Tasks 1–6 cover Assistants CRUD (spec §"API Endpoints → Assistants (admin)"). Tasks 7–12 cover the tool registry (spec §"Function Calling → MediatR Bridge" + §"API Endpoints → Tools"). Tasks 13–18 cover function-calling integration (spec §"Execution Engine → Tool execution flow"). RAG, autonomous agents, and triggers are explicitly deferred.
- **Placeholder scan:** No TBDs, no "similar to…", no "add appropriate validation" without specifics. Every step has either a full code block, a full command, or a specific check.
- **Type consistency:** `AiAssistantDto` signature matches between Tasks 1 and 5/6. `AiToolDto` matches Tasks 7, 11, 12. `ToolResolutionResult` introduced in Task 8, consumed in Tasks 9, 15, 16, 17. `ChatTurnState` signature change introduced in Task 15 is used by the loops in Tasks 16 and 17.
- **Step granularity:** Each step is write-a-block / build / run / verify / commit — bite-sized enough for a fresh subagent.

---

## Execution Handoff

After saving this plan, run Plan 3 via **superpowers:subagent-driven-development**: fresh subagent per task, spec-compliance review, then code-quality review between tasks.
