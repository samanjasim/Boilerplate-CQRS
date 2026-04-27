# AI Plan 5d-2 — Safety + Content Moderation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Layer input/output content moderation (`Standard` / `ChildSafe` / `ProfessionalModerated` presets) and a `[DangerousAction]` human-approval pause on top of 5d-1's agent identity foundation, with cross-module integration to the Communication module so approval lifecycle events flow through tenant-configured notification channels.

**Architecture:** A `ContentModerationEnforcingAgentRuntime` decorator wraps `CostCapEnforcingAgentRuntime` (5d-1) and scans agent input pre-flight + agent output post-flight; preset-conditional `BufferingSink` / `PassthroughSink` decide whether to suppress streaming for safety presets. A `[DangerousAction]` attribute check inside `AgentToolDispatcher` short-circuits dangerous tool calls into an `AiPendingApproval` row, terminating the run with `AgentRunStatus.AwaitingApproval`; an `ApprovePendingActionCommand` re-dispatches the original MediatR command via an `ApprovalGrantExecutionContext` that one-shot bypasses the dispatcher's check. A `AiPendingApprovalExpirationJob` hosted service uses `UPDATE ... FOR UPDATE SKIP LOCKED ... RETURNING` for multi-replica-safe atomic expiration without distributed locks. Four `AgentApproval*Event` MediatR notifications are subscribed by a new `CommunicationAiEventHandler` in the Communication module which routes through the existing `ITriggerRuleEvaluator`.

**Tech Stack:** .NET 10, EF Core 9, MediatR, FluentValidation, xUnit + FluentAssertions + Moq, Redis (StackExchange.Redis), PostgreSQL, OpenAI SDK (`OpenAI.Moderations.ModerationClient`).

**Spec:** `docs/superpowers/specs/2026-04-27-ai-plan-5d-2-safety-moderation-design.md`

---

## Conventions used by every task

**EF entity persistence (verified by inspecting peer configs in `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/`):**
- Table names: **snake_case**, prefixed with `ai_` (e.g., `ai_safety_preset_profiles`, `ai_moderation_events`, `ai_pending_approvals`).
- Column names: **snake_case** via explicit `HasColumnName(...)` overrides on every property.
- PK property: `b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();` — GUID PKs are assigned in code.
- Index database names: `ix_{table}_{column[_column...]}`. Use `HasDatabaseName(...)`.
- DbSet property names on `AiDbContext`: **plural** (e.g., `AiSafetyPresetProfiles`, `AiModerationEvents`, `AiPendingApprovals`).
- jsonb columns use `ValueConverter<...>` + `ValueComparer<...>` exactly like `EnabledToolNames` in `AiAssistantConfiguration`.

When creating any new entity in this plan, copy the shape from `AiAssistantConfiguration.cs` — do **not** invent a new naming scheme.

**Tests:**
- Tests live under `boilerplateBE/tests/Starter.Api.Tests/Ai/<area>/`. Use `AiDbContext` over `UseInMemoryDatabase($"<test-id>-{Guid.NewGuid()}")` for isolation. Mock `ICurrentUserService` via Moq. Existing AI tests build the context inline; do **not** assume a `TestDb<T>()` helper exists — the snippet below is the canonical setup, copied verbatim into every test that needs an `AiDbContext`:

  ```csharp
  static (AiDbContext db, Mock<ICurrentUserService> cu) MakeAiDb(Guid? tenant)
  {
      var cu = new Mock<ICurrentUserService>();
      cu.SetupGet(x => x.TenantId).Returns(tenant);
      var opts = new DbContextOptionsBuilder<AiDbContext>()
          .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
      return (new AiDbContext(opts, cu.Object), cu);
  }
  ```

- Acid tests live under `Ai/AcidTests/Plan5d2*.cs`, mirroring 5d-1's `Plan5d1*AcidTests.cs`. Acid tests M2 (input blocked, no cost claim), M5 (streaming buffering), and M6 (provider unavailable + fail-closed) need the full DI graph; copy the existing `AiPostgresFixture`/Testcontainers pattern from `tests/Starter.Api.Tests/Ai/Retrieval/AiPostgresFixture.cs` only if a Postgres/Redis-backed test is unavoidable.
- Entities follow the existing pattern: `private set` properties, private parameterless ctor, static `Create(...)` factory.
- Errors use `Starter.Shared.Results.Error` with stable `Code` strings: `"AiAgent.<Reason>"`, `"AiModeration.<Reason>"`, `"PendingApproval.<Reason>"`.
- **Never inject `IPublishEndpoint` in MediatR handlers** (per CLAUDE.md). 5d-2 uses in-process MediatR `INotification` events for all approval lifecycle signaling. Communication subscribes via `INotificationHandler<T>`.
- Migrations are generated locally for verification but **not committed** (per CLAUDE.md: this is boilerplate; consuming apps generate their own migrations).
- After every code change: `dotnet build boilerplateBE/Starter.sln` must succeed before commit.
- Commit message convention: `feat(ai): 5d-2 — <short>` for features, `test(ai): 5d-2 — <short>` for test-only.

---

## Phase A — Foundations (no behavior, just scaffolding)

### Task A1: Permissions, error codes, enum additions, new enums

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/AiModerationErrors.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/PendingApprovalErrors.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Runtime/AgentRunResult.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/ModerationStage.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/ModerationOutcome.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/ModerationProvider.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/ModerationFailureMode.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/PendingApprovalStatus.cs`

- [ ] **Step 1: Add the four new permissions**

Append to `AiPermissions.cs`:

```csharp
public const string SafetyProfilesManage = "Ai.SafetyProfiles.Manage";
public const string AgentsApproveAction = "Ai.Agents.ApproveAction";
public const string AgentsViewApprovals = "Ai.Agents.ViewApprovals";
public const string ModerationView = "Ai.Moderation.View";
```

- [ ] **Step 2: Add `AgentRunStatus` values**

In `AgentRunResult.cs`, append to the `AgentRunStatus` enum (after `RateLimitExceeded`):

```csharp
InputBlocked = 7,
OutputBlocked = 8,
AwaitingApproval = 9,
ModerationProviderUnavailable = 10,
```

- [ ] **Step 3: Create the five new enums**

Each enum is a single file in `Domain/Enums/`. Stable integer values matter (persisted as `smallint`):

`ModerationStage.cs`:
```csharp
namespace Starter.Module.AI.Domain.Enums;
public enum ModerationStage { Input = 0, Output = 1 }
```

`ModerationOutcome.cs`:
```csharp
namespace Starter.Module.AI.Domain.Enums;
public enum ModerationOutcome { Allowed = 0, Blocked = 1, Redacted = 2 }
```

`ModerationProvider.cs`:
```csharp
namespace Starter.Module.AI.Domain.Enums;
public enum ModerationProvider { OpenAi = 0 }
```

`ModerationFailureMode.cs`:
```csharp
namespace Starter.Module.AI.Domain.Enums;
public enum ModerationFailureMode { FailOpen = 0, FailClosed = 1 }
```

`PendingApprovalStatus.cs`:
```csharp
namespace Starter.Module.AI.Domain.Enums;
public enum PendingApprovalStatus { Pending = 0, Approved = 1, Denied = 2, Expired = 3 }
```

- [ ] **Step 4: Create `AiModerationErrors.cs`**

```csharp
using Starter.Shared.Results;

namespace Starter.Module.AI.Domain.Errors;

public static class AiModerationErrors
{
    public static Error InputBlocked(string categoriesSummary) =>
        Error.Validation("AiModeration.InputBlocked",
            $"Input blocked by content moderation: {categoriesSummary}.");

    public static Error OutputBlocked(string categoriesSummary) =>
        Error.Validation("AiModeration.OutputBlocked",
            $"Output blocked by content moderation: {categoriesSummary}.");

    public static readonly Error ProviderUnavailable =
        Error.Failure("AiModeration.ProviderUnavailable",
            "Content moderation provider is unavailable; safe presets refuse the request.");

    public static Error PresetProfileNotFound(ModerationProvider provider, Domain.Enums.SafetyPreset preset) =>
        Error.NotFound("AiModeration.PresetProfileNotFound",
            $"No safety profile configured for preset '{preset}' on provider '{provider}'.");
}
```

> **Note:** `SafetyPreset` lives under `Starter.Abstractions.Ai` (added in 5b). The `using` for it goes in implementation files; the error helper above takes a generic param to avoid an extra `using` here. If `Domain.Enums.SafetyPreset` doesn't compile, swap to `Starter.Abstractions.Ai.SafetyPreset`.

- [ ] **Step 5: Create `PendingApprovalErrors.cs`**

```csharp
using Starter.Shared.Results;

namespace Starter.Module.AI.Domain.Errors;

public static class PendingApprovalErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("PendingApproval.NotFound", $"Pending approval '{id}' not found.");

    public static readonly Error NotPending =
        Error.Conflict("PendingApproval.NotPending",
            "Pending approval is no longer in the Pending state (already approved, denied, or expired).");

    public static Error ToolUnavailable(string commandTypeName) =>
        Error.Failure("PendingApproval.ToolUnavailable",
            $"The MediatR command type '{commandTypeName}' could not be resolved at approval time.");

    public static readonly Error DenyReasonRequired =
        Error.Validation("PendingApproval.DenyReasonRequired",
            "A reason must be provided when denying a pending approval.");

    public static readonly Error AccessDenied =
        Error.Forbidden("Caller is not permitted to view or act on this pending approval.");
}
```

- [ ] **Step 6: Build and commit**

```bash
dotnet build boilerplateBE/Starter.sln
git add -p && git commit -m "feat(ai): 5d-2 — permissions, error codes, run statuses, moderation enums"
```

---

### Task A2: `[DangerousAction]` attribute + `IExecutionContext` grant flag + `ApprovalGrantExecutionContext`

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Common/Attributes/DangerousActionAttribute.cs`
- Modify: `boilerplateBE/src/Starter.Application/Common/Interfaces/IExecutionContext.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure.Identity/Services/HttpExecutionContext.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Runtime/AgentExecutionScope.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Runtime/ApprovalGrantExecutionContext.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Approvals/ApprovalGrantExecutionContextTests.cs`

- [ ] **Step 1: Create the attribute**

```csharp
namespace Starter.Application.Common.Attributes;

/// <summary>
/// Marks a MediatR command (or any IRequest type) as a destructive action that must
/// require human approval when invoked from an agent runtime. The check is performed
/// inside AgentToolDispatcher; non-agent send paths ignore the attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DangerousActionAttribute : Attribute
{
    public string? Reason { get; }
    public DangerousActionAttribute(string? reason = null) => Reason = reason;
}
```

- [ ] **Step 2: Extend `IExecutionContext`**

Append to `IExecutionContext`:

```csharp
/// <summary>
/// True for the duration of an approved-action re-dispatch (one-shot). AgentToolDispatcher
/// skips the [DangerousAction] check when this returns true. Default impls return false.
/// </summary>
bool DangerousActionApprovalGrant { get; }
```

- [ ] **Step 3: Implement on `HttpExecutionContext`**

Add property returning `false` (HTTP path is never inside an approval re-dispatch):

```csharp
public bool DangerousActionApprovalGrant => false;
```

- [ ] **Step 4: Implement on `AgentExecutionScope`**

Add property returning `false`. The grant only ever applies inside `ApprovalGrantExecutionContext`.

```csharp
public bool DangerousActionApprovalGrant => false;
```

- [ ] **Step 5: Create `ApprovalGrantExecutionContext`**

```csharp
using Starter.Application.Common.Interfaces;

namespace Starter.Module.AI.Infrastructure.Runtime;

/// <summary>
/// One-shot wrapper installed via <see cref="AmbientExecutionContext.Use"/> for the
/// duration of an approved MediatR re-dispatch. Delegates everything to the inner
/// context but flips <see cref="IExecutionContext.DangerousActionApprovalGrant"/> to true,
/// causing AgentToolDispatcher to bypass the [DangerousAction] check exactly once.
/// </summary>
internal sealed class ApprovalGrantExecutionContext(IExecutionContext inner) : IExecutionContext
{
    public Guid? UserId => inner.UserId;
    public Guid? AgentPrincipalId => inner.AgentPrincipalId;
    public Guid? TenantId => inner.TenantId;
    public Guid? AgentRunId => inner.AgentRunId;
    public bool DangerousActionApprovalGrant => true;
    public bool HasPermission(string permission) => inner.HasPermission(permission);
}
```

- [ ] **Step 6: Write the test**

```csharp
using FluentAssertions;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Infrastructure.Runtime;
using Xunit;

namespace Starter.Api.Tests.Ai.Approvals;

public sealed class ApprovalGrantExecutionContextTests
{
    private sealed class StubInner : IExecutionContext
    {
        public Guid? UserId => Guid.NewGuid();
        public Guid? AgentPrincipalId => Guid.NewGuid();
        public Guid? TenantId => Guid.NewGuid();
        public Guid? AgentRunId => null;
        public bool DangerousActionApprovalGrant => false;
        public bool HasPermission(string permission) => permission == "ok";
    }

    [Fact]
    public void Wrapper_Sets_Grant_True_And_Delegates_Other_Members()
    {
        var inner = new StubInner();
        var wrapped = new ApprovalGrantExecutionContext(inner);

        wrapped.DangerousActionApprovalGrant.Should().BeTrue();
        wrapped.UserId.Should().Be(inner.UserId);
        wrapped.HasPermission("ok").Should().BeTrue();
        wrapped.HasPermission("nope").Should().BeFalse();
    }
}
```

- [ ] **Step 7: Run test, build, commit**

```bash
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~ApprovalGrantExecutionContextTests" --no-build
# expect: FAIL until step 1-5 are saved
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~ApprovalGrantExecutionContextTests"
# expect: PASS
git add -p && git commit -m "feat(ai): 5d-2 — [DangerousAction] attribute + IExecutionContext grant flag + ApprovalGrantExecutionContext"
```

---

### Task A3: Refusal-template RESX files (mirror `SafetyPresets.resx`)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Resources/ModerationRefusalTemplates.resx`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Resources/ModerationRefusalTemplates.ar.resx`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Moderation/ModerationRefusalResourceTests.cs`

The template lookup key shape matches `SafetyPresets.resx`: `{Preset}.{Audience}` (e.g., `ChildSafe.Internal`, `ProfessionalModerated.EndCustomer`). Plus a synthetic key per preset for the provider-unavailable refusal: `{Preset}.ProviderUnavailable`.

- [ ] **Step 1: Author `ModerationRefusalTemplates.resx`**

Use the existing `SafetyPresets.resx` as the structural template. Required keys (English):

| Key | Value |
|---|---|
| `Standard.Internal` | `I can't help with that request as it conflicts with our usage policy. Please rephrase your question or contact support if you believe this is in error.` |
| `Standard.EndCustomer` | `I'm sorry, but I can't respond to that request.` |
| `Standard.Anonymous` | `That request can't be handled here.` |
| `Standard.ProviderUnavailable` | `Content moderation is temporarily unavailable. Please try again shortly.` |
| `ChildSafe.Internal` | `I can't continue with that — let's try a different question.` |
| `ChildSafe.EndCustomer` | `Let's try a different question.` |
| `ChildSafe.Anonymous` | `Let's try a different question.` |
| `ChildSafe.ProviderUnavailable` | `I'm not able to chat right now. Please try again later.` |
| `ProfessionalModerated.Internal` | `I can't share that response in its current form. Please rephrase.` |
| `ProfessionalModerated.EndCustomer` | `I'm not able to provide that response. Please contact your account team.` |
| `ProfessionalModerated.Anonymous` | `Unable to respond.` |
| `ProfessionalModerated.ProviderUnavailable` | `This service is temporarily unavailable.` |

- [ ] **Step 2: Author `ModerationRefusalTemplates.ar.resx`**

Same keys, Arabic translations. Mirror the existing `SafetyPresets.ar.resx` style — direct translations are fine; we don't need a dialect specialist for v1.

- [ ] **Step 3: Verify the resources are picked up**

Add a smoke test:

```csharp
using System.Globalization;
using System.Resources;
using FluentAssertions;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class ModerationRefusalResourceTests
{
    [Theory]
    [InlineData("Standard.Internal", "en")]
    [InlineData("ChildSafe.Internal", "en")]
    [InlineData("ChildSafe.Internal", "ar")]
    [InlineData("ProfessionalModerated.ProviderUnavailable", "en")]
    public void All_Required_Keys_Resolve(string key, string culture)
    {
        var rm = new ResourceManager(
            "Starter.Module.AI.Resources.ModerationRefusalTemplates",
            typeof(Starter.Module.AI.AIModule).Assembly);
        var value = rm.GetString(key, new CultureInfo(culture));
        value.Should().NotBeNullOrWhiteSpace();
    }
}
```

- [ ] **Step 4: Run test + build + commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~ModerationRefusalResourceTests"
git add -p && git commit -m "feat(ai): 5d-2 — moderation refusal-template RESX (en + ar)"
```

---

## Phase B — Entities + domain events

### Task B1: `AiSafetyPresetProfile` entity + EF config + seed

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiSafetyPresetProfile.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Events/SafetyPresetProfileUpdatedEvent.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiSafetyPresetProfileConfiguration.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/Seed/SafetyPresetProfileSeed.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Moderation/AiSafetyPresetProfileTests.cs`

- [ ] **Step 1: Create the domain event**

```csharp
using MediatR;

namespace Starter.Module.AI.Domain.Events;

public sealed record SafetyPresetProfileUpdatedEvent(
    Guid? TenantId,
    Guid ProfileId) : INotification;
```

- [ ] **Step 2: Create the entity**

```csharp
using Starter.Abstractions.Ai;
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Events;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiSafetyPresetProfile : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public SafetyPreset Preset { get; private set; }
    public ModerationProvider Provider { get; private set; }
    public string CategoryThresholdsJson { get; private set; } = "{}";
    public string BlockedCategoriesJson { get; private set; } = "[]";
    public ModerationFailureMode FailureMode { get; private set; }
    public bool RedactPii { get; private set; }
    public int Version { get; private set; } = 1;
    public bool IsActive { get; private set; } = true;

    private AiSafetyPresetProfile() { }
    private AiSafetyPresetProfile(
        Guid id,
        Guid? tenantId,
        SafetyPreset preset,
        ModerationProvider provider,
        string thresholdsJson,
        string blockedCategoriesJson,
        ModerationFailureMode failureMode,
        bool redactPii) : base(id)
    {
        TenantId = tenantId;
        Preset = preset;
        Provider = provider;
        CategoryThresholdsJson = thresholdsJson;
        BlockedCategoriesJson = blockedCategoriesJson;
        FailureMode = failureMode;
        RedactPii = redactPii;
    }

    public static AiSafetyPresetProfile Create(
        Guid? tenantId,
        SafetyPreset preset,
        ModerationProvider provider,
        string thresholdsJson,
        string blockedCategoriesJson,
        ModerationFailureMode failureMode,
        bool redactPii)
    {
        if (string.IsNullOrWhiteSpace(thresholdsJson)) thresholdsJson = "{}";
        if (string.IsNullOrWhiteSpace(blockedCategoriesJson)) blockedCategoriesJson = "[]";

        var entity = new AiSafetyPresetProfile(
            Guid.NewGuid(), tenantId, preset, provider,
            thresholdsJson, blockedCategoriesJson, failureMode, redactPii);
        entity.RaiseDomainEvent(new SafetyPresetProfileUpdatedEvent(tenantId, entity.Id));
        return entity;
    }

    public void Update(
        string thresholdsJson,
        string blockedCategoriesJson,
        ModerationFailureMode failureMode,
        bool redactPii)
    {
        CategoryThresholdsJson = string.IsNullOrWhiteSpace(thresholdsJson) ? "{}" : thresholdsJson;
        BlockedCategoriesJson = string.IsNullOrWhiteSpace(blockedCategoriesJson) ? "[]" : blockedCategoriesJson;
        FailureMode = failureMode;
        RedactPii = redactPii;
        Version += 1;
        ModifiedAt = DateTime.UtcNow;
        RaiseDomainEvent(new SafetyPresetProfileUpdatedEvent(TenantId, Id));
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        ModifiedAt = DateTime.UtcNow;
        RaiseDomainEvent(new SafetyPresetProfileUpdatedEvent(TenantId, Id));
    }
}
```

- [ ] **Step 3: Create the EF configuration**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiSafetyPresetProfileConfiguration : IEntityTypeConfiguration<AiSafetyPresetProfile>
{
    public void Configure(EntityTypeBuilder<AiSafetyPresetProfile> b)
    {
        b.ToTable("ai_safety_preset_profiles");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Preset).HasColumnName("preset").HasConversion<int>().IsRequired();
        b.Property(x => x.Provider).HasColumnName("provider").HasConversion<int>().IsRequired();
        b.Property(x => x.CategoryThresholdsJson).HasColumnName("category_thresholds").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.BlockedCategoriesJson).HasColumnName("blocked_categories").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.FailureMode).HasColumnName("failure_mode").HasConversion<int>().IsRequired();
        b.Property(x => x.RedactPii).HasColumnName("redact_pii").IsRequired();
        b.Property(x => x.Version).HasColumnName("version").IsRequired();
        b.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.ModifiedAt).HasColumnName("modified_at");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.ModifiedBy).HasColumnName("modified_by");

        // Unique active row per (tenant_id, preset, provider). NULL tenant_id = platform default.
        b.HasIndex(x => new { x.TenantId, x.Preset, x.Provider })
            .IsUnique()
            .HasFilter("is_active = true")
            .HasDatabaseName("ux_ai_safety_preset_profiles_tenant_preset_provider_active");
    }
}
```

- [ ] **Step 4: Create the seed**

```csharp
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Persistence.Seed;

internal static class SafetyPresetProfileSeed
{
    public static async Task SeedAsync(AiDbContext db, CancellationToken ct = default)
    {
        var any = await db.AiSafetyPresetProfiles
            .IgnoreQueryFilters()
            .AnyAsync(p => p.TenantId == null, ct);
        if (any) return;

        const string standardThresholds =
            """{"sexual":0.85,"hate":0.85,"violence":0.85,"self-harm":0.85,"harassment":0.85}""";
        const string childSafeThresholds =
            """{"sexual":0.5,"hate":0.5,"violence":0.5,"self-harm":0.3,"harassment":0.5}""";
        const string emptyBlocked = "[]";
        const string childSafeBlocked =
            """["sexual-minors","violence-graphic"]""";

        db.AiSafetyPresetProfiles.AddRange(
            AiSafetyPresetProfile.Create(
                tenantId: null,
                preset: SafetyPreset.Standard,
                provider: ModerationProvider.OpenAi,
                thresholdsJson: standardThresholds,
                blockedCategoriesJson: emptyBlocked,
                failureMode: ModerationFailureMode.FailOpen,
                redactPii: false),
            AiSafetyPresetProfile.Create(
                tenantId: null,
                preset: SafetyPreset.ChildSafe,
                provider: ModerationProvider.OpenAi,
                thresholdsJson: childSafeThresholds,
                blockedCategoriesJson: childSafeBlocked,
                failureMode: ModerationFailureMode.FailClosed,
                redactPii: false),
            AiSafetyPresetProfile.Create(
                tenantId: null,
                preset: SafetyPreset.ProfessionalModerated,
                provider: ModerationProvider.OpenAi,
                thresholdsJson: standardThresholds,
                blockedCategoriesJson: emptyBlocked,
                failureMode: ModerationFailureMode.FailClosed,
                redactPii: true)
        );

        await db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 5: Write the round-trip test**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Events;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class AiSafetyPresetProfileTests
{
    private static (AiDbContext db, Mock<ICurrentUserService> cu) MakeAiDb(Guid? tenant)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        return (new AiDbContext(opts, cu.Object), cu);
    }

    [Fact]
    public async Task Create_Round_Trips_And_Raises_Updated_Event()
    {
        var (db, _) = MakeAiDb(null);
        var entity = AiSafetyPresetProfile.Create(
            tenantId: null,
            preset: SafetyPreset.ChildSafe,
            provider: ModerationProvider.OpenAi,
            thresholdsJson: """{"sexual":0.5}""",
            blockedCategoriesJson: """["sexual-minors"]""",
            failureMode: ModerationFailureMode.FailClosed,
            redactPii: false);

        entity.DomainEvents.Should().ContainSingle(e => e is SafetyPresetProfileUpdatedEvent);

        db.AiSafetyPresetProfiles.Add(entity);
        await db.SaveChangesAsync();

        var loaded = await db.AiSafetyPresetProfiles.FirstAsync();
        loaded.Preset.Should().Be(SafetyPreset.ChildSafe);
        loaded.RedactPii.Should().BeFalse();
        loaded.FailureMode.Should().Be(ModerationFailureMode.FailClosed);
        loaded.Version.Should().Be(1);
    }

    [Fact]
    public void Update_Bumps_Version_And_Raises_Event()
    {
        var entity = AiSafetyPresetProfile.Create(
            null, SafetyPreset.Standard, ModerationProvider.OpenAi,
            "{}", "[]", ModerationFailureMode.FailOpen, false);
        entity.ClearDomainEvents();

        entity.Update("""{"sexual":0.9}""", "[]", ModerationFailureMode.FailOpen, false);

        entity.Version.Should().Be(2);
        entity.DomainEvents.Should().ContainSingle(e => e is SafetyPresetProfileUpdatedEvent);
    }
}
```

- [ ] **Step 6: Build, run test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiSafetyPresetProfileTests"
git add -p && git commit -m "feat(ai): 5d-2 — AiSafetyPresetProfile entity + config + seed"
```

> **DbSet wiring** (`AiDbContext.AiSafetyPresetProfiles`) and seed call site happen in **Task B7**. The build passes here because the new entity isn't yet referenced from the context.

---

### Task B2: `AiModerationEvent` entity + EF config

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiModerationEvent.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiModerationEventConfiguration.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Moderation/AiModerationEventTests.cs`

- [ ] **Step 1: Create the entity**

```csharp
using Starter.Abstractions.Ai;
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiModerationEvent : BaseEntity, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid? AssistantId { get; private set; }
    public Guid? AgentPrincipalId { get; private set; }
    public Guid? ConversationId { get; private set; }
    public Guid? AgentTaskId { get; private set; }
    public Guid? MessageId { get; private set; }
    public ModerationStage Stage { get; private set; }
    public SafetyPreset Preset { get; private set; }
    public ModerationOutcome Outcome { get; private set; }
    public string CategoriesJson { get; private set; } = "{}";
    public ModerationProvider Provider { get; private set; }
    public string? BlockedReason { get; private set; }
    public bool RedactionFailed { get; private set; }
    public int LatencyMs { get; private set; }

    private AiModerationEvent() { }
    private AiModerationEvent(
        Guid id,
        Guid? tenantId,
        Guid? assistantId,
        Guid? agentPrincipalId,
        Guid? conversationId,
        Guid? agentTaskId,
        Guid? messageId,
        ModerationStage stage,
        SafetyPreset preset,
        ModerationOutcome outcome,
        string categoriesJson,
        ModerationProvider provider,
        string? blockedReason,
        bool redactionFailed,
        int latencyMs) : base(id)
    {
        TenantId = tenantId;
        AssistantId = assistantId;
        AgentPrincipalId = agentPrincipalId;
        ConversationId = conversationId;
        AgentTaskId = agentTaskId;
        MessageId = messageId;
        Stage = stage;
        Preset = preset;
        Outcome = outcome;
        CategoriesJson = categoriesJson;
        Provider = provider;
        BlockedReason = blockedReason;
        RedactionFailed = redactionFailed;
        LatencyMs = latencyMs;
    }

    public static AiModerationEvent Create(
        Guid? tenantId,
        Guid? assistantId,
        Guid? agentPrincipalId,
        Guid? conversationId,
        Guid? agentTaskId,
        Guid? messageId,
        ModerationStage stage,
        SafetyPreset preset,
        ModerationOutcome outcome,
        string categoriesJson,
        ModerationProvider provider,
        int latencyMs,
        string? blockedReason = null,
        bool redactionFailed = false)
    {
        if (string.IsNullOrWhiteSpace(categoriesJson)) categoriesJson = "{}";
        return new AiModerationEvent(
            Guid.NewGuid(), tenantId, assistantId, agentPrincipalId,
            conversationId, agentTaskId, messageId, stage, preset, outcome,
            categoriesJson, provider, blockedReason, redactionFailed, latencyMs);
    }
}
```

- [ ] **Step 2: Create EF configuration**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiModerationEventConfiguration : IEntityTypeConfiguration<AiModerationEvent>
{
    public void Configure(EntityTypeBuilder<AiModerationEvent> b)
    {
        b.ToTable("ai_moderation_events");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.AssistantId).HasColumnName("assistant_id");
        b.Property(x => x.AgentPrincipalId).HasColumnName("agent_principal_id");
        b.Property(x => x.ConversationId).HasColumnName("conversation_id");
        b.Property(x => x.AgentTaskId).HasColumnName("agent_task_id");
        b.Property(x => x.MessageId).HasColumnName("message_id");
        b.Property(x => x.Stage).HasColumnName("stage").HasConversion<int>().IsRequired();
        b.Property(x => x.Preset).HasColumnName("preset").HasConversion<int>().IsRequired();
        b.Property(x => x.Outcome).HasColumnName("outcome").HasConversion<int>().IsRequired();
        b.Property(x => x.CategoriesJson).HasColumnName("categories").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.Provider).HasColumnName("provider").HasConversion<int>().IsRequired();
        b.Property(x => x.BlockedReason).HasColumnName("blocked_reason");
        b.Property(x => x.RedactionFailed).HasColumnName("redaction_failed").IsRequired();
        b.Property(x => x.LatencyMs).HasColumnName("latency_ms").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        b.HasIndex(x => new { x.TenantId, x.CreatedAt })
            .HasDatabaseName("ix_ai_moderation_events_tenant_id_created_at")
            .IsDescending(false, true);
        b.HasIndex(x => new { x.TenantId, x.Outcome })
            .HasDatabaseName("ix_ai_moderation_events_tenant_id_outcome");
        b.HasIndex(x => x.MessageId)
            .HasDatabaseName("ix_ai_moderation_events_message_id")
            .HasFilter("message_id IS NOT NULL");
    }
}
```

- [ ] **Step 3: Write a basic shape test**

```csharp
using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class AiModerationEventTests
{
    [Fact]
    public void Create_Defaults_And_Stamps_Values()
    {
        var ev = AiModerationEvent.Create(
            tenantId: Guid.NewGuid(),
            assistantId: Guid.NewGuid(),
            agentPrincipalId: null,
            conversationId: Guid.NewGuid(),
            agentTaskId: null,
            messageId: null,
            stage: ModerationStage.Output,
            preset: SafetyPreset.ChildSafe,
            outcome: ModerationOutcome.Blocked,
            categoriesJson: """{"sexual-minors":0.93}""",
            provider: ModerationProvider.OpenAi,
            latencyMs: 42,
            blockedReason: "moderation: sexual-minors");

        ev.Id.Should().NotBe(Guid.Empty);
        ev.Stage.Should().Be(ModerationStage.Output);
        ev.Outcome.Should().Be(ModerationOutcome.Blocked);
        ev.RedactionFailed.Should().BeFalse();
        ev.LatencyMs.Should().Be(42);
    }
}
```

- [ ] **Step 4: Build + test + commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiModerationEventTests"
git add -p && git commit -m "feat(ai): 5d-2 — AiModerationEvent entity + config"
```

---

### Task B3: `AiPendingApproval` entity + EF config + lifecycle events

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiPendingApproval.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Events/AgentApprovalPendingEvent.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Events/AgentApprovalApprovedEvent.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Events/AgentApprovalDeniedEvent.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Events/AgentApprovalExpiredEvent.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiPendingApprovalConfiguration.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Approvals/AiPendingApprovalTests.cs`

- [ ] **Step 1: Create the four lifecycle events**

```csharp
// AgentApprovalPendingEvent.cs
using MediatR;
namespace Starter.Module.AI.Domain.Events;
public sealed record AgentApprovalPendingEvent(
    Guid TenantId,
    Guid ApprovalId,
    Guid AssistantId,
    string AssistantName,
    string ToolName,
    string? Reason,
    Guid? RequestingUserId,
    Guid? ConversationId,
    Guid? AgentTaskId,
    DateTime ExpiresAt) : INotification;
```

```csharp
// AgentApprovalApprovedEvent.cs
using MediatR;
namespace Starter.Module.AI.Domain.Events;
public sealed record AgentApprovalApprovedEvent(
    Guid TenantId,
    Guid ApprovalId,
    Guid AssistantId,
    string AssistantName,
    string ToolName,
    Guid? RequestingUserId,
    Guid DecisionUserId,
    string? DecisionReason,
    Guid? ConversationId) : INotification;
```

```csharp
// AgentApprovalDeniedEvent.cs
using MediatR;
namespace Starter.Module.AI.Domain.Events;
public sealed record AgentApprovalDeniedEvent(
    Guid TenantId,
    Guid ApprovalId,
    Guid AssistantId,
    string AssistantName,
    string ToolName,
    Guid? RequestingUserId,
    Guid DecisionUserId,
    string DecisionReason,
    Guid? ConversationId) : INotification;
```

```csharp
// AgentApprovalExpiredEvent.cs
using MediatR;
namespace Starter.Module.AI.Domain.Events;
public sealed record AgentApprovalExpiredEvent(
    Guid TenantId,
    Guid ApprovalId,
    Guid AssistantId,
    string AssistantName,
    string ToolName,
    Guid? RequestingUserId,
    DateTime ExpiredAt) : INotification;
```

- [ ] **Step 2: Create the entity**

```csharp
using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Events;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiPendingApproval : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public Guid AssistantId { get; private set; }
    public string AssistantName { get; private set; } = default!;
    public Guid AgentPrincipalId { get; private set; }
    public Guid? ConversationId { get; private set; }
    public Guid? AgentTaskId { get; private set; }
    public Guid? RequestingUserId { get; private set; }
    public string ToolName { get; private set; } = default!;
    public string CommandTypeName { get; private set; } = default!;
    public string ArgumentsJson { get; private set; } = "{}";
    public string? ReasonHint { get; private set; }
    public PendingApprovalStatus Status { get; private set; } = PendingApprovalStatus.Pending;
    public Guid? DecisionUserId { get; private set; }
    public string? DecisionReason { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private AiPendingApproval() { }
    private AiPendingApproval(
        Guid id, Guid? tenantId, Guid assistantId, string assistantName,
        Guid agentPrincipalId, Guid? conversationId, Guid? agentTaskId,
        Guid? requestingUserId, string toolName, string commandTypeName,
        string argumentsJson, string? reasonHint, DateTime expiresAt) : base(id)
    {
        TenantId = tenantId;
        AssistantId = assistantId;
        AssistantName = assistantName;
        AgentPrincipalId = agentPrincipalId;
        ConversationId = conversationId;
        AgentTaskId = agentTaskId;
        RequestingUserId = requestingUserId;
        ToolName = toolName;
        CommandTypeName = commandTypeName;
        ArgumentsJson = argumentsJson;
        ReasonHint = reasonHint;
        ExpiresAt = expiresAt;
    }

    public static AiPendingApproval Create(
        Guid? tenantId,
        Guid assistantId,
        string assistantName,
        Guid agentPrincipalId,
        Guid? conversationId,
        Guid? agentTaskId,
        Guid? requestingUserId,
        string toolName,
        string commandTypeName,
        string argumentsJson,
        string? reasonHint,
        DateTime expiresAt)
    {
        if (conversationId is null && agentTaskId is null)
            throw new ArgumentException(
                "At least one of conversationId or agentTaskId must be set.",
                nameof(conversationId));
        if (string.IsNullOrWhiteSpace(toolName))
            throw new ArgumentException("toolName required.", nameof(toolName));
        if (string.IsNullOrWhiteSpace(commandTypeName))
            throw new ArgumentException("commandTypeName required.", nameof(commandTypeName));
        if (string.IsNullOrWhiteSpace(argumentsJson)) argumentsJson = "{}";

        var entity = new AiPendingApproval(
            Guid.NewGuid(), tenantId, assistantId, assistantName.Trim(),
            agentPrincipalId, conversationId, agentTaskId, requestingUserId,
            toolName.Trim(), commandTypeName.Trim(), argumentsJson, reasonHint?.Trim(), expiresAt);

        if (tenantId is { } tid)
        {
            entity.RaiseDomainEvent(new AgentApprovalPendingEvent(
                TenantId: tid,
                ApprovalId: entity.Id,
                AssistantId: assistantId,
                AssistantName: entity.AssistantName,
                ToolName: entity.ToolName,
                Reason: entity.ReasonHint,
                RequestingUserId: requestingUserId,
                ConversationId: conversationId,
                AgentTaskId: agentTaskId,
                ExpiresAt: expiresAt));
        }
        return entity;
    }

    public bool TryApprove(Guid decisionUserId, string? reason)
    {
        if (Status != PendingApprovalStatus.Pending) return false;
        Status = PendingApprovalStatus.Approved;
        DecisionUserId = decisionUserId;
        DecisionReason = reason?.Trim();
        DecidedAt = DateTime.UtcNow;
        ModifiedAt = DecidedAt;
        if (TenantId is { } tid)
            RaiseDomainEvent(new AgentApprovalApprovedEvent(
                tid, Id, AssistantId, AssistantName, ToolName,
                RequestingUserId, decisionUserId, DecisionReason, ConversationId));
        return true;
    }

    public bool TryDeny(Guid decisionUserId, string reason)
    {
        if (Status != PendingApprovalStatus.Pending) return false;
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("reason required.", nameof(reason));
        Status = PendingApprovalStatus.Denied;
        DecisionUserId = decisionUserId;
        DecisionReason = reason.Trim();
        DecidedAt = DateTime.UtcNow;
        ModifiedAt = DecidedAt;
        if (TenantId is { } tid)
            RaiseDomainEvent(new AgentApprovalDeniedEvent(
                tid, Id, AssistantId, AssistantName, ToolName,
                RequestingUserId, decisionUserId, DecisionReason!, ConversationId));
        return true;
    }

    public bool TryExpire()
    {
        if (Status != PendingApprovalStatus.Pending) return false;
        Status = PendingApprovalStatus.Expired;
        DecidedAt = DateTime.UtcNow;
        ModifiedAt = DecidedAt;
        if (TenantId is { } tid)
            RaiseDomainEvent(new AgentApprovalExpiredEvent(
                tid, Id, AssistantId, AssistantName, ToolName,
                RequestingUserId, DecidedAt!.Value));
        return true;
    }
}
```

- [ ] **Step 3: Create EF configuration**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiPendingApprovalConfiguration : IEntityTypeConfiguration<AiPendingApproval>
{
    public void Configure(EntityTypeBuilder<AiPendingApproval> b)
    {
        b.ToTable("ai_pending_approvals");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.AssistantId).HasColumnName("assistant_id").IsRequired();
        b.Property(x => x.AssistantName).HasColumnName("assistant_name").HasMaxLength(200).IsRequired();
        b.Property(x => x.AgentPrincipalId).HasColumnName("agent_principal_id").IsRequired();
        b.Property(x => x.ConversationId).HasColumnName("conversation_id");
        b.Property(x => x.AgentTaskId).HasColumnName("agent_task_id");
        b.Property(x => x.RequestingUserId).HasColumnName("requesting_user_id");
        b.Property(x => x.ToolName).HasColumnName("tool_name").HasMaxLength(200).IsRequired();
        b.Property(x => x.CommandTypeName).HasColumnName("command_type_name").HasMaxLength(500).IsRequired();
        b.Property(x => x.ArgumentsJson).HasColumnName("arguments_json").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.ReasonHint).HasColumnName("reason_hint").HasMaxLength(500);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        b.Property(x => x.DecisionUserId).HasColumnName("decision_user_id");
        b.Property(x => x.DecisionReason).HasColumnName("decision_reason").HasMaxLength(1000);
        b.Property(x => x.DecidedAt).HasColumnName("decided_at");
        b.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.ModifiedAt).HasColumnName("modified_at");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.ModifiedBy).HasColumnName("modified_by");

        b.HasIndex(x => new { x.TenantId, x.Status, x.ExpiresAt })
            .HasDatabaseName("ix_ai_pending_approvals_tenant_status_expires");
        b.HasIndex(x => new { x.RequestingUserId, x.Status })
            .HasDatabaseName("ix_ai_pending_approvals_requesting_user_status")
            .HasFilter("requesting_user_id IS NOT NULL");
    }
}
```

- [ ] **Step 4: Write the entity tests**

```csharp
using FluentAssertions;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Events;
using Xunit;

namespace Starter.Api.Tests.Ai.Approvals;

public sealed class AiPendingApprovalTests
{
    private static AiPendingApproval Make(Guid? convId = null, Guid? taskId = null) =>
        AiPendingApproval.Create(
            tenantId: Guid.NewGuid(),
            assistantId: Guid.NewGuid(),
            assistantName: "Tutor",
            agentPrincipalId: Guid.NewGuid(),
            conversationId: convId ?? Guid.NewGuid(),
            agentTaskId: taskId,
            requestingUserId: Guid.NewGuid(),
            toolName: "DeleteAllUsers",
            commandTypeName: "Some.Module.DeleteAllUsersCommand, Some.Module",
            argumentsJson: """{"confirm":true}""",
            reasonHint: "Mass user deletion",
            expiresAt: DateTime.UtcNow.AddHours(24));

    [Fact]
    public void Create_Requires_Conversation_Or_Task()
    {
        var act = () => AiPendingApproval.Create(
            null, Guid.NewGuid(), "x", Guid.NewGuid(),
            conversationId: null, agentTaskId: null,
            requestingUserId: null, toolName: "t",
            commandTypeName: "T, A", argumentsJson: "{}",
            reasonHint: null, expiresAt: DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_Raises_Pending_Event()
    {
        var pa = Make();
        pa.DomainEvents.Should().ContainSingle(e => e is AgentApprovalPendingEvent);
    }

    [Fact]
    public void TryApprove_Transitions_And_Raises_Event()
    {
        var pa = Make();
        pa.ClearDomainEvents();

        var ok = pa.TryApprove(Guid.NewGuid(), "looks good");

        ok.Should().BeTrue();
        pa.Status.Should().Be(PendingApprovalStatus.Approved);
        pa.DecidedAt.Should().NotBeNull();
        pa.DomainEvents.Should().ContainSingle(e => e is AgentApprovalApprovedEvent);
    }

    [Fact]
    public void TryApprove_Returns_False_If_Already_Decided()
    {
        var pa = Make();
        pa.TryDeny(Guid.NewGuid(), "no");
        pa.ClearDomainEvents();

        var ok = pa.TryApprove(Guid.NewGuid(), null);

        ok.Should().BeFalse();
        pa.Status.Should().Be(PendingApprovalStatus.Denied);
        pa.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void TryDeny_Requires_Reason()
    {
        var pa = Make();
        var act = () => pa.TryDeny(Guid.NewGuid(), "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryExpire_Transitions_And_Raises_Event()
    {
        var pa = Make();
        pa.ClearDomainEvents();
        var ok = pa.TryExpire();
        ok.Should().BeTrue();
        pa.Status.Should().Be(PendingApprovalStatus.Expired);
        pa.DomainEvents.Should().ContainSingle(e => e is AgentApprovalExpiredEvent);
    }
}
```

- [ ] **Step 5: Build, test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiPendingApprovalTests"
git add -p && git commit -m "feat(ai): 5d-2 — AiPendingApproval entity + lifecycle events"
```

---

### Task B4: `AiAssistant.SafetyPresetOverride` column + `SetSafetyPreset` method

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiAssistant.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiAssistantConfiguration.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Moderation/AiAssistantSafetyOverrideTests.cs`

- [ ] **Step 1: Add the property + method to `AiAssistant`**

After the existing `RequestsPerMinute` property:

```csharp
public SafetyPreset? SafetyPresetOverride { get; private set; }
```

Add `using Starter.Abstractions.Ai;` if it isn't already imported (it is — line 5).

Add a method (alongside `SetBudget`):

```csharp
public void SetSafetyPreset(SafetyPreset? preset)
{
    SafetyPresetOverride = preset;
    ModifiedAt = DateTime.UtcNow;
    if (TenantId is { } tenantId)
        RaiseDomainEvent(new Domain.Events.AssistantUpdatedEvent(tenantId, Id));
}
```

- [ ] **Step 2: Add the column mapping to `AiAssistantConfiguration`**

After `builder.Property(e => e.RequestsPerMinute)`:

```csharp
builder.Property(e => e.SafetyPresetOverride)
    .HasColumnName("safety_preset_override")
    .HasConversion<int?>();
```

- [ ] **Step 3: Write the test**

```csharp
using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Events;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class AiAssistantSafetyOverrideTests
{
    private static AiAssistant Make() =>
        AiAssistant.Create(
            tenantId: Guid.NewGuid(),
            name: "Tutor",
            description: null,
            systemPrompt: "you are a tutor",
            createdByUserId: Guid.NewGuid());

    [Fact]
    public void SetSafetyPreset_Persists_Value_And_Raises_Updated()
    {
        var a = Make();
        a.ClearDomainEvents();

        a.SetSafetyPreset(SafetyPreset.ChildSafe);

        a.SafetyPresetOverride.Should().Be(SafetyPreset.ChildSafe);
        a.DomainEvents.Should().ContainSingle(e => e is AssistantUpdatedEvent);
    }

    [Fact]
    public void SetSafetyPreset_Null_Clears_Override()
    {
        var a = Make();
        a.SetSafetyPreset(SafetyPreset.ChildSafe);
        a.SetSafetyPreset(null);

        a.SafetyPresetOverride.Should().BeNull();
    }
}
```

- [ ] **Step 4: Build, test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiAssistantSafetyOverrideTests"
git add -p && git commit -m "feat(ai): 5d-2 — AiAssistant.SafetyPresetOverride column + SetSafetyPreset"
```

---

### Task B5: `AiPersona.Update` raises `PersonaUpdatedEvent`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Events/PersonaUpdatedEvent.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiPersona.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Moderation/AiPersonaUpdatedEventTests.cs`

- [ ] **Step 1: Create the event**

```csharp
using MediatR;
using Starter.Abstractions.Ai;

namespace Starter.Module.AI.Domain.Events;

public sealed record PersonaUpdatedEvent(
    Guid? TenantId,
    Guid PersonaId,
    string Slug,
    SafetyPreset SafetyPreset) : INotification;
```

- [ ] **Step 2: Raise from `AiPersona.Update`**

At the end of the existing `Update(...)` method (after the `_permittedAgentSlugs` line), add:

```csharp
RaiseDomainEvent(new Domain.Events.PersonaUpdatedEvent(TenantId, Id, Slug, safetyPreset));
```

- [ ] **Step 3: Write test**

```csharp
using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Events;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class AiPersonaUpdatedEventTests
{
    [Fact]
    public void Update_Raises_PersonaUpdatedEvent()
    {
        var p = AiPersona.Create(
            tenantId: Guid.NewGuid(),
            slug: "teacher",
            displayName: "Teacher",
            description: null,
            audienceType: PersonaAudienceType.Internal,
            safetyPreset: SafetyPreset.Standard,
            createdByUserId: Guid.NewGuid());
        p.ClearDomainEvents();

        p.Update("Teacher", null, SafetyPreset.ChildSafe, null, isActive: true);

        p.DomainEvents.Should().ContainSingle(e => e is PersonaUpdatedEvent);
    }
}
```

- [ ] **Step 4: Build, test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiPersonaUpdatedEventTests"
git add -p && git commit -m "feat(ai): 5d-2 — AiPersona.Update raises PersonaUpdatedEvent"
```

---

### Task B6: `AiDbContext` DbSets + apply EF configurations + seed call

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Persistence/AiDbContext.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs` (`SeedDataAsync` add `SafetyPresetProfileSeed.SeedAsync`)
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Moderation/AiDbContextSetsTests.cs`

- [ ] **Step 1: Add the three new DbSets to `AiDbContext`**

In the property section near the existing `AiAssistants`, `AiUsageLogs`:

```csharp
public DbSet<AiSafetyPresetProfile> AiSafetyPresetProfiles => Set<AiSafetyPresetProfile>();
public DbSet<AiModerationEvent> AiModerationEvents => Set<AiModerationEvent>();
public DbSet<AiPendingApproval> AiPendingApprovals => Set<AiPendingApproval>();
```

Add `using Starter.Module.AI.Domain.Entities;` if not already present.

- [ ] **Step 2: Verify configurations are applied**

`AiDbContext.OnModelCreating` typically calls `modelBuilder.ApplyConfigurationsFromAssembly(typeof(AiDbContext).Assembly)`. Inspect the file — if it does, the three new `*Configuration` classes are picked up automatically. If it explicitly registers each `ApplyConfiguration<>(new ...)`, add three lines.

- [ ] **Step 3: Wire `SafetyPresetProfileSeed.SeedAsync` into `AIModule.SeedDataAsync`**

After `await ModelPricingSeed.SeedAsync(aiDb, cancellationToken);` add:

```csharp
await SafetyPresetProfileSeed.SeedAsync(aiDb, cancellationToken);
```

(`using Starter.Module.AI.Infrastructure.Persistence.Seed;` is already imported by `ModelPricingSeed`.)

- [ ] **Step 4: Smoke test — DbSets reachable**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class AiDbContextSetsTests
{
    [Fact]
    public void Three_New_DbSets_Are_Available()
    {
        var cu = new Mock<ICurrentUserService>();
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"sets-{Guid.NewGuid()}").Options;
        using var db = new AiDbContext(opts, cu.Object);

        db.AiSafetyPresetProfiles.Should().NotBeNull();
        db.AiModerationEvents.Should().NotBeNull();
        db.AiPendingApprovals.Should().NotBeNull();
    }
}
```

- [ ] **Step 5: Build, test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AiDbContextSetsTests"
git add -p && git commit -m "feat(ai): 5d-2 — DbContext DbSets + seed wiring"
```

---

### Task B7: Generate + verify migration locally (do not commit)

**Files:** none committed.

- [ ] **Step 1: Generate the migration**

```bash
cd boilerplateBE
dotnet ef migrations add Plan5d2_SafetyAndApprovals \
    --project src/modules/Starter.Module.AI \
    --startup-project src/Starter.Api \
    --context AiDbContext \
    --no-build
```

- [ ] **Step 2: Inspect the generated migration**

Verify it creates: `ai_safety_preset_profiles`, `ai_moderation_events`, `ai_pending_approvals`, plus the `safety_preset_override` column on `ai_assistants`. Check that all jsonb columns + indexes match the configurations.

- [ ] **Step 3: Apply locally to confirm DDL is valid**

```bash
dotnet ef database update \
    --project src/modules/Starter.Module.AI \
    --startup-project src/Starter.Api \
    --context AiDbContext
```

- [ ] **Step 4: Roll back the migration locally**

```bash
dotnet ef migrations remove \
    --project src/modules/Starter.Module.AI \
    --startup-project src/Starter.Api \
    --context AiDbContext
```

> **Why no commit:** per CLAUDE.md `# No migrations in boilerplate` — consuming apps generate their own migrations. We only generate locally to verify the EF model maps cleanly.

---

## Phase C — Application services

### Task C1: `IContentModerator` interface + `ModerationVerdict` record + `NoOpContentModerator`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Moderation/IContentModerator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Moderation/ModerationVerdict.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Moderation/ResolvedSafetyProfile.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Moderation/NoOpContentModerator.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Moderation/NoOpContentModeratorTests.cs`

- [ ] **Step 1: Create `ResolvedSafetyProfile` record**

```csharp
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Moderation;

internal sealed record ResolvedSafetyProfile(
    SafetyPreset Preset,
    ModerationProvider Provider,
    IReadOnlyDictionary<string, double> CategoryThresholds,
    IReadOnlyList<string> BlockedCategories,
    ModerationFailureMode FailureMode,
    bool RedactPii);
```

- [ ] **Step 2: Create `ModerationVerdict` record**

```csharp
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Moderation;

internal sealed record ModerationVerdict(
    ModerationOutcome Outcome,
    IReadOnlyDictionary<string, double> Categories,
    string? BlockedReason,
    int LatencyMs,
    bool ProviderUnavailable = false)
{
    public static ModerationVerdict Allowed(int latencyMs) =>
        new(ModerationOutcome.Allowed, new Dictionary<string, double>(), null, latencyMs);

    public static ModerationVerdict Blocked(IReadOnlyDictionary<string, double> categories, string reason, int latencyMs) =>
        new(ModerationOutcome.Blocked, categories, reason, latencyMs);

    public static ModerationVerdict Unavailable(int latencyMs) =>
        new(ModerationOutcome.Allowed, new Dictionary<string, double>(), null, latencyMs, ProviderUnavailable: true);
}
```

- [ ] **Step 3: Create `IContentModerator`**

```csharp
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Moderation;

internal interface IContentModerator
{
    /// <summary>
    /// Scans a piece of text for unsafe content per the resolved profile.
    /// Implementations must NOT throw on transient provider errors — return
    /// <see cref="ModerationVerdict.Unavailable"/>; the decorator decides
    /// FailOpen / FailClosed based on the profile.
    /// </summary>
    Task<ModerationVerdict> ScanAsync(
        string text,
        ModerationStage stage,
        ResolvedSafetyProfile profile,
        string? language,
        CancellationToken ct);
}
```

- [ ] **Step 4: Create `NoOpContentModerator`**

```csharp
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Services.Moderation;

/// <summary>
/// Registered when no moderation provider key is configured. Reports as unavailable;
/// the decorator's failure-mode logic determines whether FailOpen (Standard) or
/// FailClosed (ChildSafe / Pro) is enforced.
/// </summary>
internal sealed class NoOpContentModerator : IContentModerator
{
    public Task<ModerationVerdict> ScanAsync(
        string text, ModerationStage stage, ResolvedSafetyProfile profile,
        string? language, CancellationToken ct) =>
        Task.FromResult(ModerationVerdict.Unavailable(0));
}
```

- [ ] **Step 5: Test**

```csharp
using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Services.Moderation;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class NoOpContentModeratorTests
{
    [Fact]
    public async Task Reports_Unavailable()
    {
        var moderator = new NoOpContentModerator();
        var profile = new ResolvedSafetyProfile(
            SafetyPreset.Standard, ModerationProvider.OpenAi,
            new Dictionary<string, double>(), Array.Empty<string>(),
            ModerationFailureMode.FailOpen, false);

        var verdict = await moderator.ScanAsync("hi", ModerationStage.Input, profile, null, default);

        verdict.ProviderUnavailable.Should().BeTrue();
    }
}
```

- [ ] **Step 6: Build, test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~NoOpContentModeratorTests"
git add -p && git commit -m "feat(ai): 5d-2 — IContentModerator interface + NoOp impl + verdict types"
```

---

### Task C2: `OpenAiContentModerator` (real OpenAI SDK call)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Moderation/OpenAiContentModerator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Moderation/ModerationKeyResolver.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Moderation/OpenAiContentModeratorTests.cs` (uses an injected `Func<...>` to avoid live HTTP — wire-compat test is W1 in Phase H)

- [ ] **Step 1: Create the API-key resolver**

```csharp
using Microsoft.Extensions.Configuration;

namespace Starter.Module.AI.Infrastructure.Services.Moderation;

/// <summary>
/// Resolves the OpenAI API key used by the moderation client. Tries the dedicated
/// moderation key first, then falls back to the existing chat-provider key. A null
/// return means no key is configured — caller registers <see cref="NoOpContentModerator"/>.
/// </summary>
internal interface IModerationKeyResolver
{
    string? Resolve();
}

internal sealed class ConfigurationModerationKeyResolver(IConfiguration configuration) : IModerationKeyResolver
{
    public string? Resolve()
    {
        var dedicated = configuration["AI:Moderation:OpenAi:ApiKey"];
        if (!string.IsNullOrWhiteSpace(dedicated)) return dedicated;
        var fallback = configuration["AI:Providers:OpenAI:ApiKey"];
        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }
}
```

- [ ] **Step 2: Create `OpenAiContentModerator`**

```csharp
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Moderations;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Services.Moderation;

/// <summary>
/// Provider-native moderation via OpenAI's Moderations API. Used uniformly regardless
/// of which chat provider the agent runs on. Threshold + always-block-categories come
/// from the resolved profile so per-tenant tuning works without code changes.
/// </summary>
internal sealed class OpenAiContentModerator(
    IModerationKeyResolver keyResolver,
    IHttpClientFactory httpClientFactory,
    ILogger<OpenAiContentModerator> logger) : IContentModerator
{
    private const string DefaultModel = "omni-moderation-latest";

    public async Task<ModerationVerdict> ScanAsync(
        string text, ModerationStage stage, ResolvedSafetyProfile profile,
        string? language, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(text))
            return ModerationVerdict.Allowed(0);

        var apiKey = keyResolver.Resolve();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogDebug("OpenAI moderation key not configured; reporting Unavailable.");
            return ModerationVerdict.Unavailable((int)sw.ElapsedMilliseconds);
        }

        try
        {
            var options = new OpenAIClientOptions
            {
                Transport = new HttpClientPipelineTransport(httpClientFactory.CreateClient(nameof(OpenAiContentModerator)))
            };
            var client = new ModerationClient(DefaultModel, new ApiKeyCredential(apiKey), options);
            var result = await client.ClassifyTextAsync(text, ct).ConfigureAwait(false);

            // OpenAI returns ModerationResult with .Flagged + .Categories (each with .Flagged + .Score).
            // Project to a flat dict<category, score>.
            var moderation = result.Value;
            var scores = ProjectScores(moderation);

            // Always-block category list (e.g., "sexual-minors") trumps thresholds.
            foreach (var blocked in profile.BlockedCategories)
            {
                if (scores.TryGetValue(blocked, out var s) && s > 0.0)
                    return ModerationVerdict.Blocked(scores, $"category:{blocked} (always-block)", (int)sw.ElapsedMilliseconds);
            }

            // Threshold check.
            foreach (var (category, threshold) in profile.CategoryThresholds)
            {
                if (scores.TryGetValue(category, out var s) && s >= threshold)
                    return ModerationVerdict.Blocked(scores, $"category:{category} score:{s:F2}>={threshold:F2}", (int)sw.ElapsedMilliseconds);
            }

            return new ModerationVerdict(ModerationOutcome.Allowed, scores, null, (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OpenAI moderation call failed at stage {Stage}; reporting Unavailable.", stage);
            return ModerationVerdict.Unavailable((int)sw.ElapsedMilliseconds);
        }
    }

    private static IReadOnlyDictionary<string, double> ProjectScores(ModerationResult result)
    {
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in result.Categories)
            scores[category.Name] = category.Score;
        return scores;
    }
}
```

> **OpenAI SDK API check:** the property names (`result.Categories`, `category.Name`, `category.Score`) are the surface in current `OpenAI` NuGet versions. If a build error indicates a renamed property, run `dotnet list package` for the version in `Directory.Packages.props` and align names to that release.

- [ ] **Step 3: Test (substitute the network call via a derived class)**

Easiest deterministic shape: override `ScanAsync` in a test subclass that returns canned scores. We are testing the *threshold + always-block* logic, not the HTTP wire path (W1 covers that).

Refactor: extract score-vs-profile decision into a static helper so it's testable without a live HTTP call.

Add to `OpenAiContentModerator.cs`:

```csharp
internal static ModerationVerdict EvaluateScores(
    IReadOnlyDictionary<string, double> scores,
    ResolvedSafetyProfile profile,
    int latencyMs)
{
    foreach (var blocked in profile.BlockedCategories)
        if (scores.TryGetValue(blocked, out var s) && s > 0.0)
            return ModerationVerdict.Blocked(scores, $"category:{blocked} (always-block)", latencyMs);

    foreach (var (category, threshold) in profile.CategoryThresholds)
        if (scores.TryGetValue(category, out var s) && s >= threshold)
            return ModerationVerdict.Blocked(scores, $"category:{category} score:{s:F2}>={threshold:F2}", latencyMs);

    return new ModerationVerdict(ModerationOutcome.Allowed, scores, null, latencyMs);
}
```

Then update the live call site to delegate: `return EvaluateScores(scores, profile, (int)sw.ElapsedMilliseconds);`.

Test:

```csharp
using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Services.Moderation;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class OpenAiContentModeratorTests
{
    private static ResolvedSafetyProfile ChildSafe() => new(
        SafetyPreset.ChildSafe, ModerationProvider.OpenAi,
        new Dictionary<string, double> { ["sexual"] = 0.5, ["violence"] = 0.5 },
        new[] { "sexual-minors" },
        ModerationFailureMode.FailClosed,
        RedactPii: false);

    [Fact]
    public void EvaluateScores_Blocks_On_Always_Block_Category()
    {
        var scores = new Dictionary<string, double> { ["sexual-minors"] = 0.4, ["sexual"] = 0.1 };
        var v = OpenAiContentModerator.EvaluateScores(scores, ChildSafe(), 10);
        v.Outcome.Should().Be(ModerationOutcome.Blocked);
        v.BlockedReason.Should().Contain("sexual-minors");
    }

    [Fact]
    public void EvaluateScores_Blocks_When_Threshold_Met()
    {
        var scores = new Dictionary<string, double> { ["sexual"] = 0.55, ["violence"] = 0.0 };
        var v = OpenAiContentModerator.EvaluateScores(scores, ChildSafe(), 10);
        v.Outcome.Should().Be(ModerationOutcome.Blocked);
        v.BlockedReason.Should().Contain("sexual");
    }

    [Fact]
    public void EvaluateScores_Allows_When_All_Below_Threshold()
    {
        var scores = new Dictionary<string, double> { ["sexual"] = 0.1, ["violence"] = 0.0 };
        var v = OpenAiContentModerator.EvaluateScores(scores, ChildSafe(), 10);
        v.Outcome.Should().Be(ModerationOutcome.Allowed);
    }
}
```

- [ ] **Step 4: Build, test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~OpenAiContentModeratorTests"
git add -p && git commit -m "feat(ai): 5d-2 — OpenAiContentModerator + key resolver + threshold evaluator"
```

---

### Task C3: `IPiiRedactor` + `RegexPiiRedactor`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Moderation/IPiiRedactor.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Moderation/RedactionResult.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Moderation/RegexPiiRedactor.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Moderation/RegexPiiRedactorTests.cs`

- [ ] **Step 1: Create `RedactionResult`**

```csharp
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Moderation;

internal sealed record RedactionResult(
    ModerationOutcome Outcome,
    string Text,
    IReadOnlyDictionary<string, int> Hits,
    bool Failed = false);
```

- [ ] **Step 2: Create `IPiiRedactor`**

```csharp
namespace Starter.Module.AI.Application.Services.Moderation;

internal interface IPiiRedactor
{
    Task<RedactionResult> RedactAsync(string text, ResolvedSafetyProfile profile, CancellationToken ct);
}
```

- [ ] **Step 3: Create `RegexPiiRedactor`**

```csharp
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Services.Moderation;

internal sealed partial class RegexPiiRedactor(ILogger<RegexPiiRedactor> logger) : IPiiRedactor
{
    private const string Token = "[REDACTED]";

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\+?[1-9]\d{1,14}", RegexOptions.Compiled)]
    private static partial Regex E164PhoneRegex();

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex SsnRegex();

    [GeneratedRegex(@"\b(?:\d[ -]?){13,19}\b", RegexOptions.Compiled)]
    private static partial Regex CardCandidateRegex();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}[A-Z0-9]{1,30}\b", RegexOptions.Compiled)]
    private static partial Regex IbanRegex();

    public Task<RedactionResult> RedactAsync(string text, ResolvedSafetyProfile profile, CancellationToken ct)
    {
        if (!profile.RedactPii || string.IsNullOrEmpty(text))
            return Task.FromResult(new RedactionResult(ModerationOutcome.Allowed, text, new Dictionary<string, int>()));

        try
        {
            var hits = new Dictionary<string, int>();
            var working = text;

            working = ReplaceWithCount(working, EmailRegex(), "pii-email", hits);
            working = ReplaceWithCount(working, SsnRegex(), "pii-ssn", hits);
            working = ReplaceWithCount(working, IbanRegex(), "pii-iban", hits);
            working = ReplaceCards(working, hits);
            // Phone last so it doesn't mis-match digit runs that were card numbers
            working = ReplaceWithCount(working, E164PhoneRegex(), "pii-phone", hits);

            var outcome = hits.Count > 0 ? ModerationOutcome.Redacted : ModerationOutcome.Allowed;
            return Task.FromResult(new RedactionResult(outcome, working, hits));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PII redactor threw; returning text unmodified.");
            return Task.FromResult(new RedactionResult(
                ModerationOutcome.Allowed, text, new Dictionary<string, int>(), Failed: true));
        }
    }

    private static string ReplaceWithCount(string input, Regex pattern, string label, Dictionary<string, int> hits)
    {
        var matches = pattern.Matches(input);
        if (matches.Count == 0) return input;
        hits[label] = (hits.TryGetValue(label, out var n) ? n : 0) + matches.Count;
        return pattern.Replace(input, Token);
    }

    private static string ReplaceCards(string input, Dictionary<string, int> hits)
    {
        return CardCandidateRegex().Replace(input, m =>
        {
            var digits = new string(m.Value.Where(char.IsDigit).ToArray());
            if (digits.Length is < 13 or > 19) return m.Value;
            if (!LuhnValid(digits)) return m.Value;
            hits["pii-card"] = (hits.TryGetValue("pii-card", out var n) ? n : 0) + 1;
            return Token;
        });
    }

    private static bool LuhnValid(string digits)
    {
        var sum = 0;
        var alt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (alt) { d *= 2; if (d > 9) d -= 9; }
            sum += d;
            alt = !alt;
        }
        return sum % 10 == 0;
    }
}
```

- [ ] **Step 4: Tests**

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Services.Moderation;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class RegexPiiRedactorTests
{
    private static ResolvedSafetyProfile Profile(bool redact) => new(
        SafetyPreset.ProfessionalModerated, ModerationProvider.OpenAi,
        new Dictionary<string, double>(), Array.Empty<string>(),
        ModerationFailureMode.FailClosed, redact);

    private static IPiiRedactor Make() => new RegexPiiRedactor(NullLogger<RegexPiiRedactor>.Instance);

    [Fact]
    public async Task NoOp_When_Profile_Disables_Redaction()
    {
        var r = await Make().RedactAsync("contact me at a@b.com", Profile(redact: false), default);
        r.Outcome.Should().Be(ModerationOutcome.Allowed);
        r.Text.Should().Contain("a@b.com");
    }

    [Fact]
    public async Task Redacts_Email_Phone_And_Reports_Hits()
    {
        var input = "email a@b.com call +14155552671";
        var r = await Make().RedactAsync(input, Profile(redact: true), default);
        r.Outcome.Should().Be(ModerationOutcome.Redacted);
        r.Text.Should().NotContain("a@b.com");
        r.Text.Should().NotContain("+14155552671");
        r.Hits.Should().ContainKey("pii-email");
        r.Hits.Should().ContainKey("pii-phone");
    }

    [Fact]
    public async Task Card_Number_Redacted_Only_When_Luhn_Valid()
    {
        // Visa test number "4111 1111 1111 1111" — Luhn valid.
        var goodInput = "card 4111 1111 1111 1111";
        var bad = "card 4111 1111 1111 1112"; // not Luhn valid

        var rGood = await Make().RedactAsync(goodInput, Profile(redact: true), default);
        var rBad = await Make().RedactAsync(bad, Profile(redact: true), default);

        rGood.Hits.Should().ContainKey("pii-card");
        rBad.Hits.Should().NotContainKey("pii-card");
    }

    [Fact]
    public async Task SSN_Format_Redacted()
    {
        var r = await Make().RedactAsync("ssn 123-45-6789", Profile(redact: true), default);
        r.Hits.Should().ContainKey("pii-ssn");
        r.Text.Should().NotContain("123-45-6789");
    }

    [Fact]
    public async Task Iban_Redacted()
    {
        var r = await Make().RedactAsync("iban GB29NWBK60161331926819", Profile(redact: true), default);
        r.Hits.Should().ContainKey("pii-iban");
    }
}
```

- [ ] **Step 5: Build, test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~RegexPiiRedactorTests"
git add -p && git commit -m "feat(ai): 5d-2 — IPiiRedactor + regex impl with Luhn-validated card detection"
```

---

### Task C4: `ISafetyProfileResolver` + cached impl + cache-invalidation handlers

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Moderation/ISafetyProfileResolver.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Moderation/SafetyProfileResolver.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/EventHandlers/InvalidateSafetyProfileCacheOnAssistantUpdate.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/EventHandlers/InvalidateSafetyProfileCacheOnPersonaUpdate.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/EventHandlers/InvalidateSafetyProfileCacheOnProfileUpdate.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Moderation/SafetyProfileResolverTests.cs`

- [ ] **Step 1: Create the interface**

```csharp
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Moderation;

internal interface ISafetyProfileResolver
{
    /// <summary>
    /// Resolves the active safety profile for an agent run. Override precedence:
    /// assistant.SafetyPresetOverride > persona.SafetyPreset > Standard. Threshold profile
    /// precedence: tenant row > platform row > hard-coded fallback.
    /// </summary>
    Task<ResolvedSafetyProfile> ResolveAsync(
        Guid? tenantId,
        AiAssistant assistant,
        SafetyPreset? personaPreset,
        ModerationProvider provider,
        CancellationToken ct);

    Task InvalidateAsync(Guid? tenantId, CancellationToken ct);
}
```

- [ ] **Step 2: Create the impl**

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Services.Moderation;

internal sealed class SafetyProfileResolver(
    AiDbContext db,
    ICacheService cache) : ISafetyProfileResolver
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public async Task<ResolvedSafetyProfile> ResolveAsync(
        Guid? tenantId,
        AiAssistant assistant,
        SafetyPreset? personaPreset,
        ModerationProvider provider,
        CancellationToken ct)
    {
        var preset = assistant.SafetyPresetOverride ?? personaPreset ?? SafetyPreset.Standard;
        var key = $"safety:profile:{tenantId?.ToString() ?? "platform"}:{preset}:{provider}";

        var cached = await cache.GetAsync<CachedProfile>(key, ct);
        if (cached is not null)
            return cached.ToResolved(preset, provider);

        var resolved = await LoadFromDbAsync(tenantId, preset, provider, ct);
        await cache.SetAsync(key, CachedProfile.From(resolved), Ttl, ct);
        return resolved;
    }

    public async Task InvalidateAsync(Guid? tenantId, CancellationToken ct)
    {
        var prefix = $"safety:profile:{tenantId?.ToString() ?? "platform"}:";
        await cache.RemoveByPrefixAsync(prefix, ct);
    }

    private async Task<ResolvedSafetyProfile> LoadFromDbAsync(
        Guid? tenantId, SafetyPreset preset, ModerationProvider provider, CancellationToken ct)
    {
        // Tenant override → platform default → hard-coded fallback.
        AiSafetyPresetProfile? row = null;
        if (tenantId is { } tid)
        {
            row = await db.AiSafetyPresetProfiles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.TenantId == tid && p.Preset == preset && p.Provider == provider && p.IsActive, ct);
        }
        row ??= await db.AiSafetyPresetProfiles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.TenantId == null && p.Preset == preset && p.Provider == provider && p.IsActive, ct);

        if (row is null) return Fallback(preset, provider);

        var thresholds = JsonSerializer.Deserialize<Dictionary<string, double>>(row.CategoryThresholdsJson)
                         ?? new Dictionary<string, double>();
        var blocked = JsonSerializer.Deserialize<List<string>>(row.BlockedCategoriesJson) ?? new List<string>();
        return new ResolvedSafetyProfile(preset, provider, thresholds, blocked, row.FailureMode, row.RedactPii);
    }

    private static ResolvedSafetyProfile Fallback(SafetyPreset preset, ModerationProvider provider) =>
        preset switch
        {
            SafetyPreset.ChildSafe => new(
                preset, provider,
                new Dictionary<string, double> { ["sexual"] = 0.5, ["hate"] = 0.5, ["violence"] = 0.5, ["self-harm"] = 0.3, ["harassment"] = 0.5 },
                new[] { "sexual-minors", "violence-graphic" },
                ModerationFailureMode.FailClosed, false),
            SafetyPreset.ProfessionalModerated => new(
                preset, provider,
                new Dictionary<string, double> { ["sexual"] = 0.85, ["hate"] = 0.85, ["violence"] = 0.85, ["self-harm"] = 0.85, ["harassment"] = 0.85 },
                Array.Empty<string>(),
                ModerationFailureMode.FailClosed, true),
            _ => new(
                preset, provider,
                new Dictionary<string, double> { ["sexual"] = 0.85, ["hate"] = 0.85, ["violence"] = 0.85, ["self-harm"] = 0.85, ["harassment"] = 0.85 },
                Array.Empty<string>(),
                ModerationFailureMode.FailOpen, false)
        };

    private sealed record CachedProfile(
        Dictionary<string, double> Thresholds,
        List<string> BlockedCategories,
        ModerationFailureMode FailureMode,
        bool RedactPii)
    {
        public static CachedProfile From(ResolvedSafetyProfile p) => new(
            new Dictionary<string, double>(p.CategoryThresholds),
            p.BlockedCategories.ToList(), p.FailureMode, p.RedactPii);

        public ResolvedSafetyProfile ToResolved(SafetyPreset preset, ModerationProvider provider) =>
            new(preset, provider, Thresholds, BlockedCategories, FailureMode, RedactPii);
    }
}
```

- [ ] **Step 3: Cache-invalidation handlers**

```csharp
// InvalidateSafetyProfileCacheOnAssistantUpdate.cs
using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Events;

namespace Starter.Module.AI.Application.EventHandlers;

internal sealed class InvalidateSafetyProfileCacheOnAssistantUpdate(
    ISafetyProfileResolver resolver,
    ILogger<InvalidateSafetyProfileCacheOnAssistantUpdate> logger)
    : INotificationHandler<AssistantUpdatedEvent>
{
    public async Task Handle(AssistantUpdatedEvent notification, CancellationToken ct)
    {
        await resolver.InvalidateAsync(notification.TenantId, ct);
        logger.LogDebug("Invalidated safety-profile cache for tenant {TenantId} after AssistantUpdatedEvent.", notification.TenantId);
    }
}
```

```csharp
// InvalidateSafetyProfileCacheOnPersonaUpdate.cs
using MediatR;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Events;

namespace Starter.Module.AI.Application.EventHandlers;

internal sealed class InvalidateSafetyProfileCacheOnPersonaUpdate(
    ISafetyProfileResolver resolver) : INotificationHandler<PersonaUpdatedEvent>
{
    public Task Handle(PersonaUpdatedEvent notification, CancellationToken ct) =>
        resolver.InvalidateAsync(notification.TenantId, ct);
}
```

```csharp
// InvalidateSafetyProfileCacheOnProfileUpdate.cs
using MediatR;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Events;

namespace Starter.Module.AI.Application.EventHandlers;

internal sealed class InvalidateSafetyProfileCacheOnProfileUpdate(
    ISafetyProfileResolver resolver) : INotificationHandler<SafetyPresetProfileUpdatedEvent>
{
    public Task Handle(SafetyPresetProfileUpdatedEvent notification, CancellationToken ct) =>
        resolver.InvalidateAsync(notification.TenantId, ct);
}
```

- [ ] **Step 4: Tests (in-memory cache fake)**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Services.Moderation;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class SafetyProfileResolverTests
{
    private static (AiDbContext db, Mock<ICacheService> cache) Make()
    {
        var cu = new Mock<ICurrentUserService>();
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        return (new AiDbContext(opts, cu.Object), new Mock<ICacheService>());
    }

    private static AiAssistant Assistant(SafetyPreset? overridePreset = null)
    {
        var a = AiAssistant.Create(
            tenantId: Guid.NewGuid(), name: "x", description: null,
            systemPrompt: "x", createdByUserId: Guid.NewGuid());
        if (overridePreset is { } p) a.SetSafetyPreset(p);
        return a;
    }

    [Fact]
    public async Task Override_Preset_Wins_Over_Persona()
    {
        var (db, cache) = Make();
        cache.Setup(c => c.GetAsync<It.IsAnyType>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((object?)null);
        var resolver = new SafetyProfileResolver(db, cache.Object);

        var a = Assistant(SafetyPreset.ChildSafe);
        var resolved = await resolver.ResolveAsync(
            tenantId: a.TenantId, assistant: a,
            personaPreset: SafetyPreset.Standard,
            provider: ModerationProvider.OpenAi, ct: default);

        resolved.Preset.Should().Be(SafetyPreset.ChildSafe);
        resolved.FailureMode.Should().Be(ModerationFailureMode.FailClosed); // ChildSafe fallback default
    }

    [Fact]
    public async Task Tenant_Row_Wins_Over_Platform()
    {
        var (db, cache) = Make();
        cache.Setup(c => c.GetAsync<It.IsAnyType>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((object?)null);
        var tenantId = Guid.NewGuid();
        // platform default
        db.AiSafetyPresetProfiles.Add(AiSafetyPresetProfile.Create(
            null, SafetyPreset.Standard, ModerationProvider.OpenAi,
            """{"sexual":0.85}""", "[]", ModerationFailureMode.FailOpen, false));
        // tenant override
        db.AiSafetyPresetProfiles.Add(AiSafetyPresetProfile.Create(
            tenantId, SafetyPreset.Standard, ModerationProvider.OpenAi,
            """{"sexual":0.5}""", "[]", ModerationFailureMode.FailClosed, false));
        await db.SaveChangesAsync();

        var resolver = new SafetyProfileResolver(db, cache.Object);
        var a = AiAssistant.Create(tenantId, "x", null, "x", Guid.NewGuid());
        var resolved = await resolver.ResolveAsync(tenantId, a, null, ModerationProvider.OpenAi, default);

        resolved.CategoryThresholds["sexual"].Should().Be(0.5);
        resolved.FailureMode.Should().Be(ModerationFailureMode.FailClosed);
    }

    [Fact]
    public async Task Falls_Back_To_Hard_Coded_When_No_Rows()
    {
        var (db, cache) = Make();
        cache.Setup(c => c.GetAsync<It.IsAnyType>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((object?)null);
        var resolver = new SafetyProfileResolver(db, cache.Object);
        var a = AiAssistant.Create(Guid.NewGuid(), "x", null, "x", Guid.NewGuid());

        var resolved = await resolver.ResolveAsync(a.TenantId, a, SafetyPreset.ChildSafe, ModerationProvider.OpenAi, default);

        resolved.BlockedCategories.Should().Contain("sexual-minors");
        resolved.FailureMode.Should().Be(ModerationFailureMode.FailClosed);
    }
}
```

- [ ] **Step 5: Build, test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~SafetyProfileResolverTests"
git add -p && git commit -m "feat(ai): 5d-2 — ISafetyProfileResolver with 60s cache + 3 invalidation handlers"
```

---

### Task C5: `IModerationRefusalProvider` (RESX-backed)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Moderation/IModerationRefusalProvider.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Services/Moderation/ResxModerationRefusalProvider.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Moderation/ResxModerationRefusalProviderTests.cs`

- [ ] **Step 1: Interface**

```csharp
using System.Globalization;
using Starter.Abstractions.Ai;

namespace Starter.Module.AI.Application.Services.Moderation;

internal interface IModerationRefusalProvider
{
    string GetRefusal(SafetyPreset preset, PersonaAudienceType audience, CultureInfo culture);
    string GetProviderUnavailable(SafetyPreset preset, CultureInfo culture);
}
```

- [ ] **Step 2: Impl**

```csharp
using System.Globalization;
using System.Resources;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Services.Moderation;

namespace Starter.Module.AI.Infrastructure.Services.Moderation;

internal sealed class ResxModerationRefusalProvider : IModerationRefusalProvider
{
    private static readonly ResourceManager Manager = new(
        "Starter.Module.AI.Resources.ModerationRefusalTemplates",
        typeof(ResxModerationRefusalProvider).Assembly);

    public string GetRefusal(SafetyPreset preset, PersonaAudienceType audience, CultureInfo culture)
    {
        var key = $"{preset}.{audience}";
        return Lookup(key, culture);
    }

    public string GetProviderUnavailable(SafetyPreset preset, CultureInfo culture)
    {
        var key = $"{preset}.ProviderUnavailable";
        return Lookup(key, culture);
    }

    private static string Lookup(string key, CultureInfo culture)
    {
        var localised = Manager.GetString(key, culture);
        if (!string.IsNullOrEmpty(localised)) return localised;
        var fallback = Manager.GetString(key, CultureInfo.InvariantCulture);
        return fallback ?? "Request not allowed.";
    }
}
```

- [ ] **Step 3: Tests**

```csharp
using System.Globalization;
using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Infrastructure.Services.Moderation;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class ResxModerationRefusalProviderTests
{
    [Theory]
    [InlineData(SafetyPreset.ChildSafe, PersonaAudienceType.Internal, "en")]
    [InlineData(SafetyPreset.ChildSafe, PersonaAudienceType.Internal, "ar")]
    [InlineData(SafetyPreset.ProfessionalModerated, PersonaAudienceType.EndCustomer, "en")]
    public void Returns_Non_Empty_Refusal(SafetyPreset preset, PersonaAudienceType audience, string culture)
    {
        var p = new ResxModerationRefusalProvider();
        p.GetRefusal(preset, audience, new CultureInfo(culture)).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Provider_Unavailable_Resolves_For_Each_Preset()
    {
        var p = new ResxModerationRefusalProvider();
        foreach (var preset in Enum.GetValues<SafetyPreset>())
            p.GetProviderUnavailable(preset, CultureInfo.InvariantCulture).Should().NotBeNullOrWhiteSpace();
    }
}
```

- [ ] **Step 4: Build, test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~ResxModerationRefusalProviderTests"
git add -p && git commit -m "feat(ai): 5d-2 — IModerationRefusalProvider RESX-backed impl"
```

---

### Task C6: `IPendingApprovalService` orchestrator

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Approvals/IPendingApprovalService.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Approvals/PendingApprovalService.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Approvals/PendingApprovalServiceTests.cs`

- [ ] **Step 1: Interface**

```csharp
using Starter.Module.AI.Domain.Entities;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Services.Approvals;

internal interface IPendingApprovalService
{
    Task<AiPendingApproval> CreateAsync(
        Guid? tenantId,
        Guid assistantId,
        string assistantName,
        Guid agentPrincipalId,
        Guid? conversationId,
        Guid? agentTaskId,
        Guid? requestingUserId,
        string toolName,
        string commandTypeName,
        string argumentsJson,
        string? reasonHint,
        TimeSpan expiresIn,
        CancellationToken ct);

    Task<Result<AiPendingApproval>> ApproveAsync(Guid approvalId, Guid decisionUserId, string? reason, CancellationToken ct);
    Task<Result<AiPendingApproval>> DenyAsync(Guid approvalId, Guid decisionUserId, string reason, CancellationToken ct);
    Task<int> ExpireDueAsync(int batchSize, CancellationToken ct);
}
```

- [ ] **Step 2: Impl**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services.Approvals;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Services.Approvals;

internal sealed class PendingApprovalService(
    AiDbContext db,
    ILogger<PendingApprovalService> logger) : IPendingApprovalService
{
    public async Task<AiPendingApproval> CreateAsync(
        Guid? tenantId, Guid assistantId, string assistantName, Guid agentPrincipalId,
        Guid? conversationId, Guid? agentTaskId, Guid? requestingUserId,
        string toolName, string commandTypeName, string argumentsJson,
        string? reasonHint, TimeSpan expiresIn, CancellationToken ct)
    {
        var entity = AiPendingApproval.Create(
            tenantId: tenantId,
            assistantId: assistantId,
            assistantName: assistantName,
            agentPrincipalId: agentPrincipalId,
            conversationId: conversationId,
            agentTaskId: agentTaskId,
            requestingUserId: requestingUserId,
            toolName: toolName,
            commandTypeName: commandTypeName,
            argumentsJson: argumentsJson,
            reasonHint: reasonHint,
            expiresAt: DateTime.UtcNow.Add(expiresIn));

        db.AiPendingApprovals.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<Result<AiPendingApproval>> ApproveAsync(
        Guid approvalId, Guid decisionUserId, string? reason, CancellationToken ct)
    {
        var entity = await db.AiPendingApprovals.FirstOrDefaultAsync(p => p.Id == approvalId, ct);
        if (entity is null)
            return Result.Failure<AiPendingApproval>(PendingApprovalErrors.NotFound(approvalId));
        if (!entity.TryApprove(decisionUserId, reason))
            return Result.Failure<AiPendingApproval>(PendingApprovalErrors.NotPending);

        await db.SaveChangesAsync(ct);
        return Result.Success(entity);
    }

    public async Task<Result<AiPendingApproval>> DenyAsync(
        Guid approvalId, Guid decisionUserId, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure<AiPendingApproval>(PendingApprovalErrors.DenyReasonRequired);

        var entity = await db.AiPendingApprovals.FirstOrDefaultAsync(p => p.Id == approvalId, ct);
        if (entity is null)
            return Result.Failure<AiPendingApproval>(PendingApprovalErrors.NotFound(approvalId));
        if (!entity.TryDeny(decisionUserId, reason))
            return Result.Failure<AiPendingApproval>(PendingApprovalErrors.NotPending);

        await db.SaveChangesAsync(ct);
        return Result.Success(entity);
    }

    public async Task<int> ExpireDueAsync(int batchSize, CancellationToken ct)
    {
        // Atomic claim with FOR UPDATE SKIP LOCKED — multi-replica safe by construction.
        // We hydrate the matching entities (limit batchSize), call TryExpire to raise events,
        // then SaveChanges. Rows that another replica already grabbed are skipped.
        var due = await db.AiPendingApprovals
            .FromSqlRaw(
                """
                SELECT * FROM ai_pending_approvals
                WHERE status = 0 AND expires_at < now()
                ORDER BY expires_at ASC
                LIMIT {0}
                FOR UPDATE SKIP LOCKED
                """, batchSize)
            .ToListAsync(ct);

        if (due.Count == 0) return 0;

        var expired = 0;
        foreach (var row in due)
            if (row.TryExpire()) expired++;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Expired {Count} pending approvals.", expired);
        return expired;
    }
}
```

> **Note on SKIP LOCKED + EF Core:** `FromSqlRaw` returns tracked entities; the `FOR UPDATE SKIP LOCKED` row lock is held until the transaction completes. EF's default behaviour (no explicit `BeginTransaction`) wraps `SaveChangesAsync` in a transaction that commits/rolls back together. For Postgres this is the documented `SELECT ... FOR UPDATE SKIP LOCKED` pattern.

- [ ] **Step 3: Tests**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Approvals;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Approvals;

public sealed class PendingApprovalServiceTests
{
    private static (AiDbContext db, IPendingApprovalService svc) Make()
    {
        var cu = new Mock<ICurrentUserService>();
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        return (db, new PendingApprovalService(db, NullLogger<PendingApprovalService>.Instance));
    }

    private static AiAssistant MakeAssistant() =>
        AiAssistant.Create(Guid.NewGuid(), "Tutor", null, "p", Guid.NewGuid());

    [Fact]
    public async Task Create_Persists_Pending_Row()
    {
        var (db, svc) = Make();
        var a = MakeAssistant();
        var entity = await svc.CreateAsync(
            a.TenantId, a.Id, a.Name, Guid.NewGuid(),
            conversationId: Guid.NewGuid(), agentTaskId: null, requestingUserId: Guid.NewGuid(),
            toolName: "DeleteAllUsers",
            commandTypeName: "X.Y, X",
            argumentsJson: "{}",
            reasonHint: null,
            expiresIn: TimeSpan.FromHours(24),
            ct: default);

        entity.Status.Should().Be(PendingApprovalStatus.Pending);
        var loaded = await db.AiPendingApprovals.FirstAsync();
        loaded.Id.Should().Be(entity.Id);
    }

    [Fact]
    public async Task Approve_Of_Already_Denied_Returns_Failure()
    {
        var (db, svc) = Make();
        var a = MakeAssistant();
        var pa = await svc.CreateAsync(
            a.TenantId, a.Id, a.Name, Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(),
            "T", "X.Y, X", "{}", null, TimeSpan.FromHours(1), default);
        await svc.DenyAsync(pa.Id, Guid.NewGuid(), "no", default);

        var result = await svc.ApproveAsync(pa.Id, Guid.NewGuid(), null, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PendingApproval.NotPending");
    }

    [Fact]
    public async Task Deny_Without_Reason_Returns_Failure()
    {
        var (db, svc) = Make();
        var a = MakeAssistant();
        var pa = await svc.CreateAsync(
            a.TenantId, a.Id, a.Name, Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(),
            "T", "X.Y, X", "{}", null, TimeSpan.FromHours(1), default);

        var result = await svc.DenyAsync(pa.Id, Guid.NewGuid(), "", default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PendingApproval.DenyReasonRequired");
    }
}
```

> **ExpireDueAsync** uses `FromSqlRaw` with Postgres `FOR UPDATE SKIP LOCKED` — that path can't run on the in-memory provider. It is exercised by the Postgres-backed acid test in **Task H6** (M5 alternative path) and the dedicated background-job test in **Task G2**. Don't add an `ExpireDueAsync` test here.

- [ ] **Step 4: Build, test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~PendingApprovalServiceTests"
git add -p && git commit -m "feat(ai): 5d-2 — IPendingApprovalService orchestrator + atomic ExpireDueAsync"
```

---

## Phase D — Runtime + dispatcher integration

### Task D1: `BufferingSink` + `PassthroughSink` wrappers

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Runtime/Moderation/BufferingSink.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Runtime/Moderation/PassthroughSink.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Moderation/SinkWrappersTests.cs`

- [ ] **Step 1: Create `PassthroughSink`**

```csharp
using Starter.Module.AI.Application.Services.Runtime;

namespace Starter.Module.AI.Infrastructure.Runtime.Moderation;

/// <summary>
/// Forwards every event to the inner sink immediately. Used for Standard preset where
/// the moderator's final-pass scan happens after the run completes, but deltas stream live.
/// </summary>
internal sealed class PassthroughSink(IAgentRunSink inner) : IAgentRunSink
{
    public Task OnStepStartedAsync(int stepIndex, CancellationToken ct) => inner.OnStepStartedAsync(stepIndex, ct);
    public Task OnAssistantMessageAsync(AgentAssistantMessage message, CancellationToken ct) => inner.OnAssistantMessageAsync(message, ct);
    public Task OnToolCallAsync(AgentToolCallEvent call, CancellationToken ct) => inner.OnToolCallAsync(call, ct);
    public Task OnToolResultAsync(AgentToolResultEvent result, CancellationToken ct) => inner.OnToolResultAsync(result, ct);
    public Task OnDeltaAsync(string contentDelta, CancellationToken ct) => inner.OnDeltaAsync(contentDelta, ct);
    public Task OnStepCompletedAsync(AgentStepEvent step, CancellationToken ct) => inner.OnStepCompletedAsync(step, ct);
    public Task OnRunCompletedAsync(AgentRunResult result, CancellationToken ct) => inner.OnRunCompletedAsync(result, ct);
}
```

- [ ] **Step 2: Create `BufferingSink`**

```csharp
using System.Text;
using Starter.Module.AI.Application.Services.Runtime;

namespace Starter.Module.AI.Infrastructure.Runtime.Moderation;

/// <summary>
/// Holds <see cref="OnDeltaAsync"/> and <see cref="OnAssistantMessageAsync"/> events while
/// the run executes; observability events (step start/complete, tool call/result) pass through
/// immediately. After the run completes the moderation decorator inspects
/// <see cref="BufferedContent"/>, decides allow/block/redact, and either calls
/// <see cref="ReleaseAsync"/> with the (possibly redacted) text or skips release on Block.
/// </summary>
internal sealed class BufferingSink(IAgentRunSink inner) : IAgentRunSink
{
    private readonly StringBuilder _buffer = new();
    private AgentAssistantMessage? _heldMessage;

    public string BufferedContent => _buffer.ToString();

    public Task OnStepStartedAsync(int stepIndex, CancellationToken ct) => inner.OnStepStartedAsync(stepIndex, ct);
    public Task OnToolCallAsync(AgentToolCallEvent call, CancellationToken ct) => inner.OnToolCallAsync(call, ct);
    public Task OnToolResultAsync(AgentToolResultEvent result, CancellationToken ct) => inner.OnToolResultAsync(result, ct);
    public Task OnStepCompletedAsync(AgentStepEvent step, CancellationToken ct) => inner.OnStepCompletedAsync(step, ct);

    public Task OnDeltaAsync(string contentDelta, CancellationToken ct)
    {
        _buffer.Append(contentDelta);
        return Task.CompletedTask;
    }

    public Task OnAssistantMessageAsync(AgentAssistantMessage message, CancellationToken ct)
    {
        _heldMessage = message;
        return Task.CompletedTask;
    }

    public Task OnRunCompletedAsync(AgentRunResult result, CancellationToken ct) =>
        inner.OnRunCompletedAsync(result, ct);

    /// <summary>Flush held content (after moderation passes / redaction completes).</summary>
    public async Task ReleaseAsync(string content, CancellationToken ct)
    {
        if (_heldMessage is { } msg)
            await inner.OnAssistantMessageAsync(msg with { Content = content }, ct);
        if (!string.IsNullOrEmpty(content))
            await inner.OnDeltaAsync(content, ct);
    }
}
```

> **`AgentAssistantMessage`** is the existing record from `Starter.Module.AI.Application.Services.Runtime`. If its `Content` property is `init`-only, the `with` expression works; if it's `private set`, change `_heldMessage = message;` to capture the message and reconstruct via the existing factory in the decorator. Inspect the type to confirm.

- [ ] **Step 3: Tests**

```csharp
using FluentAssertions;
using Moq;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Infrastructure.Runtime.Moderation;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class SinkWrappersTests
{
    [Fact]
    public async Task BufferingSink_Holds_Deltas_Until_Release()
    {
        var inner = new Mock<IAgentRunSink>();
        var sink = new BufferingSink(inner.Object);

        await sink.OnDeltaAsync("hello ", default);
        await sink.OnDeltaAsync("world", default);

        inner.Verify(s => s.OnDeltaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        sink.BufferedContent.Should().Be("hello world");

        await sink.ReleaseAsync("hello world", default);
        inner.Verify(s => s.OnDeltaAsync("hello world", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BufferingSink_Forwards_Observability_Events_Live()
    {
        var inner = new Mock<IAgentRunSink>();
        var sink = new BufferingSink(inner.Object);

        await sink.OnStepStartedAsync(0, default);
        inner.Verify(s => s.OnStepStartedAsync(0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PassthroughSink_Forwards_All_Events()
    {
        var inner = new Mock<IAgentRunSink>();
        var sink = new PassthroughSink(inner.Object);

        await sink.OnDeltaAsync("x", default);
        inner.Verify(s => s.OnDeltaAsync("x", It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 4: Build, test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~SinkWrappersTests"
git add -p && git commit -m "feat(ai): 5d-2 — BufferingSink + PassthroughSink wrappers"
```

---

### Task D2: `ContentModerationEnforcingAgentRuntime` decorator

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Runtime/ContentModerationEnforcingAgentRuntime.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Moderation/ContentModerationEnforcingAgentRuntimeTests.cs`

- [ ] **Step 1: Implement the decorator**

```csharp
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Runtime.Moderation;

namespace Starter.Module.AI.Infrastructure.Runtime;

/// <summary>
/// Outermost decorator. Scans user input pre-flight (refuses before delegating to the
/// inner cost-cap layer if blocked), then either streams output through (Standard) or
/// buffers it for a post-run scan (ChildSafe / ProfessionalModerated). Writes one
/// AiModerationEvent per non-Allowed outcome.
/// </summary>
internal sealed class ContentModerationEnforcingAgentRuntime(
    IAiAgentRuntime inner,
    IContentModerator moderator,
    IPiiRedactor redactor,
    ISafetyProfileResolver profileResolver,
    IModerationRefusalProvider refusals,
    AiDbContext db,
    ICurrentUserService currentUser,
    ILogger<ContentModerationEnforcingAgentRuntime> logger) : IAiAgentRuntime
{
    public async Task<AgentRunResult> RunAsync(
        AgentRunContext ctx, IAgentRunSink sink, CancellationToken ct = default)
    {
        if (ctx.AssistantId is not { } assistantId || ctx.TenantId is not { } tenantId)
            return await inner.RunAsync(ctx, sink, ct);

        var assistant = await db.AiAssistants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assistantId, ct);
        if (assistant is null)
            return await inner.RunAsync(ctx, sink, ct);

        var profile = await profileResolver.ResolveAsync(
            tenantId, assistant, ctx.Persona?.Safety,
            ModerationProvider.OpenAi, ct);

        // 1. Input scan (last user message)
        var inputText = ctx.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty;
        var inputVerdict = await moderator.ScanAsync(
            inputText, ModerationStage.Input, profile, ctx.Persona?.Slug, ct);

        if (await HandleUnavailableAsync(inputVerdict, profile, ctx, assistant, ModerationStage.Input, ct) is { } unavailableResult)
            return unavailableResult;

        if (inputVerdict.Outcome == ModerationOutcome.Blocked)
            return await BlockedResultAsync(ctx, assistant, profile, ModerationStage.Input,
                inputVerdict, sink, ct);

        // 2. Choose sink wrapper based on preset
        var bufferingSink = profile.Preset == SafetyPreset.Standard
            ? null
            : new BufferingSink(sink);
        var wrappedSink = bufferingSink ?? (IAgentRunSink)new PassthroughSink(sink);

        var inner_result = await inner.RunAsync(ctx, wrappedSink, ct);
        if (inner_result.Status != AgentRunStatus.Completed)
            return inner_result; // upstream errors / cap / awaiting bypass output scan

        var outputText = bufferingSink?.BufferedContent ?? inner_result.FinalContent ?? string.Empty;

        // 3. Output scan
        var outputVerdict = await moderator.ScanAsync(
            outputText, ModerationStage.Output, profile, ctx.Persona?.Slug, ct);

        if (await HandleUnavailableAsync(outputVerdict, profile, ctx, assistant, ModerationStage.Output, ct) is { } unavailableOut)
            return unavailableOut;

        if (outputVerdict.Outcome == ModerationOutcome.Blocked)
            return await BlockedResultAsync(ctx, assistant, profile, ModerationStage.Output,
                outputVerdict, sink, ct);

        // 4. PII redaction (ProfessionalModerated)
        var redaction = await redactor.RedactAsync(outputText, profile, ct);
        if (redaction.Outcome == ModerationOutcome.Redacted)
            await PersistModerationEventAsync(ctx, assistant, profile, ModerationStage.Output,
                outputVerdict with { Outcome = ModerationOutcome.Redacted, Categories = redaction.Hits.ToDictionary(kv => kv.Key, kv => (double)kv.Value) },
                ct);

        var finalText = redaction.Outcome == ModerationOutcome.Redacted ? redaction.Text : outputText;

        // 5. Release buffered content if we suppressed streaming
        if (bufferingSink is not null)
            await bufferingSink.ReleaseAsync(finalText, ct);

        return inner_result with { FinalContent = finalText };
    }

    private async Task<AgentRunResult?> HandleUnavailableAsync(
        ModerationVerdict verdict, ResolvedSafetyProfile profile, AgentRunContext ctx,
        AiAssistant assistant, ModerationStage stage, CancellationToken ct)
    {
        if (!verdict.ProviderUnavailable) return null;
        if (profile.FailureMode == ModerationFailureMode.FailOpen)
        {
            logger.LogWarning("Moderation provider unavailable; FailOpen on preset {Preset} stage {Stage} — allowing.",
                profile.Preset, stage);
            return null;
        }
        var refusal = refusals.GetProviderUnavailable(profile.Preset, CultureInfo.CurrentUICulture);
        return new AgentRunResult(
            Status: AgentRunStatus.ModerationProviderUnavailable,
            FinalContent: refusal,
            Steps: Array.Empty<AgentStepEvent>(),
            TotalInputTokens: 0,
            TotalOutputTokens: 0,
            TerminationReason: AiModerationErrors.ProviderUnavailable.Description);
    }

    private async Task<AgentRunResult> BlockedResultAsync(
        AgentRunContext ctx, AiAssistant assistant, ResolvedSafetyProfile profile,
        ModerationStage stage, ModerationVerdict verdict, IAgentRunSink sink, CancellationToken ct)
    {
        await PersistModerationEventAsync(ctx, assistant, profile, stage, verdict, ct);

        var audience = ctx.Persona?.Audience ?? Starter.Abstractions.Ai.PersonaAudienceType.Internal;
        var refusal = refusals.GetRefusal(profile.Preset, audience, CultureInfo.CurrentUICulture);
        var status = stage == ModerationStage.Input ? AgentRunStatus.InputBlocked : AgentRunStatus.OutputBlocked;

        return new AgentRunResult(
            Status: status,
            FinalContent: refusal,
            Steps: Array.Empty<AgentStepEvent>(),
            TotalInputTokens: 0,
            TotalOutputTokens: 0,
            TerminationReason: $"moderation: {verdict.BlockedReason ?? "blocked"}");
    }

    private async Task PersistModerationEventAsync(
        AgentRunContext ctx, AiAssistant assistant, ResolvedSafetyProfile profile,
        ModerationStage stage, ModerationVerdict verdict, CancellationToken ct)
    {
        var ev = AiModerationEvent.Create(
            tenantId: assistant.TenantId,
            assistantId: assistant.Id,
            agentPrincipalId: ctx.AgentPrincipalId,
            conversationId: stage == ModerationStage.Input ? null : (Guid?)null, // populated in ChatExecutionService for output
            agentTaskId: null,
            messageId: null,
            stage: stage,
            preset: profile.Preset,
            outcome: verdict.Outcome,
            categoriesJson: JsonSerializer.Serialize(verdict.Categories),
            provider: profile.Provider,
            latencyMs: verdict.LatencyMs,
            blockedReason: verdict.BlockedReason);
        db.AiModerationEvents.Add(ev);
        await db.SaveChangesAsync(ct);
    }
}
```

> **`AgentPrincipalId` on `AgentRunContext`** isn't currently a field — extending the run context is out of scope. The decorator can resolve it via `db.AiAgentPrincipals.Where(p => p.AiAssistantId == assistant.Id)` if needed, or accept null on the moderation event row. v1 leaves it null on auto-persisted moderation events; the chat layer can backfill in `ChatExecutionService` when it has the data.

- [ ] **Step 2: Tests (with `FakeContentModerator` + InMemory DB)**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Runtime;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class ContentModerationEnforcingAgentRuntimeTests
{
    private sealed class FakeRuntime : IAiAgentRuntime
    {
        public AgentRunResult ToReturn { get; set; } = new(
            AgentRunStatus.Completed, "hello world", Array.Empty<AgentStepEvent>(), 1, 1, null);
        public bool Called { get; private set; }
        public Task<AgentRunResult> RunAsync(AgentRunContext ctx, IAgentRunSink sink, CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(ToReturn);
        }
    }

    private sealed class FakeModerator : IContentModerator
    {
        public Func<string, ModerationStage, ResolvedSafetyProfile, ModerationVerdict>? Verdict { get; set; }
        public Task<ModerationVerdict> ScanAsync(string text, ModerationStage stage, ResolvedSafetyProfile profile, string? language, CancellationToken ct) =>
            Task.FromResult(Verdict?.Invoke(text, stage, profile) ?? ModerationVerdict.Allowed(0));
    }

    private sealed class FakeRefusals : IModerationRefusalProvider
    {
        public string GetRefusal(SafetyPreset preset, PersonaAudienceType audience, System.Globalization.CultureInfo culture) => $"refused:{preset}";
        public string GetProviderUnavailable(SafetyPreset preset, System.Globalization.CultureInfo culture) => $"unavailable:{preset}";
    }

    private sealed class FakeRedactor : IPiiRedactor
    {
        public Task<RedactionResult> RedactAsync(string text, ResolvedSafetyProfile profile, CancellationToken ct) =>
            Task.FromResult(new RedactionResult(ModerationOutcome.Allowed, text, new Dictionary<string, int>()));
    }

    private static (AiDbContext db, AiAssistant assistant) Seed(SafetyPreset? overridePreset = null)
    {
        var cu = new Mock<ICurrentUserService>();
        var tenant = Guid.NewGuid();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        var a = AiAssistant.Create(tenant, "Tutor", null, "be safe", Guid.NewGuid());
        if (overridePreset is { } p) a.SetSafetyPreset(p);
        db.AiAssistants.Add(a);
        db.SaveChanges();
        return (db, a);
    }

    private static AgentRunContext Ctx(AiAssistant a, SafetyPreset personaPreset, bool streaming = false)
    {
        var persona = new PersonaContext(
            Slug: "student",
            Audience: PersonaAudienceType.Internal,
            Safety: personaPreset,
            PermittedAgentSlugs: Array.Empty<string>());
        return new AgentRunContext(
            Messages: new[] { new AiChatMessage("user", "tell me a story") },
            SystemPrompt: "be safe",
            ModelConfig: new AgentModelConfig(AiProviderType.OpenAI, "gpt-4o", 0.7, 100),
            Tools: new ToolResolutionResult(new Dictionary<string, ResolvedToolDefinition>(), Array.Empty<AiToolDefinition>()),
            MaxSteps: 1,
            LoopBreak: LoopBreakPolicy.Default,
            Streaming: streaming,
            Persona: persona,
            AssistantId: a.Id,
            TenantId: a.TenantId);
    }

    private static ContentModerationEnforcingAgentRuntime Wire(AiDbContext db, IAiAgentRuntime inner, IContentModerator moderator)
    {
        var profileResolver = new Mock<ISafetyProfileResolver>();
        profileResolver.Setup(r => r.ResolveAsync(It.IsAny<Guid?>(), It.IsAny<AiAssistant>(), It.IsAny<SafetyPreset?>(),
                                                  It.IsAny<ModerationProvider>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync((Guid? t, AiAssistant a, SafetyPreset? p, ModerationProvider mp, CancellationToken c) =>
                           new ResolvedSafetyProfile(
                               a.SafetyPresetOverride ?? p ?? SafetyPreset.Standard,
                               mp,
                               new Dictionary<string, double> { ["sexual"] = 0.5 },
                               new[] { "sexual-minors" },
                               (a.SafetyPresetOverride ?? p ?? SafetyPreset.Standard) == SafetyPreset.Standard ? ModerationFailureMode.FailOpen : ModerationFailureMode.FailClosed,
                               (a.SafetyPresetOverride ?? p ?? SafetyPreset.Standard) == SafetyPreset.ProfessionalModerated));
        return new ContentModerationEnforcingAgentRuntime(
            inner, moderator, new FakeRedactor(), profileResolver.Object,
            new FakeRefusals(), db, new Mock<ICurrentUserService>().Object,
            NullLogger<ContentModerationEnforcingAgentRuntime>.Instance);
    }

    [Fact]
    public async Task Standard_Allowed_Passes_Through()
    {
        var (db, a) = Seed();
        var inner = new FakeRuntime();
        var moderator = new FakeModerator();
        var rt = Wire(db, inner, moderator);

        var sink = new Mock<IAgentRunSink>();
        var result = await rt.RunAsync(Ctx(a, SafetyPreset.Standard), sink.Object, default);

        result.Status.Should().Be(AgentRunStatus.Completed);
        inner.Called.Should().BeTrue();
        result.FinalContent.Should().Be("hello world");
    }

    [Fact]
    public async Task Input_Blocked_Returns_Refusal_And_Skips_Inner()
    {
        var (db, a) = Seed();
        var inner = new FakeRuntime();
        var moderator = new FakeModerator
        {
            Verdict = (text, stage, profile) => stage == ModerationStage.Input
                ? ModerationVerdict.Blocked(new Dictionary<string, double> { ["sexual"] = 0.9 }, "blocked", 5)
                : ModerationVerdict.Allowed(5)
        };
        var rt = Wire(db, inner, moderator);

        var sink = new Mock<IAgentRunSink>();
        var result = await rt.RunAsync(Ctx(a, SafetyPreset.ChildSafe), sink.Object, default);

        result.Status.Should().Be(AgentRunStatus.InputBlocked);
        result.FinalContent.Should().Contain("refused");
        inner.Called.Should().BeFalse();

        var ev = await db.AiModerationEvents.FirstAsync();
        ev.Stage.Should().Be(ModerationStage.Input);
        ev.Outcome.Should().Be(ModerationOutcome.Blocked);
    }

    [Fact]
    public async Task Output_Blocked_For_ChildSafe_Returns_Refusal_And_Persists_Event()
    {
        var (db, a) = Seed();
        a.SetSafetyPreset(SafetyPreset.ChildSafe);
        await db.SaveChangesAsync();

        var inner = new FakeRuntime();
        var moderator = new FakeModerator
        {
            Verdict = (text, stage, profile) => stage == ModerationStage.Output
                ? ModerationVerdict.Blocked(new Dictionary<string, double> { ["sexual-minors"] = 0.9 }, "always-block:sexual-minors", 5)
                : ModerationVerdict.Allowed(5)
        };
        var rt = Wire(db, inner, moderator);

        var sink = new Mock<IAgentRunSink>();
        var result = await rt.RunAsync(Ctx(a, SafetyPreset.Standard /*persona*/), sink.Object, default);

        result.Status.Should().Be(AgentRunStatus.OutputBlocked);
        result.FinalContent.Should().Contain("refused");
        var ev = await db.AiModerationEvents.FirstAsync();
        ev.Stage.Should().Be(ModerationStage.Output);
    }

    [Fact]
    public async Task Provider_Unavailable_FailClosed_Returns_Unavailable_For_ChildSafe()
    {
        var (db, a) = Seed(SafetyPreset.ChildSafe);
        var inner = new FakeRuntime();
        var moderator = new FakeModerator { Verdict = (_, _, _) => ModerationVerdict.Unavailable(0) };
        var rt = Wire(db, inner, moderator);
        var sink = new Mock<IAgentRunSink>();

        var result = await rt.RunAsync(Ctx(a, SafetyPreset.ChildSafe), sink.Object, default);

        result.Status.Should().Be(AgentRunStatus.ModerationProviderUnavailable);
        inner.Called.Should().BeFalse();
    }

    [Fact]
    public async Task Provider_Unavailable_FailOpen_Allows_Standard()
    {
        var (db, a) = Seed();
        var inner = new FakeRuntime();
        var moderator = new FakeModerator { Verdict = (_, _, _) => ModerationVerdict.Unavailable(0) };
        var rt = Wire(db, inner, moderator);
        var sink = new Mock<IAgentRunSink>();

        var result = await rt.RunAsync(Ctx(a, SafetyPreset.Standard), sink.Object, default);

        result.Status.Should().Be(AgentRunStatus.Completed);
        inner.Called.Should().BeTrue();
    }
}
```

- [ ] **Step 3: Build, test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~ContentModerationEnforcingAgentRuntimeTests"
git add -p && git commit -m "feat(ai): 5d-2 — ContentModerationEnforcingAgentRuntime decorator with input/output scan + buffering"
```

---

### Task D3: `AgentToolDispatcher` `[DangerousAction]` check

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Runtime/AgentToolDispatcher.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Runtime/AgentToolDispatcher.cs` (interface signature if needed)
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Approvals/AgentToolDispatcherDangerousActionTests.cs`

The dispatcher needs to read the assistant's `Id` (for the pending-approval row), the agent principal's `Id`, conversation/task linkage, and the requesting user. The cleanest approach: pass an `IPendingApprovalContext` provider that exposes the *current* run's assistant + principal + conversation IDs (set by the runtime when it starts the run). For 5d-2, we read from `IExecutionContext` (TenantId, AgentPrincipalId, UserId) and require the runtime factory to wire a small "pending-approval context accessor" before each run.

For minimal invasiveness, read the current state directly from `IExecutionContext` + a new `ICurrentAgentRunContextAccessor` (mirror of `IPersonaContextAccessor`). The runtime sets it at scope-open; the dispatcher reads it.

- [ ] **Step 1: Create `ICurrentAgentRunContextAccessor`**

```csharp
namespace Starter.Module.AI.Application.Services.Runtime;

/// <summary>
/// AsyncLocal accessor exposing the current agent run's assistant + conversation/task linkage
/// to scoped services (notably AgentToolDispatcher) without threading it through every method.
/// Set by the runtime decorator at run start, cleared on dispose.
/// </summary>
internal interface ICurrentAgentRunContextAccessor
{
    Guid? AssistantId { get; }
    string? AssistantName { get; }
    Guid? AgentPrincipalId { get; }
    Guid? ConversationId { get; }
    Guid? AgentTaskId { get; }
    Guid? RequestingUserId { get; }
    Guid? TenantId { get; }
}
```

- [ ] **Step 2: Implementation backed by `AsyncLocal`**

```csharp
namespace Starter.Module.AI.Infrastructure.Runtime;

internal sealed class CurrentAgentRunContextAccessor : ICurrentAgentRunContextAccessor
{
    private static readonly AsyncLocal<RunCtx?> _current = new();

    public Guid? AssistantId => _current.Value?.AssistantId;
    public string? AssistantName => _current.Value?.AssistantName;
    public Guid? AgentPrincipalId => _current.Value?.AgentPrincipalId;
    public Guid? ConversationId => _current.Value?.ConversationId;
    public Guid? AgentTaskId => _current.Value?.AgentTaskId;
    public Guid? RequestingUserId => _current.Value?.RequestingUserId;
    public Guid? TenantId => _current.Value?.TenantId;

    public IDisposable Use(RunCtx ctx)
    {
        var prev = _current.Value;
        _current.Value = ctx;
        return new Restorer(prev);
    }

    internal sealed record RunCtx(
        Guid AssistantId, string AssistantName, Guid AgentPrincipalId,
        Guid? ConversationId, Guid? AgentTaskId, Guid? RequestingUserId, Guid? TenantId);

    private sealed class Restorer(RunCtx? prev) : IDisposable
    {
        public void Dispose() => _current.Value = prev;
    }
}
```

- [ ] **Step 3: Update `AgentToolDispatcher` to check the attribute**

In `AgentToolDispatcher.DispatchAsync`, between the permission check and `sender.Send`, add:

```csharp
// Plan 5d-2: [DangerousAction] check. Skipped when the caller already holds an approval grant.
var attr = def.CommandType.GetCustomAttribute<DangerousActionAttribute>();
if (attr is not null && !execution.DangerousActionApprovalGrant)
{
    var runCtx = runContext.AssistantId is { } aid && runContext.AgentPrincipalId is { } apid
        ? (assistantId: aid, principalId: apid)
        : default((Guid assistantId, Guid principalId)?);
    if (runCtx is null)
    {
        // Defensive: no agent context — refuse outright (HTTP path can't trigger DangerousAction
        // because the runtime is the only thing that sets the AsyncLocal).
        return Failure(PendingApprovalErrors.AccessDenied);
    }

    var pa = await pendingApprovals.CreateAsync(
        tenantId: runContext.TenantId,
        assistantId: runCtx.Value.assistantId,
        assistantName: runContext.AssistantName ?? "agent",
        agentPrincipalId: runCtx.Value.principalId,
        conversationId: runContext.ConversationId,
        agentTaskId: runContext.AgentTaskId,
        requestingUserId: runContext.RequestingUserId,
        toolName: call.Name,
        commandTypeName: def.CommandType.AssemblyQualifiedName ?? def.CommandType.FullName!,
        argumentsJson: call.ArgumentsJson,
        reasonHint: attr.Reason,
        expiresIn: TimeSpan.FromHours(approvalExpirationHours),
        ct: ct);

    return new AgentToolDispatchResult(
        JsonSerializer.Serialize(new
        {
            ok = false,
            error = new
            {
                code = "AiAgent.AwaitingApproval",
                message = $"Approval required for tool '{call.Name}'.",
                approvalId = pa.Id,
                expiresAt = pa.ExpiresAt
            }
        }, AiJsonDefaults.Serializer),
        IsError: true,
        AwaitingApproval: true,
        ApprovalId: pa.Id);
}
```

- [ ] **Step 4: Extend `AgentToolDispatchResult` with the new fields**

In `AgentToolDispatchResult.cs` (or wherever it's defined), add two new fields:

```csharp
internal sealed record AgentToolDispatchResult(
    string ResultJson,
    bool IsError,
    bool AwaitingApproval = false,
    Guid? ApprovalId = null);
```

(If existing call sites use positional construction, this default-only change is binary-compat.)

- [ ] **Step 5: Update `AgentToolDispatcher` constructor to inject the new dependencies**

```csharp
internal sealed class AgentToolDispatcher(
    ISender sender,
    IExecutionContext execution,
    ICurrentAgentRunContextAccessor runContext,
    IPendingApprovalService pendingApprovals,
    IConfiguration configuration,
    ILogger<AgentToolDispatcher> logger) : IAgentToolDispatcher
{
    private int approvalExpirationHours =>
        configuration.GetValue<int?>("Ai:Moderation:ApprovalExpirationHours") ?? 24;
    // … existing body, modified per Step 3
}
```

> The dispatcher passes `assistantId` + `assistantName` directly — these come from the `ICurrentAgentRunContextAccessor` populated by `AgentExecutionScope`. No `AiAssistant` lookup at dispatch time.

- [ ] **Step 6: Tests**

```csharp
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Application.Common.Attributes;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Approvals;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Runtime;
using Starter.Shared.Results;
using Xunit;

namespace Starter.Api.Tests.Ai.Approvals;

[DangerousAction("Test mass deletion")]
public sealed record FakeDeleteAllCommand(bool Confirm) : IRequest<Result>;

public sealed class AgentToolDispatcherDangerousActionTests
{
    [Fact]
    public async Task Persists_Approval_And_Returns_Awaiting()
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);

        var sender = new Mock<ISender>();
        var exec = new Mock<IExecutionContext>();
        exec.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
        exec.SetupGet(x => x.DangerousActionApprovalGrant).Returns(false);

        var runCtx = new Mock<ICurrentAgentRunContextAccessor>();
        runCtx.SetupGet(x => x.AssistantId).Returns(Guid.NewGuid());
        runCtx.SetupGet(x => x.AssistantName).Returns("Tutor");
        runCtx.SetupGet(x => x.AgentPrincipalId).Returns(Guid.NewGuid());
        runCtx.SetupGet(x => x.ConversationId).Returns(Guid.NewGuid());
        runCtx.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        runCtx.SetupGet(x => x.RequestingUserId).Returns(Guid.NewGuid());

        var approvals = new PendingApprovalService(db, NullLogger<PendingApprovalService>.Instance);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var dispatcher = new AgentToolDispatcher(
            sender.Object, exec.Object, runCtx.Object, approvals, cfg,
            NullLogger<AgentToolDispatcher>.Instance);

        var def = new ResolvedToolDefinition(
            Name: "DeleteAll",
            Description: "test",
            Schema: "{}",
            CommandType: typeof(FakeDeleteAllCommand),
            RequiredPermission: "AnyPermission");
        var tools = new ToolResolutionResult(
            new Dictionary<string, ResolvedToolDefinition> { ["DeleteAll"] = def },
            Array.Empty<AiToolDefinition>());

        var result = await dispatcher.DispatchAsync(
            new AiToolCall("call-1", "DeleteAll", """{"Confirm":true}"""), tools, default);

        result.IsError.Should().BeTrue();
        result.AwaitingApproval.Should().BeTrue();
        result.ApprovalId.Should().NotBeNull();
        sender.Verify(s => s.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);

        var pa = await db.AiPendingApprovals.FirstAsync();
        pa.ToolName.Should().Be("DeleteAll");
    }

    [Fact]
    public async Task Skip_Check_When_Grant_Is_Active()
    {
        // ... mirror the test above but exec.SetupGet(x => x.DangerousActionApprovalGrant).Returns(true)
        // and verify sender.Send(...) is invoked once.
    }
}
```

- [ ] **Step 7: Build, test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~AgentToolDispatcherDangerousActionTests"
git add -p && git commit -m "feat(ai): 5d-2 — AgentToolDispatcher [DangerousAction] check + AwaitingApproval result"
```

---

### Task D4: Wire the moderation decorator into `AiAgentRuntimeFactory`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Runtime/AiAgentRuntimeFactory.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Runtime/AgentExecutionScope.cs` (set the new `ICurrentAgentRunContextAccessor` at scope open)

- [ ] **Step 1: Wrap with the new decorator outermost**

```csharp
public IAiAgentRuntime Create(AiProviderType providerType)
{
    IAiAgentRuntime inner = providerType switch
    {
        AiProviderType.OpenAI => services.GetRequiredService<OpenAiAgentRuntime>(),
        AiProviderType.Anthropic => services.GetRequiredService<AnthropicAgentRuntime>(),
        AiProviderType.Ollama => services.GetRequiredService<OllamaAgentRuntime>(),
        _ => throw new NotSupportedException($"No agent runtime registered for provider {providerType}.")
    };

    // 5d-1: cost cap layer
    var costEnforced = new CostCapEnforcingAgentRuntime(
        inner,
        services.GetRequiredService<ICostCapResolver>(),
        services.GetRequiredService<ICostCapAccountant>(),
        services.GetRequiredService<IAgentRateLimiter>(),
        services.GetRequiredService<IModelPricingService>(),
        services.GetRequiredService<ILogger<CostCapEnforcingAgentRuntime>>());

    // 5d-2: moderation layer (outermost)
    return new ContentModerationEnforcingAgentRuntime(
        costEnforced,
        services.GetRequiredService<IContentModerator>(),
        services.GetRequiredService<IPiiRedactor>(),
        services.GetRequiredService<ISafetyProfileResolver>(),
        services.GetRequiredService<IModerationRefusalProvider>(),
        services.GetRequiredService<AiDbContext>(),
        services.GetRequiredService<ICurrentUserService>(),
        services.GetRequiredService<ILogger<ContentModerationEnforcingAgentRuntime>>());
}
```

- [ ] **Step 2: Set the run-context accessor when `AgentExecutionScope.Begin` is called**

The current `AgentExecutionScope.Begin` accepts the user/agent/tenant tuple. Extend the factory pattern: callers (`ChatExecutionService` for chat, future task runners for operational agents) pass the assistant ID/name + conversation/task to `AgentExecutionScope.Begin`. The scope, in addition to installing the `IExecutionContext`, also installs the `ICurrentAgentRunContextAccessor` ambient value.

For 5d-2 task scope, the `ChatExecutionService` is the only call site. Update its `Begin` invocation to pass the new params; update the scope to expose them through `_runCtxScope` and dispose both on `Dispose`.

```csharp
// AgentExecutionScope.Begin signature additions
public static AgentExecutionScope Begin(
    Guid? userId,
    Guid agentPrincipalId,
    Guid? tenantId,
    Func<string, bool>? callerHasPermission,
    Func<string, bool> agentHasPermission,
    Guid assistantId,
    string assistantName,
    Guid? conversationId,
    Guid? agentTaskId,
    CurrentAgentRunContextAccessor runCtxAccessor)
{
    ArgumentNullException.ThrowIfNull(agentHasPermission);
    var scope = new AgentExecutionScope(userId, agentPrincipalId, tenantId, callerHasPermission, agentHasPermission);
    scope._runCtxScope = runCtxAccessor.Use(new CurrentAgentRunContextAccessor.RunCtx(
        AssistantId: assistantId,
        AssistantName: assistantName,
        AgentPrincipalId: agentPrincipalId,
        ConversationId: conversationId,
        AgentTaskId: agentTaskId,
        RequestingUserId: userId,
        TenantId: tenantId));
    return scope;
}
```

Add `private IDisposable? _runCtxScope;` and update `Dispose` to call both:

```csharp
public void Dispose()
{
    _runCtxScope?.Dispose();
    _ambientScope.Dispose();
}
```

- [ ] **Step 3: Build (no new test — exercised by integration in D5 and acid tests)**

```bash
dotnet build boilerplateBE/Starter.sln
git add -p && git commit -m "feat(ai): 5d-2 — wire moderation decorator outermost + run-context accessor"
```

---

### Task D5: `ChatExecutionService` — handle `AwaitingApproval` as success + emit SSE frames

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/ChatExecutionService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiChatReplyDto.cs` (add `Status`, `ApprovalId`, `ExpiresAt` fields)

- [ ] **Step 1: Extend `AiChatReplyDto`**

Add three optional properties:

```csharp
public string? Status { get; init; }            // "completed" | "awaiting_approval" | "blocked"
public Guid? ApprovalId { get; init; }
public DateTime? ExpiresAt { get; init; }
public string? ToolName { get; init; }
public string? ApprovalReason { get; init; }
```

- [ ] **Step 2: Handle the two new run statuses in `ExecuteAsync`**

After the existing `runResult.Status == AgentRunStatus.RateLimitExceeded` branch:

```csharp
if (runResult.Status == AgentRunStatus.InputBlocked || runResult.Status == AgentRunStatus.OutputBlocked)
{
    // Refusal text already in runResult.FinalContent. Persist as the assistant message.
    var refusalContent = runResult.FinalContent ?? "Request blocked by content moderation.";
    var citations = Array.Empty<AiMessageCitation>();
    var finalMessage = await FinalizeTurnAsync(state, refusalContent,
        (int)runResult.TotalInputTokens, (int)runResult.TotalOutputTokens,
        sink.NextOrder, citations, ct);
    return Result.Success(new AiChatReplyDto(
        state.Conversation.Id,
        state.UserMessage.ToDto(),
        finalMessage.ToDto(),
        PersonaSlug: state.Persona?.Slug)
    {
        Status = "blocked",
    });
}

if (runResult.Status == AgentRunStatus.AwaitingApproval)
{
    await FailTurnAsync(state); // user message stays orphaned-as-detached for chat continuity
    // The pending approval row was written by AgentToolDispatcher. Surface its details:
    var approvalId = ExtractApprovalId(runResult); // helper that pulls from runResult.TerminationReason or the last step's tool result
    var approval = await context.AiPendingApprovals.FirstOrDefaultAsync(p => p.Id == approvalId, ct);
    return Result.Success(new AiChatReplyDto(
        state.Conversation.Id,
        state.UserMessage.ToDto(),
        AssistantMessage: null,
        PersonaSlug: state.Persona?.Slug)
    {
        Status = "awaiting_approval",
        ApprovalId = approval?.Id,
        ExpiresAt = approval?.ExpiresAt,
        ToolName = approval?.ToolName,
        ApprovalReason = approval?.ReasonHint
    });
}

if (runResult.Status == AgentRunStatus.ModerationProviderUnavailable)
{
    await FailTurnAsync(state);
    return Result.Failure<AiChatReplyDto>(AiModerationErrors.ProviderUnavailable);
}
```

`ExtractApprovalId` helper — read from the last step event's tool-result JSON:

```csharp
private static Guid? ExtractApprovalId(AgentRunResult result)
{
    // Steps[^1].ToolResults[^1].ResultJson contains {"ok":false,"error":{"approvalId":"..."}}
    var lastStep = result.Steps.LastOrDefault();
    var lastResult = lastStep?.ToolResults?.LastOrDefault();
    if (lastResult is null) return null;
    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(lastResult.ResultJson);
        if (doc.RootElement.TryGetProperty("error", out var err) &&
            err.TryGetProperty("approvalId", out var idEl) &&
            Guid.TryParse(idEl.GetString(), out var id))
            return id;
    }
    catch { /* best-effort */ }
    return null;
}
```

> If `AgentStepEvent.ToolResults` doesn't exist on the existing record, inspect the file and adjust to the correct property name. The intent is: `runResult.Steps` last entry → its tool-result JSON → `error.approvalId`.

- [ ] **Step 3: Same handling in `ExecuteStreamAsync`**

Mirror the four new branches before the existing `runResult.Status == AgentRunStatus.CostCapExceeded` block. Stream events:

```csharp
if (runResult.Status == AgentRunStatus.InputBlocked || runResult.Status == AgentRunStatus.OutputBlocked)
{
    yield return new ChatStreamEvent("moderation_blocked", new
    {
        Stage = runResult.Status == AgentRunStatus.InputBlocked ? "input" : "output",
        Reason = runResult.TerminationReason
    });
    // Persist refusal as assistant message and emit done
    var refusalContent = runResult.FinalContent ?? "Request blocked by content moderation.";
    var assistantMessage = await FinalizeTurnAsync(state, refusalContent,
        (int)runResult.TotalInputTokens, (int)runResult.TotalOutputTokens,
        sink.NextOrder, Array.Empty<AiMessageCitation>(), ct);
    yield return new ChatStreamEvent("done", new
    {
        MessageId = assistantMessage.Id,
        InputTokens = (int)runResult.TotalInputTokens,
        OutputTokens = (int)runResult.TotalOutputTokens,
        FinishReason = runResult.Status.ToString()
    });
    yield break;
}

if (runResult.Status == AgentRunStatus.AwaitingApproval)
{
    var approvalId = ExtractApprovalId(runResult);
    var approval = await context.AiPendingApprovals.FirstOrDefaultAsync(p => p.Id == approvalId, ct);
    yield return new ChatStreamEvent("awaiting_approval", new
    {
        ApprovalId = approval?.Id,
        ExpiresAt = approval?.ExpiresAt,
        ToolName = approval?.ToolName,
        Reason = approval?.ReasonHint
    });
    yield return new ChatStreamEvent("done", new { FinishReason = "awaiting_approval" });
    yield break;
}

if (runResult.Status == AgentRunStatus.ModerationProviderUnavailable)
{
    await FailTurnAsync(state);
    yield return new ChatStreamEvent("error", new
    {
        Code = "AiModeration.ProviderUnavailable",
        Message = "Content moderation provider is unavailable; please retry."
    });
    yield break;
}
```

- [ ] **Step 4: Build (integration covered by acid tests in Phase H)**

```bash
dotnet build boilerplateBE/Starter.sln
git add -p && git commit -m "feat(ai): 5d-2 — ChatExecutionService handles AwaitingApproval/Blocked + new SSE frames"
```

---

## Phase E — CQRS surface + controllers + DI

> All commands return `Result<T>` and follow the existing 5d-1 conventions (sealed record, internal sealed handler with primary constructor, optional FluentValidation validator). Each task here lists the file paths and the canonical handler shape. Where a single task pairs two commands/queries that share concerns, the listing is consolidated.

### Task E1: `UpsertSafetyPresetProfileCommand` + `DeactivateSafetyPresetProfileCommand`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Safety/UpsertSafetyPresetProfile/UpsertSafetyPresetProfileCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Safety/UpsertSafetyPresetProfile/UpsertSafetyPresetProfileCommandHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Safety/UpsertSafetyPresetProfile/UpsertSafetyPresetProfileCommandValidator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Safety/DeactivateSafetyPresetProfile/DeactivateSafetyPresetProfileCommand.cs` + handler
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Moderation/UpsertSafetyPresetProfileCommandTests.cs`

- [ ] **Step 1: Command**

```csharp
using MediatR;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Safety.UpsertSafetyPresetProfile;

public sealed record UpsertSafetyPresetProfileCommand(
    Guid? TenantId,                              // null = platform default; SuperAdmin only
    SafetyPreset Preset,
    ModerationProvider Provider,
    string CategoryThresholdsJson,
    string BlockedCategoriesJson,
    ModerationFailureMode FailureMode,
    bool RedactPii) : IRequest<Result<Guid>>;
```

- [ ] **Step 2: Validator**

```csharp
using FluentValidation;

namespace Starter.Module.AI.Application.Commands.Safety.UpsertSafetyPresetProfile;

public sealed class UpsertSafetyPresetProfileCommandValidator : AbstractValidator<UpsertSafetyPresetProfileCommand>
{
    public UpsertSafetyPresetProfileCommandValidator()
    {
        RuleFor(x => x.CategoryThresholdsJson).NotEmpty();
        RuleFor(x => x.BlockedCategoriesJson).NotEmpty();
    }
}
```

- [ ] **Step 3: Handler**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Constants;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Safety.UpsertSafetyPresetProfile;

internal sealed class UpsertSafetyPresetProfileCommandHandler(
    AiDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<UpsertSafetyPresetProfileCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UpsertSafetyPresetProfileCommand cmd, CancellationToken ct)
    {
        // SuperAdmin only for platform-default rows. Tenant admin scoped to own tenant.
        if (cmd.TenantId is null)
        {
            if (!currentUser.HasPermission(AiPermissions.SafetyProfilesManage) || currentUser.TenantId is not null)
                return Result.Failure<Guid>(Error.Forbidden("Only platform admins can edit platform-default safety profiles."));
        }
        else
        {
            if (currentUser.TenantId is not Guid mine || mine != cmd.TenantId)
                if (!IsPlatformAdmin(currentUser))
                    return Result.Failure<Guid>(Error.Forbidden("Cannot manage another tenant's safety profile."));
        }

        var existing = await db.AiSafetyPresetProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p =>
                p.TenantId == cmd.TenantId && p.Preset == cmd.Preset &&
                p.Provider == cmd.Provider && p.IsActive, ct);

        if (existing is null)
        {
            var entity = AiSafetyPresetProfile.Create(
                cmd.TenantId, cmd.Preset, cmd.Provider,
                cmd.CategoryThresholdsJson, cmd.BlockedCategoriesJson,
                cmd.FailureMode, cmd.RedactPii);
            db.AiSafetyPresetProfiles.Add(entity);
            await db.SaveChangesAsync(ct);
            return Result.Success(entity.Id);
        }

        existing.Update(cmd.CategoryThresholdsJson, cmd.BlockedCategoriesJson, cmd.FailureMode, cmd.RedactPii);
        await db.SaveChangesAsync(ct);
        return Result.Success(existing.Id);
    }

    private static bool IsPlatformAdmin(ICurrentUserService cu) => cu.TenantId is null;
}
```

- [ ] **Step 4: `DeactivateSafetyPresetProfileCommand`**

Same shape, calls `entity.Deactivate()`. Body:

```csharp
public sealed record DeactivateSafetyPresetProfileCommand(Guid ProfileId) : IRequest<Result>;

internal sealed class DeactivateSafetyPresetProfileCommandHandler(
    AiDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<DeactivateSafetyPresetProfileCommand, Result>
{
    public async Task<Result> Handle(DeactivateSafetyPresetProfileCommand cmd, CancellationToken ct)
    {
        var entity = await db.AiSafetyPresetProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == cmd.ProfileId, ct);
        if (entity is null)
            return Result.Failure(AiModerationErrors.PresetProfileNotFound(default!, default!));

        if (entity.TenantId is null && currentUser.TenantId is not null)
            return Result.Failure(Error.Forbidden("Only platform admins can deactivate platform-default profiles."));
        if (entity.TenantId is { } et && currentUser.TenantId is { } ct2 && et != ct2)
            return Result.Failure(Error.Forbidden("Cannot deactivate another tenant's profile."));

        entity.Deactivate();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

- [ ] **Step 5: Test (handler-level smoke)**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Safety.UpsertSafetyPresetProfile;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class UpsertSafetyPresetProfileCommandTests
{
    [Fact]
    public async Task Tenant_Admin_Upserts_Own_Tenant_Row()
    {
        var tenant = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        cu.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);

        var handler = new UpsertSafetyPresetProfileCommandHandler(db, cu.Object);
        var result = await handler.Handle(new UpsertSafetyPresetProfileCommand(
            TenantId: tenant, Preset: SafetyPreset.ChildSafe, Provider: ModerationProvider.OpenAi,
            CategoryThresholdsJson: """{"sexual":0.5}""",
            BlockedCategoriesJson: """["sexual-minors"]""",
            FailureMode: ModerationFailureMode.FailClosed,
            RedactPii: false), default);

        result.IsSuccess.Should().BeTrue();
        var row = await db.AiSafetyPresetProfiles.FirstAsync();
        row.TenantId.Should().Be(tenant);
        row.Preset.Should().Be(SafetyPreset.ChildSafe);
    }

    [Fact]
    public async Task Tenant_Admin_Cannot_Edit_Platform_Default()
    {
        var tenant = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        cu.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
        var opts = new DbContextOptionsBuilder<AiDbContext>().UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);

        var handler = new UpsertSafetyPresetProfileCommandHandler(db, cu.Object);
        var result = await handler.Handle(new UpsertSafetyPresetProfileCommand(
            TenantId: null, Preset: SafetyPreset.Standard, Provider: ModerationProvider.OpenAi,
            CategoryThresholdsJson: "{}", BlockedCategoriesJson: "[]",
            FailureMode: ModerationFailureMode.FailOpen, RedactPii: false), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }
}
```

- [ ] **Step 6: Build, test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~UpsertSafetyPresetProfileCommandTests"
git add -p && git commit -m "feat(ai): 5d-2 — Upsert + Deactivate SafetyPresetProfile commands"
```

---

### Task E2: `SetAssistantSafetyPresetOverrideCommand`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Safety/SetAssistantSafetyPresetOverride/SetAssistantSafetyPresetOverrideCommand.cs`
- Create handler in same folder
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Moderation/SetAssistantSafetyPresetOverrideCommandTests.cs`

- [ ] **Step 1: Command + handler**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Safety.SetAssistantSafetyPresetOverride;

public sealed record SetAssistantSafetyPresetOverrideCommand(
    Guid AssistantId,
    SafetyPreset? Preset) : IRequest<Result>;

internal sealed class SetAssistantSafetyPresetOverrideCommandHandler(
    AiDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<SetAssistantSafetyPresetOverrideCommand, Result>
{
    public async Task<Result> Handle(SetAssistantSafetyPresetOverrideCommand cmd, CancellationToken ct)
    {
        var assistant = await db.AiAssistants.FirstOrDefaultAsync(a => a.Id == cmd.AssistantId, ct);
        if (assistant is null) return Result.Failure(AiErrors.AssistantNotFound);

        if (currentUser.TenantId is { } tenant && assistant.TenantId != tenant && currentUser.TenantId is not null)
            return Result.Failure(Error.Forbidden("Cannot manage another tenant's assistant."));

        assistant.SetSafetyPreset(cmd.Preset);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

- [ ] **Step 2: Test (handler smoke)**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Safety.SetAssistantSafetyPresetOverride;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class SetAssistantSafetyPresetOverrideCommandTests
{
    [Fact]
    public async Task Sets_Override_For_Own_Tenant()
    {
        var tenant = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        var opts = new DbContextOptionsBuilder<AiDbContext>().UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        var assistant = AiAssistant.Create(tenant, "Tutor", null, "p", Guid.NewGuid());
        db.AiAssistants.Add(assistant);
        await db.SaveChangesAsync();

        var handler = new SetAssistantSafetyPresetOverrideCommandHandler(db, cu.Object);
        var result = await handler.Handle(
            new SetAssistantSafetyPresetOverrideCommand(assistant.Id, SafetyPreset.ChildSafe), default);

        result.IsSuccess.Should().BeTrue();
        (await db.AiAssistants.FindAsync(assistant.Id))!.SafetyPresetOverride.Should().Be(SafetyPreset.ChildSafe);
    }
}
```

- [ ] **Step 3: Build, test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~SetAssistantSafetyPresetOverrideCommandTests"
git add -p && git commit -m "feat(ai): 5d-2 — SetAssistantSafetyPresetOverride command"
```

---

### Task E3: `ApprovePendingActionCommand` + `DenyPendingActionCommand`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Approvals/ApprovePendingAction/ApprovePendingActionCommand.cs` + handler
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/Approvals/DenyPendingAction/DenyPendingActionCommand.cs` + handler
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Approvals/ApproveDenyPendingActionTests.cs`

- [ ] **Step 1: Approve command + handler**

```csharp
using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Approvals;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Runtime;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Approvals.ApprovePendingAction;

public sealed record ApprovePendingActionCommand(Guid ApprovalId, string? Note) : IRequest<Result<object?>>;

internal sealed class ApprovePendingActionCommandHandler(
    IPendingApprovalService approvals,
    ISender sender,
    ICurrentUserService currentUser,
    AiDbContext db) : IRequestHandler<ApprovePendingActionCommand, Result<object?>>
{
    public async Task<Result<object?>> Handle(ApprovePendingActionCommand cmd, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<object?>(Error.Unauthorized());

        // Resolve the command type before flipping state — if missing, auto-deny.
        var paProbe = await db.AiPendingApprovals.FindAsync(new object?[] { cmd.ApprovalId }, ct);
        if (paProbe is null)
            return Result.Failure<object?>(PendingApprovalErrors.NotFound(cmd.ApprovalId));

        var commandType = Type.GetType(paProbe.CommandTypeName, throwOnError: false);
        if (commandType is null)
        {
            await approvals.DenyAsync(cmd.ApprovalId, userId, $"tool unavailable: {paProbe.CommandTypeName}", ct);
            return Result.Failure<object?>(PendingApprovalErrors.ToolUnavailable(paProbe.CommandTypeName));
        }

        // Tenant scope
        if (currentUser.TenantId is not null && paProbe.TenantId != currentUser.TenantId)
            return Result.Failure<object?>(PendingApprovalErrors.AccessDenied);

        // Flip status to Approved (raises AgentApprovalApprovedEvent).
        var approveResult = await approvals.ApproveAsync(cmd.ApprovalId, userId, cmd.Note, ct);
        if (approveResult.IsFailure)
            return Result.Failure<object?>(approveResult.Error);

        // Reconstitute and re-dispatch via ApprovalGrantExecutionContext.
        var commandObject = System.Text.Json.JsonSerializer.Deserialize(
            paProbe.ArgumentsJson, commandType, AiJsonDefaults.Serializer);
        if (commandObject is null)
            return Result.Failure<object?>(PendingApprovalErrors.ToolUnavailable(paProbe.CommandTypeName));

        var ambient = Starter.Application.Common.Interfaces.AmbientExecutionContext.Current
            ?? new HttpExecutionContextStub(); // safety: should never be null inside an HTTP request
        using var grantScope = Starter.Application.Common.Interfaces.AmbientExecutionContext.Use(
            new ApprovalGrantExecutionContext(ambient));

        var toolResult = await sender.Send(commandObject, ct);
        return Result.Success(toolResult);
    }

    private sealed class HttpExecutionContextStub : IExecutionContext
    {
        public Guid? UserId => null;
        public Guid? AgentPrincipalId => null;
        public Guid? TenantId => null;
        public Guid? AgentRunId => null;
        public bool DangerousActionApprovalGrant => false;
        public bool HasPermission(string permission) => false;
    }
}
```

> **`AiJsonDefaults.Serializer`** is already used by `AgentToolDispatcher`; reuse the same instance.
>
> **`HttpExecutionContextStub`**: defensive fallback if `AmbientExecutionContext.Current` is null. In the chat path this is set by `HttpExecutionContext`; the stub avoids a `NullReferenceException` if a future caller sends the command without an HTTP scope. Inspect the existing `HttpExecutionContext` registration to confirm whether it always installs itself; if so the stub can be deleted and a guard added.

- [ ] **Step 2: Deny command + handler**

```csharp
using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Approvals;
using Starter.Module.AI.Domain.Errors;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Approvals.DenyPendingAction;

public sealed record DenyPendingActionCommand(Guid ApprovalId, string Reason) : IRequest<Result>;

internal sealed class DenyPendingActionCommandHandler(
    IPendingApprovalService approvals,
    ICurrentUserService currentUser) : IRequestHandler<DenyPendingActionCommand, Result>
{
    public async Task<Result> Handle(DenyPendingActionCommand cmd, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure(Error.Unauthorized());
        if (string.IsNullOrWhiteSpace(cmd.Reason))
            return Result.Failure(PendingApprovalErrors.DenyReasonRequired);

        var result = await approvals.DenyAsync(cmd.ApprovalId, userId, cmd.Reason, ct);
        return result.IsFailure ? Result.Failure(result.Error) : Result.Success();
    }
}
```

- [ ] **Step 3: Tests (focused on the deny path; approve path covered by acid M4)**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Approvals.DenyPendingAction;
using Starter.Module.AI.Application.Services.Approvals;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Approvals;

public sealed class ApproveDenyPendingActionTests
{
    [Fact]
    public async Task Deny_Without_Reason_Fails_Validation()
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        var opts = new DbContextOptionsBuilder<AiDbContext>().UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        var svc = new PendingApprovalService(db, NullLogger<PendingApprovalService>.Instance);
        var handler = new DenyPendingActionCommandHandler(svc, cu.Object);

        var result = await handler.Handle(new DenyPendingActionCommand(Guid.NewGuid(), ""), default);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PendingApproval.DenyReasonRequired");
    }
}
```

- [ ] **Step 4: Build, test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~ApproveDenyPendingActionTests"
git add -p && git commit -m "feat(ai): 5d-2 — Approve + Deny PendingAction commands with grant-scoped re-dispatch"
```

---

### Task E4: Queries — `GetSafetyPresetProfiles`, `GetModerationEvents`, `GetPendingApprovals`, `GetPendingApprovalById`

**Files:** Four query/handler pairs under `Application/Queries/Safety/` and `Application/Queries/Approvals/` plus DTOs in `Application/DTOs/`.

- [ ] **Step 1: `GetSafetyPresetProfilesQuery`**

```csharp
public sealed record GetSafetyPresetProfilesQuery(int Page = 1, int PageSize = 20) : IRequest<Result<PagedResult<SafetyPresetProfileDto>>>;
```

Handler reads tenant-aware: SuperAdmin sees all, tenant admin sees `(tenant_id IS NULL OR tenant_id = currentUser.TenantId)`. Maps to `SafetyPresetProfileDto { Id, TenantId?, Preset, Provider, CategoryThresholdsJson, BlockedCategoriesJson, FailureMode, RedactPii, Version, IsActive, CreatedAt, ModifiedAt? }`.

- [ ] **Step 2: `GetModerationEventsQuery`**

```csharp
public sealed record GetModerationEventsQuery(
    DateTime? From = null, DateTime? To = null,
    ModerationOutcome? Outcome = null, ModerationStage? Stage = null,
    Guid? AssistantId = null,
    int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedResult<ModerationEventDto>>>;
```

Tenant-scoped via global filter on `AiModerationEvent` (which `OnModelCreating` should apply — verify in B6 / when first DbSet is added; if it doesn't, the handler should add the `tenant_id = current` clause manually for tenant admin).

- [ ] **Step 3: `GetPendingApprovalsQuery`**

```csharp
public sealed record GetPendingApprovalsQuery(
    PendingApprovalStatus? Status = null,
    Guid? AssistantId = null,
    int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<PendingApprovalDto>>>;
```

Permission scoping: caller has `Ai.Agents.ApproveAction` → see all rows in tenant; caller only has `Ai.Agents.ViewApprovals` → filter `requesting_user_id == currentUser.UserId`.

- [ ] **Step 4: `GetPendingApprovalByIdQuery`**

Single-row read; same scoping. Returns `Result<PendingApprovalDto>` (or `Result.Failure(PendingApprovalErrors.NotFound(...))`).

- [ ] **Step 5: DTOs**

```csharp
public sealed record SafetyPresetProfileDto(
    Guid Id, Guid? TenantId, SafetyPreset Preset, ModerationProvider Provider,
    string CategoryThresholdsJson, string BlockedCategoriesJson,
    ModerationFailureMode FailureMode, bool RedactPii,
    int Version, bool IsActive, DateTime CreatedAt, DateTime? ModifiedAt);

public sealed record ModerationEventDto(
    Guid Id, Guid? TenantId, Guid? AssistantId, ModerationStage Stage,
    SafetyPreset Preset, ModerationOutcome Outcome,
    string CategoriesJson, ModerationProvider Provider,
    string? BlockedReason, int LatencyMs, DateTime CreatedAt);

public sealed record PendingApprovalDto(
    Guid Id, Guid AssistantId, string AssistantName,
    string ToolName, string CommandTypeName, string ArgumentsJson, string? ReasonHint,
    PendingApprovalStatus Status, Guid? RequestingUserId,
    Guid? DecisionUserId, string? DecisionReason, DateTime? DecidedAt,
    DateTime ExpiresAt, DateTime CreatedAt);
```

- [ ] **Step 6: Handler shape — full example for `GetPendingApprovalsQueryHandler`** (the most permission-sensitive of the four; the others follow the same template, swapping the entity + projection)

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Constants;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Approvals.GetPendingApprovals;

internal sealed class GetPendingApprovalsQueryHandler(
    AiDbContext db,
    ICurrentUserService currentUser) : IRequestHandler<GetPendingApprovalsQuery, Result<PagedResult<PendingApprovalDto>>>
{
    public async Task<Result<PagedResult<PendingApprovalDto>>> Handle(
        GetPendingApprovalsQuery q, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<PagedResult<PendingApprovalDto>>(Error.Unauthorized());

        IQueryable<Domain.Entities.AiPendingApproval> source = db.AiPendingApprovals.AsNoTracking();

        // Permission scoping: ApproveAction sees all rows; ViewApprovals only sees own.
        var canApprove = currentUser.HasPermission(AiPermissions.AgentsApproveAction);
        if (!canApprove)
            source = source.Where(p => p.RequestingUserId == userId);

        if (q.Status is { } s) source = source.Where(p => p.Status == s);
        if (q.AssistantId is { } a) source = source.Where(p => p.AssistantId == a);

        source = source.OrderByDescending(p => p.CreatedAt);

        var total = await source.CountAsync(ct);
        var pageItems = await source
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(p => new PendingApprovalDto(
                p.Id, p.AssistantId, p.AssistantName,
                p.ToolName, p.CommandTypeName, p.ArgumentsJson, p.ReasonHint,
                p.Status, p.RequestingUserId,
                p.DecisionUserId, p.DecisionReason, p.DecidedAt,
                p.ExpiresAt, p.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<PendingApprovalDto>(pageItems, total, q.Page, q.PageSize));
    }
}
```

The other three handlers (`GetSafetyPresetProfilesQueryHandler`, `GetModerationEventsQueryHandler`, `GetPendingApprovalByIdQueryHandler`) follow the same shape: filter by tenant + caller-permission, project to the matching DTO, return `PagedResult<T>` (or single-item `Result<T>` for `ById`).

Smoke test (one per handler), example for the same query:

```csharp
[Fact]
public async Task ViewApprovals_Only_Sees_Own_Rows()
{
    var tenant = Guid.NewGuid();
    var me = Guid.NewGuid();
    var other = Guid.NewGuid();
    var cu = new Mock<ICurrentUserService>();
    cu.SetupGet(x => x.TenantId).Returns(tenant);
    cu.SetupGet(x => x.UserId).Returns(me);
    cu.Setup(x => x.HasPermission(AiPermissions.AgentsApproveAction)).Returns(false);

    var opts = new DbContextOptionsBuilder<AiDbContext>()
        .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
    var db = new AiDbContext(opts, cu.Object);
    db.AiPendingApprovals.AddRange(
        AiPendingApproval.Create(tenant, Guid.NewGuid(), "x", Guid.NewGuid(),
            Guid.NewGuid(), null, me, "T1", "X.Y, X", "{}", null, DateTime.UtcNow.AddHours(1)),
        AiPendingApproval.Create(tenant, Guid.NewGuid(), "x", Guid.NewGuid(),
            Guid.NewGuid(), null, other, "T2", "X.Y, X", "{}", null, DateTime.UtcNow.AddHours(1)));
    await db.SaveChangesAsync();

    var handler = new GetPendingApprovalsQueryHandler(db, cu.Object);
    var result = await handler.Handle(new GetPendingApprovalsQuery(), default);

    result.Value.Items.Should().HaveCount(1);
    result.Value.Items.First().ToolName.Should().Be("T1");
}
```

- [ ] **Step 7: Build, run all queries' tests, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~GetSafetyPresetProfiles|FullyQualifiedName~GetModerationEvents|FullyQualifiedName~GetPendingApprovals"
git add -p && git commit -m "feat(ai): 5d-2 — query handlers for safety profiles, moderation events, pending approvals"
```

---

### Task E5: `AiSafetyController` + `AiAgentApprovalsController` + endpoint on `AiAssistantsController`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiSafetyController.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiAgentApprovalsController.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiAssistantsController.cs`

- [ ] **Step 1: `AiSafetyController`**

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Abstractions.Web;
using Starter.Module.AI.Application.Commands.Safety.DeactivateSafetyPresetProfile;
using Starter.Module.AI.Application.Commands.Safety.UpsertSafetyPresetProfile;
using Starter.Module.AI.Application.Queries.Safety.GetModerationEvents;
using Starter.Module.AI.Application.Queries.Safety.GetSafetyPresetProfiles;
using Starter.Module.AI.Constants;

namespace Starter.Module.AI.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/ai/safety")]
[ApiVersion("1.0")]
public sealed class AiSafetyController(ISender sender) : BaseApiController(sender)
{
    [HttpGet("profiles")]
    [Authorize(Policy = AiPermissions.SafetyProfilesManage)]
    public async Task<IActionResult> GetProfiles([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        HandlePagedResult(await Sender.Send(new GetSafetyPresetProfilesQuery(page, pageSize), ct));

    [HttpPost("profiles")]
    [Authorize(Policy = AiPermissions.SafetyProfilesManage)]
    public async Task<IActionResult> Upsert([FromBody] UpsertSafetyPresetProfileCommand cmd, CancellationToken ct) =>
        HandleResult(await Sender.Send(cmd, ct));

    [HttpDelete("profiles/{profileId:guid}")]
    [Authorize(Policy = AiPermissions.SafetyProfilesManage)]
    public async Task<IActionResult> Deactivate(Guid profileId, CancellationToken ct) =>
        HandleResult(await Sender.Send(new DeactivateSafetyPresetProfileCommand(profileId), ct));

    [HttpGet("moderation-events")]
    [Authorize(Policy = AiPermissions.ModerationView)]
    public async Task<IActionResult> GetModerationEvents([FromQuery] GetModerationEventsQuery query, CancellationToken ct) =>
        HandlePagedResult(await Sender.Send(query, ct));
}
```

- [ ] **Step 2: `AiAgentApprovalsController`**

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Abstractions.Web;
using Starter.Module.AI.Application.Commands.Approvals.ApprovePendingAction;
using Starter.Module.AI.Application.Commands.Approvals.DenyPendingAction;
using Starter.Module.AI.Application.Queries.Approvals.GetPendingApprovalById;
using Starter.Module.AI.Application.Queries.Approvals.GetPendingApprovals;
using Starter.Module.AI.Constants;

namespace Starter.Module.AI.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/ai/agents/approvals")]
[ApiVersion("1.0")]
public sealed class AiAgentApprovalsController(ISender sender) : BaseApiController(sender)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.AgentsViewApprovals)]
    public async Task<IActionResult> List([FromQuery] GetPendingApprovalsQuery q, CancellationToken ct) =>
        HandlePagedResult(await Sender.Send(q, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AiPermissions.AgentsViewApprovals)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        HandleResult(await Sender.Send(new GetPendingApprovalByIdQuery(id), ct));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = AiPermissions.AgentsApproveAction)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveBody body, CancellationToken ct) =>
        HandleResult(await Sender.Send(new ApprovePendingActionCommand(id, body?.Note), ct));

    [HttpPost("{id:guid}/deny")]
    [Authorize(Policy = AiPermissions.AgentsApproveAction)]
    public async Task<IActionResult> Deny(Guid id, [FromBody] DenyBody body, CancellationToken ct) =>
        HandleResult(await Sender.Send(new DenyPendingActionCommand(id, body.Reason), ct));

    public sealed record ApproveBody(string? Note);
    public sealed record DenyBody(string Reason);
}
```

- [ ] **Step 3: Endpoint on `AiAssistantsController`**

Append:

```csharp
[HttpPut("{id:guid}/safety-preset")]
[Authorize(Policy = AiPermissions.ManageAssistants)]
public async Task<IActionResult> SetSafetyPreset(Guid id, [FromBody] SafetyPresetBody body, CancellationToken ct) =>
    HandleResult(await Sender.Send(new SetAssistantSafetyPresetOverrideCommand(id, body?.Preset), ct));

public sealed record SafetyPresetBody(SafetyPreset? Preset);
```

- [ ] **Step 4: Build (controller wiring is exercised by acid tests + manual API verification)**

```bash
dotnet build boilerplateBE/Starter.sln
git add -p && git commit -m "feat(ai): 5d-2 — AiSafetyController + AiAgentApprovalsController + safety-preset endpoint"
```

---

### Task E6: Register DI + permissions in `AIModule.cs`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

- [ ] **Step 1: DI registrations** (in `ConfigureServices`, in the AI-services block)

```csharp
// Plan 5d-2: moderation
services.AddScoped<IContentModerator>(sp =>
{
    var resolver = sp.GetRequiredService<IModerationKeyResolver>();
    return string.IsNullOrWhiteSpace(resolver.Resolve())
        ? new NoOpContentModerator()
        : ActivatorUtilities.CreateInstance<OpenAiContentModerator>(sp);
});
services.AddScoped<IModerationKeyResolver, ConfigurationModerationKeyResolver>();
services.AddScoped<IPiiRedactor, RegexPiiRedactor>();
services.AddScoped<ISafetyProfileResolver, SafetyProfileResolver>();
services.AddSingleton<IModerationRefusalProvider, ResxModerationRefusalProvider>();
services.AddScoped<IPendingApprovalService, PendingApprovalService>();

// Run-context accessor: registered as Singleton because backed by AsyncLocal.
services.AddSingleton<CurrentAgentRunContextAccessor>();
services.AddSingleton<ICurrentAgentRunContextAccessor>(sp => sp.GetRequiredService<CurrentAgentRunContextAccessor>());
```

- [ ] **Step 2: Permissions**

In `GetPermissions()`, append:

```csharp
yield return (AiPermissions.SafetyProfilesManage, "Manage AI safety preset profiles", "AI");
yield return (AiPermissions.AgentsApproveAction, "Approve or deny dangerous AI agent actions", "AI");
yield return (AiPermissions.AgentsViewApprovals, "View pending AI agent approval inbox", "AI");
yield return (AiPermissions.ModerationView, "View AI moderation events", "AI");
```

- [ ] **Step 3: Default role bindings**

Add the four new permissions to SuperAdmin + Admin lists. Add `AgentsViewApprovals` to the User list.

- [ ] **Step 4: Build, commit**

```bash
dotnet build boilerplateBE/Starter.sln
git add -p && git commit -m "feat(ai): 5d-2 — DI + permission + role registrations"
```

---

## Phase F — Cross-module integration (Communication + webhooks)

### Task F1: `CommunicationAiEventHandler` in the Communication module

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Communication/Application/EventHandlers/CommunicationAiEventHandler.cs`

The handler implements all four `INotificationHandler<T>` interfaces for the `AgentApproval*Event` family, resolves recipients (admins-with-`Ai.Agents.ApproveAction` for *pending*; the requesting user for *decided/expired* events), and calls `ITriggerRuleEvaluator.EvaluateAsync(eventName, tenantId, actorUserId, eventData, ct)`.

- [ ] **Step 1: Implement the handler**

```csharp
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Domain.Events;
using Starter.Module.Communication.Infrastructure.Services;

namespace Starter.Module.Communication.Application.EventHandlers;

internal sealed class CommunicationAiEventHandler(
    ITriggerRuleEvaluator evaluator,
    IConfiguration configuration,
    ILogger<CommunicationAiEventHandler> logger)
    : INotificationHandler<AgentApprovalPendingEvent>,
      INotificationHandler<AgentApprovalApprovedEvent>,
      INotificationHandler<AgentApprovalDeniedEvent>,
      INotificationHandler<AgentApprovalExpiredEvent>
{
    public Task Handle(AgentApprovalPendingEvent ev, CancellationToken ct) =>
        Evaluate("ai.agent.approval.pending", ev.TenantId, ev.RequestingUserId, new()
        {
            ["approvalId"] = ev.ApprovalId.ToString(),
            ["assistantId"] = ev.AssistantId.ToString(),
            ["assistantName"] = ev.AssistantName,
            ["toolName"] = ev.ToolName,
            ["reason"] = ev.Reason ?? "",
            ["expiresAt"] = ev.ExpiresAt.ToString("o"),
            ["deepLink"] = BuildDeepLink(ev.ApprovalId)
        }, ct);

    public Task Handle(AgentApprovalApprovedEvent ev, CancellationToken ct) =>
        Evaluate("ai.agent.approval.approved", ev.TenantId, ev.RequestingUserId, new()
        {
            ["approvalId"] = ev.ApprovalId.ToString(),
            ["assistantId"] = ev.AssistantId.ToString(),
            ["assistantName"] = ev.AssistantName,
            ["toolName"] = ev.ToolName,
            ["decisionUserId"] = ev.DecisionUserId.ToString(),
            ["decisionReason"] = ev.DecisionReason ?? ""
        }, ct);

    public Task Handle(AgentApprovalDeniedEvent ev, CancellationToken ct) =>
        Evaluate("ai.agent.approval.denied", ev.TenantId, ev.RequestingUserId, new()
        {
            ["approvalId"] = ev.ApprovalId.ToString(),
            ["assistantId"] = ev.AssistantId.ToString(),
            ["assistantName"] = ev.AssistantName,
            ["toolName"] = ev.ToolName,
            ["decisionUserId"] = ev.DecisionUserId.ToString(),
            ["decisionReason"] = ev.DecisionReason
        }, ct);

    public Task Handle(AgentApprovalExpiredEvent ev, CancellationToken ct) =>
        Evaluate("ai.agent.approval.expired", ev.TenantId, ev.RequestingUserId, new()
        {
            ["approvalId"] = ev.ApprovalId.ToString(),
            ["assistantId"] = ev.AssistantId.ToString(),
            ["assistantName"] = ev.AssistantName,
            ["toolName"] = ev.ToolName,
            ["expiredAt"] = ev.ExpiredAt.ToString("o")
        }, ct);

    private async Task Evaluate(string eventName, Guid tenantId, Guid? actorUserId,
        Dictionary<string, object> data, CancellationToken ct)
    {
        try
        {
            await evaluator.EvaluateAsync(eventName, tenantId, actorUserId, data, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to evaluate trigger rules for {EventName} in tenant {TenantId}", eventName, tenantId);
        }
    }

    private string BuildDeepLink(Guid approvalId)
    {
        var basePath = configuration["FrontendUrl"]?.TrimEnd('/') ?? "";
        return $"{basePath}/ai/agents/approvals/{approvalId}";
    }
}
```

> **Cross-module project reference:** `Starter.Module.Communication` must reference `Starter.Module.AI` to see the `AgentApproval*Event` types. Inspect `Starter.Module.Communication.csproj` — if no reference exists, add `<ProjectReference Include="..\Starter.Module.AI\Starter.Module.AI.csproj" />`. This is acceptable per CLAUDE.md's "Core vs Module vs Shared" rule (modules may depend on each other; only core must not depend on modules).
>
> **If a reference is undesirable** (Communication subscribing to AI events creates a one-directional Communication → AI dependency), an alternative is to define a generic `IModuleEvent` contract in `Starter.Abstractions` that the AI events implement, and have Communication subscribe to that. For 5d-2 we accept the direct reference for simplicity; revisit if more such cross-module subscriptions accumulate.

- [ ] **Step 2: Verify MediatR auto-registration picks up the new handler**

The Communication module's `MediatR` service registration in `CommunicationModule.cs` already calls `AddMediatR(typeof(CommunicationModule).Assembly)` (or equivalent). The new handler is in the same assembly so it's auto-discovered. **No DI change needed** — but `dotnet build` then a focused integration test in **Task H4** confirms wiring.

- [ ] **Step 3: Build + commit**

```bash
dotnet build boilerplateBE/Starter.sln
git add -p && git commit -m "feat(communication): 5d-2 — subscribe to AgentApproval* events and route through trigger rules"
```

---

### Task F2: Seed `RequiredNotification` rows for the four event keys

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.Communication/Infrastructure/Persistence/Seed/RequiredNotificationSeed.cs` (or wherever the seed lives — search for the existing seed first)
- If no seed exists yet: Create one and wire it into `CommunicationModule.SeedDataAsync`.

- [ ] **Step 1: Locate the existing required-notification seed**

```bash
grep -rl "RequiredNotification.Create" boilerplateBE/src/modules/Starter.Module.Communication/
```

- [ ] **Step 2: Add the four AI event keys**

Append (idempotent: skip if rows already exist for the tenant + category):

```csharp
private static readonly string[] AiApprovalEventKeys =
{
    "ai.agent.approval.pending",
    "ai.agent.approval.approved",
    "ai.agent.approval.denied",
    "ai.agent.approval.expired"
};

private static async Task SeedAiApprovalNotificationsAsync(
    CommunicationDbContext db, Guid tenantId, CancellationToken ct)
{
    foreach (var key in AiApprovalEventKeys)
    {
        var exists = await db.RequiredNotifications
            .IgnoreQueryFilters()
            .AnyAsync(r => r.TenantId == tenantId && r.Category == key, ct);
        if (exists) continue;

        db.RequiredNotifications.Add(
            RequiredNotification.Create(tenantId, key, NotificationChannel.InApp));
    }
    await db.SaveChangesAsync(ct);
}
```

Call this from the existing per-tenant seed loop (look for the existing `foreach (var tenant in tenants)` structure and add a call alongside it). For the first run on a fresh database, only seed for tenants that already exist; new tenants will get the rows via the existing tenant-creation event handler (verify in `CommunicationTenantEventHandler`).

- [ ] **Step 3: Build + commit**

```bash
dotnet build boilerplateBE/Starter.sln
git add -p && git commit -m "feat(communication): 5d-2 — seed RequiredNotification rows for ai.agent.approval.* event keys"
```

---

### Task F3: Webhook event publication

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/EventHandlers/PublishWebhookOnAgentApproval.cs` (new)
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Runtime/ContentModerationEnforcingAgentRuntime.cs` (publish `ai.moderation.blocked` after persisting the moderation event)

The four approval lifecycle events fan out through a single `INotificationHandler` that publishes the matching webhook event. Moderation-block webhooks are published inline by the decorator since they aren't tied to a domain event.

- [ ] **Step 1: Create the webhook fan-out handler**

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Domain.Events;

namespace Starter.Module.AI.Application.EventHandlers;

internal sealed class PublishWebhookOnAgentApproval(
    IWebhookPublisher publisher,
    ILogger<PublishWebhookOnAgentApproval> logger)
    : INotificationHandler<AgentApprovalPendingEvent>,
      INotificationHandler<AgentApprovalApprovedEvent>,
      INotificationHandler<AgentApprovalDeniedEvent>,
      INotificationHandler<AgentApprovalExpiredEvent>
{
    public Task Handle(AgentApprovalPendingEvent ev, CancellationToken ct) =>
        SafePublish("ai.agent.approval.pending", ev.TenantId, new
        {
            ev.TenantId, ev.ApprovalId, ev.AssistantId, ev.ToolName, ev.Reason,
            ev.RequestingUserId, ev.ExpiresAt
        }, ct);

    public Task Handle(AgentApprovalApprovedEvent ev, CancellationToken ct) =>
        SafePublish("ai.agent.approval.approved", ev.TenantId, new
        {
            ev.TenantId, ev.ApprovalId, ev.DecisionUserId
        }, ct);

    public Task Handle(AgentApprovalDeniedEvent ev, CancellationToken ct) =>
        SafePublish("ai.agent.approval.denied", ev.TenantId, new
        {
            ev.TenantId, ev.ApprovalId, ev.DecisionUserId, ev.DecisionReason
        }, ct);

    public Task Handle(AgentApprovalExpiredEvent ev, CancellationToken ct) =>
        SafePublish("ai.agent.approval.expired", ev.TenantId, new
        {
            ev.TenantId, ev.ApprovalId, ev.ExpiredAt
        }, ct);

    private async Task SafePublish(string eventType, Guid tenantId, object payload, CancellationToken ct)
    {
        try
        {
            await publisher.PublishAsync(eventType, tenantId, payload, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish webhook {EventType} for tenant {TenantId}", eventType, tenantId);
        }
    }
}
```

- [ ] **Step 2: Publish `ai.moderation.blocked` from the decorator**

Inside `ContentModerationEnforcingAgentRuntime.PersistModerationEventAsync`, after `await db.SaveChangesAsync(ct);` and only when `verdict.Outcome == ModerationOutcome.Blocked`:

```csharp
if (verdict.Outcome == ModerationOutcome.Blocked)
{
    try
    {
        await webhookPublisher.PublishAsync("ai.moderation.blocked", assistant.TenantId, new
        {
            assistant.TenantId,
            AssistantId = assistant.Id,
            ConversationId = (Guid?)null, // set by chat layer in a future enhancement
            Stage = stage,
            Preset = profile.Preset,
            Categories = verdict.Categories,
            Reason = verdict.BlockedReason
        }, ct);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to publish ai.moderation.blocked webhook for tenant {TenantId}", assistant.TenantId);
    }
}
```

Inject `IWebhookPublisher webhookPublisher` into the decorator's primary constructor + DI registration.

- [ ] **Step 3: Build + commit**

```bash
dotnet build boilerplateBE/Starter.sln
git add -p && git commit -m "feat(ai): 5d-2 — publish ai.moderation.blocked + ai.agent.approval.* webhook events"
```

---

## Phase G — Background expiration job

### Task G1: `AiPendingApprovalExpirationJob`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Background/AiPendingApprovalExpirationJob.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs` (`AddHostedService`)

- [ ] **Step 1: Implement the hosted service**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services.Approvals;

namespace Starter.Module.AI.Infrastructure.Background;

/// <summary>
/// Multi-replica-safe expiration job. Uses Postgres FOR UPDATE SKIP LOCKED inside
/// IPendingApprovalService.ExpireDueAsync so concurrent ticks across replicas claim
/// disjoint rows. Bounded batch keeps each transaction sub-second; AgentApprovalExpiredEvent
/// is raised per row inside the SaveChanges, so notifications + webhook fan-out happen
/// in the same transactional unit.
/// </summary>
internal sealed class AiPendingApprovalExpirationJob(
    IServiceScopeFactory scopeFactory,
    ILogger<AiPendingApprovalExpirationJob> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(5);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var svc = scope.ServiceProvider.GetRequiredService<IPendingApprovalService>();
                var n = await svc.ExpireDueAsync(BatchSize, stoppingToken);
                if (n > 0)
                    logger.LogInformation("AiPendingApprovalExpirationJob expired {Count} approvals.", n);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AiPendingApprovalExpirationJob iteration failed; retrying after interval.");
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (TaskCanceledException) { return; }
        }
    }
}
```

- [ ] **Step 2: Register the hosted service**

In `AIModule.ConfigureServices`, alongside `services.AddHostedService<Infrastructure.Background.AiCostReconciliationJob>();`:

```csharp
services.AddHostedService<Infrastructure.Background.AiPendingApprovalExpirationJob>();
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build boilerplateBE/Starter.sln
git add -p && git commit -m "feat(ai): 5d-2 — AiPendingApprovalExpirationJob (multi-replica-safe atomic expiration)"
```

---

### Task G2: Postgres-backed atomicity test

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/AcidTests/Plan5d2ApprovalExpirationAcidTests.cs`

Uses the existing `AiPostgresFixture` (Testcontainers) so we can exercise the real `FOR UPDATE SKIP LOCKED` path.

- [ ] **Step 1: Write the test**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Module.AI.Application.Services.Approvals;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Api.Tests.Ai.Retrieval; // AiPostgresFixture
using Xunit;

namespace Starter.Api.Tests.Ai.AcidTests;

public sealed class Plan5d2ApprovalExpirationAcidTests : IClassFixture<AiPostgresFixture>
{
    private readonly AiPostgresFixture _fx;

    public Plan5d2ApprovalExpirationAcidTests(AiPostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Atomic_Update_Marks_Expired_And_Skips_Already_Decided()
    {
        var tenant = Guid.NewGuid();
        var assistant = Guid.NewGuid();
        var principal = Guid.NewGuid();

        // Seed three rows: one expired+pending, one expired+approved, one not-yet-expired.
        await using (var db = _fx.NewDbContext(tenant))
        {
            db.AiPendingApprovals.AddRange(
                AiPendingApproval.Create(tenant, assistant, "Tutor", principal,
                    Guid.NewGuid(), null, Guid.NewGuid(),
                    "T1", "X.Y, X", "{}", null, DateTime.UtcNow.AddMinutes(-1)),
                AiPendingApproval.Create(tenant, assistant, "Tutor", principal,
                    Guid.NewGuid(), null, Guid.NewGuid(),
                    "T2", "X.Y, X", "{}", null, DateTime.UtcNow.AddMinutes(-1)),
                AiPendingApproval.Create(tenant, assistant, "Tutor", principal,
                    Guid.NewGuid(), null, Guid.NewGuid(),
                    "T3", "X.Y, X", "{}", null, DateTime.UtcNow.AddMinutes(60)));
            // Approve the second one
            var rows = await db.AiPendingApprovals.ToListAsync();
            rows[1].TryApprove(Guid.NewGuid(), null);
            await db.SaveChangesAsync();
        }

        // Run expire under a fresh scope
        await using (var db = _fx.NewDbContext(tenant))
        {
            var svc = new PendingApprovalService(db, NullLogger<PendingApprovalService>.Instance);
            var n = await svc.ExpireDueAsync(100, default);
            n.Should().Be(1);
        }

        // Verify state
        await using (var db = _fx.NewDbContext(tenant))
        {
            var byTool = (await db.AiPendingApprovals.ToListAsync()).ToDictionary(p => p.ToolName);
            byTool["T1"].Status.Should().Be(PendingApprovalStatus.Expired);
            byTool["T2"].Status.Should().Be(PendingApprovalStatus.Approved);
            byTool["T3"].Status.Should().Be(PendingApprovalStatus.Pending);
        }
    }

    [Fact]
    public async Task Concurrent_Expiration_Calls_Are_Safe()
    {
        var tenant = Guid.NewGuid();
        var assistant = Guid.NewGuid();
        var principal = Guid.NewGuid();

        await using (var db = _fx.NewDbContext(tenant))
        {
            for (var i = 0; i < 20; i++)
                db.AiPendingApprovals.Add(AiPendingApproval.Create(
                    tenant, assistant, "Tutor", principal,
                    Guid.NewGuid(), null, Guid.NewGuid(),
                    $"T{i}", "X.Y, X", "{}", null, DateTime.UtcNow.AddMinutes(-1)));
            await db.SaveChangesAsync();
        }

        // Run two expirations concurrently — total expired must equal 20, no row twice.
        async Task<int> RunOne()
        {
            await using var db = _fx.NewDbContext(tenant);
            var svc = new PendingApprovalService(db, NullLogger<PendingApprovalService>.Instance);
            return await svc.ExpireDueAsync(100, default);
        }

        var (a, b) = (await Task.WhenAll(RunOne(), RunOne())) switch { var arr => (arr[0], arr[1]) };
        (a + b).Should().Be(20);

        await using (var db = _fx.NewDbContext(tenant))
        {
            var pending = await db.AiPendingApprovals.CountAsync(p => p.Status == PendingApprovalStatus.Pending);
            pending.Should().Be(0);
        }
    }
}
```

> **`AiPostgresFixture.NewDbContext`** is the helper from `tests/Starter.Api.Tests/Ai/Retrieval/AiPostgresFixture.cs`. If its current signature requires extra params (test isolation database name, current-user mock), follow the call site of the existing 5d-1 acid tests (`Plan5d1ConcurrentCostCapAcidTests.cs`) for the canonical invocation.

- [ ] **Step 2: Run test + commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Plan5d2ApprovalExpirationAcidTests"
git add -p && git commit -m "test(ai): 5d-2 — Postgres-backed acid tests for atomic expiration + concurrency"
```

---

## Phase H — Acid tests (M1–M6 + W1)

> Six behavior acid tests use `FakeContentModerator` + the in-memory `AiDbContext`. The seventh (W1) is gated on `MODERATION_LIVE_TESTS=1`. All live under `boilerplateBE/tests/Starter.Api.Tests/Ai/AcidTests/Plan5d2*.cs`.

### Task H1: M1 — ChildSafe blocks output (school flagship)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/AcidTests/Plan5d2ChildSafeOutputBlockedAcidTests.cs`

- [ ] **Step 1: Write the acid test**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Runtime;
using Xunit;

namespace Starter.Api.Tests.Ai.AcidTests;

public sealed class Plan5d2ChildSafeOutputBlockedAcidTests
{
    [Fact]
    public async Task ChildSafe_Output_Blocked_Returns_Refusal_And_Persists_Event()
    {
        // Arrange: tenant + ChildSafe assistant + persona; FakeModerator blocks output
        var tenant = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        var opts = new DbContextOptionsBuilder<AiDbContext>().UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        var assistant = AiAssistant.Create(tenant, "Tutor", null, "be safe", Guid.NewGuid());
        assistant.SetSafetyPreset(SafetyPreset.ChildSafe);
        db.AiAssistants.Add(assistant);
        await db.SaveChangesAsync();

        var inner = new StubRuntime(new AgentRunResult(
            AgentRunStatus.Completed, "innocuous-text", Array.Empty<AgentStepEvent>(), 5, 5, null));
        var moderator = new BlockingModerator(blockOnStage: ModerationStage.Output);
        var rt = TestRuntimeBuilder.Wire(db, inner, moderator, SafetyPreset.ChildSafe, ModerationFailureMode.FailClosed);

        // Act
        var sink = new RecordingSink();
        var result = await rt.RunAsync(TestRuntimeBuilder.Ctx(assistant, SafetyPreset.ChildSafe), sink, default);

        // Assert
        result.Status.Should().Be(AgentRunStatus.OutputBlocked);
        result.FinalContent.Should().NotContain("innocuous-text");
        var ev = await db.AiModerationEvents.FirstAsync();
        ev.Stage.Should().Be(ModerationStage.Output);
        ev.Outcome.Should().Be(ModerationOutcome.Blocked);
        ev.Preset.Should().Be(SafetyPreset.ChildSafe);
        // Sink must NOT have received any pre-block deltas (BufferingSink suppression)
        sink.DeltaCount.Should().Be(0); // moderator blocked before release
    }

    private sealed class StubRuntime(AgentRunResult result) : IAiAgentRuntime
    {
        public Task<AgentRunResult> RunAsync(AgentRunContext ctx, IAgentRunSink sink, CancellationToken ct = default)
        {
            sink.OnDeltaAsync("innocuous-text", ct).GetAwaiter().GetResult();
            return Task.FromResult(result);
        }
    }

    private sealed class BlockingModerator(ModerationStage blockOnStage) : IContentModerator
    {
        public Task<ModerationVerdict> ScanAsync(string text, ModerationStage stage, ResolvedSafetyProfile profile, string? language, CancellationToken ct) =>
            Task.FromResult(stage == blockOnStage
                ? ModerationVerdict.Blocked(new Dictionary<string, double> { ["sexual-minors"] = 0.93 }, "always-block:sexual-minors", 5)
                : ModerationVerdict.Allowed(5));
    }

    private sealed class RecordingSink : IAgentRunSink
    {
        public int DeltaCount { get; private set; }
        public Task OnStepStartedAsync(int stepIndex, CancellationToken ct) => Task.CompletedTask;
        public Task OnAssistantMessageAsync(AgentAssistantMessage message, CancellationToken ct) => Task.CompletedTask;
        public Task OnToolCallAsync(AgentToolCallEvent call, CancellationToken ct) => Task.CompletedTask;
        public Task OnToolResultAsync(AgentToolResultEvent result, CancellationToken ct) => Task.CompletedTask;
        public Task OnDeltaAsync(string contentDelta, CancellationToken ct) { DeltaCount++; return Task.CompletedTask; }
        public Task OnStepCompletedAsync(AgentStepEvent step, CancellationToken ct) => Task.CompletedTask;
        public Task OnRunCompletedAsync(AgentRunResult result, CancellationToken ct) => Task.CompletedTask;
    }
}
```

> **`TestRuntimeBuilder`** is a tiny shared helper with the wire-up logic (profile resolver mock, refusal provider stub, etc.). Create `Plan5d2TestRuntimeBuilder.cs` alongside the acid tests; copy the `Wire(...)` and `Ctx(...)` static helpers from `ContentModerationEnforcingAgentRuntimeTests` (Task D2). Reuse across M1–M6.

- [ ] **Step 2: Build, run test, commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Plan5d2ChildSafeOutputBlockedAcidTests"
git add -p && git commit -m "test(ai): 5d-2 — M1 acid: ChildSafe output blocked (school flagship)"
```

---

### Task H2: M2 — Input blocked, no LLM call

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/AcidTests/Plan5d2InputBlockedAcidTests.cs`

- [ ] **Step 1: Test**

```csharp
[Fact]
public async Task Input_Blocked_Skips_Inner_Runtime_No_Cost_Claim_No_Usage_Log()
{
    // Arrange — same wiring as M1 but moderator blocks on Input
    var tenant = Guid.NewGuid();
    // ... setup db, assistant w/ ChildSafe override ...
    var inner = new RecordingRuntime(); // tracks if RunAsync was called
    var moderator = new BlockingModerator(blockOnStage: ModerationStage.Input);
    var rt = TestRuntimeBuilder.Wire(db, inner, moderator, SafetyPreset.ChildSafe, ModerationFailureMode.FailClosed);

    var result = await rt.RunAsync(TestRuntimeBuilder.Ctx(assistant, SafetyPreset.ChildSafe), Mock.Of<IAgentRunSink>(), default);

    result.Status.Should().Be(AgentRunStatus.InputBlocked);
    inner.WasCalled.Should().BeFalse();
    (await db.AiUsageLogs.CountAsync()).Should().Be(0);
    (await db.AiModerationEvents.FirstAsync()).Stage.Should().Be(ModerationStage.Input);
}

private sealed class RecordingRuntime : IAiAgentRuntime
{
    public bool WasCalled { get; private set; }
    public Task<AgentRunResult> RunAsync(AgentRunContext ctx, IAgentRunSink sink, CancellationToken ct = default)
    {
        WasCalled = true;
        return Task.FromResult(new AgentRunResult(AgentRunStatus.Completed, "x", Array.Empty<AgentStepEvent>(), 1, 1, null));
    }
}
```

(Full file structure mirrors H1; condensed here for brevity. The complete file includes the same `using` block, namespace, and class wrapper.)

- [ ] **Step 2: Run + commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Plan5d2InputBlockedAcidTests"
git add -p && git commit -m "test(ai): 5d-2 — M2 acid: input blocked, no usage log, no inner-runtime call"
```

---

### Task H3: M3 — ProfessionalModerated PII redaction (social flagship)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/AcidTests/Plan5d2ProfessionalRedactionAcidTests.cs`

- [ ] **Step 1: Test outline**

```csharp
[Fact]
public async Task ProfessionalModerated_Redacts_PII_In_Output()
{
    // Arrange — assistant w/ ProfessionalModerated; FakeModerator allows; real RegexPiiRedactor
    // Inner runtime returns: "Email me at john@example.com or call +14155552671"
    // Act → Final content has both replaced with [REDACTED]; AiModerationEvent Outcome=Redacted

    // ...wire decorator with new RegexPiiRedactor(...)...
    var inner = new StubRuntime(new AgentRunResult(
        AgentRunStatus.Completed, "Email me at john@example.com or call +14155552671",
        Array.Empty<AgentStepEvent>(), 5, 5, null));
    var moderator = new AlwaysAllowModerator();
    var rt = TestRuntimeBuilder.Wire(db, inner, moderator, SafetyPreset.ProfessionalModerated, ModerationFailureMode.FailClosed, redactPii: true);

    var result = await rt.RunAsync(TestRuntimeBuilder.Ctx(assistant, SafetyPreset.ProfessionalModerated), Mock.Of<IAgentRunSink>(), default);

    result.Status.Should().Be(AgentRunStatus.Completed);
    result.FinalContent.Should().NotContain("john@example.com");
    result.FinalContent.Should().NotContain("+14155552671");
    result.FinalContent.Should().Contain("[REDACTED]");
    (await db.AiModerationEvents.FirstAsync()).Outcome.Should().Be(ModerationOutcome.Redacted);
}
```

- [ ] **Step 2: Run + commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Plan5d2ProfessionalRedactionAcidTests"
git add -p && git commit -m "test(ai): 5d-2 — M3 acid: ProfessionalModerated redacts PII (social flagship)"
```

---

### Task H4: M4 — `[DangerousAction]` approval flow end-to-end

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/AcidTests/Plan5d2DangerousActionApprovalAcidTests.cs`

Covers: dispatcher persists pending row → approve re-dispatches the original command → deny appends denial → expiration auto-denies after 24h. Uses a fake MediatR command (`FakeDeleteAllCommand` from D3 test) so we don't touch real domain commands.

- [ ] **Step 1: Test outline**

```csharp
[Fact]
public async Task Dispatch_Persists_Pending_And_Run_Awaits_Approval()
{
    // Wire dispatcher with the run-context accessor pre-populated;
    // Send a tool call whose CommandType is FakeDeleteAllCommand;
    // Assert: AiPendingApprovals row exists with status=Pending, ToolName="DeleteAll"
}

[Fact]
public async Task Approve_Reissues_Command_Via_Grant_Context()
{
    // Use a real ISender wired to a stub MediatR pipeline that records
    // the inner DangerousActionAttribute check observed `DangerousActionApprovalGrant=true`.
    // Verify status flips to Approved and the original command was Send'd exactly once.
}

[Fact]
public async Task Deny_Without_Reason_Returns_Validation_Failure()
{
    var (db, svc) = MakePendingService();
    var pa = await svc.CreateAsync(/* ... */);
    var handler = new DenyPendingActionCommandHandler(svc, currentUserMock.Object);
    var result = await handler.Handle(new DenyPendingActionCommand(pa.Id, ""), default);
    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Be("PendingApproval.DenyReasonRequired");
}

[Fact]
public async Task Expiration_Job_Tick_Auto_Denies_Past_Due_Pending_Rows()
{
    // Seed expired Pending row; call svc.ExpireDueAsync(100, default); assert status=Expired.
    // (Postgres-backed acid test G2 covers concurrency; this one uses InMemory and skips
    // the FOR UPDATE SKIP LOCKED path — adapt the service to use a soft path on InMemory
    // OR run this assertion only against AiPostgresFixture; pick the latter for simplicity.)
}
```

> **Gating:** the `Expiration_Job_Tick` assertion uses Postgres (FOR UPDATE SKIP LOCKED isn't supported by the EF in-memory provider). Either reuse `AiPostgresFixture` here OR move that single assertion into `Plan5d2ApprovalExpirationAcidTests` (G2) which already runs on Postgres.

- [ ] **Step 2: Run + commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Plan5d2DangerousActionApprovalAcidTests"
git add -p && git commit -m "test(ai): 5d-2 — M4 acid: [DangerousAction] approval flow (create/approve/deny/expire)"
```

---

### Task H5: M5 — Streaming buffering

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/AcidTests/Plan5d2StreamingBufferingAcidTests.cs`

- [ ] **Step 1: Test outline**

```csharp
[Fact]
public async Task ChildSafe_Streaming_Suppresses_Deltas_Until_Moderation_Passes()
{
    // Inner runtime calls sink.OnDeltaAsync("first chunk") and "second chunk" before returning.
    // Decorator wraps with BufferingSink; moderator allows on output.
    // Assert: recording sink received the deltas AFTER moderation completes,
    // not interleaved with the runtime stream.
    var deltaTimes = new List<DateTime>();
    var moderationTime = new TaskCompletionSource<DateTime>();

    var sink = new TimeRecordingSink(deltaTimes);
    var inner = new ChunkingRuntime(); // sleeps 10ms between chunks
    var moderator = new TimingModerator(moderationTime);
    var rt = TestRuntimeBuilder.Wire(db, inner, moderator, SafetyPreset.ChildSafe, ModerationFailureMode.FailClosed);

    var result = await rt.RunAsync(TestRuntimeBuilder.Ctx(assistant, SafetyPreset.ChildSafe, streaming: true), sink, default);

    var firstDeltaTime = deltaTimes.First();
    var modCompleted = await moderationTime.Task;
    firstDeltaTime.Should().BeOnOrAfter(modCompleted, "ChildSafe must not stream until moderation passes");
    result.FinalContent.Should().Be("first chunk + second chunk");
}

[Fact]
public async Task Standard_Streaming_Passes_Deltas_Live()
{
    // Same inner runtime; Standard preset; assert deltas arrive before moderation completes
    // (PassthroughSink — no suppression).
}
```

- [ ] **Step 2: Run + commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Plan5d2StreamingBufferingAcidTests"
git add -p && git commit -m "test(ai): 5d-2 — M5 acid: streaming buffering (Standard live, ChildSafe suppressed)"
```

---

### Task H6: M6 — Provider unavailable: FailOpen vs FailClosed

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/AcidTests/Plan5d2ProviderUnavailableAcidTests.cs`

- [ ] **Step 1: Tests**

```csharp
[Fact]
public async Task ChildSafe_FailClosed_Returns_ProviderUnavailable()
{
    var moderator = new UnavailableModerator();
    var rt = TestRuntimeBuilder.Wire(db, inner, moderator, SafetyPreset.ChildSafe, ModerationFailureMode.FailClosed);
    var result = await rt.RunAsync(TestRuntimeBuilder.Ctx(assistant, SafetyPreset.ChildSafe), Mock.Of<IAgentRunSink>(), default);
    result.Status.Should().Be(AgentRunStatus.ModerationProviderUnavailable);
    inner.Called.Should().BeFalse();
}

[Fact]
public async Task Standard_FailOpen_Allows_With_Warning_Log()
{
    var moderator = new UnavailableModerator();
    var rt = TestRuntimeBuilder.Wire(db, inner, moderator, SafetyPreset.Standard, ModerationFailureMode.FailOpen);
    var result = await rt.RunAsync(TestRuntimeBuilder.Ctx(assistant, SafetyPreset.Standard), Mock.Of<IAgentRunSink>(), default);
    result.Status.Should().Be(AgentRunStatus.Completed);
    inner.Called.Should().BeTrue();
}

private sealed class UnavailableModerator : IContentModerator
{
    public Task<ModerationVerdict> ScanAsync(string text, ModerationStage stage, ResolvedSafetyProfile profile, string? language, CancellationToken ct) =>
        Task.FromResult(ModerationVerdict.Unavailable(0));
}
```

- [ ] **Step 2: Run + commit**

```bash
dotnet build boilerplateBE/Starter.sln
dotnet test boilerplateBE/Starter.sln --filter "FullyQualifiedName~Plan5d2ProviderUnavailableAcidTests"
git add -p && git commit -m "test(ai): 5d-2 — M6 acid: provider unavailable (FailOpen Standard / FailClosed ChildSafe)"
```

---

### Task H7: W1 — OpenAI Moderation wire-compat (gated `MODERATION_LIVE_TESTS=1`)

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/AcidTests/Plan5d2OpenAiModerationWireTests.cs`

Mirrors the RAG eval harness gating pattern. Test runs only when `MODERATION_LIVE_TESTS=1` AND a moderation key is configured (via dotnet-user-secrets or env). Otherwise emits a skip reason via `ITestOutputHelper`.

- [ ] **Step 1: Test**

```csharp
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Services.Moderation;
using Xunit;
using Xunit.Abstractions;

namespace Starter.Api.Tests.Ai.AcidTests;

public sealed class Plan5d2OpenAiModerationWireTests
{
    private readonly ITestOutputHelper _output;
    public Plan5d2OpenAiModerationWireTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Wire_Compat_Live_Call_Returns_Expected_Categories()
    {
        if (Environment.GetEnvironmentVariable("MODERATION_LIVE_TESTS") != "1")
        {
            _output.WriteLine("Skipped: set MODERATION_LIVE_TESTS=1 to run.");
            return;
        }

        var config = new ConfigurationBuilder()
            .AddUserSecrets<Starter.Api.Program>()
            .AddEnvironmentVariables()
            .Build();
        var resolver = new ConfigurationModerationKeyResolver(config);
        if (string.IsNullOrWhiteSpace(resolver.Resolve()))
        {
            _output.WriteLine("Skipped: no moderation key configured in user-secrets / env.");
            return;
        }

        var http = new HttpClientFactory();
        var moderator = new OpenAiContentModerator(resolver, http, NullLogger<OpenAiContentModerator>.Instance);
        var profile = new ResolvedSafetyProfile(
            Starter.Abstractions.Ai.SafetyPreset.Standard, ModerationProvider.OpenAi,
            new Dictionary<string, double> { ["sexual"] = 0.85, ["violence"] = 0.85, ["hate"] = 0.85 },
            Array.Empty<string>(), ModerationFailureMode.FailOpen, false);

        // Deliberately bad strings that OpenAI is well-known to flag in published examples.
        var v1 = await moderator.ScanAsync("explicit violence example", ModerationStage.Output, profile, "en", default);
        v1.Categories.Should().NotBeEmpty();
    }

    private sealed class HttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
```

- [ ] **Step 2: Run (locally with key) + commit**

```bash
# Local verification only:
MODERATION_LIVE_TESTS=1 dotnet test boilerplateBE/Starter.sln \
    --filter "FullyQualifiedName~Plan5d2OpenAiModerationWireTests"
# CI runs the test as a no-op skip until ops opts the gate on.

git add -p && git commit -m "test(ai): 5d-2 — W1 wire-compat: live OpenAI moderation call (MODERATION_LIVE_TESTS=1)"
```

---

## Self-review checklist

Run before opening the PR:

- [ ] All seven acid tests in Phase H pass (M1–M6 in CI; W1 gated locally).
- [ ] `dotnet build boilerplateBE/Starter.sln` clean (no warnings on new files).
- [ ] `dotnet test boilerplateBE/Starter.sln` full pass (excluding W1 in CI).
- [ ] `dotnet ef migrations add Plan5d2 ... && dotnet ef migrations remove ...` succeeds locally; migration is **not** committed.
- [ ] `AIModule.GetPermissions()` lists the four new permissions; default role bindings updated.
- [ ] Communication module has the new `RequiredNotification` rows for the four `ai.agent.approval.*` keys.
- [ ] Spec § coverage check: every locked decision in the spec maps to at least one task here.

## Operational notes

- **Moderation key configuration:** add `AI:Moderation:OpenAi:ApiKey` to dotnet-user-secrets (`dotnet user-secrets set "AI:Moderation:OpenAi:ApiKey" "<key>"` from `boilerplateBE/src/Starter.Api`). Falls back to `AI:Providers:OpenAI:ApiKey` if unset.
- **Tenant opt-in to full moderation logging:** flip `Ai:Moderation:LogAllOutcomes=true` in superadmin settings to log Allowed events too (default: only non-Allowed are persisted).
- **Approval expiration:** default 24h via `Ai:Moderation:ApprovalExpirationHours`; tune per environment.
- **Wire-compat test:** schedule `MODERATION_LIVE_TESTS=1` in a nightly CI job (not per-PR) to catch OpenAI API drift.

---






