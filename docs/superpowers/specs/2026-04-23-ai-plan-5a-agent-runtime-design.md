# Plan 5a — Agent Runtime Abstraction + Provider Adapters

**Date:** 2026-04-23
**Branch:** `feature/ai-phase-5a`
**Target:** AI module — Plan 5 family, sub-plan 5a
**Supersedes:** n/a (new)
**Companion:** `2026-04-23-ai-module-vision-revised-design.md`

---

## Goal

Extract the multi-step agentic loop currently embedded in `ChatExecutionService` into a standalone, reusable `IAiAgentRuntime` abstraction with per-provider adapter seams. Emit normalised step events usable for both streaming render AND persistable audit. Add loop-break safety on repeated identical tool calls.

This unblocks downstream plans (5b persona-aware runs, 8a insights, 8b actions, 8c automations/agent tasks) without retrofitting the loop logic.

## Non-goals (explicitly deferred)

- **Wiring `AiAgentTask` execution.** The task entity exists; its executor is a later sub-plan. 5a designs the runtime contract so the executor can plug in without changes.
- **Moving to provider-native runtimes** (OpenAI Responses API / Agents SDK, Anthropic native tool-use primitives). The per-provider adapter seam is introduced; all three adapters initially share one loop in `AgentRuntimeBase`. Migration to genuine provider-native runtimes is a per-provider follow-up that does not break callers.
- **Adding new `IAiProvider` implementations.** 5a scope covers abstraction of what already ships (OpenAI, Anthropic, Ollama). Google Gemini lands in Plan 5g using the same seam. Other providers (xAI, Mistral, Cohere, DeepSeek, …) land when needed using the same pattern.
- **Changes to `IAiProvider`, provider implementations, tool catalog, or `IAiToolRegistry`.**
- **UI changes.** No frontend work in 5a.
- **Persona wiring.** 5b adds personas; 5a keeps assistant-as-identity as today.

---

## Provider + model coverage (current and planned)

The point of the `IAiAgentRuntime` seam is that no caller — chat, agent task, insight generator, action handler — needs to know which provider or model is serving a request. The abstraction accommodates all of the families below. 5a ships the first three. Everything else lands against the same contract without breaking callers.

### Text / chat / agent (runtime-owned)

| Family | Example models | Plan | Notes |
|---|---|---|---|
| OpenAI | GPT-4o, GPT-4o-mini, GPT-4.1 | **5a** | Current `OpenAiProvider` |
| Anthropic | Claude Sonnet 4.x, Claude Haiku, Claude Opus | **5a** | Current `AnthropicAiProvider` |
| Ollama (self-hosted) | Llama 3.x, Mistral, Qwen, DeepSeek-coder | **5a** | No native tool calling — runtime guard strips tools |
| Google Gemini | Gemini 2.5 Pro, Gemini 2.5 Flash, Gemini 2.5 Flash-Lite | 5g | Text + tool-call + embeddings |
| Reasoning-specialised | OpenAI o-series (o1 / o3 / o4-mini), Claude Opus with extended thinking, Gemini 2.5 Pro "thinking", DeepSeek R1 | follow-ons | No new runtime class needed — different `Model` value; runtime surfaces thinking tokens as an optional step field when the provider emits them |
| xAI / Grok, Mistral, Cohere Command, DeepSeek | Various | later | Each ships a new `IAiProvider` + thin `*AgentRuntime : AgentRuntimeBase` |

### Embeddings (not runtime-owned; used by RAG)

| Family | Example models | Plan |
|---|---|---|
| OpenAI | text-embedding-3-small / -large | shipped |
| Ollama | nomic-embed-text, bge | shipped |
| Google Gemini | text-embedding-004, gemini-embedding | 5g |
| Cohere, Voyage | embed-v3, voyage-3 | later |

### Multi-modal (not runtime-owned; Plan 10 abstractions)

| Capability | Candidate providers / models | Plan |
|---|---|---|
| Image generation | OpenAI DALL·E 3, OpenAI gpt-image-1, **Google Nano Banana (Gemini 2.5 Flash Image)**, Stable Diffusion 3, Flux.1 | 10 |
| Image editing / inpainting | **Nano Banana**, Flux.1 edit, DALL·E edit | 10 |
| Vision (image → text) | GPT-4o vision, Claude Sonnet vision, Gemini 2.5 vision | runtime handles natively; input shape extension |
| Speech-to-text (STT) | Whisper, Google STT, ElevenLabs Scribe | 10 |
| Text-to-speech (TTS) | OpenAI TTS, ElevenLabs, Google TTS | 10 |
| Video generation | Sora, Veo, Runway Gen-3 | 10 |

### What this means for 5a

The runtime contract (`AgentRunContext`, `AgentStepEvent`, `AgentRunResult`) is vendor-agnostic by design. Specifically:

- `ModelConfig.Provider` is an enum — adding Google adds one value. Adding xAI adds one value. No caller changes.
- `AgentStepEvent` has `InputTokens` / `OutputTokens` as provider-reported numbers; a future "thinking tokens" extension is a new nullable field, not a new event type. Reasoning models (o-series, Gemini thinking, Claude Opus thinking) surface their thinking budget through this field.
- Tool-calling is abstracted through `AiToolCall` / `AiToolDefinitionDto` which already normalise OpenAI function-calling, Anthropic tool_use blocks, and (when they land) Gemini function-calling. A provider that doesn't support tools (today: Ollama llama3.1) is handled by the runtime's "no tools when unsupported" guard.
- Vision input: the existing `AiChatMessage.Content` is a string today; when Plan 10 or a specific vision use case lands, we extend to `AiChatMessagePart[]` (text + image refs) without changing the runtime surface — only `AiChatMessage` grows.

In short: **5a is the seam that makes all of these adapters drop-in, not the place that ships them.**

---

## Current state (verified)

`ChatExecutionService` (~934 lines) mixes four concerns:

1. Chat turn preparation — conversation load, assistant resolve, ACL, quota pre-flight, message load, ordering
2. RAG retrieval — `RetrieveContextSafelyAsync`, webhook lifecycle, metrics
3. **Multi-step agent loop** — sync (`for step in stepBudget`) and streaming (`for step in stepBudget` yielding `ChatStreamEvent`)
4. **Tool dispatch** — `DispatchToolAsync` via `ISender.Send()` with permission checks and `Result<T>` unwrapping

Tool execution *is* wired end-to-end today; this is a re-architecture, not a gap fill.

Missing for 5a:
- `IAiAgentRuntime` interface — the loop is hand-rolled directly in the service
- Per-provider runtime adapters — `IAiProvider.ChatAsync`/`StreamChatAsync` is called directly with no per-provider seam for loop behavior
- Normalised step events usable as audit records (current `ChatStreamEvent` is streaming-frame-shaped, not step-shaped)
- Loop-break safety on repeated identical tool calls

---

## Architecture

```
Chat layer                              Agent Task layer (future, 8c)
  ChatExecutionService                    AgentTaskExecutor
     │                                       │
     │  ChatAgentRunSink                     │  TaskAgentRunSink
     │  (chat persistence, stream events,    │  (AiAgentTask.AddStep,
     │   quota, title, webhooks)             │   task status, task webhooks)
     │                                       │
     └───────────────────┬───────────────────┘
                         ▼
           IAiAgentRuntime.RunAsync(
             AgentRunContext ctx,
             IAgentRunSink sink,
             CancellationToken ct)
                         │
            ┌────────────┼────────────┐
            ▼            ▼            ▼
    OpenAiAgentRuntime  AnthropicAgentRuntime  OllamaAgentRuntime
            └────────────┼────────────┘
                         ▼
                AgentRuntimeBase (shared loop — today)
                         │
                         ▼
                   IAiProvider (unchanged)
```

**Invariants:**
- The runtime never touches the database.
- The runtime never calls webhooks.
- The runtime never reads `ICurrentUserService` — auth context is resolved by caller.
- All side effects go through `IAgentRunSink`.
- Sinks are owned by callers, not by the runtime module.

This keeps the runtime pure enough to test without EF, without MediatR (beyond the injected dispatcher), and without HTTP infrastructure.

---

## Components

### 1. `IAiAgentRuntime`

```csharp
internal interface IAiAgentRuntime
{
    Task<AgentRunResult> RunAsync(
        AgentRunContext context,
        IAgentRunSink sink,
        CancellationToken ct = default);
}
```

**Per-provider implementations** (all thin in 5a, all derive from `AgentRuntimeBase`):
- `OpenAiAgentRuntime`
- `AnthropicAgentRuntime`
- `OllamaAgentRuntime`

Selection via `IAiAgentRuntimeFactory.Create(AiProviderType)`.

The per-provider seam exists so a later sub-plan can override loop behavior for one provider (e.g., OpenAI Responses) without touching the other two and without breaking callers. In 5a, all three classes are 3–5 lines of `: AgentRuntimeBase(...)` boilerplate.

### 2. `AgentRunContext`

```csharp
internal sealed record AgentRunContext(
    IReadOnlyList<AiChatMessage> Messages,    // provider shape; caller-assembled
    string SystemPrompt,                      // caller-resolved (incl. RAG context)
    AgentModelConfig ModelConfig,
    ToolResolutionResult Tools,
    int MaxSteps,                             // caller-clamped 1..20
    LoopBreakPolicy LoopBreak);

internal sealed record AgentModelConfig(
    AiProviderType Provider,
    string Model,
    double Temperature,
    int MaxTokens);

internal sealed record LoopBreakPolicy(
    bool Enabled = true,
    int MaxIdenticalRepeats = 3);
```

Notes:
- `Messages` is already in provider shape (`AiChatMessage` from `Infrastructure.Providers`). Keeping this type at the boundary means the runtime doesn't own a DTO mapping layer.
- `SystemPrompt` is post-RAG-augmentation. Caller owns retrieval.
- `MaxSteps = 1` is the single-turn chat case per vision Decision 4.

### 3. `AgentRunResult`

```csharp
internal sealed record AgentRunResult(
    AgentRunStatus Status,
    string? FinalContent,
    IReadOnlyList<AgentStepEvent> Steps,
    int TotalInputTokens,
    int TotalOutputTokens,
    string? TerminationReason);

internal enum AgentRunStatus
{
    Completed,
    MaxStepsExceeded,
    LoopBreak,
    ProviderError,
    Cancelled
}
```

### 4. `AgentStepEvent` — normalised step schema

```csharp
internal sealed record AgentStepEvent(
    int StepIndex,
    AgentStepKind Kind,
    string? AssistantContent,
    IReadOnlyList<AgentToolInvocation> ToolInvocations,
    int InputTokens,
    int OutputTokens,
    string FinishReason,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);

internal enum AgentStepKind
{
    Final,        // assistant replied without tool calls; run terminates
    ToolCall,     // assistant requested tool(s); dispatched and fed back
    ThinkOnly     // reserved for future "chain-of-thought" steps (not emitted in 5a)
}

internal sealed record AgentToolInvocation(
    string CallId,
    string Name,
    string ArgumentsJson,
    string ResultJson,
    bool IsError,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
```

This schema is what eventually gets serialised into `AiAgentTask.Steps` (currently a freeform `string` defaulting to `"[]"`) and what Plan 8d's run-trace dashboard will read.

### 5. `IAgentRunSink` — caller-owned side effects

```csharp
internal interface IAgentRunSink
{
    Task OnStepStartedAsync(int stepIndex, CancellationToken ct);
    Task OnAssistantMessageAsync(AgentAssistantMessage message, CancellationToken ct);
    Task OnToolCallAsync(AgentToolCallEvent call, CancellationToken ct);
    Task OnToolResultAsync(AgentToolResultEvent result, CancellationToken ct);
    Task OnDeltaAsync(string contentDelta, CancellationToken ct);        // streaming only
    Task OnStepCompletedAsync(AgentStepEvent step, CancellationToken ct);
    Task OnRunCompletedAsync(AgentRunResult result, CancellationToken ct);
}
```

**`ChatAgentRunSink`** (in the chat layer) handles:
- Persisting assistant-tool-call rows + tool-result rows as they happen (matches current behavior)
- In streaming mode, pushing `ChatStreamEvent("delta" | "tool_call" | "tool_result" | "citations")` frames to a channel for `ExecuteStreamAsync` to yield
- Nothing else — quota, title, webhooks stay at the end in `FinalizeTurnAsync`

**Future `TaskAgentRunSink`** (not built in 5a — documented contract):
- `AiAgentTask.AddStep(serialisedStep, inputTokens, outputTokens)` on each `OnStepCompletedAsync`
- `MarkCompleted` / `MarkFailed` on `OnRunCompletedAsync`

### 6. Streaming bridge

The current streaming path yields `ChatStreamEvent` from inside the loop. After the runtime owns the loop, we need a way to surface events back to the `IAsyncEnumerable` consumer.

Pattern: **`Channel<ChatStreamEvent>`** driven by the streaming sink.

```csharp
// ChatExecutionService.ExecuteStreamAsync (sketch)
var channel = Channel.CreateUnbounded<ChatStreamEvent>();
var sink = new ChatAgentRunSink(streamWriter: channel.Writer, …);

var runTask = Task.Run(async () =>
{
    try { await runtime.RunAsync(ctx, sink, ct); }
    finally { channel.Writer.TryComplete(); }
}, ct);

await foreach (var frame in channel.Reader.ReadAllAsync(ct))
    yield return frame;

await runTask;  // surface any exception
// then FinalizeTurnAsync, yield "done"
```

This keeps the caller-facing API identical while inverting control inside.

### 7. `IAgentToolDispatcher`

Extracted verbatim from `ChatExecutionService.DispatchToolAsync`:

```csharp
internal interface IAgentToolDispatcher
{
    Task<AgentToolDispatchResult> DispatchAsync(
        AiToolCall call,
        ToolResolutionResult tools,
        CancellationToken ct);
}

internal sealed record AgentToolDispatchResult(string Json, bool IsError);
```

Implementation lives in `Infrastructure/Runtime/AgentToolDispatcher.cs` and takes `ISender`, `ICurrentUserService` (for permission check), `ILogger`. Responsibility unchanged: deserialise args → permission gate → `ISender.Send` → unwrap `Result<T>` → serialise JSON.

Extraction benefit: runtime testing can substitute a fake dispatcher; this path is unit-testable without MediatR.

### 8. `LoopBreakDetector`

```csharp
internal sealed class LoopBreakDetector
{
    private readonly int _maxIdenticalRepeats;
    private readonly List<string> _recent = new();

    public LoopBreakDetector(LoopBreakPolicy policy) { … }

    /// <summary>
    /// Call once per tool invocation, in order. Returns true when the last
    /// N invocations (by name + canonical-JSON args) are identical.
    /// </summary>
    public bool ShouldBreak(AiToolCall call);
}
```

**Canonical JSON**: `JsonNode.Parse(argsJson).ToJsonString()` with a deterministic property ordering (alphabetical) so `{"a":1,"b":2}` and `{"b":2,"a":1}` collapse.

**Default policy:** 3 identical in a row → break. Tunable at the assistant level later (stored config); 5a ships the primitive and default.

---

## Integration with `ChatExecutionService`

Before → after:

| Concern | Before | After |
|---|---|---|
| Turn preparation (load conv, ACL, quota, prior msgs) | `PrepareTurnAsync` | `PrepareTurnAsync` (unchanged) |
| RAG retrieval | `RetrieveContextSafelyAsync` | `RetrieveContextSafelyAsync` (unchanged) |
| **Multi-step loop (sync)** | Inline `for step in stepBudget` | `runtime.RunAsync(ctx, sink, ct)` |
| **Multi-step loop (streaming)** | Inline + yields | `runtime.RunAsync` + channel-bridged sink |
| **Tool dispatch** | Private `DispatchToolAsync` | `IAgentToolDispatcher` |
| Token accumulation | Inline counters | `AgentRunResult.TotalInputTokens/TotalOutputTokens` |
| Final message persistence + citations | `FinalizeTurnAsync` | `FinalizeTurnAsync` (unchanged) |
| Quota + usage increment | `FinalizeTurnAsync` | `FinalizeTurnAsync` (unchanged) |
| Title generation | `FinalizeTurnAsync` | `FinalizeTurnAsync` (unchanged) |
| Webhooks (`ai.chat.completed`, `ai.quota.exceeded`, RAG lifecycle) | In `ChatExecutionService` | In `ChatExecutionService` (unchanged) |

Target size: `ChatExecutionService` from ~934 lines to ≤450.

---

## File plan

### New — Application layer

```
Application/Services/Runtime/
├── IAiAgentRuntime.cs
├── IAiAgentRuntimeFactory.cs
├── AgentRunContext.cs               // + AgentModelConfig + LoopBreakPolicy
├── AgentRunResult.cs                // + AgentRunStatus
├── AgentStepEvent.cs                // + AgentStepKind + AgentToolInvocation
├── AgentAssistantMessage.cs         // + AgentToolCallEvent + AgentToolResultEvent
├── IAgentRunSink.cs
├── IAgentToolDispatcher.cs          // + AgentToolDispatchResult
└── LoopBreakDetector.cs             // LoopBreakPolicy lives in AgentRunContext
```

### New — Infrastructure layer

```
Infrastructure/Runtime/
├── AgentRuntimeBase.cs              // shared loop; sync + streaming
├── OpenAiAgentRuntime.cs            // 3–5 lines: : AgentRuntimeBase(provider, …)
├── AnthropicAgentRuntime.cs         // 3–5 lines
├── OllamaAgentRuntime.cs            // 3–5 lines (+ guard: strip tools if provider can't)
├── AiAgentRuntimeFactory.cs
└── AgentToolDispatcher.cs
```

### New — Chat layer

```
Application/Services/
└── ChatAgentRunSink.cs              // sync + streaming variants via constructor flag
```

### Modified

```
Application/Services/ChatExecutionService.cs   // shrink; delegate loop to runtime
AIModule.cs                                    // register runtime + dispatcher + factory
```

### New — Tests

```
tests/Starter.Api.Tests/Ai/Runtime/
├── AgentRuntimeBaseTests.cs
├── LoopBreakDetectorTests.cs
├── AgentToolDispatcherTests.cs
└── ChatAgentRunSinkTests.cs
```

**Critical regression suite:** existing `ChatExecutionService` tests must pass unchanged. Parity is the primary acceptance criterion.

---

## DI registration (AIModule.cs)

```csharp
services.AddScoped<IAgentToolDispatcher, AgentToolDispatcher>();
services.AddScoped<OpenAiAgentRuntime>();
services.AddScoped<AnthropicAgentRuntime>();
services.AddScoped<OllamaAgentRuntime>();
services.AddScoped<IAiAgentRuntimeFactory, AiAgentRuntimeFactory>();
```

The factory is scoped and resolves the concrete runtime from the provider type, matching the existing `IAiProviderFactory` pattern.

---

## Observability

- **Metric:** `ai_agent_steps` histogram labeled by provider + status, recording step count per run.
- **Metric:** `ai_agent_loop_breaks` counter labeled by provider + tool name.
- **Trace:** `Activity("ai.agent.run")` span wrapping `RunAsync`, with per-step child spans `ai.agent.step` (kind, tool names, token counts).
- **Log:** structured log on `LoopBreak` and `MaxStepsExceeded` with assistant id, tool names in the repeating window.

Uses existing `Activity`/`Meter` infrastructure — no new OTel plumbing.

---

## Testing

### Unit — `AgentRuntimeBaseTests`

| Scenario | Expected |
|---|---|
| Provider returns content, no tools | `Completed`, 1 step, `Kind=Final` |
| Provider returns 1 tool call, then content | `Completed`, 2 steps, tool invocation recorded |
| Provider returns same tool+args 3× | `LoopBreak`, 3 steps, `TerminationReason` names the tool |
| Provider returns tool call beyond `MaxSteps` | `MaxStepsExceeded`, all steps recorded |
| Provider throws mid-stream | `ProviderError`, partial steps recorded, error surfaced |
| Tool dispatcher returns `IsError=true` | Run continues (error JSON fed back to model) |
| Ctx with `MaxSteps=1` + no tool calls | `Completed` after 1 step (chat parity) |

### Unit — `LoopBreakDetectorTests`
- 2 identical → no break
- 3 identical → break
- 3 identical but 2nd has reordered JSON args → still break (canonical compare)
- Disabled policy → never breaks

### Unit — `AgentToolDispatcherTests`
- Valid args → success
- Missing permission → `AiErrors.ToolPermissionDenied`
- Unknown tool → `AiErrors.ToolNotFound`
- Malformed JSON → `AiErrors.ToolArgumentsInvalid`
- Handler returns `Result.Failure` → dispatch returns `IsError=true`
- Handler throws → `AiErrors.ToolExecutionFailed`

### Unit — `ChatAgentRunSinkTests`
- Non-streaming: all assistant rows + tool result rows persisted in order
- Streaming: `ChatStreamEvent` frames match current expected shapes for delta / tool_call / tool_result

### Integration — existing suite
All current `ChatExecutionService` tests pass unchanged. This is the acceptance gate.

---

## Migration / compat

- No DB migration. `AiAgentTask` entity is not touched in 5a.
- No public API changes. `IChatExecutionService` signatures identical.
- No SSE payload changes. Frame types and data identical.
- No `IAiProvider` changes.
- No changes to seed data, appsettings, or permissions.

---

## Success criteria

1. Existing chat test suite passes with zero modification. ✅ parity test
2. `ChatExecutionService` ≤ 450 lines. Only chat concerns remain.
3. Unit tests: `AgentRuntimeBase` covers all 7 scenarios above; `LoopBreakDetector` covers all 4; `AgentToolDispatcher` covers all 6.
4. Manual smoke test: a test assistant that provokes a tool loop (same tool, same args) is stopped after 3 invocations with `LoopBreak` status surfaced to the client as the "I couldn't fully complete…" message.
5. A non-chat consumer (unit test fake sink) drives the runtime end-to-end with no chat, no EF, no MediatR dependencies — proves isolation.
6. Build + CI green. No warnings added.

---

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| Streaming event ordering diverges from current output | Golden-file parity tests comparing emitted `ChatStreamEvent` sequences before/after |
| `Channel<T>` bridge introduces latency vs current direct yield | Unbounded channel + `Task.Run` in continuation; measured in smoke test |
| Token accounting drift (sync vs stream, fallback estimates) | Unit test asserting `AgentRunResult.TotalInputTokens` equals pre-refactor values on shared fixture |
| `canonical JSON` for loop-break misidentifies legitimate retries | Tunable `MaxIdenticalRepeats` (default 3, high enough for legit retry-with-backoff) |
| Ollama has no native tool-calling — silently misbehaves | `OllamaAgentRuntime` guard: if `ctx.Tools` is non-empty, emit a `ProviderError` step event rather than silently drop tools. Documented. |
| Loop-break triggers during long legitimate multi-tool sequences | Detector operates on *identical* invocations only, not "3 tool calls in a row" |

---

## Future hooks (documented, not implemented in 5a)

- **Google Gemini adapter** (Plan 5g): first beneficiary of the per-provider seam. Ships as a thin `GoogleGeminiAgentRuntime : AgentRuntimeBase` with a Gemini-specific `IAiProvider` implementation. Caller code (`ChatExecutionService`, future `AgentTaskExecutor`) unchanged. Establishes the pattern for any future provider addition.
- **`TaskAgentRunSink`** for `AiAgentTask` execution (Plan 8c).
- **Provider-native runtimes** — swap one per-provider class at a time (e.g., OpenAI Responses API, Anthropic native tool-use loop); caller unaffected.
- **Persona injection hook** (Plan 5b): adds `PersonaContext` to `AgentRunContext`; runtime forwards to sink but doesn't interpret.
- **`[DangerousAction]` pause** (Plan 5d): tool dispatcher checks for attribute; runtime emits `PendingApproval` step status and returns early.
- **Safety preset enforcement** (Plan 5d): an input/output moderation adapter wraps the runtime at the sink boundary — runtime does not own moderation.
- **Multi-modal runtimes** (Plan 10): image/video/audio generation abstractions (`IAiImageGenerator` etc.) sit beside `IAiAgentRuntime`, not inside it. Initial adapters will include OpenAI image models and Google Nano Banana (Gemini image model).

---

## Out of scope — explicit list

- OpenAI Responses API / Agents SDK adoption
- Anthropic native agent loop adoption
- Ollama native tool-calling (doesn't exist)
- `AiAgentTask` execution path
- `AiAgentTrigger` firing (cron / event)
- Persona wiring (5b)
- Safety / moderation pipeline (5d)
- Admin UI
- New permissions
