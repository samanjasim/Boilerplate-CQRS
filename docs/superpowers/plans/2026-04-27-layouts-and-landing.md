# Visual Foundation — Phase 3 (Layouts & Landing)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Tasks use checkbox (`- [ ]`) syntax.

**Goal:** Apply the J4 aurora to the three app layouts (MainLayout, AuthLayout, PublicLayout), restyle the Header and Sidebar to feel cohesive with the J4 system, and compose the 8-section landing page that delivers the WOW first impression promised in the spec.

**Architecture:** Layouts get an `.aurora-canvas` wrapper so aurora bleeds underneath every page. Header becomes glass with the 4-control right cluster polished. Sidebar gets glass surface with copper-glow on active items. Landing page is composed from 8 reusable section components in `@/features/landing/components/` per spec §12.

**Tech Stack:** React 19, Tailwind 4, TypeScript, react-router-dom, lucide-react. No new dependencies.

**Spec reference:** `docs/superpowers/specs/2026-04-26-phase-0-visual-foundation-design.md` §9 (Header), §10 (Sidebar), §11 (Layouts), §12 (Landing blueprint).

**Plan position:** Plan **3 of 3** (final) in Phase 0. Plans 1, 2, 2B all on `fe/base`.

**Branch:** `fe/base`. **Test app:** `_testJ4visual` running BE 5100 / FE 3100.

---

## File Structure

| File | Status | Responsibility |
|---|---|---|
| `boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx` | Modify | Wrap content in `.aurora-canvas` (full aurora) with `data-page-style="dense"` toggle |
| `boilerplateFE/src/components/layout/MainLayout/Header.tsx` | Modify | Glass background, hairline bottom border, refined cluster spacing |
| `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx` | Modify | Glass surface, copper-glow on active item, J4 logo dot |
| `boilerplateFE/src/components/layout/AuthLayout/AuthLayout.tsx` | Modify | Aurora canvas, glass card for login/register form |
| `boilerplateFE/src/components/layout/PublicLayout/PublicLayout.tsx` | Modify | Aurora canvas (full Spectrum) for landing/marketing |
| `boilerplateFE/src/features/landing/components/LandingNav.tsx` | Create | Top nav: logo + links + sign-in + gradient CTA |
| `boilerplateFE/src/features/landing/components/HeroSection.tsx` | Create | Eyebrow pill + gradient-text headline + deck + CTAs + meta |
| `boilerplateFE/src/features/landing/components/TechStrip.tsx` | Create | "BUILT ON" + 11 mono tech tags |
| `boilerplateFE/src/features/landing/components/FeatureGrid.tsx` | Create | 8 feature cards (Auth, Multi-tenancy, RBAC, Billing, Audit, Webhooks, FF, Observability) |
| `boilerplateFE/src/features/landing/components/CodeSection.tsx` | Create | Code preview + 4 numbered talking points |
| `boilerplateFE/src/features/landing/components/ArchitectureSection.tsx` | Create | 3 client cells + flow line |
| `boilerplateFE/src/features/landing/components/StatsStrip.tsx` | Create | 4 mono numerics with gradient accents |
| `boilerplateFE/src/features/landing/components/FooterCta.tsx` | Create | Gradient-text CTA + clone command + footer nav |
| `boilerplateFE/src/features/landing/components/index.ts` | Create | Barrel export |
| `boilerplateFE/src/features/landing/pages/LandingPage.tsx` | Modify (rewrite) | Compose the 8 sections in order |

---

## Pre-flight

### Task 0: Confirm test app healthy

(Inline.)

```bash
ps -p $(cat /tmp/_testJ4visual.be.pid 2>/dev/null) -p $(cat /tmp/_testJ4visual.fe.pid 2>/dev/null) -o pid,ppid,comm
curl -s -o /dev/null -w "BE %{http_code}  FE %{http_code}\n" http://localhost:5100/health http://localhost:3100/
```

---

## Phase 3A — Layouts get aurora

### Task 1: Apply aurora to all 3 layouts

**Files:**
- Modify: `boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx`
- Modify: `boilerplateFE/src/components/layout/AuthLayout/AuthLayout.tsx`
- Modify: `boilerplateFE/src/components/layout/PublicLayout/PublicLayout.tsx`

- [ ] **Step 1: MainLayout — wrap with aurora-canvas + dense mode**

In `boilerplateFE/src/components/layout/MainLayout/MainLayout.tsx`, the current root is `<div className="min-h-screen bg-background">`. Use Edit to wrap with `.aurora-canvas` and add `data-page-style="dense"` so list pages get the toned-down corner-bloom rather than the full Spectrum.

**old_string:**
```tsx
  return (
    <div className="min-h-screen bg-background">
      <Sidebar />
      <Header />
      <main
        className={cn(
          'pt-16 transition-all duration-300',
          isCollapsed ? 'ltr:pl-16 rtl:pr-16' : 'ltr:pl-60 rtl:pr-60'
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
```

**new_string:**
```tsx
  return (
    <div className="aurora-canvas min-h-screen bg-background" data-page-style="dense">
      <Sidebar />
      <Header />
      <main
        className={cn(
          'pt-16 transition-all duration-300',
          isCollapsed ? 'ltr:pl-16 rtl:pr-16' : 'ltr:pl-60 rtl:pr-60'
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
```

(Added `aurora-canvas` class and `data-page-style="dense"` attribute. The dense attribute makes the aurora a single corner bloom instead of the full three-axis spectrum, so it doesn't compete with data tables.)

- [ ] **Step 2: AuthLayout — full aurora + glass card center**

Read the current `boilerplateFE/src/components/layout/AuthLayout/AuthLayout.tsx` first. The typical shape centers an `<Outlet />` over a background. Update so the layout has full aurora and the form lives in a glass-strong card.

If the current layout is something like:
```tsx
<div className="min-h-screen flex items-center justify-center bg-background">
  <div className="w-full max-w-md">
    <Outlet />
  </div>
</div>
```

Change to:
```tsx
<div className="aurora-canvas min-h-screen flex items-center justify-center bg-background relative">
  <div className="w-full max-w-md surface-glass-strong rounded-2xl p-8 shadow-float z-10">
    <Outlet />
  </div>
</div>
```

Read the actual file and apply the equivalent change (adding `aurora-canvas` to the outer wrapper, and wrapping the `<Outlet />` container with `surface-glass-strong rounded-2xl p-8 shadow-float`). If the existing layout already wraps the outlet in a card, just add `surface-glass-strong shadow-float` and remove any conflicting `bg-card` / `border` classes.

If the layout has any logo/branding above the card, leave that part untouched.

- [ ] **Step 3: PublicLayout — full aurora**

Read `boilerplateFE/src/components/layout/PublicLayout/PublicLayout.tsx`. It probably renders `<Outlet />` inside a flex container. Add `aurora-canvas` to the outer wrapper.

If the current shape is:
```tsx
<div className="min-h-screen bg-background">
  <Outlet />
</div>
```

Change to:
```tsx
<div className="aurora-canvas min-h-screen bg-background relative">
  <Outlet />
</div>
```

The `relative` is harmless if it's already there. The `aurora-canvas` utility sets `position:relative` itself but adding it explicitly avoids any cascading surprises.

- [ ] **Step 4: Copy + verify + commit**

```bash
cp_fe() {
  for f in "$@"; do
    src="boilerplateFE/$f"; dst="_testJ4visual/_testJ4visual-FE/$f"
    mkdir -p "$(dirname "$dst")"; cp "$src" "$dst" && echo "→ $dst"
  done
}

cp_fe src/components/layout/MainLayout/MainLayout.tsx \
      src/components/layout/AuthLayout/AuthLayout.tsx \
      src/components/layout/PublicLayout/PublicLayout.tsx

cd boilerplateFE && npx tsc --noEmit && cd ..

curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3100/
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3100/login
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3100/dashboard

git add boilerplateFE/src/components/layout/
git commit -m "feat(theme): apply aurora canvas to MainLayout (dense), AuthLayout, PublicLayout"
```

Visual check (browser): on `/login`, the form should now sit on a glass card with aurora behind it. On `/dashboard`, a subtle copper corner-bloom should peek behind the content (data still legible). On `/` (landing — current placeholder), aurora visible.

---

## Phase 3B — Header + Sidebar restyle

### Task 2: Restyle Header — glass background

**Files:**
- Modify: `boilerplateFE/src/components/layout/MainLayout/Header.tsx`

The header currently has no background — it floats over the page. With aurora behind it, that means the header content sits over the aurora gradient and may compete for attention. Add a glass background + hairline bottom border so the header sits as a clear band.

- [ ] **Step 1: Edit the header element**

In `boilerplateFE/src/components/layout/MainLayout/Header.tsx` (around lines 28-36), the `<header>` element starts with `'fixed top-0 z-30 flex h-14 items-center justify-between px-6 transition-all duration-300'`. Add glass background + bottom border.

**old_string:**
```tsx
    <header
      className={cn(
        'fixed top-0 z-30 flex h-14 items-center justify-between px-6 transition-all duration-300',
        isCollapsed
          ? 'ltr:left-16 rtl:right-16 ltr:right-0 rtl:left-0'
          : 'ltr:left-60 rtl:right-60 ltr:right-0 rtl:left-0'
      )}
    >
```

**new_string:**
```tsx
    <header
      className={cn(
        'fixed top-0 z-30 flex h-14 items-center justify-between px-6 transition-all duration-300',
        'surface-glass border-b border-border/40',
        isCollapsed
          ? 'ltr:left-16 rtl:right-16 ltr:right-0 rtl:left-0'
          : 'ltr:left-60 rtl:right-60 ltr:right-0 rtl:left-0'
      )}
    >
```

(Added `surface-glass border-b border-border/40` — translucent header band with a hairline bottom border.)

- [ ] **Step 2: Slightly tighten the user dropdown trigger styling**

In the same file, find the user dropdown trigger button (around lines 60-66). The `hover:bg-secondary/80` already works. No change needed.

- [ ] **Step 3: Copy + verify + commit**

```bash
cp_fe src/components/layout/MainLayout/Header.tsx
cd boilerplateFE && npx tsc --noEmit && cd ..
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3100/dashboard

git add boilerplateFE/src/components/layout/MainLayout/Header.tsx
git commit -m "feat(theme): restyle Header with surface-glass band + hairline bottom border"
```

### Task 3: Restyle Sidebar — glass + copper-glow active

**Files:**
- Modify: `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx`

- [ ] **Step 1: Sidebar surface + logo mark**

The current `<aside>` uses `bg-card` (solid) and `border-r border-border`. Switch to the J4 glass surface for cohesion with the header.

**Edit 1 — sidebar root surface:**

old_string:
```tsx
    <aside
      className={cn(
        'fixed top-0 z-40 flex h-screen flex-col bg-card transition-all duration-300',
        'ltr:left-0 ltr:border-r rtl:right-0 rtl:border-l border-border',
        isCollapsed ? 'w-16' : 'w-60'
      )}
    >
```

new_string:
```tsx
    <aside
      className={cn(
        'fixed top-0 z-40 flex h-screen flex-col surface-glass transition-all duration-300',
        'ltr:left-0 ltr:border-r rtl:right-0 rtl:border-l border-border/40',
        isCollapsed ? 'w-16' : 'w-60'
      )}
    >
```

(Replaced `bg-card` with `surface-glass`. Border softened from `border-border` to `border-border/40`.)

**Edit 2 — logo mark uses J4 gradient + glow:**

old_string:
```tsx
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary shrink-0">
```

new_string:
```tsx
          <div className="flex h-8 w-8 items-center justify-center rounded-lg btn-primary-gradient glow-primary-sm shrink-0">
```

(Logo dot now has the gradient + halo — matches every other primary CTA in the app.)

- [ ] **Step 2: Active nav item — gradient backdrop + copper glow icon**

The current `state-active` utility (in `index.css`) handles the active state via CSS-vars. It produces a copper-tinted background using `--active-bg` (a `color-mix` of primary at 10%). That's already J4-correct — no change needed for the active state container.

For the active item icon, the current implementation renders `<item.icon>` with no special class. To add a subtle copper glow on the active icon, we need to know whether the item is active inside the NavLink render-prop. Add the glow conditionally.

old_string:
```tsx
              <NavLink
                to={item.path}
                end={item.path === ROUTES.DASHBOARD || item.path === ROUTES.BILLING || item.path === ROUTES.SUBSCRIPTIONS?.LIST || item.path === ROUTES.WEBHOOKS_ADMIN?.LIST}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-2.5 rounded-lg h-10 px-3 text-sm transition-colors duration-150 cursor-pointer',
                    isCollapsed && 'justify-center px-0',
                    isActive
                      ? 'state-active'
                      : 'state-hover'
                  )
                }
              >
                <item.icon className="h-[18px] w-[18px] shrink-0" />
                {!isCollapsed && <span className="flex-1">{item.label}</span>}
                {!isCollapsed && 'badge' in item && item.badge != null && (
                  <span className="flex h-5 min-w-5 items-center justify-center rounded-full bg-destructive px-1.5 text-[10px] font-bold text-destructive-foreground">
                    {(item.badge as number) > 99 ? '99+' : item.badge}
                  </span>
                )}
              </NavLink>
```

new_string:
```tsx
              <NavLink
                to={item.path}
                end={item.path === ROUTES.DASHBOARD || item.path === ROUTES.BILLING || item.path === ROUTES.SUBSCRIPTIONS?.LIST || item.path === ROUTES.WEBHOOKS_ADMIN?.LIST}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-2.5 rounded-lg h-10 px-3 text-sm transition-all duration-150 cursor-pointer',
                    isCollapsed && 'justify-center px-0',
                    isActive
                      ? 'state-active'
                      : 'state-hover'
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <item.icon className={cn('h-[18px] w-[18px] shrink-0', isActive && 'drop-shadow-[0_0_6px_color-mix(in_srgb,var(--color-primary)_45%,transparent)]')} />
                    {!isCollapsed && <span className="flex-1">{item.label}</span>}
                    {!isCollapsed && 'badge' in item && item.badge != null && (
                      <span className="flex h-5 min-w-5 items-center justify-center rounded-full btn-primary-gradient glow-primary-sm px-1.5 text-[10px] font-bold text-destructive-foreground font-mono">
                        {(item.badge as number) > 99 ? '99+' : item.badge}
                      </span>
                    )}
                  </>
                )}
              </NavLink>
```

What changed:
- `transition-colors` → `transition-all` (for the drop-shadow animation).
- Children now use the `(({ isActive }) => ...)` render-prop pattern of NavLink, so the icon gets a conditional copper glow when active.
- The badge uses `btn-primary-gradient glow-primary-sm font-mono` (per spec — copper gradient counter pills with mono numerics).

If the file currently uses NavLink in a way that doesn't support the children render-prop here, fall back to a simpler approach: leave the icon alone but keep the badge restyle. Document any deviation.

- [ ] **Step 3: Copy + verify + commit**

```bash
cp_fe src/components/layout/MainLayout/Sidebar.tsx
cd boilerplateFE && npx tsc --noEmit && cd ..
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3100/dashboard

git add boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx
git commit -m "feat(theme): restyle Sidebar with surface-glass + J4 logo gradient + copper-glow active icon"
```

---

## Phase 3C — Landing page sections

For Tasks 4-7, each task creates 1-2 section components. Each section is a self-contained React component exported as a named export. The composition happens in Task 8.

### Task 4: Create LandingNav + HeroSection

**Files:**
- Create: `boilerplateFE/src/features/landing/components/LandingNav.tsx`
- Create: `boilerplateFE/src/features/landing/components/HeroSection.tsx`
- Create: `boilerplateFE/src/features/landing/components/index.ts`

- [ ] **Step 1: LandingNav**

`boilerplateFE/src/features/landing/components/LandingNav.tsx`:

```tsx
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { ROUTES } from '@/config';

const APP_NAME = import.meta.env.VITE_APP_NAME || 'Starter';

const NAV_LINKS = [
  { label: 'Product', href: '#product' },
  { label: 'Architecture', href: '#architecture' },
  { label: 'Pricing', href: '#pricing' },
  { label: 'Docs', href: '#docs' },
  { label: 'GitHub', href: import.meta.env.VITE_GITHUB_URL || 'https://github.com' },
];

export function LandingNav() {
  return (
    <nav className="flex items-center justify-between px-7 py-3.5 relative z-10">
      <Link to={ROUTES.LANDING} className="flex items-center gap-2.5 font-semibold text-sm">
        <div className="flex h-[22px] w-[22px] items-center justify-center rounded-md btn-primary-gradient glow-primary-sm text-primary-foreground text-xs font-bold">
          {APP_NAME.charAt(0)}
        </div>
        {APP_NAME}
      </Link>
      <div className="hidden md:flex items-center gap-[18px] text-xs font-medium">
        {NAV_LINKS.map((link) => (
          <a key={link.label} href={link.href} className="text-muted-foreground hover:text-foreground transition-colors">
            {link.label}
          </a>
        ))}
      </div>
      <div className="flex items-center gap-2">
        <Button variant="ghost" size="sm" asChild>
          <Link to={ROUTES.LOGIN}>Sign in</Link>
        </Button>
        <Button size="sm" asChild>
          <Link to={ROUTES.REGISTER_TENANT}>Get started</Link>
        </Button>
      </div>
    </nav>
  );
}
```

- [ ] **Step 2: HeroSection**

`boilerplateFE/src/features/landing/components/HeroSection.tsx`:

```tsx
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { ROUTES } from '@/config';

export function HeroSection() {
  return (
    <section className="px-7 pt-8 pb-10 max-w-[760px] relative z-[2]">
      <div className="inline-flex items-center gap-2 px-2.5 py-1 rounded-full mb-4 text-[10px] font-bold uppercase tracking-[0.18em] bg-[color-mix(in_srgb,var(--color-accent-500)_8%,transparent)] text-[var(--color-accent-700)] border border-[color-mix(in_srgb,var(--color-accent-500)_20%,transparent)]">
        Open source · Multi-tenant · Production-grade
      </div>
      <h1 className="text-[44px] font-extralight tracking-[-0.04em] leading-[1.05] mb-3.5 font-display text-foreground">
        Stop rebuilding the foundation.<br />
        <em className="not-italic font-medium gradient-text">Build what's actually yours.</em>
      </h1>
      <p className="text-sm leading-[1.6] max-w-[540px] mb-5 text-muted-foreground">
        A full-stack CQRS starter spanning .NET 10, React 19, and Flutter 3 — with the fifteen things every SaaS quietly needs (auth, RBAC, multi-tenancy, billing, audit, webhooks, feature flags, observability) already wired together. Skip a quarter of foundation work and ship the part only your team can build.
      </p>
      <div className="flex flex-wrap gap-2.5 mb-4">
        <Button asChild>
          <a href={import.meta.env.VITE_GITHUB_URL || '#github'}>Clone on GitHub →</a>
        </Button>
        <Button variant="outline" asChild>
          <Link to={ROUTES.LOGIN}>Read the architecture</Link>
        </Button>
      </div>
      <div className="text-[11px] text-muted-foreground">
        <span className="text-foreground font-semibold">Three clients</span> · TypeScript-strict · CQRS via MediatR · Apache-2.0
      </div>
    </section>
  );
}
```

- [ ] **Step 3: Barrel export**

`boilerplateFE/src/features/landing/components/index.ts`:

```ts
export { LandingNav } from './LandingNav';
export { HeroSection } from './HeroSection';
```

(Add more exports here in subsequent tasks.)

- [ ] **Step 4: Copy + commit**

```bash
cp_fe() {
  for f in "$@"; do
    src="boilerplateFE/$f"; dst="_testJ4visual/_testJ4visual-FE/$f"
    mkdir -p "$(dirname "$dst")"; cp "$src" "$dst" && echo "→ $dst"
  done
}

cp_fe src/features/landing/components/LandingNav.tsx \
      src/features/landing/components/HeroSection.tsx \
      src/features/landing/components/index.ts

cd boilerplateFE && npx tsc --noEmit && cd ..

git add boilerplateFE/src/features/landing/components/
git commit -m "feat(landing): add LandingNav and HeroSection components"
```

(No browser visible change yet — sections aren't composed in LandingPage until Task 8.)

### Task 5: Create TechStrip + FeatureGrid

**Files:**
- Create: `boilerplateFE/src/features/landing/components/TechStrip.tsx`
- Create: `boilerplateFE/src/features/landing/components/FeatureGrid.tsx`
- Modify: `boilerplateFE/src/features/landing/components/index.ts`

- [ ] **Step 1: TechStrip**

`boilerplateFE/src/features/landing/components/TechStrip.tsx`:

```tsx
const TAGS = [
  '.NET 10', 'React 19', 'Tailwind 4', 'Flutter 3', 'PostgreSQL',
  'Redis', 'RabbitMQ', 'MediatR', 'EF Core', 'MassTransit', 'OpenTelemetry',
];

export function TechStrip() {
  return (
    <div className="px-7 py-4 border-y border-border/30 flex flex-wrap items-center gap-3 text-[11px] surface-glass relative z-[2]">
      <span className="text-[9px] font-bold uppercase tracking-[0.2em] text-muted-foreground">Built on</span>
      {TAGS.map((tag) => (
        <span
          key={tag}
          className="px-2.5 py-1 rounded-md text-[11px] font-mono bg-[color-mix(in_srgb,var(--color-primary)_8%,transparent)] text-[var(--color-primary-700)] dark:text-[var(--color-primary-300)] border border-[var(--border-strong)]"
        >
          {tag}
        </span>
      ))}
    </div>
  );
}
```

- [ ] **Step 2: FeatureGrid**

`boilerplateFE/src/features/landing/components/FeatureGrid.tsx`:

```tsx
type IconColor = 'copper' | 'emerald' | 'violet' | 'amber';

const ICON_BG: Record<IconColor, string> = {
  copper: 'btn-primary-gradient glow-primary-sm',
  emerald: 'bg-gradient-to-br from-[var(--color-accent-400)] to-[var(--color-accent-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-accent-500)_30%,transparent)]',
  violet: 'bg-gradient-to-br from-[var(--color-violet-400)] to-[var(--color-violet-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-violet-500)_30%,transparent)]',
  amber: 'bg-gradient-to-br from-[var(--color-amber-400)] to-[var(--color-amber-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-amber-500)_30%,transparent)]',
};

const FEATURES: { letter: string; color: IconColor; title: string; body: string; tags: string[] }[] = [
  { letter: 'A', color: 'copper', title: 'Auth & Sessions', body: 'JWT + refresh-token rotation, TOTP/2FA, invitations, password reset, session listing, login history. Mailpit-wired for dev SMTP.', tags: ['JWT', '2FA', 'API keys'] },
  { letter: 'T', color: 'emerald', title: 'Multi-tenancy', body: 'Global EF query filters keep tenant data isolated automatically. Platform admins still see everything; tenant users see only theirs. Onboarding, branding, status, business info.', tags: ['RLS-style', 'Branding'] },
  { letter: 'R', color: 'violet', title: 'RBAC & Permissions', body: 'Role/permission matrix with policy-based authorization. Permissions mirrored across BE/FE/Mobile so adding one is a single source-of-truth edit.', tags: ['Roles', 'Policies'] },
  { letter: '$', color: 'amber', title: 'Billing & Plans', body: 'Subscription plans CRUD, plan changes with proration, usage tracking, payment records. Stripe-adapter-ready, no provider lock-in.', tags: ['Plans', 'Usage'] },
  { letter: 'L', color: 'copper', title: 'Audit Trail', body: 'Every state-changing action logged with actor, tenant, before/after diff. Filterable, immutable, exportable to CSV or PDF.', tags: ['Immutable', 'CSV/PDF'] },
  { letter: 'W', color: 'emerald', title: 'Webhooks', body: 'Endpoint CRUD, signed deliveries, retry with exponential backoff, delivery log, secret rotation, manual test-fire. Outbox pattern under the hood.', tags: ['Outbox', 'HMAC'] },
  { letter: 'F', color: 'violet', title: 'Feature flags', body: 'Tenant-scoped overrides, opt-out, enforcement modes. Ship behind a flag, ramp without redeploys.', tags: ['Tenant override'] },
  { letter: 'O', color: 'amber', title: 'Observability', body: 'OpenTelemetry → Jaeger, Prometheus metrics, Serilog structured logs with conversation-id correlation across every consumer.', tags: ['OTel', 'Jaeger', 'Prom'] },
];

export function FeatureGrid() {
  return (
    <section id="product" className="px-7 py-9 relative z-[2]">
      <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-2">What's already in</div>
      <h2 className="text-[26px] font-light tracking-[-0.025em] leading-[1.15] mb-2 font-display">
        Fifteen capabilities.<br />
        <em className="not-italic font-medium gradient-text">Eight you'd dread building from scratch.</em>
      </h2>
      <p className="text-[13px] leading-[1.55] max-w-[540px] mb-6 text-muted-foreground">
        Real implementations of the boring-but-load-bearing pieces — JWT rotation, RBAC matrices, transactional outbox, billing proration, audit diffs. The ones that take weeks to get right and months to clean up.
      </p>
      <div className="grid gap-3 md:grid-cols-2">
        {FEATURES.map((f) => (
          <div key={f.title} className="surface-glass rounded-[10px] p-4">
            <div className={`w-[26px] h-[26px] rounded-[7px] flex items-center justify-center text-[13px] font-bold mb-2.5 text-white ${ICON_BG[f.color]}`}>
              {f.letter}
            </div>
            <h3 className="text-[13px] font-semibold mb-1 text-foreground">{f.title}</h3>
            <p className="text-[11px] leading-[1.55] text-muted-foreground">{f.body}</p>
            <div className="mt-2 flex gap-1 flex-wrap">
              {f.tags.map((t) => (
                <span key={t} className="font-mono text-[9px] px-1.5 py-px rounded bg-[color-mix(in_srgb,var(--color-primary)_6%,transparent)] text-[var(--color-primary-700)] dark:text-[var(--color-primary-300)]">
                  {t}
                </span>
              ))}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
```

- [ ] **Step 3: Update barrel**

In `boilerplateFE/src/features/landing/components/index.ts`, append:
```ts
export { TechStrip } from './TechStrip';
export { FeatureGrid } from './FeatureGrid';
```

- [ ] **Step 4: Copy + commit**

```bash
cp_fe src/features/landing/components/TechStrip.tsx \
      src/features/landing/components/FeatureGrid.tsx \
      src/features/landing/components/index.ts

cd boilerplateFE && npx tsc --noEmit && cd ..

git add boilerplateFE/src/features/landing/components/
git commit -m "feat(landing): add TechStrip and FeatureGrid components"
```

### Task 6: Create CodeSection + ArchitectureSection

**Files:**
- Create: `boilerplateFE/src/features/landing/components/CodeSection.tsx`
- Create: `boilerplateFE/src/features/landing/components/ArchitectureSection.tsx`
- Modify: `boilerplateFE/src/features/landing/components/index.ts`

- [ ] **Step 1: CodeSection**

`boilerplateFE/src/features/landing/components/CodeSection.tsx`:

```tsx
const POINTS: { num: string; title: string; body: string }[] = [
  { num: '01', title: 'Sealed primary constructors.', body: 'Less ceremony, smaller files, no DI mistakes.' },
  { num: '02', title: 'Result<T> everywhere.', body: 'No exceptions for control flow. Controllers map to HTTP via HandleResult().' },
  { num: '03', title: 'Outbox over IPublishEndpoint.', body: 'Events commit with the business row. Architecture test fails the build if you regress.' },
  { num: '04', title: 'Validators auto-discovered.', body: 'FluentValidation + a MediatR pipeline behavior — drop a class in, it runs.' },
];

export function CodeSection() {
  return (
    <section className="px-7 py-9 relative z-[2]">
      <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-2">Show, don't tell</div>
      <h2 className="text-[26px] font-light tracking-[-0.025em] leading-[1.15] mb-2 font-display">
        Real handlers.<br />
        <em className="not-italic font-medium gradient-text">Transactional outbox, by default.</em>
      </h2>
      <p className="text-[13px] leading-[1.55] max-w-[540px] mb-6 text-muted-foreground">
        Every command handler is a sealed primary-constructor record. Events are scheduled through a collector — never published mid-handler — so the row commits atomically with the business write.
      </p>

      <div className="grid gap-5 lg:grid-cols-[1.1fr_1fr] items-start">
        <div className="rounded-[10px] overflow-hidden bg-[#1c1815] text-[#d4cfc3] border border-border shadow-float font-mono text-[11px] leading-[1.65]">
          <div className="px-3 py-2 text-[10px] flex gap-1.5 items-center bg-white/5 border-b border-white/[0.06] text-muted-foreground">
            <span className="px-2 py-0.5 rounded bg-[color-mix(in_srgb,var(--color-primary)_18%,transparent)] text-[var(--color-primary-300)] font-sans font-semibold">C# · Application</span>
            <span className="ml-auto">RegisterTenantCommandHandler.cs</span>
          </div>
          <div className="p-4">
            <div><span className="text-[#a5b4fc]">internal sealed class</span> <span className="text-[#6ee7b7]">RegisterTenantCommandHandler</span>(</div>
            <div>  <span className="text-[#6ee7b7]">IApplicationDbContext</span> context,</div>
            <div>  <span className="text-[#6ee7b7]">IIntegrationEventCollector</span> events) <span className="text-muted-foreground italic">// not IPublishEndpoint</span></div>
            <div>  : <span className="text-[#6ee7b7]">IRequestHandler</span>&lt;<span className="text-[#6ee7b7]">RegisterTenantCommand</span>, <span className="text-[#6ee7b7]">Result</span>&lt;<span className="text-[#6ee7b7]">Guid</span>&gt;&gt;</div>
            <div>{`{`}</div>
            <div>  <span className="text-[#a5b4fc]">public async</span> <span className="text-[#6ee7b7]">Task</span>&lt;<span className="text-[#6ee7b7]">Result</span>&lt;<span className="text-[#6ee7b7]">Guid</span>&gt;&gt; <span className="text-[#fbbf24]">Handle</span>(</div>
            <div>    <span className="text-[#6ee7b7]">RegisterTenantCommand</span> cmd, <span className="text-[#6ee7b7]">CancellationToken</span> ct)</div>
            <div>  {`{`}</div>
            <div>    <span className="text-[#a5b4fc]">var</span> tenant = <span className="text-[#6ee7b7]">Tenant</span>.<span className="text-[#fbbf24]">Create</span>(cmd.<span className="text-[#fbbf24]">Name</span>, cmd.<span className="text-[#fbbf24]">Slug</span>);</div>
            <div>    context.Tenants.<span className="text-[#fbbf24]">Add</span>(tenant);</div>
            <div>    events.<span className="text-[#fbbf24]">Schedule</span>(<span className="text-[#a5b4fc]">new</span> <span className="text-[#6ee7b7]">TenantRegisteredEvent</span>(tenant.<span className="text-[#fbbf24]">Id</span>));</div>
            <div>    <span className="text-[#a5b4fc]">await</span> context.<span className="text-[#fbbf24]">SaveChangesAsync</span>(ct); <span className="text-muted-foreground italic">// atomic</span></div>
            <div>    <span className="text-[#a5b4fc]">return</span> <span className="text-[#6ee7b7]">Result</span>.<span className="text-[#fbbf24]">Success</span>(tenant.<span className="text-[#fbbf24]">Id</span>);</div>
            <div>  {`}`}</div>
            <div>{`}`}</div>
          </div>
        </div>

        <div className="flex flex-col gap-3 pt-1">
          {POINTS.map((p) => (
            <div key={p.num} className="flex gap-2.5">
              <span className="font-mono text-[10px] font-bold pt-px min-w-[18px] text-primary">{p.num}</span>
              <span className="text-[12px] leading-[1.55] text-foreground">
                <strong className="font-semibold">{p.title}</strong> {p.body}
              </span>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 2: ArchitectureSection**

`boilerplateFE/src/features/landing/components/ArchitectureSection.tsx`:

```tsx
const CELLS: { label: string; name: string; meta: string }[] = [
  { label: 'Backend', name: '.NET 10', meta: 'CQRS · MediatR · EF Core' },
  { label: 'Frontend', name: 'React 19', meta: 'Tailwind 4 · TanStack · shadcn/ui' },
  { label: 'Mobile', name: 'Flutter 3', meta: 'flutter_bloc · Clean Arch · Hive' },
];

export function ArchitectureSection() {
  return (
    <section id="architecture" className="px-7 py-9 relative z-[2]">
      <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-2">Architecture at a glance</div>
      <h2 className="text-[26px] font-light tracking-[-0.025em] leading-[1.15] mb-2 font-display">
        Three clients.<br />
        <em className="not-italic font-medium gradient-text">One source of truth.</em>
      </h2>
      <p className="text-[13px] leading-[1.55] max-w-[540px] mb-6 text-muted-foreground">
        Permission strings, theme tokens, and API response envelopes mirror across the .NET backend, React frontend, and Flutter mobile client. Define a permission once — it's enforced everywhere.
      </p>

      <div className="surface-glass rounded-xl p-5">
        <div className="grid gap-2 md:grid-cols-3 mb-4">
          {CELLS.map((c) => (
            <div key={c.label} className="bg-card/70 rounded-lg p-3 text-center border border-border/40">
              <div className="text-[9px] font-bold uppercase tracking-[0.18em] text-primary mb-1">{c.label}</div>
              <div className="text-[13px] font-semibold text-foreground mb-1">{c.name}</div>
              <div className="text-[10px] font-mono text-muted-foreground">{c.meta}</div>
            </div>
          ))}
        </div>
        <div className="text-center font-mono text-[10px] text-muted-foreground">
          <strong className="text-primary font-bold">API</strong> → <strong className="text-primary font-bold">Application</strong> (MediatR CQRS) → <strong className="text-primary font-bold">Domain</strong> ← <strong className="text-primary font-bold">Infrastructure</strong> (EF Core, Outbox, Services)
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 3: Update barrel**

```ts
export { CodeSection } from './CodeSection';
export { ArchitectureSection } from './ArchitectureSection';
```

- [ ] **Step 4: Copy + commit**

```bash
cp_fe src/features/landing/components/CodeSection.tsx \
      src/features/landing/components/ArchitectureSection.tsx \
      src/features/landing/components/index.ts

cd boilerplateFE && npx tsc --noEmit && cd ..

git add boilerplateFE/src/features/landing/components/
git commit -m "feat(landing): add CodeSection and ArchitectureSection components"
```

### Task 7: Create StatsStrip + FooterCta

**Files:**
- Create: `boilerplateFE/src/features/landing/components/StatsStrip.tsx`
- Create: `boilerplateFE/src/features/landing/components/FooterCta.tsx`
- Modify: `boilerplateFE/src/features/landing/components/index.ts`

- [ ] **Step 1: StatsStrip**

`boilerplateFE/src/features/landing/components/StatsStrip.tsx`:

```tsx
const STATS: { value: string; label: string }[] = [
  { value: '15', label: 'backend features' },
  { value: '22', label: 'frontend modules' },
  { value: '3', label: 'production clients' },
  { value: '0', label: 'hello-worlds' },
];

export function StatsStrip() {
  return (
    <div className="grid grid-cols-2 md:grid-cols-4 gap-0 px-7 py-5 border-y border-border/30 surface-glass relative z-[2]">
      {STATS.map((s, i) => (
        <div
          key={s.label}
          className={`px-3 ${i < STATS.length - 1 ? 'md:border-r border-border/30' : ''}`}
        >
          <div className="text-[28px] font-light tracking-[-0.03em] leading-none font-display gradient-text">{s.value}</div>
          <div className="text-[10px] font-bold uppercase tracking-[0.15em] mt-1.5 text-muted-foreground">{s.label}</div>
        </div>
      ))}
    </div>
  );
}
```

- [ ] **Step 2: FooterCta**

`boilerplateFE/src/features/landing/components/FooterCta.tsx`:

```tsx
const GITHUB_URL = import.meta.env.VITE_GITHUB_URL || 'https://github.com/<org>/boilerplate-cqrs';

export function FooterCta() {
  return (
    <section className="px-7 py-9 text-center relative z-[2]">
      <h3 className="text-[24px] font-light tracking-[-0.025em] leading-[1.2] mb-2.5 font-display">
        Ship the boring parts<br />
        <em className="not-italic font-medium gradient-text">before lunch.</em>
      </h3>
      <p className="text-[13px] mb-4 text-muted-foreground">
        Clone, run <code className="font-mono text-[12px] px-1 rounded bg-[color-mix(in_srgb,var(--color-primary)_8%,transparent)]">docker compose up</code>, log in as <code className="font-mono text-[12px] px-1 rounded bg-[color-mix(in_srgb,var(--color-primary)_8%,transparent)]">superadmin@starter.com</code> in 60 seconds.
      </p>
      <a
        href={GITHUB_URL}
        className="inline-block px-3.5 py-2 rounded-lg font-mono text-[11px] mb-3.5 bg-[#1c1815] text-[var(--color-primary-300)] border border-white/[0.08] hover:bg-[#252018] transition-colors"
      >
        git clone {GITHUB_URL.replace(/^https:\/\//, '')}
      </a>
      <div className="text-[11px] text-muted-foreground space-x-2">
        <a href="#product" className="hover:text-foreground">Product</a>
        <span>·</span>
        <a href="#docs" className="hover:text-foreground">Docs</a>
        <span>·</span>
        <a href={GITHUB_URL} className="hover:text-foreground">GitHub</a>
        <span>·</span>
        <span>License</span>
        <span>·</span>
        <span>© 2026</span>
      </div>
    </section>
  );
}
```

- [ ] **Step 3: Update barrel**

```ts
export { StatsStrip } from './StatsStrip';
export { FooterCta } from './FooterCta';
```

- [ ] **Step 4: Copy + commit**

```bash
cp_fe src/features/landing/components/StatsStrip.tsx \
      src/features/landing/components/FooterCta.tsx \
      src/features/landing/components/index.ts

cd boilerplateFE && npx tsc --noEmit && cd ..

git add boilerplateFE/src/features/landing/components/
git commit -m "feat(landing): add StatsStrip and FooterCta components"
```

### Task 8: Compose LandingPage

**Files:**
- Modify (rewrite): `boilerplateFE/src/features/landing/pages/LandingPage.tsx`

- [ ] **Step 1: Rewrite LandingPage**

Use Write to overwrite the file with:

```tsx
import {
  ArchitectureSection,
  CodeSection,
  FeatureGrid,
  FooterCta,
  HeroSection,
  LandingNav,
  StatsStrip,
  TechStrip,
} from '@/features/landing/components';

export default function LandingPage() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <LandingNav />
      <HeroSection />
      <TechStrip />
      <FeatureGrid />
      <CodeSection />
      <ArchitectureSection />
      <StatsStrip />
      <FooterCta />
    </div>
  );
}
```

This page is rendered inside `<PublicLayout />` (Task 1) which already provides the aurora canvas, so we don't wrap again here.

- [ ] **Step 2: Copy + verify + commit**

```bash
cp_fe src/features/landing/pages/LandingPage.tsx

cd boilerplateFE && npx tsc --noEmit && npm run build 2>&1 | tail -5 && cd ..

curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3100/

git add boilerplateFE/src/features/landing/pages/LandingPage.tsx
git commit -m "feat(landing): compose LandingPage from 8 J4 sections"
```

Visual check: open `http://localhost:3100/` in the browser. The full J4 landing page should render with aurora, hero gradient text, tech tags, feature grid, code block, architecture, stats, and footer CTA.

---

## Phase 3D — Verification

### Task 9: End-to-end verification

(Inline.)

```bash
# Source build + lint
cd boilerplateFE && npm run build 2>&1 | tail -5 && npm run lint 2>&1 | tail -3 && cd ..

# Test app build
cd _testJ4visual/_testJ4visual-FE && npm run build 2>&1 | tail -3 && cd ../..

# Pages
for url in "/" "/login" "/styleguide" "/dashboard" "/users" "/tenants"; do
  code=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:3100$url")
  echo "$code  $url"
done

# Process check
ps -p $(cat /tmp/_testJ4visual.be.pid) -p $(cat /tmp/_testJ4visual.fe.pid) -o pid,ppid,comm

# Plan 3 commits
git log --oneline fe/base ^f97d852 | head -10
```

---

## Self-Review

**Spec coverage (this plan only — §9 Header, §10 Sidebar, §11 Layouts, §12 Landing):**
- §9 Header — Task 2 (glass background + hairline border).
- §10 Sidebar — Task 3 (glass surface + J4 logo + copper-glow active item + gradient badge).
- §11 Layouts — Task 1 (aurora applied to Main/Auth/Public).
- §12 Landing — Tasks 4-8 (8 reusable section components + composed page).

**Placeholder scan:** No "TBD" / "implement later" in any task. Every task has full code.

**Type consistency:** All landing components named-export the same way; barrel re-exports them. `LandingPage` imports from `@/features/landing/components` (the barrel). No new types introduced.

---

## Execution

Use **superpowers:subagent-driven-development**. Same cadence as plans 1, 2, 2B.
