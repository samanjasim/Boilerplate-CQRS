# Phase 3 — Data cluster redesign — implementation plan

> **For agentic workers:** Use `superpowers:executing-plans` to implement this plan task-by-task. Only use `superpowers:subagent-driven-development` if the user explicitly asks for parallel/subagent work. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring Files / Reports / Notifications onto the J4 Spectrum visual language with two patterns — hero-strip (Files, Reports) and grouped-list (Notifications) — without changing existing behaviour.

**Architecture:** One compact metric-card primitive for data/status hero cards, leaving the existing dashboard `StatCard` intact. One pure date-grouping utility. One small BE endpoint for Reports status counts. Three FE hero-strip / list-redesign tasks. Carry-along decomposition of the 852-LOC `FilesPage.tsx`. Review per page.

**Tech Stack:** React 19, TypeScript, Tailwind 4, shadcn/ui, TanStack Query, react-i18next, .NET 10 (Reports BE endpoint).

**Spec:** [`docs/superpowers/specs/2026-04-29-redesign-phase-3-data.md`](../specs/2026-04-29-redesign-phase-3-data.md)

---

## File structure

**New (FE):**
- `boilerplateFE/src/components/common/MetricCard.tsx` — compact metric tile extracted from `FeatureFlagStatStrip.tsx`, reused by data/admin hero strips. The existing dashboard `StatCard.tsx` remains unchanged.
- `boilerplateFE/src/features/files/components/StorageHeroStrip.tsx` — replaces `StorageSummaryPanel.tsx`.
- `boilerplateFE/src/features/files/components/FileUploadDialog.tsx` — extracted from `FilesPage.tsx`.
- `boilerplateFE/src/features/files/components/FileEditDialog.tsx` — extracted from `FilesPage.tsx` (rename + edit dialog inside detail modal).
- `boilerplateFE/src/features/files/components/FileRowActions.tsx` — per-row dropdown menu + its dialog state.
- `boilerplateFE/src/features/files/components/FilesGridView.tsx` — grid layout.
- `boilerplateFE/src/features/files/components/FilesTableView.tsx` — table layout.
- `boilerplateFE/src/features/files/utils/file-display.ts` — shared file display helpers (`FILE_CATEGORIES`, `isImageType`, file icon mapping) used by grid/table/detail.
- `boilerplateFE/src/features/reports/components/ReportStatusHeroStrip.tsx` — Active / Completed / Failed cards.
- `boilerplateFE/src/features/reports/api/reports.api.ts` — extend with `getStatusCounts()`.
- `boilerplateFE/src/features/notifications/utils/groupByDate.ts` — pure grouping utility (Today / Yesterday / This week / This month / Older).

**Modified (FE):**
- `boilerplateFE/src/features/files/pages/FilesPage.tsx` — composition only after extracts; target < 250 LOC.
- `boilerplateFE/src/features/reports/pages/ReportsPage.tsx` — insert `<ReportStatusHeroStrip />` above filter row.
- `boilerplateFE/src/features/notifications/pages/NotificationsPage.tsx` — segmented filter + grouped rendering + preferences link.
- `boilerplateFE/src/components/common/index.ts` — export `MetricCard`.
- `boilerplateFE/src/features/feature-flags/components/FeatureFlagStatStrip.tsx` — re-import `MetricCard` from common.
- `boilerplateFE/src/features/reports/api/reports.queries.ts` — add `useReportStatusCounts()`.
- `boilerplateFE/src/types/report.types.ts` — add `ReportStatusCounts` interface.
- `boilerplateFE/src/config/api.config.ts` — add `REPORTS.STATUS_COUNTS` route.
- `boilerplateFE/src/lib/query/keys.ts` — add `queryKeys.reports.statusCounts()`.
- `boilerplateFE/src/i18n/locales/{en,ar,ku}/translation.json` — new keys per task.

**New (BE):**
- `boilerplateBE/src/Starter.Application/Features/Reports/Queries/GetReportStatusCounts/GetReportStatusCountsQuery.cs`
- `boilerplateBE/src/Starter.Application/Features/Reports/Queries/GetReportStatusCounts/GetReportStatusCountsQueryHandler.cs`
- `boilerplateBE/src/Starter.Application/Features/Reports/DTOs/ReportStatusCountsDto.cs`

**Modified (BE):**
- `boilerplateBE/src/Starter.Api/Controllers/ReportsController.cs` — add `GET /status-counts` action.

**Deleted:**
- `boilerplateFE/src/features/files/components/StorageSummaryPanel.tsx` — replaced by hero strip.

---

## Tasks

The plan reads top-to-bottom and ships in three review checkpoints: **(A) Reports BE + hero**, **(B) Files**, **(C) Notifications**, then final integration. Tasks 0–1 are shared setup that must land before A/B/C. Per the spec, all three pages ship in one PR.

> **Path note:** all `npm` / source paths are relative to `boilerplateFE/`. All `dotnet` / source paths are relative to `boilerplateBE/`. Run lint/build commands from inside the relevant repo root.

---

### Task 0: Branch + shared `MetricCard` extraction

**Files:**
- Create: `boilerplateFE/src/components/common/MetricCard.tsx`
- Modify: `boilerplateFE/src/components/common/index.ts`
- Modify: `boilerplateFE/src/features/feature-flags/components/FeatureFlagStatStrip.tsx`

- [ ] **Step 1: Confirm branch state**

```bash
git status
git rev-parse --abbrev-ref HEAD
```

Expected: clean working tree, on `fe/redesign-phase-3-views`. If not on the branch, `git checkout fe/redesign-phase-3-views`.

- [ ] **Step 2: Create the shared `MetricCard` component**

Create `boilerplateFE/src/components/common/MetricCard.tsx` with:

```tsx
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import type { ReactNode } from 'react';

export interface MetricCardProps {
  label: string;
  /** Primary value rendered with `tabular-nums`. */
  value: ReactNode;
  /** Trailing fragment shown after the value (e.g., `/ 100`, `of 24 GB`). */
  secondary?: ReactNode;
  /** Subtle line under the label (e.g., `in flight`, `ready to download`). */
  eyebrow?: string;
  /** Apply `gradient-text` to the primary value. */
  emphasis?: boolean;
  /** Tailwind override hook for tinted cards (Active, Failed). */
  tone?: 'default' | 'active' | 'destructive';
  /** Optional inline glyph next to the value (e.g., spinner). */
  glyph?: ReactNode;
  className?: string;
}

const TONE_CLASSES: Record<NonNullable<MetricCardProps['tone']>, string> = {
  default: '',
  active: 'border-primary/20 bg-[var(--active-bg)]/40',
  destructive: 'border-destructive/30 bg-destructive/10',
};

export function MetricCard({
  label,
  value,
  secondary,
  eyebrow,
  emphasis,
  tone = 'default',
  glyph,
  className,
}: MetricCardProps) {
  return (
    <Card variant="elevated" className={cn(TONE_CLASSES[tone], className)}>
      <CardContent className="pt-5">
        <div className="text-xs uppercase tracking-wide text-muted-foreground">{label}</div>
        {eyebrow && (
          <div className="mt-0.5 text-[10px] uppercase tracking-[0.12em] text-muted-foreground/70">
            {eyebrow}
          </div>
        )}
        <div className="mt-2 flex items-baseline gap-2">
          <span className={cn('text-2xl font-semibold tabular-nums', emphasis && 'gradient-text')}>
            {value}
          </span>
          {glyph && <span className="text-muted-foreground">{glyph}</span>}
          {secondary && <span className="text-sm text-muted-foreground">{secondary}</span>}
        </div>
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 3: Re-export from the common barrel**

Read `boilerplateFE/src/components/common/index.ts` first to confirm format. Add a line:

```ts
export * from './MetricCard';
```

(If the barrel uses named exports instead of `export *`, follow the existing pattern.)

- [ ] **Step 4: Migrate `FeatureFlagStatStrip` to import the shared `MetricCard`**

Read `boilerplateFE/src/features/feature-flags/components/FeatureFlagStatStrip.tsx` (lines 20–39 currently define a local compact `StatCard`). Delete the local definition and replace its consumers with the imported shared component:

```tsx
// At the top:
import { MetricCard } from '@/components/common';

// Delete the local StatCard function (lines ~20–39 currently).

// Rename each JSX usage:
<MetricCard ... />
```

The local compact `StatCard` accepted `{ label, value, secondary, emphasis }` — exactly the shared `MetricCard` API plus optional fields. Only the component name changes. Do not touch `boilerplateFE/src/components/common/StatCard.tsx`; that file already exists with a different dashboard-card API and is used by Dashboard / Users / Tenants / Roles.

- [ ] **Step 5: Lint + build**

From `boilerplateFE/`:

```bash
npm run lint
npm run build
```

Expected: both pass.

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/components/common/MetricCard.tsx \
        boilerplateFE/src/components/common/index.ts \
        boilerplateFE/src/features/feature-flags/components/FeatureFlagStatStrip.tsx
git commit -m "refactor(fe): extract MetricCard to @/components/common

Phase 3 needs the same compact metric-card primitive on data/admin
heroes. Lifted the local one out of FeatureFlagStatStrip and added the
optional fields (eyebrow, tone, glyph) the new heroes will use.
FeatureFlags keeps its existing visuals. The existing dashboard StatCard
component is intentionally unchanged."
```

---

## Checkpoint A — Reports

### Task 1: Reports — BE status-counts endpoint

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Features/Reports/DTOs/ReportStatusCountsDto.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Reports/Queries/GetReportStatusCounts/GetReportStatusCountsQuery.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Reports/Queries/GetReportStatusCounts/GetReportStatusCountsQueryHandler.cs`
- Modify: `boilerplateBE/src/Starter.Api/Controllers/ReportsController.cs`

- [ ] **Step 1: Read the existing list handler for conventions**

```bash
ls boilerplateBE/src/Starter.Application/Features/Reports/Queries/
```

Find the `GetReportsQueryHandler` and read it. Note: namespace, the `IApplicationDbContext` injection, the `Result<T>` pattern, the `[Authorize(Policy = ...)]` policy used on the controller's list action.

- [ ] **Step 2: Create the DTO**

Create `boilerplateBE/src/Starter.Application/Features/Reports/DTOs/ReportStatusCountsDto.cs`:

```csharp
namespace Starter.Application.Features.Reports.DTOs;

public sealed record ReportStatusCountsDto(
    int Pending,
    int Processing,
    int Completed,
    int Failed
);
```

If the project namespace differs (check sibling DTO files in the same folder), match it.

- [ ] **Step 3: Create the query**

Create `boilerplateBE/src/Starter.Application/Features/Reports/Queries/GetReportStatusCounts/GetReportStatusCountsQuery.cs`:

```csharp
using MediatR;
using Starter.Application.Features.Reports.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Reports.Queries.GetReportStatusCounts;

public sealed record GetReportStatusCountsQuery() : IRequest<Result<ReportStatusCountsDto>>;
```

This matches the existing Reports list query's `Result<T>` namespace and DTO placement.

- [ ] **Step 4: Create the handler**

Create `boilerplateBE/src/Starter.Application/Features/Reports/Queries/GetReportStatusCounts/GetReportStatusCountsQueryHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Reports.DTOs;
using Starter.Domain.Common;
using Starter.Domain.Common.Enums;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Reports.Queries.GetReportStatusCounts;

internal sealed class GetReportStatusCountsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetReportStatusCountsQuery, Result<ReportStatusCountsDto>>
{
    public async Task<Result<ReportStatusCountsDto>> Handle(
        GetReportStatusCountsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is null)
        {
            return Result.Failure<ReportStatusCountsDto>(UserErrors.Unauthorized());
        }

        var query = context.Set<ReportRequest>().AsNoTracking().AsQueryable();

        // Match GetReportsQueryHandler's tenant/user scoping so the hero count
        // and the table describe the same data surface.
        if (currentUserService.TenantId is not null)
        {
            var userId = currentUserService.UserId.Value;
            var tenantId = currentUserService.TenantId.Value;

            query = query.Where(r =>
                r.RequestedBy == userId ||
                r.TenantId == tenantId);
        }

        var counts = await query
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountFor(ReportStatus status) =>
            counts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        var dto = new ReportStatusCountsDto(
            Pending: CountFor(ReportStatus.Pending),
            Processing: CountFor(ReportStatus.Processing),
            Completed: CountFor(ReportStatus.Completed),
            Failed: CountFor(ReportStatus.Failed)
        );

        return Result.Success(dto);
    }
}
```

This solution currently uses `Starter.Domain.Common.Enums.ReportStatus`, `context.Set<ReportRequest>()`, and `ICurrentUserService` in the existing list handler; keep this endpoint aligned with that pattern.

- [ ] **Step 5: Add controller action**

Read `boilerplateBE/src/Starter.Api/Controllers/ReportsController.cs` first to see the policy used on the list action. Add a new action mirroring the list action's authorization and `HandleResult` pattern:

```csharp
using Starter.Application.Features.Reports.Queries.GetReportStatusCounts;

// After the existing list action.

[HttpGet("status-counts")]
[Authorize(Policy = Permissions.System.ExportData)] // match the list action's policy
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public async Task<IActionResult> GetStatusCounts(CancellationToken ct)
{
    var result = await Mediator.Send(new GetReportStatusCountsQuery(), ct);
    return HandleResult(result);
}
```

If the list action uses a different policy constant, use that one.

- [ ] **Step 6: Build the BE**

```bash
cd boilerplateBE && dotnet build src/Starter.Api
```

Expected: build clean. Fix any using/namespace issues that surface.

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Features/Reports/DTOs/ReportStatusCountsDto.cs \
        boilerplateBE/src/Starter.Application/Features/Reports/Queries/GetReportStatusCounts/ \
        boilerplateBE/src/Starter.Api/Controllers/ReportsController.cs
git commit -m "feat(be/reports): add GET /reports/status-counts

Returns per-status totals (pending, processing, completed, failed)
scoped with the same current-user/tenant rules as the list query. Backs
the Phase 3 Reports status-hero strip — the existing list response only carries
pagination, no aggregates."
```

---

### Task 2: Reports — FE hook + hero strip

**Files:**
- Modify: `boilerplateFE/src/types/report.types.ts`
- Modify: `boilerplateFE/src/config/api.config.ts`
- Modify: `boilerplateFE/src/features/reports/api/reports.api.ts`
- Modify: `boilerplateFE/src/features/reports/api/reports.queries.ts`
- Modify: `boilerplateFE/src/lib/query/keys.ts`
- Create: `boilerplateFE/src/features/reports/components/ReportStatusHeroStrip.tsx`
- Modify: `boilerplateFE/src/features/reports/pages/ReportsPage.tsx`
- Modify: `boilerplateFE/src/i18n/locales/{en,ar,ku}/translation.json`

- [ ] **Step 1: Add the type**

Append to `boilerplateFE/src/types/report.types.ts`:

```ts
export interface ReportStatusCounts {
  pending: number;
  processing: number;
  completed: number;
  failed: number;
}
```

- [ ] **Step 2: Add the API endpoint constant**

Read `boilerplateFE/src/config/api.config.ts` and locate the `REPORTS:` block (lines around the `LIST: '/Reports'` declaration). Add a new key:

```ts
REPORTS: {
  LIST: '/Reports',
  STATUS_COUNTS: '/Reports/status-counts',  // ← new
  REQUEST: '/Reports',
  DOWNLOAD: (id: string) => `/Reports/${id}/download`,
  DELETE: (id: string) => `/Reports/${id}`,
},
```

- [ ] **Step 3: Add the API call**

Read `boilerplateFE/src/features/reports/api/reports.api.ts` to see the existing pattern. Append a method:

```ts
// Inside the existing `reportsApi` object/export:
getStatusCounts: async (): Promise<ReportStatusCounts> => {
  const response = await apiClient.get<ApiResponse<ReportStatusCounts>>(
    API_ENDPOINTS.REPORTS.STATUS_COUNTS
  );
  return response.data.data;
},
```

Add the imports: `import type { ApiResponse } from '@/types';` and `import type { ReportStatusCounts } from '@/types/report.types';` if not already present. Keep this method unwrapped, matching the way page hooks consume `reportsApi.getReports()`.

- [ ] **Step 4: Add the query key + React Query hook**

Read `boilerplateFE/src/lib/query/keys.ts` and extend the Reports key factory:

```ts
statusCounts: () => [...queryKeys.reports.all, 'status-counts'] as const,
```

Then read `boilerplateFE/src/features/reports/api/reports.queries.ts` to see the existing `useReports` hook. Add:

```ts
export function useReportStatusCounts() {
  return useQuery({
    queryKey: queryKeys.reports.statusCounts(),
    queryFn: reportsApi.getStatusCounts,
    staleTime: 30_000,     // counts don't change often
    refetchInterval: (query) => {
      const data = query.state.data;
      return data && (data.pending > 0 || data.processing > 0) ? 5000 : false;
    },
  });
}
```

The polling keeps the "Active" card fresh while a report is pending/processing and goes quiet when there is no active work.

- [ ] **Step 5: Add translation keys to all three locales**

Read each translation file and add a `reports.hero` block. Find an existing `"reports": { ... }` section and insert (matching JSON style — existing trailing-comma pattern):

EN (`boilerplateFE/src/i18n/locales/en/translation.json`):
```json
"hero": {
  "active": "Active",
  "activeEyebrow": "in flight",
  "completed": "Completed",
  "completedEyebrow": "ready to download",
  "failed": "Failed",
  "failedEyebrow": "failed"
}
```

AR (`boilerplateFE/src/i18n/locales/ar/translation.json`):
```json
"hero": {
  "active": "نشط",
  "activeEyebrow": "قيد المعالجة",
  "completed": "مكتمل",
  "completedEyebrow": "جاهز للتنزيل",
  "failed": "فشل",
  "failedEyebrow": "فشل"
}
```

KU (`boilerplateFE/src/i18n/locales/ku/translation.json`):
```json
"hero": {
  "active": "چالاک",
  "activeEyebrow": "لە جێبەجێکردندا",
  "completed": "تەواوبوو",
  "completedEyebrow": "ئامادەیە بۆ داگرتن",
  "failed": "شکستی هێنا",
  "failedEyebrow": "شکستی هێنا"
}
```

Validate JSON after each edit:

```bash
node -e "JSON.parse(require('fs').readFileSync('boilerplateFE/src/i18n/locales/en/translation.json','utf8'))"
node -e "JSON.parse(require('fs').readFileSync('boilerplateFE/src/i18n/locales/ar/translation.json','utf8'))"
node -e "JSON.parse(require('fs').readFileSync('boilerplateFE/src/i18n/locales/ku/translation.json','utf8'))"
```

Expected: no output = valid JSON.

- [ ] **Step 6: Create the hero component**

Create `boilerplateFE/src/features/reports/components/ReportStatusHeroStrip.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/common';
import { Spinner } from '@/components/ui/spinner';
import { useReportStatusCounts } from '../api/reports.queries';

export function ReportStatusHeroStrip() {
  const { t } = useTranslation();
  const { data, isLoading } = useReportStatusCounts();

  if (isLoading || !data) {
    // Render layout skeleton so the page doesn't reflow when counts arrive.
    return (
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 mb-6">
        <MetricCard label={t('reports.hero.active')} eyebrow={t('reports.hero.activeEyebrow')} value="—" tone="active" />
        <MetricCard label={t('reports.hero.completed')} eyebrow={t('reports.hero.completedEyebrow')} value="—" />
      </div>
    );
  }

  const active = data.pending + data.processing;
  const showFailed = data.failed > 0;
  const isProcessing = data.processing > 0;

  return (
    <div className={`grid gap-4 sm:grid-cols-2 ${showFailed ? 'lg:grid-cols-3' : ''} mb-6`}>
      <MetricCard
        label={t('reports.hero.active')}
        eyebrow={t('reports.hero.activeEyebrow')}
        value={active}
        emphasis={active > 0}
        tone="active"
        glyph={isProcessing ? <Spinner size="sm" className="h-4 w-4" /> : undefined}
      />
      <MetricCard
        label={t('reports.hero.completed')}
        eyebrow={t('reports.hero.completedEyebrow')}
        value={data.completed}
      />
      {showFailed && (
        <MetricCard
          label={t('reports.hero.failed')}
          eyebrow={t('reports.hero.failedEyebrow')}
          value={data.failed}
          tone="destructive"
        />
      )}
    </div>
  );
}
```

- [ ] **Step 7: Insert hero into ReportsPage**

Read `boilerplateFE/src/features/reports/pages/ReportsPage.tsx`. Insert the hero immediately after `<PageHeader>` and before the filter row (the `<Card>` containing the filter `<Select>`s):

```tsx
// Top of file imports — add:
import { ReportStatusHeroStrip } from '../components/ReportStatusHeroStrip';

// In JSX, immediately after <PageHeader ... />:
<ReportStatusHeroStrip />
```

No other changes to the page.

- [ ] **Step 8: Lint + build**

```bash
cd boilerplateFE && npm run lint && npm run build
```

Expected: both pass.

- [ ] **Step 9: Sync to test app + manual visual check**

```bash
rsync -a boilerplateFE/src/ _testJ4visual/_testJ4visual-FE/src/
```

Note: the FE harness sync above does not add the BE endpoint to `_testJ4visual`. To see real counts there, copy/regenerate the matching BE files with the `_testJ4visual` namespaces and restart the test backend. Until then, the hero should handle the failed request without breaking the page; verify the skeleton/no-data state, then verify live counts after the backend is synced.

Open `http://localhost:3100/reports` in the browser. Verify:
- Hero strip renders above the filter row.
- Three cards (or two if no failures): Active, Completed, optional Failed.
- "Active" card shows tinted background; "Failed" card (when present) shows red tint.
- Switch to AR: hero mirrors correctly, eyebrow text reads right-to-left, the gradient-text number stays Latin digits.

- [ ] **Step 10: Commit**

```bash
git add boilerplateFE/src/types/report.types.ts \
        boilerplateFE/src/config/api.config.ts \
        boilerplateFE/src/lib/query/keys.ts \
        boilerplateFE/src/features/reports/ \
        boilerplateFE/src/i18n/locales/
git commit -m "feat(fe/phase3): Reports status hero strip

Adds Active / Completed / Failed cards above the filter row, sourced
from the new GET /reports/status-counts endpoint. Failed slot collapses
when zero. EN + AR + KU keys land inline. Existing filter row, table,
and action buttons unchanged."
```

**REVIEW CHECKPOINT A.** Pause for human review of the Reports redesign before continuing to Files.

---

## Checkpoint B — Files

### Task 3: Files — Decompose page (no behavioural change)

This task is purely structural — preserve existing behaviour 1:1, no visual changes. Done first so Task 4 (the hero) can target a smaller, cleaner file.

**Files:**
- Create: focused components under `boilerplateFE/src/features/files/components/`
- Create: `boilerplateFE/src/features/files/utils/file-display.ts`
- Modify: `boilerplateFE/src/features/files/pages/FilesPage.tsx`

- [ ] **Step 1: Re-read the current page to understand boundaries**

```bash
wc -l boilerplateFE/src/features/files/pages/FilesPage.tsx
```

Expected: 852 LOC. Read it end-to-end. Per the explorer survey:
- Upload dialog: lines 574–647 (state: `uploadFile`, `uploadCategory`, `uploadDescription`, `uploadTags`, `uploadIsPublic`, `uploadDialogOpen`).
- Edit dialog: lines 674–707 (state: `isEditing`, `editDescription`, `editCategory`, `editTags`).
- Row dropdown actions: lines 514–554 (state: `shareFile`, `transferFile`, `deleteFile`).
- Grid view: lines 404–460.
- Table view: lines 462–562.
- Shared display helpers: `CATEGORIES`, `getFileIcon`, `isImageType`, and any category label mapping currently living in the page.

- [ ] **Step 2: Extract shared file display helpers**

Create `boilerplateFE/src/features/files/utils/file-display.ts` and move the existing file display constants/helpers into it without changing behavior:

```ts
// Move these from FilesPage.tsx as-is, adjusting imports/types only:
export const FILE_CATEGORIES = [...];
export function isImageType(contentType?: string | null): boolean { ... }
export function getFileIcon(file: FileMetadata): LucideIcon { ... }
```

Use this utility from `FilesGridView`, `FilesTableView`, and the detail modal. This keeps grid/table/detail category labels, icons, and image checks unified instead of duplicating display decisions in each extracted component.

- [ ] **Step 3: Extract `FileUploadDialog`**

Create `boilerplateFE/src/features/files/components/FileUploadDialog.tsx`. Lift the upload-dialog JSX, the upload state, and the upload mutation call into the new component. Prop interface:

```tsx
export interface FileUploadDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Optional default category (e.g., when uploading to a specific category from a sub-page). */
  defaultCategory?: FileCategory;
}
```

The dialog should manage its own form state internally (the page doesn't need to read it). On successful upload, close itself and rely on TanStack Query invalidation for the list refresh — that's already wired in `useUploadFile()`.

In `FilesPage.tsx`, replace the inline dialog block with `<FileUploadDialog open={uploadDialogOpen} onOpenChange={setUploadDialogOpen} />` and remove the upload form state declarations.

- [ ] **Step 4: Extract `FileEditDialog`**

Create `boilerplateFE/src/features/files/components/FileEditDialog.tsx`. Lift the edit form JSX (rename / description / tags / category) and its form state.

```tsx
export interface FileEditDialogProps {
  file: FileMetadata | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved?: (file: FileMetadata) => void;
}
```

The dialog calls `useUpdateFile()` internally, closes on success, and calls `onSaved` so the page can update/close the detail modal consistently. Keep the edit dialog controlled by `open` instead of hiding state inside the detail modal.

- [ ] **Step 5: Extract `FileRowActions`**

Create `boilerplateFE/src/features/files/components/FileRowActions.tsx`. Lift the dropdown menu + the trio of action dialogs that hang off it (delete confirm, share dialog, ownership transfer dialog). Prop interface:

```tsx
export interface FileRowActionsProps {
  file: FileMetadata;
  onDeleted?: (fileId: string) => void;
  onDownload?: (file: FileMetadata) => void | Promise<void>;
  onCopyUrl?: (file: FileMetadata) => void | Promise<void>;
  onEdit?: (file: FileMetadata) => void;
  trigger?: 'icon' | 'inline';
}
```

Permissions logic (`canDelete`, `isOwner`, `fileCanShare`) moves inside the component and uses `usePermissions()` + `useAuthStore` directly. Each dialog state is component-local (`shareOpen`, `transferOpen`, `deleteConfirm`).

In `FilesPage.tsx`, the `<TableCell>` for actions becomes `<FileRowActions file={f} onDeleted={handleDeletedFile} onEdit={openEditDialog} ... />`. The page-level `shareFile`, `transferFile`, `deleteFile` state declarations + their dialogs are removed. Grid cards also use this component in their top/right action area, with click propagation stopped, so grid and table expose the same action set.

- [ ] **Step 6: Extract `FilesGridView` and `FilesTableView`**

Create `boilerplateFE/src/features/files/components/FilesGridView.tsx` and `FilesTableView.tsx`. Each accepts:

```tsx
export interface FilesViewProps {
  files: FileMetadata[];
  isLoading: boolean;
  onSelect: (file: FileMetadata) => void; // for opening detail modal
  onDeleted?: (fileId: string) => void;
  onEdit?: (file: FileMetadata) => void;
}
```

Move the inline JSX. Each view imports the shared helpers from `../utils/file-display` and uses `<FileRowActions>` for the per-row/per-card action menu.

- [ ] **Step 7: Verify FilesPage shrinks**

```bash
wc -l boilerplateFE/src/features/files/pages/FilesPage.tsx
```

Expected: under 250 LOC. The page should now be: imports + state for filters/pagination + query call + `<PageHeader>` + filter chips + view toggle + `{viewMode === 'grid' ? <FilesGridView ... /> : <FilesTableView ... />}` + `<Pagination>` + `<FileUploadDialog>` + `<FileEditDialog file={editingFile} open={editOpen} ... />` + the file detail modal (still page-level since it shows extra info beyond editing).

- [ ] **Step 8: Lint + build**

```bash
npm run lint && npm run build
```

Expected: both pass with no behaviour changes.

- [ ] **Step 9: Sync + smoke test**

```bash
rsync -a boilerplateFE/src/ _testJ4visual/_testJ4visual-FE/src/
```

Open `http://localhost:3100/files`. Walk through:
1. Upload a file → opens dialog → submits → file appears in list.
2. Click a file → detail modal opens → enter edit mode → save → values update.
3. Open a row's action menu → trigger delete → confirm → file disappears.
4. Switch grid → list and back.

If any flow breaks, the extraction missed a piece of state. Fix and re-test.

- [ ] **Step 10: Commit**

```bash
git add boilerplateFE/src/features/files/
git commit -m "refactor(fe/files): extract FilesPage components

Carry-along refactor before the storage-hero work. Pulls upload dialog,
edit dialog, row actions, grid view, and table view out of the 852-LOC
page into focused components under features/files/components/. No
behaviour changes — same flows, same state semantics, same failure
modes. Page now reads as composition + filter/pagination state."
```

---

### Task 4: Files — Storage hero strip

**Files:**
- Create: `boilerplateFE/src/features/files/components/StorageHeroStrip.tsx`
- Delete: `boilerplateFE/src/features/files/components/StorageSummaryPanel.tsx`
- Modify: `boilerplateFE/src/features/files/pages/FilesPage.tsx`
- Modify: `boilerplateFE/src/i18n/locales/{en,ar,ku}/translation.json`

- [ ] **Step 1: Add translation keys**

EN (`boilerplateFE/src/i18n/locales/en/translation.json`) — find the `files` block, add a new sub-tree:

```json
"storageHero": {
  "total": "Total storage",
  "ofQuota": "of {{quota}}",
  "byCategory": "By category",
  "allTenants": "All tenants",
  "other": "Other"
}
```

AR (matching position):
```json
"storageHero": {
  "total": "المساحة الإجمالية",
  "ofQuota": "من {{quota}}",
  "byCategory": "حسب الفئة",
  "allTenants": "جميع المستأجرين",
  "other": "أخرى"
}
```

KU:
```json
"storageHero": {
  "total": "کۆی بیرگە",
  "ofQuota": "لە {{quota}}",
  "byCategory": "بە پێی پۆل",
  "allTenants": "هەموو خاوەنکارەکان",
  "other": "ئەوانی تر"
}
```

Validate JSON for each locale (same `node -e` commands as Task 2 step 5).

The old `files.storageSummary.*` keys can stay in place during this commit — Task 4 step 5 deletes the panel and step 6 cleans up the dead keys.

- [ ] **Step 2: Create the hero component**

Create `boilerplateFE/src/features/files/components/StorageHeroStrip.tsx`:

```tsx
import { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/common';
import { Card, CardContent } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import { useAuthStore } from '@/stores/auth.store';
import { useFeatureFlag } from '@/hooks';
import { useStorageSummary } from '../api/files.queries';
import { formatFileSize } from '@/utils';
import { cn } from '@/lib/utils';

const MAX_BARS = 4;

export function StorageHeroStrip() {
  const { t } = useTranslation();
  const user = useAuthStore(s => s.user);
  const isPlatformAdmin = !user?.tenantId;
  const [allTenants, setAllTenants] = useState(false);
  const inCrossTenantView = isPlatformAdmin && allTenants;

  const { data, isLoading } = useStorageSummary(allTenants && isPlatformAdmin);

  // Quota: only meaningful for tenant-scoped views.
  const quotaFlag = useFeatureFlag('files.max_storage_mb');
  const quotaMb = quotaFlag.value ? parseInt(quotaFlag.value, 10) : null;
  const quotaBytes = quotaMb && !Number.isNaN(quotaMb) ? quotaMb * 1024 * 1024 : null;
  const showQuota = !inCrossTenantView && quotaBytes != null && quotaBytes > 0;

  // Compress to MAX_BARS — fold the tail into "Other".
  const bars = useMemo(() => {
    if (!data) return [];
    const sorted = [...data.byCategory].sort((a, b) => b.bytes - a.bytes);
    if (sorted.length <= MAX_BARS) return sorted;
    const head = sorted.slice(0, MAX_BARS - 1);
    const tail = sorted.slice(MAX_BARS - 1);
    const other = tail.reduce(
      (acc, c) => ({
        category: t('files.storageHero.other'),
        bytes: acc.bytes + c.bytes,
        fileCount: acc.fileCount + c.fileCount,
      }),
      { category: t('files.storageHero.other'), bytes: 0, fileCount: 0 }
    );
    return [...head, other];
  }, [data, t]);

  const maxBarBytes = bars.reduce((m, c) => Math.max(m, c.bytes), 1);
  const usagePct =
    showQuota && data && quotaBytes
      ? Math.min(100, (data.totalBytes / quotaBytes) * 100)
      : null;

  return (
    <div className="mb-6 grid gap-4 lg:grid-cols-[minmax(220px,0.75fr)_minmax(0,1.5fr)]">
      <div className="space-y-3">
        <MetricCard
          label={t('files.storageHero.total')}
          value={isLoading || !data ? '—' : formatFileSize(data.totalBytes)}
          secondary={
            showQuota && quotaBytes
              ? t('files.storageHero.ofQuota', { quota: formatFileSize(quotaBytes) })
              : undefined
          }
          emphasis={!isLoading && !!data}
        />
        {usagePct != null && (
          <div className="h-1.5 overflow-hidden rounded-full bg-muted">
            <div
              className={cn(
                'h-full rounded-full transition-all',
                usagePct > 90 ? 'bg-destructive' : 'bg-primary'
              )}
              style={{ width: `${usagePct}%` }}
            />
          </div>
        )}
      </div>

      <Card variant="glass">
        <CardContent className="py-5">
          <div className="grid gap-6 md:grid-cols-[minmax(0,1fr)_auto] items-start">
            <div className="space-y-2">
              <div className="text-xs uppercase tracking-wide text-muted-foreground">
                {t('files.storageHero.byCategory')}
              </div>
              {isLoading || !data ? (
                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                  <Spinner size="sm" /> …
                </div>
              ) : (
                <ul className="space-y-1.5">
                  {bars.map(c => (
                    <li key={c.category} className="space-y-1">
                      <div className="flex justify-between text-xs">
                        <span className="text-muted-foreground">{c.category}</span>
                        <span className="text-muted-foreground tabular-nums">
                          {formatFileSize(c.bytes)} · {c.fileCount}
                        </span>
                      </div>
                      <div className="h-1.5 rounded-full bg-muted overflow-hidden">
                        <div
                          className="h-full rounded-full bg-primary transition-all"
                          style={{ width: `${(c.bytes / maxBarBytes) * 100}%` }}
                        />
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </div>

            {isPlatformAdmin && (
              <div className="md:justify-self-end">
                <label className="inline-flex items-center gap-2 text-xs cursor-pointer">
                  <input
                    type="checkbox"
                    checked={allTenants}
                    onChange={e => setAllTenants(e.target.checked)}
                    className="h-3.5 w-3.5"
                  />
                  {t('files.storageHero.allTenants')}
                </label>
              </div>
            )}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
```

- [ ] **Step 3: Wire it into `FilesPage`**

Modify `boilerplateFE/src/features/files/pages/FilesPage.tsx`:

```tsx
// Replace the existing import:
//   import { StorageSummaryPanel } from '../components/StorageSummaryPanel';
// with:
import { StorageHeroStrip } from '../components/StorageHeroStrip';

// In the JSX, immediately after <PageHeader ...>, BEFORE the filter chips:
<StorageHeroStrip />

// Remove the existing <StorageSummaryPanel /> button from wherever it sits in the page header / toolbar (~line 306).
```

- [ ] **Step 4: Lint + build**

```bash
npm run lint && npm run build
```

Expected: both pass. An unreferenced `StorageSummaryPanel.tsx` file does not fail the build; delete it in the next step before final QA to remove dead code.

- [ ] **Step 5: Delete the old panel + dead translation keys**

```bash
rm boilerplateFE/src/features/files/components/StorageSummaryPanel.tsx
```

Search for any other consumer:
```bash
rg "StorageSummaryPanel" boilerplateFE/src/
```
Expected: no results. If a consumer turns up, replace it with `<StorageHeroStrip />` (or remove if it was page-internal).

Remove the obsolete `files.storageSummary.*` block from each translation file (the keys `trigger`, `title`, `total`, `byCategory`, `topUploaders`, `allTenants`). Validate JSON after each edit.

- [ ] **Step 6: Lint + build (final)**

```bash
npm run lint && npm run build
```

Expected: both pass.

- [ ] **Step 7: Sync + visual verification**

```bash
rsync -a boilerplateFE/src/ _testJ4visual/_testJ4visual-FE/src/
rm -f _testJ4visual/_testJ4visual-FE/src/features/files/components/StorageSummaryPanel.tsx
```

Open `http://localhost:3100/files` as super-admin. Verify:
- Hero strip renders above the filter row.
- "Total storage" figure is gradient-text.
- Category bars render proportionally with category names + sizes + counts.
- "All tenants" checkbox is visible (super-admin); toggling it refetches and updates the figure.
- "Storage Summary" button is gone from the page header.
- Switch to AR — hero mirrors correctly, "of 100 GB" reads "من 100 جيجابايت" (the unit string comes from `formatFileSize`).
- Login as `acme.admin` — checkbox is hidden, no quota progress bar unless the `files.max_storage_mb` flag is set.

- [ ] **Step 8: Commit**

```bash
git add boilerplateFE/src/features/files/components/StorageHeroStrip.tsx \
        boilerplateFE/src/features/files/components/ \
        boilerplateFE/src/features/files/pages/FilesPage.tsx \
        boilerplateFE/src/i18n/locales/
git commit -m "feat(fe/files): persistent storage hero strip

Replaces the StorageSummaryPanel dialog (deleted) with a permanently
visible hero strip: total + quota + per-category bars + super-admin
cross-tenant toggle. Quota comes from the files.max_storage_mb feature
flag; quota-less tenants render only the total. EN + AR + KU keys land
inline; the obsolete files.storageSummary.* keys are removed."
```

**REVIEW CHECKPOINT B.** Pause for human review of the Files redesign before continuing to Notifications.

---

## Checkpoint C — Notifications

### Task 5: Notifications — Date-grouping utility

The grouping logic is pure and date-bound. This repo does not currently include a FE unit-test runner (`package.json` has no Vitest/Jest script), so do not add a new test dependency in this phase. Keep the function small, deterministic, and verify it through build/lint plus the fixture matrix below during code review/manual QA.

**Files:**
- Create: `boilerplateFE/src/features/notifications/utils/groupByDate.ts`

- [ ] **Step 1: Implement the utility**

Create `boilerplateFE/src/features/notifications/utils/groupByDate.ts`:

```ts
import type { Notification } from '@/types';

export type GroupKey =
  | 'today'
  | 'yesterday'
  | 'earlierThisWeek'
  | 'earlierThisMonth'
  | 'older';

const ORDER: GroupKey[] = [
  'today',
  'yesterday',
  'earlierThisWeek',
  'earlierThisMonth',
  'older',
];

export interface NotificationGroup {
  key: GroupKey;
  items: Notification[];
}

function startOfDay(d: Date): Date {
  const c = new Date(d);
  c.setHours(0, 0, 0, 0);
  return c;
}

function classify(createdAt: string, now: Date): GroupKey {
  const created = new Date(createdAt);
  const today = startOfDay(now);
  const yesterday = startOfDay(new Date(today.getTime() - 86400000));

  if (created >= today) return 'today';
  if (created >= yesterday) return 'yesterday';

  // ISO week start: Monday. Adjust if the project uses Sunday-start; matches
  // common European convention. Notifications page is mixed-locale anyway.
  const dayOfWeek = (today.getDay() + 6) % 7; // Mon=0..Sun=6
  const weekStart = startOfDay(new Date(today.getTime() - dayOfWeek * 86400000));
  if (created >= weekStart) return 'earlierThisWeek';

  const monthStart = new Date(today.getFullYear(), today.getMonth(), 1);
  if (created >= monthStart) return 'earlierThisMonth';

  return 'older';
}

export function groupNotificationsByDate(
  notifications: Notification[],
  now: Date = new Date()
): NotificationGroup[] {
  const buckets = new Map<GroupKey, Notification[]>();
  for (const n of notifications) {
    const key = classify(n.createdAt, now);
    if (!buckets.has(key)) buckets.set(key, []);
    buckets.get(key)!.push(n);
  }

  return ORDER
    .filter(k => buckets.has(k))
    .map(k => ({ key: k, items: buckets.get(k)! }));
}
```

- [ ] **Step 2: Review deterministic boundary cases**

Use `now = new Date('2026-04-29T12:00:00Z')` (Wednesday) and inspect the logic against this matrix:

- `2026-04-29T00:30:00Z` → `today`
- `2026-04-28T23:30:00Z` → `yesterday`
- `2026-04-27T12:00:00Z` → `earlierThisWeek`
- `2026-04-19T12:00:00Z` → `earlierThisMonth`
- `2026-01-01T12:00:00Z` → `older`

Confirm empty groups are skipped and input order is preserved within each returned group. Keep grouping page-local; pagination semantics do not change.

- [ ] **Step 3: Lint + build**

```bash
npm run lint && npm run build
```

Expected: both pass. Do not run `npx vitest` unless a test runner is intentionally added in a separate tooling task.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/src/features/notifications/utils/
git commit -m "feat(fe/notifications): groupNotificationsByDate utility

Pure function used by the Phase 3 Notifications redesign to bucket
rows into Today / Yesterday / Earlier-this-week / Earlier-this-month /
Older. Per-page client-side grouping; pagination is unchanged."
```

---

### Task 6: Notifications — Page redesign

**Files:**
- Modify: `boilerplateFE/src/features/notifications/pages/NotificationsPage.tsx`
- Modify: `boilerplateFE/src/i18n/locales/{en,ar,ku}/translation.json`

- [ ] **Step 1: Add translation keys**

EN — find the `notifications` block. Add:

```json
"groups": {
  "today": "Today",
  "yesterday": "Yesterday",
  "earlierThisWeek": "Earlier this week",
  "earlierThisMonth": "Earlier this month",
  "older": "Older"
},
"filter": {
  "all": "All ({{count}})",
  "unread": "Unread ({{count}})"
},
"preferencesLink": "Notification preferences"
```

AR:
```json
"groups": {
  "today": "اليوم",
  "yesterday": "أمس",
  "earlierThisWeek": "في وقت سابق من هذا الأسبوع",
  "earlierThisMonth": "في وقت سابق من هذا الشهر",
  "older": "أقدم"
},
"filter": {
  "all": "الكل ({{count}})",
  "unread": "غير مقروء ({{count}})"
},
"preferencesLink": "تفضيلات الإشعارات"
```

KU:
```json
"groups": {
  "today": "ئەمڕۆ",
  "yesterday": "دوێنێ",
  "earlierThisWeek": "پێشتر لەم هەفتەیەدا",
  "earlierThisMonth": "پێشتر لەم مانگەدا",
  "older": "کۆنتر"
},
"filter": {
  "all": "هەموو ({{count}})",
  "unread": "نەخوێندراوەکان ({{count}})"
},
"preferencesLink": "ڕێکخستنی ئاگادارییەکان"
```

Validate JSON after each.

- [ ] **Step 2: Rewrite the page**

Overwrite `boilerplateFE/src/features/notifications/pages/NotificationsPage.tsx`:

```tsx
import { useState, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useTimeAgoFormatter } from '@/hooks';
import { Bell, CheckCheck, ArrowRight } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { PageHeader, Pagination, getPersistedPageSize } from '@/components/common';
import { useNotifications, useUnreadCount, useMarkRead, useMarkAllRead } from '@/features/notifications/api';
import { NOTIFICATION_ICONS } from '@/constants';
import { ROUTES } from '@/config';
import { cn } from '@/lib/utils';
import { groupNotificationsByDate } from '../utils/groupByDate';
import type { Notification } from '@/types';

type FilterType = 'all' | 'unread';

export default function NotificationsPage() {
  const { t } = useTranslation();
  const [filter, setFilter] = useState<FilterType>('all');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const formatTimeAgo = useTimeAgoFormatter();

  const isReadParam = filter === 'unread' ? false : undefined;

  const { data, isLoading, isFetching } = useNotifications({
    pageNumber: page,
    pageSize,
    isRead: isReadParam,
  });
  const { data: allNotificationsMeta } = useNotifications(
    { pageNumber: 1, pageSize: 1 },
    { refetchInterval: false }
  );
  const { data: totalUnread = 0 } = useUnreadCount();
  const { mutate: markRead } = useMarkRead();
  const { mutate: markAllRead } = useMarkAllRead();

  const notifications = data?.data ?? [];
  const pagination = data?.pagination;
  const totalAll =
    filter === 'all'
      ? pagination?.totalCount ?? 0
      : allNotificationsMeta?.pagination?.totalCount ?? 0;

  // Group rows on the current page only — pagination unchanged.
  const groups = useMemo(() => groupNotificationsByDate(notifications), [notifications]);

  const handleNotificationClick = (n: Notification) => {
    if (!n.isRead) markRead(n.id);
  };

  const SegmentButton = ({ value, label }: { value: FilterType; label: string }) => (
    <button
      type="button"
      onClick={() => { setFilter(value); setPage(1); }}
      className={cn(
        'h-8 px-3 rounded-[10px] text-sm motion-safe:transition-colors motion-safe:duration-150',
        filter === value ? 'pill-active' : 'state-hover'
      )}
      aria-pressed={filter === value}
    >
      {label}
    </button>
  );

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('notifications.title')}
        actions={
          <Button asChild variant="ghost" size="sm">
            <Link to={ROUTES.PROFILE} className="gap-1">
              {t('notifications.preferencesLink')}
              <ArrowRight className="h-3.5 w-3.5 ltr:ml-1 rtl:mr-1 rtl:rotate-180" />
            </Link>
          </Button>
        }
      />

      <div className="flex items-center justify-between flex-wrap gap-3">
        <div className="inline-flex items-center gap-1 rounded-[12px] border border-border/40 bg-foreground/5 p-1">
          <SegmentButton value="all" label={t('notifications.filter.all', { count: totalAll })} />
          <SegmentButton value="unread" label={t('notifications.filter.unread', { count: totalUnread })} />
        </div>
        <Button variant="outline" size="sm" onClick={() => markAllRead()} disabled={totalUnread === 0}>
          <CheckCheck className="h-4 w-4 ltr:mr-1 rtl:ml-1" />
          {t('notifications.markAllRead')}
        </Button>
      </div>

      <Card>
        <CardContent className="py-4">
          {isLoading && !data ? (
            <div className="flex items-center justify-center py-8 text-muted-foreground">
              {t('common.loading')}
            </div>
          ) : notifications.length === 0 && !isFetching ? (
            <div className="flex flex-col items-center justify-center py-12 text-muted-foreground">
              <Bell className="h-10 w-10 mb-3 opacity-40" />
              <p>{t('notifications.noNotifications')}</p>
            </div>
          ) : (
            <div className="divide-y">
              {groups.map(group => (
                <section key={group.key} className="pt-3 first:pt-0">
                  <div className="px-2 pb-1 text-[10px] font-semibold uppercase tracking-[0.12em] text-muted-foreground">
                    <span className="inline-block w-1 h-1 rounded-full bg-primary/70 me-1.5 align-middle -translate-y-px" />
                    {t(`notifications.groups.${group.key}`)}
                  </div>
                  <div className="divide-y">
                    {group.items.map(n => {
                      const Icon = NOTIFICATION_ICONS[n.type] ?? Bell;
                      const timeAgo = formatTimeAgo(n.createdAt);
                      return (
                        <div
                          key={n.id}
                          className={cn(
                            'flex items-start gap-4 py-4 px-2 cursor-pointer rounded-lg transition-colors hover:bg-muted/50',
                            !n.isRead && 'bg-primary/5'
                          )}
                          onClick={() => handleNotificationClick(n)}
                        >
                          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-muted">
                            <Icon className="h-5 w-5 text-muted-foreground" />
                          </div>
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2">
                              <p className={cn('text-sm', !n.isRead && 'font-semibold')}>{n.title}</p>
                              {!n.isRead && (
                                <Badge variant="default" className="text-[10px] px-1.5 py-0">
                                  {t('notifications.new')}
                                </Badge>
                              )}
                            </div>
                            <p className="text-sm text-muted-foreground mt-0.5">{n.message}</p>
                            <p className="text-xs text-muted-foreground mt-1">{timeAgo}</p>
                          </div>
                          {!n.isRead && <span className="mt-2 h-2.5 w-2.5 shrink-0 rounded-full bg-primary" />}
                        </div>
                      );
                    })}
                  </div>
                </section>
              ))}
            </div>
          )}

          {pagination && (
            <Pagination
              pagination={pagination}
              onPageChange={setPage}
              onPageSizeChange={(size) => { setPageSize(size); setPage(1); }}
            />
          )}
        </CardContent>
      </Card>
    </div>
  );
}
```

- [ ] **Step 3: Lint + build**

```bash
npm run lint && npm run build
```

Expected: both pass. `useUnreadCount()` already returns a number because `notificationsApi.getUnreadCount()` unwraps `ApiResponse<number>`; do not treat it like `{ data: { count } }`.

- [ ] **Step 4: Sync + visual check**

```bash
rsync -a boilerplateFE/src/ _testJ4visual/_testJ4visual-FE/src/
```

Open `http://localhost:3100/notifications`. Verify:
- Segmented filter shows `All (n) · Unread (n)` with active segment using `pill-active`.
- Date-group section headers render between rows.
- "Notification preferences" button appears in the header and links to `/profile`.
- "Mark all as read" disabled when unread count is 0.
- Switch to AR — group headers use `me-1.5` spacing (logical), arrow icon mirrors via `rtl:rotate-180`.

If the page has no rows, manually trigger a notification (e.g., upload a file or run any action that the seed data wires to a notification) to populate the list.

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/features/notifications/ \
        boilerplateFE/src/i18n/locales/
git commit -m "feat(fe/notifications): grouped list + segmented filter + prefs link

Replaces the flat list with date-grouped sections (Today / Yesterday /
Earlier-this-week / Earlier-this-month / Older) and the All/Unread
buttons with a counted segmented filter using pill-active. Adds a
\"Notification preferences →\" link in the page header pointing to
/profile, where the prefs section already lives. Pagination semantics
unchanged. EN + AR + KU translations land inline."
```

**REVIEW CHECKPOINT C.** Pause for human review of the Notifications redesign before final integration.

---

## Final integration + QA

### Task 7: Cross-page verification + PR prep

- [ ] **Step 1: Full lint + build**

```bash
cd boilerplateFE && npm run lint && npm run build
cd ../boilerplateBE && dotnet build src/Starter.Api
```

Expected: all three pass.

- [ ] **Step 2: Live RTL pass on every Phase 3 page**

In the test app browser, switch language to AR (`localStorage.setItem('starter-ui', JSON.stringify({state:{...,language:'ar'}}))` and reload, or use the language switcher). Visit:
- `/files` — hero strip mirrors, total figure stays Latin digits, super-admin toggle on the inline-end side.
- `/reports` — hero cards mirror, eyebrow text right-aligned, Spinner glyph stays correctly placed.
- `/notifications` — group section headers right-aligned, segmented filter pill mirrors, preferences arrow mirrors.

Switch back to EN. No visual differences except direction.

- [ ] **Step 3: Permission matrix sanity check**

Per the spec §6:
- **Super-admin** (`superadmin@testj4visual.com`): all three pages render; Files toggle visible; Reports cross-tenant counts; Notifications scoped to user.
- **Tenant admin** (`acme.admin@acme.com`): all three pages render; Files toggle hidden; Reports counts scoped to tenant; Notifications scoped to user.
- **Regular user** (`acme.alice@acme.com`): Files visible (scoped via visibility filter); Notifications visible; Reports redirects to `/dashboard` (no `System.ExportData`).

- [ ] **Step 4: Phase 1 + Phase 2 spot-check (regression)**

Visit each: `/users`, `/roles`, `/tenants`, `/profile`, `/audit-logs`, `/feature-flags`, `/api-keys`, `/settings`. Confirm no visual changes.

- [ ] **Step 5: Confirm `FilesPage.tsx` is under target**

```bash
wc -l boilerplateFE/src/features/files/pages/FilesPage.tsx
```

Expected: under 250 LOC.

- [ ] **Step 6: Final commit (only if anything fixed up during QA)**

If QA surfaced fixes, commit them with a `fixup:` or scoped fix message. Otherwise this step is a no-op.

- [ ] **Step 7: Push + open PR**

```bash
git push -u origin fe/redesign-phase-3-views
```

PR title: `feat(fe): Phase 3 redesign — Data cluster (Files / Reports / Notifications)`. Body: see Phase 2 PR description for the format; reference the spec + this plan; flag the BE addition (`GET /reports/status-counts`) as the only non-FE change. Note no migrations.

---

## Self-review check

- **Spec coverage:**
  - Files hero strip → Task 4. ✅
  - Files page decomposition → Task 3. ✅
  - Reports status hero → Task 2. ✅
  - Reports BE endpoint → Task 1 (decision committed: existing list query has no aggregates per the BE handler reading; new endpoint added). ✅
  - Notifications date grouping → Tasks 5 + 6. ✅
  - Notifications segmented filter → Task 6. ✅
  - Notifications preferences link → Task 6 (resolved to `/profile`, where the prefs section actually lives). ✅
  - All translations EN + AR + KU inline → Tasks 2 / 4 / 6. ✅
  - Verification routine → Task 7. ✅

- **Placeholders:** scanned — all code blocks complete; no TBD/TODO; conditional branches are implementation choices with concrete resolution paths.

- **Type consistency:** `ReportStatusCounts` (FE type) ↔ `ReportStatusCountsDto` (BE record) — same fields, same names. `MetricCard` props (Task 0) match call-sites in Tasks 2 + 4, while the pre-existing dashboard `StatCard` API is untouched.

- **Open question from spec resolved:** Reports counts come from a new endpoint, decided based on the explorer's read of `GetReportsQueryHandler` showing no per-status totals in the paginated envelope.
