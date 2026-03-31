# Tenant Self-Service Portal — Design Spec

## Problem

Tenant admins currently navigate the same UI as platform admins — they see a "Tenants" list (showing only their own tenant), click into it, and manage from there. There's no dedicated self-service experience, no usage visibility, no onboarding, and no upgrade prompts. The functionality mostly exists in the backend but the UX doesn't reflect a tenant admin's perspective.

## Goals

1. Tenant admins get a dedicated "Organization" experience — no "Tenants" list
2. First-time tenants get a guided onboarding wizard
3. Usage vs limits is visible with upgrade prompts
4. Tenant-scoped audit logs for admin accountability
5. Dashboard shows tenant-specific metrics, not platform-wide stats
6. Platform admins continue to see the full "Tenants" list as before

---

## User Flows

### Flow 1: New Tenant Registration → Onboarding

```
Register Tenant (/register-tenant)
  → Email verification
  → First login
  → Onboarding wizard appears (modal/full-screen):
      Step 1: Organization Profile
        - Company name (pre-filled from registration)
        - Upload logo
        - Primary color picker
        - Description
      Step 2: Invite Your Team
        - Email + role fields (add multiple)
        - "Skip for now" option
      Step 3: Configure
        - Default language
        - Date format
        - Timezone
        - "You're all set!" confirmation
  → Dashboard (tenant-scoped)
```

**Trigger condition**: Show wizard when ALL of these are true:
- User is a tenant admin (has tenantId)
- Tenant has only 1 user (the admin who just registered)
- Tenant has no logo set
- User hasn't dismissed the wizard before (persist dismissal in localStorage)

### Flow 2: Tenant Admin Daily Experience

```
Login → Dashboard (tenant-scoped)
  Sidebar shows:
    - Dashboard
    - Users
    - Roles
    - Organization ← (replaces "Tenants")
    - Files
    - Reports
    - Audit Logs ← (tenant-scoped, new)
    - API Keys
    - Feature Flags
    - Billing ← (if billing enabled)
    - Settings
```

**Key difference**: "Tenants" sidebar item is hidden. "Organization" links directly to `/organization` which resolves to their tenant detail page.

### Flow 3: Tenant Admin Manages Organization

```
Sidebar → Organization (/organization)
  Tabs:
    - Overview (name, status, created date, default role)
    - Branding (logo, colors, description)
    - Business Info (address, phone, website, tax ID)
    - Custom Text (login page text, email footer, multi-language)
    - Usage ← (NEW)
    - Activity ← (NEW — tenant-scoped audit log)
    - Feature Flags (opt-out controls)
```

### Flow 4: Usage Tab — Limits & Upgrade Prompts

```
Organization → Usage tab
  ┌─────────────────────────────────────────────────┐
  │ Current Plan: Pro                    [Upgrade]  │
  ├─────────────────────────────────────────────────┤
  │                                                 │
  │ Users         ████████░░  8 / 10 seats          │
  │ Storage       ██░░░░░░░░  450 MB / 5 GB         │
  │ API Keys      ██░░░░░░░░  2 / 10                │
  │ Reports       Enabled ✓                         │
  │ PDF Export    Enabled ✓                         │
  │                                                 │
  │ ⚠ You're at 80% of your user seat limit.       │
  │   Upgrade to Enterprise for unlimited users.    │
  │                                                 │
  └─────────────────────────────────────────────────┘
```

**Data source**: Feature flags provide the limits (users.max_count, files.max_storage_mb, api_keys.max_count). Actual counts come from existing API queries (user count, file storage sum, API key count).

**Threshold alerts** (shown as banners):
- 80% usage: yellow warning
- 95% usage: red warning with upgrade CTA
- 100%: blocked with "Upgrade required" message

### Flow 5: Tenant-Scoped Audit Logs

```
Organization → Activity tab
  (or Sidebar → Audit Logs for tenant admins)

  Shows same AuditLogsPage but scoped to tenant.
  Backend EF query filter already handles this.

  Only change needed:
  - Grant System.ViewAuditLogs to Admin role
  - The global query filter ensures tenant admins
    only see their own tenant's audit entries
```

### Flow 6: Tenant-Scoped Dashboard

```
Dashboard (/dashboard) for tenant admins shows:

  Welcome banner (same as today)

  Stat cards:
    - My Users: 8       (not "Total Users" across platform)
    - My Roles: 3       (custom roles in this tenant)
    - Storage Used: 450 MB
    - API Keys: 2

  Usage alert (if near limits):
    ⚠ 80% of user seats used. Upgrade →

  Recent Activity (tenant-scoped):
    Same as today but EF filter scopes it

  Recent Users (tenant-scoped):
    Same as today
```

### Flow 7: Usage Threshold Email Notifications

When a tenant crosses usage thresholds, send email to tenant admins:

- **80% threshold**: "You're approaching your user limit"
- **95% threshold**: "Almost at capacity — upgrade to avoid disruption"
- **100% threshold**: "Limit reached — new operations blocked until upgrade"

**Implementation**: Background job checks usage daily or on-demand when the relevant resource is created (user created → check user count vs limit).

---

## Sidebar Logic

```typescript
// Pseudo-code for sidebar nav items
const isTenantUser = !!user?.tenantId;
const isPlatformAdmin = !user?.tenantId;

const navItems = [
  { label: 'Dashboard', icon: LayoutDashboard, path: '/dashboard', show: true },
  { label: 'Users', icon: Users, path: '/users', show: hasPermission(Users.View) },
  { label: 'Roles', icon: Shield, path: '/roles', show: hasPermission(Roles.View) },

  // Platform admin sees "Tenants" (list all)
  // Tenant admin sees "Organization" (own tenant)
  ...(isPlatformAdmin && hasPermission(Tenants.View)
    ? [{ label: 'Tenants', icon: Building, path: '/tenants' }]
    : []),
  ...(isTenantUser
    ? [{ label: 'Organization', icon: Building, path: '/organization' }]
    : []),

  { label: 'Files', icon: FolderOpen, path: '/files', show: hasPermission(Files.View) },
  { label: 'Reports', icon: FileText, path: '/reports', show: hasPermission(System.ExportData) },
  { label: 'Audit Logs', icon: ClipboardList, path: '/audit-logs', show: hasPermission(System.ViewAuditLogs) },
  { label: 'API Keys', icon: KeyRound, path: '/api-keys', show: hasPermission(ApiKeys.View) },
  { label: 'Feature Flags', icon: ToggleRight, path: '/feature-flags', show: hasPermission(FeatureFlags.View) },
  { label: 'Settings', icon: Settings2, path: '/settings', show: hasPermission(System.ManageSettings) },
];
```

---

## Component Architecture

### New Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `OnboardingWizard` | `src/features/tenants/components/OnboardingWizard.tsx` | 3-step setup for new tenants |
| `UsageTab` | `src/features/tenants/components/UsageTab.tsx` | Usage bars + limits from feature flags |
| `UsageCard` | `src/features/tenants/components/UsageCard.tsx` | Single progress bar for a resource |
| `UsageAlert` | `src/features/tenants/components/UsageAlert.tsx` | Threshold warning banner |
| `ActivityTab` | `src/features/tenants/components/ActivityTab.tsx` | Tenant-scoped audit log (reuses AuditLogsPage logic) |

### Modified Components

| Component | Change |
|-----------|--------|
| `Sidebar.tsx` | Conditional "Tenants" vs "Organization" based on user type |
| `DashboardPage.tsx` | Tenant-scoped stats when user is tenant user |
| `routes.tsx` | Add `/organization` route |
| `routes.config.ts` | Add `ROUTES.ORGANIZATION` |

### Reused Components (no changes)

| Component | Reused For |
|-----------|-----------|
| `TenantDetailPage.tsx` | Organization page renders this with auto-resolved tenant ID |
| `Pagination` | Activity tab pagination |
| `EmptyState` | When no activity yet |
| `Badge` | Usage status badges |
| `Card` | Usage cards |

---

## Backend Changes

### New Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `GET /api/v1/tenants/my` | GET | Returns current user's tenant detail (resolves from JWT tenantId) |
| `GET /api/v1/tenants/my/usage` | GET | Returns usage stats (user count, storage, API keys) vs limits (from feature flags) |

### Permission Changes

| Permission | Current Role | Change |
|------------|-------------|--------|
| `System.ViewAuditLogs` | SuperAdmin only | **Add to Admin role** — EF filter already scopes to tenant |

### New Query

**`GetTenantUsageQuery`** → Returns:
```json
{
  "users": { "current": 8, "limit": 100, "percentage": 8 },
  "storage": { "currentMb": 450, "limitMb": 5120, "percentage": 8.8 },
  "apiKeys": { "current": 2, "limit": 10, "percentage": 20 },
  "features": {
    "reports": { "enabled": true },
    "pdfExport": { "enabled": true },
    "invitations": { "enabled": true }
  }
}
```

This query:
1. Counts users in tenant: `context.Users.CountAsync()`
2. Sums file sizes: `context.FileMetadata.SumAsync(f => f.Size)`
3. Counts API keys: `context.ApiKeys.CountAsync()`
4. Reads limits from `IFeatureFlagService.GetValueAsync<int>("users.max_count")` etc.
5. Reads boolean flags for feature enablement

### New Background Job (for email notifications)

**`UsageThresholdCheckerJob`**:
- Runs daily (or triggered on resource creation)
- For each tenant, checks usage vs limits
- If crossing 80%/95%/100% threshold for the first time, sends email to tenant admins
- Tracks last notified threshold in cache to avoid spam

---

## Onboarding Wizard Detail

### Trigger Logic

```typescript
// In App.tsx or DashboardPage.tsx
const shouldShowOnboarding = useMemo(() => {
  if (!user?.tenantId) return false; // platform admin
  if (localStorage.getItem('onboarding-dismissed')) return false;
  // Check: tenant has 1 user, no logo
  return tenant?.userCount === 1 && !tenant?.logoUrl;
}, [user, tenant]);
```

### Step 1: Organization Profile

| Field | Type | Pre-filled From |
|-------|------|----------------|
| Company Name | Input | tenant.name (from registration) |
| Logo | FileUpload | Empty |
| Primary Color | Color picker | Empty (defaults to theme) |
| Description | Textarea | Empty |

**On save**: Calls `useUpdateTenantBranding` mutation.

### Step 2: Invite Your Team

| Field | Type | Notes |
|-------|------|-------|
| Email | Input | Add multiple rows |
| Role | Select | From assignable roles |
| [+ Add another] | Button | Adds a row |
| [Skip for now] | Link | Skips to step 3 |

**On save**: Calls invite mutation for each email. Errors shown per-row.

### Step 3: Configure Preferences

| Field | Type | Notes |
|-------|------|-------|
| Default Language | Select | en, ar, ku |
| Date Format | Select | yyyy-MM-dd, dd/MM/yyyy, MM/dd/yyyy |
| Timezone | Select | Common timezones |

**On save**: Calls `useUpdateSettings` for tenant-level settings.

### Final Screen

"You're all set! Your organization is ready."
- [Go to Dashboard] button
- Sets `localStorage.setItem('onboarding-dismissed', 'true')`

---

## Dashboard Scoping

### Current Behavior

```
Dashboard queries:
- useUsers() → ALL users (platform admin sees all, tenant admin sees theirs via filter)
- useRoles() → ALL roles
- useAuditLogs() → ALL audit logs (only SuperAdmin has permission)
```

### New Behavior

```
Dashboard queries for tenant admin:
- useSearchUsers({ pageSize: 1 }) → get totalCount from pagination
- useRoles() → already scoped by EF filter
- useAuditLogs() → now accessible (after permission grant)
- useGetTenantUsage() → NEW query for usage stats

Stat cards for tenant admin:
- "My Users" instead of "Total Users"
- "Storage Used" instead of "Active Roles"
- "API Keys" instead of "Total Roles"
- "Plan: Pro" instead of "Platform Status"
```

---

## Implementation Order

### Phase 1: Sidebar + Organization Route (~1 day)
1. Add `ROUTES.ORGANIZATION = '/organization'` to routes config
2. Add route in `routes.tsx` — renders TenantDetailPage with auto-resolved ID
3. Create `OrganizationPage.tsx` wrapper that gets tenantId from auth store and passes to TenantDetailPage
4. Update Sidebar to show "Organization" for tenant users, "Tenants" for platform admins
5. Add `useBackNavigation` to OrganizationPage (back to dashboard)

### Phase 2: Usage Tab (~3 days)
1. Backend: Create `GetTenantUsageQuery` + handler
2. Backend: Add `GET /api/v1/tenants/my/usage` endpoint
3. Frontend: Create `UsageTab`, `UsageCard`, `UsageAlert` components
4. Frontend: Add Usage tab to TenantDetailPage
5. Frontend: Add usage API + query hooks

### Phase 3: Tenant-Scoped Dashboard (~2 days)
1. Grant `System.ViewAuditLogs` to Admin role in `Roles.cs`
2. Update DashboardPage to detect tenant user and show tenant-specific stats
3. Add usage alert banner to dashboard when near limits
4. Use `GetTenantUsageQuery` for dashboard stat cards

### Phase 4: Onboarding Wizard (~3 days)
1. Create `OnboardingWizard` component with 3 steps
2. Add trigger logic in App.tsx or DashboardPage
3. Step 1: Reuse branding form fields
4. Step 2: Reuse invite user form logic
5. Step 3: Settings form for language/date/timezone
6. LocalStorage dismissal tracking

### Phase 5: Activity Tab (~1 day)
1. Create `ActivityTab` that wraps AuditLogsPage query logic
2. Add to TenantDetailPage tabs
3. Pagination + filters reused from existing audit logs

### Phase 6: Email Notifications (~2 days)
1. Create `UsageThresholdCheckerJob` background service
2. Email templates for 80%/95%/100% thresholds
3. Cache-based tracking to avoid duplicate notifications
4. Trigger on user creation / file upload / API key creation

**Total estimated effort: ~12 days**

---

## Files to Create

| File | Purpose |
|------|---------|
| `boilerplateFE/src/features/tenants/pages/OrganizationPage.tsx` | Wrapper that resolves own tenant ID |
| `boilerplateFE/src/features/tenants/components/OnboardingWizard.tsx` | 3-step setup wizard |
| `boilerplateFE/src/features/tenants/components/UsageTab.tsx` | Usage bars + limits display |
| `boilerplateFE/src/features/tenants/components/UsageCard.tsx` | Single usage progress bar |
| `boilerplateFE/src/features/tenants/components/UsageAlert.tsx` | Threshold warning banner |
| `boilerplateFE/src/features/tenants/components/ActivityTab.tsx` | Tenant-scoped audit log tab |
| `boilerplateBE/.../Queries/GetTenantUsage/GetTenantUsageQuery.cs` | Usage stats query |
| `boilerplateBE/.../Queries/GetTenantUsage/GetTenantUsageQueryHandler.cs` | Handler |
| `boilerplateBE/.../Queries/GetMyTenant/GetMyTenantQuery.cs` | Resolve own tenant |
| `boilerplateBE/.../DTOs/TenantUsageDto.cs` | Usage response DTO |
| `boilerplateBE/.../Services/UsageThresholdCheckerJob.cs` | Background threshold checker |

## Files to Modify

| File | Change |
|------|--------|
| `Sidebar.tsx` | Conditional Organization vs Tenants |
| `routes.tsx` | Add /organization route |
| `routes.config.ts` | Add ROUTES.ORGANIZATION |
| `DashboardPage.tsx` | Tenant-scoped stats |
| `TenantDetailPage.tsx` | Add Usage and Activity tabs |
| `Roles.cs` | Grant ViewAuditLogs to Admin |
| `api.config.ts` | Add usage endpoint |
| `tenants.api.ts` | Add getMyTenant, getUsage |
| `tenants.queries.ts` | Add useMyTenant, useUsage hooks |
