# Sidebar Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the flat 25-item sidebar into a modules-first grouped navigation with mobile drawer behavior, exactly as specified in §4 of `docs/superpowers/specs/2026-04-27-redesign-phase-1-design.md` (Plan A).

**Architecture:**
- Data-builder hook (`useNavGroups`) returns `SidebarNavGroup[]`, applying all permission / module / feature-flag / tenancy gates. Sidebar render becomes a pure consumer.
- Group eyebrow labels + 1px tinted dividers in expanded mode; thin separators only in collapsed mode. Empty groups (zero visible items) hide entirely.
- On viewports `<lg`, the sidebar leaves the layout flow and becomes a slide-out drawer driven by the existing `useUIStore.sidebarOpen` state. Backdrop click + Esc + route-change all close it. Body scroll locks while the drawer is open. The Header's mobile trigger swaps Menu ↔ X to mirror the drawer state.

**Tech Stack:** React 19, TypeScript 5.9, Tailwind CSS 4 (with `motion-safe:` and `max-lg:` variants), Zustand 5, react-router-dom 7, react-i18next 15, lucide-react.

**Verification model:** This codebase has no FE unit-test runner. Every task verifies with (1) `npm run build` (type/build check), (2) `npm run lint` (ESLint), and (3) a visual pass against the running test app at `http://localhost:3100`. The test app is the same Phase 0 harness — `_testJ4visual/` if it still exists, or re-spin via `pwsh scripts/rename.ps1 -Name "_testJ4visual" -OutputDir "."`. **All edits go in `boilerplateFE/src/...`** and are then copied (or hot-reloaded if symlinked) into `_testJ4visual/boilerplateFE/`.

**Working directory for all `npm` commands:** `/Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE`. **Working directory for all `git` commands:** the repo root.

**Naming conventions used throughout this plan** (chosen for consistency with the existing store / selector API in `src/stores/ui.store.ts`):

| Concept | Identifier |
|---|---|
| Mobile drawer state in store | `sidebarOpen: boolean` (default **`false`**) |
| Store action to toggle | `toggleSidebar()` |
| Store action to set | `setSidebarOpen(open: boolean)` |
| Selector | `selectSidebarOpen` |
| Local React variables | `sidebarOpen`, `setSidebarOpen` (match store) |
| Desktop collapsed-width state | `sidebarCollapsed`, `selectSidebarCollapsed` (unchanged) |
| Group typescript types | `SidebarNavItem`, `SidebarNavGroup` |
| Hook | `useNavGroups(): SidebarNavGroup[]` |

---

## Task 1: Add `nav.groups.*` i18n keys (EN only)

**Why:** Sidebar groups need labels. Phase 1 spec §4.6 lists exactly these keys. AR / KU fall back to EN automatically via i18next — see deferred item in spec §3.

**Files:**
- Modify: `src/i18n/locales/en/translation.json`

- [ ] **Step 1.1: Add the keys**

Open `src/i18n/locales/en/translation.json`, find the `"nav": { ... }` block, and add a `"groups"` sub-object as the **last** entry inside `nav` (so the diff is small and clean):

```json
"nav": {
  "dashboard": "Dashboard",
  "users": "Users",
  ...existing keys unchanged...
  "workflowDefinitions": "Definitions",
  "groups": {
    "workflow": "Workflow",
    "communication": "Communication",
    "products": "Products",
    "billing": "Billing",
    "webhooks": "Webhooks",
    "importExport": "Import / Export",
    "people": "People",
    "content": "Content",
    "platform": "Platform"
  }
}
```

Do not modify any existing keys. Do not touch `ar/translation.json` or `ku/translation.json` — i18next will fall back to EN.

- [ ] **Step 1.2: Build check**

```bash
npm run build
```

Expected: build succeeds. (No code consumes the keys yet — purely a JSON addition.)

- [ ] **Step 1.3: Commit**

```bash
git add boilerplateFE/src/i18n/locales/en/translation.json
git commit -m "feat(fe/i18n): add nav.groups.* keys for sidebar grouping"
```

---

## Task 2: Extract `useNavGroups` hook + adopt grouped data shape

**Why:** Spec §4.1, §4.2. The existing `Sidebar.tsx` interleaves data-building with render across 130+ lines. Splitting the data builder into a hook gives Sidebar a single responsibility (rendering) and makes the gating logic testable / scannable in isolation. Hoisting the module flag to the group level (instead of repeating `activeModules.X && hasPermission(...)` per item) trims duplication and clarifies intent.

**Files:**
- Create: `src/components/layout/MainLayout/useNavGroups.ts`
- Modify: `src/components/layout/MainLayout/Sidebar.tsx` (rewrite)

- [ ] **Step 2.1: Create `useNavGroups.ts`**

Create `src/components/layout/MainLayout/useNavGroups.ts` with this exact content:

```ts
import { useTranslation } from 'react-i18next';
import {
  ArrowLeftRight,
  Bell,
  Building,
  ClipboardCheck,
  ClipboardList,
  CreditCard,
  FileBarChart2,
  FileText,
  FolderOpen,
  GitBranch,
  History,
  KeyRound,
  LayoutDashboard,
  Link2,
  ListChecks,
  MessageSquare,
  Package,
  ReceiptText,
  ScrollText,
  Settings2,
  Shield,
  ToggleRight,
  Users,
  Webhook,
  Zap,
  type LucideIcon,
} from 'lucide-react';

import { ROUTES } from '@/config';
import { activeModules, isModuleActive } from '@/config/modules.config';
import { PERMISSIONS } from '@/constants';
import { usePendingTaskCount } from '@/features/workflow/api';
import { useFeatureFlag, usePermissions } from '@/hooks';
import { selectUser, useAuthStore } from '@/stores';

export interface SidebarNavItem {
  label: string;
  icon: LucideIcon;
  path: string;
  end?: boolean;
  badge?: number;
}

export interface SidebarNavGroup {
  id: string;
  label?: string;
  items: SidebarNavItem[];
}

/**
 * Builds the sidebar nav as a list of permission/module/flag-gated groups.
 * Empty groups are stripped, so consumers can render the result directly.
 */
export function useNavGroups(): SidebarNavGroup[] {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const user = useAuthStore(selectUser);
  const tenantScoped = Boolean(user?.tenantId);

  const webhooksFlag = useFeatureFlag('webhooks.enabled');
  const importsFlag = useFeatureFlag('imports.enabled');
  const exportsFlag = useFeatureFlag('exports.enabled');

  const { data: pendingTaskCount = 0 } = usePendingTaskCount(isModuleActive('workflow'));

  const groups: SidebarNavGroup[] = [];

  // Top block (no label, no group divider above it)
  groups.push({
    id: 'top',
    items: [
      { label: t('nav.dashboard'), icon: LayoutDashboard, path: ROUTES.DASHBOARD, end: true },
      { label: t('nav.notifications'), icon: Bell, path: ROUTES.NOTIFICATIONS },
    ],
  });

  // Workflow module
  if (activeModules.workflow) {
    const items: SidebarNavItem[] = [];
    if (hasPermission(PERMISSIONS.Workflows.View)) {
      items.push({
        label: t('workflow.sidebar.taskInbox'),
        icon: ClipboardCheck,
        path: ROUTES.WORKFLOWS.INBOX,
        badge: pendingTaskCount > 0 ? pendingTaskCount : undefined,
      });
      items.push({
        label: t('workflow.sidebar.history'),
        icon: History,
        path: ROUTES.WORKFLOWS.INSTANCES,
      });
    }
    if (hasPermission(PERMISSIONS.Workflows.ManageDefinitions)) {
      items.push({
        label: t('workflow.sidebar.definitions'),
        icon: GitBranch,
        path: ROUTES.WORKFLOWS.DEFINITIONS,
      });
    }
    groups.push({ id: 'workflow', label: t('nav.groups.workflow'), items });
  }

  // Communication module (tenant-scoped only)
  if (activeModules.communication && tenantScoped) {
    const items: SidebarNavItem[] = [];
    if (hasPermission(PERMISSIONS.Communication.View)) {
      items.push({ label: t('nav.channels'), icon: MessageSquare, path: ROUTES.COMMUNICATION.CHANNELS });
      items.push({ label: t('nav.templates'), icon: FileText, path: ROUTES.COMMUNICATION.TEMPLATES });
      items.push({ label: t('nav.triggerRules'), icon: Zap, path: ROUTES.COMMUNICATION.TRIGGER_RULES });
      items.push({ label: t('nav.integrations'), icon: Link2, path: ROUTES.COMMUNICATION.INTEGRATIONS });
    }
    if (hasPermission(PERMISSIONS.Communication.ViewDeliveryLog)) {
      items.push({ label: t('nav.deliveryLog'), icon: ScrollText, path: ROUTES.COMMUNICATION.DELIVERY_LOG });
    }
    groups.push({ id: 'communication', label: t('nav.groups.communication'), items });
  }

  // Products module
  if (activeModules.products) {
    const items: SidebarNavItem[] = [];
    if (hasPermission(PERMISSIONS.Products.View)) {
      items.push({ label: t('nav.products', 'Products'), icon: Package, path: ROUTES.PRODUCTS.LIST });
    }
    groups.push({ id: 'products', label: t('nav.groups.products'), items });
  }

  // Billing module (tenant-scoped only for the main Billing page; Plans/Subscriptions reachable by platform admins too)
  if (activeModules.billing) {
    const items: SidebarNavItem[] = [];
    if (hasPermission(PERMISSIONS.Billing.View) && tenantScoped) {
      items.push({ label: t('nav.billing'), icon: CreditCard, path: ROUTES.BILLING, end: true });
    }
    if (hasPermission(PERMISSIONS.Billing.ViewPlans)) {
      items.push({ label: t('nav.billingPlans'), icon: ReceiptText, path: ROUTES.BILLING_PLANS });
    }
    if (hasPermission(PERMISSIONS.Billing.ManageTenantSubscriptions)) {
      items.push({ label: t('nav.subscriptions'), icon: ListChecks, path: ROUTES.SUBSCRIPTIONS.LIST, end: true });
    }
    groups.push({ id: 'billing', label: t('nav.groups.billing'), items });
  }

  // Webhooks module — tenant-scoped link only (the platform-admin link lives in the Platform group).
  if (activeModules.webhooks && tenantScoped && webhooksFlag.isEnabled) {
    const items: SidebarNavItem[] = [];
    if (hasPermission(PERMISSIONS.Webhooks.View)) {
      items.push({ label: t('nav.webhooks'), icon: Webhook, path: ROUTES.WEBHOOKS });
    }
    groups.push({ id: 'webhooks', label: t('nav.groups.webhooks'), items });
  }

  // Import / Export module
  if (activeModules.importExport) {
    const items: SidebarNavItem[] = [];
    const canExport = hasPermission(PERMISSIONS.System.ExportData) && exportsFlag.isEnabled;
    const canImport = hasPermission(PERMISSIONS.System.ImportData) && importsFlag.isEnabled;
    if (canExport || canImport) {
      items.push({ label: t('nav.importExport'), icon: ArrowLeftRight, path: ROUTES.IMPORT_EXPORT });
    }
    groups.push({ id: 'importExport', label: t('nav.groups.importExport'), items });
  }

  // People (core)
  {
    const items: SidebarNavItem[] = [];
    if (hasPermission(PERMISSIONS.Users.View)) {
      items.push({ label: t('nav.users'), icon: Users, path: ROUTES.USERS.LIST });
    }
    if (hasPermission(PERMISSIONS.Roles.View)) {
      items.push({ label: t('nav.roles'), icon: Shield, path: ROUTES.ROLES.LIST });
    }
    if (hasPermission(PERMISSIONS.Tenants.View)) {
      items.push(
        tenantScoped
          ? { label: t('nav.organization'), icon: Building, path: ROUTES.ORGANIZATION }
          : { label: t('nav.tenants'), icon: Building, path: ROUTES.TENANTS.LIST }
      );
    }
    groups.push({ id: 'people', label: t('nav.groups.people'), items });
  }

  // Content (core)
  {
    const items: SidebarNavItem[] = [];
    if (hasPermission(PERMISSIONS.Files.View)) {
      items.push({ label: t('nav.files'), icon: FolderOpen, path: ROUTES.FILES.LIST });
    }
    if (hasPermission(PERMISSIONS.System.ExportData)) {
      items.push({ label: t('nav.reports'), icon: FileBarChart2, path: ROUTES.REPORTS.LIST });
    }
    groups.push({ id: 'content', label: t('nav.groups.content'), items });
  }

  // Platform (core + cross-tenant admin)
  {
    const items: SidebarNavItem[] = [];
    if (hasPermission(PERMISSIONS.System.ViewAuditLogs)) {
      items.push({ label: t('nav.auditLogs'), icon: ClipboardList, path: ROUTES.AUDIT_LOGS.LIST });
    }
    if (hasPermission(PERMISSIONS.ApiKeys.View)) {
      items.push({ label: t('nav.apiKeys'), icon: KeyRound, path: ROUTES.API_KEYS.LIST });
    }
    if (hasPermission(PERMISSIONS.FeatureFlags.View)) {
      items.push({ label: t('nav.featureFlags'), icon: ToggleRight, path: ROUTES.FEATURE_FLAGS.LIST });
    }
    if (activeModules.webhooks && hasPermission(PERMISSIONS.Webhooks.ViewPlatform)) {
      items.push({ label: t('nav.webhooksAdmin'), icon: Webhook, path: ROUTES.WEBHOOKS_ADMIN.LIST, end: true });
    }
    if (hasPermission(PERMISSIONS.System.ManageSettings)) {
      items.push({ label: t('nav.settings'), icon: Settings2, path: ROUTES.SETTINGS });
    }
    groups.push({ id: 'platform', label: t('nav.groups.platform'), items });
  }

  // Strip empty groups so labels and dividers never render for nothing.
  return groups.filter((g) => g.items.length > 0);
}
```

- [ ] **Step 2.2: Rewrite `Sidebar.tsx` as a pure consumer**

Replace the full contents of `src/components/layout/MainLayout/Sidebar.tsx` with:

```tsx
import { NavLink } from 'react-router-dom';
import { ChevronsLeft, ChevronsRight } from 'lucide-react';

import { cn } from '@/lib/utils';
import { selectSidebarCollapsed, selectUser, useAuthStore, useUIStore } from '@/stores';
import { useNavGroups } from './useNavGroups';

export function Sidebar() {
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const toggleCollapse = useUIStore((state) => state.toggleSidebarCollapse);
  const user = useAuthStore(selectUser);
  const groups = useNavGroups();

  const tenantLogoUrl = user?.tenantLogoUrl;
  const tenantName = user?.tenantName;
  const appName = tenantName ?? import.meta.env.VITE_APP_NAME ?? 'Starter';

  return (
    <aside
      className={cn(
        'fixed top-0 z-40 flex h-screen flex-col surface-glass',
        'motion-safe:transition-all motion-safe:duration-300',
        'ltr:border-r rtl:border-l border-border/40',
        'w-60',
        isCollapsed && 'lg:w-16',
        'lg:translate-x-0 ltr:left-0 rtl:right-0'
        // Mobile drawer translate (`!sidebarOpen` → off-screen) is added in Task 4.
      )}
    >
      {/* Logo */}
      <div className={cn('flex h-14 items-center gap-2.5 px-5', isCollapsed && 'justify-center px-0')}>
        <button
          type="button"
          onClick={isCollapsed ? toggleCollapse : undefined}
          className={cn('flex items-center gap-2.5 min-w-0', isCollapsed && 'cursor-pointer')}
        >
          <div className="flex h-8 w-8 items-center justify-center rounded-lg btn-primary-gradient glow-primary-sm shrink-0">
            {tenantLogoUrl ? (
              <img src={tenantLogoUrl} alt={appName} className="h-7 w-7 rounded object-cover" />
            ) : (
              <span className="text-sm font-bold text-white">{appName.charAt(0)}</span>
            )}
          </div>
          {!isCollapsed && (
            <span className="text-lg font-semibold text-foreground tracking-tight">{appName}</span>
          )}
        </button>
        {!isCollapsed && (
          <button
            type="button"
            onClick={toggleCollapse}
            className="hidden lg:flex h-7 w-7 items-center justify-center rounded-md text-muted-foreground hover:text-foreground motion-safe:transition-colors motion-safe:duration-150 shrink-0 ltr:ml-auto rtl:mr-auto"
          >
            <ChevronsLeft className="h-[18px] w-[18px] rtl:rotate-180" />
          </button>
        )}
      </div>

      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto px-3 pt-2 pb-3">
        {groups.map((group, groupIndex) => (
          <div
            key={group.id}
            className={cn(groupIndex > 0 && !isCollapsed && 'mt-4 border-t border-border/40 pt-2')}
          >
            {!isCollapsed && group.label && (
              <div className="px-3 pb-1.5 pt-1 text-[10px] font-medium uppercase tracking-[0.08em] text-muted-foreground">
                {group.label}
              </div>
            )}
            <ul className="space-y-1">
              {group.items.map((item) => (
                <li key={item.path}>
                  <NavLink
                    to={item.path}
                    end={item.end}
                    className={({ isActive }) =>
                      cn(
                        'flex items-center gap-2.5 rounded-lg h-10 px-3 text-sm motion-safe:transition-all motion-safe:duration-150 cursor-pointer',
                        isCollapsed && 'justify-center px-0',
                        isActive ? 'state-active' : 'state-hover'
                      )
                    }
                  >
                    {({ isActive }) => (
                      <>
                        <item.icon
                          className={cn(
                            'h-[18px] w-[18px] shrink-0',
                            isActive && 'drop-shadow-[0_0_6px_color-mix(in_srgb,var(--color-primary)_45%,transparent)]'
                          )}
                        />
                        {!isCollapsed && <span className="flex-1">{item.label}</span>}
                        {!isCollapsed && item.badge != null && (
                          <span className="flex h-5 min-w-5 items-center justify-center rounded-full btn-primary-gradient glow-primary-sm px-1.5 text-[10px] font-bold text-primary-foreground font-mono">
                            {item.badge > 99 ? '99+' : item.badge}
                          </span>
                        )}
                      </>
                    )}
                  </NavLink>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </nav>

      {/* Collapsed: expand chevron (desktop only) */}
      {isCollapsed && (
        <div className="hidden lg:block p-2 border-t border-border">
          <button
            type="button"
            onClick={toggleCollapse}
            className="flex w-full items-center justify-center rounded-lg h-9 text-muted-foreground hover:bg-secondary hover:text-foreground motion-safe:transition-colors motion-safe:duration-150 cursor-pointer"
          >
            <ChevronsRight className="h-4 w-4 rtl:rotate-180" />
          </button>
        </div>
      )}
    </aside>
  );
}
```

Notes on the rewrite:
- All translation lives in `useNavGroups`; Sidebar never calls `useTranslation` directly. Future a11y labels on the chevron buttons should be added through `useNavGroups`'s return shape (e.g., expose a `chrome: { collapseLabel, expandLabel }` field) rather than re-introducing translation in the render component.
- The `end={item.end}` replacement removes the hand-rolled `item.path === ROUTES.DASHBOARD || ...` switch.
- Both collapse-chevron buttons gain `hidden lg:flex` / `hidden lg:block` so the desktop-only collapse UX never appears in the mobile drawer.
- All transitions become `motion-safe:` so users with `prefers-reduced-motion: reduce` get instant state changes — matches the conventions established by Phase 0 in `src/styles/index.css`.
- The mobile drawer translate-off class is intentionally **deferred to Task 4** so this commit produces a working desktop sidebar without half-implemented mobile behavior.

- [ ] **Step 2.3: Build + lint check**

```bash
npm run build && npm run lint
```

Expected: build succeeds, lint reports no new errors.

- [ ] **Step 2.4: Visual verification**

Sync the change into the test app. Open `http://localhost:3100/dashboard` in Chrome.

Expected:
- All previous nav items still present.
- Group eyebrow labels visible: "Workflow", "Communication", "Products", "Billing", "Webhooks", "Import / Export", "People", "Content", "Platform".
- Top "Dashboard / Notifications" block has no label and no top divider.
- Each subsequent labeled group has a 1 px tinted divider above its label.
- Empty groups (e.g., as a tenant member without `Workflows.ManageDefinitions`, the Workflow group should still appear with two items; if all gates fail it disappears) hide the label and divider together.
- Active link still highlights correctly (e.g., `/dashboard`, `/billing`).
- Task Inbox badge still renders if pending tasks exist.

- [ ] **Step 2.5: Commit**

```bash
git add boilerplateFE/src/components/layout/MainLayout/useNavGroups.ts \
        boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx
git commit -m "feat(fe/sidebar): typed groups via useNavGroups hook + eyebrow labels"
```

---

## Task 3: Collapsed-mode group separators

**Why:** Spec §4.4. In `w-16` mode the eyebrow labels disappear, but a thin divider per group keeps the visual rhythm so the icon column doesn't feel like one undifferentiated stack.

**Files:**
- Modify: `src/components/layout/MainLayout/Sidebar.tsx` (the group wrapper className only)

- [ ] **Step 3.1: Update the group wrapper className**

In `Sidebar.tsx`, find the group wrapper added in Task 2:

```tsx
<div
  key={group.id}
  className={cn(groupIndex > 0 && !isCollapsed && 'mt-4 border-t border-border/40 pt-2')}
>
```

Replace with:

```tsx
<div
  key={group.id}
  className={cn(
    groupIndex > 0 && (
      isCollapsed
        ? 'mx-3 my-2 border-t border-border/40'
        : 'mt-4 border-t border-border/40 pt-2'
    )
  )}
>
```

The collapsed branch produces a thin horizontal rule with horizontal margin so it doesn't span edge-to-edge — same visual language as a divider in a dropdown menu. The label `<div>` and items already gate on `!isCollapsed`, so labels stay hidden in collapsed mode without further changes.

- [ ] **Step 3.2: Build check**

```bash
npm run build
```

Expected: build succeeds.

- [ ] **Step 3.3: Visual verification**

Open `http://localhost:3100/dashboard`, click the collapse chevron at the top of the sidebar, then expand again.

Expected (collapsed `w-16`):
- Icon-only column.
- Thin horizontal separator between groups (Dashboard/Notifications block → Workflow icons → Communication icons → ...).
- No labels, no extra padding.

Expected (expanded `w-60`):
- No regression from Task 2.

- [ ] **Step 3.4: Commit**

```bash
git add boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx
git commit -m "feat(fe/sidebar): thin separators between groups in collapsed mode"
```

---

## Task 4: Mobile drawer behavior (`<lg`)

**Why:** Spec §4.5 and §7. On viewports `<lg` (< 1024 px) the desktop fixed sidebar is hostile — it overlaps the content. Convert to a slide-out drawer with backdrop, body scroll lock, route-change auto-close, Esc auto-close, and a Header trigger that swaps Menu ↔ X to mirror the drawer state.

**Files:**
- Modify: `src/stores/ui.store.ts` (default `sidebarOpen: false`)
- Modify: `src/components/layout/MainLayout/Sidebar.tsx` (drawer translate class + auto-close effects)
- Modify: `src/components/layout/MainLayout/MainLayout.tsx` (backdrop overlay, body scroll lock, responsive `<main>` padding)
- Modify: `src/components/layout/MainLayout/Header.tsx` (Menu ↔ X icon swap, accessible label)

- [ ] **Step 4.1: Default `sidebarOpen` to `false`**

In `src/stores/ui.store.ts`, find:

```ts
sidebarOpen: true,
```

Change to:

```ts
sidebarOpen: false,
```

This is the initial mobile-drawer state — closed on first paint of any `<lg` viewport. On `lg+` viewports the responsive classes added in Step 4.2 ignore `sidebarOpen` entirely.

- [ ] **Step 4.2: Add drawer translate + auto-close effects to Sidebar**

In `src/components/layout/MainLayout/Sidebar.tsx`:

1. Update the `react` and `react-router-dom` imports to add `useEffect` and `useLocation`:

```tsx
import { useEffect } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
```

2. Update the `@/stores` import to add the open-state pieces:

```tsx
import {
  selectSidebarCollapsed,
  selectSidebarOpen,
  selectUser,
  useAuthStore,
  useUIStore,
} from '@/stores';
```

3. Inside the component body, after the existing hooks (`isCollapsed`, `toggleCollapse`, `user`, `groups`), add:

```tsx
const sidebarOpen = useUIStore(selectSidebarOpen);
const setSidebarOpen = useUIStore((state) => state.setSidebarOpen);
const location = useLocation();

// Auto-close mobile drawer on route change. Harmless on desktop (`sidebarOpen`
// has no UI effect at lg+).
useEffect(() => {
  setSidebarOpen(false);
}, [location.pathname, setSidebarOpen]);

// Auto-close on Escape while the drawer is open.
useEffect(() => {
  if (!sidebarOpen) return;
  const onKeyDown = (e: KeyboardEvent) => {
    if (e.key === 'Escape') setSidebarOpen(false);
  };
  document.addEventListener('keydown', onKeyDown);
  return () => document.removeEventListener('keydown', onKeyDown);
}, [sidebarOpen, setSidebarOpen]);
```

4. Update the `<aside>` className to add the mobile-drawer translate. Find:

```tsx
<aside
  className={cn(
    'fixed top-0 z-40 flex h-screen flex-col surface-glass',
    'motion-safe:transition-all motion-safe:duration-300',
    'ltr:border-r rtl:border-l border-border/40',
    'w-60',
    isCollapsed && 'lg:w-16',
    'lg:translate-x-0 ltr:left-0 rtl:right-0'
  )}
>
```

Replace with:

```tsx
<aside
  className={cn(
    'fixed top-0 z-40 flex h-screen flex-col surface-glass',
    'motion-safe:transition-all motion-safe:duration-300',
    'ltr:border-r rtl:border-l border-border/40',
    'w-60',
    isCollapsed && 'lg:w-16',
    'lg:translate-x-0 ltr:left-0 rtl:right-0',
    !sidebarOpen && 'max-lg:ltr:-translate-x-full max-lg:rtl:translate-x-full'
  )}
>
```

Behavior summary:
- `lg+`: `lg:translate-x-0` always wins; `sidebarOpen` is irrelevant.
- `<lg`, `sidebarOpen=false`: `-translate-x-full` (or `+translate-x-full` in RTL) hides the drawer off-screen.
- `<lg`, `sidebarOpen=true`: no translate override; drawer renders at `left-0` / `right-0` over the page.

- [ ] **Step 4.3: Add backdrop, body scroll lock, and responsive `<main>` padding in MainLayout**

In `src/components/layout/MainLayout/MainLayout.tsx`:

Replace the file contents with:

```tsx
import { useEffect } from 'react';
import { Outlet } from 'react-router-dom';

import { RouteErrorBoundary } from '@/components/common';
import { OnboardingWizard } from '@/features/onboarding/components/OnboardingWizard';
import { useOnboardingCheck } from '@/features/onboarding/hooks/useOnboardingCheck';
import { cn } from '@/lib/utils';
import { selectSidebarCollapsed, selectSidebarOpen, useUIStore } from '@/stores';

import { Header } from './Header';
import { Sidebar } from './Sidebar';

export function MainLayout() {
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const sidebarOpen = useUIStore(selectSidebarOpen);
  const setSidebarOpen = useUIStore((state) => state.setSidebarOpen);
  const { showOnboarding, completeOnboarding, remindLater } = useOnboardingCheck();

  // Lock body scroll while the mobile drawer is open. The `lg:hidden` backdrop
  // already gates the visual; this prevents the page behind from scrolling on touch.
  useEffect(() => {
    if (!sidebarOpen) return;
    const previous = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = previous;
    };
  }, [sidebarOpen]);

  if (showOnboarding) {
    return (
      <OnboardingWizard onComplete={completeOnboarding} onRemindLater={remindLater} />
    );
  }

  return (
    <div className="aurora-canvas min-h-screen bg-background" data-page-style="dense">
      <Sidebar />
      <Header />
      {/* Mobile drawer backdrop — only renders when open, only visible <lg */}
      {sidebarOpen && (
        <button
          type="button"
          aria-label="Close navigation"
          className="fixed inset-0 z-30 bg-background/60 backdrop-blur-sm lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}
      <main
        className={cn(
          'pt-16 motion-safe:transition-all motion-safe:duration-300',
          // No left padding on <lg — sidebar is a drawer, not in flow
          'pl-0',
          isCollapsed ? 'lg:ltr:pl-16 lg:rtl:pr-16' : 'lg:ltr:pl-60 lg:rtl:pr-60'
        )}
      >
        <div className="p-8">
          <RouteErrorBoundary>
            <Outlet />
          </RouteErrorBoundary>
        </div>
      </main>
    </div>
  );
}
```

Notes:
- The backdrop is a `<button>` (not a `<div>` with `onClick`) so keyboard users can dismiss it with Enter / Space, and screen readers announce it as "Close navigation". This is the lightweight a11y win without pulling Radix Dialog into a custom drawer.
- `lg:hidden` on the backdrop guarantees it never renders on desktop even if `sidebarOpen` somehow becomes `true`.
- The body scroll lock is gated on `sidebarOpen` only. On desktop the drawer is always visible at its w-16/w-60 width and the body never scrolls because of the drawer — no harm in locking briefly if a user rapidly toggles state across breakpoints.
- The `motion-safe:` prefix matches the Sidebar's transitions for a unified feel.

- [ ] **Step 4.4: Swap the Header trigger icon Menu ↔ X**

In `src/components/layout/MainLayout/Header.tsx`:

1. Update the lucide imports to include `X` and replace the static `Menu` reference:

```tsx
import { LogOut, User, Menu, X, ArrowLeft } from 'lucide-react';
```

2. Update the `@/stores` import to add `selectSidebarOpen`:

```tsx
import {
  useAuthStore,
  selectUser,
  useUIStore,
  selectSidebarCollapsed,
  selectSidebarOpen,
  selectBackNavigation,
} from '@/stores';
```

3. Inside the component, add:

```tsx
const sidebarOpen = useUIStore(selectSidebarOpen);
```

4. Replace the Menu button:

```tsx
<Button variant="ghost" size="icon" onClick={toggleSidebar} className="lg:hidden">
  <Menu className="h-5 w-5" />
</Button>
```

with:

```tsx
<Button
  variant="ghost"
  size="icon"
  onClick={toggleSidebar}
  className="lg:hidden"
  aria-label={sidebarOpen ? 'Close navigation' : 'Open navigation'}
  aria-expanded={sidebarOpen}
>
  {sidebarOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
</Button>
```

This unifies the trigger experience: same button position, content swaps to mirror state, and screen readers announce the live state via `aria-expanded`.

- [ ] **Step 4.5: Build + lint check**

```bash
npm run build && npm run lint
```

Expected: build succeeds, no new lint errors.

- [ ] **Step 4.6: Visual verification — desktop**

At `lg+` viewport (≥ 1024 px), reload `http://localhost:3100/dashboard`.

Expected:
- Sidebar visible at start edge, no regression.
- Collapse / expand chevrons work.
- Backdrop never appears.
- Body scroll works normally.
- Header's mobile menu button is hidden.

- [ ] **Step 4.7: Visual verification — mobile**

Open Chrome DevTools' device toolbar (`Cmd+Shift+M`) and switch to a 768 px viewport.

Expected on first load:
- Sidebar **off-screen** to the start side.
- `<main>` content fills the full viewport width.
- Header's icon-only Menu button is visible.

Click the Menu button:
- Button icon swaps Menu → X.
- Sidebar slides in over the content.
- Backdrop appears over the rest of the viewport.
- Body scroll is locked (try scrolling — no movement).

Click the backdrop:
- Sidebar slides out.
- Header button icon swaps X → Menu.
- Body scroll resumes.

Open the drawer again, click any nav link:
- Drawer auto-closes after navigation.
- Lands on the new page with the sidebar hidden.

Open the drawer again, press `Esc`:
- Drawer closes.

Switch language to Arabic (LanguageSwitcher → AR):
- Drawer slides in from the **end** (right) edge.
- Backdrop / route-close behavior unchanged.

In `prefers-reduced-motion: reduce` (DevTools → Rendering → Emulate CSS media feature):
- Open / close transitions are instant; no slide animation.
- All other behavior identical.

- [ ] **Step 4.8: Commit**

```bash
git add boilerplateFE/src/stores/ui.store.ts \
        boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx \
        boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx \
        boilerplateFE/src/components/layout/MainLayout/Header.tsx
git commit -m "feat(fe/layout): mobile sidebar drawer (backdrop, scroll-lock, Menu↔X swap)"
```

---

## Task 5: Code-review pass

**Why:** Phase 0 cadence (per spec §9). Catch regressions before stacking Plan B / C on top.

- [ ] **Step 5.1: Dispatch code-reviewer subagent**

Invoke the `superpowers:code-reviewer` subagent against the diff for this plan (Tasks 1-4). Brief:

> Review all commits on `fe/redesign-phase-1` since the spec commit (`6652ce91`):
> - i18n keys for `nav.groups.*`
> - `useNavGroups` hook + Sidebar typed groups + expanded-mode eyebrow labels & dividers
> - Collapsed-mode separators
> - Mobile drawer (sidebarOpen state, backdrop, body scroll-lock, responsive translate, Header Menu↔X swap)
>
> Verify against `docs/superpowers/specs/2026-04-27-redesign-phase-1-design.md` §4.
>
> Specific things to check:
> 1. No new `dark:` overrides for primary colors and no hardcoded `primary-{shade}` classes (CLAUDE.md Frontend Rules).
> 2. RTL works for the mobile drawer — translate direction flips, backdrop click still closes.
> 3. Empty-group hide logic — sign in as a tenant member with limited permissions and confirm groups with zero items hide their label and divider together.
> 4. The `end` flag on individual nav items correctly replaces the prior hand-rolled exact-match switch (Dashboard, Billing root, Subscriptions list, Webhooks Admin list).
> 5. No regression to the desktop `lg+` layout — Sidebar / Header / Main padding still correct in both expanded and collapsed states.
> 6. Tailwind 4 `max-lg:` and `motion-safe:` variants are honored in the project's Tailwind config.
> 7. Body scroll lock cleans up correctly on unmount and on `sidebarOpen → false` (no orphaned `overflow: hidden`).
> 8. The Menu↔X swap keeps `aria-label` and `aria-expanded` in sync with state.

Address any findings with follow-up commits before proceeding to Plan B.

---

## Self-Review

**Spec coverage (§4 of the design doc):**
- §4.1 Group structure → Task 2 (`useNavGroups` builds the same 9 groups + the unlabeled top block, in the same order, with the same per-item gates).
- §4.2 Data shape → Task 2 (`SidebarNavItem`, `SidebarNavGroup` exported from `useNavGroups.ts`).
- §4.3 Expanded visual treatment → Task 2 (eyebrow label classes, divider, item rows, `motion-safe:` transitions).
- §4.4 Collapsed visual treatment → Task 3.
- §4.5 Mobile drawer → Task 4 (translate, backdrop, scroll-lock, route + Esc auto-close, Menu↔X icon).
- §4.6 i18n keys → Task 1.
- §4.7 Logo, collapse toggle, tenant logo logic — explicitly preserved in Task 2's full rewrite.

**Placeholder scan:** No `TBD` / `TODO` / "implement later" / vague error-handling references. Every step has concrete code or commands.

**Type consistency check:**
- `SidebarNavItem` and `SidebarNavGroup` defined once in `useNavGroups.ts` and imported nowhere else (Sidebar uses them via the return type of `useNavGroups()`).
- `sidebarOpen` / `setSidebarOpen` / `selectSidebarOpen` consistent across `ui.store.ts`, `Sidebar.tsx`, `MainLayout.tsx`, `Header.tsx`.
- `isCollapsed` (local React var) maps to `selectSidebarCollapsed` consistently.
- The `end?: boolean` flag matches the `NavLink`'s `end` prop type.

**Variable / class unification:**
- All transitions use `motion-safe:transition-* motion-safe:duration-*` (matches Phase 0 conventions in `src/styles/index.css`).
- All `cn()` arrays group concerns: positioning → border → width → translate. No mixed concerns inside one ternary.
- All buttons that mutate state include `type="button"` to prevent unintended form submissions inside ancestor forms.

**End-user experience unification:**
- Menu and X share the Header position and toggle behavior; `aria-label` and `aria-expanded` reflect state.
- Drawer closes via three independent paths (backdrop, Esc, route change) — never a dead-end.
- Body scroll locks while the drawer covers the page so the content behind doesn't shift under touch.
- `prefers-reduced-motion` users skip every slide animation; state changes are instant.
- RTL is handled via `ltr:` / `rtl:` Tailwind variants on every directional class — no manual flips needed.

**Out of scope here (deferred to later plans):**
- Header `⌘K` palette → Plan B
- `PageHeader` `breadcrumbs` / `tabs` props → Plan C
- Identity cluster page polish → Plans D / E
