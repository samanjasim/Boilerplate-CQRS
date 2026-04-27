# Sidebar Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the flat 25-item sidebar into a modules-first grouped navigation with mobile drawer behavior, exactly as specified in §4 of `docs/superpowers/specs/2026-04-27-redesign-phase-1-design.md` (Plan A).

**Architecture:** A typed `SidebarNavGroup[]` data structure replaces the current flat `navItems` array. Group eyebrow labels + 1px tinted dividers render in expanded mode; thin separators only in collapsed mode. Empty groups (all items filtered by permissions / module flags) hide entirely. On viewports `<lg`, the sidebar leaves the layout flow and becomes a slide-out drawer driven by the existing (currently unused) `useUIStore.sidebarOpen` state, with a backdrop click + Esc + route-change auto-close.

**Tech Stack:** React 19, TypeScript 5.9, Tailwind CSS 4, Zustand 5, react-router-dom 7, react-i18next 15, lucide-react.

**Verification model:** This codebase has no FE unit-test runner. Every task verifies with (1) `npm run build` (type/build check), (2) `npm run lint` (ESLint), and (3) a visual pass against the running test app at `http://localhost:3100`. The test app is the same Phase 0 harness — `_testJ4visual/` if it still exists, or re-spin via `pwsh scripts/rename.ps1 -Name "_testJ4visual" -OutputDir "."`. **All edits go in `boilerplateFE/src/...`** and are then copied (or hot-reloaded if symlinked) into `_testJ4visual/boilerplateFE/`.

**Working directory for all commands:** `/Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE` unless stated otherwise.

---

## Task 1: Add `nav.groups.*` i18n keys (EN only)

**Why:** Sidebar groups need labels. Phase 1 spec §4.6 lists exactly these keys. AR/KU fall back to EN automatically via i18next — see deferred item in spec §3.

**Files:**
- Modify: `src/i18n/locales/en/translation.json` (add `nav.groups` block)

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

Run from `boilerplateFE/`:

```bash
npm run build
```

Expected: build succeeds with no errors. (No code changes consume the keys yet — this is purely a JSON addition.)

- [ ] **Step 1.3: Commit**

From the repo root:

```bash
git add boilerplateFE/src/i18n/locales/en/translation.json
git commit -m "feat(fe/i18n): add nav.groups.* keys for sidebar grouping"
```

---

## Task 2: Refactor Sidebar to grouped data + expanded-mode render

**Why:** Spec §4.1, §4.2, §4.3. Replace the flat `navItems` array with a typed `SidebarNavGroup[]`, render eyebrow labels and dividers in expanded mode, and ensure empty groups (all items filtered out) hide entirely.

**Files:**
- Modify: `src/components/layout/MainLayout/Sidebar.tsx` (full rewrite of the body — keep imports + collapse-toggle UI as-is)

- [ ] **Step 2.1: Replace the body of Sidebar.tsx**

Open `src/components/layout/MainLayout/Sidebar.tsx`. Keep the existing imports block (lines 1-38) and the existing logo/toggle JSX. Replace the `navItems` array, the nav `<ul>` rendering, and the surrounding return statement so the file matches this structure end-to-end:

```tsx
import { NavLink } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  LayoutDashboard,
  Users,
  Shield,
  ChevronsLeft,
  ChevronsRight,
  ClipboardList,
  Building,
  FolderOpen,
  Settings2,
  KeyRound,
  ToggleRight,
  CreditCard,
  ReceiptText,
  ListChecks,
  Webhook,
  ArrowLeftRight,
  Package,
  MessageSquare,
  FileText,
  Zap,
  Link2,
  ScrollText,
  ClipboardCheck,
  History,
  GitBranch,
  FileBarChart2,
  Bell,
  type LucideIcon,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { useUIStore, useAuthStore, selectSidebarCollapsed, selectUser } from '@/stores';
import { ROUTES } from '@/config';
import { activeModules, isModuleActive } from '@/config/modules.config';
import { usePermissions, useFeatureFlag } from '@/hooks';
import { PERMISSIONS } from '@/constants';
import { usePendingTaskCount } from '@/features/workflow/api';

interface SidebarNavItem {
  label: string;
  icon: LucideIcon;
  path: string;
  end?: boolean;
  badge?: number;
}

interface SidebarNavGroup {
  id: string;
  label?: string;
  items: SidebarNavItem[];
}

export function Sidebar() {
  const { t } = useTranslation();
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const toggleCollapse = useUIStore((state) => state.toggleSidebarCollapse);
  const { hasPermission } = usePermissions();
  const user = useAuthStore(selectUser);

  const webhooksFlag = useFeatureFlag('webhooks.enabled');
  const importsFlag = useFeatureFlag('imports.enabled');
  const exportsFlag = useFeatureFlag('exports.enabled');

  const { data: pendingTaskCount = 0 } = usePendingTaskCount(isModuleActive('workflow'));

  const tenantLogoUrl = user?.tenantLogoUrl;
  const tenantName = user?.tenantName;
  const appName = tenantName ?? import.meta.env.VITE_APP_NAME ?? 'Starter';

  const groups: SidebarNavGroup[] = [
    // Top block (no label)
    {
      id: 'top',
      items: [
        { label: t('nav.dashboard'), icon: LayoutDashboard, path: ROUTES.DASHBOARD, end: true },
        { label: t('nav.notifications'), icon: Bell, path: ROUTES.NOTIFICATIONS },
      ],
    },
    // Workflow module
    {
      id: 'workflow',
      label: t('nav.groups.workflow'),
      items: [
        ...(activeModules.workflow && hasPermission(PERMISSIONS.Workflows.View)
          ? [{
              label: t('workflow.sidebar.taskInbox'),
              icon: ClipboardCheck,
              path: ROUTES.WORKFLOWS.INBOX,
              badge: pendingTaskCount > 0 ? pendingTaskCount : undefined,
            }]
          : []),
        ...(activeModules.workflow && hasPermission(PERMISSIONS.Workflows.View)
          ? [{ label: t('workflow.sidebar.history'), icon: History, path: ROUTES.WORKFLOWS.INSTANCES }]
          : []),
        ...(activeModules.workflow && hasPermission(PERMISSIONS.Workflows.ManageDefinitions)
          ? [{ label: t('workflow.sidebar.definitions'), icon: GitBranch, path: ROUTES.WORKFLOWS.DEFINITIONS }]
          : []),
      ],
    },
    // Communication module
    {
      id: 'communication',
      label: t('nav.groups.communication'),
      items: [
        ...(activeModules.communication && hasPermission(PERMISSIONS.Communication.View) && user?.tenantId
          ? [
              { label: t('nav.channels'), icon: MessageSquare, path: ROUTES.COMMUNICATION.CHANNELS },
              { label: t('nav.templates'), icon: FileText, path: ROUTES.COMMUNICATION.TEMPLATES },
              { label: t('nav.triggerRules'), icon: Zap, path: ROUTES.COMMUNICATION.TRIGGER_RULES },
              { label: t('nav.integrations'), icon: Link2, path: ROUTES.COMMUNICATION.INTEGRATIONS },
            ]
          : []),
        ...(activeModules.communication && hasPermission(PERMISSIONS.Communication.ViewDeliveryLog) && user?.tenantId
          ? [{ label: t('nav.deliveryLog'), icon: ScrollText, path: ROUTES.COMMUNICATION.DELIVERY_LOG }]
          : []),
      ],
    },
    // Products module
    {
      id: 'products',
      label: t('nav.groups.products'),
      items: [
        ...(activeModules.products && hasPermission(PERMISSIONS.Products.View)
          ? [{ label: t('nav.products', 'Products'), icon: Package, path: ROUTES.PRODUCTS.LIST }]
          : []),
      ],
    },
    // Billing module
    {
      id: 'billing',
      label: t('nav.groups.billing'),
      items: [
        ...(activeModules.billing && hasPermission(PERMISSIONS.Billing.View) && user?.tenantId
          ? [{ label: t('nav.billing'), icon: CreditCard, path: ROUTES.BILLING, end: true }]
          : []),
        ...(activeModules.billing && hasPermission(PERMISSIONS.Billing.ViewPlans)
          ? [{ label: t('nav.billingPlans'), icon: ReceiptText, path: ROUTES.BILLING_PLANS }]
          : []),
        ...(activeModules.billing && hasPermission(PERMISSIONS.Billing.ManageTenantSubscriptions)
          ? [{ label: t('nav.subscriptions'), icon: ListChecks, path: ROUTES.SUBSCRIPTIONS.LIST, end: true }]
          : []),
      ],
    },
    // Webhooks module (tenant-scoped only)
    {
      id: 'webhooks',
      label: t('nav.groups.webhooks'),
      items: [
        ...(activeModules.webhooks && hasPermission(PERMISSIONS.Webhooks.View) && user?.tenantId && webhooksFlag.isEnabled
          ? [{ label: t('nav.webhooks'), icon: Webhook, path: ROUTES.WEBHOOKS }]
          : []),
      ],
    },
    // Import / Export module
    {
      id: 'importExport',
      label: t('nav.groups.importExport'),
      items: [
        ...(activeModules.importExport && ((hasPermission(PERMISSIONS.System.ExportData) && exportsFlag.isEnabled) || (hasPermission(PERMISSIONS.System.ImportData) && importsFlag.isEnabled))
          ? [{ label: t('nav.importExport'), icon: ArrowLeftRight, path: ROUTES.IMPORT_EXPORT }]
          : []),
      ],
    },
    // People (core)
    {
      id: 'people',
      label: t('nav.groups.people'),
      items: [
        ...(hasPermission(PERMISSIONS.Users.View)
          ? [{ label: t('nav.users'), icon: Users, path: ROUTES.USERS.LIST }]
          : []),
        ...(hasPermission(PERMISSIONS.Roles.View)
          ? [{ label: t('nav.roles'), icon: Shield, path: ROUTES.ROLES.LIST }]
          : []),
        ...(hasPermission(PERMISSIONS.Tenants.View)
          ? [
              user?.tenantId
                ? { label: t('nav.organization'), icon: Building, path: ROUTES.ORGANIZATION }
                : { label: t('nav.tenants'), icon: Building, path: ROUTES.TENANTS.LIST },
            ]
          : []),
      ],
    },
    // Content (core)
    {
      id: 'content',
      label: t('nav.groups.content'),
      items: [
        ...(hasPermission(PERMISSIONS.Files.View)
          ? [{ label: t('nav.files'), icon: FolderOpen, path: ROUTES.FILES.LIST }]
          : []),
        ...(hasPermission(PERMISSIONS.System.ExportData)
          ? [{ label: t('nav.reports'), icon: FileBarChart2, path: ROUTES.REPORTS.LIST }]
          : []),
      ],
    },
    // Platform (core)
    {
      id: 'platform',
      label: t('nav.groups.platform'),
      items: [
        ...(hasPermission(PERMISSIONS.System.ViewAuditLogs)
          ? [{ label: t('nav.auditLogs'), icon: ClipboardList, path: ROUTES.AUDIT_LOGS.LIST }]
          : []),
        ...(hasPermission(PERMISSIONS.ApiKeys.View)
          ? [{ label: t('nav.apiKeys'), icon: KeyRound, path: ROUTES.API_KEYS.LIST }]
          : []),
        ...(hasPermission(PERMISSIONS.FeatureFlags.View)
          ? [{ label: t('nav.featureFlags'), icon: ToggleRight, path: ROUTES.FEATURE_FLAGS.LIST }]
          : []),
        ...(activeModules.webhooks && hasPermission(PERMISSIONS.Webhooks.ViewPlatform)
          ? [{ label: t('nav.webhooksAdmin'), icon: Webhook, path: ROUTES.WEBHOOKS_ADMIN.LIST, end: true }]
          : []),
        ...(hasPermission(PERMISSIONS.System.ManageSettings)
          ? [{ label: t('nav.settings'), icon: Settings2, path: ROUTES.SETTINGS }]
          : []),
      ],
    },
  ];

  // Drop empty groups so labels and dividers don't render for nothing.
  const visibleGroups = groups.filter((g) => g.items.length > 0);

  return (
    <aside
      className={cn(
        'fixed top-0 z-40 flex h-screen flex-col surface-glass transition-all duration-300',
        'ltr:left-0 ltr:border-r rtl:right-0 rtl:border-l border-border/40',
        isCollapsed ? 'w-16' : 'w-60'
      )}
    >
      {/* Logo */}
      <div className={cn('flex h-14 items-center gap-2.5 px-5', isCollapsed && 'justify-center px-0')}>
        <button
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
            onClick={toggleCollapse}
            className="flex h-7 w-7 items-center justify-center rounded-md text-muted-foreground hover:text-foreground transition-colors duration-150 shrink-0 ltr:ml-auto rtl:mr-auto"
          >
            <ChevronsLeft className="h-[18px] w-[18px] rtl:rotate-180" />
          </button>
        )}
      </div>

      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto px-3 pt-2 pb-3">
        {visibleGroups.map((group, groupIndex) => (
          <div
            key={group.id}
            className={cn(
              groupIndex > 0 && !isCollapsed && 'mt-4 border-t border-border/40 pt-2'
            )}
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
                        'flex items-center gap-2.5 rounded-lg h-10 px-3 text-sm transition-all duration-150 cursor-pointer',
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

      {/* Collapsed: expand */}
      {isCollapsed && (
        <div className="p-2 border-t border-border">
          <button
            onClick={toggleCollapse}
            className="flex w-full items-center justify-center rounded-lg h-9 text-muted-foreground hover:bg-secondary hover:text-foreground transition-colors duration-150 cursor-pointer"
          >
            <ChevronsRight className="h-4 w-4 rtl:rotate-180" />
          </button>
        </div>
      )}
    </aside>
  );
}
```

Notes:
- The `end={item.end}` replacement removes the hand-rolled `item.path === ROUTES.DASHBOARD || item.path === ROUTES.BILLING || ...` switch from the old code. Items that need exact-match (Dashboard, Billing root, Subscriptions list, Webhooks Admin list) carry `end: true` in their definitions above.
- The `pb-3` on `<nav>` prevents the last group's items from butting against the bottom edge.
- `groupIndex > 0` guards the divider so the first **rendered** group (after empty-group filtering) never gets a top divider.

- [ ] **Step 2.2: Build + lint check**

```bash
npm run build && npm run lint
```

Expected: build succeeds, lint reports no new errors.

- [ ] **Step 2.3: Visual verification**

Sync the change into the test app (or restart it pointing at the source). Open `http://localhost:3100/dashboard` in Chrome with the test app running.

Expected:
- All previous nav items still present.
- Group eyebrow labels visible: "Workflow", "Communication", "Products", "Billing", "Webhooks", "Import / Export", "People", "Content", "Platform".
- Top "Dashboard / Notifications" block has no label and no top divider.
- Each subsequent labeled group has a 1 px tinted divider above its label.
- Active link still highlights correctly (e.g., `/dashboard`).
- Task Inbox badge still renders if pending tasks exist.

If any group is empty for the test user, its label and divider should be absent.

- [ ] **Step 2.4: Commit**

From the repo root:

```bash
git add boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx
git commit -m "feat(fe/sidebar): typed groups + expanded-mode eyebrow labels & dividers"
```

---

## Task 3: Collapsed-mode group separators

**Why:** Spec §4.4. In `w-16` mode the eyebrow labels disappear, but a thin divider per group keeps the visual rhythm so the icon column doesn't feel like one undifferentiated stack.

**Files:**
- Modify: `src/components/layout/MainLayout/Sidebar.tsx` (the group wrapper `<div>` className)

- [ ] **Step 3.1: Update the group wrapper className**

Find the group wrapper in `Sidebar.tsx` (added in Task 2):

```tsx
<div
  key={group.id}
  className={cn(
    groupIndex > 0 && !isCollapsed && 'mt-4 border-t border-border/40 pt-2'
  )}
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

The collapsed branch produces a thin horizontal rule with horizontal margin so it doesn't span edge-to-edge — same look as a divider in a dropdown menu. The label `<div>` and items already gate on `!isCollapsed`, so labels stay hidden in collapsed mode without changes.

- [ ] **Step 3.2: Build check**

```bash
npm run build
```

Expected: build succeeds.

- [ ] **Step 3.3: Visual verification**

Open `http://localhost:3100/dashboard`, click the collapse chevron at the top of the sidebar. Then expand again.

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

**Why:** Spec §4.5 and §7. On viewports `<lg` (< 1024 px) the desktop fixed sidebar is hostile — it overlaps the content. Convert to a slide-out drawer with backdrop, driven by the existing `useUIStore.sidebarOpen` state (currently unused).

**Files:**
- Modify: `src/stores/ui.store.ts` (default `sidebarOpen: false` instead of `true`)
- Modify: `src/components/layout/MainLayout/Sidebar.tsx` (responsive classes + route-change auto-close)
- Modify: `src/components/layout/MainLayout/MainLayout.tsx` (backdrop overlay + `<main>` padding swap)

- [ ] **Step 4.1: Default `sidebarOpen` to `false`**

In `src/stores/ui.store.ts`, find:

```ts
sidebarOpen: true,
```

Change to:

```ts
sidebarOpen: false,
```

This is the initial mobile-drawer state — closed on first paint of any `<lg` viewport. On `lg+` viewports the responsive classes added in 4.2 ignore this state entirely.

- [ ] **Step 4.2: Make Sidebar responsive + auto-close on route change**

In `src/components/layout/MainLayout/Sidebar.tsx`:

1. Add `selectSidebarOpen` to the `useUIStore` import:

```ts
import {
  useUIStore,
  useAuthStore,
  selectSidebarCollapsed,
  selectSidebarOpen,
  selectUser,
} from '@/stores';
```

2. Add `useLocation` to the `react-router-dom` import:

```ts
import { NavLink, useLocation } from 'react-router-dom';
```

3. Add `useEffect` to the React import (the file currently imports nothing from `react`; add a direct import):

```ts
import { useEffect } from 'react';
```

4. Inside the `Sidebar` component body, after the existing hooks, add:

```ts
const isOpen = useUIStore(selectSidebarOpen);
const setSidebarOpen = useUIStore((state) => state.setSidebarOpen);
const location = useLocation();

// Auto-close mobile drawer on route change
useEffect(() => {
  setSidebarOpen(false);
}, [location.pathname, setSidebarOpen]);

// Auto-close on Esc
useEffect(() => {
  if (!isOpen) return;
  const onKeyDown = (e: KeyboardEvent) => {
    if (e.key === 'Escape') setSidebarOpen(false);
  };
  document.addEventListener('keydown', onKeyDown);
  return () => document.removeEventListener('keydown', onKeyDown);
}, [isOpen, setSidebarOpen]);
```

5. Update the `<aside>` className to swap fixed-positioning behavior at `<lg`. Find:

```tsx
<aside
  className={cn(
    'fixed top-0 z-40 flex h-screen flex-col surface-glass transition-all duration-300',
    'ltr:left-0 ltr:border-r rtl:right-0 rtl:border-l border-border/40',
    isCollapsed ? 'w-16' : 'w-60'
  )}
>
```

Replace with:

```tsx
<aside
  className={cn(
    'fixed top-0 z-40 flex h-screen flex-col surface-glass transition-all duration-300',
    'ltr:border-r rtl:border-l border-border/40',
    // Width: drawer is always w-60 on <lg; on lg+ it follows the collapse state
    'w-60',
    isCollapsed && 'lg:w-16',
    // Position: desktop sits at start edge; mobile slides in from start when open
    'lg:translate-x-0 ltr:left-0 rtl:right-0',
    !isOpen && 'max-lg:ltr:-translate-x-full max-lg:rtl:translate-x-full'
  )}
>
```

Notes:
- The `max-lg:` Tailwind 4 prefix targets `<lg` only.
- On `lg+` the sidebar is always visible at its `w-16` / `w-60` width (controlled by `isCollapsed`).
- On `<lg` the sidebar is always `w-60` and translates fully off-screen unless `isOpen` is `true`.
- `transition-all duration-300` already covers the slide animation.

6. Hide the desktop-only collapse toggle at `<lg`. Find both `toggleCollapse` buttons (the small `ChevronsLeft` in the logo row and the bottom `ChevronsRight` in collapsed mode). Wrap each in a `<div className="hidden lg:flex">` or add `hidden lg:flex` to the button itself.

The expand-chevron block at the bottom (collapsed mode):

```tsx
{isCollapsed && (
  <div className="p-2 border-t border-border">
```

becomes:

```tsx
{isCollapsed && (
  <div className="hidden lg:block p-2 border-t border-border">
```

The collapse-chevron in the header row:

```tsx
{!isCollapsed && (
  <button
    onClick={toggleCollapse}
    className="flex h-7 w-7 items-center justify-center rounded-md text-muted-foreground hover:text-foreground transition-colors duration-150 shrink-0 ltr:ml-auto rtl:mr-auto"
  >
    <ChevronsLeft className="h-[18px] w-[18px] rtl:rotate-180" />
  </button>
)}
```

becomes:

```tsx
{!isCollapsed && (
  <button
    onClick={toggleCollapse}
    className="hidden lg:flex h-7 w-7 items-center justify-center rounded-md text-muted-foreground hover:text-foreground transition-colors duration-150 shrink-0 ltr:ml-auto rtl:mr-auto"
  >
    <ChevronsLeft className="h-[18px] w-[18px] rtl:rotate-180" />
  </button>
)}
```

- [ ] **Step 4.3: Add backdrop overlay + responsive `<main>` padding in MainLayout**

In `src/components/layout/MainLayout/MainLayout.tsx`:

1. Replace the existing imports block to include the open-state selector:

```tsx
import { Outlet } from 'react-router-dom';
import { useUIStore, selectSidebarCollapsed, selectSidebarOpen } from '@/stores';
import { cn } from '@/lib/utils';
import { Sidebar } from './Sidebar';
import { Header } from './Header';
import { useOnboardingCheck } from '@/features/onboarding/hooks/useOnboardingCheck';
import { OnboardingWizard } from '@/features/onboarding/components/OnboardingWizard';
import { RouteErrorBoundary } from '@/components/common';
```

2. Replace the component body with:

```tsx
export function MainLayout() {
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const isOpen = useUIStore(selectSidebarOpen);
  const setSidebarOpen = useUIStore((state) => state.setSidebarOpen);
  const { showOnboarding, completeOnboarding, remindLater } = useOnboardingCheck();

  if (showOnboarding) {
    return (
      <OnboardingWizard
        onComplete={completeOnboarding}
        onRemindLater={remindLater}
      />
    );
  }

  return (
    <div className="aurora-canvas min-h-screen bg-background" data-page-style="dense">
      <Sidebar />
      <Header />
      {/* Mobile drawer backdrop */}
      {isOpen && (
        <div
          className="fixed inset-0 z-30 bg-background/60 backdrop-blur-sm lg:hidden"
          onClick={() => setSidebarOpen(false)}
          aria-hidden
        />
      )}
      <main
        className={cn(
          'pt-16 transition-all duration-300',
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
- `lg:hidden` on the backdrop ensures it never renders on desktop even if `isOpen` somehow stays `true`.
- The `pl-0` default + `lg:ltr:pl-{16,60}` switches based on the desktop collapse state — on `<lg` content goes edge-to-edge while the drawer overlays it.
- The Header already swaps its own `left` offset based on `sidebarCollapsed` for `lg+` and stays at `left-0` (with the mobile menu button visible at `<lg`) — no Header change needed.

- [ ] **Step 4.4: Build + lint check**

```bash
npm run build && npm run lint
```

Expected: build succeeds, no new lint errors.

- [ ] **Step 4.5: Visual verification — desktop**

At `lg+` viewport (≥ 1024 px), reload `http://localhost:3100/dashboard`.

Expected:
- Sidebar visible at start edge, no regression.
- Collapse / expand chevrons work.
- Backdrop never appears.

- [ ] **Step 4.6: Visual verification — mobile**

Use Chrome DevTools' device toolbar (`Cmd+Shift+M`) and switch to a 768 px viewport (or any `<1024`).

Expected on first load:
- Sidebar **off-screen** to the start side.
- `<main>` content fills the full viewport width.
- Header's `Menu` button is visible.

Click the Menu button:
- Sidebar slides in over the content (drawer pattern).
- Backdrop appears over the rest of the viewport.

Click the backdrop:
- Sidebar slides out, backdrop disappears.

Open the drawer again, click any nav link:
- Drawer auto-closes after navigation; you land on the new page with the sidebar hidden.

Open the drawer again, press `Esc`:
- Drawer closes.

In RTL mode (switch language to Arabic via the LanguageSwitcher):
- Sidebar slides in from the **end** (right) edge.
- Backdrop / route-close behavior unchanged.

- [ ] **Step 4.7: Commit**

```bash
git add boilerplateFE/src/stores/ui.store.ts \
        boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx \
        boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx
git commit -m "feat(fe/layout): mobile sidebar drawer with backdrop, route + Esc auto-close"
```

---

## Task 5: Code-review pass

**Why:** Phase 0 cadence (per spec §9). Catch any regressions before stacking Plan B / C on top.

- [ ] **Step 5.1: Dispatch code-reviewer subagent**

Invoke the `superpowers:code-reviewer` subagent against the diff for this plan (Tasks 1-4). Brief:

> Review the four commits on `fe/redesign-phase-1` since the spec commit (`6652ce91`):
> - Sidebar grouped data shape + expanded-mode eyebrow labels & dividers
> - Collapsed-mode separators
> - Mobile drawer behavior (sidebarOpen state, backdrop, responsive classes, route + Esc auto-close)
> - i18n keys for `nav.groups.*`
>
> Verify against `docs/superpowers/specs/2026-04-27-redesign-phase-1-design.md` §4.
>
> Specific things to check:
> 1. No new `dark:` overrides for primary colors (Frontend Rules in CLAUDE.md).
> 2. No hardcoded `primary-{shade}` classes — semantic tokens only.
> 3. RTL works for the mobile drawer (translate direction flips).
> 4. Empty-group hide logic works for a permission-restricted user (e.g., a tenant member with no admin permissions should see fewer groups).
> 5. The `end` flag on individual nav items correctly replaces the prior hand-rolled exact-match switch.
> 6. No regression to the desktop `lg+` layout — Sidebar / Header / Main padding still correct in both expanded and collapsed states.
> 7. The Tailwind `max-lg:` arbitrary prefix is supported in this project's Tailwind 4 setup.

Address any findings with follow-up commits before proceeding to Plan B.

---

## Self-Review

Run through this checklist after writing all tasks:

**Spec coverage (§4 of the design doc):**
- §4.1 Group structure → Task 2 (entire `groups` array maps 1-to-1 with the spec table).
- §4.2 Data shape → Task 2 (`SidebarNavItem` and `SidebarNavGroup` interfaces).
- §4.3 Expanded visual treatment → Task 2 (eyebrow label classes, divider, item rows unchanged).
- §4.4 Collapsed visual treatment → Task 3.
- §4.5 Mobile drawer → Task 4.
- §4.6 i18n keys → Task 1.
- §4.7 Logo, collapse toggle, tenant logo logic — explicitly preserved in Task 2's full file rewrite.

**Placeholder scan:** No `TBD` / `TODO` / "implement later" / "appropriate error handling" — every step has concrete code or commands.

**Type consistency:** `SidebarNavItem` / `SidebarNavGroup` / `groups` / `visibleGroups` consistent across Task 2, 3, 4.

**Verification model:** Each task has a build check, lint where applicable, visual check, and a single commit. The plan's verification adapts to a no-FE-test codebase — `npm run build` + visual + code-review subagent stand in for unit tests.

**Out of scope here (deferred to later plans):**
- Header `⌘K` palette → Plan B
- `PageHeader` `breadcrumbs` / `tabs` props → Plan C
- Identity cluster page polish → Plans D / E
