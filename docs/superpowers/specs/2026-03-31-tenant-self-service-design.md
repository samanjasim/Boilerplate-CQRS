# Tenant Self-Service Portal — Design Spec (Revised)

## Problem

Tenant admins navigate the same UI as platform admins — they see a "Tenants" list (only their own tenant), click into it, and manage from there. There's no dedicated self-service experience, no onboarding, and the SuperAdmin tenant detail page doesn't show billing/usage. The billing page exists separately but isn't integrated into the tenant view.

## Goals

1. **One component, two routes** — `TenantDetailPage` serves both `/organization` (self-service) and `/tenants/:id` (admin) with context-aware rendering
2. **Permission-driven visibility** — every tab, action, and sidebar item is controlled by permissions. Tenant admins create custom roles with granular access.
3. **SuperAdmin sees everything** — the tenant detail includes subscription, usage, and activity in addition to branding/business
4. **No duplication** — Organization reuses TenantDetailPage, Activity reuses audit log queries, Subscription tab reuses billing components
5. **Onboarding wizard** for first-time tenants

---

## Architecture: One Component, Two Routes

```
/organization          → TenantDetailPage(selfService=true)
/tenants/:id           → TenantDetailPage(selfService=false)
```

`TenantDetailPage` detects context:

```tsx
function TenantDetailPage({ selfService }: { selfService?: boolean }) {
  const { id } = useParams();
  const user = useAuthStore(selectUser);

  // Resolve tenant ID: own tenant in self-service, URL param for admin
  const tenantId = selfService ? user?.tenantId : id;
  const isPlatformAdmin = !user?.tenantId;
}
```

No separate `OrganizationPage.tsx` needed. No duplication.

---

## Sidebar Logic

### Tenant Users (has tenantId)

```
Dashboard                    ← always
Users                        ← if Users.View
Roles                        ← if Roles.View
Organization                 ← if Tenants.View (goes to /organization)
Files                        ← if Files.View
Reports                      ← if System.ExportData
Audit Logs                   ← if System.ViewAuditLogs
API Keys                     ← if ApiKeys.View
Feature Flags                ← if FeatureFlags.View
Billing                      ← if Billing.View (goes to /billing)
Settings                     ← if System.ManageSettings
```

### Platform Admin (no tenantId)

```
Dashboard                    ← always
Users                        ← if Users.View
Roles                        ← if Roles.View
Tenants                      ← if Tenants.View (goes to /tenants list)
Files                        ← if Files.View
Reports                      ← if System.ExportData
Audit Logs                   ← if System.ViewAuditLogs
API Keys                     ← if ApiKeys.View
Feature Flags                ← if FeatureFlags.View
Billing Plans                ← if Billing.ViewPlans
Subscriptions                ← if Billing.ManageTenantSubscriptions
Settings                     ← if System.ManageSettings
```

**Key**: Tenant users see "Organization" + "Billing". Platform admins see "Tenants" + "Billing Plans" + "Subscriptions". The sidebar item, icon, label, and destination all change based on `user.tenantId`.

---

## Tab Structure

### Tabs Shown Per Context

| Tab | Permission Required | Tenant User | SuperAdmin on Tenant |
|-----|-------------------|-------------|---------------------|
| Overview | `Tenants.View` | Read-only (name, status, created, member count) | Read + status actions (suspend/activate/deactivate) |
| Branding | `Tenants.Update` | Edit own logo, colors, description | Edit any tenant |
| Business Info | `Tenants.Update` | Edit own address, phone, tax ID | Edit any tenant |
| Custom Text | `Tenants.Update` | Edit own login page text, email footer | Edit any tenant |
| Activity | `System.ViewAuditLogs` | Tenant-scoped audit logs | Same tenant's audit logs |
| Feature Flags | `FeatureFlags.View` | View values + opt-out (`FeatureFlags.OptOut`) | View + override (`FeatureFlags.ManageTenantOverrides`) |
| Subscription | `Billing.ManageTenantSubscriptions` | **Hidden** (tenant users use /billing instead) | Inline: plan, usage, payments, change plan |

### Tab Visibility Logic

```tsx
const tabs = [
  { key: 'overview', label: t('tenants.overview'), show: hasPermission(Tenants.View) },
  { key: 'branding', label: t('tenants.branding'), show: hasPermission(Tenants.Update) },
  { key: 'businessInfo', label: t('tenants.businessInfo'), show: hasPermission(Tenants.Update) },
  { key: 'customText', label: t('tenants.customText'), show: hasPermission(Tenants.Update) },
  { key: 'activity', label: t('tenants.activity'), show: hasPermission(System.ViewAuditLogs) },
  { key: 'featureFlags', label: t('tenants.featureFlags'), show: hasPermission(FeatureFlags.View) },
  { key: 'subscription', label: t('tenants.subscription'), show: isPlatformAdmin && hasPermission(Billing.ManageTenantSubscriptions) },
].filter(tab => tab.show);
```

A user with only `Tenants.View` sees just the Overview tab (read-only). A tenant admin with full permissions sees all tabs except Subscription. SuperAdmin sees everything.

---

## Permission Changes

### Grant to Admin Role

| Permission | Current | New | Reason |
|-----------|---------|-----|--------|
| `Roles.ManagePermissions` | SuperAdmin only | **Admin** | Tenant admins need to configure custom roles |
| `System.ViewAuditLogs` | SuperAdmin only | **Admin** | Tenant admins need to see their org's activity |

### Permission Ceiling Enforcement

When an Admin assigns permissions to a role, the backend validates:

```
For each permission being assigned:
  If the caller does NOT have this permission → reject with error

Example:
  Admin has: [Users.View, Users.Create, Files.View, Billing.View]
  Admin creates "Editor" role and assigns:
    ✅ Users.View → Admin has it → allowed
    ✅ Files.View → Admin has it → allowed
    ❌ Billing.ManagePlans → Admin doesn't have it → blocked
    ❌ FeatureFlags.ManageTenantOverrides → Admin doesn't have it → blocked
```

**Backend location**: `ManageRolePermissionsCommandHandler` — add ceiling check before saving.

### New Permission (Optional)

Consider adding `Tenants.ViewOwn` for users who should see Organization overview but not edit. Currently `Tenants.View` controls both the list (SuperAdmin) and the detail. If we want finer control:

- `Tenants.View` — list all tenants (SuperAdmin)
- `Tenants.Show` — view single tenant detail
- `Tenants.Update` — edit branding, business info, etc.

The existing `Tenants.Show` already serves this purpose. Users with `Tenants.Show` can see the Overview tab. Users with `Tenants.Update` can see and edit Branding/Business/CustomText tabs.

---

## Detailed Tab Specs

### Overview Tab

**Read-only section** (visible to all with `Tenants.Show`):
- Organization name
- Created date
- Status badge (Active, Suspended, etc.)
- Total members count
- Current plan name (from subscription, if billing enabled)
- Subdomain URL (if configured)

**Admin actions** (visible only to SuperAdmin / `isPlatformAdmin`):
- Suspend button
- Activate button
- Deactivate button
- Delete button (with ConfirmDialog)
- Default registration role selector

**Tenant admin actions** (visible with `Tenants.Update`):
- Default registration role selector (with permission ceiling)

### Activity Tab

Reuses the audit log query (`useAuditLogs`) with the tenant's ID implicitly scoped by the EF query filter.

```tsx
function ActivityTab() {
  // Same hooks as AuditLogsPage but simplified UI
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const { data } = useAuditLogs({ pageNumber, pageSize, sortBy: 'performedAt', sortDescending: true });

  return (
    <>
      <Table>
        {/* Entity Type, Action, Performed By, Date columns */}
      </Table>
      {data?.pagination && <Pagination ... />}
    </>
  );
}
```

No new backend endpoint needed — existing audit logs endpoint + EF filter handles scoping.

### Subscription Tab (SuperAdmin Only)

Embeds existing `SubscriptionDetailPage` content:
- Current plan with badge
- Usage bars (from `GetUsageQuery`)
- Payment history table (from `GetPaymentsQuery`)
- Change plan button (from `PlanSelectorModal`)
- Cancel button

**This replaces the separate `/subscriptions/:tenantId` route.** SuperAdmin accesses subscription management through the Tenant detail page instead of a separate page.

---

## Dashboard Scoping

### Current State

Dashboard shows platform-wide stats (total users, total roles across ALL tenants).

### Revised State

```tsx
function DashboardPage() {
  const user = useAuthStore(selectUser);
  const isTenantUser = !!user?.tenantId;

  if (isTenantUser) {
    // Tenant-scoped stats
    return <TenantDashboard />;
  }
  // Platform-wide stats (existing behavior)
  return <PlatformDashboard />;
}
```

**TenantDashboard** shows:
- Welcome banner (same)
- Stat cards: My Users, My Files, My API Keys, Current Plan
- Usage alert if near limits (from `GetUsageQuery` via billing)
- Recent Activity (tenant-scoped audit logs)
- Recent Users (already scoped by EF filter)

**PlatformDashboard** shows:
- Welcome banner (same)
- Stat cards: Total Users, Total Tenants, Total Roles, Platform Status
- Recent Activity (all audit logs)
- Recent Users (all users)

---

## Onboarding Wizard

### Trigger

Show when ALL of these are true:
- `user.tenantId` exists (tenant user, not platform admin)
- Tenant has 1 user (only the admin who registered)
- Tenant has no logo set
- `localStorage.getItem('onboarding-complete')` is falsy

### Steps

**Step 1: Organization Profile**
- Company name (pre-filled, editable)
- Logo upload
- Primary color picker
- Description
- Calls: `useUpdateTenantBranding`

**Step 2: Invite Your Team**
- Dynamic rows: email + role selector
- "Add another" button
- "Skip for now" link
- Calls: invite mutation per row

**Step 3: You're All Set**
- Summary of what was configured
- "Go to Dashboard" button
- Sets `localStorage.setItem('onboarding-complete', 'true')`

### UI

Full-screen modal overlay (similar to auth layout). Not a sidebar page — it takes over the screen on first login.

---

## Implementation Order

### Phase 1: Sidebar + Organization Route (1 day)
- Add `selfService` prop to TenantDetailPage
- Add `/organization` route pointing to `TenantDetailPage(selfService=true)`
- Update Sidebar: "Organization" for tenant users, "Tenants" for platform admins
- `useBackNavigation` adapts label based on mode

### Phase 2: Permission Grants + Ceiling (1.5 days)
- Grant `Roles.ManagePermissions` to Admin in `Roles.cs`
- Grant `System.ViewAuditLogs` to Admin in `Roles.cs`
- Add permission ceiling check in `ManageRolePermissionsCommandHandler`
- Frontend: Update role edit page to show permission matrix for tenant admins

### Phase 3: Activity Tab (1 day)
- Create Activity tab component wrapping audit log query
- Add to TenantDetailPage tab list (gated by `System.ViewAuditLogs`)
- Reuse existing Table, Pagination, Badge components

### Phase 4: Subscription Tab for SuperAdmin (1 day)
- Move SubscriptionDetailPage content into a tab component
- Add "Subscription" tab to TenantDetailPage (gated by `isPlatformAdmin && Billing.ManageTenantSubscriptions`)
- Remove separate `/subscriptions/:tenantId` route (or redirect to `/tenants/:id?tab=subscription`)

### Phase 5: Dashboard Scoping (1 day)
- Split DashboardPage into TenantDashboard / PlatformDashboard
- TenantDashboard uses billing `GetUsageQuery` for stat cards
- Add usage alert banner when near limits

### Phase 6: Onboarding Wizard (2-3 days)
- Create OnboardingWizard component (3 steps)
- Trigger logic in App.tsx
- Reuse branding form, invite form components
- LocalStorage dismissal

**Total: ~8 days**

---

## Files to Modify

| File | Change |
|------|--------|
| `Sidebar.tsx` | Conditional Organization vs Tenants based on `user.tenantId` |
| `TenantDetailPage.tsx` | Add `selfService` prop, Activity tab, Subscription tab, conditional rendering |
| `routes.tsx` | Add `/organization` route |
| `routes.config.ts` | Add `ROUTES.ORGANIZATION` |
| `DashboardPage.tsx` | Split into Tenant/Platform dashboard |
| `Roles.cs` | Grant ManagePermissions + ViewAuditLogs to Admin |
| `ManageRolePermissionsCommandHandler.cs` | Add permission ceiling check |

## Files to Create

| File | Purpose |
|------|---------|
| `ActivityTab.tsx` | Tenant-scoped audit log tab |
| `SubscriptionTab.tsx` | Inline subscription management for SuperAdmin |
| `TenantDashboard.tsx` | Tenant-scoped dashboard stats |
| `PlatformDashboard.tsx` | Platform-wide dashboard stats |
| `OnboardingWizard.tsx` | 3-step setup wizard |

---

## What We're NOT Building (Deferred)

- Email notifications for usage thresholds (can be added later as a background job)
- Payment method management UI (external payment processor handles this)
- Invoice generation (external payment processor handles this)
- Discount codes / promotional pricing
- Multi-currency per-tenant (plans define currency globally)
