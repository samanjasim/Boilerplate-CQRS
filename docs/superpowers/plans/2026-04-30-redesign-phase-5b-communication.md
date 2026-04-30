# Phase 5b Communication — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring all 5 communication pages onto J4 Spectrum tokens with three earned structural changes (DeliveryLog modal→drawer + 7-day status hero, Templates sticky category rail, Channels & Integrations paired card refresh).

**Architecture:** Frontend-heavy redesign with one new BE query handler (`GetDeliveryStatusCountsQuery`) for the DeliveryLog hero. No schema changes. Channels and Integrations counts are client-derived from the existing list endpoints. Reuse the existing communication route/API/query layout, `MetricCard`, `STATUS_BADGE_VARIANT`, `Card variant="elevated"`, `surface-glass` and `brand-halo` utilities, and the shared table/badge primitives before creating local components. All touched visible strings must have EN + AR + KU keys with no `defaultValue` fallback in committed UI code.

**Tech Stack:** .NET 10 / EF Core / xUnit (BE); React 19 + TypeScript 5 + Tailwind 4 + TanStack Query + Radix Dialog + i18next + shadcn/ui (FE).

**Spec:** [`docs/superpowers/specs/2026-04-30-redesign-phase-5b-communication-design.md`](../specs/2026-04-30-redesign-phase-5b-communication-design.md)

**Branch:** `fe/phase-5b-design` (already created off `origin/main` post-5a; spec already committed).

**Cadence:** Land commits directly on the branch. One commit per task (no per-task feature branches), matching Phase 0–5a cadence.

**Real names** (verified during plan stage; differ from spec where noted):
- BE permissions are in `Starter.Module.Communication.Constants.CommunicationPermissions` (not `Permissions.Communication.*`). All policy strings are e.g. `CommunicationPermissions.ViewDeliveryLog`.
- BE controllers are split per resource (`DeliveryLogsController`, `ChannelConfigsController`, `MessageTemplatesController`, `TriggerRulesController`, `IntegrationConfigsController`, etc.) — there is no single `CommunicationController`. The new endpoint lives on `DeliveryLogsController`.
- The `DeliveryStatus` enum is `{ Pending = 0, Queued = 1, Sending = 2, Delivered = 3, Failed = 4, Bounced = 5 }`.
- FE communication API client is at `boilerplateFE/src/features/communication/api/communication.api.ts`; query hooks at `communication.queries.ts`.
- FE query keys for communication live under `queryKeys.communication.*` in `boilerplateFE/src/lib/query/keys.ts`. The existing nested groups are `templates`, `triggerRules`, `events`, `integrationConfigs`, `preferences`, `required`, `deliveryLogs`, `dashboard`, plus a top-level `channelConfigs` group (search before adding).
- FE existing translations live in `boilerplateFE/src/i18n/locales/{en,ar,ku}/translation.json` under the root `communication` object.
- FE `MetricCard` is at `boilerplateFE/src/components/common/MetricCard.tsx` with `tone?: 'default' | 'active' | 'destructive'` and an `eyebrow` line.
- FE `--state-warn-{bg,fg,border}` and `--shell-header-h` semantic tokens were added in Phase 5a (`Task 1`); Task 1 below verifies their presence rather than re-adds them.
- FE `STATUS_BADGE_VARIANT` lives in `boilerplateFE/src/constants/status.ts` (re-exported from `@/constants`). Phase 5a already extended it for workflow values; Task 16 below verifies and extends for communication enum values.
- The FE has no `<Sheet>` / `<SideDrawer>` primitive yet — Task 5 introduces it as a thin wrapper over Radix Dialog (already a dep via the existing `<Dialog>`).
- The `TimeProvider` is currently registered only by `WorkflowModule` (`AddSingleton(TimeProvider.System)`); Task 2 adds the same registration to `CommunicationModule` so the new handler can take a `TimeProvider` constructor parameter and tests can swap in `FakeTimeProvider`.

---

## Task 1: Foundation — verify Phase 5a tokens are still present

**Files:**
- Verify: `boilerplateFE/src/styles/index.css` — `--state-warn-bg` / `--state-warn-fg` / `--state-warn-border` semantic tokens
- Verify: `boilerplateFE/src/components/layout/MainLayout/*` — `--shell-header-h` CSS var exposure

- [ ] **Step 1: Verify `--state-warn-*` tokens** — from repo root run:

```bash
grep -n "state-warn" boilerplateFE/src/styles/index.css
```

Expected: at least 6 hits (3 tokens × 2 themes — `:root` and `.dark`). If absent, copy the snippet from Phase 5a Task 1 step 2 ([`docs/superpowers/plans/2026-04-29-redesign-phase-5a-workflow.md`](2026-04-29-redesign-phase-5a-workflow.md)) and add it; otherwise skip.

- [ ] **Step 2: Verify `--shell-header-h` exposure** — from repo root run:

```bash
grep -rn "shell-header-h" boilerplateFE/src/styles/index.css boilerplateFE/src/components/layout
```

Expected: at least one hit on a layout shell wrapper (`MainLayout`, `FloatingShell`, etc.). If absent, see Phase 5a Task 1 step 4 and add the inline style on the layout root.

- [ ] **Step 3: Confirm no changes** — if both verifications passed, the working tree is clean and the task is complete with no commit. If either was added, run `cd boilerplateFE && npm run build` (must pass; no behavior change yet) and commit:

```bash
git add boilerplateFE/src/styles/index.css boilerplateFE/src/components/layout
git commit -m "feat(fe/styles): backfill state-warn tokens / shell-header-h var

Phase 5b reuses these foundations from 5a; this commit only fires if
they were missing on the branch base."
```

---

## Task 2: BE — `GetDeliveryStatusCountsQuery` handler + endpoint + tests

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Communication/Application/DTOs/DeliveryStatusCountsDto.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Communication/Application/Queries/GetDeliveryStatusCounts/GetDeliveryStatusCountsQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Communication/Application/Queries/GetDeliveryStatusCounts/GetDeliveryStatusCountsQueryHandler.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Communication/CommunicationModule.cs` (register `TimeProvider.System` if absent)
- Modify: `boilerplateBE/src/modules/Starter.Module.Communication/Controllers/DeliveryLogsController.cs` (add endpoint)
- Create: `boilerplateBE/tests/Starter.Api.Tests/Communication/GetDeliveryStatusCountsQueryHandlerTests.cs`

- [ ] **Step 1: Write the DTO** — `boilerplateBE/src/modules/Starter.Module.Communication/Application/DTOs/DeliveryStatusCountsDto.cs`:

```csharp
namespace Starter.Module.Communication.Application.DTOs;

public sealed record DeliveryStatusCountsDto(
    int Delivered,
    int Failed,
    int Pending,
    int Bounced,
    int WindowDays);
```

- [ ] **Step 2: Write the query record** — `boilerplateBE/src/modules/Starter.Module.Communication/Application/Queries/GetDeliveryStatusCounts/GetDeliveryStatusCountsQuery.cs`:

```csharp
using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetDeliveryStatusCounts;

public sealed record GetDeliveryStatusCountsQuery(int WindowDays = 7)
    : IRequest<Result<DeliveryStatusCountsDto>>;
```

- [ ] **Step 3: Write the failing test scaffold** — `boilerplateBE/tests/Starter.Api.Tests/Communication/GetDeliveryStatusCountsQueryHandlerTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Starter.Module.Communication.Application.Queries.GetDeliveryStatusCounts;
using Starter.Module.Communication.Domain.Entities;
using Starter.Module.Communication.Domain.Enums;
using Starter.Module.Communication.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Communication;

public sealed class GetDeliveryStatusCountsQueryHandlerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly CommunicationDbContext _db;
    private readonly FakeTimeProvider _clock = new(Now);

    public GetDeliveryStatusCountsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<CommunicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CommunicationDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Handle_BucketsByStatusWithinWindow()
    {
        var withinWindow = Now.AddDays(-3).UtcDateTime;
        var outsideWindow = Now.AddDays(-30).UtcDateTime;

        _db.DeliveryLogs.AddRange(
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Delivered, createdAt: withinWindow),
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Delivered, createdAt: withinWindow),
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Failed,    createdAt: withinWindow),
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Pending,   createdAt: withinWindow),
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Queued,    createdAt: withinWindow),
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Sending,   createdAt: withinWindow),
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Bounced,   createdAt: withinWindow),
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Delivered, createdAt: outsideWindow));
        await _db.SaveChangesAsync();

        var sut = new GetDeliveryStatusCountsQueryHandler(_db, _clock);
        var result = await sut.Handle(new GetDeliveryStatusCountsQuery(WindowDays: 7), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Delivered.Should().Be(2);
        result.Value.Failed.Should().Be(1);
        result.Value.Pending.Should().Be(3); // Pending + Queued + Sending
        result.Value.Bounced.Should().Be(1);
        result.Value.WindowDays.Should().Be(7);
    }

    [Theory]
    [InlineData(0, 1)]    // Below floor → clamp to 1
    [InlineData(91, 90)]  // Above ceiling → clamp to 90
    [InlineData(7, 7)]    // Pass through
    public async Task Handle_ClampsWindowDaysToValidRange(int requested, int expected)
    {
        var sut = new GetDeliveryStatusCountsQueryHandler(_db, _clock);
        var result = await sut.Handle(new GetDeliveryStatusCountsQuery(WindowDays: requested), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.WindowDays.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_RowAtExactWindowBoundaryIncluded()
    {
        // Row created exactly 7 days ago should still count when WindowDays = 7.
        _db.DeliveryLogs.Add(
            DeliveryLogTestFactory.WithStatus(DeliveryStatus.Delivered, createdAt: Now.AddDays(-7).UtcDateTime));
        await _db.SaveChangesAsync();

        var sut = new GetDeliveryStatusCountsQueryHandler(_db, _clock);
        var result = await sut.Handle(new GetDeliveryStatusCountsQuery(WindowDays: 7), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Delivered.Should().Be(1);
    }
}

internal static class DeliveryLogTestFactory
{
    public static DeliveryLog WithStatus(DeliveryStatus status, DateTime createdAt)
    {
        var log = new DeliveryLog
        {
            Id = Guid.NewGuid(),
            TemplateName = "test.template",
            Channel = NotificationChannel.Email,
            Status = status,
            AttemptCount = 1,
            CreatedAt = createdAt,
        };
        return log;
    }
}
```

> **Note:** the `DeliveryLog` entity may have additional required fields. After Step 4 fails to compile, inspect the existing `DeliveryLog` class in `boilerplateBE/src/modules/Starter.Module.Communication/Domain/Entities/DeliveryLog.cs` and extend the `WithStatus` factory with whatever required properties exist (e.g. `RecipientUserId`, `IntegrationType`, `TenantId`). Use plausible defaults that don't affect the bucket logic. Do **not** modify the entity itself.

- [ ] **Step 4: Run tests to verify they fail** — from repo root run:

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~GetDeliveryStatusCountsQueryHandlerTests" --no-restore 2>&1 | tail -30
```

Expected: build error or `Handle_*` tests fail because `GetDeliveryStatusCountsQueryHandler` doesn't exist yet. If the `DeliveryLogTestFactory.WithStatus` builder needs more required entity fields, fix that first (see Step 3 note) before proceeding.

- [ ] **Step 5: Write the handler** — `boilerplateBE/src/modules/Starter.Module.Communication/Application/Queries/GetDeliveryStatusCounts/GetDeliveryStatusCountsQueryHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Enums;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetDeliveryStatusCounts;

internal sealed class GetDeliveryStatusCountsQueryHandler(
    CommunicationDbContext context,
    TimeProvider timeProvider)
    : IRequestHandler<GetDeliveryStatusCountsQuery, Result<DeliveryStatusCountsDto>>
{
    private const int MinWindowDays = 1;
    private const int MaxWindowDays = 90;

    public async Task<Result<DeliveryStatusCountsDto>> Handle(
        GetDeliveryStatusCountsQuery request,
        CancellationToken cancellationToken)
    {
        var clampedWindow = Math.Clamp(request.WindowDays, MinWindowDays, MaxWindowDays);
        var cutoff = timeProvider.GetUtcNow().AddDays(-clampedWindow).UtcDateTime;

        var counts = await context.DeliveryLogs
            .AsNoTracking()
            .Where(d => d.CreatedAt >= cutoff)
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byStatus = counts.ToDictionary(c => c.Status, c => c.Count);

        var dto = new DeliveryStatusCountsDto(
            Delivered: byStatus.GetValueOrDefault(DeliveryStatus.Delivered),
            Failed:    byStatus.GetValueOrDefault(DeliveryStatus.Failed),
            Pending:   byStatus.GetValueOrDefault(DeliveryStatus.Pending)
                       + byStatus.GetValueOrDefault(DeliveryStatus.Queued)
                       + byStatus.GetValueOrDefault(DeliveryStatus.Sending),
            Bounced:   byStatus.GetValueOrDefault(DeliveryStatus.Bounced),
            WindowDays: clampedWindow);

        return Result.Success(dto);
    }
}
```

- [ ] **Step 6: Register `TimeProvider.System` in CommunicationModule** — open `boilerplateBE/src/modules/Starter.Module.Communication/CommunicationModule.cs` and add the registration alongside the other singletons in `ConfigureServices`. If a global registration already exists at the host level, this is a no-op (idempotent for the System provider). Add this line near `AddSingleton<ITemplateEngine, StubbleTemplateEngine>()`:

```csharp
services.AddSingleton(TimeProvider.System);
```

- [ ] **Step 7: Add the controller endpoint** — modify `boilerplateBE/src/modules/Starter.Module.Communication/Controllers/DeliveryLogsController.cs`. Add the using statement for the new query namespace at the top alongside the existing `using` for `GetDeliveryLogs`:

```csharp
using Starter.Module.Communication.Application.Queries.GetDeliveryStatusCounts;
```

Then add a new action method below the existing `GetById` (at the bottom of the controller class):

```csharp
/// <summary>
/// Get delivery status counts grouped over a recent window (default 7 days).
/// </summary>
[HttpGet("status-counts")]
[Authorize(Policy = CommunicationPermissions.ViewDeliveryLog)]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<IActionResult> GetStatusCounts(
    [FromQuery] int windowDays = 7,
    CancellationToken ct = default)
{
    var result = await Mediator.Send(new GetDeliveryStatusCountsQuery(windowDays), ct);
    return HandleResult(result);
}
```

- [ ] **Step 8: Run tests to verify they pass**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~GetDeliveryStatusCountsQueryHandlerTests" 2>&1 | tail -10
```

Expected: 5 tests pass (1 fact + 3 theory rows + 1 boundary fact).

- [ ] **Step 9: Build the full solution to make sure nothing else broke**

```bash
cd boilerplateBE && dotnet build Starter.sln 2>&1 | tail -5
```

Expected: Build succeeded; 0 errors, 0 warnings.

- [ ] **Step 10: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Communication boilerplateBE/tests/Starter.Api.Tests/Communication
git commit -m "feat(be/communication): add delivery status-counts query and endpoint

Returns Delivered/Failed/Pending/Bounced counts over a recent window
(default 7 days, clamped to [1, 90]). Pending bucket folds Queued and
Sending. Tenant-scoped via the existing CommunicationDbContext global
filter. Drives the Phase 5b DeliveryLogPage hero strip."
```

---

## Task 3: FE — API endpoint, types, query keys, query hook

**Files:**
- Modify: `boilerplateFE/src/config/api.config.ts` (add `STATUS_COUNTS` to `DELIVERY_LOGS`)
- Modify: `boilerplateFE/src/types/communication.types.ts` (add `DeliveryStatusCountsDto`)
- Modify: `boilerplateFE/src/lib/query/keys.ts` (add `statusCounts` under `deliveryLogs`)
- Modify: `boilerplateFE/src/features/communication/api/communication.api.ts` (add API method)
- Modify: `boilerplateFE/src/features/communication/api/communication.queries.ts` (add hook + invalidate from resend mutation)

- [ ] **Step 1: Add endpoint constant** — modify `boilerplateFE/src/config/api.config.ts`. Inside the `DELIVERY_LOGS` block (around lines 192–196):

```ts
DELIVERY_LOGS: {
  LIST: '/DeliveryLogs',
  DETAIL: (id: string) => `/DeliveryLogs/${id}`,
  RESEND: (id: string) => `/DeliveryLogs/${id}/resend`,
  STATUS_COUNTS: '/DeliveryLogs/status-counts',
},
```

- [ ] **Step 2: Add the type** — modify `boilerplateFE/src/types/communication.types.ts`. Append at end of file:

```ts
export interface DeliveryStatusCountsDto {
  delivered: number;
  failed: number;
  pending: number;
  bounced: number;
  windowDays: number;
}
```

- [ ] **Step 3: Add the query key** — modify `boilerplateFE/src/lib/query/keys.ts`. Inside the existing `deliveryLogs` group (around lines 186–190), add `statusCounts`:

```ts
deliveryLogs: {
  all: ['communication', 'delivery-logs'] as const,
  list: (params?: Record<string, unknown>) => ['communication', 'delivery-logs', 'list', params] as const,
  detail: (id: string) => ['communication', 'delivery-logs', 'detail', id] as const,
  statusCounts: (windowDays?: number) =>
    ['communication', 'delivery-logs', 'status-counts', windowDays ?? 7] as const,
},
```

- [ ] **Step 4: Add the API method** — modify `boilerplateFE/src/features/communication/api/communication.api.ts`. Add `DeliveryStatusCountsDto` to the imports at the top (alongside `DeliveryLogDto`, `DeliveryLogDetailDto`):

```ts
import type {
  // … existing imports …
  DeliveryStatusCountsDto,
} from '@/types/communication.types';
```

Add the method to the exported object near the existing `getDeliveryLog` method:

```ts
getDeliveryStatusCounts: (windowDays = 7) =>
  apiClient
    .get<{ data: DeliveryStatusCountsDto }>(API_ENDPOINTS.COMMUNICATION.DELIVERY_LOGS.STATUS_COUNTS, {
      params: { windowDays },
    })
    .then((r) => r.data),
```

- [ ] **Step 5: Add the query hook** — modify `boilerplateFE/src/features/communication/api/communication.queries.ts`. Add the hook near `useDeliveryLogs` / `useDeliveryLog`:

```ts
export function useDeliveryStatusCounts(windowDays = 7) {
  return useQuery({
    queryKey: queryKeys.communication.deliveryLogs.statusCounts(windowDays),
    queryFn: () => communicationApi.getDeliveryStatusCounts(windowDays),
    staleTime: 30_000,
  });
}
```

- [ ] **Step 6: Invalidate status-counts from resend** — locate `useResendDelivery` in the same file. In its `onSuccess`, alongside the existing `deliveryLogs.all` invalidation, add:

```ts
queryClient.invalidateQueries({ queryKey: queryKeys.communication.deliveryLogs.all });
```

If the `deliveryLogs.all` invalidation already cascades to children (which it does — TanStack Query treats it as a prefix), no extra line is needed. Double-check by reading the existing `useResendDelivery` body. If it does explicit list-only invalidation (not `.all`), add the new key explicitly.

- [ ] **Step 7: Type-check**

```bash
cd boilerplateFE && npm run build
```

Expected: 0 errors.

- [ ] **Step 8: Commit**

```bash
git add boilerplateFE/src/config/api.config.ts boilerplateFE/src/types/communication.types.ts boilerplateFE/src/lib/query/keys.ts boilerplateFE/src/features/communication/api
git commit -m "feat(fe/communication): add delivery status-counts API client and hook

Wires the new BE endpoint into the communication API client +
TanStack Query hook. 30-second staleTime matches the workflow
status-count hero strips (5a precedent). Resend mutation already
invalidates the deliveryLogs.all prefix so the hero refreshes too."
```

---

## Task 4: FE — i18n keys for the cluster

**Files:**
- Modify: `boilerplateFE/src/i18n/locales/en/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ar/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ku/translation.json`

The snippets below are shown as `jsonc` only so existing-key comments can be explanatory. The actual locale files are strict JSON: do not paste comments or trailing commas.

- [ ] **Step 1: Locate the existing `communication` object** in each locale file:

```bash
rg -n '"communication": \{' boilerplateFE/src/i18n/locales/*/translation.json
```

Add keys into the existing nested objects; do not create separate namespace files.

- [ ] **Step 2: Add EN keys** — append into the existing `communication` namespace in `boilerplateFE/src/i18n/locales/en/translation.json`:

```jsonc
{
  "communication": {
    // … existing keys …
    "deliveryLog": {
      // … existing keys …
      "statusCounts": {
        "delivered": "Delivered",
        "deliveredEyebrow": "Successfully sent",
        "failed": "Failed",
        "failedEyebrow": "Needs attention",
        "pending": "Pending",
        "pendingEyebrow": "In flight",
        "bounced": "Bounced",
        "bouncedEyebrow": "Address rejected"
      },
      "window": {
        "last7Days": "Last 7 days"
      },
      "drawer": {
        "title": "Delivery details",
        "attemptCount_one": "{{count}} attempt",
        "attemptCount_other": "{{count}} attempts"
      }
    },
    "channels": {
      // … existing keys …
      "statusCounts": {
        "active": "Active",
        "activeEyebrow": "Currently delivering",
        "configured": "Configured",
        "configuredEyebrow": "Set up but disabled",
        "errored": "Errored",
        "erroredEyebrow": "Needs attention"
      },
      "lastTested": {
        "justNow": "Tested {{relative}}",
        "today": "Tested {{relative}}",
        "thisWeek": "Tested {{relative}}",
        "older": "Tested {{relative}}",
        "never": "Never tested"
      }
    },
    "integrations": {
      // … existing keys …
      "statusCounts": {
        "active": "Active",
        "activeEyebrow": "Currently delivering",
        "configured": "Configured",
        "configuredEyebrow": "Set up but disabled",
        "errored": "Errored",
        "erroredEyebrow": "Needs attention"
      }
    },
    "templates": {
      // … existing keys …
      "categoryCount_one": "{{count}} template",
      "categoryCount_other": "{{count}} templates"
    },
    "triggerRules": {
      // … existing keys …
      "channelSequence": {
        "connector": "then"
      }
    },
    "providers": {
      "unknown": "Provider {{name}}"
    }
  }
}
```

- [ ] **Step 3: Add AR keys** (Arabic) — same structure in `boilerplateFE/src/i18n/locales/ar/translation.json`:

```jsonc
{
  "communication": {
    "deliveryLog": {
      "statusCounts": {
        "delivered": "تم التسليم",
        "deliveredEyebrow": "تم الإرسال بنجاح",
        "failed": "فشل",
        "failedEyebrow": "يتطلب الانتباه",
        "pending": "قيد الانتظار",
        "pendingEyebrow": "قيد الإرسال",
        "bounced": "ارتد",
        "bouncedEyebrow": "تم رفض العنوان"
      },
      "window": {
        "last7Days": "آخر 7 أيام"
      },
      "drawer": {
        "title": "تفاصيل التسليم",
        "attemptCount_one": "محاولة واحدة",
        "attemptCount_other": "{{count}} محاولات"
      }
    },
    "channels": {
      "statusCounts": {
        "active": "نشطة",
        "activeEyebrow": "تعمل حالياً",
        "configured": "مهيأة",
        "configuredEyebrow": "تم إعدادها ولكنها معطلة",
        "errored": "بها أخطاء",
        "erroredEyebrow": "يتطلب الانتباه"
      },
      "lastTested": {
        "justNow": "آخر اختبار {{relative}}",
        "today": "آخر اختبار {{relative}}",
        "thisWeek": "آخر اختبار {{relative}}",
        "older": "آخر اختبار {{relative}}",
        "never": "لم تُختبر مطلقاً"
      }
    },
    "integrations": {
      "statusCounts": {
        "active": "نشطة",
        "activeEyebrow": "تعمل حالياً",
        "configured": "مهيأة",
        "configuredEyebrow": "تم إعدادها ولكنها معطلة",
        "errored": "بها أخطاء",
        "erroredEyebrow": "يتطلب الانتباه"
      }
    },
    "templates": {
      "categoryCount_one": "قالب واحد",
      "categoryCount_other": "{{count}} قوالب"
    },
    "triggerRules": {
      "channelSequence": {
        "connector": "ثم"
      }
    },
    "providers": {
      "unknown": "مزود {{name}}"
    }
  }
}
```

- [ ] **Step 4: Add KU keys** (Kurdish/Sorani) — same structure in `boilerplateFE/src/i18n/locales/ku/translation.json`:

```jsonc
{
  "communication": {
    "deliveryLog": {
      "statusCounts": {
        "delivered": "گەیشتوو",
        "deliveredEyebrow": "بە سەرکەوتوویی نێردرا",
        "failed": "شکستی هێنا",
        "failedEyebrow": "پێویستی بە سەرنجە",
        "pending": "چاوەڕوان",
        "pendingEyebrow": "لە ناردندایە",
        "bounced": "گەڕاوەتەوە",
        "bouncedEyebrow": "ناونیشان ڕەتکرایەوە"
      },
      "window": {
        "last7Days": "٧ ڕۆژی ڕابردوو"
      },
      "drawer": {
        "title": "وردەکارییەکانی گەیاندن",
        "attemptCount_one": "یەک هەوڵ",
        "attemptCount_other": "{{count}} هەوڵ"
      }
    },
    "channels": {
      "statusCounts": {
        "active": "چالاک",
        "activeEyebrow": "ئێستا دەنێرێت",
        "configured": "ڕێکخراو",
        "configuredEyebrow": "ڕێکخراوە بەڵام ناچالاکە",
        "errored": "هەڵە تێدایە",
        "erroredEyebrow": "پێویستی بە سەرنجە"
      },
      "lastTested": {
        "justNow": "تاقیکراوەتەوە {{relative}}",
        "today": "تاقیکراوەتەوە {{relative}}",
        "thisWeek": "تاقیکراوەتەوە {{relative}}",
        "older": "تاقیکراوەتەوە {{relative}}",
        "never": "هەرگیز تاقینەکراوەتەوە"
      }
    },
    "integrations": {
      "statusCounts": {
        "active": "چالاک",
        "activeEyebrow": "ئێستا دەنێرێت",
        "configured": "ڕێکخراو",
        "configuredEyebrow": "ڕێکخراوە بەڵام ناچالاکە",
        "errored": "هەڵە تێدایە",
        "erroredEyebrow": "پێویستی بە سەرنجە"
      }
    },
    "templates": {
      "categoryCount_one": "یەک قاڵب",
      "categoryCount_other": "{{count}} قاڵب"
    },
    "triggerRules": {
      "channelSequence": {
        "connector": "پاشان"
      }
    },
    "providers": {
      "unknown": "دابینکەری {{name}}"
    }
  }
}
```

- [ ] **Step 5: Validate JSON** — run:

```bash
for f in boilerplateFE/src/i18n/locales/{en,ar,ku}/translation.json; do
  node -e "JSON.parse(require('fs').readFileSync('$f','utf8'))" && echo "$f OK"
done
```

Expected: 3 lines printing `… OK`. If a parse error fires, find and fix the trailing comma or comment.

- [ ] **Step 6: Type-check + build**

```bash
cd boilerplateFE && npm run build
```

Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add boilerplateFE/src/i18n/locales
git commit -m "feat(fe/communication): add Phase 5b i18n keys (EN/AR/KU)

Hero status counts for delivery log + channels + integrations,
delivery drawer copy, last-tested chip relative-time labels,
template category count plurals, trigger-rule channel-sequence
connector, provider fallback label."
```

---

## Task 5: FE — `<SideDrawer>` primitive (`src/components/ui/sheet.tsx`)

**Files:**
- Create: `boilerplateFE/src/components/ui/sheet.tsx`

The new primitive wraps `@radix-ui/react-dialog` (already a dep via the existing `<Dialog>` primitive at `boilerplateFE/src/components/ui/dialog.tsx`). Three width variants and a bottom-sheet variant cover all 5b uses.

- [ ] **Step 1: Confirm Radix dialog is already a dep** —

```bash
grep -n "@radix-ui/react-dialog" boilerplateFE/package.json
```

Expected: a line in `dependencies`. If absent, the existing `<Dialog>` primitive could not work — re-investigate before continuing.

- [ ] **Step 2: Read the existing Dialog primitive** to mirror its structure (forwardRefs, classnames, animation conventions):

```bash
cat boilerplateFE/src/components/ui/dialog.tsx
```

Take note of how the overlay and content are composed. The new `<Sheet>` follows the same shape with a different `position` and `transform`.

- [ ] **Step 3: Create the primitive** — `boilerplateFE/src/components/ui/sheet.tsx`:

```tsx
import * as React from 'react';
import * as DialogPrimitive from '@radix-ui/react-dialog';
import { X } from 'lucide-react';
import { cn } from '@/lib/utils';

const Sheet = DialogPrimitive.Root;
const SheetTrigger = DialogPrimitive.Trigger;
const SheetClose = DialogPrimitive.Close;
const SheetPortal = DialogPrimitive.Portal;

const SheetOverlay = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Overlay>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Overlay>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Overlay
    ref={ref}
    className={cn(
      'fixed inset-0 z-50 bg-black/40 backdrop-blur-[2px]',
      'data-[state=open]:animate-in data-[state=closed]:animate-out',
      'data-[state=open]:fade-in-0 data-[state=closed]:fade-out-0',
      className,
    )}
    {...props}
  />
));
SheetOverlay.displayName = 'SheetOverlay';

type SheetSide = 'end' | 'bottom';
type SheetWidth = 'sm' | 'md' | 'lg';

const SIDE_CLASSES: Record<SheetSide, string> = {
  end: 'inset-y-0 inset-inline-end-0 h-full border-l rtl:border-l-0 rtl:border-r data-[state=open]:translate-x-0 data-[state=closed]:translate-x-full rtl:data-[state=closed]:-translate-x-full',
  bottom:
    'inset-x-0 bottom-0 max-h-[90vh] rounded-t-2xl border-t data-[state=open]:translate-y-0 data-[state=closed]:translate-y-full',
};

const WIDTH_CLASSES: Record<SheetWidth, string> = {
  sm: 'sm:w-[400px]',
  md: 'sm:w-[480px]',
  lg: 'sm:w-[560px]',
};

interface SheetContentProps
  extends React.ComponentPropsWithoutRef<typeof DialogPrimitive.Content> {
  side?: SheetSide;
  width?: SheetWidth;
  /** Show the X close button in the top-end corner. Default true. */
  showClose?: boolean;
}

const SheetContent = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Content>,
  SheetContentProps
>(
  (
    { side = 'end', width = 'lg', showClose = true, className, children, ...props },
    ref,
  ) => (
    <SheetPortal>
      <SheetOverlay />
      <DialogPrimitive.Content
        ref={ref}
        className={cn(
          'fixed z-50 flex flex-col gap-4 bg-background shadow-xl border',
          'transition-transform duration-300 ease-out',
          'p-6 w-full',
          SIDE_CLASSES[side],
          side === 'end' && WIDTH_CLASSES[width],
          'data-[state=open]:animate-in data-[state=closed]:animate-out',
          className,
        )}
        {...props}
      >
        {children}
        {showClose && (
          <SheetClose
            className={cn(
              'absolute top-4 inline-end-4 rounded-md p-1.5 opacity-70 transition-opacity',
              'hover:opacity-100 focus:outline-none focus:ring-2 focus:ring-ring',
            )}
          >
            <X className="h-4 w-4" />
            <span className="sr-only">Close</span>
          </SheetClose>
        )}
      </DialogPrimitive.Content>
    </SheetPortal>
  ),
);
SheetContent.displayName = 'SheetContent';

const SheetHeader = ({
  className,
  ...props
}: React.HTMLAttributes<HTMLDivElement>) => (
  <div
    className={cn('flex flex-col gap-1.5 text-start', className)}
    {...props}
  />
);
SheetHeader.displayName = 'SheetHeader';

const SheetTitle = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Title>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Title>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Title
    ref={ref}
    className={cn('text-lg font-semibold text-foreground', className)}
    {...props}
  />
));
SheetTitle.displayName = 'SheetTitle';

const SheetDescription = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Description>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Description>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Description
    ref={ref}
    className={cn('text-sm text-muted-foreground', className)}
    {...props}
  />
));
SheetDescription.displayName = 'SheetDescription';

const SheetBody = ({
  className,
  ...props
}: React.HTMLAttributes<HTMLDivElement>) => (
  <div
    className={cn('flex-1 overflow-y-auto -mx-6 px-6', className)}
    {...props}
  />
);
SheetBody.displayName = 'SheetBody';

export {
  Sheet,
  SheetTrigger,
  SheetClose,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
  SheetBody,
};
```

- [ ] **Step 4: Type-check + build**

```bash
cd boilerplateFE && npm run build
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/components/ui/sheet.tsx
git commit -m "feat(fe/ui): add Sheet primitive (side drawer + bottom sheet)

Wraps @radix-ui/react-dialog with end-anchored and bottom-anchored
variants. Three width tiers (sm=400 / md=480 / lg=560). RTL-safe via
inset-inline-end-0 and the rtl: translate-x variant. Used by Phase 5b
DeliveryDetailDrawer; designed to absorb future side-peek surfaces."
```

---

## Task 6: FE — `<DeliveryDetailDrawer>` (replace `DeliveryDetailModal`)

**Files:**
- Create: `boilerplateFE/src/features/communication/components/DeliveryDetailDrawer.tsx`
- Delete: `boilerplateFE/src/features/communication/components/DeliveryDetailModal.tsx`

The drawer reuses the existing modal's body content verbatim — only the wrapper changes from `<Dialog>` to `<Sheet>`.

- [ ] **Step 1: Create the drawer** — `boilerplateFE/src/features/communication/components/DeliveryDetailDrawer.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { format } from 'date-fns';
import { RefreshCw, CheckCircle2, XCircle, Clock, AlertTriangle } from 'lucide-react';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
  SheetBody,
} from '@/components/ui/sheet';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { Spinner } from '@/components/ui/spinner';
import { usePermissions } from '@/hooks';
import { PERMISSIONS, STATUS_BADGE_VARIANT } from '@/constants';
import { useDeliveryLog, useResendDelivery } from '../api';
import type { DeliveryStatus } from '@/types/communication.types';

const STATUS_ICONS: Record<DeliveryStatus, typeof CheckCircle2> = {
  Pending: Clock,
  Queued: Clock,
  Sending: Clock,
  Delivered: CheckCircle2,
  Failed: XCircle,
  Bounced: AlertTriangle,
};

interface DeliveryDetailDrawerProps {
  id: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function DeliveryDetailDrawer({ id, open, onOpenChange }: DeliveryDetailDrawerProps) {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const canResend = hasPermission(PERMISSIONS.Communication.Resend);

  const { data, isLoading } = useDeliveryLog(id);
  const resendMutation = useResendDelivery();

  const log = data?.data;

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="end" width="lg">
        <SheetHeader>
          <SheetTitle>{t('communication.deliveryLog.drawer.title')}</SheetTitle>
          {log && (
            <SheetDescription>
              {t('communication.deliveryLog.drawer.attemptCount', { count: log.attempts.length })}
            </SheetDescription>
          )}
        </SheetHeader>

        <SheetBody>
          {isLoading || !log ? (
            <div className="flex justify-center py-8"><Spinner /></div>
          ) : (
            <div className="space-y-6">
              {/* Summary */}
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <p className="text-sm text-muted-foreground">{t('communication.deliveryLog.columns.recipient')}</p>
                  <p className="text-sm font-medium">{log.recipientAddress ?? '-'}</p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">{t('communication.deliveryLog.columns.template')}</p>
                  <p className="text-sm font-medium">{log.templateName}</p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">{t('communication.deliveryLog.columns.channel')}</p>
                  <p className="text-sm font-medium">
                    {log.channel
                      ? t(`communication.channels.channelNames.${log.channel}`)
                      : log.integrationType ?? '-'}
                    {log.provider ? ` (${log.provider})` : ''}
                  </p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">{t('communication.deliveryLog.columns.status')}</p>
                  <Badge variant={STATUS_BADGE_VARIANT[log.status]}>
                    {t(`communication.deliveryLog.statusLabels.${log.status}`)}
                  </Badge>
                </div>
                {log.subject && (
                  <div className="col-span-2">
                    <p className="text-sm text-muted-foreground">Subject</p>
                    <p className="text-sm font-medium">{log.subject}</p>
                  </div>
                )}
                {log.bodyPreview && (
                  <div className="col-span-2">
                    <p className="text-sm text-muted-foreground">Body Preview</p>
                    <p className="text-sm text-muted-foreground bg-muted rounded-lg p-3 mt-1 whitespace-pre-wrap max-h-32 overflow-y-auto">
                      {log.bodyPreview}
                    </p>
                  </div>
                )}
                {log.errorMessage && (
                  <div className="col-span-2">
                    <p className="text-sm text-destructive font-medium">Error</p>
                    <p className="text-sm text-destructive/80">{log.errorMessage}</p>
                  </div>
                )}
              </div>

              {/* Resend */}
              {canResend && (log.status === 'Failed' || log.status === 'Bounced') && (
                <Button
                  variant="outline"
                  disabled={resendMutation.isPending}
                  onClick={() => resendMutation.mutate(log.id)}
                >
                  <RefreshCw className="h-4 w-4 ltr:mr-2 rtl:ml-2" />
                  {t('communication.deliveryLog.resend')}
                </Button>
              )}

              <Separator />

              {/* Attempts Timeline */}
              <div>
                <h3 className="text-sm font-semibold mb-4">
                  {t('communication.deliveryLog.detail.attempts')}
                </h3>
                {log.attempts.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    {t('communication.deliveryLog.detail.noAttempts')}
                  </p>
                ) : (
                  <div className="space-y-4">
                    {log.attempts.map((attempt) => {
                      const Icon = STATUS_ICONS[attempt.status];
                      return (
                        <div key={attempt.id} className="flex gap-3 rounded-lg border p-3">
                          <Icon
                            className={`h-5 w-5 mt-0.5 shrink-0 ${
                              attempt.status === 'Delivered'
                                ? 'text-[var(--color-emerald-500)]'
                                : attempt.status === 'Failed' || attempt.status === 'Bounced'
                                ? 'text-destructive'
                                : 'text-muted-foreground'
                            }`}
                          />
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center justify-between gap-2">
                              <span className="text-sm font-medium">
                                {t('communication.deliveryLog.detail.attemptNumber', { number: attempt.attemptNumber })}
                              </span>
                              <span className="text-xs text-muted-foreground">
                                {format(new Date(attempt.attemptedAt), 'MMM d, HH:mm:ss')}
                              </span>
                            </div>
                            <div className="flex items-center gap-2 mt-1">
                              <Badge variant={STATUS_BADGE_VARIANT[attempt.status]} className="text-xs">
                                {t(`communication.deliveryLog.statusLabels.${attempt.status}`)}
                              </Badge>
                              {attempt.provider && (
                                <span className="text-xs text-muted-foreground">{attempt.provider}</span>
                              )}
                              {attempt.durationMs !== null && (
                                <span className="text-xs text-muted-foreground">
                                  {attempt.durationMs < 1000
                                    ? `${attempt.durationMs}ms`
                                    : `${(attempt.durationMs / 1000).toFixed(1)}s`}
                                </span>
                              )}
                            </div>
                            {attempt.errorMessage && (
                              <p className="text-xs text-destructive mt-1">{attempt.errorMessage}</p>
                            )}
                            {attempt.providerResponse && (
                              <details className="mt-1">
                                <summary className="text-xs text-muted-foreground cursor-pointer">
                                  {t('communication.deliveryLog.detail.providerResponse')}
                                </summary>
                                <pre className="text-xs text-muted-foreground bg-muted rounded p-2 mt-1 overflow-x-auto max-h-24">
                                  {attempt.providerResponse}
                                </pre>
                              </details>
                            )}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            </div>
          )}
        </SheetBody>
      </SheetContent>
    </Sheet>
  );
}
```

- [ ] **Step 2: Verify no other imports** — before deleting the modal file, confirm only `DeliveryLogPage` references it:

```bash
grep -rn "DeliveryDetailModal" boilerplateFE/src --include="*.tsx" --include="*.ts"
```

Expected: hits only in `DeliveryDetailModal.tsx` itself and `DeliveryLogPage.tsx`. If anything else imports it, update those references first (most likely the `index.tsx` barrel — change to `DeliveryDetailDrawer`).

- [ ] **Step 3: Delete the old modal**

```bash
rm boilerplateFE/src/features/communication/components/DeliveryDetailModal.tsx
```

- [ ] **Step 4: Update DeliveryLogPage to import the drawer instead of the modal** — modify `boilerplateFE/src/features/communication/pages/DeliveryLogPage.tsx`. Change the import line:

```ts
import { DeliveryDetailDrawer } from '../components/DeliveryDetailDrawer';
```

And the JSX usage at the bottom of the file:

```tsx
{selectedId && (
  <DeliveryDetailDrawer
    id={selectedId}
    open={!!selectedId}
    onOpenChange={(open) => {
      if (!open) setSelectedId(null);
    }}
  />
)}
```

- [ ] **Step 5: Type-check + build**

```bash
cd boilerplateFE && npm run build
```

Expected: 0 errors. (DeliveryLogPage now uses the drawer; structural redesign of the page itself happens in Task 8.)

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/features/communication
git commit -m "refactor(fe/communication): swap DeliveryDetailModal for Drawer

Same body content; the wrapper changes from centered Dialog to
end-anchored Sheet. Operators sweeping failures keep the list
visible while the drawer content swaps. Old modal removed."
```

---

## Task 7: FE — `<DeliveryLogStatusHero>` component

**Files:**
- Create: `boilerplateFE/src/features/communication/components/DeliveryLogStatusHero.tsx`

- [ ] **Step 1: Create the component** — `boilerplateFE/src/features/communication/components/DeliveryLogStatusHero.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/common';
import { cn } from '@/lib/utils';
import { useDeliveryStatusCounts } from '../api/communication.queries';

export function DeliveryLogStatusHero() {
  const { t } = useTranslation();
  const { data, isLoading } = useDeliveryStatusCounts(7);

  if (isLoading || !data) {
    return null;
  }

  const total = data.delivered + data.failed + data.pending + data.bounced;
  if (total === 0) {
    return null;
  }

  const showDelivered = data.delivered > 0;
  const showFailed = data.failed > 0;
  const showPending = data.pending > 0;
  const showBounced = data.bounced > 0;

  const visibleCount = [showDelivered, showFailed, showPending, showBounced].filter(Boolean).length;

  return (
    <div className="space-y-2 mb-6">
      <div
        className={cn(
          'grid gap-4',
          visibleCount === 1 && 'sm:grid-cols-1',
          visibleCount === 2 && 'sm:grid-cols-2',
          visibleCount === 3 && 'sm:grid-cols-2 lg:grid-cols-3',
          visibleCount === 4 && 'sm:grid-cols-2 lg:grid-cols-4',
        )}
      >
        {showDelivered && (
          <MetricCard
            label={t('communication.deliveryLog.statusCounts.delivered')}
            eyebrow={t('communication.deliveryLog.statusCounts.deliveredEyebrow')}
            value={data.delivered}
            tone="active"
            emphasis
          />
        )}
        {showFailed && (
          <MetricCard
            label={t('communication.deliveryLog.statusCounts.failed')}
            eyebrow={t('communication.deliveryLog.statusCounts.failedEyebrow')}
            value={data.failed}
            tone="destructive"
            emphasis={data.failed > 0}
          />
        )}
        {showPending && (
          <MetricCard
            label={t('communication.deliveryLog.statusCounts.pending')}
            eyebrow={t('communication.deliveryLog.statusCounts.pendingEyebrow')}
            value={data.pending}
          />
        )}
        {showBounced && (
          <MetricCard
            label={t('communication.deliveryLog.statusCounts.bounced')}
            eyebrow={t('communication.deliveryLog.statusCounts.bouncedEyebrow')}
            value={data.bounced}
            tone="destructive"
            emphasis={data.bounced > 0}
          />
        )}
      </div>
      <p className="text-xs text-muted-foreground">
        {t('communication.deliveryLog.window.last7Days')}
      </p>
    </div>
  );
}
```

- [ ] **Step 2: Type-check**

```bash
cd boilerplateFE && npm run build
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add boilerplateFE/src/features/communication/components/DeliveryLogStatusHero.tsx
git commit -m "feat(fe/communication): add DeliveryLogStatusHero component

Four-card hero with collapse-when-zero behavior over a 7-day window.
Mirrors InboxStatusHero (Phase 5a). 'Last 7 days' caption keeps the
window unambiguous so the strip never reads as all-time."
```

---

## Task 8: FE — Wire hero + row stripe + drawer into `DeliveryLogPage`

**Files:**
- Modify: `boilerplateFE/src/features/communication/pages/DeliveryLogPage.tsx`

- [ ] **Step 1: Add the row stripe helper** — at the top of `DeliveryLogPage.tsx` (after the existing imports), add:

```ts
import type { DeliveryStatus } from '@/types/communication.types';

const ROW_STRIPE_BY_STATUS: Record<DeliveryStatus, string> = {
  Delivered: 'border-s-[var(--color-emerald-500)]',
  Failed: 'border-s-destructive',
  Bounced: 'border-s-destructive',
  Pending: 'border-s-[var(--active-bg)]',
  Queued: 'border-s-[var(--active-bg)]',
  Sending: 'border-s-[var(--active-bg)]',
};
```

`border-s-*` is Tailwind's logical inline-start utility (RTL-safe). If the project pins an older Tailwind 4 version that lacks `border-s-*`, use `border-l rtl:border-l-0 rtl:border-r` plus the color class.

- [ ] **Step 2: Add the hero import** at the top:

```ts
import { DeliveryLogStatusHero } from '../components/DeliveryLogStatusHero';
```

- [ ] **Step 3: Render the hero** — modify the JSX directly under `<PageHeader>`:

```tsx
<PageHeader
  title={t('communication.deliveryLog.title')}
  subtitle={t('communication.deliveryLog.subtitle')}
/>

<DeliveryLogStatusHero />

{/* Filters */}
<div className="flex flex-wrap items-center gap-3">
  …
</div>
```

- [ ] **Step 4: Apply the row stripe** — locate the `<TableRow>` inside the `logs.map((log) => (` block. Add `className` props that combine the existing `cursor-pointer` with the stripe class:

```tsx
<TableRow
  key={log.id}
  className={cn(
    'cursor-pointer border-s-[3px]',
    ROW_STRIPE_BY_STATUS[log.status] ?? 'border-s-transparent',
  )}
  onClick={() => setSelectedId(log.id)}
>
```

You'll need to add `cn` to the imports if it isn't present:

```ts
import { cn } from '@/lib/utils';
```

- [ ] **Step 5: Type-check + build**

```bash
cd boilerplateFE && npm run build
```

Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/features/communication/pages/DeliveryLogPage.tsx
git commit -m "feat(fe/communication): wire hero + row stripe into DeliveryLogPage

4-card hero with 'Last 7 days' caption above the filter row. Each
table row gains a 3px leading-edge stripe colored by status:
emerald (Delivered), destructive (Failed/Bounced), copper amber
(Pending/Queued/Sending). RTL-safe via border-inline-start. Drawer
swap from Task 6 is now exercised through this list."
```

---

## Task 9: FE — `<ProviderLogo>` component + provider logo SVG map

**Files:**
- Create: `boilerplateFE/src/features/communication/components/providerLogos.ts` (SVG string map)
- Create: `boilerplateFE/src/features/communication/components/ProviderLogo.tsx` (component)

The 13 providers are split across two BE-side enums (`ChannelProvider` for channel configs, `IntegrationType` for integrations). The FE component accepts either string and looks up in a single map.

- [ ] **Step 1: Create the SVG map** — `boilerplateFE/src/features/communication/components/providerLogos.ts`:

```ts
/**
 * Inline SVG strings for known channel providers and integration types.
 *
 * Logos are intentionally minimal monochrome marks (use currentColor) so they
 * tint with the surrounding text and adapt to light/dark themes without
 * per-theme variants. For brand-faithful logos, swap individual entries with
 * vendor-supplied SVG marks while keeping the (24, 24) viewBox contract.
 */

export const PROVIDER_LOGOS: Record<string, string> = {
  // Email
  Smtp: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="5" width="18" height="14" rx="2"/><path d="m3 7 9 6 9-6"/></svg>',
  SendGrid: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M3 3h9v9H3zM12 12h9v9h-9z" opacity=".7"/><path d="M12 3h9v9h-9zM3 12h9v9H3z"/></svg>',
  Ses: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M5 4h14l-1 4H6zM4 9h16l-1 11H5z" opacity=".85"/></svg>',
  // SMS / Voice
  Twilio: '<svg viewBox="0 0 24 24" fill="currentColor"><circle cx="12" cy="12" r="9" opacity=".15"/><circle cx="9" cy="9" r="1.6"/><circle cx="15" cy="9" r="1.6"/><circle cx="9" cy="15" r="1.6"/><circle cx="15" cy="15" r="1.6"/></svg>',
  // Push
  Fcm: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M6 4h12l-2 7 3 5-4-1-1 4-3-3-3 3-1-4-4 1 3-5z"/></svg>',
  Apns: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M16 13c-.7 1.5-2 2.6-3.5 3-1.5-.4-2.8-1.5-3.5-3-1-2 0-5 2-5.5.7-.2 1.3 0 2 .3.7-.3 1.3-.5 2-.3 2 .5 3 3.5 2 5.5zM13 4c-.6.6-1.5 1-2 1.7-.5 0-1-.5-1-1.2.5-.8 1.5-1.5 2-1.5.5 0 1 .3 1 1z"/></svg>',
  // WhatsApp
  TwilioWhatsApp: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2a10 10 0 0 0-8 16l-2 4 4-2a10 10 0 1 0 6-18zm5 13c-.5 1-2 2-3 2-1.5 0-4-2-6-4s-4-4.5-4-6c0-1 1-2.5 2-3l1 1-1 2 1 2 2 2 2 1 2-1 1 1-1 1z"/></svg>',
  MetaWhatsApp: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2a10 10 0 0 0-8 16l-2 4 4-2a10 10 0 1 0 6-18zm5 13c-.5 1-2 2-3 2-1.5 0-4-2-6-4s-4-4.5-4-6c0-1 1-2.5 2-3l1 1-1 2 1 2 2 2 2 1 2-1 1 1-1 1z"/></svg>',
  // Realtime
  Ably: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 3 3 21h18zM12 9l4 8H8z"/></svg>',
  // Team integrations
  Slack: '<svg viewBox="0 0 24 24" fill="currentColor"><rect x="9" y="3" width="3" height="9" rx="1.5"/><rect x="3" y="12" width="9" height="3" rx="1.5"/><rect x="12" y="9" width="3" height="9" rx="1.5"/><rect x="12" y="15" width="9" height="3" rx="1.5" opacity=".7"/></svg>',
  Telegram: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="m22 3-9 17-2-7-8-3z" opacity=".85"/><path d="m22 3-11 9 2 8z"/></svg>',
  Discord: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M19 5a16 16 0 0 0-4-1l-.5 1A14 14 0 0 0 9.5 5L9 4a16 16 0 0 0-4 1c-2 3-3 7-2 11 1.5 1 3 1.7 5 2l1-2c-1-.3-2-.7-3-1.3a8 8 0 0 0 14 0c-1 .6-2 1-3 1.3l1 2c2-.3 3.5-1 5-2 1-4 0-8-2-11zM10 14a1.6 1.6 0 1 1 0-3 1.6 1.6 0 0 1 0 3zm4 0a1.6 1.6 0 1 1 0-3 1.6 1.6 0 0 1 0 3z"/></svg>',
  MicrosoftTeams: '<svg viewBox="0 0 24 24" fill="currentColor"><rect x="2" y="6" width="11" height="12" rx="1"/><text x="7.5" y="15" fill="var(--background)" font-size="9" font-weight="700" text-anchor="middle">T</text><circle cx="17" cy="9" r="3"/><rect x="14" y="13" width="6" height="6" rx="2"/></svg>',
};

export type KnownProvider = keyof typeof PROVIDER_LOGOS;

export function isKnownProvider(name: string): name is KnownProvider {
  return name in PROVIDER_LOGOS;
}
```

- [ ] **Step 2: Create the component** — `boilerplateFE/src/features/communication/components/ProviderLogo.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/utils';
import { PROVIDER_LOGOS, isKnownProvider } from './providerLogos';

interface ProviderLogoProps {
  provider: string;
  size?: 'sm' | 'md';
  className?: string;
}

const SIZE_CLASSES: Record<NonNullable<ProviderLogoProps['size']>, string> = {
  sm: 'size-5',
  md: 'size-8',
};

export function ProviderLogo({ provider, size = 'md', className }: ProviderLogoProps) {
  const { t } = useTranslation();

  if (isKnownProvider(provider)) {
    const svg = PROVIDER_LOGOS[provider];
    return (
      <span
        role="img"
        aria-label={provider}
        className={cn(
          'inline-flex items-center justify-center rounded-md bg-[var(--active-bg)]/40 text-[var(--tinted-fg)]',
          SIZE_CLASSES[size],
          className,
        )}
        // Logo strings are static, hardcoded SVG with no user input — safe to inject
        // eslint-disable-next-line react/no-danger
        dangerouslySetInnerHTML={{ __html: svg }}
      />
    );
  }

  const initial = provider.trim().charAt(0).toUpperCase() || '?';
  return (
    <span
      role="img"
      aria-label={t('communication.providers.unknown', { name: provider })}
      className={cn(
        'inline-flex items-center justify-center rounded-md bg-[var(--active-bg)] text-[var(--active-text)] font-semibold',
        SIZE_CLASSES[size],
        size === 'sm' ? 'text-xs' : 'text-sm',
        className,
      )}
    >
      {initial}
    </span>
  );
}
```

- [ ] **Step 3: Type-check**

```bash
cd boilerplateFE && npm run build
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/src/features/communication/components/ProviderLogo.tsx boilerplateFE/src/features/communication/components/providerLogos.ts
git commit -m "feat(fe/communication): add ProviderLogo + 13-provider SVG map

Inline monochrome marks (currentColor) for the 9 channel providers
and 4 integration types. Falls back to a tinted-initial chip for
unknown provider strings, with an aria-label generated from the
providers.unknown i18n key. Marks are intentionally minimal — swap
in vendor-supplied SVGs as licensing allows."
```

---

## Task 10: FE — `<ChannelConfigCard>` component

**Files:**
- Create: `boilerplateFE/src/features/communication/utils/lastTested.ts` (shared age-tinted chip helper for Channels + Integrations)
- Create: `boilerplateFE/src/features/communication/components/ChannelConfigCard.tsx`

- [ ] **Step 1: Create the shared lastTested helper** — `boilerplateFE/src/features/communication/utils/lastTested.ts`:

```ts
import { formatDistanceToNowStrict, differenceInHours, differenceInDays } from 'date-fns';

export type LastTestedTone = 'fresh' | 'today' | 'week' | 'older' | 'never';

export interface LastTestedState {
  tone: LastTestedTone;
  /** Display string, e.g. "5 minutes ago", or null when never tested. */
  label: string | null;
  /** Tailwind class string for the chip background + text. */
  chipClass: string;
}

const CHIP_CLASS_BY_TONE: Record<LastTestedTone, string> = {
  fresh:
    'bg-[var(--color-emerald-500)]/10 text-[var(--color-emerald-700)] dark:text-[var(--color-emerald-300)]',
  today: 'bg-muted text-muted-foreground',
  week:
    'bg-[var(--state-warn-bg)] text-[var(--state-warn-fg)] border border-[var(--state-warn-border)]',
  older: 'bg-muted text-muted-foreground',
  never: 'bg-muted text-muted-foreground',
};

export function deriveLastTestedState(
  lastTestedAt: string | null | undefined,
  now: Date = new Date(),
): LastTestedState {
  if (!lastTestedAt) {
    return { tone: 'never', label: null, chipClass: CHIP_CLASS_BY_TONE.never };
  }

  const tested = new Date(lastTestedAt);
  const hours = differenceInHours(now, tested);
  const days = differenceInDays(now, tested);

  let tone: LastTestedTone;
  if (hours < 1) tone = 'fresh';
  else if (hours < 24) tone = 'today';
  else if (days < 7) tone = 'week';
  else tone = 'older';

  return {
    tone,
    label: formatDistanceToNowStrict(tested, { addSuffix: true }),
    chipClass: CHIP_CLASS_BY_TONE[tone],
  };
}
```

- [ ] **Step 2: Create the card** — `boilerplateFE/src/features/communication/components/ChannelConfigCard.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { Pencil, Trash2, Send, Star, AlertCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { STATUS_BADGE_VARIANT } from '@/constants/status';
import { ProviderLogo } from './ProviderLogo';
import { deriveLastTestedState } from '../utils/lastTested';
import type { ChannelConfigDto } from '@/types/communication.types';

interface ChannelConfigCardProps {
  config: ChannelConfigDto;
  canManage: boolean;
  onTest: () => void;
  onEdit: () => void;
  onSetDefault: () => void;
  onDelete: () => void;
  isTestPending: boolean;
  isSetDefaultPending: boolean;
}

export function ChannelConfigCard({
  config,
  canManage,
  onTest,
  onEdit,
  onSetDefault,
  onDelete,
  isTestPending,
  isSetDefaultPending,
}: ChannelConfigCardProps) {
  const { t } = useTranslation();
  const lastTested = deriveLastTestedState(config.lastTestedAt);
  const isErrored = config.status === 'Error';

  return (
    <Card variant="elevated">
      <CardHeader className="pb-3">
        <div className="flex items-start gap-3">
          <ProviderLogo provider={config.provider} size="md" />
          <div className="flex-1 min-w-0">
            <CardTitle className="text-base flex items-center gap-2">
              <span className="truncate">{config.displayName}</span>
              {config.isDefault && (
                <Star
                  className="h-4 w-4 fill-amber-400 text-amber-400 brand-halo shrink-0"
                  aria-label="Default channel"
                />
              )}
            </CardTitle>
            <p className="text-sm text-muted-foreground">{config.provider}</p>
          </div>
          <Badge
            variant={STATUS_BADGE_VARIANT[config.status] ?? 'secondary'}
            className="shrink-0"
          >
            {isErrored && <AlertCircle className="h-3 w-3 ltr:mr-1 rtl:ml-1" />}
            {t(`communication.channels.status.${config.status}`)}
          </Badge>
        </div>
      </CardHeader>
      <CardContent>
        <div className="space-y-3">
          <div className={cn('inline-flex items-center rounded-md px-2 py-0.5 text-xs', lastTested.chipClass)}>
            {lastTested.label
              ? t(`communication.channels.lastTested.${toneToKey(lastTested.tone)}`, { relative: lastTested.label })
              : t('communication.channels.lastTested.never')}
          </div>

          {canManage && (
            <div className="flex gap-1 pt-1">
              <Button
                variant="ghost"
                size="sm"
                title={t('communication.channels.testButton')}
                onClick={onTest}
                disabled={isTestPending}
              >
                <Send className="h-4 w-4" />
              </Button>
              <Button variant="ghost" size="sm" onClick={onEdit}>
                <Pencil className="h-4 w-4" />
              </Button>
              {!config.isDefault && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={onSetDefault}
                  disabled={isSetDefaultPending}
                  title="Set as default"
                >
                  <Star className="h-4 w-4" />
                </Button>
              )}
              <Button variant="ghost" size="sm" onClick={onDelete}>
                <Trash2 className="h-4 w-4 text-destructive" />
              </Button>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

function toneToKey(tone: ReturnType<typeof deriveLastTestedState>['tone']): string {
  switch (tone) {
    case 'fresh': return 'justNow';
    case 'today': return 'today';
    case 'week': return 'thisWeek';
    case 'older': return 'older';
    case 'never': return 'never';
  }
}
```

- [ ] **Step 3: Verify `<Card>` supports `variant="elevated"`** — read the existing Card primitive:

```bash
grep -n "variant" boilerplateFE/src/components/ui/card.tsx
```

Expected: a `variant?: 'solid' | 'glass' | 'elevated'` (or similar) prop with hover-lift styling. The CLAUDE.md project rules document `Card` accepting `variant`. If absent, the variant must already exist (per the project memory) or be added in a separate prep task — re-investigate before continuing.

- [ ] **Step 4: Type-check**

```bash
cd boilerplateFE && npm run build
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/features/communication/components/ChannelConfigCard.tsx boilerplateFE/src/features/communication/utils/lastTested.ts
git commit -m "feat(fe/communication): add ChannelConfigCard + lastTested helper

Elevated card with provider logo header, default-star with brand-halo
glow, status pill (AlertCircle prefix on Error), and an age-tinted
last-tested chip (fresh/today/week/older/never thresholds). The
lastTested helper is shared with the upcoming IntegrationConfigCard."
```

---

## Task 11: FE — `<ChannelsStatusHero>` + wire into `ChannelsPage`

**Files:**
- Create: `boilerplateFE/src/features/communication/components/ChannelsStatusHero.tsx`
- Modify: `boilerplateFE/src/features/communication/pages/ChannelsPage.tsx`

- [ ] **Step 1: Create the hero** — `boilerplateFE/src/features/communication/components/ChannelsStatusHero.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/common';
import { cn } from '@/lib/utils';
import type { ChannelConfigDto } from '@/types/communication.types';

interface ChannelsStatusHeroProps {
  configs: ChannelConfigDto[];
}

export function ChannelsStatusHero({ configs }: ChannelsStatusHeroProps) {
  const { t } = useTranslation();

  const active = configs.filter((c) => c.status === 'Active').length;
  const configured = configs.filter((c) => c.status === 'Inactive').length;
  const errored = configs.filter((c) => c.status === 'Error').length;

  if (active + configured + errored === 0) {
    return null;
  }

  const showActive = active > 0;
  const showConfigured = configured > 0;
  const showErrored = errored > 0;

  const visibleCount = [showActive, showConfigured, showErrored].filter(Boolean).length;

  return (
    <div
      className={cn(
        'mb-6 grid gap-4',
        visibleCount === 1 && 'sm:grid-cols-1',
        visibleCount === 2 && 'sm:grid-cols-2',
        visibleCount === 3 && 'sm:grid-cols-2 lg:grid-cols-3',
      )}
    >
      {showActive && (
        <MetricCard
          label={t('communication.channels.statusCounts.active')}
          eyebrow={t('communication.channels.statusCounts.activeEyebrow')}
          value={active}
          tone="active"
          emphasis
        />
      )}
      {showConfigured && (
        <MetricCard
          label={t('communication.channels.statusCounts.configured')}
          eyebrow={t('communication.channels.statusCounts.configuredEyebrow')}
          value={configured}
        />
      )}
      {showErrored && (
        <MetricCard
          label={t('communication.channels.statusCounts.errored')}
          eyebrow={t('communication.channels.statusCounts.erroredEyebrow')}
          value={errored}
          tone="destructive"
          emphasis
        />
      )}
    </div>
  );
}
```

- [ ] **Step 2: Wire into ChannelsPage** — modify `boilerplateFE/src/features/communication/pages/ChannelsPage.tsx`. Add imports at the top:

```ts
import { ChannelsStatusHero } from '../components/ChannelsStatusHero';
import { ChannelConfigCard } from '../components/ChannelConfigCard';
```

Replace the inline `<Card>` … `</Card>` block inside the `channelConfigs.map((cfg) => (` loop with a `<ChannelConfigCard>` usage:

```tsx
{channelConfigs.map((cfg) => (
  <ChannelConfigCard
    key={cfg.id}
    config={cfg}
    canManage={canManage}
    onTest={() => testMutation.mutate(cfg.id)}
    onEdit={() => {
      setEditTarget(cfg);
      setSetupOpen(true);
    }}
    onSetDefault={() => setDefaultMutation.mutate(cfg.id)}
    onDelete={() => setDeleteTarget(cfg)}
    isTestPending={testMutation.isPending}
    isSetDefaultPending={setDefaultMutation.isPending}
  />
))}
```

Render `<ChannelsStatusHero>` between `<PageHeader>` and the existing `configs.length === 0 ? …` ternary:

```tsx
<PageHeader … />

<ChannelsStatusHero configs={configs} />

{configs.length === 0 ? (
  …
)}
```

Remove now-unused imports (`Card`, `CardContent`, `CardHeader`, `CardTitle`, `Mail`, `Smartphone`, `Bell`, `MessageCircle`, `Inbox`, `formatDistanceToNow`, `Pencil`, `Trash2`, `Send`) — `npm run build` will flag any leftovers.

- [ ] **Step 3: Type-check + build**

```bash
cd boilerplateFE && npm run build
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/src/features/communication
git commit -m "feat(fe/communication): wire status hero + ChannelConfigCard into ChannelsPage

3-card hero (Active/Configured/Errored) above the grouped sections,
client-derived from the existing useChannelConfigs query. Each card
renders the new ChannelConfigCard primitive with provider logo,
default-star halo, age-tinted last-tested chip, and AlertCircle
icon on Error pills."
```

---

## Task 12: FE — `<IntegrationConfigCard>` component + wire into `IntegrationsPage`

**Files:**
- Create: `boilerplateFE/src/features/communication/components/IntegrationsStatusHero.tsx`
- Create: `boilerplateFE/src/features/communication/components/IntegrationConfigCard.tsx`
- Modify: `boilerplateFE/src/features/communication/pages/IntegrationsPage.tsx`

- [ ] **Step 1: Create the hero** — `boilerplateFE/src/features/communication/components/IntegrationsStatusHero.tsx`. Same shape as `ChannelsStatusHero`, just typed for `IntegrationConfigDto`:

```tsx
import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/common';
import { cn } from '@/lib/utils';
import type { IntegrationConfigDto } from '@/types/communication.types';

interface IntegrationsStatusHeroProps {
  configs: IntegrationConfigDto[];
}

export function IntegrationsStatusHero({ configs }: IntegrationsStatusHeroProps) {
  const { t } = useTranslation();

  const active = configs.filter((c) => c.status === 'Active').length;
  const configured = configs.filter((c) => c.status === 'Inactive').length;
  const errored = configs.filter((c) => c.status === 'Error').length;

  if (active + configured + errored === 0) {
    return null;
  }

  const showActive = active > 0;
  const showConfigured = configured > 0;
  const showErrored = errored > 0;
  const visibleCount = [showActive, showConfigured, showErrored].filter(Boolean).length;

  return (
    <div
      className={cn(
        'mb-6 grid gap-4',
        visibleCount === 1 && 'sm:grid-cols-1',
        visibleCount === 2 && 'sm:grid-cols-2',
        visibleCount === 3 && 'sm:grid-cols-2 lg:grid-cols-3',
      )}
    >
      {showActive && (
        <MetricCard
          label={t('communication.integrations.statusCounts.active')}
          eyebrow={t('communication.integrations.statusCounts.activeEyebrow')}
          value={active}
          tone="active"
          emphasis
        />
      )}
      {showConfigured && (
        <MetricCard
          label={t('communication.integrations.statusCounts.configured')}
          eyebrow={t('communication.integrations.statusCounts.configuredEyebrow')}
          value={configured}
        />
      )}
      {showErrored && (
        <MetricCard
          label={t('communication.integrations.statusCounts.errored')}
          eyebrow={t('communication.integrations.statusCounts.erroredEyebrow')}
          value={errored}
          tone="destructive"
          emphasis
        />
      )}
    </div>
  );
}
```

- [ ] **Step 2: Create the card** — `boilerplateFE/src/features/communication/components/IntegrationConfigCard.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { Pencil, Trash2, Send, AlertCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { STATUS_BADGE_VARIANT } from '@/constants/status';
import { ProviderLogo } from './ProviderLogo';
import { deriveLastTestedState } from '../utils/lastTested';
import type { IntegrationConfigDto } from '@/types/communication.types';

interface IntegrationConfigCardProps {
  config: IntegrationConfigDto;
  canManage: boolean;
  onTest: () => void;
  onEdit: () => void;
  onDelete: () => void;
  isTestPending: boolean;
}

export function IntegrationConfigCard({
  config,
  canManage,
  onTest,
  onEdit,
  onDelete,
  isTestPending,
}: IntegrationConfigCardProps) {
  const { t } = useTranslation();
  const lastTested = deriveLastTestedState(config.lastTestedAt);
  const isErrored = config.status === 'Error';

  return (
    <Card variant="elevated">
      <CardHeader className="pb-3">
        <div className="flex items-start gap-3">
          <ProviderLogo provider={config.integrationType} size="md" />
          <div className="flex-1 min-w-0">
            <CardTitle className="text-base">
              <span className="truncate">{config.displayName}</span>
            </CardTitle>
            <p className="text-sm text-muted-foreground">
              {t(`communication.integrations.types.${config.integrationType}`)}
            </p>
          </div>
          <Badge
            variant={STATUS_BADGE_VARIANT[config.status] ?? 'secondary'}
            className="shrink-0"
          >
            {isErrored && <AlertCircle className="h-3 w-3 ltr:mr-1 rtl:ml-1" />}
            {config.status}
          </Badge>
        </div>
      </CardHeader>
      <CardContent>
        <div className="space-y-3">
          <div className={cn('inline-flex items-center rounded-md px-2 py-0.5 text-xs', lastTested.chipClass)}>
            {lastTested.label
              ? t(`communication.channels.lastTested.${toneToKey(lastTested.tone)}`, { relative: lastTested.label })
              : t('communication.channels.lastTested.never')}
          </div>

          {canManage && (
            <div className="flex gap-1 pt-1">
              <Button
                variant="ghost"
                size="sm"
                title={t('communication.integrations.testButton')}
                onClick={onTest}
                disabled={isTestPending}
              >
                <Send className="h-4 w-4" />
              </Button>
              <Button variant="ghost" size="sm" onClick={onEdit}>
                <Pencil className="h-4 w-4" />
              </Button>
              <Button variant="ghost" size="sm" onClick={onDelete}>
                <Trash2 className="h-4 w-4 text-destructive" />
              </Button>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

function toneToKey(tone: ReturnType<typeof deriveLastTestedState>['tone']): string {
  switch (tone) {
    case 'fresh': return 'justNow';
    case 'today': return 'today';
    case 'week': return 'thisWeek';
    case 'older': return 'older';
    case 'never': return 'never';
  }
}
```

- [ ] **Step 3: Wire into IntegrationsPage** — modify `boilerplateFE/src/features/communication/pages/IntegrationsPage.tsx`. Add imports:

```ts
import { IntegrationsStatusHero } from '../components/IntegrationsStatusHero';
import { IntegrationConfigCard } from '../components/IntegrationConfigCard';
```

Replace the inline `<Card>` … `</Card>` block inside the `typeConfigs.map((cfg) => (` loop with:

```tsx
{typeConfigs.map((cfg) => (
  <IntegrationConfigCard
    key={cfg.id}
    config={cfg}
    canManage={canManage}
    onTest={() => testMutation.mutate(cfg.id)}
    onEdit={() => {
      setEditTarget(cfg);
      setSetupOpen(true);
    }}
    onDelete={() => setDeleteTarget(cfg)}
    isTestPending={testMutation.isPending}
  />
))}
```

Render `<IntegrationsStatusHero>` between `<PageHeader>` and the empty-state ternary:

```tsx
<PageHeader … />

<IntegrationsStatusHero configs={configs} />

{configs.length === 0 ? ( … )}
```

Remove now-unused imports (`Card`, `CardContent`, `CardHeader`, `CardTitle`, `formatDistanceToNow`, `Pencil`, `Trash2`, `Send`) — `npm run build` will flag any leftovers.

- [ ] **Step 4: Type-check + build**

```bash
cd boilerplateFE && npm run build
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/features/communication
git commit -m "feat(fe/communication): wire status hero + IntegrationConfigCard into IntegrationsPage

Symmetric to ChannelsPage (Task 11). 3-card hero, elevated card,
provider logo, AlertCircle-on-error pill, age-tinted last-tested
chip. No default-star (integrations don't have a default concept).
ChannelConfigCard and IntegrationConfigCard now feel like sibling
surfaces."
```

---

## Task 13: FE — `<TemplateCategoryRail>` component (rail + chips variants)

**Files:**
- Create: `boilerplateFE/src/features/communication/components/TemplateCategoryRail.tsx`

- [ ] **Step 1: Create the component** — `boilerplateFE/src/features/communication/components/TemplateCategoryRail.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { cn } from '@/lib/utils';

export interface TemplateCategoryRailProps {
  categories: Array<{ name: string; count: number }>;
  /** undefined = "All categories" pseudo-row active */
  selectedCategory: string | undefined;
  onSelect: (category: string | undefined) => void;
  totalCount: number;
  variant?: 'rail' | 'chips';
  className?: string;
}

export function TemplateCategoryRail({
  categories,
  selectedCategory,
  onSelect,
  totalCount,
  variant = 'rail',
  className,
}: TemplateCategoryRailProps) {
  const { t } = useTranslation();

  if (variant === 'chips') {
    return (
      <div className={cn('flex flex-wrap gap-2', className)}>
        <Button
          variant={selectedCategory === undefined ? 'default' : 'outline'}
          size="sm"
          onClick={() => onSelect(undefined)}
        >
          {t('communication.templates.allCategories')}
          <span className="ms-1.5 text-xs opacity-70">({totalCount})</span>
        </Button>
        {categories.map((cat) => (
          <Button
            key={cat.name}
            variant={selectedCategory === cat.name ? 'default' : 'outline'}
            size="sm"
            onClick={() => onSelect(cat.name)}
          >
            {cat.name}
            <span className="ms-1.5 text-xs opacity-70">({cat.count})</span>
          </Button>
        ))}
      </div>
    );
  }

  return (
    <nav
      aria-label="Categories"
      className={cn(
        'surface-glass rounded-2xl p-2 sticky',
        'top-[var(--shell-header-h,4rem)]',
        className,
      )}
    >
      <ul className="space-y-0.5">
        {categories.map((cat) => {
          const isActive = selectedCategory === cat.name;
          return (
            <li key={cat.name}>
              <button
                type="button"
                onClick={() => onSelect(cat.name)}
                className={cn(
                  'w-full flex items-center justify-between rounded-lg px-3 py-2 text-sm text-start',
                  'transition-colors',
                  isActive
                    ? 'bg-[var(--active-bg)] text-[var(--active-text)] font-medium'
                    : 'text-foreground hover:bg-[var(--hover-bg)]',
                )}
                aria-current={isActive ? 'page' : undefined}
              >
                <span className="truncate">{cat.name}</span>
                <span
                  className={cn(
                    'rounded-full px-2 py-0.5 text-xs tabular-nums',
                    isActive
                      ? 'bg-[var(--active-text)]/15 text-[var(--active-text)]'
                      : 'bg-muted text-muted-foreground',
                  )}
                  aria-label={t('communication.templates.categoryCount', { count: cat.count })}
                >
                  {cat.count}
                </span>
              </button>
            </li>
          );
        })}
      </ul>

      <Separator className="my-2" />

      <button
        type="button"
        onClick={() => onSelect(undefined)}
        className={cn(
          'w-full flex items-center justify-between rounded-lg px-3 py-2 text-sm text-start',
          'transition-colors',
          selectedCategory === undefined
            ? 'bg-[var(--active-bg)] text-[var(--active-text)] font-medium'
            : 'text-muted-foreground hover:bg-[var(--hover-bg)]',
        )}
        aria-current={selectedCategory === undefined ? 'page' : undefined}
      >
        <span>{t('communication.templates.allCategories')}</span>
        <span
          className={cn(
            'rounded-full px-2 py-0.5 text-xs tabular-nums',
            selectedCategory === undefined
              ? 'bg-[var(--active-text)]/15 text-[var(--active-text)]'
              : 'bg-muted text-muted-foreground',
          )}
          aria-label={t('communication.templates.categoryCount', { count: totalCount })}
        >
          {totalCount}
        </span>
      </button>
    </nav>
  );
}
```

- [ ] **Step 2: Type-check**

```bash
cd boilerplateFE && npm run build
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add boilerplateFE/src/features/communication/components/TemplateCategoryRail.tsx
git commit -m "feat(fe/communication): add TemplateCategoryRail component

Sticky 200px rail variant for lg+ (surface-glass, top-offset uses
--shell-header-h) and a horizontal chip-row variant for <lg.
Selection model is shared: undefined = 'All categories'. The 'All'
pseudo-row sits at the bottom of the rail with a Separator above
and at the start of the chip row."
```

---

## Task 14: FE — Wire rail into `TemplatesPage` with URL persistence + client-side grouping

**Files:**
- Modify: `boilerplateFE/src/features/communication/pages/TemplatesPage.tsx`

- [ ] **Step 1: Switch from server-filtered fetch to client-grouped fetch + add URL persistence**. Replace the body of `TemplatesPage.tsx` with:

```tsx
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router-dom';
import { FileText, Mail, Smartphone, Bell, MessageCircle, Inbox } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader, EmptyState } from '@/components/common';
import { useMessageTemplates } from '../api';
import { TemplateEditorDialog } from '../components/TemplateEditorDialog';
import { TemplateCategoryRail } from '../components/TemplateCategoryRail';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import type { MessageTemplateDto, NotificationChannel } from '@/types/communication.types';

const CHANNEL_ICONS: Record<NotificationChannel, typeof Mail> = {
  Email: Mail,
  Sms: Smartphone,
  Push: Bell,
  WhatsApp: MessageCircle,
  InApp: Inbox,
};

export default function TemplatesPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const [searchParams, setSearchParams] = useSearchParams();

  const [selectedTemplateId, setSelectedTemplateId] = useState<string | null>(null);

  // Fetch the full unfiltered list — categories derived client-side.
  const { data, isLoading, isError } = useMessageTemplates();
  const templates: MessageTemplateDto[] = data?.data ?? [];

  const canManageTemplates = hasPermission(PERMISSIONS.Communication.ManageTemplates);

  // Group templates by category. Categories sorted alphabetically; templates within each
  // category sorted by name.
  const grouped = useMemo(() => {
    const buckets = new Map<string, MessageTemplateDto[]>();
    for (const tpl of templates) {
      const list = buckets.get(tpl.category) ?? [];
      list.push(tpl);
      buckets.set(tpl.category, list);
    }
    for (const list of buckets.values()) {
      list.sort((a, b) => a.name.localeCompare(b.name));
    }
    return Array.from(buckets.entries())
      .map(([name, list]) => ({ name, list }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [templates]);

  const totalCount = templates.length;
  const categoryDescriptors = grouped.map(({ name, list }) => ({ name, count: list.length }));

  const urlCategory = searchParams.get('category') ?? undefined;
  const knownCategoryNames = new Set(grouped.map((g) => g.name));
  const selectedCategory =
    urlCategory && knownCategoryNames.has(urlCategory) ? urlCategory : undefined;

  const handleSelect = (category: string | undefined) => {
    const next = new URLSearchParams(searchParams);
    if (category === undefined) {
      next.delete('category');
    } else {
      next.set('category', category);
    }
    setSearchParams(next, { replace: true });
  };

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('communication.templates.title')} />
        <EmptyState
          icon={FileText}
          title={t('common.errorOccurred')}
          description={t('common.tryAgain')}
        />
      </div>
    );
  }

  if (isLoading && !data) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  const visibleGroups =
    selectedCategory === undefined
      ? grouped
      : grouped.filter((g) => g.name === selectedCategory);

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('communication.templates.title')}
        subtitle={t('communication.templates.subtitle')}
      />

      {totalCount === 0 ? (
        <EmptyState
          icon={FileText}
          title={t('communication.templates.noTemplates')}
          description={t('communication.templates.noTemplatesDescription')}
        />
      ) : (
        <div className="grid gap-6 lg:grid-cols-[200px_minmax(0,1fr)]">
          {/* lg+ rail */}
          <div className="hidden lg:block">
            <TemplateCategoryRail
              categories={categoryDescriptors}
              selectedCategory={selectedCategory}
              onSelect={handleSelect}
              totalCount={totalCount}
              variant="rail"
            />
          </div>

          {/* <lg chip row */}
          <div className="lg:hidden lg:col-span-2">
            <TemplateCategoryRail
              categories={categoryDescriptors}
              selectedCategory={selectedCategory}
              onSelect={handleSelect}
              totalCount={totalCount}
              variant="chips"
            />
          </div>

          {/* Main column */}
          <div className="space-y-8 min-w-0">
            {visibleGroups.map(({ name, list }) => (
              <section key={name} className="space-y-3">
                <div className="flex items-center gap-2">
                  <h3 className="text-lg font-semibold text-foreground">{name}</h3>
                  <Badge variant="secondary" className="text-xs">{list.length}</Badge>
                </div>

                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>{t('common.name', 'Name')}</TableHead>
                      <TableHead className="hidden md:table-cell">{t('communication.templates.moduleSource')}</TableHead>
                      <TableHead className="hidden lg:table-cell">{t('communication.templates.defaultChannel')}</TableHead>
                      <TableHead>{t('common.status')}</TableHead>
                      <TableHead className="text-end">{t('common.actions')}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {list.map((tpl) => {
                      const ChannelIcon = CHANNEL_ICONS[tpl.defaultChannel];
                      return (
                        <TableRow key={tpl.id}>
                          <TableCell>
                            <div className="space-y-1">
                              <p className="font-medium text-foreground">{tpl.name}</p>
                              {tpl.description && (
                                <p className="text-sm text-muted-foreground line-clamp-1">{tpl.description}</p>
                              )}
                            </div>
                          </TableCell>
                          <TableCell className="hidden md:table-cell">
                            <span className="text-sm text-muted-foreground">{tpl.moduleSource}</span>
                          </TableCell>
                          <TableCell className="hidden lg:table-cell">
                            <div className="flex items-center gap-1.5">
                              {ChannelIcon && <ChannelIcon className="h-4 w-4 text-muted-foreground" />}
                              <span className="text-sm">{tpl.defaultChannel}</span>
                            </div>
                          </TableCell>
                          <TableCell>
                            <div className="flex items-center gap-1.5">
                              {tpl.isSystem && (
                                <Badge variant="secondary">{t('communication.templates.systemTemplate')}</Badge>
                              )}
                              {tpl.hasOverride && (
                                <Badge variant="default">{t('communication.templates.customized')}</Badge>
                              )}
                            </div>
                          </TableCell>
                          <TableCell className="text-end">
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => setSelectedTemplateId(tpl.id)}
                            >
                              {canManageTemplates
                                ? t('common.edit')
                                : t('common.view', 'View')}
                            </Button>
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </section>
            ))}
          </div>
        </div>
      )}

      <TemplateEditorDialog
        templateId={selectedTemplateId}
        open={!!selectedTemplateId}
        onOpenChange={(open) => {
          if (!open) setSelectedTemplateId(null);
        }}
      />
    </div>
  );
}
```

- [ ] **Step 2: Verify `useMessageTemplates()` accepts being called without args** — read `boilerplateFE/src/features/communication/api/communication.queries.ts`. The hook signature should be `useMessageTemplates(category?: string)`. If it's positional with a required param, fix the hook signature first.

- [ ] **Step 3: Type-check + build**

```bash
cd boilerplateFE && npm run build
```

Expected: 0 errors.

- [ ] **Step 4: Verify React Router import is used** — the file pulls in `useSearchParams` from `react-router-dom`. Confirm the project uses RR v6 or v7 (both export this hook). If the codebase uses a different router (TanStack Router, etc.), swap the import to that router's URL-state primitive.

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/features/communication/pages/TemplatesPage.tsx
git commit -m "feat(fe/communication): wire category rail into TemplatesPage

200px sticky rail on lg+, horizontal chips on <lg. Selection
persisted in URL ?category=. Categories derived client-side from a
single unfiltered fetch — refresh / share preserves selection.
'All categories' shows every group; selecting one filters to that
group only."
```

---

## Task 15: FE — `TriggerRulesPage` channel-sequence chip-arrow polish

**Files:**
- Modify: `boilerplateFE/src/features/communication/pages/TriggerRulesPage.tsx`
- Modify: `boilerplateFE/src/constants/status.ts` (verify Active / Inactive variants)

- [ ] **Step 1: Verify `STATUS_BADGE_VARIANT` covers Active / Inactive** — read `boilerplateFE/src/constants/status.ts`:

```bash
grep -n "Active\|Inactive\|STATUS_BADGE_VARIANT" boilerplateFE/src/constants/status.ts
```

Expected: both keys mapped to a non-`undefined` variant. If absent, add:

```ts
export const STATUS_BADGE_VARIANT: Record<string, BadgeVariant> = {
  // … existing entries …
  Active: 'default',
  Inactive: 'secondary',
};
```

(Phase 5a likely already added these for workflow definitions — verify before adding duplicates.)

- [ ] **Step 2: Add the chip-arrow chain rendering** — modify `boilerplateFE/src/features/communication/pages/TriggerRulesPage.tsx`. Add `ChevronRight` to the lucide imports:

```ts
import { Zap, Plus, Pencil, Trash2, Power, ChevronRight } from 'lucide-react';
```

Replace the current channel-sequence cell (the `<TableCell>` containing the chips) with:

```tsx
<TableCell>
  <div className="flex flex-wrap items-center gap-1">
    {rule.channelSequence.map((ch, idx) => (
      <span key={ch} className="flex items-center gap-1">
        {idx > 0 && (
          <ChevronRight
            className="h-3 w-3 text-muted-foreground rtl:rotate-180"
            aria-label={t('communication.triggerRules.channelSequence.connector')}
          />
        )}
        <Badge variant="secondary" className="text-xs">{ch}</Badge>
      </span>
    ))}
  </div>
</TableCell>
```

- [ ] **Step 3: Type-check + build**

```bash
cd boilerplateFE && npm run build
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/src/features/communication/pages/TriggerRulesPage.tsx boilerplateFE/src/constants/status.ts
git commit -m "feat(fe/communication): polish TriggerRulesPage channel sequence

Channel sequence cell now renders as Email -> Sms -> Push with
ChevronRight connectors that rotate 180 degrees on RTL. Verified
STATUS_BADGE_VARIANT covers Active/Inactive (extended if missing).
Token sweep on the page; structural shape unchanged."
```

---

## Task 16: FE — Token sweep across remaining dialogs

**Files (verify each — sweep only when hardcoded primary-shade or `dark:` overrides appear):**
- Modify: `boilerplateFE/src/features/communication/components/TemplateEditorDialog.tsx`
- Modify: `boilerplateFE/src/features/communication/components/TriggerRuleFormDialog.tsx`
- Modify: `boilerplateFE/src/features/communication/components/ChannelSetupDialog.tsx`
- Modify: `boilerplateFE/src/features/communication/components/IntegrationSetupDialog.tsx`
- Modify: `boilerplateFE/src/features/communication/components/NotificationPreferencesPanel.tsx`
- Modify: `boilerplateFE/src/features/communication/components/RequiredNotificationsManager.tsx`
- Modify: `boilerplateFE/src/features/communication/components/CommunicationDashboardWidget.tsx`

- [ ] **Step 1: Audit for hardcoded primary shades and dark: overrides** — from repo root:

```bash
grep -nE "(primary-(50|100|200|300|400|500|600|700|800|900|950))|dark:bg-primary|dark:text-primary|dark:border-primary" boilerplateFE/src/features/communication/components/{TemplateEditorDialog,TriggerRuleFormDialog,ChannelSetupDialog,IntegrationSetupDialog,NotificationPreferencesPanel,RequiredNotificationsManager,CommunicationDashboardWidget}.tsx
```

Expected: zero hits is ideal. Each hit is a sweep candidate.

- [ ] **Step 2: For each hit, replace** following these mappings (also documented in CLAUDE.md):

| Hardcoded | Replacement |
|---|---|
| `bg-primary-50` / `bg-primary-100` (light tint) | `bg-[var(--active-bg)]` |
| `text-primary-700` / `text-primary-600` | `text-primary` or `text-[var(--tinted-fg)]` |
| `border-primary-200` / `border-primary-300` | `border-[var(--active-border)]` |
| `bg-primary-600` / `bg-primary-700` (solid fill) | `bg-primary` |
| `dark:bg-primary-*` overrides | delete (theme preset handles dark mode) |
| `dark:text-primary-*` overrides | delete |
| `bg-muted/30` ad-hoc form sub-cards | `bg-[var(--surface-glass)]` if glass is desired, else leave |

- [ ] **Step 3: Re-run the audit** — same `grep` from Step 1 should return zero hits.

- [ ] **Step 4: Type-check + build**

```bash
cd boilerplateFE && npm run build
```

Expected: 0 errors.

- [ ] **Step 5: Commit (only if sweeps were made)**

```bash
git add boilerplateFE/src/features/communication/components
git commit -m "polish(fe/communication): token sweep across comms dialogs

Replace hardcoded primary-shade utilities with semantic tokens
(--active-bg, --active-text, --active-border, --tinted-fg) so the
dialogs adapt to theme presets. Drop dark: overrides since the
preset system handles dark mode automatically."
```

If grep returned zero hits in Step 1 with no changes needed, the working tree is clean and the commit step is skipped.

---

## Task 17: FE — Final polish sweep and dialog CTA verification

**Files:**
- Modify: any page or component flagged by the audit below.

- [ ] **Step 1: Audit `<Card>` wrappers around `<Table>` instances** — the design system rule says `<Table>` includes its own `surface-glass` container, so wrapping in `<Card>` is duplication. From repo root:

```bash
grep -nB2 -A5 "<Table>" boilerplateFE/src/features/communication
```

For any `<Card>` directly wrapping `<Table>`, remove the `<Card>` wrapper.

- [ ] **Step 2: Audit primary-action button variants on dialogs** — confirm save/submit buttons use `variant="default"` (not `variant="primary"` or hardcoded gradient classes).

```bash
grep -nE 'variant="primary"' boilerplateFE/src/features/communication
```

Expected: 0 hits. If any, swap to `variant="default"`.

- [ ] **Step 3: Visual smoke** — start the FE dev server and visit each redesigned page.

```bash
cd boilerplateFE && npm run dev
```

Browse to:
- `/communication/delivery-log` — hero shows, row stripe colors readable, click row → drawer slides in from end, click another row → swap, mobile width → bottom sheet.
- `/communication/templates` — rail visible at `lg+`, chip row at `<lg`, click category → URL updates, refresh preserves selection, "All categories" shows everything.
- `/communication/channels` — hero, provider logos, default-star halo, last-tested chip colored by age.
- `/communication/integrations` — same shape as channels.
- `/communication/trigger-rules` — channel-sequence chain renders with chevrons; RTL flips them.

- [ ] **Step 4: Commit (if any sweeps were made; otherwise skip)**

```bash
git add boilerplateFE/src/features/communication
git commit -m "polish(fe/communication): card/table wrapper + button variant audit

Remove redundant Card wrappers around Table instances; verify
primary-action buttons use variant=default (not deprecated
variant=primary or hardcoded gradient classes)."
```

---

## Task 18: Verification — full lint + typecheck + build + Playwright run in test app

**Files:** none modified — run-only.

- [ ] **Step 1: Backend** — full solution build + tests:

```bash
cd boilerplateBE && dotnet build Starter.sln 2>&1 | tail -5
cd boilerplateBE && dotnet test Starter.sln 2>&1 | tail -10
```

Expected: 0 errors; all tests pass.

- [ ] **Step 2: Frontend** — lint + build:

```bash
cd boilerplateFE && npm run lint && npm run build
```

Expected: 0 errors. (If `lint` script doesn't exist, skip and run `npm run build` only.)

- [ ] **Step 3: Set up the test app (per CLAUDE.md "Post-Feature Testing Workflow")** — only if the existing `_testJ4visual` test app on the test ports (5100/3100 or 5200/3200 etc.) is no longer running or is from a previous phase. Otherwise, hot-copy the FE diff:

```bash
# Diff-copy the changed FE files into the running test app
rsync -a --include='*/' \
  --include='src/features/communication/***' \
  --include='src/components/ui/sheet.tsx' \
  --include='src/i18n/locales/**/translation.json' \
  --include='src/types/communication.types.ts' \
  --include='src/lib/query/keys.ts' \
  --include='src/config/api.config.ts' \
  --include='src/constants/status.ts' \
  --exclude='*' \
  boilerplateFE/ \
  /path/to/_testJ4visual/boilerplateFE/
```

(Adjust path. Vite HMR picks up the diff.) For the BE endpoint, restart the test-app `dotnet run` so the new controller endpoint surfaces.

- [ ] **Step 4: Playwright / Chrome DevTools MCP visual sweep** — exercise each redesigned page:

- **DeliveryLog** — login as a tenant admin with mixed delivery statuses spanning 7 days; verify hero counts match table reality, row stripes render the right colors, drawer slides in correctly, list stays visible behind drawer, click another row → drawer content swaps, resend button works from drawer, mobile fallback to bottom sheet on `<sm` (Chrome DevTools → emulate iPhone).
- **Templates** — login as a tenant admin with 5+ categories totaling 30+ templates; verify rail renders with counts, "All categories" pseudo-row at bottom, URL `?category=` persistence on refresh, mobile fallback to chip row on `<lg`, default selection is "All categories".
- **Channels** — login as a tenant admin; verify hero counts match config reality, provider logos render for all 9 channel providers (cycle through configs), default-star glow visible, age-tinted chip thresholds (test by editing `lastTestedAt` in DB or seed), `Error` pill shows AlertCircle icon.
- **Integrations** — login as a tenant admin; verify hero counts, provider logos for Slack/Telegram/Discord/MS Teams, no default-star.
- **TriggerRules** — verify channel-sequence chip-arrow chain renders, RTL flips arrows correctly, status badges use new `STATUS_BADGE_VARIANT` mappings.

Capture a screenshot of each redesigned page (LTR + RTL + dark) for the PR body.

- [ ] **Step 5: Smoke regression** — visit Phase 0–5a surfaces and confirm nothing visibly broke (dashboard, billing, products, workflow inbox, instance detail, designer):

- `/dashboard` — no console errors, hero strips render.
- `/billing` — page renders.
- `/products/items` — page renders.
- `/workflows/inbox` — hero strips and SLA pressure rows still render correctly.
- `/workflows/instances/<id>` — sticky right rail still works.

- [ ] **Step 6: Tag completion** — no commit; report screenshots and any regressions to the user.

---

## Task 19: Final code review + PR

**Files:** none modified — review and PR only.

- [ ] **Step 1: Run the code-reviewer agent** on the full diff:

```
Use superpowers:code-reviewer (or whatever agent is registered for code review) on the diff between origin/main and HEAD.
```

Address findings inline — fix any issues, re-run lint+build, and commit fixes (one per concern unless trivially related).

- [ ] **Step 2: Push the branch**

```bash
git push -u origin fe/phase-5b-design
```

- [ ] **Step 3: Open the PR**

```bash
gh pr create --title "feat(fe): Phase 5b Communication — comms cluster (5 pages + side drawer + 4 new components)" --body "$(cat <<'EOF'
## Summary

Phase 5b brings the communication cluster onto J4 Spectrum tokens with three earned structural changes:

- **DeliveryLog** — modal → side drawer (operator failure-sweep flow) + 4-card 7-day status hero + per-row leading-edge color stripe by status.
- **Templates** — sticky 200px category rail on `lg+` (chips on `<lg`); selection persisted via URL `?category=`. Counts derived client-side from a single unfiltered fetch.
- **Channels & Integrations** — paired card refresh: provider logos (13 inline SVGs), elevated cards, default-star halo (channels only), age-tinted last-tested chip, AlertCircle on Error pills. 3-card hero on each.
- **TriggerRules** — channel-sequence chip-arrow chain (1 → 2 → 3); RTL-safe.

### Backend

One new query handler — `GetDeliveryStatusCountsQuery` — on `DeliveryLogsController.GetStatusCounts`. Tenant-scoped, window clamped to `[1, 90]` days (default 7). No schema changes, no migrations, no new permissions.

### New FE primitives

- `<Sheet>` (Radix Dialog wrapper) — end-anchored side drawer + bottom-sheet variant; three width tiers.
- `<ProviderLogo>` + `providerLogos.ts` — inline SVG marks for 9 channel providers + 4 integration types; tinted-initial fallback.
- `<ChannelConfigCard>`, `<IntegrationConfigCard>` — extracted card surfaces.
- `<TemplateCategoryRail>` — sticky rail + chip-row variants in one component.
- `<DeliveryLogStatusHero>`, `<ChannelsStatusHero>`, `<IntegrationsStatusHero>` — hero strips.

### Spec / plan

- Spec: `docs/superpowers/specs/2026-04-30-redesign-phase-5b-communication-design.md`
- Plan: `docs/superpowers/plans/2026-04-30-redesign-phase-5b-communication.md`

This is the second of three Phase 5 PRs. Phase 5c (Webhooks + Import/Export + Comments-Activity + Onboarding) follows.

## Test plan

- [ ] BE: `dotnet test` — all tests pass including the new `GetDeliveryStatusCountsQueryHandlerTests` (5 cases).
- [ ] FE: `npm run build` — 0 errors.
- [ ] Visual: DeliveryLog — hero, row stripe, drawer slide-in, drawer swap, mobile bottom sheet, RTL.
- [ ] Visual: Templates — rail, URL persistence, chip-row fallback, "All categories" pseudo-row, RTL.
- [ ] Visual: Channels — hero, provider logos, default-star halo, age-tinted chip, AlertCircle.
- [ ] Visual: Integrations — same as Channels minus default-star.
- [ ] Visual: TriggerRules — chip-arrow chain, RTL chevron flip.
- [ ] Regression: Phase 0–5a surfaces unchanged (dashboard, billing, products, workflow).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Note: per the user's [feedback memory](../../memory/feedback_no_coauthor.md), do **not** add `Co-Authored-By` lines on commits. The PR body's Generated-with footer is fine; the per-commit footers throughout this plan don't include co-author lines.

- [ ] **Step 4: Report PR URL** to the user.
