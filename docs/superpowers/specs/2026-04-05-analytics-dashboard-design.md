# Analytics Dashboard — Design Specification

## Overview

Enhance the existing `/dashboard` page with rich, feature-flag-aware analytics. Stat cards with trend indicators, time-series charts (Recharts), and activity breakdowns. Data computed on-the-fly from existing tables with Redis caching (15-min TTL). Scoped by tenant for tenant admins, platform-wide for SuperAdmin. Only metrics for enabled features are computed and displayed — a Free plan tenant sees fewer cards than an Enterprise tenant.

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Audience | Both platform + tenant admins | Same page, data scoped by tenantId. Follows existing pattern |
| Time ranges | Preset: 7d, 30d, 90d, 12m | Covers 95% of needs. Dropdown selector. Aggregated by day (7/30/90d) or month (12m) |
| Data strategy | On-the-fly with Redis caching | No new tables. Query existing entities with GROUP BY date. Cache 15 min |
| Chart library | Recharts | React-native, declarative, TypeScript, lightweight, easy theming via CSS variables |
| Page placement | Enhance existing /dashboard | Replace basic 4-card dashboard with rich analytics. Single URL |
| Feature gating | Feature-flag-aware sections | Only compute/display metrics for features enabled in tenant's plan |

## Architecture

### Single Backend Endpoint

```
GET /api/v1/dashboard/analytics?period=7d|30d|90d|12m
```

**Authorization:** `System.ViewDashboard` (all roles have this).

**Handler logic:**
1. Resolve current tenant (from JWT or null for platform admin)
2. Determine enabled features via `IFeatureFlagService`:
   - `users` — always enabled (every plan has users)
   - `storage` — enabled if `files.max_storage_mb` > 0
   - `apiKeys` — enabled if `api_keys.enabled` == true
   - `reports` — enabled if `reports.enabled` == true
   - `webhooks` — enabled if `webhooks.enabled` == true
   - `imports` — enabled if `imports.enabled` == true
   - `billing` — platform admin only (no flag check)
   - `tenants` — platform admin only
3. For each enabled section, compute current value + previous period value (for trend %)
4. For each enabled chart, compute time-series data points
5. Cache entire response in Redis: key `analytics:{tenantId|platform}:{period}`, TTL 15 min
6. Return response with `enabledSections` array

### Response Shape

```json
{
  "period": "30d",
  "enabledSections": ["users", "storage", "apiKeys", "loginActivity"],
  "summary": {
    "users": { "current": 245, "previous": 218, "trend": 12.4 },
    "storage": { "currentBytes": 5368709120, "previousBytes": 4961034240, "trend": 8.2 },
    "apiKeys": { "current": 15, "previous": 16, "trend": -6.3 },
    "webhooks": { "current": 8, "previous": 5, "trend": 60.0 },
    "revenue": { "current": 2970.00, "previous": 2580.00, "trend": 15.1 },
    "tenants": { "current": 12, "previous": 10, "trend": 20.0 }
  },
  "charts": {
    "userGrowth": [
      { "date": "2026-03-01", "count": 5 },
      { "date": "2026-03-02", "count": 8 }
    ],
    "loginActivity": [
      { "date": "2026-03-01", "count": 32 },
      { "date": "2026-03-02", "count": 28 }
    ],
    "storageGrowth": [
      { "date": "2026-03-01", "bytes": 1073741824 },
      { "date": "2026-03-02", "bytes": 1200000000 }
    ],
    "activityBreakdown": [
      { "action": "Created", "count": 120 },
      { "action": "Updated", "count": 85 },
      { "action": "Deleted", "count": 12 }
    ],
    "topEntities": [
      { "entityType": "User", "count": 95 },
      { "entityType": "File", "count": 67 },
      { "entityType": "Role", "count": 23 }
    ]
  }
}
```

### Section → Feature Flag Mapping

| Section | Condition | Summary Metric | Chart |
|---------|-----------|---------------|-------|
| `users` | Always enabled | Total users + trend | User growth (area) |
| `loginActivity` | Always enabled | — | Login activity (bar) |
| `activityBreakdown` | Always enabled | — | Activity breakdown (donut) + top entities |
| `storage` | `files.max_storage_mb > 0` | Total storage + trend | Storage growth (area) |
| `apiKeys` | `api_keys.enabled == true` | Active API keys + trend | — |
| `reports` | `reports.enabled == true` | Reports generated + trend | — |
| `webhooks` | `webhooks.enabled == true` | Active endpoints + deliveries trend | — |
| `imports` | `imports.enabled == true` | Import jobs + trend | — |
| `revenue` | Platform admin only | Total revenue + trend | — |
| `tenants` | Platform admin only | Total tenants + trend | Tenant growth (area) |

### Trend Calculation

```
trend% = ((current - previous) / previous) * 100

Where:
  period=7d  → current = last 7 days, previous = 7 days before that
  period=30d → current = last 30 days, previous = 30 days before that
  period=90d → current = last 90 days, previous = 90 days before that
  period=12m → current = last 12 months, previous = 12 months before that

Special case: previous = 0 → trend = 100% (or null if current also 0)
```

### Data Queries Per Section

**users:**
- Current: `COUNT(Users) WHERE CreatedAt >= periodStart`
- Previous: `COUNT(Users) WHERE CreatedAt >= prevStart AND CreatedAt < periodStart`
- Chart: `GROUP BY DATE(CreatedAt)` for user growth

**loginActivity:**
- Chart: `COUNT(LoginHistory) GROUP BY DATE(CreatedAt)` within period

**activityBreakdown:**
- Chart: `COUNT(AuditLogs) GROUP BY Action` within period
- Top entities: `COUNT(AuditLogs) GROUP BY EntityType ORDER BY COUNT DESC LIMIT 5`

**storage:**
- Current: `SUM(FileMetadata.Size)` for current files
- Chart: cumulative storage growth — `SUM(Size) GROUP BY DATE(CreatedAt)` (running total)

**apiKeys:**
- Current: `COUNT(ApiKeys) WHERE !IsRevoked`
- Previous: `COUNT(ApiKeys) WHERE CreatedAt < periodStart AND !IsRevoked`

**webhooks:**
- Current: `COUNT(WebhookEndpoints) WHERE IsActive`
- Deliveries trend: `COUNT(WebhookDeliveries)` current vs previous period

**revenue (platform only):**
- Current: `SUM(PaymentRecords.Amount) WHERE Status == Completed` within period
- Previous: same for previous period

**tenants (platform only):**
- Current: `COUNT(Tenants) WHERE CreatedAt >= periodStart`
- Chart: `GROUP BY DATE(CreatedAt)`

All queries use `IgnoreQueryFilters()` for platform admin, standard filters for tenant users.

### Caching

**Redis key:** `analytics:{tenantId|platform}:{period}`
**TTL:** 15 minutes
**Invalidation:** No active invalidation — TTL-based expiry only. Dashboard data is not real-time; 15-min staleness is acceptable.

## Frontend

### Enhanced Dashboard Layout

```
┌──────────────────────────────────────────────────────┐
│  Welcome back, {Name}!              [Period: 30d ▾]  │
├──────────────────────────────────────────────────────┤
│                                                      │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────┐  │
│  │ Users    │ │ Storage  │ │ API Keys │ │Revenue │  │
│  │ 245      │ │ 5.0 GB   │ │ 15       │ │$2,970  │  │
│  │ ↑ 12.4%  │ │ ↑ 8.2%   │ │ ↓ 6.3%   │ │↑15.1% │  │
│  └──────────┘ └──────────┘ └──────────┘ └────────┘  │
│                                                      │
│  ┌────────────────────────┐ ┌──────────────────────┐ │
│  │ User Growth            │ │ Login Activity       │ │
│  │ (area chart)           │ │ (bar chart)          │ │
│  │                        │ │                      │ │
│  └────────────────────────┘ └──────────────────────┘ │
│                                                      │
│  ┌────────────────────────┐ ┌──────────────────────┐ │
│  │ Activity Breakdown     │ │ Storage Growth       │ │
│  │ (donut chart)          │ │ (area chart)         │ │
│  │ + Top Entities list    │ │                      │ │
│  └────────────────────────┘ └──────────────────────┘ │
│                                                      │
│  ┌──────────────────────────────────────────────────┐ │
│  │ Recent Activity (existing feed, unchanged)       │ │
│  └──────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────┘
```

### Dynamic Card/Chart Rendering

The stat cards and charts are driven by `enabledSections` from the API response:

```typescript
// Cards render dynamically
const cardConfigs = [
  { section: 'users', label: t('dashboard.users'), icon: Users, format: 'number' },
  { section: 'storage', label: t('dashboard.storage'), icon: HardDrive, format: 'bytes' },
  { section: 'apiKeys', label: t('dashboard.apiKeys'), icon: KeyRound, format: 'number' },
  { section: 'webhooks', label: t('dashboard.webhooks'), icon: Webhook, format: 'number' },
  { section: 'reports', label: t('dashboard.reports'), icon: FileText, format: 'number' },
  { section: 'imports', label: t('dashboard.imports'), icon: ArrowLeftRight, format: 'number' },
  { section: 'revenue', label: t('dashboard.revenue'), icon: DollarSign, format: 'currency' },
  { section: 'tenants', label: t('dashboard.tenants'), icon: Building, format: 'number' },
];

const visibleCards = cardConfigs.filter(c => enabledSections.includes(c.section));
// Renders 2-8 cards in a responsive grid
```

Same pattern for charts — only render charts for enabled sections.

### Stat Card Component

```
┌─────────────────────┐
│ 👥 Users            │
│                     │
│    245              │  ← large number (formatted)
│    ↑ 12.4%          │  ← green arrow + percentage (or red ↓)
│    vs previous 30d  │  ← muted subtitle
└─────────────────────┘
```

- Positive trend: green text + up arrow
- Negative trend: red text + down arrow
- Zero/null trend: gray text + dash
- Format: `number` (commas), `bytes` (human-readable GB/MB), `currency` ($X,XXX.XX)

### Recharts Theming

Use CSS custom properties for chart colors so they adapt to light/dark mode and theme presets:

```css
:root {
  --chart-1: hsl(var(--primary));
  --chart-2: hsl(var(--primary) / 0.7);
  --chart-3: hsl(142 76% 36%);  /* green */
  --chart-4: hsl(47 96% 53%);   /* amber */
  --chart-5: hsl(0 84% 60%);    /* red */
}
```

Recharts components read these via `getComputedStyle()` or pass them as props.

### Responsive Layout

- Desktop: 4 cards per row, 2 charts per row
- Tablet: 2 cards per row, 1 chart per row
- Mobile: 1 card per row, 1 chart per row

Charts resize via Recharts `ResponsiveContainer`.

## Components

### New Components

| Component | Purpose |
|-----------|---------|
| `AnalyticsSummaryCards.tsx` | Dynamic grid of stat cards with trend indicators |
| `StatCard.tsx` | Single stat card with icon, value, trend arrow |
| `AnalyticsCharts.tsx` | Container rendering enabled charts in responsive grid |
| `UserGrowthChart.tsx` | Area chart for new users over time |
| `LoginActivityChart.tsx` | Bar chart for daily logins |
| `ActivityBreakdownChart.tsx` | Donut chart for audit log action types + top entities list |
| `StorageGrowthChart.tsx` | Area chart for cumulative storage |
| `TenantGrowthChart.tsx` | Area chart for new tenants (platform admin only) |
| `PeriodSelector.tsx` | Dropdown for 7d/30d/90d/12m |

### Modified Components

| Component | Change |
|-----------|--------|
| `DashboardPage.tsx` | Replace static cards + basic feed with analytics layout |

## API

| Method | Path | Permission | Purpose |
|--------|------|-----------|---------|
| GET | `/api/v1/dashboard/analytics?period=30d` | System.ViewDashboard | Aggregated analytics with trend data |

No new permissions needed. No new entities or migrations.

## Backend Files

```
boilerplateBE/src/Starter.Application/Features/Dashboard/
├── DTOs/
│   └── DashboardAnalyticsDto.cs
├── Queries/
│   └── GetDashboardAnalytics/
│       ├── GetDashboardAnalyticsQuery.cs
│       └── GetDashboardAnalyticsQueryHandler.cs

boilerplateBE/src/Starter.Api/Controllers/
└── DashboardController.cs
```

## Seed Data

No new seed data needed. All data comes from existing entities.

## Package Addition

```bash
cd boilerplateFE && npm install recharts
```

## i18n Keys

Add to all 3 locales:
```
dashboard.analytics, dashboard.period, dashboard.7d, dashboard.30d, dashboard.90d, dashboard.12m
dashboard.users, dashboard.storage, dashboard.apiKeys, dashboard.webhooks
dashboard.reports, dashboard.imports, dashboard.revenue, dashboard.tenants
dashboard.trend, dashboard.vsPrevious, dashboard.noData
dashboard.userGrowth, dashboard.loginActivity, dashboard.activityBreakdown
dashboard.storageGrowth, dashboard.tenantGrowth, dashboard.topEntities
```

## Performance

- **Redis caching:** 15-min TTL prevents repeated expensive queries
- **Conditional computation:** Disabled features skip DB queries entirely
- **No N+1:** All aggregation uses single `GROUP BY` queries per section
- **Lightweight response:** Only numbers and date/count pairs, no raw entity data
- **Platform admin cache separate from tenant caches** — no cross-contamination

## Testing Checklist

- [ ] Free plan tenant: sees Users + Login Activity only (no storage, no API keys, no webhooks)
- [ ] Pro plan tenant: sees Users + Storage + API Keys + Webhooks + Reports
- [ ] Platform admin: sees all sections including Revenue + Tenants
- [ ] Period selector: switching 7d → 30d refreshes data correctly
- [ ] Trend arrows: positive = green ↑, negative = red ↓, zero = gray
- [ ] Trend calculation: correct % vs previous period
- [ ] Charts render with theme colors (test light + dark mode)
- [ ] Responsive: 4 columns → 2 → 1 on resize
- [ ] Caching: second load within 15 min returns cached data
- [ ] Empty state: new tenant with no activity shows zero values, not errors
