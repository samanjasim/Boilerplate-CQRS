# Phase 3 — Data cluster redesign — implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring Files / Reports / Notifications onto the J4 Spectrum visual language with two patterns — hero-strip (Files, Reports) and grouped-list (Notifications) — without changing existing behaviour.

**Architecture:** One pure-utility task (date grouping) tested before use. One small BE endpoint for Reports status counts. Three FE hero-strip / list-redesign tasks. Carry-along decomposition of the 852-LOC `FilesPage.tsx`. Subagent-driven cadence with review per page.

**Tech Stack:** React 19, TypeScript, Tailwind 4, shadcn/ui, TanStack Query, react-i18next, .NET 10 (Reports BE endpoint).

**Spec:** [`docs/superpowers/specs/2026-04-29-redesign-phase-3-data.md`](../specs/2026-04-29-redesign-phase-3-data.md)

---

## File structure

**New (FE):**
- `boilerplateFE/src/components/common/StatCard.tsx` — extracted from `FeatureFlagStatStrip.tsx`, reused by Files + Reports heroes.
- `boilerplateFE/src/features/files/components/StorageHeroStrip.tsx` — replaces `StorageSummaryPanel.tsx`.
- `boilerplateFE/src/features/files/components/FileUploadDialog.tsx` — extracted from `FilesPage.tsx`.
- `boilerplateFE/src/features/files/components/FileEditDialog.tsx` — extracted from `FilesPage.tsx` (rename + edit dialog inside detail modal).
- `boilerplateFE/src/features/files/components/FileRowActions.tsx` — per-row dropdown menu + its dialog state.
- `boilerplateFE/src/features/files/components/FilesGridView.tsx` — grid layout.
- `boilerplateFE/src/features/files/components/FilesTableView.tsx` — table layout.
- `boilerplateFE/src/features/reports/components/ReportStatusHeroStrip.tsx` — Active / Completed / Failed cards.
- `boilerplateFE/src/features/reports/api/reports.api.ts` — extend with `getStatusCounts()`.
- `boilerplateFE/src/features/notifications/utils/groupByDate.ts` — pure grouping utility (Today / Yesterday / This week / This month / Older).
- `boilerplateFE/src/features/notifications/utils/groupByDate.test.ts` — vitest tests for the grouping function.

**Modified (FE):**
- `boilerplateFE/src/features/files/pages/FilesPage.tsx` — composition only after extracts; target < 250 LOC.
- `boilerplateFE/src/features/reports/pages/ReportsPage.tsx` — insert `<ReportStatusHeroStrip />` above filter row.
- `boilerplateFE/src/features/notifications/pages/NotificationsPage.tsx` — segmented filter + grouped rendering + preferences link.
- `boilerplateFE/src/features/feature-flags/components/FeatureFlagStatStrip.tsx` — re-import `StatCard` from common.
- `boilerplateFE/src/features/reports/api/reports.queries.ts` — add `useReportStatusCounts()`.
- `boilerplateFE/src/types/report.types.ts` — add `ReportStatusCounts` interface.
- `boilerplateFE/src/config/api.config.ts` — add `REPORTS.STATUS_COUNTS` route.
- `boilerplateFE/src/i18n/locales/{en,ar,ku}/translation.json` — new keys per task.

**New (BE):**
- `boilerplateBE/src/Starter.Application/Features/Reports/Queries/GetReportStatusCounts/GetReportStatusCountsQuery.cs`
- `boilerplateBE/src/Starter.Application/Features/Reports/Queries/GetReportStatusCounts/GetReportStatusCountsQueryHandler.cs`
- `boilerplateBE/src/Starter.Abstractions/DTOs/Reports/ReportStatusCountsDto.cs`

**Modified (BE):**
- `boilerplateBE/src/Starter.Api/Controllers/ReportsController.cs` — add `GET /status-counts` action.

**Deleted:**
- `boilerplateFE/src/features/files/components/StorageSummaryPanel.tsx` — replaced by hero strip.

---

## Tasks

The plan reads top-to-bottom and ships in three review checkpoints: **(A) Reports BE + hero**, **(B) Files**, **(C) Notifications**, then final integration. Tasks 0–1 are shared setup that must land before A/B/C. Per the spec, all three pages ship in one PR.

> **Path note:** all `npm` / source paths are relative to `boilerplateFE/`. All `dotnet` / source paths are relative to `boilerplateBE/`. Run lint/build commands from inside the relevant repo root.

---

### Task 0: Branch + shared `StatCard` extraction

**Files:**
- Create: `boilerplateFE/src/components/common/StatCard.tsx`
- Modify: `boilerplateFE/src/components/common/index.ts`
- Modify: `boilerplateFE/src/features/feature-flags/components/FeatureFlagStatStrip.tsx`

- [ ] **Step 1: Confirm branch state**

```bash
git status
git rev-parse --abbrev-ref HEAD
```

Expected: clean working tree, on `fe/redesign-phase-3-views`. If not on the branch, `git checkout fe/redesign-phase-3-views`.

- [ ] **Step 2: Create the shared `StatCard` component**

Create `boilerplateFE/src/components/common/StatCard.tsx` with:

```tsx
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import type { ReactNode } from 'react';

export interface StatCardProps {
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

const TONE_CLASSES: Record<NonNullable<StatCardProps['tone']>, string> = {
  default: '',
  active: 'bg-[var(--active-bg)]/40',
  destructive: 'bg-destructive/10',
};

export function StatCard({
  label,
  value,
  secondary,
  eyebrow,
  emphasis,
  tone = 'default',
  glyph,
  className,
}: StatCardProps) {
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
export * from './StatCard';
```

(If the barrel uses named exports instead of `export *`, follow the existing pattern.)

- [ ] **Step 4: Migrate `FeatureFlagStatStrip` to import the shared `StatCard`**

Read `boilerplateFE/src/features/feature-flags/components/FeatureFlagStatStrip.tsx` (lines 20–39 currently define a local `StatCard`). Delete the local definition and replace its consumers with the imported one:

```tsx
// At the top:
import { StatCard } from '@/components/common';

// Delete the local StatCard function (lines ~20–39 currently).

// JSX usages in lines ~51–68 stay the same shape since props are compatible.
```

The local `StatCard` accepted `{ label, value, secondary, emphasis }` — exactly the shared version's API plus the new optional fields. No call-site changes needed.

- [ ] **Step 5: Lint + build**

From `boilerplateFE/`:

```bash
npm run lint
npm run build
```

Expected: both pass.

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/components/common/StatCard.tsx \
        boilerplateFE/src/components/common/index.ts \
        boilerplateFE/src/features/feature-flags/components/FeatureFlagStatStrip.tsx
git commit -m "refactor(fe): extract StatCard to @/components/common

Phase 3 needs the same stat-card primitive on Files and Reports heroes.
Lifted the local one out of FeatureFlagStatStrip and added the optional
fields (eyebrow, tone, glyph) the new heroes will use. FeatureFlags
keeps its existing visuals — props are a superset of the old local one."
```

---

## Checkpoint A — Reports

### Task 1: Reports — BE status-counts endpoint

**Files:**
- Create: `boilerplateBE/src/Starter.Abstractions/DTOs/Reports/ReportStatusCountsDto.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Reports/Queries/GetReportStatusCounts/GetReportStatusCountsQuery.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Reports/Queries/GetReportStatusCounts/GetReportStatusCountsQueryHandler.cs`
- Modify: `boilerplateBE/src/Starter.Api/Controllers/ReportsController.cs`

- [ ] **Step 1: Read the existing list handler for conventions**

```bash
ls boilerplateBE/src/Starter.Application/Features/Reports/Queries/
```

Find the `GetReportsQueryHandler` and read it. Note: namespace, the `IApplicationDbContext` injection, the `Result<T>` pattern, the `[Authorize(Policy = ...)]` policy used on the controller's list action.

- [ ] **Step 2: Create the DTO**

Create `boilerplateBE/src/Starter.Abstractions/DTOs/Reports/ReportStatusCountsDto.cs`:

```csharp
namespace Starter.Abstractions.DTOs.Reports;

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
using Starter.Abstractions.DTOs.Reports;
using Starter.Domain.Common;

namespace Starter.Application.Features.Reports.Queries.GetReportStatusCounts;

public sealed record GetReportStatusCountsQuery() : IRequest<Result<ReportStatusCountsDto>>;
```

If `Result<T>` lives in a different namespace in this solution (`Starter.Domain.Common` vs `Starter.Abstractions.Common` etc.), match the existing list query's `using` block.

- [ ] **Step 4: Create the handler**

Create `boilerplateBE/src/Starter.Application/Features/Reports/Queries/GetReportStatusCounts/GetReportStatusCountsQueryHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.DTOs.Reports;
using Starter.Application.Common.Persistence;
using Starter.Domain.Common;
using Starter.Domain.Reports.Enums;

namespace Starter.Application.Features.Reports.Queries.GetReportStatusCounts;

internal sealed class GetReportStatusCountsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetReportStatusCountsQuery, Result<ReportStatusCountsDto>>
{
    public async Task<Result<ReportStatusCountsDto>> Handle(
        GetReportStatusCountsQuery request,
        CancellationToken cancellationToken)
    {
        // Tenant filter is applied automatically via ApplicationDbContext global filters.
        var counts = await context.ReportRequests
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var dict = counts.ToDictionary(x => x.Status, x => x.Count);

        var dto = new ReportStatusCountsDto(
            Pending: dict.GetValueOrDefault(ReportStatus.Pending),
            Processing: dict.GetValueOrDefault(ReportStatus.Processing),
            Completed: dict.GetValueOrDefault(ReportStatus.Completed),
            Failed: dict.GetValueOrDefault(ReportStatus.Failed)
        );

        return Result.Success(dto);
    }
}
```

If `ReportStatus` is a string enum (check `Domain/Reports/Enums/ReportStatus.cs`), the `GetValueOrDefault` calls work the same; if `Status` is a string column instead of an enum, swap to dictionary keys of `"Pending"` etc.

- [ ] **Step 5: Add controller action**

Read `boilerplateBE/src/Starter.Api/Controllers/ReportsController.cs` first to see the policy used on the list action. Add a new action mirroring the list action's authorization and `HandleResult` pattern:

```csharp
// After the existing list action.

[HttpGet("status-counts")]
[Authorize(Policy = Permissions.System.ExportData)] // match the list action's policy
public async Task<IActionResult> GetStatusCounts(CancellationToken ct)
{
    var result = await Sender.Send(new GetReportStatusCountsQuery(), ct);
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
git add boilerplateBE/src/Starter.Abstractions/DTOs/Reports/ReportStatusCountsDto.cs \
        boilerplateBE/src/Starter.Application/Features/Reports/Queries/GetReportStatusCounts/ \
        boilerplateBE/src/Starter.Api/Controllers/ReportsController.cs
git commit -m "feat(be/reports): add GET /reports/status-counts

Returns per-status totals (pending, processing, completed, failed)
scoped via the existing tenant query filter. Backs the Phase 3 Reports
status-hero strip — the existing list query response only carries
pagination, no aggregates."
```

---

### Task 2: Reports — FE hook + hero strip

**Files:**
- Modify: `boilerplateFE/src/types/report.types.ts`
- Modify: `boilerplateFE/src/config/api.config.ts`
- Modify: `boilerplateFE/src/features/reports/api/reports.api.ts`
- Modify: `boilerplateFE/src/features/reports/api/reports.queries.ts`
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
getStatusCounts: async (): Promise<ApiResponse<ReportStatusCounts>> => {
  const r = await apiClient.get(API_ENDPOINTS.REPORTS.STATUS_COUNTS);
  return r.data;
},
```

Add the import: `import type { ReportStatusCounts } from '@/types/report.types';` at the top if not already present.

- [ ] **Step 4: Add the React Query hook**

Read `boilerplateFE/src/features/reports/api/reports.queries.ts` to see the existing `useReports` hook + `queryKeys` shape. Add:

```ts
import { reportsApi } from './reports.api';
import type { ReportStatusCounts } from '@/types/report.types';

// Extend queryKeys with:
//   statusCounts: () => [...queryKeys.reports.all, 'statusCounts'] as const,
// (Match the pattern of the existing keys factory.)

export function useReportStatusCounts() {
  return useQuery({
    queryKey: queryKeys.reports.statusCounts(),
    queryFn: () => reportsApi.getStatusCounts(),
    select: (r) => r.data, // unwrap ApiResponse<T>.data
    staleTime: 30_000,     // counts don't change often
  });
}
```

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
import { StatCard } from '@/components/common';
import { Spinner } from '@/components/ui/spinner';
import { useReportStatusCounts } from '../api/reports.queries';

export function ReportStatusHeroStrip() {
  const { t } = useTranslation();
  const { data, isLoading } = useReportStatusCounts();

  if (isLoading || !data) {
    // Render layout skeleton so the page doesn't reflow when counts arrive.
    return (
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 mb-6">
        <StatCard label={t('reports.hero.active')} eyebrow={t('reports.hero.activeEyebrow')} value="—" tone="active" />
        <StatCard label={t('reports.hero.completed')} eyebrow={t('reports.hero.completedEyebrow')} value="—" />
      </div>
    );
  }

  const active = data.pending + data.processing;
  const showFailed = data.failed > 0;
  const isProcessing = data.processing > 0;

  return (
    <div className={`grid gap-4 sm:grid-cols-2 ${showFailed ? 'lg:grid-cols-3' : ''} mb-6`}>
      <StatCard
        label={t('reports.hero.active')}
        eyebrow={t('reports.hero.activeEyebrow')}
        value={active}
        emphasis={active > 0}
        tone="active"
        glyph={isProcessing ? <Spinner size="sm" className="h-4 w-4" /> : undefined}
      />
      <StatCard
        label={t('reports.hero.completed')}
        eyebrow={t('reports.hero.completedEyebrow')}
        value={data.completed}
      />
      {showFailed && (
        <StatCard
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
cp boilerplateFE/src/features/reports/components/ReportStatusHeroStrip.tsx _testJ4visual/_testJ4visual-FE/src/features/reports/components/
cp boilerplateFE/src/features/reports/api/reports.api.ts _testJ4visual/_testJ4visual-FE/src/features/reports/api/
cp boilerplateFE/src/features/reports/api/reports.queries.ts _testJ4visual/_testJ4visual-FE/src/features/reports/api/
cp boilerplateFE/src/features/reports/pages/ReportsPage.tsx _testJ4visual/_testJ4visual-FE/src/features/reports/pages/
cp boilerplateFE/src/types/report.types.ts _testJ4visual/_testJ4visual-FE/src/types/
cp boilerplateFE/src/config/api.config.ts _testJ4visual/_testJ4visual-FE/src/config/
cp boilerplateFE/src/i18n/locales/en/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/en/
cp boilerplateFE/src/i18n/locales/ar/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/ar/
cp boilerplateFE/src/i18n/locales/ku/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/ku/
cp boilerplateFE/src/components/common/StatCard.tsx _testJ4visual/_testJ4visual-FE/src/components/common/
cp boilerplateFE/src/components/common/index.ts _testJ4visual/_testJ4visual-FE/src/components/common/
```

Note: the BE endpoint requires regenerating the test app to see real counts. Until then, the hero will fail the request and render the skeleton — that's expected and acceptable for the visual check at this stage. (Or seed a few report requests via `POST /api/v1/reports` to exercise the hero with real data.)

Open `http://localhost:3100/reports` in the browser. Verify:
- Hero strip renders above the filter row.
- Three cards (or two if no failures): Active, Completed, optional Failed.
- "Active" card shows tinted background; "Failed" card (when present) shows red tint.
- Switch to AR: hero mirrors correctly, eyebrow text reads right-to-left, the gradient-text number stays Latin digits.

- [ ] **Step 10: Commit**

```bash
git add boilerplateFE/src/types/report.types.ts \
        boilerplateFE/src/config/api.config.ts \
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
- Create: 5 new components under `boilerplateFE/src/features/files/components/`
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

- [ ] **Step 2: Extract `FileUploadDialog`**

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

- [ ] **Step 3: Extract `FileEditDialog`**

Create `boilerplateFE/src/features/files/components/FileEditDialog.tsx`. Lift the edit form JSX (rename / description / tags / category) and the `isEditing` state.

```tsx
export interface FileEditDialogProps {
  file: FileMetadata | null;
  onClose: () => void;
}
```

Treat `file === null` as closed. The dialog calls `useUpdateFile()` internally and closes on success.

- [ ] **Step 4: Extract `FileRowActions`**

Create `boilerplateFE/src/features/files/components/FileRowActions.tsx`. Lift the dropdown menu + the trio of action dialogs that hang off it (delete confirm, share dialog, ownership transfer dialog). Prop interface:

```tsx
export interface FileRowActionsProps {
  file: FileMetadata;
}
```

Permissions logic (`canDelete`, `isOwner`, `fileCanShare`) moves inside the component and uses `usePermissions()` + `useAuthStore` directly. Each dialog state is component-local (`shareOpen`, `transferOpen`, `deleteConfirm`).

In `FilesPage.tsx`, the `<TableCell>` for actions becomes simply `<FileRowActions file={f} />`. The page-level `shareFile`, `transferFile`, `deleteFile` state declarations + their dialogs are removed.

- [ ] **Step 5: Extract `FilesGridView` and `FilesTableView`**

Create `boilerplateFE/src/features/files/components/FilesGridView.tsx` and `FilesTableView.tsx`. Each accepts:

```tsx
export interface FilesViewProps {
  files: FileMetadata[];
  isLoading: boolean;
  onSelect: (file: FileMetadata) => void; // for opening detail modal
}
```

Move the inline JSX. Each view uses `<FileRowActions>` for the per-row action menu.

- [ ] **Step 6: Verify FilesPage shrinks**

```bash
wc -l boilerplateFE/src/features/files/pages/FilesPage.tsx
```

Expected: under 250 LOC. The page should now be: imports + state for filters/pagination + query call + `<PageHeader>` + filter chips + view toggle + `{viewMode === 'grid' ? <FilesGridView ... /> : <FilesTableView ... />}` + `<Pagination>` + `<FileUploadDialog>` + `<FileEditDialog file={detailFile && isEditing ? detailFile : null} ... />` + the file detail modal (still page-level since it shows extra info beyond editing).

- [ ] **Step 7: Lint + build**

```bash
npm run lint && npm run build
```

Expected: both pass with no behaviour changes.

- [ ] **Step 8: Sync + smoke test**

```bash
cp -r boilerplateFE/src/features/files/ _testJ4visual/_testJ4visual-FE/src/features/files/
```

Open `http://localhost:3100/files`. Walk through:
1. Upload a file → opens dialog → submits → file appears in list.
2. Click a file → detail modal opens → enter edit mode → save → values update.
3. Open a row's action menu → trigger delete → confirm → file disappears.
4. Switch grid → list and back.

If any flow breaks, the extraction missed a piece of state. Fix and re-test.

- [ ] **Step 9: Commit**

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
import { HardDrive } from 'lucide-react';
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
    <Card variant="glass" className="mb-6">
      <CardContent className="py-5">
        <div className="grid gap-6 md:grid-cols-12 items-start">
          {/* Total */}
          <div className="md:col-span-4">
            <div className="flex items-center gap-2 text-xs uppercase tracking-wide text-muted-foreground">
              <HardDrive className="h-3.5 w-3.5" />
              {t('files.storageHero.total')}
            </div>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-3xl font-semibold tabular-nums gradient-text">
                {isLoading || !data ? '—' : formatFileSize(data.totalBytes)}
              </span>
              {showQuota && quotaBytes && (
                <span className="text-sm text-muted-foreground">
                  {t('files.storageHero.ofQuota', { quota: formatFileSize(quotaBytes) })}
                </span>
              )}
            </div>
            {usagePct != null && (
              <div className="mt-3 h-1.5 rounded-full bg-muted overflow-hidden">
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

          {/* By category */}
          <div className="md:col-span-7 space-y-2">
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

          {/* Cross-tenant toggle (super-admin only) */}
          {isPlatformAdmin && (
            <div className="md:col-span-1 md:justify-self-end">
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

Expected: build will fail because `StorageSummaryPanel` is no longer referenced but still exists. That's OK — fixed in the next step.

- [ ] **Step 5: Delete the old panel + dead translation keys**

```bash
rm boilerplateFE/src/features/files/components/StorageSummaryPanel.tsx
```

Search for any other consumer:
```bash
grep -rn "StorageSummaryPanel" boilerplateFE/src/
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
cp -r boilerplateFE/src/features/files/ _testJ4visual/_testJ4visual-FE/src/features/files/
cp boilerplateFE/src/i18n/locales/en/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/en/
cp boilerplateFE/src/i18n/locales/ar/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/ar/
cp boilerplateFE/src/i18n/locales/ku/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/ku/
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

### Task 5: Notifications — Date-grouping utility (TDD)

The grouping logic is pure and date-bound — easy to unit-test, hard to verify visually without contrived data. TDD it.

**Files:**
- Create: `boilerplateFE/src/features/notifications/utils/groupByDate.ts`
- Create: `boilerplateFE/src/features/notifications/utils/groupByDate.test.ts`

- [ ] **Step 1: Write the failing test**

Create `boilerplateFE/src/features/notifications/utils/groupByDate.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { groupNotificationsByDate, type GroupKey } from './groupByDate';
import type { Notification } from '@/types';

const now = new Date('2026-04-29T12:00:00Z'); // Wednesday

const make = (id: string, daysAgo: number): Notification => ({
  id,
  type: 'system',
  title: id,
  message: '',
  data: null,
  isRead: false,
  createdAt: new Date(now.getTime() - daysAgo * 86400000).toISOString(),
});

describe('groupNotificationsByDate', () => {
  it('returns groups in order today / yesterday / earlierThisWeek / earlierThisMonth / older', () => {
    const items = [
      make('today',          0),
      make('yesterday',      1),
      make('earlierWeek',    2), // Monday — same ISO week
      make('earlierMonth',   10), // earlier in April
      make('older',          120), // 4 months ago
    ];

    const groups = groupNotificationsByDate(items, now);

    expect(groups.map(g => g.key)).toEqual([
      'today',
      'yesterday',
      'earlierThisWeek',
      'earlierThisMonth',
      'older',
    ] satisfies GroupKey[]);
  });

  it('skips empty groups', () => {
    const items = [make('only', 0), make('also-today', 0), make('older', 60)];
    const groups = groupNotificationsByDate(items, now);
    expect(groups.map(g => g.key)).toEqual(['today', 'older']);
  });

  it('preserves input order within a group', () => {
    const items = [make('a', 0), make('b', 0), make('c', 0)];
    const groups = groupNotificationsByDate(items, now);
    expect(groups[0].items.map(i => i.id)).toEqual(['a', 'b', 'c']);
  });

  it('treats today by calendar day, not 24h window', () => {
    const earlyToday = {
      ...make('early', 0),
      createdAt: new Date('2026-04-29T00:30:00Z').toISOString(),
    };
    const yesterdayLate = {
      ...make('lateYesterday', 0),
      createdAt: new Date('2026-04-28T23:30:00Z').toISOString(),
    };
    const groups = groupNotificationsByDate([earlyToday, yesterdayLate], now);
    expect(groups.find(g => g.key === 'today')?.items.map(i => i.id)).toEqual(['early']);
    expect(groups.find(g => g.key === 'yesterday')?.items.map(i => i.id)).toEqual(['lateYesterday']);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd boilerplateFE && npx vitest run src/features/notifications/utils/groupByDate.test.ts
```

Expected: FAIL with "Cannot find module './groupByDate'".

- [ ] **Step 3: Implement the utility**

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

- [ ] **Step 4: Run test to verify it passes**

```bash
npx vitest run src/features/notifications/utils/groupByDate.test.ts
```

Expected: 4 tests PASS.

- [ ] **Step 5: Commit**

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
  const { data: unreadCountData } = useUnreadCount();
  const { mutate: markRead } = useMarkRead();
  const { mutate: markAllRead } = useMarkAllRead();

  const notifications = data?.data ?? [];
  const pagination = data?.pagination;
  const totalAll = pagination?.totalCount ?? 0;
  const totalUnread = unreadCountData?.data?.count ?? 0;

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

Expected: both pass. If `useUnreadCount` returns a different shape than assumed (`data.count` vs `data.data.count`), adjust the `unreadCountData?.data?.count` line — read the hook in `boilerplateFE/src/features/notifications/api/notifications.queries.ts` to confirm.

- [ ] **Step 4: Sync + visual check**

```bash
cp -r boilerplateFE/src/features/notifications/ _testJ4visual/_testJ4visual-FE/src/features/notifications/
cp boilerplateFE/src/i18n/locales/en/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/en/
cp boilerplateFE/src/i18n/locales/ar/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/ar/
cp boilerplateFE/src/i18n/locales/ku/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/ku/
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

- **Placeholders:** scanned — all code blocks complete; no TBD/TODO; the only "if X then else" branches are explicit conditional logic (e.g., `useUnreadCount` shape adjust note in Task 6 step 3) with clear resolution paths.

- **Type consistency:** `ReportStatusCounts` (FE type) ↔ `ReportStatusCountsDto` (BE record) — same fields, same names. `StatCard` props (Task 0) match call-sites in Tasks 2 + 4.

- **Open question from spec resolved:** Reports counts come from a new endpoint, decided based on the explorer's read of `GetReportsQueryHandler` showing no per-status totals in the paginated envelope.
