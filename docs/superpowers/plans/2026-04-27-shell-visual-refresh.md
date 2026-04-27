# Shell Visual Refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the Floating-glass (Balanced) visual refresh from `docs/superpowers/specs/2026-04-27-shell-visual-refresh-design.md` to the app shell — Sidebar, Header, MainLayout, PageHeader — including a `useBackNavigation` → breadcrumbs migration across all detail/edit pages.

**Architecture:** New utility classes (`.surface-floating`, `.pill-active`) and CSS vars (`--floating-shadow`, `--floating-highlight`) layer on top of Phase 0's existing token system. Sidebar and Header become floating glass cards with margin around them; the aurora bleeds through the gaps. PageHeader drops its card wrapper, gains gradient titles + breadcrumbs + tabs props. The `useBackNavigation` hook gets deprecated in favor of breadcrumbs.

**Tech Stack:** React 19, TypeScript 5.9, Tailwind CSS 4, react-router-dom 7, react-i18next 15, lucide-react.

**Verification model:** Same as Plan A. No FE unit-test runner — every task verifies with (1) `npm run build`, (2) `npm run lint`, and (3) a visual pass against the running test app at `http://localhost:3100`. The test app at `_testJ4visual/` is already running from Plan A — sync changes via `cp` from `boilerplateFE/src/` to `_testJ4visual/_testJ4visual-FE/src/`.

**Working directory for `npm` commands:** `/Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE`. **Working directory for `git` commands:** the repo root `/Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe`.

**Out of scope for this plan** (deferred per spec):
- ⌘K palette content/keyboard handling — its own plan; this plan only lands the search-bar *trigger* visual.
- StatCard extraction — deferred to the Identity cluster plan; the dashboard's existing inline `StatCard` (with tone + sparkline) stays as-is.
- Identity cluster page polish — separate plan.
- AR + KU translations of new strings introduced here — English only; i18next falls back.

---

## Task 1: New tokens + utility classes + aurora bump

**Why:** Spec §9 + §10. Adds the building blocks every later task consumes: a floating-glass surface, a pill-active state, light/dark shadow vars, and a slightly stronger aurora visible through chrome gaps.

**Files:**
- Modify: `boilerplateFE/src/styles/index.css` — three additions and one update.

- [ ] **Step 1.1: Add `--floating-shadow` and `--floating-highlight` to `:root` and `.dark`**

In `boilerplateFE/src/styles/index.css`, find the end of the `:root` block (just before its closing `}` on line ~97 — the line `--tinted-fg: var(--color-primary-700);`). Add two new lines AFTER `--tinted-fg`, BEFORE the closing `}`:

```css
    /* Floating-chrome shadow + inset top highlight (light) */
    --floating-shadow: 0 8px 28px rgb(0 0 0 / 0.10);
    --floating-highlight: inset 0 1px 0 rgb(255 255 255 / 0.6);
```

Then find the end of the `.dark` block (just before its closing `}` on line ~173 — the line `--tinted-fg: var(--color-primary-300);`). Add two new lines AFTER `--tinted-fg`:

```css
    /* Floating-chrome shadow + inset top highlight (dark) */
    --floating-shadow: 0 10px 36px rgb(0 0 0 / 0.45);
    --floating-highlight: inset 0 1px 0 rgb(255 255 255 / 0.06);
```

- [ ] **Step 1.2: Add `.surface-floating` and `.pill-active` utilities**

Still in `boilerplateFE/src/styles/index.css`, find the existing `.surface-glass-strong` rule (around line 442). Add two new utility class blocks **immediately after** the `.surface-glass-strong` block:

```css
  /* Floating glass surface — extends surface-glass with shadow + inset highlight.
   * Use for chrome elements that float over the aurora canvas (sidebar, header). */
  .surface-floating {
    background-color: var(--surface-glass);
    border: 1px solid var(--border-strong);
    backdrop-filter: blur(20px);
    -webkit-backdrop-filter: blur(20px);
    box-shadow: var(--floating-shadow), var(--floating-highlight);
  }

  /* Pill active state — sidebar nav item only. Gradient + halo + inset border.
   * NOT a replacement for state-active (used by buttons / chips elsewhere). */
  .pill-active {
    background: linear-gradient(135deg,
      color-mix(in srgb, hsl(var(--primary)) 22%, transparent),
      color-mix(in srgb, hsl(var(--primary)) 10%, transparent));
    color: hsl(var(--primary));
    box-shadow:
      0 0 22px color-mix(in srgb, hsl(var(--primary)) 18%, transparent),
      inset 0 0 0 1px color-mix(in srgb, hsl(var(--primary)) 30%, transparent);
  }
```

- [ ] **Step 1.3: Bump aurora intensity for dense pages**

Still in `boilerplateFE/src/styles/index.css`, find the existing `[data-page-style="dense"].aurora-canvas::before` rule (around line 405-408):

```css
  [data-page-style="dense"].aurora-canvas::before {
    background: var(--aurora-corner);
    filter: none;
  }
```

Replace with:

```css
  [data-page-style="dense"].aurora-canvas::before {
    background:
      radial-gradient(ellipse 60% 80% at 80% 10%,
        color-mix(in srgb, hsl(var(--primary)) 18%, transparent),
        transparent 65%),
      radial-gradient(ellipse 70% 55% at 12% 90%,
        color-mix(in srgb, var(--color-violet-500) 13%, transparent),
        transparent 70%);
    filter: none;
  }
```

- [ ] **Step 1.4: Build check**

From `boilerplateFE/`:

```bash
npm run build
```

Expected: build succeeds (CSS-only change).

- [ ] **Step 1.5: Visual smoke test**

Sync index.css to the test app and reload `http://localhost:3100/dashboard`.

```bash
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/styles/index.css \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/styles/index.css
```

Expected:
- Aurora has two visible blooms (warm copper top-right, subtle violet bottom-left).
- No regression to existing surfaces — chrome still uses old `surface-glass`, no visible change there yet.

- [ ] **Step 1.6: Commit**

From the repo root:

```bash
git add boilerplateFE/src/styles/index.css
git commit -m "feat(fe/styles): floating-shadow vars, surface-floating + pill-active utilities, two-bloom dense aurora"
```

Do NOT include any "Co-Authored-By" trailer.

---

## Task 2: Sidebar — floating geometry + pill active state

**Why:** Spec §4. The sidebar becomes a floating glass card with margin, the active item becomes a copper-gradient pill with halo, group eyebrows get a copper dot prefix, and expanded-mode dividers go away.

**Files:**
- Modify: `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx`

- [ ] **Step 2.1: Update the `<aside>` className**

Find the existing `<aside>` className expression (lines 45-54 of the current `Sidebar.tsx`):

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

Replace with:

```tsx
<aside
  className={cn(
    // floating geometry — 14px margin, no longer edge-to-edge
    'fixed top-3.5 bottom-3.5 z-40 flex flex-col rounded-[18px]',
    'surface-floating',
    'motion-safe:transition-all motion-safe:duration-300',
    // width: drawer always w-60 on <lg; lg+ follows collapse state
    'w-60',
    isCollapsed && 'lg:w-16',
    // position: 14px from start edge in both ltr / rtl
    'ltr:left-3.5 rtl:right-3.5 lg:translate-x-0',
    !sidebarOpen && 'max-lg:ltr:-translate-x-full max-lg:rtl:translate-x-full'
  )}
>
```

Notes:
- `surface-floating` (Task 1) replaces `surface-glass` and the old border/shadow. The floating utility supplies its own border + shadow + inset highlight.
- `top-3.5 bottom-3.5` (= 14 px) replaces `top-0 h-screen`. The sidebar is now bounded by the viewport with margin instead of filling it edge-to-edge.
- `rounded-[18px]` replaces the old edge-to-edge shape.
- The `ltr:border-r rtl:border-l border-border/40` line is gone — `surface-floating` already provides a border.

- [ ] **Step 2.2: Update the active nav item class**

In `Sidebar.tsx`, find the `<NavLink>` className expression (around line 112):

```tsx
className={({ isActive }) =>
  cn(
    'flex items-center gap-2.5 rounded-lg h-10 px-3 text-sm motion-safe:transition-all motion-safe:duration-150 cursor-pointer',
    isCollapsed && 'justify-center px-0',
    isActive ? 'state-active' : 'state-hover'
  )
}
```

Replace `state-active` with `pill-active` and bump radius to `rounded-[10px]`:

```tsx
className={({ isActive }) =>
  cn(
    'flex items-center gap-2.5 rounded-[10px] h-10 px-3 text-sm motion-safe:transition-all motion-safe:duration-150 cursor-pointer',
    isCollapsed && 'justify-center px-0',
    isActive ? 'pill-active' : 'state-hover'
  )
}
```

- [ ] **Step 2.3: Add copper dot prefix to group eyebrow labels**

Find the eyebrow `<div>` (around line 101):

```tsx
{!isCollapsed && group.label && (
  <div className="px-3 pb-1.5 pt-1 text-[10px] font-medium uppercase tracking-[0.08em] text-muted-foreground">
    {group.label}
  </div>
)}
```

Replace with:

```tsx
{!isCollapsed && group.label && (
  <div className="px-3 pb-1 pt-1 text-[10px] font-semibold uppercase tracking-[0.12em] text-muted-foreground">
    <span className="inline-block w-1 h-1 rounded-full bg-primary/70 me-1.5 align-middle" style={{ transform: 'translateY(-1px)' }} />
    {group.label}
  </div>
)}
```

The dot uses `bg-primary/70` so it picks up the active theme preset, and the `me-1.5` (margin-end) flips correctly in RTL.

- [ ] **Step 2.4: Drop expanded-mode group dividers**

Find the group wrapper className (around lines 88-99):

```tsx
<div
  key={group.id}
  className={cn(
    groupIndex > 0 && (
      isCollapsed
        ? 'mx-3 my-2 border-t border-border/40'
        : cn(
            'mt-4 pt-2',
            groupIndex > 1 && 'border-t border-border/40'
          )
    )
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
        : 'mt-4'
    )
  )}
>
```

Now expanded mode uses only `mt-4` (top margin) to separate groups — no border. The eyebrow dot prefix from Step 2.3 carries the visual cue. Collapsed mode still gets the inset border-t separator (labels are hidden there).

- [ ] **Step 2.5: Bump logo dot size + glow strength**

Find the logo dot (around line 63):

```tsx
<div className="flex h-8 w-8 items-center justify-center rounded-lg btn-primary-gradient glow-primary-sm shrink-0">
  {tenantLogoUrl ? (
    <img src={tenantLogoUrl} alt={appName} className="h-7 w-7 rounded object-cover" />
  ) : (
    <span className="text-sm font-bold text-white">{appName.charAt(0)}</span>
  )}
</div>
```

Replace with:

```tsx
<div className="flex h-9 w-9 items-center justify-center rounded-lg btn-primary-gradient glow-primary-md shrink-0">
  {tenantLogoUrl ? (
    <img src={tenantLogoUrl} alt={appName} className="h-8 w-8 rounded object-cover" />
  ) : (
    <span className="text-[15px] font-bold text-white">{appName.charAt(0)}</span>
  )}
</div>
```

The size goes from `h-8 w-8` to `h-9 w-9`, the glow from `sm` to `md`, and the inner image / fallback letter scales to match.

- [ ] **Step 2.6: Build + lint**

From `boilerplateFE/`:

```bash
npm run build && npm run lint
```

Expected: both pass; no NEW lint errors (pre-existing warnings can be ignored).

- [ ] **Step 2.7: Visual verification**

Sync to test app:

```bash
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/components/layout/MainLayout/Sidebar.tsx
```

Reload `http://localhost:3100/dashboard`.

Expected:
- Sidebar is a floating glass card with 14 px margin around it.
- Active item is a copper-gradient pill with a soft halo, not a flat tinted rectangle.
- Each group eyebrow shows a tiny copper dot before the label.
- Expanded mode has NO horizontal divider lines between groups (just top margin).
- Collapsed mode (click chevron) still has thin separators between groups.
- Logo dot is slightly larger with a stronger glow.
- No regression on the mobile drawer — slides in from the start edge with backdrop.

- [ ] **Step 2.8: Commit**

From the repo root:

```bash
git add boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx
git commit -m "feat(fe/sidebar): floating-glass card, pill-active, copper dot eyebrows, larger logo glow"
```

---

## Task 3: Header floating chrome + MainLayout content padding + search bar trigger

**Why:** Spec §5 + §6. Header becomes a floating glass card matching the sidebar. The bare Menu icon goes away — its job folds into a search-bar control with a `⌘K` keycap. On `<lg` clicking the search bar opens the drawer; on `lg+` it's a placeholder for the palette (Plan B's content). The header back-link (`useBackNavigation` consumer) is removed; breadcrumbs in PageHeader take over (Task 4 migrates the call sites).

**Files:**
- Modify: `boilerplateFE/src/components/layout/MainLayout/Header.tsx` (rewrite)
- Modify: `boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx` (content padding + drop the `selectBackNavigation` flow)

- [ ] **Step 3.1: Rewrite `Header.tsx`**

Replace the FULL contents of `boilerplateFE/src/components/layout/MainLayout/Header.tsx` with:

```tsx
import { LogOut, Menu, Search, User, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';

import {
  LanguageSwitcher,
  NotificationBell,
  ThemeToggle,
  UserAvatar,
} from '@/components/common';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { ROUTES } from '@/config';
import { useLogout } from '@/features/auth/api';
import { cn } from '@/lib/utils';
import {
  selectSidebarCollapsed,
  selectSidebarOpen,
  selectUser,
  useAuthStore,
  useUIStore,
} from '@/stores';

export function Header() {
  const { t } = useTranslation();
  const user = useAuthStore(selectUser);
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const sidebarOpen = useUIStore(selectSidebarOpen);
  const toggleSidebar = useUIStore((state) => state.toggleSidebar);
  const navigate = useNavigate();
  const handleLogout = useLogout();

  // Search-bar trigger doubles as the mobile drawer trigger.
  // On lg+ this is a placeholder for the command palette (Plan B).
  // For now the click handler still calls toggleSidebar — harmless on lg+
  // (sidebarOpen has no UI effect there) and opens the drawer on <lg.
  const onSearchClick = () => toggleSidebar();

  return (
    <header
      className={cn(
        'fixed top-3.5 z-30 h-12 flex items-center gap-2 rounded-2xl px-3',
        'surface-floating',
        'motion-safe:transition-all motion-safe:duration-300',
        // start edge follows sidebar width + 14px gap on lg+; flush on <lg
        'max-lg:ltr:left-3.5 max-lg:rtl:right-3.5',
        isCollapsed
          ? 'lg:ltr:left-[calc(4rem+1.75rem)] lg:rtl:right-[calc(4rem+1.75rem)]'
          : 'lg:ltr:left-[calc(15rem+1.75rem)] lg:rtl:right-[calc(15rem+1.75rem)]',
        'ltr:right-3.5 rtl:left-3.5'
      )}
    >
      {/* Search-bar trigger / mobile drawer trigger */}
      <button
        type="button"
        onClick={onSearchClick}
        aria-label={sidebarOpen ? t('nav.toggle.close') : t('nav.toggle.open')}
        aria-expanded={sidebarOpen}
        className={cn(
          'flex h-8 items-center gap-2 rounded-[9px] border border-white/10 bg-white/5 px-3',
          'text-sm text-muted-foreground',
          'motion-safe:transition-colors motion-safe:duration-150',
          'hover:bg-white/8 hover:text-foreground',
          'flex-1 max-w-[320px]'
        )}
      >
        {/* Mobile shows menu/X icon; desktop shows search icon */}
        <span className="lg:hidden">
          {sidebarOpen ? <X className="h-4 w-4" /> : <Menu className="h-4 w-4" />}
        </span>
        <Search className="hidden lg:block h-4 w-4 opacity-60" />
        <span className="hidden lg:inline flex-1 text-start">{t('header.searchPlaceholder')}</span>
        <span className="hidden lg:inline ms-auto rounded-md border border-white/15 bg-white/8 px-1.5 py-0.5 font-mono text-[10px] tracking-[0.05em] text-muted-foreground">
          ⌘K
        </span>
      </button>

      {/* Spacer pushes right cluster to the end */}
      <div className="flex-1 max-lg:hidden" />

      {/* Right cluster — language, theme, notifications, avatar */}
      <div className="flex items-center gap-1">
        <LanguageSwitcher />
        <ThemeToggle />
        <NotificationBell />

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button
              type="button"
              className={cn(
                'ms-1 flex items-center gap-2 rounded-full border border-white/8 bg-white/5 ps-1 pe-3 py-1',
                'motion-safe:transition-colors motion-safe:duration-150',
                'hover:bg-white/8'
              )}
            >
              <UserAvatar firstName={user?.firstName} lastName={user?.lastName} size="sm" />
              <span className="hidden sm:inline text-sm font-medium text-foreground">
                {user?.firstName} {user?.lastName}
              </span>
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-56">
            <DropdownMenuLabel className="font-normal">
              <div className="flex flex-col space-y-1">
                <p className="text-sm font-medium">
                  {user?.firstName} {user?.lastName}
                </p>
                <p className="text-xs text-muted-foreground">{user?.email}</p>
              </div>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              onClick={() => navigate(ROUTES.PROFILE)}
              className="cursor-pointer"
            >
              <User className="h-4 w-4" />
              {t('profile.title')}
            </DropdownMenuItem>
            <DropdownMenuItem
              onClick={handleLogout}
              className="cursor-pointer text-destructive focus:text-destructive"
            >
              <LogOut className="h-4 w-4" />
              {t('header.logout')}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
```

Notes:
- `selectBackNavigation` import is gone — the header no longer renders a back-link. Breadcrumbs in PageHeader (Task 4) take over. The hook itself stays exported in `@/stores` for one more plan; deletion happens once Task 4's migrations land.
- The `onSearchClick` is intentionally a single function for both viewports. On `lg+` it currently toggles `sidebarOpen` (no visible effect since the desktop sidebar isn't gated on it) — Plan B will replace this body with palette-open logic. Until then, the keystroke `⌘K` is a placeholder visual.
- `text-muted-foreground` + Tailwind opacity utilities work on the white-tinted backgrounds because the `surface-floating` background already mixes with the page; we don't hardcode any primary shades.

- [ ] **Step 3.2: Add the `header.searchPlaceholder` i18n key**

In `boilerplateFE/src/i18n/locales/en/translation.json`, find the `"header": { ... }` block. Add a `"searchPlaceholder"` key:

```json
"header": {
  ...existing keys unchanged...
  "logout": "Sign out",
  "searchPlaceholder": "Search anything…"
}
```

(Adjust the placement to keep the JSON valid — sibling of the existing `"logout"` key. Don't touch ar/ku.)

- [ ] **Step 3.3: Update `MainLayout.tsx` — content padding for floating chrome**

Replace the FULL contents of `boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx` with:

```tsx
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation();

  // Lock body scroll while the mobile drawer is open.
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
    <div
      className="aurora-canvas min-h-screen bg-background overflow-x-clip"
      data-page-style="dense"
    >
      <Sidebar />
      <Header />
      {/* Mobile drawer backdrop */}
      {sidebarOpen && (
        <button
          type="button"
          aria-label={t('nav.toggle.close')}
          className="fixed inset-0 z-30 bg-background/60 backdrop-blur-sm lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}
      <main
        className={cn(
          // pt = header top (14) + header height (48) + gap (8) = 70
          'pt-[70px] motion-safe:transition-all motion-safe:duration-300',
          // edge padding swaps with sidebar margin on lg+; flush on <lg
          'max-lg:px-3.5',
          isCollapsed
            ? 'lg:ltr:pl-[calc(4rem+1.75rem)] lg:rtl:pr-[calc(4rem+1.75rem)]'
            : 'lg:ltr:pl-[calc(15rem+1.75rem)] lg:rtl:pr-[calc(15rem+1.75rem)]',
          'lg:ltr:pr-3.5 lg:rtl:pl-3.5'
        )}
      >
        <div className="px-2 pb-6 pt-2">
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
- `pt-16` becomes `pt-[70px]`. The 70 px breaks down as 14 px (top margin of header) + 48 px (header height) + 8 px (gap between header and content).
- `<main>` no longer has `pl-0` baseline + `lg:ltr:pl-60` switch. It now has horizontal padding equal to the sidebar margin (`max-lg:px-3.5`) on small viewports, and on lg+ uses `calc(sidebar-width + sidebar-margin)` for the start padding plus a 14 px end padding.
- `<div className="p-8">` becomes `<div className="px-2 pb-6 pt-2">` — minimal inner padding because the `<main>` itself now provides edge breathing room.

- [ ] **Step 3.4: Build + lint**

```bash
npm run build && npm run lint
```

Expected: both pass.

- [ ] **Step 3.5: Visual verification**

Sync to test app:

```bash
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/components/layout/MainLayout/Header.tsx \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/components/layout/MainLayout/Header.tsx
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/components/layout/MainLayout/MainLayout.tsx
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/i18n/locales/en/translation.json \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/i18n/locales/en/translation.json
```

Reload `http://localhost:3100/dashboard` (desktop, lg+).

Expected:
- Header is a floating glass pill with 14 px margin top + start (matched to sidebar) + end.
- Search bar visible on the left of the header with magnifier icon, "Search anything…" placeholder, and a `⌘K` keycap on the right end of the search bar.
- Right cluster: language switcher, theme toggle, notification bell, avatar pill (avatar circle + name).
- Content sits closer to the chrome than before (less inner padding) but still has breathing room from the floating sidebar.
- Aurora visible in the gaps between sidebar / header / content edges.

At `<lg` viewport (use Chrome DevTools device toolbar at 768 px):
- Header search bar collapses — shows a Menu icon prefix instead of the magnifier; the `⌘K` keycap hides; placeholder text hides.
- Clicking the (now-Menu) search bar opens the mobile drawer; backdrop appears.
- Drawer auto-closes on backdrop click, Esc, route change.

At `lg+` collapsed sidebar (click the chevron):
- Header start edge tightens to follow the new `lg:w-16` sidebar.

- [ ] **Step 3.6: Commit**

From the repo root:

```bash
git add boilerplateFE/src/components/layout/MainLayout/Header.tsx \
        boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx \
        boilerplateFE/src/i18n/locales/en/translation.json
git commit -m "feat(fe/header): floating-glass header with search-bar trigger; main pad adjusts for floating chrome"
```

---

## Task 4: PageHeader refresh + breadcrumbs/tabs props + `useBackNavigation` migration

**Why:** Spec §7 + §11. PageHeader drops its card wrapper, gains a gradient title, gets new optional `breadcrumbs` and `tabs` props. All current `useBackNavigation` call sites migrate to PageHeader breadcrumbs.

**Files:**
- Modify: `boilerplateFE/src/components/common/PageHeader.tsx` (rewrite)
- Modify: 9 page files that currently use `useBackNavigation` — list below.

**Affected pages (from `grep useBackNavigation`):**

```
boilerplateFE/src/features/products/pages/ProductCreatePage.tsx
boilerplateFE/src/features/products/pages/ProductDetailPage.tsx
boilerplateFE/src/features/roles/pages/RoleEditPage.tsx
boilerplateFE/src/features/roles/pages/RoleCreatePage.tsx
boilerplateFE/src/features/roles/pages/RoleDetailPage.tsx
boilerplateFE/src/features/tenants/pages/TenantDetailPage.tsx
boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDetailPage.tsx
boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDesignerPage.tsx
boilerplateFE/src/features/workflow/pages/WorkflowInstanceDetailPage.tsx
boilerplateFE/src/features/users/pages/UserDetailPage.tsx
```

(10 files total — `grep` found 10. Treat the list above as authoritative.)

- [ ] **Step 4.1: Rewrite `PageHeader.tsx`**

Replace the FULL contents of `boilerplateFE/src/components/common/PageHeader.tsx` with:

```tsx
import { ChevronRight } from 'lucide-react';
import type { ReactNode } from 'react';
import { Link, NavLink } from 'react-router-dom';

import { cn } from '@/lib/utils';

export interface PageHeaderBreadcrumb {
  label: string;
  to?: string;
}

export interface PageHeaderTab {
  label: string;
  to: string;
  count?: number;
}

interface PageHeaderProps {
  title?: string;
  subtitle?: string;
  actions?: ReactNode;
  breadcrumbs?: PageHeaderBreadcrumb[];
  tabs?: PageHeaderTab[];
}

export function PageHeader({ title, subtitle, actions, breadcrumbs, tabs }: PageHeaderProps) {
  if (!title && !actions && !breadcrumbs?.length && !tabs?.length) return null;

  return (
    <div>
      {breadcrumbs && breadcrumbs.length > 0 && (
        <nav
          aria-label="Breadcrumb"
          className="mb-2 flex flex-wrap items-center gap-1.5 text-sm text-muted-foreground"
        >
          {breadcrumbs.map((crumb, idx) => {
            const isLast = idx === breadcrumbs.length - 1;
            const node = crumb.to && !isLast ? (
              <Link
                to={crumb.to}
                className="hover:text-foreground motion-safe:transition-colors motion-safe:duration-150"
              >
                {crumb.label}
              </Link>
            ) : (
              <span className={isLast ? 'font-medium text-primary' : undefined}>{crumb.label}</span>
            );
            return (
              <span key={`${crumb.label}-${idx}`} className="flex items-center gap-1.5">
                {node}
                {!isLast && (
                  <ChevronRight className="h-3 w-3 opacity-50 rtl:rotate-180" aria-hidden />
                )}
              </span>
            );
          })}
        </nav>
      )}

      {(title || actions) && (
        <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
          <div className="min-w-0">
            {title && (
              <h1
                className={cn(
                  'font-display text-3xl lg:text-[32px] font-extralight tracking-tight leading-[1.1]',
                  'bg-clip-text text-transparent',
                  'bg-[linear-gradient(95deg,_hsl(var(--foreground))_30%,_hsl(var(--primary))_100%)]'
                )}
              >
                {title}
              </h1>
            )}
            {subtitle && <p className="mt-1.5 text-sm text-muted-foreground">{subtitle}</p>}
          </div>
          {actions && <div className="shrink-0">{actions}</div>}
        </div>
      )}

      {tabs && tabs.length > 0 && (
        <div className="mt-5 -mx-2 px-2 border-b border-border/40">
          <nav className="flex gap-1" aria-label="Tabs">
            {tabs.map((tab) => (
              <NavLink
                key={tab.to}
                to={tab.to}
                end={false}
                className={({ isActive }) =>
                  cn(
                    'inline-flex items-center gap-2 px-4 py-2.5 text-sm font-medium',
                    'motion-safe:transition-colors motion-safe:duration-150',
                    isActive
                      ? 'border-b-2 border-primary text-foreground'
                      : 'border-b-2 border-transparent text-muted-foreground hover:text-foreground'
                  )
                }
              >
                {tab.label}
                {tab.count != null && (
                  <span className="rounded-full bg-secondary px-2 py-0.5 text-xs font-normal">
                    {tab.count}
                  </span>
                )}
              </NavLink>
            ))}
          </nav>
        </div>
      )}
    </div>
  );
}
```

Notes:
- `PageHeaderBreadcrumb` and `PageHeaderTab` are exported types so call sites can type their constants.
- Breadcrumbs render only when `breadcrumbs?.length > 0`; tabs likewise.
- The title gradient uses `bg-clip-text` + `text-transparent` over a 95° gradient from `--foreground` to `--primary`. Both ends are theme-aware via `hsl(var(--...))`.

- [ ] **Step 4.2: Build check**

```bash
npm run build
```

Expected: build succeeds; existing PageHeader call sites continue to work because `breadcrumbs` and `tabs` are optional and the title/subtitle/actions API is unchanged.

- [ ] **Step 4.3: Migrate `useBackNavigation` → breadcrumbs in 10 pages**

For each file, the migration follows the exact pattern below. Apply it to all 10 files listed above.

**Pattern:**

1. Remove the `useBackNavigation` import and call.
2. Add `breadcrumbs={[...]}` to the page's existing `<PageHeader>` element. The breadcrumbs are: `[{ to: <list path>, label: <list label> }, { label: <current page label> }]`.
3. Verify the page already has a `<PageHeader>` — every one of these 10 does.

**File 1: `boilerplateFE/src/features/products/pages/ProductCreatePage.tsx`**

- Remove (line 15): `import { useBackNavigation } from '@/hooks';`
- Remove (line 39): `useBackNavigation(ROUTES.PRODUCTS.LIST, t('products.title', 'Products'));`
- Find the existing `<PageHeader title=...>` element and add a `breadcrumbs` prop. If the existing call looks like:

  ```tsx
  <PageHeader title={t('products.create.title')} subtitle={t('products.create.subtitle')} />
  ```

  Update to:

  ```tsx
  <PageHeader
    title={t('products.create.title')}
    subtitle={t('products.create.subtitle')}
    breadcrumbs={[
      { to: ROUTES.PRODUCTS.LIST, label: t('products.title', 'Products') },
      { label: t('products.create.title') },
    ]}
  />
  ```

If the existing PageHeader doesn't carry `subtitle`, omit it — the migration is purely additive.

**File 2: `boilerplateFE/src/features/products/pages/ProductDetailPage.tsx`**

- Remove `import { useBackNavigation }` (preserve other named imports from `@/hooks`):
  - Was: `import { useBackNavigation, usePermissions } from '@/hooks';`
  - Becomes: `import { usePermissions } from '@/hooks';`
- Remove (line 42): `useBackNavigation(ROUTES.PRODUCTS.LIST, t('products.title', 'Products'));`
- Add `breadcrumbs` to the existing `<PageHeader>`:

  ```tsx
  breadcrumbs={[
    { to: ROUTES.PRODUCTS.LIST, label: t('products.title', 'Products') },
    { label: product?.name ?? t('common.loading') },
  ]}
  ```

  (The label of the last entry uses the product name when loaded, or the i18n loading string as a fallback. If there's no `product` variable in scope at the PageHeader site, pass a stable label like `t('products.detail.title')`.)

**File 3: `boilerplateFE/src/features/roles/pages/RoleEditPage.tsx`**

- Remove `useBackNavigation` from the `@/hooks` import:
  - Was: `import { usePermissions, useBackNavigation } from '@/hooks';`
  - Becomes: `import { usePermissions } from '@/hooks';`
- Remove the `useBackNavigation(...)` call.
- Add to the existing `<PageHeader>`:

  ```tsx
  breadcrumbs={[
    { to: ROUTES.ROLES.LIST, label: t('roles.title') },
    role
      ? { to: ROUTES.ROLES.getDetail(role.id), label: role.name }
      : { label: t('common.loading') },
    { label: t('roles.edit.title', 'Edit') },
  ]}
  ```

**File 4: `boilerplateFE/src/features/roles/pages/RoleCreatePage.tsx`**

- Remove the `useBackNavigation` import + call.
- Add to PageHeader:

  ```tsx
  breadcrumbs={[
    { to: ROUTES.ROLES.LIST, label: t('roles.title') },
    { label: t('roles.create.title', 'Create role') },
  ]}
  ```

**File 5: `boilerplateFE/src/features/roles/pages/RoleDetailPage.tsx`**

- Remove `useBackNavigation` from `@/hooks` import.
- Remove the call.
- Add to PageHeader:

  ```tsx
  breadcrumbs={[
    { to: ROUTES.ROLES.LIST, label: t('roles.title') },
    { label: role?.name ?? t('common.loading') },
  ]}
  ```

**File 6: `boilerplateFE/src/features/tenants/pages/TenantDetailPage.tsx`**

- Remove the `useBackNavigation` import + call (it spans multiple lines; remove the entire `useBackNavigation(...)` invocation).
- Add to PageHeader:

  ```tsx
  breadcrumbs={[
    { to: ROUTES.TENANTS.LIST, label: t('tenants.title') },
    { label: tenant?.name ?? t('common.loading') },
  ]}
  ```

**File 7: `boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDetailPage.tsx`**

- Remove `useBackNavigation` from `@/hooks` import.
- Remove the call.
- Add to PageHeader:

  ```tsx
  breadcrumbs={[
    { to: '/workflows/definitions', label: t('workflow.definitions.title') },
    { label: definition?.name ?? t('common.loading') },
  ]}
  ```

**File 8: `boilerplateFE/src/features/workflow/pages/WorkflowDefinitionDesignerPage.tsx`**

- Remove the `useBackNavigation` import + call.
- Add to PageHeader (use `id!` for the URL since the designer page already asserts the id is present):

  ```tsx
  breadcrumbs={[
    { to: '/workflows/definitions', label: t('workflow.definitions.title') },
    { to: ROUTES.WORKFLOWS.getDefinitionDetail(id!), label: definition?.name ?? t('common.loading') },
    { label: t('workflow.definitions.designer.title', 'Designer') },
  ]}
  ```

**File 9: `boilerplateFE/src/features/workflow/pages/WorkflowInstanceDetailPage.tsx`**

- Remove `useBackNavigation` from `@/hooks` import.
- Remove the call.
- Add to PageHeader:

  ```tsx
  breadcrumbs={[
    { to: ROUTES.WORKFLOWS.INSTANCES, label: t('workflow.instances.title') },
    { label: instance?.workflowName ?? t('common.loading') },
  ]}
  ```

  (If the loaded instance object uses a different key for the display name, substitute it. The page already loads an instance — use whatever its existing label was via `useBackNavigation`'s second arg as the fallback.)

**File 10: `boilerplateFE/src/features/users/pages/UserDetailPage.tsx`**

- Remove `useBackNavigation` from `@/hooks` import.
- Remove the call.
- Add to PageHeader:

  ```tsx
  breadcrumbs={[
    { to: ROUTES.USERS.LIST, label: t('users.title') },
    { label: user ? `${user.firstName} ${user.lastName}` : t('common.loading') },
  ]}
  ```

Notes for all 10 files:
- The existing `<PageHeader>` element already carries `title` / `subtitle` / `actions`; you are only ADDING the `breadcrumbs` prop. Don't remove or modify other props.
- If the page renders multiple `<PageHeader>` elements conditionally (e.g., loading vs. loaded), add the breadcrumbs prop to each.
- The pattern's last breadcrumb entry has `to` undefined — it represents the current page. PageHeader styles it with `text-primary font-medium`.

- [ ] **Step 4.4: Mark `useBackNavigation` as deprecated**

In `boilerplateFE/src/hooks/`, find the file that defines `useBackNavigation` (it's exported from `@/hooks/index.ts`; the implementation file is named after the hook). Locate the implementation function and add a JSDoc comment:

If the file is `boilerplateFE/src/hooks/useBackNavigation.ts` (typical pattern), find:

```ts
export function useBackNavigation(to: string, label: string) {
  // ...
}
```

Add a JSDoc above it:

```ts
/**
 * @deprecated Use the `breadcrumbs` prop on `<PageHeader>` instead.
 *   The header back-link UI was removed in the floating-glass refresh.
 *   The hook still works (it sets `useUIStore.backNavigation`) but no
 *   component reads that state any more. Will be removed in a follow-up.
 */
export function useBackNavigation(to: string, label: string) {
  // ...
}
```

If the implementation file path differs, search for the function definition: `grep -rn "export function useBackNavigation" boilerplateFE/src/`.

- [ ] **Step 4.5: Build + lint**

```bash
npm run build && npm run lint
```

Expected: both pass. New JSDoc deprecation may surface as an ESLint `@typescript-eslint/no-deprecated` warning — that's intentional. If it's an error, suppress it on individual call sites with a `// eslint-disable-next-line @typescript-eslint/no-deprecated` if absolutely needed; otherwise it's an informational signal that doesn't break the build.

- [ ] **Step 4.6: Visual verification**

Sync the touched files to the test app:

```bash
cp /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/components/common/PageHeader.tsx \
   /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/components/common/PageHeader.tsx

# Sync each migrated page (loop them with a shell script or copy individually):
for f in \
  features/products/pages/ProductCreatePage.tsx \
  features/products/pages/ProductDetailPage.tsx \
  features/roles/pages/RoleEditPage.tsx \
  features/roles/pages/RoleCreatePage.tsx \
  features/roles/pages/RoleDetailPage.tsx \
  features/tenants/pages/TenantDetailPage.tsx \
  features/workflow/pages/WorkflowDefinitionDetailPage.tsx \
  features/workflow/pages/WorkflowDefinitionDesignerPage.tsx \
  features/workflow/pages/WorkflowInstanceDetailPage.tsx \
  features/users/pages/UserDetailPage.tsx \
; do
  cp "/Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/$f" \
     "/Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/$f"
done

# Sync hook deprecation comment too — find its actual file via grep first:
grep -rln "export function useBackNavigation" /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/boilerplateFE/src/hooks/ \
  | xargs -I {} cp {} /Users/samanjasim/Projects/forme/cqrs/boilerplate-cqrs-fe/_testJ4visual/_testJ4visual-FE/src/hooks/$(basename {})
```

Visit each affected page and verify:

- `/dashboard` — page title is now a gradient (foreground → copper). No regression to the layout otherwise.
- `/users` — title gradient visible; no breadcrumbs (list page).
- `/users/<id>` — breadcrumbs row above the title showing `Users › <Name>`. Last entry in copper. Header back-link is gone.
- `/roles/<id>` — same pattern: `Roles › <name>`.
- `/roles/<id>/edit` — three-level breadcrumbs: `Roles › <name> › Edit`.
- `/tenants/<id>` — `Tenants › <tenant name>`.
- A workflow definition detail / designer page — three-level breadcrumbs with the designer link.
- `/products/<id>` — `Products › <product name>`.

Verify in BOTH light and dark modes (toggle via the ThemeToggle in the header). The gradient title should be readable in both.

Verify in RTL (switch language to AR via LanguageSwitcher). Breadcrumb separators should mirror (`rtl:rotate-180`).

- [ ] **Step 4.7: Commit**

From the repo root:

```bash
# Use git add by directory to capture all 10 page changes plus the hook + PageHeader:
git add boilerplateFE/src/components/common/PageHeader.tsx \
        boilerplateFE/src/features/products/pages/ \
        boilerplateFE/src/features/roles/pages/ \
        boilerplateFE/src/features/tenants/pages/TenantDetailPage.tsx \
        boilerplateFE/src/features/workflow/pages/ \
        boilerplateFE/src/features/users/pages/UserDetailPage.tsx \
        boilerplateFE/src/hooks/
git status   # confirm only the intended files are staged
git commit -m "feat(fe/page-header): gradient title + breadcrumbs/tabs props; migrate useBackNavigation call sites"
```

If `git status` shows unintended files staged (e.g., other pages that have unrelated changes), unstage them with `git restore --staged <file>` before committing.

---

## Task 5: Code-review pass

**Why:** Same cadence as Plan A — catch regressions before stacking the next plan.

- [ ] **Step 5.1: Dispatch code-reviewer subagent**

Invoke `superpowers:code-reviewer` against the diff for this plan. Brief:

> Review all commits since `63956415` on `fe/redesign-phase-1`. Five commits to verify against `docs/superpowers/specs/2026-04-27-shell-visual-refresh-design.md`:
>
> 1. `feat(fe/styles): floating-shadow vars, surface-floating + pill-active utilities, two-bloom dense aurora` (Task 1)
> 2. `feat(fe/sidebar): floating-glass card, pill-active, copper dot eyebrows, larger logo glow` (Task 2)
> 3. `feat(fe/header): floating-glass header with search-bar trigger; main pad adjusts for floating chrome` (Task 3)
> 4. `feat(fe/page-header): gradient title + breadcrumbs/tabs props; migrate useBackNavigation call sites` (Task 4)
>
> Specific things to check:
> 1. **No hardcoded `primary-{shade}`** classes and **no `dark:` overrides** for primary colors (CLAUDE.md Frontend Rules). All gradient/primary refs go through `hsl(var(--primary))` or `var(--color-primary*)` tokens.
> 2. **RTL** — sidebar drawer translates the right way, header start-edge offset uses `ltr:`/`rtl:` paired classes, breadcrumb chevron has `rtl:rotate-180`.
> 3. **Light + dark** — verify the title gradient is readable in both modes, and that `.surface-floating` looks correct in both (the `--floating-shadow` var should differ light vs. dark).
> 4. **Breadcrumb migrations** — every `useBackNavigation` call site got migrated. Run `grep -rn "useBackNavigation" boilerplateFE/src/` and confirm the only remaining hits are (a) the hook definition itself with the deprecation JSDoc, (b) the export in `@/hooks/index.ts` (or wherever it's re-exported).
> 5. **`type="button"`** on every state-mutating button (Sidebar logo, collapse chevrons, Header search trigger, avatar dropdown trigger, backdrop button).
> 6. **`motion-safe:`** on all transitions in the touched files.
> 7. **No regression to mobile drawer** — Sidebar still translates off-screen on `<lg` when `sidebarOpen` is false; clicking the (now-Menu-icon-prefixed) search bar still toggles the drawer; Esc + backdrop + route change still all close it.
> 8. **Aurora bleed** — the dense-page aurora (`[data-page-style="dense"].aurora-canvas::before`) now uses two color-mix blooms, not just `var(--aurora-corner)`. Visible in the gaps between sidebar and header.
> 9. **PageHeader API back-compat** — pages that don't pass `breadcrumbs` or `tabs` render unchanged.
> 10. **No lingering `useBackNavigation` rendering** — the Header no longer references `selectBackNavigation`, the dropdown / search bar layout is the only chrome on the start side.

Address findings with follow-up commits before considering Plan B-v2 done.

---

## Self-Review

**Spec coverage (`docs/superpowers/specs/2026-04-27-shell-visual-refresh-design.md`):**

- §4 Sidebar visual → Task 2 (geometry, surface-floating, pill-active, dot eyebrows, no expanded dividers, larger logo).
- §5 Header → Task 3 (floating glass, search-bar trigger combining the desktop palette opener + mobile drawer trigger, right-cluster pills, avatar pill).
- §6 MainLayout → Task 3 (content padding, removed back-link wiring).
- §7 PageHeader → Task 4 (drops card wrapper, gradient title, breadcrumbs + tabs props, light/dark gradient via `hsl(var(...))`).
- §8.5 Light mode → covered structurally — every gradient uses `hsl(var(--foreground))`/`hsl(var(--primary))` and the `--floating-shadow` / `--floating-highlight` vars are defined under both `:root` and `.dark` in Task 1.
- §9 New tokens & utilities → Task 1.
- §10 Aurora intensity → Task 1.
- §11 `useBackNavigation` deprecation → Task 4 (JSDoc + 10-page migration).

**Out of scope (explicitly deferred in Task 0):**
- StatCard extraction (`@/components/common/StatCard.tsx`) — noted in §8 of the spec as part of this plan, but here in the plan it is **deferred to the Identity cluster plan** because the dashboard already has a more complex inline `StatCard` (with `tone` and `spark` props) that would clash with the simpler API the spec proposed. A clean extraction needs design work to merge both APIs and that's better done alongside the identity cluster pages that will consume the simple variant. **Spec deviation flagged for the user to confirm.**
- ⌘K palette content (Plan B's job) — only the trigger visual lands here.

**Placeholder scan:** No `TBD` / `TODO` / "implement later" / vague error-handling. Every step has concrete code or commands. The migration pattern in Task 4 is repeated explicitly per file rather than left as "similar to file 1" — even though it's verbose, the engineer reading the plan may pick up the file out of order.

**Type consistency:**
- `PageHeaderBreadcrumb`, `PageHeaderTab` defined and exported in Task 4.1; consumed in Task 4.3 migrations.
- `surface-floating`, `pill-active` defined in Task 1; consumed in Tasks 2 + 3.
- `--floating-shadow`, `--floating-highlight` defined in Task 1; consumed by `.surface-floating` (also in Task 1). No leak.
- `sidebarOpen`, `setSidebarOpen`, `selectSidebarOpen` already exist from Plan A — referenced consistently across Header rewrite and MainLayout.

**Verification model:** Each task has build + lint + visual check + commit. Visual checks span desktop expanded, desktop collapsed, mobile drawer, light + dark, RTL.

**One spec deviation requiring user confirmation:** §8 StatCard extraction is deferred to the Identity cluster plan. If the user wants it folded in, this plan grows by one task.
