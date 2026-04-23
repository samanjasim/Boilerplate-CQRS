# AI Module — Plan 1: Foundation + Provider Layer

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create the `Starter.Module.AI` project skeleton with all domain entities, database context, LLM provider abstraction (OpenAI/Anthropic/Ollama), Qdrant Docker service, and the `IAiService` capability in Abstractions — everything needed before chat, RAG, or agents can be built.

**Architecture:** New module at `boilerplateBE/src/modules/Starter.Module.AI/` following the established IModule pattern (see Products module). Thin `IAiService` + `IAiToolDefinition` capabilities in `Starter.Abstractions/Capabilities/`. Qdrant added to Docker Compose. LLM providers abstracted behind internal `IAiProvider` interface.

**Tech Stack:** .NET 10, EF Core (PostgreSQL), Qdrant.Client, Anthropic.SDK, OpenAI (official), Tesseract (OCR), PdfPig, SharpToken

**Spec:** `docs/superpowers/specs/2026-04-13-ai-integration-module-design.md`

**Plan series:** This is Plan 1 of ~7. Subsequent plans build on this foundation:
- Plan 2: Chat + Streaming
- Plan 3: Function Calling
- Plan 4: RAG Pipeline
- Plan 5: Agent Engine
- Plan 6: Frontend
- Plan 7: Billing Integration

---

## File Map

### New files in Starter.Abstractions

| File | Purpose |
|------|---------|
| `Capabilities/IAiService.cs` | Thin AI capability (4 methods + DTOs) |
| `Capabilities/IAiToolDefinition.cs` | Tool registration interface for modules |

### New files in Starter.Infrastructure

| File | Purpose |
|------|---------|
| `Capabilities/NullObjects/NullAiService.cs` | No-op fallback when AI module absent |

### New project: Starter.Module.AI

| File | Purpose |
|------|---------|
| `Starter.Module.AI.csproj` | Project file with NuGet refs |
| `AIModule.cs` | IModule implementation |
| `Constants/AiPermissions.cs` | Permission constants |
| `Domain/Entities/AiAssistant.cs` | Configurable assistant per tenant |
| `Domain/Entities/AiConversation.cs` | Chat session |
| `Domain/Entities/AiMessage.cs` | Individual message |
| `Domain/Entities/AiDocument.cs` | Knowledge base document |
| `Domain/Entities/AiDocumentChunk.cs` | Embedded chunk |
| `Domain/Entities/AiTool.cs` | Registered function-calling tool |
| `Domain/Entities/AiAgentTask.cs` | Background agent execution |
| `Domain/Entities/AiAgentTrigger.cs` | Scheduled/event trigger |
| `Domain/Entities/AiUsageLog.cs` | Per-request usage audit |
| `Domain/Enums/AiProviderType.cs` | OpenAI, Anthropic, Ollama |
| `Domain/Enums/MessageRole.cs` | User, Assistant, System, Tool, ToolResult |
| `Domain/Enums/EmbeddingStatus.cs` | Pending, Processing, Completed, Failed |
| `Domain/Enums/AgentTaskStatus.cs` | Queued, Running, Completed, Failed, Cancelled |
| `Domain/Enums/TriggerType.cs` | Cron, DomainEvent |
| `Domain/Enums/AssistantExecutionMode.cs` | Chat, Agent |
| `Domain/Enums/AiRequestType.cs` | Chat, Completion, Embedding, AgentStep |
| `Domain/Errors/AiErrors.cs` | Static error factory |
| `Infrastructure/Persistence/AiDbContext.cs` | Module DB context |
| `Infrastructure/Configurations/AiAssistantConfiguration.cs` | EF config |
| `Infrastructure/Configurations/AiConversationConfiguration.cs` | EF config |
| `Infrastructure/Configurations/AiMessageConfiguration.cs` | EF config |
| `Infrastructure/Configurations/AiDocumentConfiguration.cs` | EF config |
| `Infrastructure/Configurations/AiDocumentChunkConfiguration.cs` | EF config |
| `Infrastructure/Configurations/AiToolConfiguration.cs` | EF config |
| `Infrastructure/Configurations/AiAgentTaskConfiguration.cs` | EF config |
| `Infrastructure/Configurations/AiAgentTriggerConfiguration.cs` | EF config |
| `Infrastructure/Configurations/AiUsageLogConfiguration.cs` | EF config |
| `Infrastructure/Providers/IAiProvider.cs` | Internal provider interface |
| `Infrastructure/Providers/AiProviderTypes.cs` | Internal DTOs (ChatMessage, ChatCompletion, etc.) |
| `Infrastructure/Providers/AnthropicAiProvider.cs` | Anthropic implementation |
| `Infrastructure/Providers/OpenAiProvider.cs` | OpenAI implementation |
| `Infrastructure/Providers/OllamaAiProvider.cs` | Ollama implementation |
| `Infrastructure/Providers/AiProviderFactory.cs` | Creates provider by type |
| `Infrastructure/Services/AiService.cs` | Implements IAiService capability |
| `Infrastructure/Services/AiUsageMetricCalculator.cs` | Usage metric for billing |

### Modified files

| File | Change |
|------|--------|
| `Starter.Infrastructure/DependencyInjection.cs` | Add `NullAiService` to `AddCapabilities()` |
| `docker-compose.yml` | Add Qdrant service |
| `Starter.Api/appsettings.json` | Add `AI` config section |
| `Starter.Api/appsettings.Development.json` | Add `AI` dev config |
| `Starter.sln` | Add `Starter.Module.AI` project |

---

### Task 1: Abstractions — IAiService capability + IAiToolDefinition

**Files:**
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/IAiService.cs`
- Create: `boilerplateBE/src/Starter.Abstractions/Capabilities/IAiToolDefinition.cs`
- Create: `boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullAiService.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Create IAiService capability interface**

Create `boilerplateBE/src/Starter.Abstractions/Capabilities/IAiService.cs`:

```csharp
using System.Text.Json;

namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Thin AI capability for cross-module AI features.
/// Modules inject this to get AI-enhanced functionality (summarization, classification, etc.)
/// without depending on the AI module directly.
/// When AI module is absent, NullAiService returns null/empty for all methods.
/// </summary>
public interface IAiService : ICapability
{
    /// <summary>Generate a text completion from a prompt.</summary>
    Task<AiCompletionResult?> CompleteAsync(
        string prompt, AiCompletionOptions? options = null, CancellationToken ct = default);

    /// <summary>Summarize content with optional instructions.</summary>
    Task<string?> SummarizeAsync(
        string content, string? instructions = null, CancellationToken ct = default);

    /// <summary>Classify text into one of the provided categories.</summary>
    Task<AiClassificationResult?> ClassifyAsync(
        string content, IReadOnlyList<string> categories, CancellationToken ct = default);

    /// <summary>Generate embedding vector for text.</summary>
    Task<float[]?> EmbedAsync(string text, CancellationToken ct = default);
}

public sealed record AiCompletionResult(string Content, int TokensUsed);

public sealed record AiClassificationResult(string Category, double Confidence);

public sealed record AiCompletionOptions(
    string? Model = null,
    double? Temperature = null,
    int? MaxTokens = null);
```

- [ ] **Step 2: Create IAiToolDefinition interface**

Create `boilerplateBE/src/Starter.Abstractions/Capabilities/IAiToolDefinition.cs`:

```csharp
using System.Text.Json;

namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Interface for registering MediatR commands as AI-callable tools.
/// Modules implement this in their ConfigureServices to expose commands
/// to the AI execution engine. When AI module is absent, registrations are unused.
/// </summary>
public interface IAiToolDefinition
{
    /// <summary>Tool name used in LLM function calling (e.g., "create_product").</summary>
    string Name { get; }

    /// <summary>Human-readable description for the LLM to understand when to use this tool.</summary>
    string Description { get; }

    /// <summary>JSON Schema describing the tool's parameters.</summary>
    JsonElement ParameterSchema { get; }

    /// <summary>Fully qualified type of the MediatR command to execute.</summary>
    Type CommandType { get; }

    /// <summary>Permission the user must have to invoke this tool (e.g., "Products.Create").</summary>
    string RequiredPermission { get; }

    /// <summary>Grouping category (e.g., "Products", "Users", "Orders").</summary>
    string Category { get; }

    /// <summary>Whether this tool only reads data (no mutations).</summary>
    bool IsReadOnly { get; }
}
```

- [ ] **Step 3: Create NullAiService**

Create `boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullAiService.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

public sealed class NullAiService(ILogger<NullAiService> logger) : IAiService
{
    public Task<AiCompletionResult?> CompleteAsync(
        string prompt, AiCompletionOptions? options = null, CancellationToken ct = default)
    {
        logger.LogDebug("AI completion skipped — AI module not installed");
        return Task.FromResult<AiCompletionResult?>(null);
    }

    public Task<string?> SummarizeAsync(
        string content, string? instructions = null, CancellationToken ct = default)
    {
        logger.LogDebug("AI summarization skipped — AI module not installed");
        return Task.FromResult<string?>(null);
    }

    public Task<AiClassificationResult?> ClassifyAsync(
        string content, IReadOnlyList<string> categories, CancellationToken ct = default)
    {
        logger.LogDebug("AI classification skipped — AI module not installed");
        return Task.FromResult<AiClassificationResult?>(null);
    }

    public Task<float[]?> EmbedAsync(string text, CancellationToken ct = default)
    {
        logger.LogDebug("AI embedding skipped — AI module not installed");
        return Task.FromResult<float[]?>(null);
    }
}
```

- [ ] **Step 4: Register NullAiService in DI**

In `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs`, add to the `AddCapabilities` method alongside the other null object registrations:

```csharp
services.TryAddScoped<IAiService, NullAiService>();
```

Add the using statement at the top of the file:
```csharp
using Starter.Abstractions.Capabilities; // if not already present
```

- [ ] **Step 5: Verify build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeds with no errors.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/Starter.Abstractions/Capabilities/IAiService.cs \
       boilerplateBE/src/Starter.Abstractions/Capabilities/IAiToolDefinition.cs \
       boilerplateBE/src/Starter.Infrastructure/Capabilities/NullObjects/NullAiService.cs \
       boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs
git commit -m "feat(ai): add IAiService + IAiToolDefinition capabilities with NullAiService fallback"
```

---

### Task 2: Module project setup + solution update

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Starter.Module.AI.csproj`
- Modify: `boilerplateBE/Starter.sln`

- [ ] **Step 1: Create module project file**

Create `boilerplateBE/src/modules/Starter.Module.AI/Starter.Module.AI.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Anthropic.SDK" Version="4.*" />
    <PackageReference Include="OpenAI" Version="2.*" />
    <PackageReference Include="Qdrant.Client" Version="1.*" />
    <PackageReference Include="MassTransit" />
    <PackageReference Include="MassTransit.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="SharpToken" Version="2.*" />
    <PackageReference Include="Tesseract" Version="5.*" />
    <PackageReference Include="PdfPig" Version="0.*" />
    <PackageReference Include="DocumentFormat.OpenXml" Version="3.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Starter.Abstractions.Web\Starter.Abstractions.Web.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add project to solution**

Run:
```bash
cd boilerplateBE
dotnet sln add src/modules/Starter.Module.AI/Starter.Module.AI.csproj --solution-folder src/modules
```

- [ ] **Step 3: Verify solution builds**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeds. The module project compiles (even though it has no code yet — the csproj is valid).

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Starter.Module.AI.csproj boilerplateBE/Starter.sln
git commit -m "feat(ai): add Starter.Module.AI project to solution"
```

---

### Task 3: Domain layer — enums, errors, and all entities

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/AiProviderType.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/MessageRole.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/EmbeddingStatus.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/AgentTaskStatus.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/TriggerType.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/AssistantExecutionMode.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/AiRequestType.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/AiErrors.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAssistant.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiConversation.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiMessage.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiDocument.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiDocumentChunk.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiTool.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAgentTask.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAgentTrigger.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiUsageLog.cs`

- [ ] **Step 1: Create all enum files**

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/AiProviderType.cs`:
```csharp
namespace Starter.Module.AI.Domain.Enums;

public enum AiProviderType
{
    OpenAI,
    Anthropic,
    Ollama
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/MessageRole.cs`:
```csharp
namespace Starter.Module.AI.Domain.Enums;

public enum MessageRole
{
    User,
    Assistant,
    System,
    Tool,
    ToolResult
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/EmbeddingStatus.cs`:
```csharp
namespace Starter.Module.AI.Domain.Enums;

public enum EmbeddingStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/AgentTaskStatus.cs`:
```csharp
namespace Starter.Module.AI.Domain.Enums;

public enum AgentTaskStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/TriggerType.cs`:
```csharp
namespace Starter.Module.AI.Domain.Enums;

public enum TriggerType
{
    Cron,
    DomainEvent
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/AssistantExecutionMode.cs`:
```csharp
namespace Starter.Module.AI.Domain.Enums;

public enum AssistantExecutionMode
{
    Chat,
    Agent
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/AiRequestType.cs`:
```csharp
namespace Starter.Module.AI.Domain.Enums;

public enum AiRequestType
{
    Chat,
    Completion,
    Embedding,
    AgentStep
}
```

- [ ] **Step 2: Create AiErrors**

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/AiErrors.cs`:
```csharp
using Starter.Shared.Results;

namespace Starter.Module.AI.Domain.Errors;

public static class AiErrors
{
    public static Error AssistantNotFound => Error.NotFound("Ai.AssistantNotFound", "AI assistant not found.");
    public static Error ConversationNotFound => Error.NotFound("Ai.ConversationNotFound", "Conversation not found.");
    public static Error DocumentNotFound => Error.NotFound("Ai.DocumentNotFound", "Document not found.");
    public static Error AgentTaskNotFound => Error.NotFound("Ai.AgentTaskNotFound", "Agent task not found.");
    public static Error TriggerNotFound => Error.NotFound("Ai.TriggerNotFound", "Agent trigger not found.");
    public static Error ToolNotFound => Error.NotFound("Ai.ToolNotFound", "AI tool not found.");

    public static Error QuotaExceeded(long limit) =>
        Error.Forbidden("Ai.QuotaExceeded", $"Monthly AI token quota exceeded. Limit: {limit:N0} tokens.");

    public static Error ProviderNotConfigured =>
        Error.Validation("Ai.ProviderNotConfigured", "AI provider is not configured. Set API key in AI settings.");

    public static Error ProviderError(string message) =>
        Error.Failure("Ai.ProviderError", $"AI provider returned an error: {message}");

    public static Error AgentStepLimitReached(int limit) =>
        Error.Validation("Ai.AgentStepLimitReached", $"Agent reached the maximum step limit of {limit}.");

    public static Error AgentTaskAlreadyCompleted =>
        Error.Validation("Ai.AgentTaskAlreadyCompleted", "Agent task has already completed.");

    public static Error DocumentProcessingFailed(string reason) =>
        Error.Failure("Ai.DocumentProcessingFailed", $"Document processing failed: {reason}");

    public static Error AssistantNameAlreadyExists =>
        Error.Conflict("Ai.AssistantNameAlreadyExists", "An assistant with this name already exists.");

    public static Error ToolPermissionDenied(string toolName) =>
        Error.Forbidden("Ai.ToolPermissionDenied", $"You don't have permission to use the tool '{toolName}'.");

    public static Error AiNotEnabled =>
        Error.Forbidden("Ai.NotEnabled", "AI features are not enabled for this tenant.");
}
```

- [ ] **Step 3: Create AiAssistant entity**

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAssistant.cs`:
```csharp
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiAssistant : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string SystemPrompt { get; private set; } = default!;
    public AiProviderType? Provider { get; private set; }
    public string? Model { get; private set; }
    public double Temperature { get; private set; }
    public int MaxTokens { get; private set; }
    public string EnabledToolNames { get; private set; } = "[]";
    public string KnowledgeBaseDocIds { get; private set; } = "[]";
    public AssistantExecutionMode ExecutionMode { get; private set; }
    public int MaxAgentSteps { get; private set; }
    public bool IsActive { get; private set; }

    private AiAssistant() { }

    public static AiAssistant Create(
        Guid? tenantId,
        string name,
        string systemPrompt,
        string? description = null,
        AiProviderType? provider = null,
        string? model = null,
        double temperature = 0.7,
        int maxTokens = 4096,
        AssistantExecutionMode executionMode = AssistantExecutionMode.Chat,
        int maxAgentSteps = 10)
    {
        return new AiAssistant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            SystemPrompt = systemPrompt,
            Provider = provider,
            Model = model,
            Temperature = temperature,
            MaxTokens = maxTokens,
            ExecutionMode = executionMode,
            MaxAgentSteps = maxAgentSteps,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string name,
        string systemPrompt,
        string? description,
        AiProviderType? provider,
        string? model,
        double temperature,
        int maxTokens,
        AssistantExecutionMode executionMode,
        int maxAgentSteps,
        bool isActive)
    {
        Name = name.Trim();
        SystemPrompt = systemPrompt;
        Description = description?.Trim();
        Provider = provider;
        Model = model;
        Temperature = temperature;
        MaxTokens = maxTokens;
        ExecutionMode = executionMode;
        MaxAgentSteps = maxAgentSteps;
        IsActive = isActive;
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetEnabledTools(string toolNamesJson)
    {
        EnabledToolNames = toolNamesJson;
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetKnowledgeBase(string docIdsJson)
    {
        KnowledgeBaseDocIds = docIdsJson;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        ModifiedAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 4: Create AiConversation + AiMessage entities**

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiConversation.cs`:
```csharp
using Starter.Domain.Common;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiConversation : BaseEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid AssistantId { get; private set; }
    public Guid UserId { get; private set; }
    public string? Title { get; private set; }
    public string Status { get; private set; } = "Active";
    public int MessageCount { get; private set; }
    public int TotalTokensUsed { get; private set; }
    public DateTime LastMessageAt { get; private set; }

    private AiConversation() { }

    public static AiConversation Create(Guid? tenantId, Guid assistantId, Guid userId, string? title = null)
    {
        var now = DateTime.UtcNow;
        return new AiConversation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssistantId = assistantId,
            UserId = userId,
            Title = title,
            CreatedAt = now,
            LastMessageAt = now
        };
    }

    public void AddMessageStats(int tokensUsed)
    {
        MessageCount++;
        TotalTokensUsed += tokensUsed;
        LastMessageAt = DateTime.UtcNow;
    }

    public void SetTitle(string title)
    {
        Title = title;
    }

    public void MarkCompleted() => Status = "Completed";
    public void MarkFailed() => Status = "Failed";
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiMessage.cs`:
```csharp
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiMessage : BaseEntity
{
    public Guid ConversationId { get; private set; }
    public MessageRole Role { get; private set; }
    public string? Content { get; private set; }
    public string? ToolCalls { get; private set; }
    public string? ToolCallId { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public int Order { get; private set; }

    private AiMessage() { }

    public static AiMessage CreateUserMessage(Guid conversationId, string content, int order)
    {
        return new AiMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = MessageRole.User,
            Content = content,
            Order = order,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static AiMessage CreateAssistantMessage(
        Guid conversationId, string? content, string? toolCalls,
        int inputTokens, int outputTokens, int order)
    {
        return new AiMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = MessageRole.Assistant,
            Content = content,
            ToolCalls = toolCalls,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            Order = order,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static AiMessage CreateToolResultMessage(
        Guid conversationId, string toolCallId, string content, int order)
    {
        return new AiMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = MessageRole.ToolResult,
            Content = content,
            ToolCallId = toolCallId,
            Order = order,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static AiMessage CreateSystemMessage(Guid conversationId, string content, int order)
    {
        return new AiMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = MessageRole.System,
            Content = content,
            Order = order,
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

- [ ] **Step 5: Create AiDocument + AiDocumentChunk entities**

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiDocument.cs`:
```csharp
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiDocument : BaseEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string FileName { get; private set; } = default!;
    public string FileRef { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public int ChunkCount { get; private set; }
    public EmbeddingStatus EmbeddingStatus { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool RequiresOcr { get; private set; }
    public string? OcrProvider { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public Guid UploadedByUserId { get; private set; }

    private AiDocument() { }

    public static AiDocument Create(
        Guid? tenantId, string name, string fileName,
        string fileRef, string contentType, long sizeBytes, Guid uploadedByUserId)
    {
        return new AiDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            FileName = fileName,
            FileRef = fileRef,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            EmbeddingStatus = EmbeddingStatus.Pending,
            UploadedByUserId = uploadedByUserId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkProcessing() => EmbeddingStatus = EmbeddingStatus.Processing;

    public void MarkCompleted(int chunkCount, bool requiredOcr, string? ocrProvider)
    {
        EmbeddingStatus = EmbeddingStatus.Completed;
        ChunkCount = chunkCount;
        RequiresOcr = requiredOcr;
        OcrProvider = ocrProvider;
        ProcessedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        EmbeddingStatus = EmbeddingStatus.Failed;
        ErrorMessage = errorMessage;
        ProcessedAt = DateTime.UtcNow;
    }

    public void ResetForReprocessing()
    {
        EmbeddingStatus = EmbeddingStatus.Pending;
        ChunkCount = 0;
        ErrorMessage = null;
        ProcessedAt = null;
    }
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiDocumentChunk.cs`:
```csharp
using Starter.Domain.Common;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiDocumentChunk : BaseEntity
{
    public Guid DocumentId { get; private set; }
    public Guid? ParentChunkId { get; private set; }
    public string ChunkLevel { get; private set; } = default!;
    public string Content { get; private set; } = default!;
    public int ChunkIndex { get; private set; }
    public string? SectionTitle { get; private set; }
    public int? PageNumber { get; private set; }
    public int TokenCount { get; private set; }
    public Guid QdrantPointId { get; private set; }

    private AiDocumentChunk() { }

    public static AiDocumentChunk Create(
        Guid documentId, string content, int chunkIndex,
        string chunkLevel, int tokenCount, Guid qdrantPointId,
        Guid? parentChunkId = null, string? sectionTitle = null, int? pageNumber = null)
    {
        return new AiDocumentChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            Content = content,
            ChunkIndex = chunkIndex,
            ChunkLevel = chunkLevel,
            TokenCount = tokenCount,
            QdrantPointId = qdrantPointId,
            ParentChunkId = parentChunkId,
            SectionTitle = sectionTitle,
            PageNumber = pageNumber,
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

- [ ] **Step 6: Create AiTool entity**

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiTool.cs`:
```csharp
using Starter.Domain.Common;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiTool : BaseEntity
{
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string ParameterSchema { get; private set; } = "{}";
    public string CommandType { get; private set; } = default!;
    public string RequiredPermission { get; private set; } = default!;
    public string Category { get; private set; } = default!;
    public bool IsEnabled { get; private set; }
    public bool IsReadOnly { get; private set; }

    private AiTool() { }

    public static AiTool Create(
        string name, string description, string parameterSchema,
        string commandType, string requiredPermission, string category, bool isReadOnly)
    {
        return new AiTool
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            ParameterSchema = parameterSchema,
            CommandType = commandType,
            RequiredPermission = requiredPermission,
            Category = category,
            IsReadOnly = isReadOnly,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Toggle(bool enabled)
    {
        IsEnabled = enabled;
        ModifiedAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 7: Create AiAgentTask + AiAgentTrigger entities**

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAgentTask.cs`:
```csharp
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiAgentTask : BaseEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid AssistantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Instruction { get; private set; } = default!;
    public AgentTaskStatus Status { get; private set; }
    public string Steps { get; private set; } = "[]";
    public string? Result { get; private set; }
    public int TotalTokensUsed { get; private set; }
    public int StepCount { get; private set; }
    public string TriggeredBy { get; private set; } = "User";
    public Guid? TriggerId { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private AiAgentTask() { }

    public static AiAgentTask Create(
        Guid? tenantId, Guid assistantId, Guid userId,
        string instruction, string triggeredBy = "User", Guid? triggerId = null)
    {
        return new AiAgentTask
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssistantId = assistantId,
            UserId = userId,
            Instruction = instruction,
            Status = AgentTaskStatus.Queued,
            TriggeredBy = triggeredBy,
            TriggerId = triggerId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkRunning()
    {
        Status = AgentTaskStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    public void AddStep(string stepsJson, int tokensUsed)
    {
        Steps = stepsJson;
        StepCount++;
        TotalTokensUsed += tokensUsed;
    }

    public void MarkCompleted(string result)
    {
        Status = AgentTaskStatus.Completed;
        Result = result;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = AgentTaskStatus.Failed;
        Result = errorMessage;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkCancelled()
    {
        Status = AgentTaskStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAgentTrigger.cs`:
```csharp
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiAgentTrigger : BaseEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid AssistantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public TriggerType TriggerType { get; private set; }
    public string? CronExpression { get; private set; }
    public string? EventType { get; private set; }
    public string Instruction { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public DateTime? LastRunAt { get; private set; }
    public DateTime? NextRunAt { get; private set; }

    private AiAgentTrigger() { }

    public static AiAgentTrigger CreateCron(
        Guid? tenantId, Guid assistantId, string name,
        string cronExpression, string instruction, string? description = null)
    {
        return new AiAgentTrigger
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssistantId = assistantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            TriggerType = TriggerType.Cron,
            CronExpression = cronExpression,
            Instruction = instruction,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static AiAgentTrigger CreateEvent(
        Guid? tenantId, Guid assistantId, string name,
        string eventType, string instruction, string? description = null)
    {
        return new AiAgentTrigger
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssistantId = assistantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            TriggerType = TriggerType.DomainEvent,
            EventType = eventType,
            Instruction = instruction,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateLastRun()
    {
        LastRunAt = DateTime.UtcNow;
    }

    public void SetNextRun(DateTime nextRunAt)
    {
        NextRunAt = nextRunAt;
    }

    public void Toggle(bool active)
    {
        IsActive = active;
        ModifiedAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 8: Create AiUsageLog entity**

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiUsageLog.cs`:
```csharp
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiUsageLog : BaseEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? ConversationId { get; private set; }
    public Guid? AgentTaskId { get; private set; }
    public AiProviderType Provider { get; private set; }
    public string Model { get; private set; } = default!;
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public decimal EstimatedCost { get; private set; }
    public AiRequestType RequestType { get; private set; }

    private AiUsageLog() { }

    public static AiUsageLog Create(
        Guid? tenantId, Guid userId,
        AiProviderType provider, string model,
        int inputTokens, int outputTokens, decimal estimatedCost,
        AiRequestType requestType,
        Guid? conversationId = null, Guid? agentTaskId = null)
    {
        return new AiUsageLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Provider = provider,
            Model = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            EstimatedCost = estimatedCost,
            RequestType = requestType,
            ConversationId = conversationId,
            AgentTaskId = agentTaskId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

- [ ] **Step 9: Verify build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeds.

- [ ] **Step 10: Commit**

```bash
cd boilerplateBE
git add src/modules/Starter.Module.AI/Domain/
git commit -m "feat(ai): add domain entities, enums, and error definitions"
```

---

### Task 4: Database — AiDbContext + entity configurations

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/AiDbContext.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiAssistantConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiConversationConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiMessageConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiDocumentConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiDocumentChunkConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiToolConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiAgentTaskConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiAgentTriggerConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiUsageLogConfiguration.cs`

- [ ] **Step 1: Create AiDbContext**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/AiDbContext.cs`:
```csharp
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Modularity;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Persistence;

public sealed class AiDbContext : DbContext, IModuleDbContext
{
    private readonly ICurrentUserService? _currentUserService;

    private Guid? CurrentTenantId => _currentUserService?.TenantId;

    public AiDbContext(
        DbContextOptions<AiDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<AiAssistant> AiAssistants => Set<AiAssistant>();
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<AiMessage> AiMessages => Set<AiMessage>();
    public DbSet<AiDocument> AiDocuments => Set<AiDocument>();
    public DbSet<AiDocumentChunk> AiDocumentChunks => Set<AiDocumentChunk>();
    public DbSet<AiTool> AiTools => Set<AiTool>();
    public DbSet<AiAgentTask> AiAgentTasks => Set<AiAgentTask>();
    public DbSet<AiAgentTrigger> AiAgentTriggers => Set<AiAgentTrigger>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Tenant query filters
        modelBuilder.Entity<AiAssistant>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiConversation>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiDocument>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiAgentTask>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiAgentTrigger>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiUsageLog>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
    }
}
```

- [ ] **Step 2: Create entity configurations**

Create all 9 configuration files. Each follows the same pattern as ProductConfiguration. Here's each one:

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiAssistantConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiAssistantConfiguration : IEntityTypeConfiguration<AiAssistant>
{
    public void Configure(EntityTypeBuilder<AiAssistant> builder)
    {
        builder.ToTable("ai_assistants");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(e => e.SystemPrompt).HasColumnName("system_prompt").IsRequired();
        builder.Property(e => e.Provider).HasColumnName("provider").HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Model).HasColumnName("model").HasMaxLength(100);
        builder.Property(e => e.Temperature).HasColumnName("temperature");
        builder.Property(e => e.MaxTokens).HasColumnName("max_tokens");
        builder.Property(e => e.EnabledToolNames).HasColumnName("enabled_tool_names").HasColumnType("jsonb").HasDefaultValue("[]");
        builder.Property(e => e.KnowledgeBaseDocIds).HasColumnName("knowledge_base_doc_ids").HasColumnType("jsonb").HasDefaultValue("[]");
        builder.Property(e => e.ExecutionMode).HasColumnName("execution_mode").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.MaxAgentSteps).HasColumnName("max_agent_steps").HasDefaultValue(10);
        builder.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ModifiedAt).HasColumnName("modified_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.ModifiedBy).HasColumnName("modified_by");

        builder.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
    }
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiConversationConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiConversationConfiguration : IEntityTypeConfiguration<AiConversation>
{
    public void Configure(EntityTypeBuilder<AiConversation> builder)
    {
        builder.ToTable("ai_conversations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.AssistantId).HasColumnName("assistant_id").IsRequired();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(500);
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(e => e.MessageCount).HasColumnName("message_count");
        builder.Property(e => e.TotalTokensUsed).HasColumnName("total_tokens_used");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.LastMessageAt).HasColumnName("last_message_at").IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.UserId });
        builder.HasIndex(e => e.AssistantId);
    }
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiMessageConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiMessageConfiguration : IEntityTypeConfiguration<AiMessage>
{
    public void Configure(EntityTypeBuilder<AiMessage> builder)
    {
        builder.ToTable("ai_messages");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.ConversationId).HasColumnName("conversation_id").IsRequired();
        builder.Property(e => e.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.Content).HasColumnName("content");
        builder.Property(e => e.ToolCalls).HasColumnName("tool_calls").HasColumnType("jsonb");
        builder.Property(e => e.ToolCallId).HasColumnName("tool_call_id").HasMaxLength(100);
        builder.Property(e => e.InputTokens).HasColumnName("input_tokens");
        builder.Property(e => e.OutputTokens).HasColumnName("output_tokens");
        builder.Property(e => e.Order).HasColumnName("order").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => new { e.ConversationId, e.Order });
    }
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiDocumentConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiDocumentConfiguration : IEntityTypeConfiguration<AiDocument>
{
    public void Configure(EntityTypeBuilder<AiDocument> builder)
    {
        builder.ToTable("ai_documents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(500).IsRequired();
        builder.Property(e => e.FileName).HasColumnName("file_name").HasMaxLength(500).IsRequired();
        builder.Property(e => e.FileRef).HasColumnName("file_ref").HasMaxLength(1000).IsRequired();
        builder.Property(e => e.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        builder.Property(e => e.SizeBytes).HasColumnName("size_bytes");
        builder.Property(e => e.ChunkCount).HasColumnName("chunk_count");
        builder.Property(e => e.EmbeddingStatus).HasColumnName("embedding_status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(e => e.RequiresOcr).HasColumnName("requires_ocr");
        builder.Property(e => e.OcrProvider).HasColumnName("ocr_provider").HasMaxLength(50);
        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at");
        builder.Property(e => e.UploadedByUserId).HasColumnName("uploaded_by_user_id").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.EmbeddingStatus);
    }
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiDocumentChunkConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiDocumentChunkConfiguration : IEntityTypeConfiguration<AiDocumentChunk>
{
    public void Configure(EntityTypeBuilder<AiDocumentChunk> builder)
    {
        builder.ToTable("ai_document_chunks");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.DocumentId).HasColumnName("document_id").IsRequired();
        builder.Property(e => e.ParentChunkId).HasColumnName("parent_chunk_id");
        builder.Property(e => e.ChunkLevel).HasColumnName("chunk_level").HasMaxLength(10).IsRequired();
        builder.Property(e => e.Content).HasColumnName("content").IsRequired();
        builder.Property(e => e.ChunkIndex).HasColumnName("chunk_index").IsRequired();
        builder.Property(e => e.SectionTitle).HasColumnName("section_title").HasMaxLength(500);
        builder.Property(e => e.PageNumber).HasColumnName("page_number");
        builder.Property(e => e.TokenCount).HasColumnName("token_count");
        builder.Property(e => e.QdrantPointId).HasColumnName("qdrant_point_id").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => e.DocumentId);
        builder.HasIndex(e => e.ParentChunkId);
        builder.HasIndex(e => e.QdrantPointId).IsUnique();
    }
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiToolConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiToolConfiguration : IEntityTypeConfiguration<AiTool>
{
    public void Configure(EntityTypeBuilder<AiTool> builder)
    {
        builder.ToTable("ai_tools");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000).IsRequired();
        builder.Property(e => e.ParameterSchema).HasColumnName("parameter_schema").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.CommandType).HasColumnName("command_type").HasMaxLength(500).IsRequired();
        builder.Property(e => e.RequiredPermission).HasColumnName("required_permission").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Category).HasColumnName("category").HasMaxLength(100).IsRequired();
        builder.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(true);
        builder.Property(e => e.IsReadOnly).HasColumnName("is_read_only");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ModifiedAt).HasColumnName("modified_at");

        builder.HasIndex(e => e.Name).IsUnique();
    }
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiAgentTaskConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiAgentTaskConfiguration : IEntityTypeConfiguration<AiAgentTask>
{
    public void Configure(EntityTypeBuilder<AiAgentTask> builder)
    {
        builder.ToTable("ai_agent_tasks");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.AssistantId).HasColumnName("assistant_id").IsRequired();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.Instruction).HasColumnName("instruction").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.Steps).HasColumnName("steps").HasColumnType("jsonb").HasDefaultValue("[]");
        builder.Property(e => e.Result).HasColumnName("result");
        builder.Property(e => e.TotalTokensUsed).HasColumnName("total_tokens_used");
        builder.Property(e => e.StepCount).HasColumnName("step_count");
        builder.Property(e => e.TriggeredBy).HasColumnName("triggered_by").HasMaxLength(20).IsRequired();
        builder.Property(e => e.TriggerId).HasColumnName("trigger_id");
        builder.Property(e => e.StartedAt).HasColumnName("started_at");
        builder.Property(e => e.CompletedAt).HasColumnName("completed_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.UserId });
        builder.HasIndex(e => e.Status);
    }
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiAgentTriggerConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiAgentTriggerConfiguration : IEntityTypeConfiguration<AiAgentTrigger>
{
    public void Configure(EntityTypeBuilder<AiAgentTrigger> builder)
    {
        builder.ToTable("ai_agent_triggers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.AssistantId).HasColumnName("assistant_id").IsRequired();
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(e => e.TriggerType).HasColumnName("trigger_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.CronExpression).HasColumnName("cron_expression").HasMaxLength(100);
        builder.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(200);
        builder.Property(e => e.Instruction).HasColumnName("instruction").IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(e => e.LastRunAt).HasColumnName("last_run_at");
        builder.Property(e => e.NextRunAt).HasColumnName("next_run_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ModifiedAt).HasColumnName("modified_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");

        builder.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
    }
}
```

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiUsageLogConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        builder.ToTable("ai_usage_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.ConversationId).HasColumnName("conversation_id");
        builder.Property(e => e.AgentTaskId).HasColumnName("agent_task_id");
        builder.Property(e => e.Provider).HasColumnName("provider").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.Model).HasColumnName("model").HasMaxLength(100).IsRequired();
        builder.Property(e => e.InputTokens).HasColumnName("input_tokens");
        builder.Property(e => e.OutputTokens).HasColumnName("output_tokens");
        builder.Property(e => e.EstimatedCost).HasColumnName("estimated_cost").HasPrecision(18, 8);
        builder.Property(e => e.RequestType).HasColumnName("request_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.CreatedAt });
        builder.HasIndex(e => e.ConversationId);
        builder.HasIndex(e => e.AgentTaskId);
    }
}
```

- [ ] **Step 3: Verify build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
cd boilerplateBE
git add src/modules/Starter.Module.AI/Infrastructure/
git commit -m "feat(ai): add AiDbContext with 9 entity configurations and tenant query filters"
```

---

### Task 5: LLM provider abstraction + internal types

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/IAiProvider.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/AiProviderTypes.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/AiProviderFactory.cs`

- [ ] **Step 1: Create internal provider interface**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/IAiProvider.cs`:
```csharp
namespace Starter.Module.AI.Infrastructure.Providers;

internal interface IAiProvider
{
    Task<AiChatCompletion> ChatAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        CancellationToken ct = default);

    IAsyncEnumerable<AiChatChunk> StreamChatAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        CancellationToken ct = default);

    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);

    Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create internal DTOs**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/AiProviderTypes.cs`:
```csharp
using System.Text.Json;

namespace Starter.Module.AI.Infrastructure.Providers;

internal sealed record AiChatMessage(string Role, string? Content, string? ToolCallId = null, IReadOnlyList<AiToolCall>? ToolCalls = null);

internal sealed record AiChatOptions(
    string Model,
    double Temperature = 0.7,
    int MaxTokens = 4096,
    string? SystemPrompt = null,
    IReadOnlyList<AiToolDefinitionDto>? Tools = null);

internal sealed record AiChatCompletion(
    string? Content,
    IReadOnlyList<AiToolCall>? ToolCalls,
    int InputTokens,
    int OutputTokens,
    string FinishReason);

internal sealed record AiChatChunk(
    string? ContentDelta,
    AiToolCall? ToolCallDelta,
    string? FinishReason);

internal sealed record AiToolCall(
    string Id,
    string Name,
    string ArgumentsJson);

internal sealed record AiToolDefinitionDto(
    string Name,
    string Description,
    JsonElement ParameterSchema);
```

- [ ] **Step 3: Create provider factory**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/AiProviderFactory.cs`:
```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Providers;

internal sealed class AiProviderFactory(
    IConfiguration configuration,
    ILoggerFactory loggerFactory)
{
    public IAiProvider Create(AiProviderType providerType)
    {
        return providerType switch
        {
            AiProviderType.Anthropic => new AnthropicAiProvider(configuration, loggerFactory.CreateLogger<AnthropicAiProvider>()),
            AiProviderType.OpenAI => new OpenAiProvider(configuration, loggerFactory.CreateLogger<OpenAiProvider>()),
            AiProviderType.Ollama => new OllamaAiProvider(configuration, loggerFactory.CreateLogger<OllamaAiProvider>()),
            _ => throw new ArgumentOutOfRangeException(nameof(providerType), providerType, "Unknown AI provider type")
        };
    }

    public AiProviderType GetDefaultProviderType()
    {
        var providerStr = configuration["AI:DefaultProvider"] ?? "Anthropic";
        return Enum.Parse<AiProviderType>(providerStr, ignoreCase: true);
    }

    public IAiProvider CreateDefault() => Create(GetDefaultProviderType());
}
```

- [ ] **Step 4: Verify build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeds (provider implementations referenced but not yet created — factory compiles because it's `internal`).

Note: The build will fail at this step because `AnthropicAiProvider`, `OpenAiProvider`, and `OllamaAiProvider` don't exist yet. This is expected — they are created in Task 6. If following strict TDD order, create stub implementations first. Otherwise, create Task 5 and Task 6 together.

- [ ] **Step 5: Commit**

```bash
cd boilerplateBE
git add src/modules/Starter.Module.AI/Infrastructure/Providers/IAiProvider.cs \
       src/modules/Starter.Module.AI/Infrastructure/Providers/AiProviderTypes.cs \
       src/modules/Starter.Module.AI/Infrastructure/Providers/AiProviderFactory.cs
git commit -m "feat(ai): add internal LLM provider abstraction and factory"
```

---

### Task 6: Provider implementations (Anthropic, OpenAI, Ollama)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/AnthropicAiProvider.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/OpenAiProvider.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/OllamaAiProvider.cs`

- [ ] **Step 1: Create Anthropic provider**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/AnthropicAiProvider.cs`:
```csharp
using System.Runtime.CompilerServices;
using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Starter.Module.AI.Infrastructure.Providers;

internal sealed class AnthropicAiProvider(
    IConfiguration configuration,
    ILogger<AnthropicAiProvider> logger) : IAiProvider
{
    private AnthropicClient CreateClient()
    {
        var apiKey = configuration["AI:Providers:Anthropic:ApiKey"]
            ?? throw new InvalidOperationException("Anthropic API key not configured");
        return new AnthropicClient(apiKey);
    }

    public async Task<AiChatCompletion> ChatAsync(
        IReadOnlyList<AiChatMessage> messages, AiChatOptions options, CancellationToken ct = default)
    {
        var client = CreateClient();
        var request = BuildRequest(messages, options);

        var response = await client.Messages.GetClaudeMessageAsync(request, ct);

        return MapCompletion(response);
    }

    public async IAsyncEnumerable<AiChatChunk> StreamChatAsync(
        IReadOnlyList<AiChatMessage> messages, AiChatOptions options,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var client = CreateClient();
        var request = BuildRequest(messages, options);

        await foreach (var streamEvent in client.Messages.StreamClaudeMessageAsync(request, ct))
        {
            if (streamEvent is ContentBlockDeltaEventArgs delta && delta.Delta?.Text is not null)
            {
                yield return new AiChatChunk(delta.Delta.Text, null, null);
            }
            else if (streamEvent is MessageCompleteEventArgs complete)
            {
                yield return new AiChatChunk(null, null, complete.Message?.StopReason);
            }
        }
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        // Anthropic doesn't have a native embedding API — delegate to OpenAI or Voyage
        // For now, throw; the system should be configured to use OpenAI for embeddings
        throw new NotSupportedException(
            "Anthropic does not support embeddings natively. Configure OpenAI or Ollama as the embedding provider.");
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        throw new NotSupportedException(
            "Anthropic does not support embeddings natively. Configure OpenAI or Ollama as the embedding provider.");
    }

    private MessageParameters BuildRequest(IReadOnlyList<AiChatMessage> messages, AiChatOptions options)
    {
        var model = options.Model
            ?? configuration["AI:Providers:Anthropic:DefaultModel"]
            ?? "claude-sonnet-4-20250514";

        var maxTokens = options.MaxTokens > 0 ? options.MaxTokens
            : int.TryParse(configuration["AI:Providers:Anthropic:MaxTokens"], out var configMax) ? configMax : 4096;

        var anthropicMessages = messages
            .Where(m => m.Role is "user" or "assistant")
            .Select(m => new Message
            {
                Role = m.Role == "user" ? RoleType.User : RoleType.Assistant,
                Content = m.Content ?? string.Empty
            })
            .ToList();

        var request = new MessageParameters
        {
            Model = model,
            MaxTokens = maxTokens,
            Temperature = (decimal)options.Temperature,
            Messages = anthropicMessages
        };

        if (!string.IsNullOrEmpty(options.SystemPrompt))
        {
            request.System = [new SystemMessage(options.SystemPrompt)];
        }

        if (options.Tools is { Count: > 0 })
        {
            request.Tools = options.Tools.Select(t => new Function(t.Name, t.Description)
            {
                InputSchema = t.ParameterSchema
            }).Cast<ToolBase>().ToList();
        }

        return request;
    }

    private static AiChatCompletion MapCompletion(MessageResponse response)
    {
        var content = string.Join("", response.Content
            .Where(c => c is TextContent)
            .Cast<TextContent>()
            .Select(c => c.Text));

        var toolCalls = response.Content
            .Where(c => c is ToolUseContent)
            .Cast<ToolUseContent>()
            .Select(t => new AiToolCall(t.Id, t.Name, JsonSerializer.Serialize(t.Input)))
            .ToList();

        return new AiChatCompletion(
            Content: string.IsNullOrEmpty(content) ? null : content,
            ToolCalls: toolCalls.Count > 0 ? toolCalls : null,
            InputTokens: (int)(response.Usage?.InputTokens ?? 0),
            OutputTokens: (int)(response.Usage?.OutputTokens ?? 0),
            FinishReason: response.StopReason ?? "end_turn");
    }
}
```

**Note:** The exact Anthropic SDK API may differ from what's shown. The implementer should check the `Anthropic.SDK` NuGet package docs and adjust method names accordingly. The structure and pattern are correct.

- [ ] **Step 2: Create OpenAI provider**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/OpenAiProvider.cs`:
```csharp
using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace Starter.Module.AI.Infrastructure.Providers;

internal sealed class OpenAiProvider(
    IConfiguration configuration,
    ILogger<OpenAiProvider> logger) : IAiProvider
{
    private OpenAIClient CreateClient()
    {
        var apiKey = configuration["AI:Providers:OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI API key not configured");
        return new OpenAIClient(apiKey);
    }

    public async Task<AiChatCompletion> ChatAsync(
        IReadOnlyList<AiChatMessage> messages, AiChatOptions options, CancellationToken ct = default)
    {
        var client = CreateClient();
        var model = options.Model ?? configuration["AI:Providers:OpenAI:DefaultModel"] ?? "gpt-4o";
        var chatClient = client.GetChatClient(model);

        var chatMessages = MapMessages(messages, options.SystemPrompt);
        var chatOptions = BuildChatOptions(options);

        var response = await chatClient.CompleteChatAsync(chatMessages, chatOptions, ct);
        var completion = response.Value;

        return MapCompletion(completion);
    }

    public async IAsyncEnumerable<AiChatChunk> StreamChatAsync(
        IReadOnlyList<AiChatMessage> messages, AiChatOptions options,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var client = CreateClient();
        var model = options.Model ?? configuration["AI:Providers:OpenAI:DefaultModel"] ?? "gpt-4o";
        var chatClient = client.GetChatClient(model);

        var chatMessages = MapMessages(messages, options.SystemPrompt);
        var chatOptions = BuildChatOptions(options);

        await foreach (var update in chatClient.CompleteChatStreamingAsync(chatMessages, chatOptions, ct))
        {
            foreach (var part in update.ContentUpdate)
            {
                yield return new AiChatChunk(part.Text, null, null);
            }

            if (update.FinishReason.HasValue)
            {
                yield return new AiChatChunk(null, null, update.FinishReason.Value.ToString());
            }
        }
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var client = CreateClient();
        var embeddingModel = configuration["AI:Providers:OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
        var embeddingClient = client.GetEmbeddingClient(embeddingModel);

        var response = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: ct);
        return response.Value.ToFloats().ToArray();
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var client = CreateClient();
        var embeddingModel = configuration["AI:Providers:OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
        var embeddingClient = client.GetEmbeddingClient(embeddingModel);

        var response = await embeddingClient.GenerateEmbeddingsAsync(texts, cancellationToken: ct);
        return response.Value
            .OrderBy(e => e.Index)
            .Select(e => e.ToFloats().ToArray())
            .ToArray();
    }

    private static List<ChatMessage> MapMessages(IReadOnlyList<AiChatMessage> messages, string? systemPrompt)
    {
        var chatMessages = new List<ChatMessage>();

        if (!string.IsNullOrEmpty(systemPrompt))
            chatMessages.Add(ChatMessage.CreateSystemMessage(systemPrompt));

        foreach (var msg in messages)
        {
            chatMessages.Add(msg.Role switch
            {
                "user" => ChatMessage.CreateUserMessage(msg.Content ?? string.Empty),
                "assistant" => ChatMessage.CreateAssistantMessage(msg.Content ?? string.Empty),
                "system" => ChatMessage.CreateSystemMessage(msg.Content ?? string.Empty),
                _ => ChatMessage.CreateUserMessage(msg.Content ?? string.Empty)
            });
        }

        return chatMessages;
    }

    private static ChatCompletionOptions BuildChatOptions(AiChatOptions options)
    {
        var chatOptions = new ChatCompletionOptions
        {
            Temperature = (float)options.Temperature,
            MaxOutputTokenCount = options.MaxTokens
        };

        if (options.Tools is { Count: > 0 })
        {
            foreach (var tool in options.Tools)
            {
                chatOptions.Tools.Add(ChatTool.CreateFunctionTool(
                    tool.Name,
                    tool.Description,
                    BinaryData.FromString(tool.ParameterSchema.GetRawText())));
            }
        }

        return chatOptions;
    }

    private static AiChatCompletion MapCompletion(ChatCompletion completion)
    {
        var toolCalls = completion.ToolCalls
            .Select(tc => new AiToolCall(tc.Id, tc.FunctionName, tc.FunctionArguments.ToString()))
            .ToList();

        return new AiChatCompletion(
            Content: completion.Content.Count > 0 ? completion.Content[0].Text : null,
            ToolCalls: toolCalls.Count > 0 ? toolCalls : null,
            InputTokens: completion.Usage?.InputTokenCount ?? 0,
            OutputTokens: completion.Usage?.OutputTokenCount ?? 0,
            FinishReason: completion.FinishReason.ToString());
    }
}
```

- [ ] **Step 3: Create Ollama provider**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Providers/OllamaAiProvider.cs`:
```csharp
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Starter.Module.AI.Infrastructure.Providers;

internal sealed class OllamaAiProvider(
    IConfiguration configuration,
    ILogger<OllamaAiProvider> logger) : IAiProvider
{
    private string BaseUrl => configuration["AI:Providers:Ollama:BaseUrl"] ?? "http://localhost:11434";
    private string DefaultModel => configuration["AI:Providers:Ollama:DefaultModel"] ?? "llama3.1";
    private string EmbeddingModel => configuration["AI:Providers:Ollama:EmbeddingModel"] ?? "nomic-embed-text";

    private static readonly HttpClient HttpClient = new();

    public async Task<AiChatCompletion> ChatAsync(
        IReadOnlyList<AiChatMessage> messages, AiChatOptions options, CancellationToken ct = default)
    {
        var model = options.Model ?? DefaultModel;
        var ollamaMessages = MapMessages(messages, options.SystemPrompt);

        var request = new
        {
            model,
            messages = ollamaMessages,
            stream = false,
            options = new { temperature = options.Temperature }
        };

        var response = await HttpClient.PostAsJsonAsync($"{BaseUrl}/api/chat", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(ct);

        return new AiChatCompletion(
            Content: result?.Message?.Content,
            ToolCalls: null,
            InputTokens: result?.PromptEvalCount ?? 0,
            OutputTokens: result?.EvalCount ?? 0,
            FinishReason: "stop");
    }

    public async IAsyncEnumerable<AiChatChunk> StreamChatAsync(
        IReadOnlyList<AiChatMessage> messages, AiChatOptions options,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var model = options.Model ?? DefaultModel;
        var ollamaMessages = MapMessages(messages, options.SystemPrompt);

        var request = new
        {
            model,
            messages = ollamaMessages,
            stream = true,
            options = new { temperature = options.Temperature }
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/chat")
        {
            Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
        };

        var response = await HttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line)) continue;

            var chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line);
            if (chunk?.Message?.Content is not null)
            {
                yield return new AiChatChunk(chunk.Message.Content, null,
                    chunk.Done ? "stop" : null);
            }
        }
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var request = new { model = EmbeddingModel, input = text };
        var response = await HttpClient.PostAsJsonAsync($"{BaseUrl}/api/embed", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(ct);
        return result?.Embeddings?.FirstOrDefault() ?? [];
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var results = new float[texts.Count][];
        for (var i = 0; i < texts.Count; i++)
            results[i] = await EmbedAsync(texts[i], ct);
        return results;
    }

    private static List<object> MapMessages(IReadOnlyList<AiChatMessage> messages, string? systemPrompt)
    {
        var ollamaMessages = new List<object>();

        if (!string.IsNullOrEmpty(systemPrompt))
            ollamaMessages.Add(new { role = "system", content = systemPrompt });

        foreach (var msg in messages)
            ollamaMessages.Add(new { role = msg.Role, content = msg.Content ?? string.Empty });

        return ollamaMessages;
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")] public OllamaMessage? Message { get; set; }
        [JsonPropertyName("done")] public bool Done { get; set; }
        [JsonPropertyName("prompt_eval_count")] public int PromptEvalCount { get; set; }
        [JsonPropertyName("eval_count")] public int EvalCount { get; set; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")] public string? Role { get; set; }
        [JsonPropertyName("content")] public string? Content { get; set; }
    }

    private sealed class OllamaEmbedResponse
    {
        [JsonPropertyName("embeddings")] public float[][]? Embeddings { get; set; }
    }
}
```

- [ ] **Step 4: Verify build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeds. All three providers compile.

- [ ] **Step 5: Commit**

```bash
cd boilerplateBE
git add src/modules/Starter.Module.AI/Infrastructure/Providers/
git commit -m "feat(ai): add Anthropic, OpenAI, and Ollama provider implementations"
```

---

### Task 7: AIModule.cs + permissions + AiService + usage metric calculator

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/AiService.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/AiUsageMetricCalculator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

- [ ] **Step 1: Create permissions**

Create `boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs`:
```csharp
namespace Starter.Module.AI.Constants;

public static class AiPermissions
{
    public const string Chat = "Ai.Chat";
    public const string ViewConversations = "Ai.ViewConversations";
    public const string ManageAssistants = "Ai.ManageAssistants";
    public const string ManageDocuments = "Ai.ManageDocuments";
    public const string ManageTools = "Ai.ManageTools";
    public const string ManageTriggers = "Ai.ManageTriggers";
    public const string ViewUsage = "Ai.ViewUsage";
    public const string RunAgentTasks = "Ai.RunAgentTasks";
    public const string ManageSettings = "Ai.ManageSettings";
}
```

- [ ] **Step 2: Create AiService (implements IAiService capability)**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/AiService.cs`:
```csharp
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Module.AI.Infrastructure.Services;

internal sealed class AiService(
    AiProviderFactory providerFactory,
    ILogger<AiService> logger) : IAiService
{
    public async Task<AiCompletionResult?> CompleteAsync(
        string prompt, AiCompletionOptions? options = null, CancellationToken ct = default)
    {
        try
        {
            var provider = providerFactory.CreateDefault();
            var messages = new List<AiChatMessage>
            {
                new("user", prompt)
            };

            var chatOptions = new AiChatOptions(
                Model: options?.Model ?? string.Empty,
                Temperature: options?.Temperature ?? 0.7,
                MaxTokens: options?.MaxTokens ?? 4096);

            var completion = await provider.ChatAsync(messages, chatOptions, ct);
            var tokensUsed = completion.InputTokens + completion.OutputTokens;

            return new AiCompletionResult(completion.Content ?? string.Empty, tokensUsed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI completion failed");
            return null;
        }
    }

    public async Task<string?> SummarizeAsync(
        string content, string? instructions = null, CancellationToken ct = default)
    {
        var prompt = string.IsNullOrEmpty(instructions)
            ? $"Summarize the following content concisely:\n\n{content}"
            : $"{instructions}\n\nContent:\n{content}";

        var result = await CompleteAsync(prompt, new AiCompletionOptions(Temperature: 0.3), ct);
        return result?.Content;
    }

    public async Task<AiClassificationResult?> ClassifyAsync(
        string content, IReadOnlyList<string> categories, CancellationToken ct = default)
    {
        var categoryList = string.Join(", ", categories);
        var prompt = $"Classify the following text into exactly one of these categories: {categoryList}\n\n" +
                     $"Text: {content}\n\n" +
                     $"Respond with only the category name, nothing else.";

        var result = await CompleteAsync(prompt, new AiCompletionOptions(Temperature: 0.0), ct);
        if (result is null) return null;

        var category = result.Content.Trim();
        var confidence = categories.Contains(category, StringComparer.OrdinalIgnoreCase) ? 0.9 : 0.5;

        return new AiClassificationResult(category, confidence);
    }

    public async Task<float[]?> EmbedAsync(string text, CancellationToken ct = default)
    {
        try
        {
            // Use OpenAI for embeddings by default (Anthropic doesn't support embeddings)
            var provider = providerFactory.Create(AiProviderType.OpenAI);
            return await provider.EmbedAsync(text, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI embedding failed");
            return null;
        }
    }
}
```

- [ ] **Step 3: Create usage metric calculator**

Create `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/AiUsageMetricCalculator.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Services;

internal sealed class AiUsageMetricCalculator(AiDbContext db) : IUsageMetricCalculator
{
    public string Metric => "ai_tokens";

    public async Task<long> CalculateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await db.AiUsageLogs
            .IgnoreQueryFilters()
            .Where(l => l.TenantId == tenantId)
            .SumAsync(l => (long)l.InputTokens + l.OutputTokens, cancellationToken);
}
```

- [ ] **Step 4: Create AIModule.cs**

Create `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Modularity;
using Starter.Module.AI.Constants;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Services;

namespace Starter.Module.AI;

public sealed class AIModule : IModule
{
    public string Name => "Starter.Module.AI";
    public string DisplayName => "AI Integration";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // DbContext with module-specific migration history table
        services.AddDbContext<AiDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_AI");
                    npgsqlOptions.MigrationsAssembly(typeof(AiDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: ["40001"]);
                });
        });

        // Provider infrastructure
        services.AddSingleton<AiProviderFactory>();

        // IAiService capability — replaces NullAiService
        services.AddScoped<IAiService, AiService>();

        // Usage metric calculator
        services.AddScoped<IUsageMetricCalculator, AiUsageMetricCalculator>();

        return services;
    }

    public IEnumerable<(string Name, string Description, string Module)> GetPermissions()
    {
        yield return (AiPermissions.Chat, "Use AI chat", "AI");
        yield return (AiPermissions.ViewConversations, "View own conversations", "AI");
        yield return (AiPermissions.ManageAssistants, "Manage AI assistants", "AI");
        yield return (AiPermissions.ManageDocuments, "Manage knowledge base documents", "AI");
        yield return (AiPermissions.ManageTools, "Manage AI tools", "AI");
        yield return (AiPermissions.ManageTriggers, "Manage agent triggers", "AI");
        yield return (AiPermissions.ViewUsage, "View AI usage statistics", "AI");
        yield return (AiPermissions.RunAgentTasks, "Start background agent tasks", "AI");
        yield return (AiPermissions.ManageSettings, "Configure AI provider settings", "AI");
    }

    public IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions()
    {
        yield return ("SuperAdmin", [
            AiPermissions.Chat, AiPermissions.ViewConversations,
            AiPermissions.ManageAssistants, AiPermissions.ManageDocuments,
            AiPermissions.ManageTools, AiPermissions.ManageTriggers,
            AiPermissions.ViewUsage, AiPermissions.RunAgentTasks,
            AiPermissions.ManageSettings
        ]);

        yield return ("Admin", [
            AiPermissions.Chat, AiPermissions.ViewConversations,
            AiPermissions.ManageAssistants, AiPermissions.ManageDocuments,
            AiPermissions.ManageTools, AiPermissions.ManageTriggers,
            AiPermissions.ViewUsage, AiPermissions.RunAgentTasks
        ]);

        yield return ("User", [
            AiPermissions.Chat, AiPermissions.ViewConversations
        ]);
    }

    public async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Verify build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeds.

- [ ] **Step 6: Commit**

```bash
cd boilerplateBE
git add src/modules/Starter.Module.AI/Constants/ \
       src/modules/Starter.Module.AI/Infrastructure/Services/ \
       src/modules/Starter.Module.AI/AIModule.cs
git commit -m "feat(ai): add AIModule with permissions, AiService capability, and usage metric calculator"
```

---

### Task 8: Docker Compose (Qdrant) + appsettings configuration

**Files:**
- Modify: `boilerplateBE/docker-compose.yml`
- Modify: `boilerplateBE/src/Starter.Api/appsettings.json`
- Modify: `boilerplateBE/src/Starter.Api/appsettings.Development.json`

- [ ] **Step 1: Add Qdrant to Docker Compose**

Add the following service to `boilerplateBE/docker-compose.yml` alongside existing services:

```yaml
  qdrant:
    image: qdrant/qdrant:v1.14.0
    container_name: starter-qdrant
    ports:
      - "6333:6333"
      - "6334:6334"
    volumes:
      - qdrant_data:/qdrant/storage
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:6333/healthz"]
      interval: 30s
      timeout: 10s
      retries: 3
```

Add `qdrant_data:` to the `volumes:` section at the bottom.

- [ ] **Step 2: Add AI config section to appsettings.json**

Add the following section to `boilerplateBE/src/Starter.Api/appsettings.json`:

```json
"AI": {
  "Enabled": true,
  "DefaultProvider": "Anthropic",
  "Providers": {
    "OpenAI": {
      "ApiKey": "",
      "DefaultModel": "gpt-4o",
      "EmbeddingModel": "text-embedding-3-small",
      "CostPerInputToken": 0.0000025,
      "CostPerOutputToken": 0.00001
    },
    "Anthropic": {
      "ApiKey": "",
      "DefaultModel": "claude-sonnet-4-20250514",
      "MaxTokens": 4096,
      "CostPerInputToken": 0.000003,
      "CostPerOutputToken": 0.000015
    },
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "DefaultModel": "llama3.1",
      "EmbeddingModel": "nomic-embed-text"
    }
  },
  "Qdrant": {
    "Host": "localhost",
    "GrpcPort": 6334,
    "HttpPort": 6333,
    "ApiKey": ""
  },
  "Rag": {
    "ChunkSize": 512,
    "ChunkOverlap": 50,
    "ParentChunkSize": 1536,
    "TopK": 5,
    "RetrievalTopK": 20,
    "HybridSearchWeight": 0.7,
    "EnableQueryExpansion": true,
    "EnableReranking": true
  },
  "Agent": {
    "DefaultMaxSteps": 10,
    "StepTimeoutSeconds": 30,
    "MaxConcurrentAgentTasks": 5
  },
  "Ocr": {
    "Enabled": true,
    "Provider": "Tesseract"
  }
}
```

- [ ] **Step 3: Add AI dev config to appsettings.Development.json**

Add development-specific overrides (if not already covered by the base config):

```json
"AI": {
  "Enabled": true,
  "DefaultProvider": "Anthropic"
}
```

- [ ] **Step 4: Verify build**

Run: `cd boilerplateBE && dotnet build`
Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
cd boilerplateBE
git add docker-compose.yml \
       src/Starter.Api/appsettings.json \
       src/Starter.Api/appsettings.Development.json
git commit -m "feat(ai): add Qdrant to Docker Compose and AI configuration to appsettings"
```

---

### Task 9: EF Core migration + build verification

**Files:**
- Create: EF migration files (auto-generated)

- [ ] **Step 1: Start Docker services**

Run: `cd boilerplateBE && docker compose up -d`
Expected: All services including Qdrant start successfully.

- [ ] **Step 2: Generate EF Core migration**

Run:
```bash
cd boilerplateBE
dotnet ef migrations add InitialAI \
  --project src/modules/Starter.Module.AI \
  --startup-project src/Starter.Api \
  --context AiDbContext
```
Expected: Migration files created in `src/modules/Starter.Module.AI/Infrastructure/Persistence/Migrations/`

- [ ] **Step 3: Apply migration**

Run:
```bash
cd boilerplateBE
dotnet ef database update \
  --project src/modules/Starter.Module.AI \
  --startup-project src/Starter.Api \
  --context AiDbContext
```
Expected: Tables created in PostgreSQL with `__EFMigrationsHistory_AI` migration history table.

- [ ] **Step 4: Verify Qdrant is accessible**

Run:
```bash
curl http://localhost:6333/healthz
```
Expected: Returns `ok` or health status JSON.

- [ ] **Step 5: Full build + run verification**

Run:
```bash
cd boilerplateBE/src/Starter.Api && dotnet run --launch-profile http
```
Expected: Application starts. In logs, you should see the AI module being discovered and loaded. Check Swagger at `http://localhost:5000/swagger` — no AI controllers yet (those come in Plan 2), but the app should start cleanly with the new module.

- [ ] **Step 6: Commit**

```bash
cd boilerplateBE
git add src/modules/Starter.Module.AI/Infrastructure/Persistence/Migrations/
git commit -m "feat(ai): add initial EF Core migration for AI module tables"
```

---

## Verification Checklist

After all tasks are complete, verify:

1. `dotnet build` succeeds for the entire solution
2. The app starts cleanly with `dotnet run`
3. The AI module is discovered in startup logs
4. PostgreSQL has all 9 `ai_*` tables with correct columns
5. The `__EFMigrationsHistory_AI` table exists (separate from other modules)
6. Qdrant is accessible on ports 6333/6334
7. `IAiService` is resolvable from DI (the real `AiService`, not `NullAiService`)
8. Removing the AI module DLL falls back to `NullAiService` (verify by temporarily renaming the DLL)
9. Existing features (users, products, etc.) still work — no regressions
