# AI Integration Module: Design Specification

**Date:** 2026-04-13
**Status:** Draft
**Scope:** Full AI platform module — LLM abstraction, RAG pipeline, configurable assistants, function calling, autonomous agents, usage tracking
**Architecture:** Approach C — Full module (`Starter.Module.AI`) + thin `IAiService` capability in `Starter.Abstractions`

---

## Vision

Make any system built from this boilerplate **AI-native**. The AI Integration Module provides a complete AI platform that serves three layers:

| Layer | User | Value Delivered |
|-------|------|----------------|
| **Developer** | Person using the boilerplate | AI is pre-built — configure provider keys and go. No custom LLM plumbing needed. |
| **Tenant Admin** | Business using the built product | Configure domain-specific assistants, upload knowledge bases, control costs — no engineering help required. |
| **End User** | Employee/customer in daily use | AI sidebar + inline helpers that answer questions, summarize data, and take real actions in the system. |

### Core Principles

- **AI is a first-class actor** — executes actions as the user, through the same MediatR pipeline (validation, authorization, audit). The AI can never do more than the user could.
- **Configurable per assistant** — each assistant has its own system prompt, enabled tools, knowledge base, and execution mode. Tenant admin creates domain agents (HR, accounting, support) without code.
- **RAG + live system data** — AI knows both uploaded documents (policies, manuals, textbooks) and live system entities (orders, users, reports).
- **Platform-managed LLM keys** — centrally configured by the platform owner. Tenants consume AI through the platform, billed via the existing billing module.
- **Composable** — the module is optional. When absent, `NullAiService` makes all AI calls silent no-ops. When present, the entire system becomes AI-enhanced.

### Target Use Cases

| Domain Agent | System Prompt Focus | Key Tools | Knowledge Base |
|-------------|-------------------|-----------|----------------|
| HR Manager | Employee management, leave, compliance | Leave CRUD, attendance, employee profiles | HR policies, labor laws |
| Accountant | Financial analysis, expense tracking | Payment queries, invoice creation, reports | Tax rules, expense policies |
| School Admin | Student management, enrollment | Enrollment workflows, grade reports | School policies, curriculum |
| AI Tutor | Subject-matter tutoring, homework help | Grade lookup, assignment queries | Textbooks, course materials |
| Customer Support | Ticket resolution, FAQ | Order lookup, refund processing | Product docs, FAQ, SLA policies |
| Sales Analyst | CRM insights, lead scoring | Contact queries, order analytics | Sales playbooks |

All built on the same engine — the tenant admin creates these through the admin UI.

---

## Architecture

### Approach: Module + Thin Capability

```
Starter.Abstractions/Capabilities/
├── IAiService.cs              ← 4 methods, thin window into AI
├── IAiToolDefinition.cs       ← tool registration interface (any module can implement)
└── (NullAiService in Infrastructure/Capabilities/NullObjects/)

Starter.Module.AI/             ← Full AI platform (optional module)
├── Domain entities, CQRS handlers, controllers
├── Provider implementations (OpenAI, Anthropic, Ollama)
├── RAG pipeline (chunking, embedding, Qdrant)
├── Agent execution engine
└── Implements IAiService capability + consumes IAiToolDefinition registrations
```

**Why this approach:** Same pattern as `IWebhookPublisher` — other modules can call `aiService.SummarizeAsync(content)` and it works with or without the AI module installed. Modules register `IAiToolDefinition` in Abstractions (like registering `IUsageMetricCalculator`) — when AI module is absent, registrations are simply unused. The AI module itself contains all the heavy lifting.

### Thin Capability (Starter.Abstractions)

```csharp
public interface IAiService : ICapability
{
    Task<AiCompletionResult> CompleteAsync(
        string prompt, AiCompletionOptions? options = null, CancellationToken ct = default);

    Task<string?> SummarizeAsync(
        string content, string? instructions = null, CancellationToken ct = default);

    Task<AiClassificationResult> ClassifyAsync(
        string content, IReadOnlyList<string> categories, CancellationToken ct = default);

    Task<float[]?> EmbedAsync(
        string text, CancellationToken ct = default);
}

public sealed record AiCompletionResult(string Content, int TokensUsed);
public sealed record AiClassificationResult(string Category, double Confidence);
public sealed record AiCompletionOptions(string? Model = null, double? Temperature = null, int? MaxTokens = null);
```

`NullAiService` returns `null`/empty for all methods.

### Module Structure

```
Starter.Module.AI/
├── AIModule.cs                           # IModule implementation
├── Constants/AiPermissions.cs
├── Domain/
│   ├── Entities/
│   │   ├── AiAssistant.cs
│   │   ├── AiConversation.cs
│   │   ├── AiMessage.cs
│   │   ├── AiDocument.cs
│   │   ├── AiDocumentChunk.cs
│   │   ├── AiTool.cs
│   │   ├── AiAgentTask.cs
│   │   ├── AiAgentTrigger.cs
│   │   └── AiUsageLog.cs
│   ├── Enums/
│   │   ├── AiProvider.cs
│   │   ├── MessageRole.cs
│   │   ├── EmbeddingStatus.cs
│   │   ├── AgentTaskStatus.cs
│   │   ├── TriggerType.cs
│   │   └── AiToolExecutionMode.cs
│   ├── Errors/AiErrors.cs
│   └── Events/
│       ├── AiChatCompletedEvent.cs
│       ├── AiDocumentProcessedEvent.cs
│       └── AiAgentTaskCompletedEvent.cs
├── Application/
│   ├── Commands/
│   │   ├── SendChatMessage/
│   │   ├── CreateAssistant/
│   │   ├── UpdateAssistant/
│   │   ├── DeleteAssistant/
│   │   ├── UploadDocument/
│   │   ├── DeleteDocument/
│   │   ├── ReprocessDocument/
│   │   ├── ToggleTool/
│   │   ├── StartAgentTask/
│   │   ├── CancelAgentTask/
│   │   ├── SendTaskMessage/
│   │   ├── CreateTrigger/
│   │   ├── UpdateTrigger/
│   │   ├── DeleteTrigger/
│   │   ├── DeleteConversation/
│   │   └── UpdateAiSettings/
│   ├── Queries/
│   │   ├── GetConversations/
│   │   ├── GetConversationById/
│   │   ├── GetAssistants/
│   │   ├── GetAssistantById/
│   │   ├── GetDocuments/
│   │   ├── GetDocumentById/
│   │   ├── GetTools/
│   │   ├── GetAgentTasks/
│   │   ├── GetAgentTaskById/
│   │   ├── GetTriggers/
│   │   ├── SemanticSearch/
│   │   ├── GetAiUsage/
│   │   └── GetAiSettings/
│   ├── DTOs/
│   ├── EventHandlers/
│   └── Services/
│       ├── IAiProviderFactory.cs
│       ├── IAiToolRegistry.cs
│       ├── IAiExecutionEngine.cs
│       └── IRagPipeline.cs
├── Infrastructure/
│   ├── Persistence/
│   │   ├── AiDbContext.cs
│   │   └── Configurations/
│   ├── Providers/
│   │   ├── OpenAiProvider.cs
│   │   ├── AnthropicProvider.cs
│   │   └── OllamaProvider.cs
│   ├── Qdrant/
│   │   └── QdrantVectorStore.cs
│   ├── Rag/
│   │   ├── DocumentProcessor.cs
│   │   ├── TextChunker.cs
│   │   ├── OcrProcessor.cs
│   │   └── HybridSearchService.cs
│   ├── Services/
│   │   ├── AiService.cs              # Implements IAiService capability
│   │   ├── AiExecutionEngine.cs
│   │   ├── AiToolRegistryService.cs
│   │   └── AiUsageMetricCalculator.cs
│   └── Consumers/
│       ├── ProcessDocumentConsumer.cs
│       └── ExecuteAgentTaskConsumer.cs
└── Controllers/
    ├── AiChatController.cs
    ├── AiAssistantsController.cs
    ├── AiDocumentsController.cs
    ├── AiToolsController.cs
    ├── AiAgentTasksController.cs
    ├── AiTriggersController.cs
    └── AiSettingsController.cs
```

---

## Data Model

### Entity: AiAssistant

Configurable AI assistant per tenant. Tenant admins create domain-specific agents.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| TenantId | Guid? | Tenant scope |
| Name | string | Display name (e.g., "HR Assistant") |
| Description | string? | Purpose description |
| SystemPrompt | string | Instructions defining the assistant's behavior and role |
| Provider | AiProvider | OpenAI, Anthropic, Ollama (overrides tenant default if set) |
| Model | string | Specific model (e.g., "claude-sonnet-4-20250514") |
| Temperature | double | 0.0–1.0, controls response creativity |
| MaxTokens | int | Max response tokens |
| EnabledToolNames | JSON (string[]) | Which MediatR tools this assistant can invoke |
| KnowledgeBaseDocIds | JSON (Guid[]) | Which documents to use for RAG context |
| ExecutionMode | enum | Chat (single-turn) or Agent (multi-step autonomous) |
| MaxAgentSteps | int | Safety limit for autonomous execution (default: 10) |
| IsActive | bool | Can be disabled without deleting |
| CreatedAt | DateTime | |
| ModifiedAt | DateTime? | |
| CreatedByUserId | Guid | |

### Entity: AiConversation

A chat session between a user and an assistant.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| TenantId | Guid? | Tenant scope |
| AssistantId | Guid | FK → AiAssistant |
| UserId | Guid | The user who started the conversation |
| Title | string? | Auto-generated or user-set |
| Status | enum | Active, Completed, Failed |
| MessageCount | int | Denormalized count |
| TotalTokensUsed | int | Denormalized sum |
| CreatedAt | DateTime | |
| LastMessageAt | DateTime | |

### Entity: AiMessage

Individual message in a conversation.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| ConversationId | Guid | FK → AiConversation |
| Role | MessageRole | User, Assistant, System, Tool, ToolResult |
| Content | string? | Message text (null for tool calls without text) |
| ToolCalls | JSON? | [{name, arguments, callId}] — when assistant requests tool execution |
| ToolCallId | string? | For ToolResult messages: which tool call this responds to |
| InputTokens | int | Tokens used for this message's input context |
| OutputTokens | int | Tokens generated in response |
| Timestamp | DateTime | |
| Order | int | Sequential ordering within conversation |

### Entity: AiDocument

Knowledge base document uploaded for RAG.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| TenantId | Guid? | Tenant scope |
| Name | string | Display name |
| FileName | string | Original filename |
| FileRef | string | MinIO object reference |
| ContentType | string | MIME type |
| SizeBytes | long | File size |
| ChunkCount | int | Number of chunks created |
| EmbeddingStatus | EmbeddingStatus | Pending, Processing, Completed, Failed |
| ErrorMessage | string? | If processing failed |
| RequiresOcr | bool | Auto-detected during processing |
| OcrProvider | string? | Provider used for OCR if applicable |
| ProcessedAt | DateTime? | When processing completed |
| CreatedAt | DateTime | |
| UploadedByUserId | Guid | |

### Entity: AiDocumentChunk

Chunked and embedded piece of a document. Hierarchical parent-child structure.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| DocumentId | Guid | FK → AiDocument |
| ParentChunkId | Guid? | Self-referencing FK for child→parent relationship |
| ChunkLevel | enum | Parent (1536 tokens) or Child (512 tokens) |
| Content | string | Chunk text |
| ChunkIndex | int | Sequential order within document |
| SectionTitle | string? | Extracted from document headings |
| PageNumber | int? | Source page number |
| TokenCount | int | Token count for this chunk |
| QdrantPointId | Guid | Reference to vector in Qdrant |

### Entity: AiTool

Registered function-calling tool that maps to a MediatR command.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| Name | string | Tool name (e.g., "create_product") |
| Description | string | Human-readable description for the LLM |
| ParameterSchema | JSON | JSON Schema describing tool parameters |
| CommandType | string | Fully qualified MediatR command type name |
| RequiredPermission | string | Permission the user must have to use this tool |
| Category | string | Grouping (e.g., "Products", "Users", "Orders") |
| IsEnabled | bool | Global enable/disable |
| IsReadOnly | bool | Whether this tool only reads data (no mutations) |

### Entity: AiAgentTask

Background autonomous agent execution.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| TenantId | Guid? | Tenant scope |
| AssistantId | Guid | FK → AiAssistant |
| UserId | Guid | User who initiated (permissions applied as this user) |
| Instruction | string | What the user asked the agent to do |
| Status | AgentTaskStatus | Queued, Running, Completed, Failed, Cancelled |
| Steps | JSON | Execution log: [{thought, action, input, output, timestamp}] |
| Result | string? | Final agent output/summary |
| TotalTokensUsed | int | |
| StepCount | int | |
| TriggeredBy | enum | User, Schedule, Event |
| TriggerId | Guid? | FK → AiAgentTrigger (if triggered) |
| StartedAt | DateTime? | |
| CompletedAt | DateTime? | |
| CreatedAt | DateTime | |

### Entity: AiAgentTrigger

Scheduled or event-driven agent task triggers.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| TenantId | Guid? | Tenant scope |
| AssistantId | Guid | FK → AiAssistant |
| Name | string | Display name (e.g., "Weekly Sales Report") |
| Description | string? | |
| TriggerType | TriggerType | Cron or DomainEvent |
| CronExpression | string? | For scheduled triggers (e.g., "0 9 * * MON") |
| EventType | string? | For event triggers (e.g., "order.created") |
| Instruction | string | What the agent should do when triggered |
| IsActive | bool | |
| LastRunAt | DateTime? | |
| NextRunAt | DateTime? | Computed from cron for scheduled triggers |
| CreatedAt | DateTime | |
| CreatedByUserId | Guid | |

### Entity: AiUsageLog

Per-request usage audit trail.

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| TenantId | Guid? | Tenant scope |
| UserId | Guid | |
| ConversationId | Guid? | FK (nullable — embeddings don't have conversations) |
| AgentTaskId | Guid? | FK (nullable — for agent step tracking) |
| Provider | AiProvider | Which provider was used |
| Model | string | Specific model used |
| InputTokens | int | |
| OutputTokens | int | |
| EstimatedCost | decimal | Based on configured pricing per provider/model |
| RequestType | enum | Chat, Completion, Embedding, AgentStep |
| Timestamp | DateTime | |

---

## Execution Engine

### The Agent Loop

The core execution engine handles both chat (single-turn) and agent tasks (multi-step) with the same pipeline.

```
User/Trigger sends instruction
        │
        ▼
┌─────────────────────────────────────────┐
│            AiExecutionEngine             │
│                                         │
│  1. Load assistant config               │
│     - System prompt, model, temperature │
│     - Enabled tools, knowledge base     │
│                                         │
│  2. Build context                       │
│     - Conversation history (chat)       │
│     - Or instruction + step log (agent) │
│     - RAG context (if knowledge base)   │
│                                         │
│  3. Check quota                         │
│     - quotaChecker.CheckAsync()         │
│     - Reject if exceeded               │
│                                         │
│  4. Call LLM via IAiProvider            │
│     - Chat: stream response via SSE     │
│     - Agent: full response              │
│                                         │
│  5. Process response                    │
│     ├── Text only → Return to user      │
│     └── Tool calls →                    │
│         a. Validate user has permission │
│         b. Deserialize to MediatR cmd   │
│         c. Send via ISender.Send()      │
│         d. Append tool result to context│
│         e. Go to step 4 (loop)          │
│                                         │
│  6. Safety checks                       │
│     - Step count < MaxAgentSteps        │
│     - Token budget remaining            │
│     - No infinite loops detected        │
│                                         │
│  7. Log usage                           │
│     - AiUsageLog entry                  │
│     - Update usage tracker (Redis)      │
│     - Increment quota                   │
│                                         │
│  8. Publish events                      │
│     - Webhook: "ai.chat.completed"      │
│     - Domain event for audit            │
└─────────────────────────────────────────┘
```

### Key Design Decisions

**Tool execution runs as the user.** `ICurrentUserService` is set to the user who initiated the chat/task. The MediatR pipeline enforces their permissions. The AI operates within the user's authorization scope — it can never do more than the user could manually.

**Step limit prevents runaway agents.** `MaxAgentSteps` per assistant (default: 10, configurable). If an agent reaches the limit, it returns its partial results with a note that it hit the step limit.

**Streaming for chat.** SSE endpoint streams tokens in real-time. When the LLM requests a tool call, the stream pauses, the tool executes, the result is appended, and streaming resumes. The frontend shows tool execution status inline ("Searching orders... Found 3 results").

**Background execution for agent tasks.** Agent tasks run via MassTransit consumer (`ExecuteAgentTaskConsumer`). The user gets a task ID immediately and can:
- Poll for status via `GET /api/v1/ai/tasks/{id}`
- Send a message to redirect the agent: `POST /api/v1/ai/tasks/{id}/message`
- Cancel: `POST /api/v1/ai/tasks/{id}/cancel`
- Get notified via Ably when complete

**User can interrupt running agents.** Sending a message to a running task injects it into the agent's context. The agent reads it on its next iteration and can adjust its approach. This supports the "discuss in the middle" pattern.

**Self-evaluation.** The agent's system prompt includes self-evaluation instructions: "After completing your task, review your work. Did you achieve the user's goal? Are there issues? Suggest improvements if applicable." This is configurable per assistant.

### Function Calling → MediatR Bridge

The `IAiToolDefinition` interface lives in `Starter.Abstractions/Capabilities/` (not in the AI module) so any module can register tools without cross-module dependencies:

```csharp
// Starter.Abstractions/Capabilities/IAiToolDefinition.cs
public interface IAiToolDefinition
{
    string Name { get; }
    string Description { get; }
    JsonElement ParameterSchema { get; }
    Type CommandType { get; }
    string RequiredPermission { get; }
    string Category { get; }
    bool IsReadOnly { get; }
}

// Example: Products module registers its tools (no AI module reference needed)
public class CreateProductAiTool : IAiToolDefinition
{
    public string Name => "create_product";
    public string Description => "Create a new product in the catalog with name, price, and description";
    public JsonElement ParameterSchema => JsonSchema.FromType<CreateProductCommand>();
    public Type CommandType => typeof(CreateProductCommand);
    public string RequiredPermission => ProductPermissions.Create;
    public string Category => "Products";
    public bool IsReadOnly => false;
}

// In ProductsModule.ConfigureServices:
services.AddSingleton<IAiToolDefinition, CreateProductAiTool>();
services.AddSingleton<IAiToolDefinition, GetProductsAiTool>();
services.AddSingleton<IAiToolDefinition, UpdateProductAiTool>();
```

When AI module is absent, `IAiToolDefinition` registrations are simply unused (no NullObject needed — nothing reads them). When present, the `AiToolRegistryService` collects all registered instances at startup. The `AiTool` entities in the database track enable/disable state. New tools from modules appear automatically when the module is loaded.

**Tool execution flow:**
1. LLM returns a tool call: `{name: "create_product", arguments: {name: "Widget", price: 9.99}}`
2. Engine looks up `IAiToolDefinition` by name
3. Checks user has `RequiredPermission`
4. Deserializes arguments into the MediatR command type
5. Sends via `ISender.Send(command)` — full pipeline (validation, auth, audit)
6. `Result<T>` is serialized back as the tool response
7. LLM receives the result and continues reasoning

---

## RAG Pipeline

### Document Processing Flow

```
Upload document (PDF, DOCX, TXT, MD, CSV)
        │
        ▼
  Save to MinIO (existing file storage)
  Create AiDocument entity (status: Pending)
        │
        ▼
  Publish ProcessDocumentMessage (MassTransit)
        │
        ▼
  ProcessDocumentConsumer (background):
  ┌──────────────────────────────────────────┐
  │ 1. Download file from MinIO              │
  │ 2. Detect content type                   │
  │    ├── Native text → Extract directly    │
  │    └── Scanned/image → OCR pipeline      │
  │        ├── Tesseract (local)             │
  │        └── Cloud OCR (Azure/Google)      │
  │ 3. Extract structure                     │
  │    - Headings, sections, page numbers    │
  │ 4. Hierarchical chunking                 │
  │    - Parent chunks: 1536 tokens          │
  │    - Child chunks: 512 tokens, 50 overlap│
  │ 5. Generate embeddings for child chunks  │
  │    - via IAiProvider.EmbedAsync()        │
  │ 6. Store vectors in Qdrant              │
  │    - Collection: tenant_{tenantId}       │
  │    - Payload: document_id, section,      │
  │      page_number, parent_chunk_id        │
  │ 7. Save AiDocumentChunk entities         │
  │ 8. Update AiDocument status → Completed  │
  └──────────────────────────────────────────┘
```

### Hierarchical Chunking

Two levels of chunks for precision + context:

- **Parent chunks** (1536 tokens): Full sections or topics. Not embedded — used for context expansion.
- **Child chunks** (512 tokens, 50-token overlap): Specific passages. Embedded and searchable.

When a child chunk matches a query, its parent chunk is also included in the LLM context — providing the surrounding context that makes the answer complete.

### Hybrid Search (Semantic + Keyword)

Two search strategies combined for best retrieval:

1. **Semantic search** (Qdrant vectors): Finds conceptually related content. Handles paraphrasing and synonyms.
2. **Keyword search** (PostgreSQL full-text on `AiDocumentChunk.Content`): Finds exact term matches. Catches cases where the definition uses different phrasing.

Combined score: `α × semantic_score + (1-α) × keyword_score` where α defaults to 0.7 (configurable in `AI:Rag:HybridSearchWeight`).

### Query Expansion

Before searching, the LLM expands the user's query into multiple search terms:

```
User: "explain photosynthesis"
→ Expanded:
  - "photosynthesis definition process plants"
  - "light reactions dark reactions Calvin cycle"
  - "chloroplast ATP glucose sunlight energy"
```

Each expanded query retrieves chunks independently. Results are merged, deduplicated, and scored.

### Re-ranking

After initial retrieval (top-20 from hybrid search across expanded queries):

1. Score each chunk for relevance to the **original** question
2. Select top-5 highest-scored child chunks
3. Include their parent chunks for surrounding context
4. Inject into LLM context with source citations (document name, section, page)

### Qdrant Tenant Isolation

Each tenant gets a separate Qdrant collection: `tenant_{tenantId}`. This provides:
- Complete data isolation between tenants
- Independent scaling and indexing
- Simple cleanup when a tenant is deleted (drop collection)

### Metadata Stored Per Vector

```json
{
  "vector": [0.12, -0.34, ...],
  "payload": {
    "document_id": "uuid",
    "document_name": "Biology Textbook",
    "section_title": "Light-Dependent Reactions",
    "page_number": 45,
    "chunk_level": "child",
    "chunk_index": 12,
    "parent_chunk_id": "uuid",
    "tenant_id": "uuid"
  }
}
```

---

## LLM Provider Abstraction

### Internal Provider Interface

```csharp
// Internal to AI module — NOT in Abstractions
internal interface IAiProvider
{
    Task<ChatCompletion> ChatAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions options,
        CancellationToken ct);

    IAsyncEnumerable<ChatCompletionChunk> StreamChatAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions options,
        CancellationToken ct);

    Task<float[]> EmbedAsync(string text, CancellationToken ct);

    Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct);
}

internal sealed record ChatOptions(
    string Model,
    double Temperature = 0.7,
    int MaxTokens = 4096,
    IReadOnlyList<ToolDefinition>? Tools = null,
    string? SystemPrompt = null);

internal sealed record ChatCompletion(
    string Content,
    IReadOnlyList<ToolCall>? ToolCalls,
    int InputTokens,
    int OutputTokens,
    string FinishReason);
```

### Provider Implementations

| Provider | Chat | Streaming | Embeddings | Use Case |
|----------|------|-----------|------------|----------|
| **OpenAI** | GPT-4o, GPT-4o-mini | Yes | text-embedding-3-small/large | General purpose |
| **Anthropic** | Claude Sonnet, Claude Haiku | Yes | Via OpenAI or Voyage | Best for complex reasoning, tool use |
| **Ollama** | Llama 3.1, Mistral, etc. | Yes | nomic-embed-text, etc. | Self-hosted, privacy-sensitive |

### Provider Selection

- **Platform default**: Set in `appsettings.json` → `AI:DefaultProvider`
- **Per-assistant override**: Each `AiAssistant` can specify a different provider/model
- **Embedding provider**: Can differ from chat provider (e.g., Anthropic for chat, OpenAI for embeddings)

---

## API Endpoints

### Chat & Conversations

```
POST   /api/v1/ai/chat                    — Send message (full response)
POST   /api/v1/ai/chat/stream             — Send message (SSE streaming)
GET    /api/v1/ai/conversations            — List user's conversations
GET    /api/v1/ai/conversations/{id}       — Get conversation with messages
DELETE /api/v1/ai/conversations/{id}       — Delete conversation
```

### Assistants (admin)

```
GET    /api/v1/ai/assistants               — List assistants
POST   /api/v1/ai/assistants               — Create assistant
GET    /api/v1/ai/assistants/{id}          — Get assistant details
PUT    /api/v1/ai/assistants/{id}          — Update assistant
DELETE /api/v1/ai/assistants/{id}          — Delete assistant
GET    /api/v1/ai/assistants/{id}/tools    — List tools enabled for assistant
```

### Knowledge Base (admin)

```
POST   /api/v1/ai/documents               — Upload document for RAG
GET    /api/v1/ai/documents                — List knowledge base documents
GET    /api/v1/ai/documents/{id}          — Document details + processing status
DELETE /api/v1/ai/documents/{id}          — Remove document (cascades to Qdrant)
POST   /api/v1/ai/documents/{id}/reprocess — Re-chunk and re-embed
```

### Tools (admin)

```
GET    /api/v1/ai/tools                    — List all registered AI tools
PUT    /api/v1/ai/tools/{name}/toggle      — Enable/disable a tool globally
```

### Agent Tasks

```
POST   /api/v1/ai/tasks                   — Start a background agent task
GET    /api/v1/ai/tasks                    — List user's agent tasks
GET    /api/v1/ai/tasks/{id}              — Task status + execution log
POST   /api/v1/ai/tasks/{id}/message      — Send message to running task
POST   /api/v1/ai/tasks/{id}/cancel       — Cancel running task
```

### Agent Triggers (admin)

```
GET    /api/v1/ai/triggers                 — List triggers
POST   /api/v1/ai/triggers                 — Create trigger
GET    /api/v1/ai/triggers/{id}           — Get trigger details
PUT    /api/v1/ai/triggers/{id}           — Update trigger
DELETE /api/v1/ai/triggers/{id}           — Delete trigger
```

### Semantic Search

```
POST   /api/v1/ai/search                  — Semantic search across knowledge base
```

### Usage

```
GET    /api/v1/ai/usage                   — Token usage stats + quota remaining
```

### Provider Settings (platform admin)

```
GET    /api/v1/ai/settings                — Current AI provider configuration
PUT    /api/v1/ai/settings                — Update provider, model, API keys
```

---

## Frontend Architecture

### 1. Global Chat Sidebar

The primary AI interaction point — accessible from any page.

```
┌──────────────────────────────────────────────────┐
│  App Layout                                       │
│  ┌────────┬────────────────────┬───────────┐     │
│  │Sidebar │  Main Content      │ AI Chat   │     │
│  │(nav)   │  (existing pages)  │ (sliding  │     │
│  │        │                    │  panel)   │     │
│  │        │                    │ ┌───────┐ │     │
│  │        │                    │ │Asst.▼ │ │     │
│  │        │                    │ ├───────┤ │     │
│  │        │                    │ │Messages│ │     │
│  │        │                    │ │  ...   │ │     │
│  │        │                    │ │  ...   │ │     │
│  │        │                    │ ├───────┤ │     │
│  │        │                    │ │[Input]│ │     │
│  │        │                    │ └───────┘ │     │
│  └────────┴────────────────────┴───────────┘     │
└──────────────────────────────────────────────────┘
```

**Features:**
- Toggle via button in top bar (keyboard shortcut: `Ctrl+/` or `Cmd+/`)
- Persists across page navigation (doesn't reset when navigating)
- Streaming responses with markdown rendering
- Tool call execution shown inline ("Searching orders... Found 3 results")
- Assistant selector dropdown at top
- Conversation history list with search
- New conversation button

### 2. Contextual Inline AI

AI helpers embedded on existing entity pages (only when AI module is present):

- **Summarize button** on detail pages (order detail, user profile)
- **Smart search** on list pages (natural language → filtered query)
- **Action suggestions** based on entity state
- Uses `IAiService` capability from Abstractions — `NullAiService` when module absent

### 3. Admin Pages

**AI Assistants page:**
- CRUD table for assistant configurations
- Detail/edit page with:
  - System prompt editor (full-text, with syntax highlighting)
  - Provider + model selector dropdowns
  - Temperature slider
  - Tool enablement — checkbox list of available tools, grouped by category
  - Knowledge base linking — select documents to include
  - Execution mode toggle (Chat vs Agent)
  - Max agent steps input

**Knowledge Base page:**
- Document upload with drag-and-drop (multi-file)
- Processing status indicators (Pending → Processing → Completed/Failed)
- Document list with search and filters
- Chunk preview — view the chunks generated from a document
- Re-process button (for re-embedding after model changes)

**AI Tools page:**
- Registry of all auto-discovered tools from modules
- Enable/disable toggle per tool
- Tool details: name, description, parameter schema, required permission, category
- Read-only vs. mutation indicator

**Agent Triggers page:**
- CRUD for scheduled and event-driven agent tasks
- Cron expression builder for scheduled triggers
- Event type selector for event triggers
- Last run / next run timestamps
- Enable/disable toggle

**AI Usage Dashboard:**
- Token consumption over time (line chart, daily/weekly/monthly)
- Cost breakdown by assistant, user, and model (bar chart)
- Quota utilization bar (current usage vs. plan limit)
- Recent AI activity log (last 50 requests)
- Top consumers table (which users/assistants use the most)

### 4. Dashboard Integration

Two new dashboard cards:

- **AI Usage card** — Token usage vs. quota, trend sparkline
- **AI Insights card** — Recent insights from scheduled agent triggers

### Frontend File Structure

```
src/features/ai/
├── api/
│   ├── ai.api.ts                    # Raw API calls
│   └── ai.queries.ts                # TanStack Query hooks
├── components/
│   ├── AiChatSidebar.tsx            # Global chat sidebar
│   ├── AiChatMessage.tsx            # Message bubble (supports markdown, tool calls)
│   ├── AiChatInput.tsx              # Message input with send button
│   ├── AiAssistantSelector.tsx      # Dropdown to pick assistant
│   ├── AiConversationList.tsx       # Sidebar conversation history
│   ├── AiToolCallDisplay.tsx        # Shows tool execution inline
│   ├── AiStreamingResponse.tsx      # Handles SSE streaming display
│   ├── AiDocumentUpload.tsx         # Drag-and-drop file upload
│   ├── AiDocumentStatus.tsx         # Processing status indicator
│   ├── AiToolRegistry.tsx           # Tool list with toggles
│   ├── AiTriggerForm.tsx            # Create/edit trigger form
│   ├── AiUsageChart.tsx             # Usage over time chart
│   └── AiInlineHelper.tsx           # Contextual summarize/suggest component
├── pages/
│   ├── AiAssistantsPage.tsx         # Assistant management
│   ├── AiAssistantDetailPage.tsx    # Edit assistant
│   ├── AiKnowledgeBasePage.tsx      # Document management
│   ├── AiToolsPage.tsx              # Tool registry
│   ├── AiTriggersPage.tsx           # Trigger management
│   ├── AiUsagePage.tsx              # Usage dashboard
│   └── AiSettingsPage.tsx           # Provider settings (platform admin)
└── index.ts
```

---

## Permissions

```
Ai.Chat                — Use AI chat (end users)
Ai.ViewConversations   — View own conversations
Ai.ManageAssistants    — Create/edit/delete assistants (admin)
Ai.ManageDocuments     — Upload/delete knowledge base documents (admin)
Ai.ManageTools         — Enable/disable AI tools (admin)
Ai.ManageTriggers      — Create/manage agent triggers (admin)
Ai.ViewUsage           — View AI usage statistics
Ai.RunAgentTasks       — Start background agent tasks
Ai.ManageSettings      — Configure AI provider settings (platform admin)
```

### Default Role Mappings

| Role | Permissions |
|------|------------|
| SuperAdmin | All Ai.* permissions |
| Admin | Ai.Chat, Ai.ViewConversations, Ai.ManageAssistants, Ai.ManageDocuments, Ai.ManageTools, Ai.ManageTriggers, Ai.ViewUsage, Ai.RunAgentTasks |
| User | Ai.Chat, Ai.ViewConversations |

---

## Billing Integration

### Token-Based Usage Tracking

New usage metric: `"ai_tokens"` — tracked via the existing `IUsageTracker` (Redis-backed with self-heal from DB).

**New metric calculator:**
```csharp
public class AiTokensMetricCalculator : IUsageMetricCalculator
{
    public string Metric => "ai_tokens";
    // Sums InputTokens + OutputTokens from AiUsageLog
    // for current billing period + tenant
}
```

**Quota enforcement flow:**
```
Before each LLM call:
  1. quotaChecker.CheckAsync(tenantId, "ai_tokens", estimatedTokens)
  2. If !Allowed → return Result.Failure(AiErrors.QuotaExceeded(quota.Limit))

After each LLM call:
  1. Create AiUsageLog entry (provider, model, tokens, cost)
  2. quotaChecker.IncrementAsync(tenantId, "ai_tokens", actualTokensUsed)
```

### Plan Feature Flags

Added to billing plan seed data:

| Plan | `ai.enabled` | `ai_tokens.monthly_limit` |
|------|-------------|--------------------------|
| Free | true | 10,000 |
| Starter | true | 100,000 |
| Pro | true | 500,000 |
| Enterprise | true | unlimited |

### Cost Estimation

Each `AiUsageLog` entry records estimated cost:

```csharp
var costPerInputToken = settings.GetProviderCostPerInputToken(provider, model);
var costPerOutputToken = settings.GetProviderCostPerOutputToken(provider, model);
var estimatedCost = (inputTokens * costPerInputToken) + (outputTokens * costPerOutputToken);
```

Configurable per provider+model in system settings. Updated when providers change pricing.

---

## Infrastructure

### Docker Compose Addition

```yaml
qdrant:
  image: qdrant/qdrant:v1.14.0
  container_name: starter-qdrant
  ports:
    - "6333:6333"    # REST API
    - "6334:6334"    # gRPC
  volumes:
    - qdrant_data:/qdrant/storage
  restart: unless-stopped
  healthcheck:
    test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:6333/healthz"]
    interval: 30s
    timeout: 10s
    retries: 3
```

### Configuration (appsettings)

```json
{
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
}
```

### NuGet Dependencies (AI Module)

| Package | Purpose |
|---------|---------|
| `Anthropic.SDK` | Anthropic Claude API client |
| `OpenAI` | OpenAI API client (official .NET package) |
| `Qdrant.Client` | Qdrant gRPC client |
| `Tesseract` | OCR for scanned documents |
| `PdfPig` | PDF text extraction |
| `DocumentFormat.OpenXml` | DOCX text extraction |
| `SharpToken` or `Tiktoken` | Token counting for chunking |

### Health Check

Add AI health check endpoint that verifies:
- Qdrant connectivity (gRPC health check)
- LLM provider connectivity (lightweight API call)
- Embedding model availability

---

## Key Integration Points

### How Other Modules Use AI

Any module can optionally consume `IAiService`:

```csharp
// Example: Orders module adds AI summarization
internal sealed class GetOrderByIdQueryHandler(
    OrdersDbContext context,
    IAiService aiService)   // NullAiService when AI module absent
{
    public async Task<Result<OrderDetailDto>> Handle(...)
    {
        var order = await context.Orders.FindAsync(id);
        
        // AI enhancement — gracefully returns null when module absent
        var summary = await aiService.SummarizeAsync(
            $"Order #{order.OrderNumber}: {order.Items.Count} items, " +
            $"total ${order.Total}, status: {order.Status}",
            "Provide a brief business summary of this order");
        
        return Result.Success(order.ToDto(summary));
    }
}
```

### How AI Uses Other Modules (Function Calling)

The AI module discovers and calls other modules' MediatR commands via the tool registry:

```csharp
// Products module registers tools in ConfigureServices
services.AddSingleton<IAiToolDefinition, CreateProductAiTool>();
services.AddSingleton<IAiToolDefinition, GetProductsAiTool>();

// AI execution engine discovers all IAiToolDefinition registrations
// and makes them available to LLM as function calling tools
```

### Webhook Events Published

| Event | When |
|-------|------|
| `ai.chat.completed` | A chat message exchange completes |
| `ai.document.processed` | A knowledge base document finishes processing |
| `ai.document.failed` | A knowledge base document fails processing |
| `ai.agent.completed` | An agent task finishes |
| `ai.agent.failed` | An agent task fails |
| `ai.quota.exceeded` | A tenant hits their token quota |

---

## Module Dependency

```
Independent (no module dependencies):
  AI Integration — fully self-contained

Optional enhancements (when present):
  + Billing → enables token quota enforcement (without Billing: unlimited)
  + Webhooks → publishes ai.* events (without Webhooks: silent no-op)
  + Any module → can register IAiToolDefinition for function calling
  + Any module → can consume IAiService for AI-enhanced features
```

The AI module has **zero hard dependencies** on other modules. It uses the same capability pattern as the rest of the system — `IQuotaChecker`, `IWebhookPublisher` fall back to null objects when their modules are absent.

---

## UX Decisions (from user journey review, 2026-04-13)

### Embedding Provider
- **Configurable** via `AI:EmbeddingProvider` in appsettings (default: "OpenAI"). Supports Ollama-only deployments.

### Chat Sidebar Context
- **Opt-in page context** — a "Pin context" button attaches the current page entity to the conversation. User controls when context is shared.

### Agent Task Results
- **Notification + conversation** — result appears as a notification AND as a conversation in the chat sidebar, so the user can continue discussing it.

### Trigger Execution Identity
- **Dedicated service account** — triggers run as a system-level identity with a configurable permission set, not tied to any human user. Avoids issues with deactivated users.

### System Prompt Templates
- **Built-in + cloneable** — ship 5-6 pre-built templates (HR, Accounting, Support, etc.). Admins can also clone/duplicate any existing assistant as a starting point.

### File Attachments in Chat
- **Deferred** — not in initial scope. Chat is text-only. Users upload to knowledge base for RAG.

### Mobile Chat
- **Separate plan** — mobile AI gets its own plan after web frontend is complete.

### Real-Time Updates
- **Ably** — use existing `IRealtimeService` (Ably) already in the boilerplate. No new dependency.

### First-Time Chat Experience
- **Suggested prompts** — empty state shows "Try asking:" with 3-4 clickable suggestions driven by the selected assistant's domain.

### Message Editing & Regeneration
- **Neither** — user sends follow-up messages to correct. Keeps implementation simple.

### Quota Exceeded UX
- **Full visibility** — quota bar always visible in sidebar footer. Warning at 80% usage. Hard block at 100% with friendly message to user + notification to tenant admin.

### Admin Testing Assistants
- **Via sidebar** — "Test in Chat" button opens the sidebar with the draft assistant pre-selected. Test messages are saved as normal conversations.

### Event Triggers Discovery
- **Auto-discovered** — system scans all registered domain events and presents them in a dropdown. Fully dynamic, zero manual maintenance.

### API Key Management
- **Environment variables only** — no UI for key management. Platform admin sets keys via env vars or appsettings. No `PUT` endpoint for API keys; settings endpoint is for model/provider preferences only.

### Trigger Failure Handling
- **No retry, notify admin** — fail once, mark as failed, send notification to admin. Avoids wasting tokens on retries.

### Audit Trail for AI Actions
- **AI-initiated + conversation link** — audit entries for AI-executed tool calls are tagged as AI-initiated and include a reference to the conversation/task ID for full traceability.
