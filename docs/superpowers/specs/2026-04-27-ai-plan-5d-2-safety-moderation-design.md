# AI Module — Plan 5d-2: Safety + Content Moderation (Design)

Status: **Spec / pre-plan**
Sequence: follows 5d-1 (Agent Identity + Enforcement, shipped as PR #26), precedes 5e (Bundled Platform Agents).
Branch: `feature/ai-phase-5d-2`.

---

## 1. Purpose

Plan 5d in the [revised AI vision](./2026-04-23-ai-module-vision-revised-design.md) bundles agent identity, cost/rate enforcement, the `[DangerousAction]` human-approval pause, and the content-moderation pipeline. The combined surface is too large for one plan, so 5d is split:

| Sub-plan | Scope | Status |
|---|---|---|
| 5d-1 | Agent principal, role assignment, cost caps, rate limits, billing/feature-flag integration | Shipped (PR #26) |
| **5d-2 (this doc)** | `[DangerousAction]` human-approval pause; input/output moderation pipeline (`Standard` / `ChildSafe` / `ProfessionalModerated`); per-agent override of persona default | Pre-plan |

5d-1 established agents as first-class principals with identity, budget, and rate. 5d-2 layers safety controls on top of that principal: it must be impossible for a `ChildSafe` student-targeted agent to expose inappropriate content, and impossible for an agent to silently execute a destructive `[DangerousAction]` tool without human approval.

---

## 2. Locked Decisions

Settled during brainstorming. Override any earlier draft.

1. **Two enforcement seams** — a runtime decorator for input/output moderation, and a check inside `AgentToolDispatcher` for `[DangerousAction]`. Single decorator wrapping the runtime, single attribute check at tool dispatch — no pipeline behaviors, no in-base hooks.
2. **Provider-native moderation engine.** OpenAI Moderation API is the default classifier, called uniformly regardless of which provider the agent itself runs on. `IContentModerator` interface lets future LLM-based or hybrid implementations slot in without changing call sites.
3. **Preset semantics.** Each preset is a threshold profile against the categories the moderation API returns. `Standard` blocks at conservative thresholds. `ChildSafe` lowers thresholds and adds always-block categories. `ProfessionalModerated` runs Standard's thresholds plus a deterministic PII regex sweep on output. PII redaction is interface-stubbed; brand-voice / tone enforcement is **out of scope** here.
4. **Failure mode is preset-defined.** Standard fails open (log + allow) when the moderator is unavailable. ChildSafe and ProfessionalModerated fail closed (refuse with a clear error). The "ChildSafe means actually safe" promise is non-negotiable.
5. **Streaming buffering is preset-conditional.** Standard streams output deltas live with a final-pass safety net. ChildSafe and ProfessionalModerated suppress streaming internally — deltas are buffered, scanned once, and emitted as a single chunk only after the moderator passes. Trades streaming UX for safety, which is the point of those presets.
6. **`[DangerousAction]` flow is async via approval inbox.** Every dangerous tool call terminates the run with `AgentRunStatus.AwaitingApproval` and writes an `AiPendingApproval` row. Approver acts later via dedicated commands; the approval handler executes the tool directly and appends the result to the conversation. Same path for chat and operational agents — no held HTTP connections, no agent-loop replay.
7. **Audit trail is a dedicated table.** `AiModerationEvent` records non-Allowed outcomes (Blocked / Redacted) by default. A superadmin flag opts a tenant into full per-turn logging. Cost-tracking (`AiUsageLog`) and audit-log (`AuditLog`) are not extended.
8. **Per-agent override is a nullable column.** `AiAssistant.SafetyPresetOverride` (nullable). Resolution: agent override → persona safety preset → `Standard`.
9. **Threshold profiles are tenant-overridable DB rows.** `AiSafetyPresetProfile` is a single table with nullable `TenantId`. Platform-default rows (TenantId=NULL) seed on first run. Tenant overrides have their own row. Resolver precedence: tenant row → null-tenant row → hard-coded fallback.
10. **`AwaitingApproval` is a success status, not an error.** `ChatExecutionService` maps it to `Result.Success(AiChatReplyDto { Status: "awaiting_approval", ApprovalId, ... })` returning HTTP 200. Streaming emits an `awaiting_approval` SSE frame.
11. **Refusal templates are RESX, mirroring `SafetyPresets.resx`.** New `ModerationRefusalTemplates.resx` + `.ar.resx`, `(Preset, Audience, Culture)` lookup. DB-backed templates deferred until tenants ask for it.
12. **Acid tests use a `FakeContentModerator` for behavior + a single live wire-compat test gated on `MODERATION_LIVE_TESTS=1`.** Mirrors the RAG eval harness `AI_EVAL_ENABLED=1` pattern.

---

## 3. Architecture Overview

```
                 ┌──────────────────────────────────────────┐
chat caller ────►│ AiChatController / event / cron trigger  │
                 └─────────────────────┬────────────────────┘
                                       │ AgentRunContext
                                       ▼
                 ┌──────────────────────────────────────────┐
                 │ ContentModerationEnforcingAgentRuntime    │  (5d-2, outermost)
                 │  pre-flight:  scan input, refuse-or-pass  │
                 │  post-flight: scan output (or sentence-   │
                 │               buffered for ChildSafe/Pro) │
                 │  emits:       AiModerationEvent rows      │
                 └─────────────────────┬────────────────────┘
                                       │ unchanged interface
                                       ▼
                 ┌──────────────────────────────────────────┐
                 │ CostCapEnforcingAgentRuntime              │  (5d-1)
                 └─────────────────────┬────────────────────┘
                                       │
                                       ▼
                 ┌──────────────────────────────────────────┐
                 │ Provider runtime (OpenAI/Anthropic/Ollama)│  (5a)
                 │  → tool call                              │
                 └─────────────────────┬────────────────────┘
                                       ▼
                 ┌──────────────────────────────────────────┐
                 │ AgentToolDispatcher                       │
                 │  permission check (IExecutionContext)     │  (5d-1)
                 │  [DangerousAction] check (5d-2)           │  ← writes AiPendingApproval,
                 │  ISender.Send(command, ct)                │    short-circuits to caller
                 └──────────────────────────────────────────┘
```

Three new application services, all behind interfaces so 7b's admin UI and future LLM-based moderators can plug in without rework.

| Service | Interface | Purpose |
|---|---|---|
| Content moderator | `IContentModerator` | `Task<ModerationVerdict> ScanAsync(text, stage, profile, language, ct)`. Default impl `OpenAiContentModerator`; stub `NoOpContentModerator` registered when no API key resolved. |
| PII redactor | `IPiiRedactor` | `Task<RedactionResult> RedactAsync(text, profile, ct)`. Default impl `RegexPiiRedactor` (email, E.164 phone, SSN-style, Luhn-validated card, IBAN). No-op for Standard/ChildSafe; engaged for ProfessionalModerated. |
| Safety profile resolver | `ISafetyProfileResolver` | `(tenantId, assistant, persona) → ResolvedSafetyProfile { Preset, CategoryThresholds, BlockedCategories, FailureMode, RedactPii }`. Cache-backed via `ICacheService` (60s TTL, mirrors `ICostCapResolver`). Invalidated on `AssistantUpdatedEvent`, `PersonaUpdatedEvent` (new), `SafetyPresetProfileUpdatedEvent` (new). |

Plus one orchestration service:

| Service | Interface | Purpose |
|---|---|---|
| Pending approval service | `IPendingApprovalService` | `CreateAsync`, `ApproveAsync`, `DenyAsync`, `ExpireOldAsync`. Encapsulates `AiPendingApproval` row lifecycle. Approve path re-issues the original MediatR send with an `IExecutionContext` flag that bypasses the dispatcher's dangerous-action check (one-shot grant). |

---

## 4. Domain Model

### 4.1 New entities (AI module DbContext)

#### `AiSafetyPresetProfile`

Tenant-aware threshold profile per `(Preset, Provider)`. Platform defaults are rows with `TenantId=NULL`; tenant overrides have their own row.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid | code-assigned PK (`ValueGeneratedNever()`) |
| `tenant_id` | uuid? | NULL = platform default |
| `preset` | smallint | `SafetyPreset` enum (Standard/ChildSafe/ProfessionalModerated) |
| `provider` | smallint | `ModerationProvider` enum (OpenAi for v1; future-extensible) |
| `category_thresholds` | jsonb | `{"sexual":0.85,"hate":0.5,...}` per OpenAI category |
| `blocked_categories` | jsonb | `["sexual-minors","violence-graphic"]` always-block list |
| `failure_mode` | smallint | `ModerationFailureMode` (FailOpen / FailClosed) |
| `redact_pii` | bool | true for ProfessionalModerated default |
| `version` | int | optimistic-concurrency / superadmin audit |
| `is_active` | bool | soft-delete |
| `created_at` / `modified_at` | timestamptz | `BaseEntity` defaults |

Unique index `(tenant_id, preset, provider, is_active)` partial-where `is_active=true`. Static factory `Create(tenantId?, preset, provider, thresholds, blockedCategories, failureMode, redactPii)` + `Update(...)`.

Domain event: `SafetyPresetProfileUpdatedEvent { TenantId? }` raised by `Create` and `Update`.

#### `AiModerationEvent`

Append-only audit row for non-Allowed outcomes.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid | PK |
| `tenant_id` | uuid? | tenant scope |
| `assistant_id` | uuid? | FK to `ai_assistants.id` |
| `agent_principal_id` | uuid? | dual-attribution from 5d-1 |
| `conversation_id` | uuid? | one of conversation/task must be set |
| `agent_task_id` | uuid? | for operational agents |
| `message_id` | uuid? | FK to `ai_messages.id` for output-stage |
| `stage` | smallint | `ModerationStage` (Input / Output) |
| `preset` | smallint | preset that was active |
| `outcome` | smallint | `ModerationOutcome` (Blocked / Redacted; Allowed only when `Ai:Moderation:LogAllOutcomes` is on) |
| `categories` | jsonb | `{"sexual":0.93,"hate":0.21}` raw scores from the moderator |
| `provider` | smallint | which moderator made the call |
| `blocked_reason` | text? | free-form for human review |
| `redaction_failed` | bool | true if PII redactor threw + we returned content unredacted |
| `latency_ms` | int | observability — also emitted as OTel metric |
| `created_at` | timestamptz | indexed desc |

Indexes: `ix_ai_moderation_events_tenant_id_created_at` (DESC), `ix_ai_moderation_events_tenant_id_outcome`, `ix_ai_moderation_events_message_id` (sparse, for back-correlation from chat history).

#### `AiPendingApproval`

| Column | Type | Notes |
|---|---|---|
| `id` | uuid | PK |
| `tenant_id` | uuid? | tenant scope |
| `assistant_id` | uuid | FK |
| `agent_principal_id` | uuid | FK |
| `conversation_id` | uuid? | nullable; constructor enforces conversation OR task is set |
| `agent_task_id` | uuid? | nullable |
| `requesting_user_id` | uuid? | the human chat caller; null for operational |
| `tool_name` | text | as it appeared in the agent tool catalog |
| `command_type_name` | text | assembly-qualified MediatR command type for re-dispatch |
| `arguments_json` | jsonb | exact args the dispatcher will replay on Approve |
| `reason_hint` | text? | optional `[DangerousAction(Reason="...")]` annotation |
| `status` | smallint | `PendingApprovalStatus` (Pending / Approved / Denied / Expired) |
| `decision_user_id` | uuid? | who approved/denied |
| `decision_reason` | text? | free-form |
| `decided_at` | timestamptz? | |
| `expires_at` | timestamptz | now + `Ai:Moderation:ApprovalExpirationHours` (default 24) |
| `created_at` / `modified_at` | timestamptz | |

Unique constraint: only one `Pending` row per `(assistant_id, conversation_id?, agent_task_id?, tool_name, arguments_json hash)` — prevents the same agent retrying and creating dupes if the run is re-invoked. Index `(tenant_id, status, expires_at)` powers the inbox query and the expiration job.

Static factory `Create(...)` validates that exactly one of `ConversationId` / `AgentTaskId` is set. Methods `Approve(decisionUserId, reason?)`, `Deny(decisionUserId, reason)`, `Expire()`. All transitions write `ModifiedAt`.

### 4.2 New column on `AiAssistant`

Add `SafetyPresetOverride` (nullable `SafetyPreset`) and method `SetSafetyPreset(SafetyPreset?)`. Setter raises `AssistantUpdatedEvent` (already raised by `SetBudget` from 5d-1) so the resolver cache invalidates.

### 4.3 New domain event on `AiPersona`

`PersonaUpdatedEvent { TenantId, PersonaSlug, SafetyPreset }` raised by `AiPersona.Update`. Currently the entity raises nothing; this is the first event off it. Subscribed by `InvalidateSafetyProfileCacheOnPersonaUpdate`.

### 4.4 Domain attribute + grant mechanism

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DangerousActionAttribute : Attribute
{
    public string? Reason { get; }
    public DangerousActionAttribute(string? reason = null) => Reason = reason;
}
```

Lives in `Starter.Application.Common.Attributes` (cross-module — any module's MediatR command can be marked). Reflection lookup happens in `AgentToolDispatcher` at dispatch time.

The approval grant flag is a new property on `IExecutionContext`:

```csharp
public interface IExecutionContext
{
    // ... existing members from 5d-1 ...

    /// <summary>
    /// True for the duration of an approved-action re-dispatch (one-shot).
    /// AgentToolDispatcher skips the [DangerousAction] check when this returns true.
    /// Default impls return false.
    /// </summary>
    bool DangerousActionApprovalGrant { get; }
}
```

`HttpExecutionContext` and `AgentExecutionScope` return `false` by default. The approval handler installs a one-shot `ApprovalGrantExecutionContext` wrapper via `AmbientExecutionContext.Use(...)` for the duration of `ISender.Send(reconstitutedCommand, ct)`, then disposes the scope. This reuses the AsyncLocal install/restore pattern from 5d-1 — no new infrastructure.

### 4.5 New enums

- `ModerationStage { Input = 0, Output = 1 }`
- `ModerationOutcome { Allowed = 0, Blocked = 1, Redacted = 2 }`
- `ModerationProvider { OpenAi = 0 }` (extensible)
- `ModerationFailureMode { FailOpen = 0, FailClosed = 1 }`
- `PendingApprovalStatus { Pending = 0, Approved = 1, Denied = 2, Expired = 3 }`

### 4.6 Additions to existing enum

`AgentRunStatus` (in `AgentRunResult.cs`) gains:
- `InputBlocked = 7`
- `OutputBlocked = 8`
- `AwaitingApproval = 9`
- `ModerationProviderUnavailable = 10`

`InputBlocked` and `ModerationProviderUnavailable` short-circuit *before* the inner runtime is called (no cost claim). `OutputBlocked` happens after the inner runtime returns; cost is recorded as actual.

### 4.7 Approval lifecycle domain events

Four new events raised by `AiPendingApproval` state transitions. All implement `INotification` (MediatR domain event, in-process). The Communication module subscribes via `INotificationHandler<T>` to convert each into channel-aware notifications (see §11).

| Event | Raised when | Payload |
|---|---|---|
| `AgentApprovalPendingEvent` | `IPendingApprovalService.CreateAsync` after `SaveChangesAsync` | `TenantId, ApprovalId, AssistantId, AssistantName, ToolName, Reason?, RequestingUserId?, ConversationId?, AgentTaskId?, ExpiresAt` |
| `AgentApprovalApprovedEvent` | `AiPendingApproval.Approve` | `TenantId, ApprovalId, AssistantId, AssistantName, ToolName, RequestingUserId?, DecisionUserId, DecisionReason?, ConversationId?` |
| `AgentApprovalDeniedEvent` | `AiPendingApproval.Deny` | same shape as Approved + `DecisionReason` (mandatory) |
| `AgentApprovalExpiredEvent` | `AiPendingApproval.Expire` (called by the expiration job) | `TenantId, ApprovalId, AssistantId, AssistantName, ToolName, RequestingUserId?, ExpiredAt` |

These events are pure in-process notifications — they do not cross the MassTransit bus. Per CLAUDE.md's outbox rule, MediatR handlers must not inject `IPublishEndpoint`. Cross-replica delivery for notifications happens inside the Communication module via its existing `MessageDispatcher` + outbox path, which is the documented seam.

A single `IPendingApprovalService` orchestrates the entity transition + event raising + `ISender.Send` re-dispatch (on Approve), so individual command handlers stay thin. All status transitions on `AiPendingApproval` use **optimistic concurrency** — `Approve` / `Deny` / `Expire` use a `WHERE status = Pending` clause in the `SaveChanges` projection so a race between an admin clicking "Approve" and the expiration job catching the same row resolves cleanly: the second writer gets `Result.Failure(PendingApproval.NotPending)` and a 409 Conflict.

---

## 5. Configuration

Per the user mandate "everything must be superadmin-configurable," all preset behavior lives in DB rows. Static `appsettings` is reserved for credentials and environment-level toggles.

### 5.1 `appsettings.json` keys

| Key | Default | Purpose |
|---|---|---|
| `Ai:Moderation:Provider` | `OpenAi` | which engine implementation to register |
| `Ai:Moderation:OpenAi:ApiKey` | unset (falls back to `Ai:Providers:OpenAI:ApiKey`) | dedicated moderation key for the rare tenant who wants it separate |
| `Ai:Moderation:ApprovalExpirationHours` | `24` | how long a pending approval lives before the expiration job auto-denies |
| `Ai:Moderation:LogAllOutcomes` | `false` | if true, write `AiModerationEvent` rows for `Allowed` outcomes too (regulated industries) |
| `Ai:Moderation:AllowUnmoderatedFallback` | `false` | if true and no key resolves, register `NoOpContentModerator` and Standard preset fails open. ChildSafe/Pro still fail closed (preset-defined). |

### 5.2 DB-backed config

`AiSafetyPresetProfile` rows. Seeded on first run (`SafetyPresetProfileSeed`) with platform defaults:

| Preset | `BlockedCategories` | `CategoryThresholds` | `FailureMode` | `RedactPii` |
|---|---|---|---|---|
| Standard | `[]` | `{"sexual":0.85,"hate":0.85,"violence":0.85,"self-harm":0.85,"harassment":0.85}` | FailOpen | false |
| ChildSafe | `["sexual-minors","violence-graphic"]` | `{"sexual":0.5,"hate":0.5,"violence":0.5,"self-harm":0.3,"harassment":0.5}` | FailClosed | false |
| ProfessionalModerated | `[]` | same as Standard | FailClosed | true |

Seed is idempotent: skips if any rows exist for `(tenant_id IS NULL, preset, provider)`. Re-seed for new presets is an explicit migration; we never overwrite tenant-tuned settings.

### 5.3 Resolution at runtime

Order, applied by `ISafetyProfileResolver`:

1. `AiAssistant.SafetyPresetOverride` if non-null → use that preset.
2. Else `AiPersona.SafetyPreset` from the active persona context.
3. Else `Standard`.

Then look up the threshold profile:

1. `AiSafetyPresetProfile` row matching `(tenantId, preset, provider, is_active=true)`.
2. Else `AiSafetyPresetProfile` row matching `(tenant_id IS NULL, preset, provider, is_active=true)`.
3. Else hard-coded compile-time fallback (panic-mode for first-run-before-seed scenarios).

Cached in `ICacheService` keyed `safety:profile:{tenantId}:{preset}:{provider}` with 60s TTL. Invalidated on `AssistantUpdatedEvent`, `PersonaUpdatedEvent`, `SafetyPresetProfileUpdatedEvent` for the matching tenant. Platform-default row changes rely on the 60s TTL for natural propagation across tenants — same trade-off as `CostCapResolver`.

---

## 6. Data Flow

### 6.1 Standard turn — allowed (happy path)

1. `ChatExecutionService` builds `AgentRunContext` with `AssistantId`, `TenantId`, `Persona` (containing safety preset), `Streaming=true`.
2. `AiAgentRuntimeFactory.Create(provider)` returns `ContentModerationEnforcingAgentRuntime(CostCapEnforcingAgentRuntime(inner, ...), moderator, profileResolver, refusalProvider, ...)`.
3. The moderation decorator's `RunAsync`:
   - Resolves profile → `Standard / FailOpen`.
   - Scans `ctx.Messages.Last()` (input). `Verdict.Allowed`. No event row.
   - Wraps the caller's `IAgentRunSink` in `PassthroughSink` (Standard preset → no buffering).
   - Delegates to inner runtime. Deltas flow live through the original sink to the chat client.
   - On `OnRunCompletedAsync`, the wrapper assembles the final content, scans it once. `Verdict.Allowed`. No event row, no replacement.
   - Returns the inner `AgentRunResult` unchanged.
4. `ChatExecutionService.FinalizeTurnAsync` writes the `AiUsageLog` row (cost) — same as today. Done.

### 6.2 ChildSafe — output blocked (acid M1)

1. Profile resolves → `ChildSafe / FailClosed`. `Streaming=true`.
2. Input scan → `Allowed`.
3. Decorator wraps the sink in `BufferingSink`: `OnDeltaAsync` events are accumulated into a `StringBuilder`, **never forwarded** to the original sink. `OnAssistantMessageAsync` and `OnStepCompletedAsync` are also held.
4. Cost-cap layer claims spend → inner runtime executes → buffering sink collects all output text.
5. Output scan against the assembled buffer → `Verdict.Blocked(category=sexual-minors, score=0.93)`.
6. Decorator:
   - Writes one `moderation_blocked` `ChatStreamEvent` to the sink (now passthrough — first frame the client sees).
   - Resolves the refusal template via `IModerationRefusalProvider.Get(ChildSafe, persona.Audience, culture)` and forwards it as a single `OnAssistantMessageAsync`.
   - Returns `AgentRunResult { Status=OutputBlocked, FinalContent=<refusal>, TotalInputTokens=<actual>, TotalOutputTokens=<actual>, TerminationReason="moderation: sexual-minors" }`.
7. CostCap reconciles actual spend (we *did* call the LLM).
8. `ChatExecutionService` persists the refusal as the assistant message and writes one `AiModerationEvent { Stage=Output, Outcome=Blocked, Categories={sexual-minors:0.93}, MessageId=<assistantMsgId> }`.

### 6.3 Input blocked (acid M2)

1. Input scan → `Verdict.Blocked`.
2. Decorator returns `AgentRunResult { Status=InputBlocked, FinalContent=<refusal-template>, TotalInputTokens=0, TotalOutputTokens=0, TerminationReason="moderation: <categories>" }` *before* delegating to the inner runtime. **No cost claim, no LLM call, no usage log.**
3. `ChatExecutionService` persists the refusal as the assistant message and writes `AiModerationEvent { Stage=Input, Outcome=Blocked }`. The user message is still saved.

### 6.4 ProfessionalModerated — PII redacted (acid M3)

1. Profile resolves → `ProfessionalModerated / RedactPii=true`.
2. Input scan → `Allowed`. (PII in user input is allowed; only output is redacted.)
3. Buffering sink collects output. Output scan from `IContentModerator` → `Allowed`.
4. `IPiiRedactor.RedactAsync` runs on the buffered text. Email + phone matched, replaced with `[REDACTED]`. Returns `RedactionResult { Outcome=Redacted, RedactedText, Hits={pii-email:1, pii-phone:1} }`.
5. Decorator forwards the redacted text via `OnAssistantMessageAsync` and `OnDeltaAsync` (one chunk).
6. Returns `AgentRunResult { Status=Completed, FinalContent=<redacted-text>, ... }`.
7. `AiModerationEvent { Stage=Output, Outcome=Redacted, Categories={pii-email:1,pii-phone:1} }` written.

### 6.5 `[DangerousAction]` flow (acid M4)

1. Agent emits a tool call to `DeleteAllUsersCommand` (annotated `[DangerousAction(Reason="Mass user deletion")]`).
2. `AgentToolDispatcher.DispatchAsync`:
   - Permission check passes (5d-1 hybrid intersection).
   - `def.CommandType.GetCustomAttribute<DangerousActionAttribute>()` returns non-null.
   - `IExecutionContext` does **not** carry an approval grant flag → action is intercepted.
   - `IPendingApprovalService.CreateAsync` writes an `AiPendingApproval` row (status=Pending, expires=now+24h, args = `call.ArgumentsJson`).
   - Returns a synthetic tool-result event: `{ "ok": false, "error": { "code": "AiAgent.AwaitingApproval", "message": "...", "approvalId": "<guid>" } }`.
3. The provider runtime sees a tool failure, terminates the loop. `AgentRuntimeBase` translates this to `AgentRunResult { Status=AwaitingApproval, ... }`.
4. `ChatExecutionService` maps `AwaitingApproval` to **`Result.Success`** (not failure):
   - Non-streaming: returns `AiChatReplyDto { Status="awaiting_approval", ApprovalId, ExpiresAt, ToolName, Reason, ... }` HTTP 200.
   - Streaming: emits an `awaiting_approval` SSE frame followed by `done`.
5. The chat client renders an inline approval card.

**Approval path:**
1. `ApprovePendingActionCommand { ApprovalId, OptionalNote }` validates the approver has `Ai.Agents.ApproveAction`.
2. Handler resolves `command_type_name` via `Type.GetType(name, throwOnError: false)`. If unresolvable (renamed / removed since approval was created), the approval is auto-denied with `decision_reason="tool unavailable"`, a `tool` message recording the failure is appended to the conversation, and `Result.Failure(PendingApproval.ToolUnavailable)` is returned to the approver.
3. Otherwise reconstitutes the original `IRequest` from the resolved type + `arguments_json`.
4. Installs an `ApprovalGrantExecutionContext` via `AmbientExecutionContext.Use(...)` so `IExecutionContext.DangerousActionApprovalGrant` returns `true` for the duration of the next send.
5. Calls `ISender.Send(reconstitutedCommand, ct)`. Disposes the scope immediately after.
6. Tool result is appended to the conversation as a `tool` role message; status flips to `Approved`. The user can then send a new turn ("continue" / "what next?") and the agent reads the tool result from history.

**Deny path:** appends a `tool` message containing the denial reason; status flips to `Denied`. Agent treats subsequent turns as having a failed tool call.

**Expiration:** `AiPendingApprovalExpirationJob` (hosted service, 5-minute tick) flips Pending rows past `expires_at` to `Expired` and writes one `AuditLog` row per flip.

### 6.6 Streaming buffering (acid M5)

`BufferingSink` and `PassthroughSink` are two implementations of `IAgentRunSink` that wrap the caller-provided sink. Decorator chooses based on resolved preset:

| Preset | Sink wrapper | Behavior |
|---|---|---|
| Standard | `PassthroughSink` | All events forwarded immediately. Final-pass scan happens on `OnRunCompletedAsync`. |
| ChildSafe | `BufferingSink` | `OnDeltaAsync`, `OnAssistantMessageAsync` held until decorator releases after the post-run scan completes. |
| ProfessionalModerated | `BufferingSink` | Same; releases after PII redaction is also complete. |

`OnStepStartedAsync`, `OnToolCallAsync`, `OnToolResultAsync` are forwarded immediately for both wrappers — those are observability events, not user-facing content.

### 6.7 Moderation provider unavailable (acid M6)

Resolved profile carries `FailureMode`. On `IContentModerator.ScanAsync` exception or `NoOpContentModerator`-registered case:

- `FailOpen` (Standard default): log warning, increment `ai_moderation_provider_unavailable_total`, treat as Allowed.
- `FailClosed` (ChildSafe/Pro default): return `AgentRunResult { Status=ModerationProviderUnavailable, FinalContent=<provider-unavailable-template>, ... }`. Chat layer maps to `Result.Failure(Error.Failure("AiModeration.ProviderUnavailable", ...))` → HTTP 500-class. (Not 503 — semantically this is a tenant-config issue, not infra outage.)

---

## 7. API Surface

### 7.1 Controllers

#### `AiSafetyController` (new) — `/api/v{version}/ai/safety`

| Verb | Path | Permission | Purpose |
|---|---|---|---|
| GET | `/profiles` | `Ai.SafetyProfiles.Manage` | List effective profiles for current tenant (tenant overrides + platform defaults). Superadmin sees all. |
| POST | `/profiles` | `Ai.SafetyProfiles.Manage` | Upsert a profile. SuperAdmin can write `tenant_id=null`; tenant admin can only write own tenant. |
| DELETE | `/profiles/{preset}/{provider}` | `Ai.SafetyProfiles.Manage` | Deactivate the row scoped to the caller's tenant (or platform if SuperAdmin). |
| GET | `/moderation-events` | `Ai.Moderation.View` | Paginated event list. Filters: `outcome`, `stage`, `from`, `to`, `assistantId?`. Tenant-scoped (admin); cross-tenant for SuperAdmin. |

#### `AiAgentApprovalsController` (new) — `/api/v{version}/ai/agents/approvals`

| Verb | Path | Permission | Purpose |
|---|---|---|---|
| GET | `` | `Ai.Agents.ViewApprovals` | Inbox: paginated `AiPendingApproval`s. Users with only `ViewApprovals` see rows where `RequestingUserId == caller`. Users with `Ai.Agents.ApproveAction` see all rows in their tenant (or all tenants for SuperAdmin). Filters: `status`, `assistantId?`. |
| GET | `/{id}` | `Ai.Agents.ViewApprovals` | Detail: full args, conversation/task linkage, expiration. Same row-level scoping as the list endpoint. |
| POST | `/{id}/approve` | `Ai.Agents.ApproveAction` | Approve + execute the tool. Returns the conversation message produced. |
| POST | `/{id}/deny` | `Ai.Agents.ApproveAction` | Deny with mandatory reason. |

#### `AiAssistantsController` (existing) — new endpoint

| Verb | Path | Permission | Purpose |
|---|---|---|---|
| PUT | `/{id}/safety-preset` | `AiPermissions.ManageAssistants` | Body `{ preset: "ChildSafe" | null }`. Null clears the override (revert to persona default). |

### 7.2 MediatR commands & queries

Commands (`Application/Commands/Safety/`):
- `UpsertSafetyPresetProfileCommand`
- `DeactivateSafetyPresetProfileCommand`
- `SetAssistantSafetyPresetOverrideCommand`
- `ApprovePendingActionCommand`
- `DenyPendingActionCommand`

Queries (`Application/Queries/Safety/`):
- `GetSafetyPresetProfilesQuery` (paginated; precedence-resolved view)
- `GetModerationEventsQuery` (paginated, filtered)
- `GetPendingApprovalsQuery` (paginated, filtered)
- `GetPendingApprovalByIdQuery`

All handlers return `Result<T>` and follow the `private set` + static `Create` factory entity convention.

### 7.3 Permissions

Four new entries in `AiPermissions`:
- `Ai.SafetyProfiles.Manage`
- `Ai.Agents.ApproveAction`
- `Ai.Agents.ViewApprovals`
- `Ai.Moderation.View`

Default role bindings (`AIModule.GetDefaultRolePermissions`):
- **SuperAdmin** — all four.
- **Admin** — all four (tenant-scoped at handler level for `SafetyProfiles.Manage`).
- **User** — `Ai.Agents.ViewApprovals` only. End users see their own pending approvals (read-only) so they know an action is gated, but cannot approve/deny. (`Ai.Agents.ApproveOwnAction` for end-user self-approval is **out of scope**, deferred to 7b feedback.)

### 7.4 Streaming SSE frame additions

`ChatStreamEvent` payload types added:
- `moderation_blocked` — `{ stage: "input" | "output", reason: string }`. Followed by a final assistant message containing the refusal template, then `done`.
- `awaiting_approval` — `{ approvalId, expiresAt, toolName, reason, argumentsJson }`. Followed by `done`. No further deltas.

---

## 8. Background services

### 8.1 `AiPendingApprovalExpirationJob`

`IHostedService` registered via `services.AddHostedService<...>()`, mirroring `AiCostReconciliationJob` from 5d-1. Five-minute tick. Per tick:

1. **Atomic claim + expire.** Single SQL statement using `UPDATE ... RETURNING`:
   ```sql
   UPDATE ai_pending_approvals
   SET status = 3 /* Expired */, decided_at = now(), modified_at = now()
   WHERE id IN (
       SELECT id FROM ai_pending_approvals
       WHERE status = 0 /* Pending */ AND expires_at < now()
       ORDER BY expires_at ASC
       LIMIT 100
       FOR UPDATE SKIP LOCKED
   )
   RETURNING id, tenant_id, assistant_id, agent_principal_id, conversation_id, agent_task_id, requesting_user_id, tool_name, expires_at;
   ```
   Skip-locked + atomic update is the gate. No additional locking needed.

2. **Per expired row:** raise `AgentApprovalExpiredEvent` (in-process MediatR notification → Communication module sends notification per §11; webhook event `ai.agent.approval.expired` published in the same handler). If `ConversationId` is set, append a `tool` message containing `"approval expired"` so the agent loop on the next user turn sees the closure.

3. **AuditLog row** per expired approval, written via the existing `AuditLogAgentAttributionInterceptor` from 5d-1 (dual-attribution: agent principal + null caller, since this is a system action).

### 8.2 Scaling, idempotency, and multi-replica safety

The API runs as multiple replicas behind a load balancer. Every replica starts the hosted service. Three properties make this safe by construction:

| Property | Mechanism |
|---|---|
| **Multi-replica safe** | The `UPDATE ... WHERE status = Pending FOR UPDATE SKIP LOCKED ... RETURNING` pattern guarantees each pending row is processed by exactly one replica per tick. Replicas that lose the race for a row simply move on to the next. No leader election, no Redis lock, no Postgres advisory lock. |
| **Crash-safe** | Mid-tick crash leaves any partially-updated rows in `Pending` (transaction rolled back). Next tick on any replica re-claims. No "stuck in Expiring" intermediate state. |
| **Bounded blast radius** | `LIMIT 100` per tick caps transaction duration. With 5-min ticks and N replicas this is `N × 100 × 12 = 1200N` expirations/hour — orders of magnitude above any plausible production volume. |

**Why no distributed lock.** Distributed locks (Redis SETNX with TTL, Postgres advisory lock, leader election via heartbeat) are required when the work itself isn't naturally idempotent (e.g., reading a counter, computing, writing it back). Here the work IS the atomic SQL statement; concurrent execution is provably correct. Adding a lock would introduce a *new* failure mode (lock-holder dies → 5-min freeze on expirations) without removing any existing one.

**Why bounded batch size.** Holding a row-level lock on 1000+ rows for several seconds while we publish events and write audit rows would block any concurrent admin trying to approve/deny one of those same rows. 100 rows/tick keeps each transaction sub-second. Domain events fire *outside* the transaction (after `SaveChangesAsync`), so notification dispatch latency doesn't affect lock duration.

**Future scaling lever.** If approval volume ever reaches the point where 100/tick can't keep up:
- Drop tick interval to 1 minute (12000N/hour without code changes).
- Increase `LIMIT` to 500 (reasonable as long as event-publishing stays fast).
- Move event publication to the integration-event outbox (already wired for the Communication module's downstream consumers — current 5d-2 design uses in-process `INotification` for simplicity).

### 8.3 Other 5d-2-related background work

No other hosted services added in 5d-2. The cost-reconciliation, tool-registry-sync, and content-ingestion background paths from earlier plans are untouched.

---

## 9. Observability

OpenTelemetry metrics added to `AiAgentMetrics`:

| Metric | Type | Tags |
|---|---|---|
| `ai_moderation_scans_total` | counter | `stage` (input/output), `outcome` (allowed/blocked/redacted), `preset`, `provider` |
| `ai_moderation_latency_ms` | histogram | `stage`, `provider` |
| `ai_moderation_provider_unavailable_total` | counter | `failure_mode` |
| `ai_pending_approvals_total` | counter | `outcome` (created/approved/denied/expired) |
| `ai_dangerous_action_blocks_total` | counter | `tool_name` |

Activity tags on the runtime span: `ai.moderation.preset`, `ai.moderation.outcome`, `ai.moderation.provider`. Already-existing persona tags from 5b stay untouched.

---

## 10. Testing strategy

### 10.1 Behavior tests (no network)

Live under `boilerplateBE/tests/Starter.Api.Tests/Ai/Moderation/` (new folder) and `Ai/Approvals/`. Use the `MakeAiDb(tenant)` inline helper from 5d-1; register `FakeContentModerator` and `FakeAiAgentRuntime` via the same `IServiceCollection` overrides used by existing AI tests.

### 10.2 Acid tests

Live under `boilerplateBE/tests/Starter.Api.Tests/Ai/AcidTests/Plan5d2*.cs`, mirroring 5d-1's `Plan5d1*AcidTests.cs`.

| ID | File | Asserts |
|---|---|---|
| M1 | `Plan5d2ChildSafeOutputBlockedAcidTests.cs` | Output scan returns Blocked → `OutputBlocked` status; refusal template returned; `AiModerationEvent` row Stage=Output Outcome=Blocked; usage log records actual tokens (LLM was called); cost reconciliation runs. |
| M2 | `Plan5d2InputBlockedAcidTests.cs` | Input scan returns Blocked → `InputBlocked` status before LLM; **no usage log row, no cost claim, no rate-limit slot consumed** (decorator returns before delegating, so `CostCapEnforcingAgentRuntime` is never invoked); `AiModerationEvent` Stage=Input. |
| M3 | `Plan5d2ProfessionalRedactionAcidTests.cs` | PII redactor finds email+phone; final content has `[REDACTED]`; `Outcome=Redacted` event; usage log normal; redaction-failure path tested separately. |
| M4 | `Plan5d2DangerousActionApprovalAcidTests.cs` | Dangerous tool call writes `AiPendingApproval` row; run terminates `AwaitingApproval`; chat returns 200 with `Status="awaiting_approval"`; Approve re-dispatches the command and appends `tool` message; Deny appends denial; expiration job auto-denies after 24h. |
| M5 | `Plan5d2StreamingBufferingAcidTests.cs` | `Streaming=true`+ChildSafe → recorded sink receives zero `OnDeltaAsync` calls before moderation completes; on Allowed, deltas arrive as one chunk after scan; on Blocked, no content delta + `moderation_blocked` frame. Standard preset: deltas interleave with the runtime stream. |
| M6 | `Plan5d2ProviderUnavailableAcidTests.cs` | No moderation key resolved + ChildSafe → `ModerationProviderUnavailable` status. Standard same setup → run completes with warning log + Allowed. |
| W1 | `Plan5d2OpenAiModerationWireTests.cs` | Gated `MODERATION_LIVE_TESTS=1`. Three known-bad strings; asserts response shape parses + at least one expected category fires. Fails the build if OpenAI changes the API contract. |

### 10.3 Unit tests

| Component | File | Coverage |
|---|---|---|
| `OpenAiContentModerator` | `OpenAiContentModeratorTests.cs` | Threshold mapping; always-block category list; PII not over-leaked into categories. |
| `RegexPiiRedactor` | `RegexPiiRedactorTests.cs` | Email, phone, SSN, Luhn-validated card, IBAN — true positives and known false-positive avoidance. |
| `SafetyProfileResolver` | `SafetyProfileResolverTests.cs` | Override precedence; tenant > platform > fallback; cache hit path; invalidation on each event. |
| `AgentToolDispatcher` (new path) | `AgentToolDispatcherDangerousActionTests.cs` | Attribute detected → `AiPendingApproval` written + `IsError=true` returned with `AiAgent.AwaitingApproval` code. With approval grant in context → action executes. |
| `BufferingSink` / `PassthroughSink` | `BufferingSinkTests.cs` | Delta hold/release semantics; observability events forwarded immediately; cancellation. |
| `AiSafetyPresetProfile` | `AiSafetyPresetProfileTests.cs` | Create / Update raise `SafetyPresetProfileUpdatedEvent`; unique-row constraint. |
| `AiPendingApproval` | `AiPendingApprovalTests.cs` | Constructor enforces conversation OR task; transitions; `Approve` rejects from non-Pending. |
| `AiPersona.Update` | extend existing tests | Now raises `PersonaUpdatedEvent`. |

---

## 11. Cross-module integration

### 11.1 Communication module (notifications)

5d-2 raises the four `AgentApproval*Event` MediatR notifications from §4.7. The Communication module subscribes to them via a new `CommunicationAiEventHandler : INotificationHandler<AgentApprovalPendingEvent>, INotificationHandler<AgentApprovalApprovedEvent>, INotificationHandler<AgentApprovalDeniedEvent>, INotificationHandler<AgentApprovalExpiredEvent>` (sibling to the existing `Communication*EventHandler` files). For each event the handler:

1. **Resolves recipients.**
   - `AgentApprovalPendingEvent` → all tenant users with `Ai.Agents.ApproveAction` (so admins see the inbox alert). The requesting user is *not* notified — they already saw the approval card inline in chat.
   - `AgentApprovalApprovedEvent` / `AgentApprovalDeniedEvent` → the `RequestingUserId` (chat caller). For operational agents (no requesting user) → tenant users with `Ai.Agents.ApproveAction`, so they see what their colleague decided.
   - `AgentApprovalExpiredEvent` → the `RequestingUserId` if present, otherwise tenant approvers.

2. **Builds the event-data dictionary** (`assistantName`, `toolName`, `reason`, `approvalId`, deep-link URL `/ai/agents/approvals/{id}`, `expiresAt`) and calls `ITriggerRuleEvaluator.EvaluateAsync(eventName, tenantId, requestingUserId, eventData, ct)`.

3. **Event keys registered with the trigger-rule system** (seeded once, no migration cost):
   - `ai.agent.approval.pending`
   - `ai.agent.approval.approved`
   - `ai.agent.approval.denied`
   - `ai.agent.approval.expired`

   Tenant admins configure preferred channels (email, in-app, push) per event key via the existing notification-preferences UI. **No 5d-2 UI work required** — the existing Communication admin pages already handle channel routing.

4. **Default channel routing seed.** `RequiredNotification` rows are seeded for the four event keys with `NotificationChannel.InApp` as the default for every tenant (so admins see something in the bell icon out of the box). Email is opt-in per tenant. The seed is idempotent.

The Communication module's existing `MessageDispatcher` + outbox pattern handles the actual delivery (cross-replica safe, persistent, retry-aware). This is the single integration seam — 5d-2 does not add direct email/push code paths.

### 11.2 Webhook event integration (existing AI module pattern)

Mirroring `ai.chat.completed`, `ai.quota.exceeded`, `ai.rag.completed/degraded/failed` already published by `IWebhookPublisher` from `ChatExecutionService`, 5d-2 adds:

| Event name | Published from | Payload |
|---|---|---|
| `ai.moderation.blocked` | `ContentModerationEnforcingAgentRuntime` after writing the moderation event row | `{ tenantId, assistantId, agentPrincipalId?, conversationId?, stage, preset, categories, reason }` |
| `ai.agent.approval.pending` | `IPendingApprovalService.CreateAsync` | `{ tenantId, approvalId, assistantId, toolName, reason?, requestingUserId?, expiresAt }` |
| `ai.agent.approval.approved` | `ApprovePendingActionCommandHandler` | `{ tenantId, approvalId, decisionUserId, decidedAt }` |
| `ai.agent.approval.denied` | `DenyPendingActionCommandHandler` | `{ tenantId, approvalId, decisionUserId, decisionReason, decidedAt }` |
| `ai.agent.approval.expired` | `AiPendingApprovalExpirationJob` | `{ tenantId, approvalId, expiredAt }` |

All publish failures are caught + logged warnings (fire-and-forget), matching the existing pattern in `ChatExecutionService`. Tenants subscribe via the existing `WebhookEndpoint` admin page — no 5d-2 UI work required.

### 11.3 Adjacent plans (forward and backward)

- **5b (Persona)** — `Persona.SafetyPreset` is the inheritance source for the agent override. Resolution order in §5.3 stays aligned.
- **5d-1 (Identity + Enforcement)** — Decorator stack in §3 wraps on top of `CostCapEnforcingAgentRuntime`. `AgentExecutionScope` / `IExecutionContext` extended in §4.4 to carry the approval grant flag.
- **5e (Bundled Platform Agents)** — The `Teacher Tutor` and `Brand Content Agent` starter templates ship with explicit `SafetyPresetOverride` set in the template definition.
- **5f (Admin AI Settings backend)** — Surfaces tenant-level safety preset defaults; the threshold-profile editing UI lands here. 5d-2 backend endpoints in §7.1 are sufficient for the Plan 6 chat sidebar to render approval cards.
- **6 (Chat Sidebar UI)** — Renders `awaiting_approval` SSE frame as inline approval card; renders `moderation_blocked` as a refusal toast.
- **7b (Advanced Admin Pages)** — Content Moderation Config UI binds to §7.1 endpoints. Moderation event dashboard binds to `GetModerationEventsQuery`.
- **11 (Marketplace + Cost Governance)** — Future: per-tenant moderation budgets if LLM-based moderation is ever enabled. Not relevant in 5d-2 (OpenAI moderation is free).

### 11.4 Modules NOT changed by 5d-2

- **Billing** — no changes. Plan ceilings (`SubscriptionPlan.Features` JSON) added in 5d-1 are sufficient; 5d-2 introduces no new plan-level limits.
- **Webhooks** — no controller / DI changes; only the new event-name strings published through the existing `IWebhookPublisher`.
- **Notifications** (core, not the Communication module) — the core `Notification` / `NotificationPreference` entities are not extended. 5d-2 routes everything through Communication's `TriggerRuleEvaluator`, which already writes to the core Notification table for the in-app channel.
- **Audit Logs** — no schema changes. Approve/deny/expire decisions write via the existing 5d-1 `AuditLogAgentAttributionInterceptor`.

---

## 12. Out of Scope (explicit)

- **LLM-based second-pass moderator.** Interface seam (`IContentModerator`) exists; impl is post-5d.
- **Sentence-boundary streaming buffering.** Preset-conditional buffering is the locked behavior. Sentence buffering is documented as a future option in this spec only.
- **Brand-voice / tone enforcement** for ProfessionalModerated. Deferred to 7b.
- **`Ai.Agents.ApproveOwnAction`** end-user permission for self-approval of dangerous actions. Approvals are admin-only in 5d-2; revisit on 7b feedback.
- **Auto-resume of the agent loop after approval.** The user explicitly continues. Auto-continue lands when Plan 6 has the chat-sidebar UX surface.
- **Public/widget surface moderation (anonymous persona).** Plan 8f.
- **Mobile chat UI for pending approvals.** Plan 9.
- **Custom presets beyond `Standard` / `ChildSafe` / `ProfessionalModerated`.** Preset is a fixed enum. Tenant-tunable threshold profiles are the only configurability.
- **Audit-log integration of moderation events.** Moderation events live in their own table; they are *not* duplicated into `AuditLog`. Approve/deny *decisions* on `AiPendingApproval` *do* write to `AuditLog` (handled by the existing `AuditLogAgentAttributionInterceptor`).
- **Per-block user notifications for moderation events.** `ai.moderation.blocked` is published as a webhook (so ops can wire alerting) but does **not** trigger user-visible notifications via the Communication module. Volume could be high and noisy; tenants who want this can register their own trigger rule against the webhook. Revisit on real-world feedback.
- **PII redactor false-negative tuning beyond the canonical formats.** v1 ships email, E.164 phone, SSN-style, Luhn-validated card, IBAN. Locale-specific government-id formats are deferred.

---

## 13. Open questions (none)

All gating decisions resolved during brainstorming. Implementation plan to follow.
