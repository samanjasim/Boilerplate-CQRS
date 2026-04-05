# Analytics Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enhance the existing dashboard with feature-flag-aware analytics — dynamic stat cards with trend indicators, time-series charts via Recharts, and activity breakdowns, all scoped by tenant with Redis caching.

**Architecture:** Single `GET /api/v1/dashboard/analytics?period=30d` endpoint computes metrics only for features enabled in the tenant's plan. Backend queries existing tables with GROUP BY aggregation, caches in Redis for 15 min. Frontend renders dynamic stat cards and Recharts charts based on `enabledSections` from the response.

**Tech Stack:** .NET 10, EF Core, Redis, MediatR, Recharts, React 19, TanStack Query, shadcn/ui, Tailwind CSS

**Spec:** `docs/superpowers/specs/2026-04-05-analytics-dashboard-design.md`

---

## File Structure

### Backend — New Files

```
boilerplateBE/src/Starter.Application/Features/Dashboard/
├── DTOs/
│   └── DashboardAnalyticsDto.cs
└── Queries/
    └── GetDashboardAnalytics/
        ├── GetDashboardAnalyticsQuery.cs
        └── GetDashboardAnalyticsQueryHandler.cs

boilerplateBE/src/Starter.Api/Controllers/
└── DashboardController.cs
```

### Frontend — New Files

```
boilerplateFE/src/types/dashboard.types.ts
boilerplateFE/src/features/dashboard/
├── api/
│   ├── dashboard.api.ts
│   ├── dashboard.queries.ts
│   └── index.ts
└── components/
    ├── StatCard.tsx
    ├── AnalyticsSummaryCards.tsx
    ├── AnalyticsCharts.tsx
    ├── PeriodSelector.tsx
    ├── UserGrowthChart.tsx
    ├── LoginActivityChart.tsx
    ├── ActivityBreakdownChart.tsx
    ├── StorageGrowthChart.tsx
    └── TenantGrowthChart.tsx
```

### Frontend — Modified Files

```
boilerplateFE/src/features/dashboard/pages/DashboardPage.tsx   (replace with analytics layout)
boilerplateFE/src/config/api.config.ts                          (+dashboard endpoint)
boilerplateFE/src/lib/query/keys.ts                             (+dashboard query keys)
boilerplateFE/src/i18n/locales/{en,ar,ku}/translation.json      (+analytics i18n keys)
boilerplateFE/src/index.css                                     (+chart CSS variables)
boilerplateFE/package.json                                      (+recharts dependency)
```

---

## Task 1: Backend — DTO + Query + Handler

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Features/Dashboard/DTOs/DashboardAnalyticsDto.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Dashboard/Queries/GetDashboardAnalytics/GetDashboardAnalyticsQuery.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Dashboard/Queries/GetDashboardAnalytics/GetDashboardAnalyticsQueryHandler.cs`

- [ ] **Step 1:** Create `DashboardAnalyticsDto.cs`

```csharp
namespace Starter.Application.Features.Dashboard.DTOs;

public sealed record DashboardAnalyticsDto(
    string Period,
    string[] EnabledSections,
    Dictionary<string, SummaryMetric> Summary,
    Dictionary<string, List<TimeSeriesPoint>> Charts);

public sealed record SummaryMetric(
    decimal Current,
    decimal Previous,
    decimal? Trend);  // null if previous is 0 and current is 0

public sealed record TimeSeriesPoint(
    string Date,
    decimal Value);
```

- [ ] **Step 2:** Create `GetDashboardAnalyticsQuery.cs`

```csharp
using MediatR;
using Starter.Application.Features.Dashboard.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Dashboard.Queries.GetDashboardAnalytics;

public sealed record GetDashboardAnalyticsQuery(string Period = "30d") : IRequest<Result<DashboardAnalyticsDto>>;
```

- [ ] **Step 3:** Create `GetDashboardAnalyticsQueryHandler.cs`

This is the core handler. Inject: `IApplicationDbContext`, `ICurrentUserService`, `IFeatureFlagService`, `ICacheService`.

**Logic:**
1. Parse period string (7d/30d/90d/12m) to `periodStart` and `prevStart` DateTimes
2. Determine tenant context: `currentUser.TenantId` (null = platform admin)
3. Build cache key: `analytics:{tenantId|platform}:{period}`
4. Check Redis cache via `ICacheService.GetAsync<DashboardAnalyticsDto>` — return if hit
5. Determine enabled sections:
   - `users`, `loginActivity`, `activityBreakdown` → always enabled
   - `storage` → `files.max_storage_mb > 0`
   - `apiKeys` → `api_keys.enabled == true`
   - `reports` → `reports.enabled == true`
   - `webhooks` → `webhooks.enabled == true`
   - `imports` → `imports.enabled == true`
   - `revenue`, `tenants` → only if platform admin (tenantId is null)
6. For each enabled section, compute summary metrics (current vs previous period counts/sums)
7. For chart sections, compute time-series data grouped by date
8. Calculate trends: `((current - previous) / previous) * 100`. If previous == 0: trend = current > 0 ? 100 : null
9. Cache result with 15-min TTL
10. Return `DashboardAnalyticsDto`

**Query scoping:**
- Platform admin: use `IgnoreQueryFilters()` on all queries
- Tenant user: standard EF query filters apply automatically

**Date grouping:**
- Period 7d/30d/90d: GROUP BY `DATE(CreatedAt)` (daily points)
- Period 12m: GROUP BY `DATE_TRUNC('month', CreatedAt)` (monthly points)

**Section-specific queries:**
- `users`: `context.Users.CountAsync()` for current count, `WHERE CreatedAt >= periodStart` for new users, GROUP BY date for chart
- `loginActivity`: `context.LoginHistories` (or similar) GROUP BY date
- `activityBreakdown`: `context.AuditLogs` GROUP BY Action + GROUP BY EntityType (top 5)
- `storage`: `context.FileMetadata.SumAsync(f => f.Size)` cumulative, GROUP BY date for growth
- `apiKeys`: `context.ApiKeys.CountAsync(k => !k.IsRevoked)`
- `webhooks`: `context.WebhookEndpoints.CountAsync(e => e.IsActive)` + delivery counts
- `revenue`: `context.PaymentRecords.Where(p => p.Status == Completed).SumAsync(p => p.Amount)`
- `tenants`: `context.Tenants.CountAsync()`

Read existing query handlers (GetUsageQueryHandler, GetWebhookAdminStatsQueryHandler) for patterns.

**IMPORTANT:** Check what entity name is used for login history — it might be `LoginHistory`, `Session`, or tracked in `AuditLogs`. Search for login tracking entities before implementing.

- [ ] **Step 4:** Verify build, commit

```
feat(dashboard): add analytics query handler with feature-flag-aware metrics
```

---

## Task 2: Backend — DashboardController

**Files:**
- Create: `boilerplateBE/src/Starter.Api/Controllers/DashboardController.cs`

- [ ] **Step 1:** Create controller

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.Dashboard.Queries.GetDashboardAnalytics;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

public sealed class DashboardController(ISender mediator) : BaseApiController(mediator)
{
    [HttpGet("analytics")]
    [Authorize(Policy = Permissions.System.ViewDashboard)]
    public async Task<IActionResult> GetAnalytics(
        [FromQuery] string period = "30d",
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetDashboardAnalyticsQuery(period), ct);
        return HandleResult(result);
    }
}
```

- [ ] **Step 2:** Verify build, commit

```
feat(api): add DashboardController with analytics endpoint
```

---

## Task 3: Frontend — Install Recharts + Types + API

**Files:**
- Modify: `boilerplateFE/package.json` (install recharts)
- Create: `boilerplateFE/src/types/dashboard.types.ts`
- Modify: `boilerplateFE/src/config/api.config.ts`
- Modify: `boilerplateFE/src/lib/query/keys.ts`
- Create: `boilerplateFE/src/features/dashboard/api/dashboard.api.ts`
- Create: `boilerplateFE/src/features/dashboard/api/dashboard.queries.ts`
- Create: `boilerplateFE/src/features/dashboard/api/index.ts`

- [ ] **Step 1:** Install recharts

```bash
cd boilerplateFE && npm install recharts
```

- [ ] **Step 2:** Create `dashboard.types.ts`

```typescript
export interface DashboardAnalytics {
  period: string;
  enabledSections: string[];
  summary: Record<string, SummaryMetric>;
  charts: Record<string, TimeSeriesPoint[]>;
}

export interface SummaryMetric {
  current: number;
  previous: number;
  trend: number | null;
}

export interface TimeSeriesPoint {
  date: string;
  value: number;
}
```

Export from `types/index.ts`.

- [ ] **Step 3:** Add API endpoint and query keys

API config:
```typescript
DASHBOARD: {
  ANALYTICS: '/Dashboard/analytics',
},
```

Query keys:
```typescript
dashboard: {
  all: ['dashboard'] as const,
  analytics: (period?: string) => ['dashboard', 'analytics', period] as const,
},
```

- [ ] **Step 4:** Create API module and hook

```typescript
// dashboard.api.ts
export const dashboardApi = {
  getAnalytics: (period: string = '30d') =>
    apiClient.get(API_ENDPOINTS.DASHBOARD.ANALYTICS, { params: { period } }).then(r => r.data),
};

// dashboard.queries.ts
export function useDashboardAnalytics(period: string = '30d') {
  return useQuery({
    queryKey: queryKeys.dashboard.analytics(period),
    queryFn: () => dashboardApi.getAnalytics(period),
  });
}
```

- [ ] **Step 5:** Verify build, commit

```
feat(frontend): install recharts, add dashboard types, API, and query hook
```

---

## Task 4: Frontend — Chart CSS Variables + i18n

**Files:**
- Modify: `boilerplateFE/src/index.css`
- Modify: `boilerplateFE/src/i18n/locales/{en,ar,ku}/translation.json`

- [ ] **Step 1:** Add chart CSS variables to `index.css`

In the `:root` or `@theme` block, add:
```css
--chart-1: var(--primary);
--chart-2: oklch(0.6 0.15 250);
--chart-3: oklch(0.65 0.18 150);
--chart-4: oklch(0.75 0.15 85);
--chart-5: oklch(0.6 0.2 25);
```

Check the existing CSS variable format (oklch vs hsl) and match it.

- [ ] **Step 2:** Add i18n keys to all 3 locales

English `dashboard` section additions:
```json
"analytics": "Analytics",
"period": "Period",
"7d": "Last 7 days",
"30d": "Last 30 days",
"90d": "Last 90 days",
"12m": "Last 12 months",
"users": "Users",
"storage": "Storage",
"apiKeys": "API Keys",
"webhooks": "Webhooks",
"reports": "Reports",
"imports": "Imports",
"revenue": "Revenue",
"tenants": "Tenants",
"trend": "vs previous period",
"noData": "No data available",
"userGrowth": "User Growth",
"loginActivity": "Login Activity",
"activityBreakdown": "Activity Breakdown",
"storageGrowth": "Storage Growth",
"tenantGrowth": "Tenant Growth",
"topEntities": "Top Entities",
"newUsers": "New Users",
"logins": "Logins"
```

Add Arabic and Kurdish translations.

- [ ] **Step 3:** Verify build, commit

```
feat(frontend): add chart CSS variables and analytics i18n translations
```

---

## Task 5: Frontend — StatCard + PeriodSelector + AnalyticsSummaryCards

**Files:**
- Create: `boilerplateFE/src/features/dashboard/components/StatCard.tsx`
- Create: `boilerplateFE/src/features/dashboard/components/PeriodSelector.tsx`
- Create: `boilerplateFE/src/features/dashboard/components/AnalyticsSummaryCards.tsx`

- [ ] **Step 1:** Create `StatCard.tsx`

A single analytics stat card showing:
- Icon (lucide-react)
- Label (translated)
- Value (formatted: number with commas, bytes as GB/MB, currency as $X,XXX.XX)
- Trend arrow + percentage: green ↑ for positive, red ↓ for negative, gray — for zero/null
- Subtitle: "vs previous {period}"

Use shadcn `Card` component. Style with theme semantic tokens (not hardcoded colors).

Props: `{ icon: LucideIcon, label: string, value: number, format: 'number' | 'bytes' | 'currency', trend: number | null, period: string }`

Format helpers:
- `number`: `Intl.NumberFormat` with commas
- `bytes`: convert to human-readable (B/KB/MB/GB/TB)
- `currency`: `Intl.NumberFormat` with USD style

- [ ] **Step 2:** Create `PeriodSelector.tsx`

Simple dropdown using shadcn `Select` component. Options: 7d, 30d, 90d, 12m. Labels from i18n. Calls `onPeriodChange(period)` callback.

- [ ] **Step 3:** Create `AnalyticsSummaryCards.tsx`

Receives `analytics: DashboardAnalytics` and `period: string` props. Maps enabled sections to StatCard configs:

```typescript
const cardConfigs = [
  { section: 'users', label: 'dashboard.users', icon: Users, format: 'number' as const },
  { section: 'storage', label: 'dashboard.storage', icon: HardDrive, format: 'bytes' as const },
  { section: 'apiKeys', label: 'dashboard.apiKeys', icon: KeyRound, format: 'number' as const },
  { section: 'webhooks', label: 'dashboard.webhooks', icon: Webhook, format: 'number' as const },
  { section: 'reports', label: 'dashboard.reports', icon: FileText, format: 'number' as const },
  { section: 'imports', label: 'dashboard.imports', icon: ArrowLeftRight, format: 'number' as const },
  { section: 'revenue', label: 'dashboard.revenue', icon: DollarSign, format: 'currency' as const },
  { section: 'tenants', label: 'dashboard.tenants', icon: Building, format: 'number' as const },
];

const visibleCards = cardConfigs.filter(c => analytics.enabledSections.includes(c.section));
```

Renders in responsive grid: `grid-cols-1 sm:grid-cols-2 lg:grid-cols-4`.

- [ ] **Step 4:** Verify build, commit

```
feat(frontend): add StatCard, PeriodSelector, and AnalyticsSummaryCards components
```

---

## Task 6: Frontend — Chart Components

**Files:**
- Create: `boilerplateFE/src/features/dashboard/components/UserGrowthChart.tsx`
- Create: `boilerplateFE/src/features/dashboard/components/LoginActivityChart.tsx`
- Create: `boilerplateFE/src/features/dashboard/components/ActivityBreakdownChart.tsx`
- Create: `boilerplateFE/src/features/dashboard/components/StorageGrowthChart.tsx`
- Create: `boilerplateFE/src/features/dashboard/components/TenantGrowthChart.tsx`
- Create: `boilerplateFE/src/features/dashboard/components/AnalyticsCharts.tsx`

- [ ] **Step 1:** Create individual chart components

Each chart is a small, focused component wrapping Recharts:

**UserGrowthChart** — `AreaChart` with gradient fill. X-axis: dates, Y-axis: new user count.
**LoginActivityChart** — `BarChart`. X-axis: dates, Y-axis: login count.
**ActivityBreakdownChart** — `PieChart` (donut). Segments: Created, Updated, Deleted, etc. Include a legend. Alongside, render a "Top Entities" list from the `topEntities` chart data.
**StorageGrowthChart** — `AreaChart` with gradient. Y-axis formatted as bytes (GB/MB).
**TenantGrowthChart** — `AreaChart`. Platform admin only.

All charts:
- Wrapped in `ResponsiveContainer` for auto-resize
- Use `Card` + heading for container
- Read chart colors from CSS variables via `getComputedStyle`
- Handle empty data: show "No data available" message
- Format dates using `date-fns` (already installed)
- Tooltips with formatted values

**Common pattern:**
```tsx
import { ResponsiveContainer, AreaChart, Area, XAxis, YAxis, Tooltip, CartesianGrid } from 'recharts';

function UserGrowthChart({ data }: { data: TimeSeriesPoint[] }) {
  const { t } = useTranslation();
  if (!data?.length) return <EmptyChartState />;

  return (
    <Card>
      <CardContent className="pt-6">
        <h3 className="text-sm font-medium text-muted-foreground mb-4">{t('dashboard.userGrowth')}</h3>
        <ResponsiveContainer width="100%" height={250}>
          <AreaChart data={data}>
            <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
            <XAxis dataKey="date" tick={{ fontSize: 12 }} tickFormatter={formatDate} />
            <YAxis tick={{ fontSize: 12 }} />
            <Tooltip formatter={...} labelFormatter={formatDate} />
            <Area type="monotone" dataKey="value" stroke="hsl(var(--chart-1))" fill="hsl(var(--chart-1) / 0.2)" />
          </AreaChart>
        </ResponsiveContainer>
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 2:** Create `AnalyticsCharts.tsx`

Container that renders only the charts for enabled sections:
```typescript
const chartConfigs = [
  { section: 'users', key: 'userGrowth', Component: UserGrowthChart },
  { section: 'loginActivity', key: 'loginActivity', Component: LoginActivityChart },
  { section: 'activityBreakdown', key: 'activityBreakdown', Component: ActivityBreakdownChart },
  { section: 'storage', key: 'storageGrowth', Component: StorageGrowthChart },
  { section: 'tenants', key: 'tenantGrowth', Component: TenantGrowthChart },
];

const visibleCharts = chartConfigs.filter(c => enabledSections.includes(c.section));
```

Renders in responsive grid: `grid-cols-1 lg:grid-cols-2`. ActivityBreakdown may span full width.

- [ ] **Step 3:** Verify build, commit

```
feat(frontend): add Recharts chart components (user growth, login, activity, storage, tenant)
```

---

## Task 7: Frontend — Enhanced DashboardPage

**Files:**
- Modify: `boilerplateFE/src/features/dashboard/pages/DashboardPage.tsx`

- [ ] **Step 1:** Replace the existing DashboardPage

Read the current `DashboardPage.tsx` fully first. The new page:

1. **Keep** the welcome hero section (greeting + illustration)
2. **Add** PeriodSelector in the header area (top right)
3. **Replace** static 4 stat cards with `AnalyticsSummaryCards` driven by `useDashboardAnalytics(period)`
4. **Add** `AnalyticsCharts` section below stat cards
5. **Keep** Recent Activity feed and Recent Users sections at the bottom
6. **Handle** loading state: skeleton placeholders for cards and charts while fetching
7. **Handle** error state: show existing dashboard content as fallback

```typescript
const [period, setPeriod] = useState('30d');
const { data: analytics, isLoading } = useDashboardAnalytics(period);

return (
  <div className="space-y-6">
    {/* Welcome + Period Selector */}
    <div className="flex items-center justify-between">
      <WelcomeHero />
      <PeriodSelector value={period} onChange={setPeriod} />
    </div>

    {/* Analytics Summary Cards */}
    {isLoading ? <StatCardSkeletons /> : analytics && (
      <AnalyticsSummaryCards analytics={analytics} period={period} />
    )}

    {/* Charts */}
    {isLoading ? <ChartSkeletons /> : analytics && (
      <AnalyticsCharts analytics={analytics} />
    )}

    {/* Existing sections */}
    <div className="grid gap-6 lg:grid-cols-2">
      <RecentActivitySection />
      <RecentUsersSection />
    </div>

    <QuickOverviewSection />
  </div>
);
```

Extract existing sections (recent activity, recent users, quick overview) into their own small components if they aren't already, to keep DashboardPage clean.

- [ ] **Step 2:** Verify build, commit

```
feat(dashboard): enhance DashboardPage with analytics cards, charts, and period selector
```

---

## Task 8: Build Verification

- [ ] **Step 1:** Full backend build: `dotnet build` — 0 errors
- [ ] **Step 2:** Full frontend build: `npm run build` — 0 errors
- [ ] **Step 3:** Commit if any remaining changes

---

## Execution Notes

- **Do NOT create EF migrations** — no new tables in this feature
- **Do NOT mention Claude or Anthropic** in commit messages. No Co-Authored-By tags
- Tasks 1-2 are backend, Tasks 3-7 are frontend, Task 8 is verification
- Task 1 (query handler) is the most complex — requires many conditional DB queries
- The handler should use helper methods per section to keep code organized (e.g., `ComputeUserMetrics()`, `ComputeStorageMetrics()`)
- Recharts components should use `ResponsiveContainer` for auto-sizing
- Chart colors should use CSS variables, not hardcoded hex values
- The existing DashboardPage sections (recent activity, recent users, quick overview) should be preserved below the new analytics sections
- Check the exact entity names for login history before implementing — might be `LoginHistory`, `Session`, or entries in AuditLogs
