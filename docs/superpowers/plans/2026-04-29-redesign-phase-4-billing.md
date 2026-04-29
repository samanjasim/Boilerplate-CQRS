# Phase 4 Billing — implementation plan

> **For agentic workers:** Use `superpowers:executing-plans` to implement this plan task-by-task. Only use `superpowers:subagent-driven-development` if the user explicitly asks for parallel/subagent work. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring the 5 billing pages onto J4 Spectrum: hero treatment for tenant-side `BillingPage` and platform-side `SubscriptionsPage` + `SubscriptionDetailPage`; J4 polish (no new hero) for `BillingPlansPage` and `PricingPage`.

**Architecture:** One shared presentational `<BillingHero>` (plan card + usage stat strip) consumed by both tenant-scoped pages. The shared `MetricCard` primitive gets a tiny optional children slot so usage progress bars stay inside the same reusable card vocabulary instead of inventing a one-off metric surface. One small BE endpoint (`/billing/subscriptions/status-counts`) reuses the Phase 3 Reports-status-counts pattern, exposed from the existing module controller. Polish work uses `<Card variant="glass">`, `gradient-text`, `tabular-nums`, existing J4 utilities, and the shared table/card primitives instead of duplicate wrappers.

**Tech Stack:** React 19, TypeScript, Tailwind 4, shadcn/ui, TanStack Query, react-i18next, .NET 10 (one new module endpoint).

**Spec:** [`docs/superpowers/specs/2026-04-29-redesign-phase-4-billing-design.md`](../specs/2026-04-29-redesign-phase-4-billing-design.md)

---

## Resolved spec §8 questions

1. **Status-counts endpoint location** — Billing is a module. Existing platform-admin subscriptions list lives at `Starter.Module.Billing/Controllers/BillingController.cs:170-179` (`GET subscriptions`, policy `BillingPermissions.ManageTenantSubscriptions`). New action goes in the **same controller** at `[HttpGet("subscriptions/status-counts")]`. Query handler lives next to the existing `GetAllSubscriptionsQueryHandler` under `Application/Queries/GetSubscriptionStatusCounts/`. Reads from `BillingDbContext.TenantSubscriptions` with `.IgnoreQueryFilters()` (cross-tenant by intent, mirroring the existing list handler at lines 26–30 of `GetAllSubscriptionsQueryHandler.cs`).

2. **`isPopular` flag on `SubscriptionPlan`** — does NOT exist in `boilerplateFE/src/types/billing.types.ts`. Heuristic: select the plan with the highest `subscriberCount` among `isPublic && !isFree && isActive` plans; ties broken by lowest `displayOrder`. Pure FE — no schema change.

3. **`InlinePlanSelector` reuse** — 80 LOC, uses shadcn `<Select>` which already follows the design system. Composes cleanly inside the shared glass `<Table>`; visual verification only, no token sweep needed.

---

## File structure

**New (FE):**
- `boilerplateFE/src/features/billing/components/BillingHero.tsx` — shared "plan card + 3 metric cards" hero used by `BillingPage` and `SubscriptionDetailPage`.
- `boilerplateFE/src/features/billing/components/SubscriptionStatusHero.tsx` — `Active / Trialing / Past-due` strip for `SubscriptionsPage`.
- `boilerplateFE/src/features/billing/components/PricingIntervalToggle.tsx` — segmented pill control (replaces inline buttons in `PricingPage`).
- `boilerplateFE/src/features/billing/utils/popular-plan.ts` — `pickPopularPlan(plans)` helper + unit tests.
- `boilerplateFE/src/features/billing/utils/popular-plan.test.ts`

**Modified (FE):**
- `boilerplateFE/src/components/common/MetricCard.tsx` — add optional `children` slot for inline progress bars; no visual changes for existing callers.
- `boilerplateFE/src/features/billing/pages/BillingPage.tsx` — replace plan card + UsageBar grid with `<BillingHero>`; keep "Cancel Subscription" button + history table.
- `boilerplateFE/src/features/billing/pages/SubscriptionDetailPage.tsx` — replace plan card + UsageBar grid with `<BillingHero>`; load tenant name via the existing tenants query for breadcrumb/eyebrow; keep change-plan dialog.
- `boilerplateFE/src/features/billing/pages/SubscriptionsPage.tsx` — add `<SubscriptionStatusHero>` above search row; keep the shared `<Table>` as the glass surface owner.
- `boilerplateFE/src/features/billing/pages/BillingPlansPage.tsx` — `Card variant="glass"`, `gradient-text` price, `tabular-nums` subscriber count, copper feature checkmarks.
- `boilerplateFE/src/features/billing/pages/PricingPage.tsx` — segmented toggle, popular-plan halo + scale, `gradient-text` price, copper feature checkmarks.
- `boilerplateFE/src/features/billing/api/billing.api.ts` — add `getSubscriptionStatusCounts()`.
- `boilerplateFE/src/features/billing/api/billing.queries.ts` — add `useSubscriptionStatusCounts()`.
- `boilerplateFE/src/types/billing.types.ts` — add `SubscriptionStatusCounts` interface.
- `boilerplateFE/src/config/api.config.ts` — add `BILLING.SUBSCRIPTION_STATUS_COUNTS` route.
- `boilerplateFE/src/lib/query/keys.ts` — add `queryKeys.billing.subscriptions.statusCounts()`.
- `boilerplateFE/src/i18n/locales/{en,ar,ku}/translation.json` — new keys per task.

**New (BE):**
- `boilerplateBE/src/modules/Starter.Module.Billing/Application/Queries/GetSubscriptionStatusCounts/GetSubscriptionStatusCountsQuery.cs`
- `boilerplateBE/src/modules/Starter.Module.Billing/Application/Queries/GetSubscriptionStatusCounts/GetSubscriptionStatusCountsQueryHandler.cs`
- `boilerplateBE/src/modules/Starter.Module.Billing/Application/DTOs/SubscriptionStatusCountsDto.cs`

**Modified (BE):**
- `boilerplateBE/src/modules/Starter.Module.Billing/Controllers/BillingController.cs` — add `[HttpGet("subscriptions/status-counts")]` action.

---

## Tasks

The plan reads top-to-bottom. Five page-level review checkpoints map cleanly to the spec's "review per page" cadence. All five pages ship in one PR.

> **Path note:** `npm` commands run from inside `boilerplateFE/`. `dotnet` commands from `boilerplateBE/`.

---

### Task 0: Branch confirm

**Files:** none

- [ ] **Step 1: Confirm branch state**

```bash
git status
git rev-parse --abbrev-ref HEAD
```

Expected: clean working tree, on `fe/post-phase-3` (or whatever billing branch the user wants — confirm before proceeding). If creating a new branch, use `fe/redesign-phase-4-billing-views`.

- [ ] **Step 2: Verify Phase 3 primitives are present**

```bash
ls boilerplateFE/src/components/common/MetricCard.tsx
grep -q '"glass"' boilerplateFE/src/components/ui/card.tsx && echo "glass variant present"
```

Expected: file listed, "glass variant present" printed. These are dependencies the new heroes consume.

---

## Checkpoint A — Tenant-side hero (BillingPage + SubscriptionDetailPage)

### Task 1: BE — Subscription status-counts endpoint

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Billing/Application/DTOs/SubscriptionStatusCountsDto.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Billing/Application/Queries/GetSubscriptionStatusCounts/GetSubscriptionStatusCountsQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Billing/Application/Queries/GetSubscriptionStatusCounts/GetSubscriptionStatusCountsQueryHandler.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Billing/Controllers/BillingController.cs`

- [ ] **Step 1: Create the DTO**

Create `boilerplateBE/src/modules/Starter.Module.Billing/Application/DTOs/SubscriptionStatusCountsDto.cs`:

```csharp
namespace Starter.Module.Billing.Application.DTOs;

public sealed record SubscriptionStatusCountsDto(
    int Trialing,
    int Active,
    int PastDue,
    int Canceled,
    int Expired
);
```

- [ ] **Step 2: Create the query**

Create `boilerplateBE/src/modules/Starter.Module.Billing/Application/Queries/GetSubscriptionStatusCounts/GetSubscriptionStatusCountsQuery.cs`:

```csharp
using MediatR;
using Starter.Module.Billing.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetSubscriptionStatusCounts;

public sealed record GetSubscriptionStatusCountsQuery() : IRequest<Result<SubscriptionStatusCountsDto>>;
```

- [ ] **Step 3: Create the handler**

Create `boilerplateBE/src/modules/Starter.Module.Billing/Application/Queries/GetSubscriptionStatusCounts/GetSubscriptionStatusCountsQueryHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Billing.Application.DTOs;
using Starter.Module.Billing.Domain.Enums;
using Starter.Module.Billing.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetSubscriptionStatusCounts;

internal sealed class GetSubscriptionStatusCountsQueryHandler(BillingDbContext billingContext)
    : IRequestHandler<GetSubscriptionStatusCountsQuery, Result<SubscriptionStatusCountsDto>>
{
    public async Task<Result<SubscriptionStatusCountsDto>> Handle(
        GetSubscriptionStatusCountsQuery request,
        CancellationToken cancellationToken)
    {
        // Cross-tenant by intent — this is the platform-admin aggregate.
        // Mirrors GetAllSubscriptionsQueryHandler's IgnoreQueryFilters() pattern.
        var counts = await billingContext.TenantSubscriptions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var dict = counts.ToDictionary(x => x.Status, x => x.Count);

        var dto = new SubscriptionStatusCountsDto(
            Trialing: dict.GetValueOrDefault(SubscriptionStatus.Trialing),
            Active: dict.GetValueOrDefault(SubscriptionStatus.Active),
            PastDue: dict.GetValueOrDefault(SubscriptionStatus.PastDue),
            Canceled: dict.GetValueOrDefault(SubscriptionStatus.Canceled),
            Expired: dict.GetValueOrDefault(SubscriptionStatus.Expired)
        );

        return Result.Success(dto);
    }
}
```

- [ ] **Step 4: Add controller action**

Edit `boilerplateBE/src/modules/Starter.Module.Billing/Controllers/BillingController.cs`. Find the existing `GetAllSubscriptions` action (around line 170–179). Add the new action **immediately after it** (so the `subscriptions` group stays contiguous):

```csharp
/// <summary>
/// Get subscription status distribution counts (SuperAdmin).
/// </summary>
[HttpGet("subscriptions/status-counts")]
[Authorize(Policy = BillingPermissions.ManageTenantSubscriptions)]
[ProducesResponseType(typeof(ApiResponse<SubscriptionStatusCountsDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetSubscriptionStatusCounts(CancellationToken ct = default)
{
    var result = await Mediator.Send(new GetSubscriptionStatusCountsQuery(), ct);
    return HandleResult(result);
}
```

Add the using if not already present:

```csharp
using Starter.Module.Billing.Application.Queries.GetSubscriptionStatusCounts;
```

- [ ] **Step 5: Build the BE**

```bash
cd boilerplateBE && dotnet build src/Starter.Api
```

Expected: build clean. If `BillingPermissions.ManageTenantSubscriptions` import is missing, copy the using block from the existing controller's other actions.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Billing/Application/DTOs/SubscriptionStatusCountsDto.cs \
        boilerplateBE/src/modules/Starter.Module.Billing/Application/Queries/GetSubscriptionStatusCounts/ \
        boilerplateBE/src/modules/Starter.Module.Billing/Controllers/BillingController.cs
git commit -m "feat(be/billing): add GET /billing/subscriptions/status-counts

Returns per-status totals (trialing, active, past-due, canceled,
expired) across all tenants for the platform-admin aggregate hero.
Mirrors the GetReportStatusCountsQuery pattern from Phase 3. Reads
from BillingDbContext with IgnoreQueryFilters() — same cross-tenant
pathway the existing GetAllSubscriptionsQueryHandler uses."
```

---

### Task 2: Shared `BillingHero` + wire into BillingPage and SubscriptionDetailPage

The two pages render the same hero from different data sources. Build the component once; consume from both.

**Files:**
- Modify: `boilerplateFE/src/components/common/MetricCard.tsx`
- Create: `boilerplateFE/src/features/billing/components/BillingHero.tsx`
- Modify: `boilerplateFE/src/features/billing/pages/BillingPage.tsx`
- Modify: `boilerplateFE/src/features/billing/pages/SubscriptionDetailPage.tsx`
- Modify: `boilerplateFE/src/i18n/locales/{en,ar,ku}/translation.json`

- [ ] **Step 1: Add translation keys**

Find the `"billing":` block in each locale file. Add a `hero` sub-tree (insert near the existing `"currentPlan"`, `"usage"`, `"changePlan"` keys to keep grouping local):

EN (`boilerplateFE/src/i18n/locales/en/translation.json`):
```json
"hero": {
  "yourPlan": "Your plan",
  "period": "{{start}} – {{end}}",
  "autoRenewOff": "Auto-renew off",
  "perMonth": "/ month",
  "perYear": "/ year",
  "free": "Free",
  "usageUsers": "Users",
  "usageStorage": "Storage",
  "usageWebhooks": "Webhooks"
}
```

AR:
```json
"hero": {
  "yourPlan": "خطتك",
  "period": "{{start}} – {{end}}",
  "autoRenewOff": "التجديد التلقائي معطّل",
  "perMonth": "/ شهرياً",
  "perYear": "/ سنوياً",
  "free": "مجاني",
  "usageUsers": "المستخدمون",
  "usageStorage": "التخزين",
  "usageWebhooks": "الويب هوكس"
}
```

KU:
```json
"hero": {
  "yourPlan": "پلانەکەت",
  "period": "{{start}} – {{end}}",
  "autoRenewOff": "نوێکردنەوەی خۆکار ناچالاکە",
  "perMonth": "/ مانگانە",
  "perYear": "/ ساڵانە",
  "free": "بەخۆڕایی",
  "usageUsers": "بەکارهێنەران",
  "usageStorage": "بیرگە",
  "usageWebhooks": "وێبهووکەکان"
}
```

Validate JSON for each:

```bash
node -e "JSON.parse(require('fs').readFileSync('boilerplateFE/src/i18n/locales/en/translation.json','utf8'))"
node -e "JSON.parse(require('fs').readFileSync('boilerplateFE/src/i18n/locales/ar/translation.json','utf8'))"
node -e "JSON.parse(require('fs').readFileSync('boilerplateFE/src/i18n/locales/ku/translation.json','utf8'))"
```

Expected: no output = valid JSON.

- [ ] **Step 2: Add an optional children slot to `MetricCard`**

Edit `boilerplateFE/src/components/common/MetricCard.tsx` so billing progress bars can live inside the shared metric surface. This is backwards-compatible: existing callers render exactly as before because `children` is optional.

```tsx
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
  /** Optional content rendered below the metric value, e.g. a progress bar. */
  children?: ReactNode;
  className?: string;
}
```

Add `children` to the function destructuring and render it after the value row:

```tsx
export function MetricCard({
  label,
  value,
  secondary,
  eyebrow,
  emphasis,
  tone = 'default',
  glyph,
  children,
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
          <span
            className={cn(
              'text-2xl font-semibold tabular-nums',
              emphasis && 'gradient-text'
            )}
          >
            {value}
          </span>
          {glyph && <span className="text-muted-foreground">{glyph}</span>}
          {secondary && <span className="text-sm text-muted-foreground">{secondary}</span>}
        </div>
        {children}
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 3: Create the `BillingHero` component**

Create `boilerplateFE/src/features/billing/components/BillingHero.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { MetricCard } from '@/components/common';
import { STATUS_BADGE_VARIANT } from '@/constants';
import { formatDate } from '@/utils/format';
import { formatFileSize } from '@/utils';
import { cn } from '@/lib/utils';
import type { ReactNode } from 'react';
import type { TenantSubscription, Usage } from '@/types';

export interface BillingHeroProps {
  subscription: TenantSubscription | undefined;
  usage: Usage | undefined;
  /** Optional overline rendered above the plan name (e.g., tenant name on the
   *  platform-admin detail page). */
  eyebrow?: string;
  /** Slot for trailing actions (e.g., "Change plan" plus optional Cancel). */
  action?: ReactNode;
  isLoading?: boolean;
}

interface UsageRow {
  label: string;
  current: number;
  max: number;
  format?: (n: number) => string;
}

function progressClass(pct: number): string {
  if (pct >= 90) return 'bg-destructive';
  if (pct >= 75) return 'bg-warning';
  return 'bg-primary';
}

function ProgressMetric({ label, current, max, format }: UsageRow) {
  const pct = max > 0 ? Math.min(100, Math.round((current / max) * 100)) : 0;
  const fmt = format ?? ((n: number) => String(n));
  return (
    <MetricCard
      label={label}
      value={
        <span className="tabular-nums">
          {fmt(current)} <span className="text-sm text-muted-foreground">/ {fmt(max)}</span>
        </span>
      }
      eyebrow={`${pct}%`}
    >
      <div className="mt-2 h-1.5 rounded-full bg-muted overflow-hidden">
        <div
          className={cn('h-full rounded-full transition-all', progressClass(pct))}
          style={{ width: `${pct}%` }}
        />
      </div>
    </MetricCard>
  );
}

export function BillingHero({ subscription, usage, eyebrow, action, isLoading }: BillingHeroProps) {
  const { t } = useTranslation();

  // Skeleton — keep layout stable while data loads so the page doesn't reflow.
  if (isLoading || !subscription) {
    return (
      <div className="grid gap-4 lg:grid-cols-12 mb-6">
        <Card variant="glass" className="lg:col-span-7">
          <CardContent className="py-5 min-h-[120px]" />
        </Card>
        <div className="lg:col-span-5 grid gap-3 sm:grid-cols-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <Card key={i} variant="glass">
              <CardContent className="py-5 min-h-[88px]" />
            </Card>
          ))}
        </div>
      </div>
    );
  }

  const isMonthly = subscription.billingInterval === 'Monthly';
  const isFree =
    subscription.lockedMonthlyPrice === 0 && subscription.lockedAnnualPrice === 0;
  const price = isFree
    ? t('billing.hero.free')
    : `${isMonthly ? subscription.lockedMonthlyPrice : subscription.lockedAnnualPrice} ${
        subscription.currency ?? ''
      } ${isMonthly ? t('billing.hero.perMonth') : t('billing.hero.perYear')}`;

  const periodText = t('billing.hero.period', {
    start: formatDate(subscription.currentPeriodStart),
    end: formatDate(subscription.currentPeriodEnd),
  });

  const rows: UsageRow[] | null = usage
    ? [
        { label: t('billing.hero.usageUsers'), current: usage.users, max: usage.maxUsers },
        {
          label: t('billing.hero.usageStorage'),
          current: usage.storageBytes,
          max: usage.maxStorageBytes,
          format: formatFileSize,
        },
        { label: t('billing.hero.usageWebhooks'), current: usage.webhooks, max: usage.maxWebhooks },
      ]
    : null;

  return (
    <div className="grid gap-4 lg:grid-cols-12 mb-6">
      {/* Plan card */}
      <Card variant="glass" className="lg:col-span-7">
        <CardContent className="py-5 flex items-start gap-4 flex-wrap">
          <div className="min-w-0 flex-1">
            {eyebrow && (
              <div className="text-[10px] uppercase tracking-[0.12em] text-muted-foreground mb-1">
                {eyebrow}
              </div>
            )}
            <div className="text-xs uppercase tracking-wide text-muted-foreground">
              {t('billing.hero.yourPlan')}
            </div>
            <div className="mt-1 flex items-baseline gap-3 flex-wrap">
              <span className="text-2xl font-semibold gradient-text">{subscription.planName}</span>
              <Badge variant={STATUS_BADGE_VARIANT[subscription.status] ?? 'outline'}>
                {subscription.status}
              </Badge>
              <span className="text-sm text-muted-foreground tabular-nums">{price}</span>
            </div>
            <div className="mt-1 text-xs text-muted-foreground">{periodText}</div>
            {!subscription.autoRenew && (
              <div className="mt-1 text-[10px] uppercase tracking-[0.12em] text-warning">
                {t('billing.hero.autoRenewOff')}
              </div>
            )}
          </div>
          {action && <div className="shrink-0">{action}</div>}
        </CardContent>
      </Card>

      {/* Usage strip */}
      <div className="lg:col-span-5 grid gap-3 sm:grid-cols-3">
        {rows?.map(r => <ProgressMetric key={r.label} {...r} />) ??
          Array.from({ length: 3 }).map((_, i) => (
            <Card key={i} variant="glass">
              <CardContent className="py-5 min-h-[88px]" />
            </Card>
          ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Wire into BillingPage**

Edit `boilerplateFE/src/features/billing/pages/BillingPage.tsx`. Replace **lines 42–142** (plan card block + usage bars block) with:

```tsx
<BillingHero
  subscription={subscription}
  usage={usage}
  isLoading={subLoading || usageLoading}
  action={
    hasPermission(PERMISSIONS.Billing.Manage) && subscription ? (
      <div className="flex flex-wrap items-center gap-2">
        <Button onClick={() => setPlanModalOpen(true)}>
          {t('billing.changePlan')}
        </Button>
        {subscription.planSlug !== 'free' && subscription.status !== 'Canceled' && (
          <Button variant="outline" onClick={() => setCancelOpen(true)}>
            {t('billing.cancelSubscription')}
          </Button>
        )}
      </div>
    ) : null
  }
/>
```

Add the import at the top:

```tsx
import { BillingHero } from '../components/BillingHero';
```

Remove the old standalone current-plan section and the old usage section. The payment history table block stays unchanged; the shared `Table` component already provides the `surface-glass` container.

- [ ] **Step 5: Wire into SubscriptionDetailPage**

Edit `boilerplateFE/src/features/billing/pages/SubscriptionDetailPage.tsx`. Replace **lines 76–165** (plan card block + usage block) with:

```tsx
<BillingHero
  subscription={subscription}
  usage={usage}
  isLoading={subLoading || usageLoading}
  eyebrow={tenant?.name ?? subscription?.tenantId}
  action={
    <Button
      onClick={() => {
        if (subscription) setSelectedPlanId(subscription.subscriptionPlanId);
        setPlanModalOpen(true);
      }}
      disabled={!subscription}
    >
      {t('billing.changePlan')}
    </Button>
  }
/>
```

Add the imports:

```tsx
import { BillingHero } from '../components/BillingHero';
import { useTenant } from '@/features/tenants/api';
```

Load the tenant explicitly near the existing subscription query. `TenantSubscriptionDto` does not carry `tenantName`, and the current page incorrectly uses `planName` as the breadcrumb label.

```tsx
const { data: tenant } = useTenant(tenantId ?? '');

const headerTitle = tenant
  ? t('billing.tenantSubscription', { tenantName: tenant.name })
  : t('billing.subscriptionDetail');
```

Update breadcrumbs to use `tenant?.name ?? subscription?.tenantId ?? t('common.loading')`. The tenant lookup is only for a better label; do not block the billing hero on it, because the page's real data dependency is still `useTenantSubscription(tenantId)`.

The change-plan dialog (lines 215–255) stays unchanged. The payment history table stays unchanged.

- [ ] **Step 6: Lint + build**

```bash
cd boilerplateFE && npm run lint && npm run build
```

Expected: both pass. If `BillingHero` exports surface a TS error about the optional `lockedMonthlyPrice`/`lockedAnnualPrice` being numbers vs strings, narrow types in the component or update the `TenantSubscription` interface to match the BE DTO exactly.

- [ ] **Step 7: Sync to test app + visual check**

```bash
cp boilerplateFE/src/features/billing/components/BillingHero.tsx _testJ4visual/_testJ4visual-FE/src/features/billing/components/
cp boilerplateFE/src/components/common/MetricCard.tsx _testJ4visual/_testJ4visual-FE/src/components/common/
cp boilerplateFE/src/features/billing/pages/BillingPage.tsx _testJ4visual/_testJ4visual-FE/src/features/billing/pages/
cp boilerplateFE/src/features/billing/pages/SubscriptionDetailPage.tsx _testJ4visual/_testJ4visual-FE/src/features/billing/pages/
cp boilerplateFE/src/i18n/locales/en/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/en/
cp boilerplateFE/src/i18n/locales/ar/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/ar/
cp boilerplateFE/src/i18n/locales/ku/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/ku/
```

Open `http://localhost:3100/billing` as `acme.admin@acme.com`. Verify:
- Plan card on the left (gradient-text plan name, status pill, price, period dates).
- 3-card usage strip on the right (Users / Storage / Webhooks with progress bars).
- "Change plan" button in the plan card's right slot.
- Cancel Subscription button still present below the hero.
- Payment history table still renders.

Then `http://localhost:3100/billing/subscriptions/<some-tenant-id>` as super-admin. Same hero, plus tenant name as eyebrow.

Switch to AR — hero mirrors, gradient-text figures stay Latin digits, progress bars fill start → end (logical, not right-to-left).

- [ ] **Step 8: Commit**

```bash
git add boilerplateFE/src/components/common/MetricCard.tsx \
        boilerplateFE/src/features/billing/components/BillingHero.tsx \
        boilerplateFE/src/features/billing/pages/BillingPage.tsx \
        boilerplateFE/src/features/billing/pages/SubscriptionDetailPage.tsx \
        boilerplateFE/src/i18n/locales/
git commit -m "feat(fe/phase4): tenant-side billing hero (BillingPage + SubscriptionDetail)

Both pages now open with a shared <BillingHero>: glass plan card on the
left (gradient-text plan name, status pill, locked price, period dates,
auto-renew flag) and a 3-card usage strip on the right (Users / Storage
/ Webhooks with progress bars). Auto-renew-off and over-90% usage are
visually flagged. SubscriptionDetailPage adds a tenant-name eyebrow.
Existing flows (Cancel, Change Plan, payment history) unchanged."
```

**REVIEW CHECKPOINT A.** Pause for human review of the tenant-side hero before continuing to the platform-admin pages.

---

## Checkpoint B — Platform-admin subscriptions hero

### Task 3: SubscriptionsPage status hero + table polish

**Files:**
- Modify: `boilerplateFE/src/types/billing.types.ts`
- Modify: `boilerplateFE/src/config/api.config.ts`
- Modify: `boilerplateFE/src/features/billing/api/billing.api.ts`
- Modify: `boilerplateFE/src/features/billing/api/billing.queries.ts`
- Modify: `boilerplateFE/src/lib/query/keys.ts`
- Create: `boilerplateFE/src/features/billing/components/SubscriptionStatusHero.tsx`
- Modify: `boilerplateFE/src/features/billing/pages/SubscriptionsPage.tsx`
- Modify: `boilerplateFE/src/i18n/locales/{en,ar,ku}/translation.json`

- [ ] **Step 1: Add the type**

Append to `boilerplateFE/src/types/billing.types.ts`:

```ts
export interface SubscriptionStatusCounts {
  trialing: number;
  active: number;
  pastDue: number;
  canceled: number;
  expired: number;
}
```

- [ ] **Step 2: Add the API endpoint constant**

Edit `boilerplateFE/src/config/api.config.ts`. Find the `BILLING:` block (or wherever subscription endpoints live). Add the new key alongside the existing subscription endpoint constant:

```ts
SUBSCRIPTION_STATUS_COUNTS: '/Billing/subscriptions/status-counts',
```

- [ ] **Step 3: Add the API method**

Edit `boilerplateFE/src/features/billing/api/billing.api.ts`. Inside the existing `billingApi` export, add:

```ts
getSubscriptionStatusCounts: () =>
  apiClient
    .get<{ data: SubscriptionStatusCounts }>(API_ENDPOINTS.BILLING.SUBSCRIPTION_STATUS_COUNTS)
    .then(r => r.data),
```

Add the import: `import type { SubscriptionStatusCounts } from '@/types/billing.types';` if not already present.

- [ ] **Step 4: Add the query-key entry**

Edit `boilerplateFE/src/lib/query/keys.ts`. Inside the `queryKeys.billing` object, add:

```ts
subscriptions: {
  all: ['billing', 'subscriptions'] as const,
  list: (params?: Record<string, unknown>) => ['billing', 'subscriptions', 'list', params] as const,
  statusCounts: () => ['billing', 'subscriptions', 'status-counts'] as const,
},
```

The `subscriptions` object already exists; add only `statusCounts`. Keep it under `subscriptions` so all platform subscription queries share one namespace.

- [ ] **Step 5: Add the React Query hook**

Edit `boilerplateFE/src/features/billing/api/billing.queries.ts`. Add:

```ts
export function useSubscriptionStatusCounts() {
  return useQuery({
    queryKey: queryKeys.billing.subscriptions.statusCounts(),
    queryFn: () => billingApi.getSubscriptionStatusCounts(),
    select: (r) => r.data,
    staleTime: 30_000,
  });
}
```

Also invalidate the counts after platform subscription mutations that can change the aggregate status distribution. In `useChangeTenantPlan`, add:

```ts
queryClient.invalidateQueries({ queryKey: queryKeys.billing.subscriptions.statusCounts() });
```

- [ ] **Step 6: Add translation keys**

EN — find the `billing.subscriptions:` block (or add it under `billing` if absent). Add:

```json
"statusHero": {
  "active": "Active",
  "activeEyebrow": "subscriptions",
  "trialing": "Trialing",
  "trialingEyebrow": "in trial",
  "pastDue": "Past-due",
  "pastDueEyebrow": "needs attention"
}
```

AR:
```json
"statusHero": {
  "active": "نشط",
  "activeEyebrow": "اشتراك",
  "trialing": "تجريبي",
  "trialingEyebrow": "في الفترة التجريبية",
  "pastDue": "مستحق",
  "pastDueEyebrow": "يحتاج إلى انتباه"
}
```

KU:
```json
"statusHero": {
  "active": "چالاک",
  "activeEyebrow": "بەشداربوون",
  "trialing": "تاقیکردنەوە",
  "trialingEyebrow": "لە ماوەی تاقیکردنەوە",
  "pastDue": "دواکەوتوو",
  "pastDueEyebrow": "پێویستی بە سەرنج هەیە"
}
```

Validate JSON.

- [ ] **Step 7: Create the hero component**

Create `boilerplateFE/src/features/billing/components/SubscriptionStatusHero.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/common';
import { useSubscriptionStatusCounts } from '../api/billing.queries';

export function SubscriptionStatusHero() {
  const { t } = useTranslation();
  const { data, isLoading } = useSubscriptionStatusCounts();

  if (isLoading || !data) {
    return (
      <div className="grid gap-4 sm:grid-cols-2 mb-6">
        <MetricCard label={t('billing.subscriptions.statusHero.active')} value="—" tone="active" eyebrow={t('billing.subscriptions.statusHero.activeEyebrow')} />
        <MetricCard label={t('billing.subscriptions.statusHero.trialing')} value="—" eyebrow={t('billing.subscriptions.statusHero.trialingEyebrow')} />
      </div>
    );
  }

  const showPastDue = data.pastDue > 0;
  const cols = showPastDue ? 'lg:grid-cols-3' : '';

  return (
    <div className={`grid gap-4 sm:grid-cols-2 ${cols} mb-6`}>
      <MetricCard
        label={t('billing.subscriptions.statusHero.active')}
        eyebrow={t('billing.subscriptions.statusHero.activeEyebrow')}
        value={data.active}
        emphasis={data.active > 0}
        tone="active"
      />
      <MetricCard
        label={t('billing.subscriptions.statusHero.trialing')}
        eyebrow={t('billing.subscriptions.statusHero.trialingEyebrow')}
        value={data.trialing}
      />
      {showPastDue && (
        <MetricCard
          label={t('billing.subscriptions.statusHero.pastDue')}
          eyebrow={t('billing.subscriptions.statusHero.pastDueEyebrow')}
          value={data.pastDue}
          tone="destructive"
        />
      )}
    </div>
  );
}
```

If the existing translations live under a flatter path (e.g., `billing.statusHero.*` instead of `billing.subscriptions.statusHero.*`), match what's already in place. The page-internal sub-tree namespace is the engineer's call as long as it's consistent across all three locales.

- [ ] **Step 8: Wire into SubscriptionsPage**

Edit `boilerplateFE/src/features/billing/pages/SubscriptionsPage.tsx`. Add the import:

```tsx
import { SubscriptionStatusHero } from '../components/SubscriptionStatusHero';
```

In JSX, immediately after `<PageHeader ... />` and **before** the search row (line ~67), insert:

```tsx
<SubscriptionStatusHero />
```

Keep the existing direct `<Table>` rendering. The shared `Table` component already wraps itself in `rounded-2xl surface-glass` and `CLAUDE.md` explicitly forbids adding an extra Card wrapper around tables. Preserve the existing `relative transition-opacity` parent so `isFetching` dimming still works.

The `<Pagination>` controls stay outside the table.

- [ ] **Step 9: Lint + build**

```bash
npm run lint && npm run build
```

Expected: both pass.

- [ ] **Step 10: Sync + visual check**

```bash
cp boilerplateFE/src/types/billing.types.ts _testJ4visual/_testJ4visual-FE/src/types/
cp boilerplateFE/src/config/api.config.ts _testJ4visual/_testJ4visual-FE/src/config/
cp boilerplateFE/src/features/billing/api/billing.api.ts _testJ4visual/_testJ4visual-FE/src/features/billing/api/
cp boilerplateFE/src/features/billing/api/billing.queries.ts _testJ4visual/_testJ4visual-FE/src/features/billing/api/
cp boilerplateFE/src/lib/query/keys.ts _testJ4visual/_testJ4visual-FE/src/lib/query/
cp boilerplateFE/src/features/billing/components/SubscriptionStatusHero.tsx _testJ4visual/_testJ4visual-FE/src/features/billing/components/
cp boilerplateFE/src/features/billing/pages/SubscriptionsPage.tsx _testJ4visual/_testJ4visual-FE/src/features/billing/pages/
cp boilerplateFE/src/i18n/locales/en/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/en/
cp boilerplateFE/src/i18n/locales/ar/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/ar/
cp boilerplateFE/src/i18n/locales/ku/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/ku/
```

Note: hero counts come from the new BE endpoint, so the test app's BE must be regenerated to expose the new route. Either regenerate the test app OR exercise the source BE: from `boilerplateBE/`, `dotnet run --project src/Starter.Api --launch-profile http`, then point the test app's `VITE_API_BASE_URL` at the source port, OR the engineer accepts the loading skeleton during the visual check.

Open `http://localhost:3100/billing/subscriptions` as super-admin. Verify:
- 2 or 3 metric cards above the search row depending on whether any subscription is past-due in the seed.
- Active card uses tinted treatment; Past-due card (when present) uses red tint.
- Search row + table + pagination still functional.
- There is no nested Card around the table; the shared `<Table>` remains the single glass surface.
- Switch to AR — hero mirrors, eyebrow text right-aligned, gradient digit treatment intact.

- [ ] **Step 11: Commit**

```bash
git add boilerplateFE/src/types/billing.types.ts \
        boilerplateFE/src/config/api.config.ts \
        boilerplateFE/src/features/billing/ \
        boilerplateFE/src/lib/query/keys.ts \
        boilerplateFE/src/i18n/locales/
git commit -m "feat(fe/phase4): SubscriptionsPage status hero + glass table

Three-card status strip (Active / Trialing / Past-due) above the
search row, sourced from the new GET /billing/subscriptions/status-counts
endpoint. Past-due collapses when zero. Existing search, table, plan
selector, and pagination unchanged — table now wrapped in glass surface
for cluster consistency."
```

**REVIEW CHECKPOINT B.** Pause for human review of the platform-admin hero before continuing to the polish pages.

---

## Checkpoint C — Polish pages (BillingPlansPage)

### Task 4: BillingPlansPage J4 token sweep

**Files:**
- Modify: `boilerplateFE/src/features/billing/pages/BillingPlansPage.tsx`

This is a list-polish task — no new heroes, no translations, no new types. Apply J4 vocabulary.

- [ ] **Step 1: Update the `PlanCard` component (lines 145–216)**

Edit `boilerplateFE/src/features/billing/pages/BillingPlansPage.tsx`. The `PlanCard` function (lines 145–216) wraps each plan. Three changes:

a) Change the outer `<Card>` to `variant="glass"`:

```tsx
<Card variant="glass" className={cn(/* existing classes if any */)}>
```

b) Wrap the price line in `gradient-text` and add `tabular-nums`. The current price block (~lines 165–178) renders monthly/annual prices or "Free". Replace the price `<span>` element so the figure (the actual number) carries:

```tsx
<span className="text-2xl font-semibold tabular-nums gradient-text">
  {plan.isFree ? t('billing.hero.free') : `${plan.monthlyPrice} ${plan.currency}`}
</span>
```

The existing label (`/ month`, `/ year`) stays in muted-foreground next to the figure. If the existing block already has a clear visual hierarchy with multiple spans, add `gradient-text` to the topmost figure span only — don't gradient-text the units.

c) Subscriber count line (~lines 180–183) gets `tabular-nums` and an eyebrow:

```tsx
<div>
  <div className="text-[10px] uppercase tracking-[0.12em] text-muted-foreground/70">
    {t('billing.subscribers')}
  </div>
  <div className="text-sm font-medium tabular-nums">{plan.subscriberCount}</div>
</div>
```

Use the existing `billing.subscribers` translation key (already in the locale file per the explorer report).

d) Feature highlight bullets (~lines 185–192) — replace bullet styling with copper checkmarks:

```tsx
{getFeatureHighlights(plan).map(h => (
  <li key={h} className="flex items-start gap-2 text-xs">
    <Check className="h-3.5 w-3.5 text-primary shrink-0 mt-0.5" />
    <span className="text-muted-foreground">{h}</span>
  </li>
))}
```

Add the import:

```tsx
import { Check } from 'lucide-react';
```

- [ ] **Step 2: Update the "Create Plan" button (PageHeader actions)**

Find the `<Button ... onClick={() => setCreateOpen(true)}>` for creating a plan (in the page header `actions` slot). Add the `btn-primary-gradient` class:

```tsx
<Button onClick={() => setCreateOpen(true)} className="btn-primary-gradient">
  {t('billing.createPlan')}
</Button>
```

- [ ] **Step 3: Sweep for hardcoded primary shades**

```bash
grep -nE "primary-[0-9]{2,3}|bg-primary/[0-9]{2}|text-primary-[0-9]{2,3}" boilerplateFE/src/features/billing/pages/BillingPlansPage.tsx
```

Expected: zero matches OR every match is a J4-token usage like `bg-primary/10` (semantic) or matches a Phase 0 allowed pattern. If raw shades like `text-primary-600` appear, replace with `text-primary` per the project rules in `CLAUDE.md`.

- [ ] **Step 4: Lint + build**

```bash
npm run lint && npm run build
```

Expected: both pass.

- [ ] **Step 5: Sync + visual check**

```bash
cp boilerplateFE/src/features/billing/pages/BillingPlansPage.tsx _testJ4visual/_testJ4visual-FE/src/features/billing/pages/
```

Open `http://localhost:3100/billing/plans` as super-admin. Verify:
- Plan cards are glass (translucent over the aurora canvas).
- Prices display gradient-text on the figure.
- Subscriber count is `tabular-nums` with a "subscribers" eyebrow above.
- Feature bullets show copper check icons.
- Create Plan button uses the gradient fill.
- Switch to AR — checkmarks stay on the start side via the logical `gap-2` flex layout.

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/features/billing/pages/BillingPlansPage.tsx
git commit -m "feat(fe/phase4): BillingPlansPage J4 polish

Glass cards, gradient-text price figure, tabular-nums subscriber count
with eyebrow, copper feature checkmarks. Create Plan button uses the
gradient fill. No structural change — the existing card grid is the
right pattern at this plan count."
```

**REVIEW CHECKPOINT C.** Pause for review.

---

## Checkpoint D — Polish pages (PricingPage)

### Task 5: PricingPage segmented toggle + popular halo + token polish

**Files:**
- Create: `boilerplateFE/src/features/billing/components/PricingIntervalToggle.tsx`
- Create: `boilerplateFE/src/features/billing/utils/popular-plan.ts`
- Create: `boilerplateFE/src/features/billing/utils/popular-plan.test.ts`
- Modify: `boilerplateFE/src/features/billing/pages/PricingPage.tsx`
- Modify: `boilerplateFE/src/i18n/locales/{en,ar,ku}/translation.json`

- [ ] **Step 1: Write the popular-plan unit tests**

Create `boilerplateFE/src/features/billing/utils/popular-plan.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { pickPopularPlan } from './popular-plan';
import type { SubscriptionPlan } from '@/types';

const make = (over: Partial<SubscriptionPlan>): SubscriptionPlan => ({
  id: over.id ?? 'plan',
  name: over.name ?? 'Plan',
  slug: over.slug ?? 'plan',
  description: '',
  translations: null,
  monthlyPrice: 10,
  annualPrice: 100,
  currency: 'USD',
  features: [],
  isFree: false,
  isActive: true,
  isPublic: true,
  displayOrder: 0,
  trialDays: 0,
  subscriberCount: 0,
  createdAt: '2026-04-29',
  modifiedAt: '2026-04-29',
  ...over,
});

describe('pickPopularPlan', () => {
  it('returns the plan with the highest subscriber count among public, non-free, active plans', () => {
    const plans = [
      make({ id: 'a', subscriberCount: 5 }),
      make({ id: 'b', subscriberCount: 50 }),
      make({ id: 'c', subscriberCount: 10 }),
    ];
    expect(pickPopularPlan(plans)?.id).toBe('b');
  });

  it('breaks ties by lowest displayOrder', () => {
    const plans = [
      make({ id: 'a', subscriberCount: 10, displayOrder: 2 }),
      make({ id: 'b', subscriberCount: 10, displayOrder: 1 }),
      make({ id: 'c', subscriberCount: 10, displayOrder: 3 }),
    ];
    expect(pickPopularPlan(plans)?.id).toBe('b');
  });

  it('skips free plans', () => {
    const plans = [
      make({ id: 'free', subscriberCount: 100, isFree: true }),
      make({ id: 'paid', subscriberCount: 5, isFree: false }),
    ];
    expect(pickPopularPlan(plans)?.id).toBe('paid');
  });

  it('skips inactive plans', () => {
    const plans = [
      make({ id: 'old', subscriberCount: 100, isActive: false }),
      make({ id: 'live', subscriberCount: 5 }),
    ];
    expect(pickPopularPlan(plans)?.id).toBe('live');
  });

  it('skips non-public plans', () => {
    const plans = [
      make({ id: 'private', subscriberCount: 100, isPublic: false }),
      make({ id: 'public', subscriberCount: 5 }),
    ];
    expect(pickPopularPlan(plans)?.id).toBe('public');
  });

  it('returns null when no eligible plans exist', () => {
    expect(pickPopularPlan([make({ isFree: true })])).toBeNull();
    expect(pickPopularPlan([])).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd boilerplateFE && npx vitest run src/features/billing/utils/popular-plan.test.ts
```

Expected: FAIL with "Cannot find module './popular-plan'".

- [ ] **Step 3: Implement the helper**

Create `boilerplateFE/src/features/billing/utils/popular-plan.ts`:

```ts
import type { SubscriptionPlan } from '@/types';

/**
 * Pick the plan to render with "popular" treatment on the pricing page.
 * Heuristic: highest subscriberCount among public, non-free, active plans;
 * ties broken by lowest displayOrder. Returns null when no eligible plan.
 */
export function pickPopularPlan(plans: SubscriptionPlan[]): SubscriptionPlan | null {
  const candidates = plans.filter(p => p.isPublic && p.isActive && !p.isFree);
  if (candidates.length === 0) return null;

  return candidates.reduce<SubscriptionPlan>((best, p) => {
    if (p.subscriberCount > best.subscriberCount) return p;
    if (p.subscriberCount === best.subscriberCount && p.displayOrder < best.displayOrder) return p;
    return best;
  }, candidates[0]);
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
npx vitest run src/features/billing/utils/popular-plan.test.ts
```

Expected: 6 tests PASS.

- [ ] **Step 5: Add translation keys**

EN — add only missing keys under the existing `billing` block. Keep PricingPage copy in the billing namespace because the current page already uses `billing.pricingTitle`, `billing.monthly`, `billing.annual`, `billing.savePercent`, `billing.getStarted`, and `billing.upgrade`.

```json
"pricing": {
  "toggleMonthly": "Monthly",
  "toggleAnnual": "Annual",
  "saveBadge": "save 20%",
  "popular": "Most popular",
  "trialDays_one": "{{count}}-day trial",
  "trialDays_other": "{{count}}-day trial",
  "currentPlan": "Current plan",
  "getStarted": "Get started",
  "upgrade": "Upgrade"
}
```

Existing flat keys can stay in place for current call sites; new PricingPage-only copy goes under `billing.pricing.*`.

AR:
```json
"pricing": {
  "toggleMonthly": "شهري",
  "toggleAnnual": "سنوي",
  "saveBadge": "وفّر 20٪",
  "popular": "الأكثر شيوعاً",
  "trialDays_one": "تجربة {{count}} يوم",
  "trialDays_other": "تجربة {{count}} يوماً",
  "currentPlan": "الخطة الحالية",
  "getStarted": "ابدأ الآن",
  "upgrade": "ترقية"
}
```

KU:
```json
"pricing": {
  "toggleMonthly": "مانگانە",
  "toggleAnnual": "ساڵانە",
  "saveBadge": "هەڵگرە 20٪",
  "popular": "زۆرترین بەکارهاتوو",
  "trialDays_one": "تاقیکردنەوەی {{count}} ڕۆژ",
  "trialDays_other": "تاقیکردنەوەی {{count}} ڕۆژ",
  "currentPlan": "پلانی ئێستا",
  "getStarted": "دەستپێبکە",
  "upgrade": "بەرزکردنەوە"
}
```

Validate JSON.

- [ ] **Step 6: Create the segmented toggle component**

Create `boilerplateFE/src/features/billing/components/PricingIntervalToggle.tsx`:

```tsx
import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/utils';

type BillingInterval = 'Monthly' | 'Annual';

export interface PricingIntervalToggleProps {
  value: BillingInterval;
  onChange: (next: BillingInterval) => void;
}

export function PricingIntervalToggle({ value, onChange }: PricingIntervalToggleProps) {
  const { t } = useTranslation();

  const segment = (key: BillingInterval, label: string, badge?: string) => (
    <button
      type="button"
      onClick={() => onChange(key)}
      aria-pressed={value === key}
      className={cn(
        'h-9 px-4 rounded-[10px] text-sm motion-safe:transition-colors motion-safe:duration-150 inline-flex items-center gap-2',
        value === key ? 'pill-active' : 'state-hover'
      )}
    >
      {label}
      {badge && (
        <span className="rounded-full bg-primary/15 text-primary px-1.5 py-0 text-[10px] font-mono">
          {badge}
        </span>
      )}
    </button>
  );

  return (
    <div className="inline-flex items-center gap-1 rounded-[12px] border border-border/40 bg-foreground/5 p-1">
      {segment('Monthly', t('billing.pricing.toggleMonthly'))}
      {segment('Annual', t('billing.pricing.toggleAnnual'), t('billing.pricing.saveBadge'))}
    </div>
  );
}
```

Keep `BillingInterval` local to this component; no shared type export is needed for a two-value UI control.

- [ ] **Step 7: Update PricingPage**

Edit `boilerplateFE/src/features/billing/pages/PricingPage.tsx`. Five changes:

a) **Replace the existing interval toggle** (lines 75–104) with:

```tsx
<PricingIntervalToggle value={interval} onChange={setInterval} />
```

Add the import:

```tsx
import { PricingIntervalToggle } from '../components/PricingIntervalToggle';
```

b) **Compute the popular plan** at the top of the page component (after `const plans = ... ?? []`):

```tsx
import { pickPopularPlan } from '../utils/popular-plan';

const popularPlan = useMemo(() => pickPopularPlan(plans), [plans]);
```

Update the React import:

```tsx
import { useMemo, useState } from 'react';
```

Add the shared card import because the current page uses plain `<div>` cards:

```tsx
import { Card, CardContent } from '@/components/ui/card';
```

Define the interval type once near the top of the file and use it for state + props:

```tsx
type BillingInterval = 'Monthly' | 'Annual';

const [interval, setInterval] = useState<BillingInterval>('Monthly');
```

c) **Remove bespoke blur blobs and token-sweep the top CTAs**

Delete the two absolute `blur-3xl` decorative divs near the top of the page. The existing `.gradient-hero` utility is already theme-driven via `--gradient-from` / `--gradient-to`; extra one-off blur blobs are not part of the shared J4 vocabulary.

In the nav/action area:
- Keep the sign-in button as `variant="outline"` with the existing translucent treatment if it remains legible.
- Change the primary register/get-started CTA to use the shared Button default styling or explicit `btn-primary-gradient glow-primary-md`; avoid `bg-white text-foreground` for primary CTAs.

d) **Update `PricingCard`** (lines 151–228) to accept `isPopular` and apply the visual treatment:

```tsx
interface PricingCardProps {
  plan: SubscriptionPlan;
  interval: BillingInterval;
  isCurrent: boolean;
  isPopular: boolean;
  price: number;
  features: string[];
  isLoggedIn: boolean;
}

function PricingCard({ plan, interval, isCurrent, isPopular, price, features, isLoggedIn }: PricingCardProps) {
  return (
    <div
      className={cn(
        'relative motion-safe:transition-transform motion-safe:duration-200',
        isPopular && 'lg:scale-105'
      )}
    >
      {isPopular && (
        <div className="absolute -top-3 left-1/2 -translate-x-1/2 z-10">
          <span className="rounded-full bg-primary px-3 py-0.5 text-[10px] uppercase tracking-[0.12em] font-semibold text-primary-foreground">
            {t('billing.pricing.popular')}
          </span>
        </div>
      )}
      <Card variant="glass" className={cn(isPopular && 'glow-primary-md border-primary/30')}>
        <CardContent className="p-6">
          {/* keep existing card content, applying the token changes below */}
        </CardContent>
      </Card>
    </div>
  );
}
```

Inside the card content:
- Wrap the price figure with `tabular-nums gradient-text`.
- Replace plain bullet markers in the feature list with `<Check className="h-4 w-4 text-primary shrink-0 mt-0.5" />` (matching the BillingPlansPage treatment from Task 4).
- Trial-days badge becomes outline pill: `border border-primary/40 text-primary bg-primary/5 rounded-full px-2 py-0.5 text-xs`, with text from `t('billing.pricing.trialDays', { count: plan.trialDays })`.
- CTA button uses the shared Button variants: default (`btn-primary-gradient` already comes from the component) for the popular/paid plan, `outline` for the free tier, `ghost` (disabled) for the current plan.

In the parent map, pass `isPopular={plan.id === popularPlan?.id}`.

e) **Sweep raw white-on-gradient card styling**

Replace plan-card `bg-white/10`, `text-white`, `hover:bg-white/15`, and `backdrop-blur-sm` styling with shared `Card variant="glass"` plus semantic text tokens (`text-foreground`, `text-muted-foreground`, `text-primary`). The PricingPage shell may keep `.gradient-hero`; card internals should use theme-aware surface/card tokens.

- [ ] **Step 8: Lint + build**

```bash
npm run lint && npm run build
```

Expected: both pass.

- [ ] **Step 9: Sync + visual check**

```bash
cp -r boilerplateFE/src/features/billing/components/ _testJ4visual/_testJ4visual-FE/src/features/billing/components/
cp -r boilerplateFE/src/features/billing/utils/ _testJ4visual/_testJ4visual-FE/src/features/billing/utils/
cp boilerplateFE/src/features/billing/pages/PricingPage.tsx _testJ4visual/_testJ4visual-FE/src/features/billing/pages/
cp boilerplateFE/src/i18n/locales/en/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/en/
cp boilerplateFE/src/i18n/locales/ar/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/ar/
cp boilerplateFE/src/i18n/locales/ku/translation.json _testJ4visual/_testJ4visual-FE/src/i18n/locales/ku/
```

Open `http://localhost:3100/pricing` (no login required — public route). Verify:
- Segmented toggle (Monthly / Annual with "save 20%" badge) at the top of the cards.
- Active segment uses `pill-active` treatment.
- The popular plan card has a copper "Most popular" pill above it, scales to 105% on lg+, and has a `glow-primary-md` halo.
- Feature lists show copper checkmarks.
- Price figures are gradient-text and tabular-nums.
- Trial-days appears as an outline pill if any plan has `trialDays > 0`.
- The top primary CTA uses the shared gradient button treatment, not a bespoke white button.
- No decorative `blur-3xl` blobs remain.
- Switch to AR — toggle direction flips, popular pill stays centered, halo and scale unchanged.

- [ ] **Step 10: Commit**

```bash
git add boilerplateFE/src/features/billing/components/PricingIntervalToggle.tsx \
        boilerplateFE/src/features/billing/utils/ \
        boilerplateFE/src/features/billing/pages/PricingPage.tsx \
        boilerplateFE/src/i18n/locales/
git commit -m "feat(fe/phase4): PricingPage J4 polish + segmented toggle + popular halo

Replaces the inline Monthly/Annual buttons with a pill-active segmented
control. The 'most popular' plan (highest subscriberCount among public,
non-free, active plans; ties by displayOrder) gets a copper halo,
glow-primary-md, and a scale-105 transform on lg+. Feature lists use
copper checkmarks; prices are gradient-text. Public route — works
without login. The popular-plan heuristic is unit-tested."
```

**REVIEW CHECKPOINT D.** Pause for review.

---

## Checkpoint E — Final integration + PR prep

### Task 6: Cross-page verification + PR prep

- [ ] **Step 1: Full lint + build**

```bash
cd boilerplateFE && npm run lint && npm run build
cd ../boilerplateBE && dotnet build src/Starter.Api
```

Expected: all three pass.

- [ ] **Step 2: Live RTL pass on every Phase 4 page**

In the test app browser, switch language to AR. Visit:
- `/billing` — hero mirrors, plan card on the start-side, usage strip on the end-side, progress bars fill start→end.
- `/billing/subscriptions` — status cards mirror, table search/filter row reads right-to-left.
- `/billing/subscriptions/<tenant-id>` — same shape as `/billing` plus tenant eyebrow.
- `/billing/plans` — plan cards mirror, copper checkmarks on the start side of feature bullets.
- `/pricing` — segmented toggle direction, popular pill stays centered above its card, scale + halo unchanged.

Switch back to EN. No structural differences except direction.

- [ ] **Step 3: Permission matrix sanity check**

Per the spec §6:
- **Super-admin** (`superadmin@testj4visual.com`): `/billing` may redirect (no own subscription); `/billing/plans`, `/billing/subscriptions`, `/billing/subscriptions/<tenant-id>` all visible; `/pricing` visible.
- **Tenant admin** (`acme.admin@acme.com`): `/billing` shows their plan + usage hero; `/pricing` visible; the three platform-admin pages 403 / redirect to dashboard.
- **Regular user** (`acme.alice@acme.com`): only `/pricing` visible (public route).
- **Unauthenticated** (logged out): `/pricing` accessible; clicking CTA routes to `/register-tenant` or `/login`.

- [ ] **Step 4: Phase 1/2/3 spot-check (regression)**

Visit each: `/users`, `/roles`, `/tenants`, `/profile`, `/audit-logs`, `/feature-flags`, `/api-keys`, `/settings`, `/files`, `/reports`, `/notifications`. Confirm no visual changes.

- [ ] **Step 5: Final commit (only if anything fixed up during QA)**

If QA surfaced fixes, commit them with a `fix(fe/phase4): ...` message. Otherwise this step is a no-op.

- [ ] **Step 6: Push + open PR**

```bash
git push -u origin <current-branch>
```

PR title: `feat(fe): Phase 4 redesign — Billing cluster (5 pages)`. Body follows the Phase 2/3 PR shape: summary section listing the five pages, BE addition flagged, deferred-list (MRR hero, comparison table, BillingPlansPage hero), test plan checklist (`npm run build` clean, `npm run lint` clean, BE `dotnet build` clean, RTL pass on all five pages, permission matrix verified, Phase 1/2/3 regression check). Link the spec + this plan in the body.

---

## Self-review

- **Spec coverage:**
  - BillingPage hero → Task 2. ✅
  - SubscriptionDetailPage hero → Task 2 (shares `BillingHero`). ✅
  - SubscriptionsPage status hero + glass table → Task 3. ✅
  - BillingPlansPage polish → Task 4. ✅
  - PricingPage polish + segmented toggle + popular halo → Task 5. ✅
  - BE endpoint `/billing/subscriptions/status-counts` → Task 1. ✅
  - All translations EN+AR+KU inline → Tasks 2, 3, 5 (Task 4 reuses existing keys, no new strings). ✅
  - Permission matrix verification → Task 6. ✅
  - Phase 1/2/3 regression check → Task 6. ✅

- **Placeholders:** scanned. Conditional notes now point to concrete existing code paths, and the shared component contract is updated before use. No TBD / TODO / "fill in details".

- **Type consistency:** `SubscriptionStatusCounts` (FE) ↔ `SubscriptionStatusCountsDto` (BE) — same fields, same names. `BillingHero` props match what `BillingPage` and `SubscriptionDetailPage` actually load. `pickPopularPlan` signature matches the call site in `PricingPage`.

- **All three spec §8 open questions resolved up front** with concrete answers and source-of-truth references.
