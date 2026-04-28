# Phase 2 — Platform Admin Cluster Polish — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Polish the four Platform admin pages (`audit-logs`, `feature-flags`, `api-keys`, `settings`) into J4-native form, add an `AuditLogDetailPage` route, with a small backend addition (`GetAuditLogByIdQuery` + controller action) to support it.

**Architecture:** Pure FE work plus one small BE addition. New FE code is page-scoped under each feature's `components/` and `pages/` folders, while reusing existing primitives (`PageHeader`, `Table`, `Badge`, `Card`, `queryKeys`) instead of creating parallel local variants. The BE addition mirrors the existing `GetAuditLogsQueryHandler` pattern (CQRS + Result + multi-tenant via `ApplicationDbContext` global filter).

**Tech Stack:** React 19 + TypeScript + Tailwind CSS 4 + shadcn/ui + TanStack Query · .NET 10 / MediatR / EF Core (BE) · Playwright MCP for visual verification.

**Spec:** [`docs/superpowers/specs/2026-04-28-redesign-phase-2-design.md`](../specs/2026-04-28-redesign-phase-2-design.md)

**Branch:** `fe/redesign-phase-2-views` (already created off `origin/main`).

**Verification model:** This codebase has no unit test runner configured (no vitest/jest in `package.json`). Per-task verification is `npm run build` + `npm run lint` for code-correctness, plus Playwright MCP for visual checks. Each task ends with a commit.

**Plan review adjustments:** Routes use the existing app paths (`/audit-logs`, no `/admin` prefix). API endpoints preserve the current PascalCase controller casing (`/AuditLogs`). Detail queries extend the shared `queryKeys` registry. Detail-page navigation uses `PageHeader.breadcrumbs` because `useBackNavigation` is deprecated in this codebase. Metric surfaces reuse existing shared UI primitives; no fake trend chart should imply time-series data when no history exists.

---

## Task 0: Branch & harness verification

**Files:** none (operational task only)

- [ ] **Step 1: Confirm branch state**

```bash
git status
git branch --show-current   # expected: fe/redesign-phase-2-views
git log --oneline -3         # expected to include: spec doc + parent merged from origin/main
```

- [ ] **Step 2: Run a clean build of both apps to establish a green baseline**

```bash
cd boilerplateFE && npm install && npm run build
cd ../boilerplateBE && dotnet build src/Starter.Api
```

Expected: both succeed. If FE build fails on `main` baseline, stop — Phase 2 inherits the break and shouldn't add to it.

- [ ] **Step 3: Decide on test-app harness**

If `_testJ4visual/` directory still exists at repo root, restart its BE/FE processes and use it for verification. Otherwise, regenerate per `.claude/skills/post-feature-testing.md` (use ports 5100 / 3100). Document which choice you made in your task log so subsequent task verification is consistent.

- [ ] **Step 4: No commit for this task** — just report status to the dispatcher.

---

## Task 1: BE `GetAuditLogByIdQuery` + controller action + FE query hook

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Features/AuditLogs/Queries/GetAuditLogById/GetAuditLogByIdQuery.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/AuditLogs/Queries/GetAuditLogById/GetAuditLogByIdQueryHandler.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/AuditLogs/DTOs/AuditLogDto.cs` (add agent attribution fields)
- Modify: `boilerplateBE/src/Starter.Application/Features/AuditLogs/Queries/GetAuditLogs/GetAuditLogsQueryHandler.cs` (project new fields)
- Modify: `boilerplateBE/src/Starter.Api/Controllers/AuditLogsController.cs` (add `GET {id}` action)
- Modify: `boilerplateFE/src/types/audit-log.types.ts` (add new fields)
- Modify: `boilerplateFE/src/config/api.config.ts` (add `BY_ID` endpoint)
- Modify: `boilerplateFE/src/lib/query/keys.ts` (add audit-log detail key)
- Modify: `boilerplateFE/src/features/audit-logs/api/audit-logs.api.ts` (add `getAuditLog`)
- Modify: `boilerplateFE/src/features/audit-logs/api/audit-logs.queries.ts` (add `useAuditLog`)

- [ ] **Step 1: Add agent attribution fields to `AuditLogDto`**

Replace the file content with:

```csharp
namespace Starter.Application.Features.AuditLogs.DTOs;

public sealed record AuditLogDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Action,
    string? Changes,
    Guid? PerformedBy,
    string? PerformedByName,
    DateTime PerformedAt,
    string? IpAddress,
    string? CorrelationId,
    Guid? OnBehalfOfUserId,
    Guid? AgentPrincipalId,
    Guid? AgentRunId);
```

- [ ] **Step 2: Project the new fields in the existing list handler**

In `GetAuditLogsQueryHandler.cs`, update the `.Select(...)` projection (currently lines ~70-80) to:

```csharp
var projected = query.Select(a => new AuditLogDto(
    a.Id,
    a.EntityType.ToString(),
    a.EntityId,
    a.Action.ToString(),
    a.Changes,
    a.PerformedBy,
    a.PerformedByName,
    a.PerformedAt,
    a.IpAddress,
    a.CorrelationId,
    a.OnBehalfOfUserId,
    a.AgentPrincipalId,
    a.AgentRunId));
```

- [ ] **Step 2b: Preserve exact `DateTo` timestamps for timeline queries**

In `GetAuditLogsQueryHandler.cs`, keep the existing inclusive end-of-day behavior for date-only filters, but do not add a day when the caller passes an exact timestamp. This lets the audit timeline request "last 24 hours" without accidentally fetching tomorrow's rows:

```csharp
if (request.DateTo.HasValue)
{
    var rawTo = DateTime.SpecifyKind(request.DateTo.Value, DateTimeKind.Utc);
    var to = rawTo.TimeOfDay == TimeSpan.Zero
        ? rawTo.AddDays(1).AddTicks(-1)
        : rawTo;
    query = query.Where(a => a.PerformedAt <= to);
}
```

Leave the existing `DateFrom` normalization unchanged.

- [ ] **Step 3: Create `GetAuditLogByIdQuery.cs`**

```csharp
using MediatR;
using Starter.Application.Features.AuditLogs.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.AuditLogs.Queries.GetAuditLogById;

public sealed record GetAuditLogByIdQuery(Guid Id) : IRequest<Result<AuditLogDto>>;
```

- [ ] **Step 4: Create `GetAuditLogByIdQueryHandler.cs`**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.AuditLogs.DTOs;
using Starter.Domain.Common;
using Starter.Shared.Results;

namespace Starter.Application.Features.AuditLogs.Queries.GetAuditLogById;

internal sealed class GetAuditLogByIdQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetAuditLogByIdQuery, Result<AuditLogDto>>
{
    public async Task<Result<AuditLogDto>> Handle(GetAuditLogByIdQuery request, CancellationToken cancellationToken)
    {
        var dto = await context.Set<AuditLog>()
            .AsNoTracking()
            .Where(a => a.Id == request.Id)
            .Select(a => new AuditLogDto(
                a.Id,
                a.EntityType.ToString(),
                a.EntityId,
                a.Action.ToString(),
                a.Changes,
                a.PerformedBy,
                a.PerformedByName,
                a.PerformedAt,
                a.IpAddress,
                a.CorrelationId,
                a.OnBehalfOfUserId,
                a.AgentPrincipalId,
                a.AgentRunId))
            .FirstOrDefaultAsync(cancellationToken);

        if (dto is null)
            return Result.Failure<AuditLogDto>(Error.NotFound("AuditLog.NotFound", "Audit log not found"));

        return Result.Success(dto);
    }
}
```

The multi-tenant global query filter on `ApplicationDbContext` automatically restricts a tenant-admin to their own tenant's rows; super-admins (`TenantId=null`) see all. No explicit tenant check needed here — that's the same pattern the list handler uses.

- [ ] **Step 5: Add the `GET {id}` controller action**

Modify `AuditLogsController.cs`. Add this using next to the existing `GetAuditLogs` import:

```csharp
using Starter.Application.Features.AuditLogs.Queries.GetAuditLogById;
```

Then add this method after the existing `GetAuditLogs` action:

```csharp
/// <summary>
/// Get a single audit log entry by id.
/// </summary>
[HttpGet("{id:guid}")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetAuditLog(Guid id)
{
    var result = await Mediator.Send(new GetAuditLogByIdQuery(id));
    return HandleResult(result);
}
```

- [ ] **Step 6: Build the BE**

```bash
cd boilerplateBE && dotnet build src/Starter.Api
```

Expected: build succeeds, no warnings about the new files.

- [ ] **Step 7: Smoke-test the endpoint**

Run BE locally (or against the test-app harness), grab a log id from the list endpoint, then:

```bash
TOKEN="<jwt-from-login>"
LOG_ID="<id-from-list>"
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/v1/AuditLogs/$LOG_ID | jq .
```

Expected: 200 with the dto including `onBehalfOfUserId`, `agentPrincipalId`, `agentRunId` fields.
Then test 404: `curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/v1/AuditLogs/00000000-0000-0000-0000-000000000000 -o /dev/null -w "%{http_code}\n"` — expect `404`.

- [ ] **Step 8: Update FE `AuditLog` type**

Edit `boilerplateFE/src/types/audit-log.types.ts`:

```ts
export interface AuditLog {
  id: string;
  entityType: string;
  entityId: string;
  action: string;
  changes: string | null;
  performedBy: string | null;
  performedByName: string | null;
  performedAt: string;
  ipAddress: string | null;
  correlationId: string | null;
  onBehalfOfUserId: string | null;
  agentPrincipalId: string | null;
  agentRunId: string | null;
}
```

- [ ] **Step 9: Add `BY_ID` endpoint**

Edit `boilerplateFE/src/config/api.config.ts`. Locate the `AUDIT_LOGS` block (~line 61) and add:

```ts
AUDIT_LOGS: {
  LIST: '/AuditLogs',
  BY_ID: (id: string) => `/AuditLogs/${id}`,
},
```

- [ ] **Step 10: Add `getAuditLog` to the api client**

Edit `boilerplateFE/src/features/audit-logs/api/audit-logs.api.ts`:

```ts
import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';
import type { PaginatedResponse, AuditLog } from '@/types';

export const auditLogsApi = {
  getAuditLogs: async (
    params?: Record<string, unknown>
  ): Promise<PaginatedResponse<AuditLog>> => {
    const response = await apiClient.get<PaginatedResponse<AuditLog>>(
      API_ENDPOINTS.AUDIT_LOGS.LIST,
      { params }
    );
    return response.data;
  },

  getAuditLog: async (id: string): Promise<AuditLog> => {
    const response = await apiClient.get<{ data: AuditLog }>(
      API_ENDPOINTS.AUDIT_LOGS.BY_ID(id)
    );
    // Backend wraps in ApiResponse<T>; axios already unwraps to response.data; envelope provides .data
    return response.data.data ?? (response.data as unknown as AuditLog);
  },
};
```

The `response.data.data ?? response.data` fallback follows the project's documented convention (CLAUDE.md "API Response Envelope") — list endpoints flatten differently than singular ones, so the pattern is intentional.

- [ ] **Step 11: Add the shared detail query key + `useAuditLog` query hook**

First edit `boilerplateFE/src/lib/query/keys.ts` and extend the existing `auditLogs` block:

```ts
auditLogs: {
  all: () => ['auditLogs'] as const,
  list: () => [...queryKeys.auditLogs.all(), 'list'] as const,
  detail: (id: string) => [...queryKeys.auditLogs.all(), 'detail', id] as const,
},
```

Then edit `boilerplateFE/src/features/audit-logs/api/audit-logs.queries.ts`. Add `useAuditLog` to the existing file:

```ts
import { useQuery } from '@tanstack/react-query';
import { auditLogsApi } from './audit-logs.api';
import { queryKeys } from '@/lib/query/keys';

export function useAuditLog(id: string | undefined, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: queryKeys.auditLogs.detail(id ?? ''),
    queryFn: () => auditLogsApi.getAuditLog(id!),
    enabled: !!id && (options?.enabled ?? true),
    staleTime: 60_000,
  });
}
```

- [ ] **Step 12: Run FE build + lint**

```bash
cd boilerplateFE && npm run build && npm run lint
```

Expected: both pass cleanly.

- [ ] **Step 13: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Features/AuditLogs/ \
        boilerplateBE/src/Starter.Api/Controllers/AuditLogsController.cs \
        boilerplateFE/src/types/audit-log.types.ts \
        boilerplateFE/src/config/api.config.ts \
        boilerplateFE/src/lib/query/keys.ts \
        boilerplateFE/src/features/audit-logs/api/
git commit -m "feat(audit-logs): add GetAuditLogByIdQuery + GET {id} endpoint + useAuditLog hook"
```

---

## Task 2: `JsonView` component

**Files:**
- Create: `boilerplateFE/src/features/audit-logs/components/JsonView.tsx`

- [ ] **Step 1: Implement the component**

```tsx
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/utils';

interface JsonViewProps {
  payload: string | null | undefined;
  className?: string;
}

type JsonValue = string | number | boolean | null | JsonObject | JsonArray;
interface JsonObject { [key: string]: JsonValue }
type JsonArray = JsonValue[];

function tryParse(payload: string | null | undefined): { ok: true; value: JsonValue } | { ok: false; raw: string } {
  if (payload == null) return { ok: false, raw: '' };
  try {
    return { ok: true, value: JSON.parse(payload) as JsonValue };
  } catch {
    return { ok: false, raw: payload };
  }
}

function renderValue(value: JsonValue, indent: number, lineNo: { n: number }): React.ReactNode {
  const pad = '  '.repeat(indent);

  if (value === null) {
    return <span className="text-[var(--color-violet-600)] dark:text-[var(--color-violet-400)]">null</span>;
  }
  if (typeof value === 'boolean' || typeof value === 'number') {
    return <span className="text-[var(--color-violet-600)] dark:text-[var(--color-violet-400)]">{String(value)}</span>;
  }
  if (typeof value === 'string') {
    return <span className="text-emerald-600 dark:text-emerald-400">"{value}"</span>;
  }
  if (Array.isArray(value)) {
    if (value.length === 0) return <span className="text-muted-foreground">[]</span>;
    return (
      <>
        <span className="text-muted-foreground">[</span>
        {value.map((item, i) => {
          lineNo.n += 1;
          return (
            <div key={i} className="hover:bg-[var(--hover-bg)] -mx-2 px-2 flex">
              <span className="text-primary/50 text-xs w-8 shrink-0 select-none">{lineNo.n}</span>
              <span>{pad}  {renderValue(item, indent + 1, lineNo)}{i < value.length - 1 ? ',' : ''}</span>
            </div>
          );
        })}
        <div className="-mx-2 px-2 flex">
          <span className="text-primary/50 text-xs w-8 shrink-0 select-none">&nbsp;</span>
          <span className="text-muted-foreground">{pad}]</span>
        </div>
      </>
    );
  }
  // object
  const entries = Object.entries(value);
  if (entries.length === 0) return <span className="text-muted-foreground">{'{}'}</span>;
  return (
    <>
      <span className="text-muted-foreground">{'{'}</span>
      {entries.map(([key, val], i) => {
        lineNo.n += 1;
        return (
          <div key={key} className="hover:bg-[var(--hover-bg)] -mx-2 px-2 flex">
            <span className="text-primary/50 text-xs w-8 shrink-0 select-none">{lineNo.n}</span>
            <span>
              {pad}  <span className="text-primary">"{key}"</span>
              <span className="text-muted-foreground">: </span>
              {renderValue(val, indent + 1, lineNo)}{i < entries.length - 1 ? ',' : ''}
            </span>
          </div>
        );
      })}
      <div className="-mx-2 px-2 flex">
        <span className="text-primary/50 text-xs w-8 shrink-0 select-none">&nbsp;</span>
        <span className="text-muted-foreground">{pad}{'}'}</span>
      </div>
    </>
  );
}

export function JsonView({ payload, className }: JsonViewProps) {
  const { t } = useTranslation();
  const parsed = useMemo(() => tryParse(payload), [payload]);

  if (payload == null || payload === '') {
    return (
      <div className={cn('text-sm text-muted-foreground italic', className)}>
        {t('auditLogs.detail.noPayload')}
      </div>
    );
  }

  if (!parsed.ok) {
    return (
      <div className={cn('font-mono text-xs', className)} dir="ltr">
        <div className="text-xs text-muted-foreground mb-2">{t('auditLogs.detail.rawPayload')}</div>
        <pre className="whitespace-pre-wrap break-all">{parsed.raw}</pre>
      </div>
    );
  }

  return (
    <div
      className={cn('font-mono text-xs leading-relaxed overflow-x-auto', className)}
      dir="ltr"
      role="region"
      aria-label={t('auditLogs.detail.eventPayload')}
      tabIndex={0}
    >
      {(() => {
        const lineNo = { n: 1 };
        const top = (
          <div className="-mx-2 px-2 flex">
            <span className="text-primary/50 text-xs w-8 shrink-0 select-none">{lineNo.n}</span>
            <span>{renderValue(parsed.value, 0, lineNo)}</span>
          </div>
        );
        return top;
      })()}
    </div>
  );
}
```

- [ ] **Step 2: Build + lint**

```bash
cd boilerplateFE && npm run build && npm run lint
```

Expected: clean.

- [ ] **Step 3: Commit**

```bash
git add boilerplateFE/src/features/audit-logs/components/JsonView.tsx
git commit -m "feat(audit-logs): add JsonView component (read-only syntax-highlighted JSON viewer)"
```

---

## Task 3: `AuditMetadataCard` component

**Files:**
- Create: `boilerplateFE/src/features/audit-logs/components/AuditMetadataCard.tsx`

- [ ] **Step 1: Implement the component**

```tsx
import { useTranslation } from 'react-i18next';
import { Copy, Check, Bot, User as UserIcon, Globe, Hash } from 'lucide-react';
import { useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { UserAvatar } from '@/components/common';
import { Link } from 'react-router-dom';
import type { AuditLog } from '@/types';

interface AuditMetadataCardProps {
  log: AuditLog;
  isSuperAdmin: boolean;
  tenantName?: string | null;
}

function CopyableField({ label, value, icon: Icon }: { label: string; value: string; icon: React.ComponentType<{ className?: string }> }) {
  const [copied, setCopied] = useState(false);
  const handleCopy = () => {
    navigator.clipboard.writeText(value).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    });
  };
  return (
    <div className="flex items-start gap-2 group">
      <Icon className="h-4 w-4 mt-0.5 text-muted-foreground shrink-0" />
      <div className="flex-1 min-w-0">
        <div className="text-xs text-muted-foreground">{label}</div>
        <div className="font-mono text-xs break-all">{value}</div>
      </div>
      <Button variant="ghost" size="icon" className="h-7 w-7 opacity-0 group-hover:opacity-100" onClick={handleCopy}>
        {copied ? <Check className="h-3 w-3 text-emerald-500" /> : <Copy className="h-3 w-3" />}
      </Button>
    </div>
  );
}

function splitDisplayName(name: string | null | undefined) {
  const parts = (name ?? '').trim().split(/\s+/).filter(Boolean);
  return {
    firstName: parts[0],
    lastName: parts.length > 1 ? parts[parts.length - 1] : undefined,
  };
}

export function AuditMetadataCard({ log, isSuperAdmin, tenantName }: AuditMetadataCardProps) {
  const { t } = useTranslation();
  const hasAgentAttribution = !!log.agentPrincipalId;
  const actorName = log.performedByName ?? t('auditLogs.detail.unknownActor');
  const actorNameParts = splitDisplayName(log.performedByName);

  return (
    <Card>
      <CardContent className="space-y-4 pt-6">
        {/* Actor */}
        <div>
          <div className="text-xs text-muted-foreground mb-2">{t('auditLogs.detail.actor')}</div>
          {log.performedBy ? (
            <div className="flex items-center gap-3">
              <UserAvatar {...actorNameParts} size="sm" />
              <div className="min-w-0">
                <div className="text-sm font-medium truncate">{actorName}</div>
                <div className="text-xs text-muted-foreground font-mono truncate">{log.performedBy}</div>
              </div>
            </div>
          ) : (
            <div className="text-sm text-muted-foreground italic">{t('auditLogs.detail.systemAction')}</div>
          )}
        </div>

        {/* Agent attribution (only when an agent acted) */}
        {hasAgentAttribution && (
          <div className="rounded-lg border border-[var(--color-violet-200)] dark:border-[var(--color-violet-800)] bg-[var(--color-violet-50)]/40 dark:bg-[var(--color-violet-950)]/40 p-3 space-y-2">
            <div className="flex items-center gap-2">
              <Bot className="h-4 w-4 text-[var(--color-violet-600)] dark:text-[var(--color-violet-400)]" />
              <span className="text-xs font-medium text-[var(--color-violet-700)] dark:text-[var(--color-violet-300)]">
                {t('auditLogs.detail.agentAction')}
              </span>
            </div>
            {log.onBehalfOfUserId && (
              <CopyableField label={t('auditLogs.detail.onBehalfOf')} value={log.onBehalfOfUserId} icon={UserIcon} />
            )}
            <CopyableField label={t('auditLogs.detail.agentPrincipal')} value={log.agentPrincipalId!} icon={Bot} />
            {log.agentRunId && (
              <CopyableField label={t('auditLogs.detail.agentRun')} value={log.agentRunId} icon={Hash} />
            )}
          </div>
        )}

        {/* IP */}
        {log.ipAddress && (
          <CopyableField label={t('auditLogs.detail.ipAddress')} value={log.ipAddress} icon={Globe} />
        )}

        {/* Correlation / Trace */}
        {log.correlationId && (
          <div className="space-y-1">
            <CopyableField label={t('auditLogs.detail.correlationId')} value={log.correlationId} icon={Hash} />
            <Link
              to={`/audit-logs?searchTerm=${encodeURIComponent(log.correlationId)}`}
              className="text-xs text-primary hover:underline ml-6"
            >
              {t('auditLogs.detail.viewSameConversation')}
            </Link>
          </div>
        )}

        {/* Tenant (super-admin only) */}
        {isSuperAdmin && tenantName && (
          <div>
            <div className="text-xs text-muted-foreground">{t('auditLogs.detail.tenant')}</div>
            <Badge variant="outline" className="mt-1">{tenantName}</Badge>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 2: Verify `UserAvatar` props**

```bash
sed -n '1,80p' boilerplateFE/src/components/common/UserAvatar.tsx
```

The current shared component accepts `firstName`, `lastName`, and `size`. Keep using that shape so audit metadata aligns with existing Identity/profile surfaces.

- [ ] **Step 3: Build + lint**

```bash
cd boilerplateFE && npm run build && npm run lint
```

Expected: clean.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/src/features/audit-logs/components/AuditMetadataCard.tsx
git commit -m "feat(audit-logs): add AuditMetadataCard (actor + IP + trace + agent attribution)"
```

---

## Task 4: `AuditLogDetailPage` route + assembly

**Files:**
- Create: `boilerplateFE/src/features/audit-logs/pages/AuditLogDetailPage.tsx`
- Modify: `boilerplateFE/src/config/routes.config.ts` (add route constant)
- Modify: `boilerplateFE/src/routes/routes.tsx` (lazy-load + register)
- Modify: `boilerplateFE/src/i18n/locales/en/translation.json` (new keys)

- [ ] **Step 1: Add route constant**

In `routes.config.ts`, locate the `AUDIT_LOGS` block (~line 52) and add `DETAIL`:

```ts
AUDIT_LOGS: {
  LIST: '/audit-logs',
  DETAIL: '/audit-logs/:id',
  getDetail: (id: string) => `/audit-logs/${id}`,
},
```

- [ ] **Step 2: Add EN translations**

Edit `boilerplateFE/src/i18n/locales/en/translation.json`. Locate the `auditLogs` namespace and add the `detail` and `timeline` sub-namespaces. If the existing JSON already has `auditLogs.title` etc., insert these alongside:

```json
"auditLogs": {
  "title": "Audit Logs",
  "timeline": {
    "totalEvents": "{{count}} event",
    "totalEvents_other": "{{count}} events",
    "windowLabel": "in the {{window}}",
    "windowOptions": {
      "lastHour": "last hour",
      "last24h": "last 24 hours",
      "last7d": "last 7 days",
      "last30d": "last 30 days",
      "custom": "selected range"
    },
    "noEvents": "No events in this window",
    "truncatedBanner": "Showing the most recent 2,000 events for the timeline. Refine the filter for accurate counts.",
    "ariaLabel": "Events over time: {{count}} events total"
  },
  "detail": {
    "title": "Audit log detail",
    "back": "Back to audit logs",
    "actor": "Actor",
    "systemAction": "System action (no user)",
    "unknownActor": "Unknown user",
    "agentAction": "Performed by an AI agent",
    "onBehalfOf": "On behalf of",
    "agentPrincipal": "Agent principal",
    "agentRun": "Agent run",
    "ipAddress": "IP address",
    "correlationId": "Correlation / trace ID",
    "viewSameConversation": "View all events with this correlation ID",
    "tenant": "Tenant",
    "eventPayload": "Event payload",
    "noPayload": "No payload data",
    "rawPayload": "Raw payload (not valid JSON)",
    "notFound": "Audit log not found"
  }
}
```

(Preserve existing keys; only add the new ones.)

- [ ] **Step 3: Implement the detail page**

```tsx
import { useParams, Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { format } from 'date-fns';
import { PageHeader } from '@/components/common';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import { useAuditLog } from '@/features/audit-logs/api/audit-logs.queries';
import { ROUTES } from '@/config';
import { useAuthStore, selectUser } from '@/stores';
import { JsonView } from '../components/JsonView';
import { AuditMetadataCard } from '../components/AuditMetadataCard';

type StatusVariant = 'failed' | 'info' | 'pending' | 'healthy';

function statusForAction(action: string): StatusVariant {
  const a = action.toLowerCase();
  if (a.includes('delete') || a.includes('revoke') || a.includes('suspend')) return 'failed';
  if (a.includes('login') || a.includes('logout')) return 'info';
  if (a.includes('create')) return 'healthy';
  return 'info';
}

export default function AuditLogDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { t } = useTranslation();
  const user = useAuthStore(selectUser);
  const isSuperAdmin = !user?.tenantId;

  const { data: log, isLoading, isError, error } = useAuditLog(id);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Spinner />
      </div>
    );
  }

  // Treat NotFound as 404 surface
  if (isError || !log) {
    const status = (error as { response?: { status?: number } } | null)?.response?.status;
    if (status === 404) {
      return <Navigate to="/404" replace />;
    }
    return (
      <Card>
        <CardContent className="py-12 text-center text-muted-foreground">
          {t('auditLogs.detail.notFound')}
        </CardContent>
      </Card>
    );
  }

  const variant = statusForAction(log.action);
  const heading = `${log.entityType} ${log.action.toLowerCase()}`;

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('auditLogs.detail.title')}
        breadcrumbs={[
          { label: t('auditLogs.title'), to: ROUTES.AUDIT_LOGS.LIST },
          { label: log.action },
        ]}
      />

      {/* Header band */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold gradient-text">{heading}</h1>
          <p className="text-sm text-muted-foreground mt-1 font-mono">{log.entityId}</p>
          <p className="text-xs text-muted-foreground mt-2">
            {format(new Date(log.performedAt), 'PPpp')}
          </p>
        </div>
        <Badge variant={variant}>{log.action}</Badge>
      </div>

      {/* Two-column body */}
      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <Card variant="glass">
          <CardContent className="pt-6">
            <h2 className="text-sm font-medium mb-4">{t('auditLogs.detail.eventPayload')}</h2>
            <JsonView payload={log.changes} />
          </CardContent>
        </Card>

        <AuditMetadataCard log={log} isSuperAdmin={isSuperAdmin} />
      </div>
    </div>
  );
}
```

> Use `PageHeader.breadcrumbs` for the back affordance. `useBackNavigation` is deprecated in this codebase and no longer renders visible header UI.

- [ ] **Step 4: Register the route**

Edit `boilerplateFE/src/routes/routes.tsx`. Add the lazy import next to the existing `AuditLogsPage`:

```tsx
const AuditLogsPage = lazy(() => import('@/features/audit-logs/pages/AuditLogsPage'));
const AuditLogDetailPage = lazy(() => import('@/features/audit-logs/pages/AuditLogDetailPage'));
```

Then add the detail route inside the existing Audit Logs permission group:

```tsx
{
  element: <PermissionGuard permission={PERMISSIONS.System.ViewAuditLogs} />,
  children: [
    { path: ROUTES.AUDIT_LOGS.LIST, element: <AuditLogsPage /> },
    { path: ROUTES.AUDIT_LOGS.DETAIL, element: <AuditLogDetailPage /> },
  ],
},
```

- [ ] **Step 5: Build + lint**

```bash
cd boilerplateFE && npm run build && npm run lint
```

Expected: clean.

- [ ] **Step 6: Visual smoke check via Playwright MCP**

With the test app running:
1. Login as superadmin.
2. Navigate to `/audit-logs`.
3. Click the first row.
4. Verify the URL becomes `/audit-logs/{guid}`.
5. Verify the JSON payload renders with key/value highlighting and line numbers.
6. Verify the metadata card shows actor + IP (when present) + correlation ID with copy button.
7. Click the breadcrumb back link — verify it returns to `/audit-logs`.
8. Visit `/audit-logs/00000000-0000-0000-0000-000000000000` directly — verify 404 redirect.

Capture screenshots of the detail page.

- [ ] **Step 7: Commit**

```bash
git add boilerplateFE/src/features/audit-logs/pages/AuditLogDetailPage.tsx \
        boilerplateFE/src/config/routes.config.ts \
        boilerplateFE/src/routes/routes.tsx \
        boilerplateFE/src/i18n/locales/en/translation.json
git commit -m "feat(audit-logs): add AuditLogDetailPage route with JsonView + metadata card"
```

---

## Task 5: `AuditTimelineHero` component

**Files:**
- Create: `boilerplateFE/src/features/audit-logs/components/AuditTimelineHero.tsx`

- [ ] **Step 1: Implement bucketing + hero**

```tsx
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import type { AuditLog } from '@/types';
import { cn } from '@/lib/utils';

interface AuditTimelineHeroProps {
  rows: AuditLog[];
  totalCount: number;
  windowMs: number;
  truncated: boolean;
  className?: string;
}

interface Bucket { start: number; count: number }

function bucketRows(rows: AuditLog[], windowMs: number, now: number): Bucket[] {
  const bucketSize = windowMs <= 60 * 60 * 1000 ? 60 * 1000           // ≤1h → per-minute
                   : windowMs <= 24 * 60 * 60 * 1000 ? 60 * 60 * 1000 // ≤24h → per-hour
                   : 24 * 60 * 60 * 1000;                              // > 24h → per-day
  const numBuckets = Math.max(1, Math.ceil(windowMs / bucketSize));
  const buckets: Bucket[] = [];
  const startEdge = now - numBuckets * bucketSize;
  for (let i = 0; i < numBuckets; i++) {
    buckets.push({ start: startEdge + i * bucketSize, count: 0 });
  }
  for (const row of rows) {
    const t = new Date(row.performedAt).getTime();
    const idx = Math.floor((t - startEdge) / bucketSize);
    if (idx >= 0 && idx < buckets.length) buckets[idx].count += 1;
  }
  return buckets;
}

function buildSparklinePath(buckets: Bucket[], width: number, height: number): string {
  if (buckets.length === 0) return '';
  const max = Math.max(1, ...buckets.map((b) => b.count));
  const stepX = width / Math.max(1, buckets.length - 1);
  return buckets
    .map((b, i) => {
      const x = i * stepX;
      const y = height - (b.count / max) * height;
      return `${i === 0 ? 'M' : 'L'} ${x.toFixed(1)} ${y.toFixed(1)}`;
    })
    .join(' ');
}

export function AuditTimelineHero({ rows, totalCount, windowMs, truncated, className }: AuditTimelineHeroProps) {
  const { t } = useTranslation();
  const now = Date.now();
  const buckets = useMemo(() => bucketRows(rows, windowMs, now), [rows, windowMs, now]);
  const path = useMemo(() => buildSparklinePath(buckets, 800, 60), [buckets]);
  const isEmpty = totalCount === 0;

  const windowKey = windowMs <= 60 * 60 * 1000 ? 'lastHour'
                   : windowMs <= 24 * 60 * 60 * 1000 ? 'last24h'
                   : windowMs <= 7 * 24 * 60 * 60 * 1000 ? 'last7d'
                   : 'last30d';

  return (
    <div className={cn('surface-glass rounded-2xl p-6 relative overflow-hidden', className)}>
      <div className="absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-primary/40 to-transparent" />
      <div className="grid gap-6 md:grid-cols-[200px_1fr]">
        <div>
          <div className="text-3xl font-semibold gradient-text tabular-nums">
            {totalCount.toLocaleString()}
          </div>
          <div className="text-sm text-muted-foreground">
            {t('auditLogs.timeline.totalEvents', { count: totalCount })}
          </div>
          <div className="text-xs text-muted-foreground mt-1">
            {t('auditLogs.timeline.windowLabel', { window: t(`auditLogs.timeline.windowOptions.${windowKey}`) })}
          </div>
        </div>
        <div className="flex flex-col justify-center">
          {isEmpty ? (
            <div className="text-sm text-muted-foreground italic">
              {t('auditLogs.timeline.noEvents')}
            </div>
          ) : (
            <svg
              viewBox="0 0 800 60"
              preserveAspectRatio="none"
              className="w-full h-[60px]"
              role="img"
              aria-label={t('auditLogs.timeline.ariaLabel', { count: totalCount })}
            >
              <path d={path} fill="none" stroke="var(--primary)" strokeWidth="1.5" strokeLinejoin="round" />
              <path d={`${path} L 800 60 L 0 60 Z`} fill="url(#audit-spark-grad)" opacity="0.3" />
              <defs>
                <linearGradient id="audit-spark-grad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="var(--primary)" stopOpacity="0.4" />
                  <stop offset="100%" stopColor="var(--primary)" stopOpacity="0" />
                </linearGradient>
              </defs>
            </svg>
          )}
          <ul className="sr-only" aria-label="Timeline buckets">
            {buckets.map((b) => (
              <li key={b.start}>{new Date(b.start).toISOString()}: {b.count}</li>
            ))}
          </ul>
        </div>
      </div>
      {truncated && (
        <div className="mt-4 text-xs text-amber-700 dark:text-amber-400 bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-900 rounded-md px-3 py-2">
          {t('auditLogs.timeline.truncatedBanner')}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Build + lint**

```bash
cd boilerplateFE && npm run build && npm run lint
```

Expected: clean.

- [ ] **Step 3: Commit**

```bash
git add boilerplateFE/src/features/audit-logs/components/AuditTimelineHero.tsx
git commit -m "feat(audit-logs): add AuditTimelineHero with client-side bucketing"
```

---

## Task 6: `AuditLogsPage` integration — timeline hero + click-through

**Files:**
- Modify: `boilerplateFE/src/features/audit-logs/pages/AuditLogsPage.tsx`

- [ ] **Step 1: Read the current file**

```bash
cat boilerplateFE/src/features/audit-logs/pages/AuditLogsPage.tsx | head -80
```

Locate the existing data fetch (`useAuditLogs`), filter state, and table block. Note the filter object shape (most likely `{ pageNumber, pageSize, dateFrom, dateTo, ... }`).

- [ ] **Step 2: Add timeline data fetch**

In the page component, after the existing `useAuditLogs` call, add a second query restricted to the active window for the hero. Decide window from `dateFrom/dateTo` if set, else default to last 24h:

```tsx
import { AuditTimelineHero } from '../components/AuditTimelineHero';
import { useAuditLogs } from '../api/audit-logs.queries';
import { useNavigate } from 'react-router-dom';
import { ROUTES } from '@/config';

// Add useEffect to the existing React import because the timeline debounce helper uses it.
// import { useEffect, useMemo, useState, Fragment } from 'react';

// Inside the component:
const navigate = useNavigate();

// Compute window for the timeline (separate from table paging)
const now = useMemo(() => Date.now(), []);
const windowMs = useMemo(() => {
  if (list.filters.dateFrom && list.filters.dateTo) {
    return new Date(list.filters.dateTo).getTime() - new Date(list.filters.dateFrom).getTime();
  }
  return 24 * 60 * 60 * 1000; // default: last 24h
}, [list.filters.dateFrom, list.filters.dateTo]);

const timelineFilters = useMemo(() => ({
  ...list.filters,      // share entityType / action / performedBy / search filters
  pageNumber: 1,
  pageSize: 2000,       // capacity per spec §4.4
  dateFrom: list.filters.dateFrom ?? new Date(now - windowMs).toISOString(),
  dateTo: list.filters.dateTo ?? new Date(now).toISOString(),
  sortBy: 'performedAt',
  sortDescending: true,
}), [list.filters, now, windowMs]);

const debouncedTimelineFilters = useDebouncedValue(timelineFilters, 500);
const { data: timelineData } = useAuditLogs(debouncedTimelineFilters);
const timelineRows = timelineData?.data ?? [];
const timelineTotal = timelineData?.pagination?.totalCount ?? timelineRows.length;
const truncated = timelineTotal > 2000;
```

This matches the frontend `PaginatedResponse<T>` shape (`data: T[]`, `pagination.totalCount`). Do not use an `.items` fallback unless the API type changes.

- [ ] **Step 3: Render the hero above the filter row**

In the JSX, place `<AuditTimelineHero>` immediately under the `<PageHeader>` and above the filter row:

```tsx
<PageHeader
  title={t('auditLogs.title')}
  actions={canExport ? <ExportButton reportType="AuditLogs" filters={exportFilters} /> : undefined}
/>

<AuditTimelineHero
  rows={timelineRows}
  totalCount={timelineTotal}
  windowMs={windowMs}
  truncated={truncated}
  className="mb-6"
/>
```

Leave the current `ListToolbar`, `ListPageState`, `Table`, and `Pagination` blocks below this hero.

- [ ] **Step 4: Wire row click-through**

Locate the existing `<TableRow>` for each log item. If the row has an inline expand/collapse, remove that handler and the inline expanded panel (the detail page replaces it). Replace the row with a clickable variant:

```tsx
<TableRow
  key={log.id}
  className="cursor-pointer hover:bg-[var(--hover-bg)]"
  onClick={() => navigate(ROUTES.AUDIT_LOGS.getDetail(log.id))}
>
  <TableCell>
    <span className="font-medium">{log.entityType}</span>
    <span className="ms-1 text-xs text-muted-foreground">
      {log.entityId.substring(0, 8)}...
    </span>
  </TableCell>
  <TableCell>
    <Badge variant={AUDIT_ACTION_VARIANTS[log.action] ?? 'secondary'}>
      {log.action}
    </Badge>
  </TableCell>
  <TableCell>{log.performedByName ?? '-'}</TableCell>
  <TableCell>{formatDateTime(log.performedAt)}</TableCell>
  <TableCell>{log.ipAddress ?? '-'}</TableCell>
</TableRow>
```

If the existing rows have a "Details" button or an inline expansion arrow, remove those — the whole row click is the new affordance.

- [ ] **Step 5: Add a 500ms debounce for the timeline query**

Keep the table filters responsive through `useListPage`, but debounce the heavier 2,000-row timeline query so typing in search does not fire one large request per keypress. Add a tiny local helper above the page component:

```tsx
function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const id = window.setTimeout(() => setDebounced(value), delayMs);
    return () => window.clearTimeout(id);
  }, [value, delayMs]);

  return debounced;
}
```

Then pass the debounced filters into the timeline query:

```tsx
const debouncedTimelineFilters = useDebouncedValue(timelineFilters, 500);
const { data: timelineData } = useAuditLogs(debouncedTimelineFilters);
```

Do not refactor `useListPage` for this phase.

- [ ] **Step 6: Build + lint**

```bash
cd boilerplateFE && npm run build && npm run lint
```

Expected: clean.

- [ ] **Step 7: Visual verification via Playwright MCP**

1. Navigate to `/audit-logs`.
2. Confirm the timeline hero renders above the filter row with a sparkline and an event count.
3. Apply a date filter narrower than the default window — confirm the sparkline updates and the bucketing stays sensible.
4. Click a row — confirm navigation to the detail page works.
5. Apply a filter that yields zero results — confirm the empty-state message renders ("No events in this window").
6. Test with a tenant-admin login — confirm no cross-tenant rows leak in.

Capture a before/after screenshot of the list page.

- [ ] **Step 8: Commit**

```bash
git add boilerplateFE/src/features/audit-logs/pages/AuditLogsPage.tsx
git commit -m "feat(audit-logs): integrate timeline hero + row click-through to detail page"
```

---

## Task 7: Feature flags — hero metric strip + status pill

**Files:**
- Create: `boilerplateFE/src/features/feature-flags/components/FeatureFlagStatStrip.tsx`
- Modify: `boilerplateFE/src/features/feature-flags/pages/FeatureFlagsPage.tsx`
- Modify: `boilerplateFE/src/i18n/locales/en/translation.json` (new keys under `featureFlags.stats`)

- [ ] **Step 1: Add EN translations**

In `translation.json`, under `featureFlags`:

```json
"featureFlags": {
  "title": "Feature Flags",
  "stats": {
    "enabledFlags": "Enabled flags",
    "tenantOverrides": "Tenant overrides",
    "optedOut": "Opted-out tenants",
    "tenantsOverridingOne": "{{count}} tenant overrides",
    "tenantsOverridingOther": "{{count}} tenants override",
    "noOverrides": "No overrides"
  },
  "status": {
    "on": "On",
    "off": "Off",
    "perTenant": "Per-tenant"
  }
}
```

- [ ] **Step 2: Implement `FeatureFlagStatStrip`**

```tsx
import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';

interface FeatureFlagStatStripProps {
  enabledCount: number;
  totalCount: number;
  tenantOverrideCount: number;
  tenantsOverridingCount: number;
  optedOutCount: number;
  className?: string;
}

function MiniSparkline() {
  // Flat baseline: current API does not expose 30d history, so do not fake a trend.
  const height = 30;
  const width = 100;
  const path = `M 0 ${height / 2} L ${width} ${height / 2}`;
  return (
    <svg viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="none" className="h-7 w-full">
      <path d={path} fill="none" stroke="var(--primary)" strokeWidth="1.5" strokeLinecap="round" />
    </svg>
  );
}

export function FeatureFlagStatStrip({
  enabledCount, totalCount, tenantOverrideCount, tenantsOverridingCount, optedOutCount, className,
}: FeatureFlagStatStripProps) {
  const { t } = useTranslation();

  return (
    <div className={cn('grid gap-4 sm:grid-cols-3', className)}>
      <Card variant="elevated">
        <CardContent className="pt-5">
          <div className="text-xs text-muted-foreground uppercase tracking-wide">
            {t('featureFlags.stats.enabledFlags')}
          </div>
          <div className="mt-2 flex items-baseline gap-2">
            <span className="text-2xl font-semibold tabular-nums gradient-text">{enabledCount}</span>
            <span className="text-sm text-muted-foreground">/ {totalCount}</span>
          </div>
          <MiniSparkline />
        </CardContent>
      </Card>

      <Card variant="elevated">
        <CardContent className="pt-5">
          <div className="text-xs text-muted-foreground uppercase tracking-wide">
            {t('featureFlags.stats.tenantOverrides')}
          </div>
          <div className="mt-2 text-2xl font-semibold tabular-nums">{tenantOverrideCount}</div>
          <div className="text-xs text-muted-foreground mt-1">
            {tenantsOverridingCount > 0
              ? t('featureFlags.stats.tenantsOverridingOne', { count: tenantsOverridingCount })
              : t('featureFlags.stats.noOverrides')}
          </div>
        </CardContent>
      </Card>

      <Card variant="elevated">
        <CardContent className="pt-5">
          <div className="text-xs text-muted-foreground uppercase tracking-wide">
            {t('featureFlags.stats.optedOut')}
          </div>
          <div className="mt-2 text-2xl font-semibold tabular-nums gradient-text">{optedOutCount}</div>
        </CardContent>
      </Card>
    </div>
  );
}
```

The `MiniSparkline` is intentionally flat because Phase 2 doesn't ship a 30d enabled-history data source. Do not encode ratios as curved "trends"; that would imply historical data the user does not have. If a 30d series later becomes available, `MiniSparkline` is replaced at one call site.

- [ ] **Step 3: Wire it into `FeatureFlagsPage`**

Edit `FeatureFlagsPage.tsx`. After the `PageHeader`, derive counts from the existing list data and render the strip:

```tsx
import { FeatureFlagStatStrip } from '../components/FeatureFlagStatStrip';

// Inside the component, after const flags = ... :
const isBooleanOn = (value: string) => value.toLowerCase() === 'true';
const enabledCount = flags.filter((f) =>
  f.valueType === 'Boolean' ? isBooleanOn(f.resolvedValue ?? f.defaultValue) : false
).length;
const tenantOverrideCount = flags.filter((f) => f.tenantOverrideValue !== null).length;
const tenantsOverridingCount = tenantOverrideCount > 0 ? 1 : 0; // current DTO exposes only the active tenant's override
const optedOutCount = flags.filter((f) =>
  f.valueType === 'Boolean' && f.tenantOverrideValue?.toLowerCase() === 'false'
).length;

// In JSX, after PageHeader:
<FeatureFlagStatStrip
  enabledCount={enabledCount}
  totalCount={flags.length}
  tenantOverrideCount={tenantOverrideCount}
  tenantsOverridingCount={tenantsOverridingCount}
  optedOutCount={optedOutCount}
  className="mb-6"
/>
```

> The current `FeatureFlagDto` exposes `tenantOverrideValue`, not arrays of tenant overrides or opt-outs. Keep the strip honest: show counts only for the active result set / active tenant unless a future backend stats endpoint provides global counts.

- [ ] **Step 4: Add status pill column**

In the existing flag table, add a "Status" column:

```tsx
import type { TFunction } from 'i18next';

function flagStatusVariant(flag: FeatureFlagDto): 'healthy' | 'failed' | 'info' {
  if (flag.tenantOverrideValue !== null) return 'info';
  return flag.valueType === 'Boolean' && flag.resolvedValue.toLowerCase() === 'true' ? 'healthy' : 'failed';
}

function flagStatusLabel(t: TFunction, flag: FeatureFlagDto): string {
  if (flag.tenantOverrideValue !== null) return t('featureFlags.status.perTenant');
  return flag.valueType === 'Boolean' && flag.resolvedValue.toLowerCase() === 'true'
    ? t('featureFlags.status.on')
    : t('featureFlags.status.off');
}

// In the table:
<TableCell>
  <Badge variant={flagStatusVariant(flag)}>{flagStatusLabel(t, flag)}</Badge>
</TableCell>
```

- [ ] **Step 5: Strip any stray `gradient-hero` classes**

```bash
grep -n "gradient-hero" boilerplateFE/src/features/feature-flags/
```

Replace any hits with the appropriate J4 surface (`surface-glass` or remove entirely). The `gradient-hero` class is the legacy solid copper from before Phase 0.

- [ ] **Step 6: Build + lint + visual check**

```bash
cd boilerplateFE && npm run build && npm run lint
```

Then via Playwright MCP: visit `/feature-flags` and confirm the three stat cards render, the table has a Status column with copper/red/info pills, and there's no leftover gradient-hero.

- [ ] **Step 7: Commit**

```bash
git add boilerplateFE/src/features/feature-flags/ \
        boilerplateFE/src/i18n/locales/en/translation.json
git commit -m "feat(feature-flags): add stat strip + status pill column + J4 polish"
```

---

## Task 8: API keys — KPI badge + secret reveal redesign

**Files:**
- Modify: `boilerplateFE/src/features/api-keys/components/ApiKeySecretDisplay.tsx` (redesign existing one-time secret dialog)
- Modify: `boilerplateFE/src/features/api-keys/pages/ApiKeysPage.tsx`
- Modify: `boilerplateFE/src/i18n/locales/en/translation.json` (`apiKeys.reveal.*`)

- [ ] **Step 1: Add EN translations**

```json
"apiKeys": {
  "title": "API Keys",
  "reveal": {
    "title": "API key created",
    "warning": "Shown once. Copy it now — it cannot be recovered later.",
    "copyButton": "Copy",
    "copiedConfirmation": "Copied",
    "doneButton": "Done",
    "closeConfirmTitle": "You haven't copied the key yet",
    "closeConfirmDescription": "If you close this dialog without copying, the key cannot be retrieved. Close anyway?",
    "ariaAnnouncement": "API key created. Copy it now."
  },
  "kpi": {
    "active": "{{count}} active",
    "expiringSoon": "{{count}} expiring in 30d"
  }
}
```

- [ ] **Step 2: Redesign the existing `ApiKeySecretDisplay`**

Do not create a second secret-display component unless the existing file becomes hard to follow. The current code already owns the dialog lifecycle (`open`, `onOpenChange`, `response`); keep that contract and redesign the internals.

```tsx
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertTriangle, Copy, Check } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { ConfirmDialog } from '@/components/common';
import type { CreateApiKeyResponse } from '../api';

interface ApiKeySecretDisplayProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  response: CreateApiKeyResponse;
}

export function ApiKeySecretDisplay({ open, onOpenChange, response }: ApiKeySecretDisplayProps) {
  const { t } = useTranslation();
  const secretRef = useRef(response.fullKey);
  const [copied, setCopied] = useState(false);
  const [confirmingClose, setConfirmingClose] = useState(false);

  useEffect(() => { secretRef.current = response.fullKey; }, [response.fullKey]);

  const handleCopy = () => {
    navigator.clipboard.writeText(secretRef.current).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  };

  const handleAttemptClose = () => {
    if (!copied) { setConfirmingClose(true); return; }
    onOpenChange(false);
  };

  return (
    <Dialog open={open} onOpenChange={(next) => (next ? onOpenChange(true) : handleAttemptClose())}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{t('apiKeys.reveal.title')}</DialogTitle>
          <DialogDescription>{response.name}</DialogDescription>
        </DialogHeader>

        <div role="alert" aria-live="assertive" className="sr-only">
          {t('apiKeys.reveal.ariaAnnouncement')}
        </div>

        <div className="space-y-4">
          <div className="flex items-start gap-3 rounded-lg border border-amber-300 dark:border-amber-700 bg-amber-50 dark:bg-amber-950/40 p-4">
            <AlertTriangle className="h-5 w-5 text-amber-600 dark:text-amber-400 shrink-0 mt-0.5" />
            <div className="text-sm text-amber-800 dark:text-amber-200">{t('apiKeys.reveal.warning')}</div>
          </div>

          <div className="rounded-lg border bg-card p-4 font-mono text-sm break-all gradient-text" dir="ltr">
            {secretRef.current}
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleAttemptClose}>{t('apiKeys.reveal.doneButton')}</Button>
          <Button onClick={handleCopy}>
            {copied ? <><Check className="h-4 w-4 me-1" />{t('apiKeys.reveal.copiedConfirmation')}</>
                    : <><Copy className="h-4 w-4 me-1" />{t('apiKeys.reveal.copyButton')}</>}
          </Button>
        </DialogFooter>
      </DialogContent>

      <ConfirmDialog
        isOpen={confirmingClose}
        onClose={() => setConfirmingClose(false)}
        title={t('apiKeys.reveal.closeConfirmTitle')}
        description={t('apiKeys.reveal.closeConfirmDescription')}
        confirmLabel={t('common.close')}
        onConfirm={() => onOpenChange(false)}
      />
    </Dialog>
  );
}
```

- [ ] **Step 3: Keep `ApiKeysPage` using `ApiKeySecretDisplay`**

`ApiKeysPage.tsx` already renders `<ApiKeySecretDisplay open={!!createdKey} onOpenChange={() => setCreatedKey(null)} response={createdKey} />`. Keep that wiring; only adjust it if the close callback needs to accept the boolean argument.

- [ ] **Step 4: Add KPI badge to `PageHeader`**

In `ApiKeysPage.tsx`, compute counts from the existing list:

```tsx
const activeCount = keys.filter((k) => !k.revokedAt).length;
const expiringCount = keys.filter((k) => {
  if (k.revokedAt || !k.expiresAt) return false;
  const days = (new Date(k.expiresAt).getTime() - Date.now()) / 86400000;
  return days >= 0 && days <= 30;
}).length;
```

Pass the counts into `<PageHeader>` either via the existing `subtitle` slot or alongside the title:

```tsx
<PageHeader
  title={t('apiKeys.title')}
  subtitle={`${t('apiKeys.kpi.active', { count: activeCount })} · ${t('apiKeys.kpi.expiringSoon', { count: expiringCount })}`}
  actions={...}
/>
```

If `PageHeader` doesn't accept a `subtitle`, render the KPI as a small badge row under the existing title:

```tsx
<div className="text-xs text-primary -mt-2 mb-4">
  {t('apiKeys.kpi.active', { count: activeCount })} · {t('apiKeys.kpi.expiringSoon', { count: expiringCount })}
</div>
```

- [ ] **Step 5: Build + lint + visual check**

```bash
cd boilerplateFE && npm run build && npm run lint
```

Then via Playwright MCP: create a new API key, confirm the redesigned reveal screen renders, copy succeeds, and that closing without copying triggers the confirm dialog.

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/features/api-keys/ \
        boilerplateFE/src/i18n/locales/en/translation.json
git commit -m "feat(api-keys): redesigned secret reveal + KPI badge"
```

---

## Task 9: Settings — sticky sidebar + glass groups + sticky save bar

**Files:**
- Create: `boilerplateFE/src/features/settings/components/SettingsCategoryNav.tsx`
- Modify: `boilerplateFE/src/features/settings/pages/SettingsPage.tsx`

- [ ] **Step 1: Read the current SettingsPage**

```bash
sed -n '1,80p' boilerplateFE/src/features/settings/pages/SettingsPage.tsx
sed -n '180,260p' boilerplateFE/src/features/settings/pages/SettingsPage.tsx
```

Identify: how categories are computed, how the active tab is set, how dirty state is tracked, where the save button currently lives.

While reading, remove the duplicate unreachable `return groups.map((g) => g.category);` in the `categories` memo if it is still present.

- [ ] **Step 2: Implement `SettingsCategoryNav`**

```tsx
import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/utils';

export interface CategoryNavItem {
  category: string;
  label: string;
  count?: number;
}

interface SettingsCategoryNavProps {
  items: CategoryNavItem[];
  activeCategory: string;
  onSelect: (category: string) => void;
  className?: string;
}

export function SettingsCategoryNav({ items, activeCategory, onSelect, className }: SettingsCategoryNavProps) {
  const { t } = useTranslation();

  // Mobile: horizontal tabs
  return (
    <>
      <nav
        aria-label={t('settings.categoriesNav', 'Settings categories')}
        className={cn('lg:hidden flex gap-2 overflow-x-auto pb-2 -mx-4 px-4', className)}
      >
        {items.map((item) => (
          <button
            key={item.category}
            type="button"
            aria-current={item.category === activeCategory ? 'true' : undefined}
            onClick={() => onSelect(item.category)}
            className={cn(
              'shrink-0 rounded-lg px-3 py-1.5 text-sm whitespace-nowrap transition-colors',
              item.category === activeCategory
                ? 'bg-[var(--active-bg)] text-[var(--active-text)]'
                : 'hover:bg-[var(--hover-bg)] text-muted-foreground'
            )}
          >
            {item.label}
            {typeof item.count === 'number' && (
              <span className="ml-2 text-xs opacity-70">{item.count}</span>
            )}
          </button>
        ))}
      </nav>

      {/* Desktop: sticky sidebar */}
      <nav
        aria-label={t('settings.categoriesNav', 'Settings categories')}
        className={cn(
          'hidden lg:flex flex-col gap-1 sticky top-24 self-start',
          'bottom-[var(--settings-save-bar-h,0px)]',
          className
        )}
        onKeyDown={(e) => {
          if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;
          const idx = items.findIndex((i) => i.category === activeCategory);
          if (idx === -1) return;
          const nextIdx = e.key === 'ArrowDown'
            ? Math.min(items.length - 1, idx + 1)
            : Math.max(0, idx - 1);
          onSelect(items[nextIdx].category);
          e.preventDefault();
        }}
      >
        {items.map((item) => (
          <button
            key={item.category}
            type="button"
            aria-current={item.category === activeCategory ? 'true' : undefined}
            onClick={() => onSelect(item.category)}
            className={cn(
              'rounded-lg px-3 py-2 text-sm text-start transition-colors',
              item.category === activeCategory
                ? 'bg-[var(--active-bg)] text-[var(--active-text)] font-medium'
                : 'hover:bg-[var(--hover-bg)] text-muted-foreground'
            )}
          >
            {item.label}
            {typeof item.count === 'number' && (
              <span className="ms-2 text-xs opacity-70">{item.count}</span>
            )}
          </button>
        ))}
      </nav>
    </>
  );
}
```

- [ ] **Step 3: Restructure `SettingsPage` layout**

Replace the existing top-tabs container with a 2-column grid (sidebar + content) on `≥lg`. Pseudocode:

```tsx
import { SettingsCategoryNav } from '../components/SettingsCategoryNav';

const items = useMemo(() => groups.map((g) => ({
  category: g.category,
  label: resolveCategoryLabel(t, g.category),
  count: g.settings.length,
})), [groups, t]);

const hasUnsavedChanges = changedCount > 0;
const handleResetAll = () => {
  if (!groups) return;
  const values: Record<string, string> = {};
  for (const group of groups) {
    for (const setting of group.settings) {
      values[setting.key] = setting.value;
    }
  }
  setLocalValues(values);
};

return (
  <div
    className="space-y-6"
    style={{ ['--settings-save-bar-h' as string]: hasUnsavedChanges ? '72px' : '0px' }}
  >
    <PageHeader title={t('settings.title')} subtitle={t('settings.subtitle')} />

    <div className="grid gap-6 lg:grid-cols-[200px_1fr]">
      <SettingsCategoryNav
        items={items}
        activeCategory={activeTab}
        onSelect={setActiveTab}
      />

      <div className="space-y-4">
        {activeGroup && (
          <Card variant="glass">
            <CardContent className="pt-6 space-y-4">
              <h2 className="text-lg font-semibold">{resolveCategoryLabel(t, activeGroup.category)}</h2>
              {activeGroup.settings.map((s) => (
                <SettingInput key={s.id} setting={s} ... />
              ))}
            </CardContent>
          </Card>
        )}
      </div>
    </div>

    {/* Sticky save bar */}
    {hasUnsavedChanges && (
      <div className="fixed bottom-4 end-4 z-40 flex items-center gap-3 rounded-2xl surface-glass-strong px-4 py-3 shadow-xl">
        <Badge variant="info">{t('settings.unsavedChanges', { count: changedCount })}</Badge>
        <Button variant="ghost" onClick={handleResetAll}>{t('common.reset')}</Button>
        <Button onClick={handleSave} disabled={isPending}>
          {isPending ? <Spinner /> : t('settings.save')}
        </Button>
      </div>
    )}
  </div>
);
```

Carry over the existing `changedCount`, `isPending`, `handleSave`, `localValues`, and per-setting `handleReset` behavior. Add only `handleResetAll` for the sticky save bar's reset action, and remove the old save button from `PageHeader.actions` so there is one primary save surface.

- [ ] **Step 4: Per-tenant override badges**

Where each `SettingInput` is rendered, if the setting has a tenant-override flag (check the existing `SystemSetting` type for the boolean), append a small badge:

```tsx
{setting.isOverridden && (
  <Badge variant="outline" className="ms-2 text-[10px] bg-primary/10 text-primary">
    {t('settings.overridden')}
  </Badge>
)}
```

Add `settings.overridden: "Tenant override"` to the EN translations.

- [ ] **Step 5: Build + lint + visual check**

```bash
cd boilerplateFE && npm run build && npm run lint
```

Then via Playwright MCP: navigate to `/settings`, confirm the sidebar appears on desktop and tabs on narrower viewports, edit a setting and confirm the sticky save bar renders, then save and confirm the bar disappears.

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/features/settings/ \
        boilerplateFE/src/i18n/locales/en/translation.json
git commit -m "feat(settings): sticky sidebar nav + glass groups + sticky save bar"
```

---

## Task 10: Code review pass + final polish + EN sweep

**Files:** all of the above (review-only, plus minor edits as findings dictate)

- [ ] **Step 1: Run a final build + lint sweep across the whole repo**

```bash
cd boilerplateFE && npm run build && npm run lint
cd ../boilerplateBE && dotnet build src/Starter.Api
```

Expected: clean.

- [ ] **Step 2: Visual regression check on Identity cluster pages**

Phase 1 must remain unchanged. Via Playwright MCP, capture screenshots of:
- `/dashboard`
- `/users`
- `/roles`
- `/tenants` (super-admin) or `/organization` (tenant-admin)
- `/profile`

Compare to a quick mental baseline (or git checkout main + screenshot if regressions are suspected).

- [ ] **Step 3: RTL pass**

Toggle the language switcher to Arabic. Visit each Phase 2 page (`/audit-logs`, `/audit-logs/{id}`, `/feature-flags`, `/api-keys`, `/settings`). Confirm:
- Audit timeline sparkline does not mirror.
- `JsonView` keys stay LTR.
- All directional borders/margins respect `ltr:`/`rtl:` prefixes.
- The settings sticky save bar appears on the start side (right in LTR, left in RTL).

- [ ] **Step 4: Permission matrix check**

Login as each role and confirm gates:
- Super-admin: sees all four pages, sees tenant chip on audit detail.
- Tenant admin: sees `/audit-logs`, `/feature-flags`, `/api-keys`, `/settings` for their tenant; cannot see other tenants' audit rows.
- Regular user: should not see Platform admin nav items at all.

- [ ] **Step 5: Translation key sweep**

```bash
cd boilerplateFE
grep -r "auditLogs.detail\|auditLogs.timeline\|featureFlags.stats\|featureFlags.status\|apiKeys.reveal\|apiKeys.kpi\|settings.overridden\|settings.categoriesNav" src/i18n/locales/en/translation.json
```

Confirm every new key referenced by the new code is present in `en/translation.json`. Missing keys silently fall back to the key string — visual regression check should have caught any, but this grep is a final guard.

- [ ] **Step 6: Request code review**

Use `superpowers:requesting-code-review` against the diff:

```
Review the diff for fe/redesign-phase-2-views vs origin/main against:
- The spec at docs/superpowers/specs/2026-04-28-redesign-phase-2-design.md
- The CLAUDE.md Frontend Rules (theme tokens, no `dark:` for primary, shared component reuse)
- General code quality
Report any blockers, design deviations, or rule violations.
```

Address findings inline. Re-run lint + build before committing fixes.

- [ ] **Step 7: Final commit**

If review surfaces fixes, commit them. Otherwise, no commit needed.

```bash
# only if fixes were made
git commit -m "polish(fe/phase2): address code review findings"
```

- [ ] **Step 8: Open the PR**

```bash
git push -u origin fe/redesign-phase-2-views
gh pr create --title "feat(fe): Phase 2 redesign — Platform admin cluster polish" --body "$(cat <<'EOF'
## Summary

- Audit logs: timeline hero (events-in-window count + sparkline), new AuditLogDetailPage with JsonView + metadata card (incl. agent attribution), row click-through
- Feature flags: hero metric strip + status pill column
- API keys: KPI badge in header + redesigned secret reveal screen with close-confirm
- Settings: sticky sidebar nav + glass content groups + sticky save bar
- Backend addition: GetAuditLogByIdQuery + GET api/v1/AuditLogs/{id} (current controller was list-only)

Spec: docs/superpowers/specs/2026-04-28-redesign-phase-2-design.md
Plan: docs/superpowers/plans/2026-04-28-redesign-phase-2-platform-admin.md

## Test plan
- [ ] `npm run build` + `npm run lint` clean
- [ ] `dotnet build` clean
- [ ] Visual smoke on `/audit-logs`, `/audit-logs/{id}`, `/feature-flags`, `/api-keys`, `/settings`
- [ ] RTL pass (Arabic) on all five pages
- [ ] Permission matrix verified (super-admin / tenant admin / regular user)
- [ ] Identity cluster pages unchanged
EOF
)"
```

---

## Self-review notes

After writing the plan, I checked it against the spec:

- ✅ Spec §2.1 — `AuditTimelineHero` covered in Task 5; integration in Task 6.
- ✅ Spec §2.2 — `AuditLogDetailPage` + `JsonView` (renamed from `JsonDiff`) + `AuditMetadataCard` covered in Tasks 2, 3, 4. BE addition in Task 1.
- ✅ Spec §2.3 — `FeatureFlagStatStrip` + status pill covered in Task 7.
- ✅ Spec §2.4 — existing `ApiKeySecretDisplay` redesign + KPI badge covered in Task 8.
- ✅ Spec §2.5 — `SettingsCategoryNav` + sticky save bar covered in Task 9.
- ✅ Spec §10 — accessibility, RTL, permission gates covered in Task 10's verification checklist.
- ✅ Spec §11 — task ordering matches.
- ✅ Spec §12 — verification checklist exercised in Task 10.

No placeholders, no "TODO", no "implement later". Type names consistent across tasks (`JsonView` everywhere; `AuditTimelineHero`, `AuditMetadataCard`, `FeatureFlagStatStrip`, `ApiKeySecretDisplay`, `SettingsCategoryNav` consistent). Tasks are dependency-ordered: BE+hooks (Task 1) → presentational components (Tasks 2-3) → page assembly (Task 4) → list-page integration (Tasks 5-6) → independent feature pages (Tasks 7-9) → review (Task 10).
