# Visual Foundation — Phase 2 (Component Restyle: Foundation)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply J4 styling to the most-used primitives (Button, Input, Textarea, Card, Badge, Table) and stand up a development-only `/styleguide` page that renders every restyled component as it ships. After this plan lands, **the app starts to look different** — first visible J4 change of Phase 0.

**Architecture:** Each task either adds a `/styleguide` section, or restyles one primitive + adds its showcase section. The styleguide grows incrementally as a navigable design-system page; component restyles edit the existing shadcn `cn(...)` / CVA strings to consume J4 utilities (`.btn-primary-gradient`, `.glow-primary-md`, `.surface-glass`, etc.) and the preset-aware tokens added in Plan 1. Test app gets per-task file copies for live HMR verification — no regenerate.

**Tech Stack:** React 19, Tailwind 4, TypeScript, class-variance-authority, shadcn/ui, react-router-dom.

**Spec reference:** [docs/superpowers/specs/2026-04-26-phase-0-visual-foundation-design.md](../specs/2026-04-26-phase-0-visual-foundation-design.md) — sections §7.1 (Button), §7.2 (Input/Textarea), §7.3 (Card), §7.7 (Badge), §7.8 (Table), §15 (Style Reference page).

**Plan position:** This is **plan 2 of 3** in Phase 0. Plan 1 (Tokens & Utilities) is complete and on `fe/base`. Plan 3 (Layouts & Landing) follows.

**Branch:** Stay on `fe/base`.

**Test app:** `_testJ4visual` running at `http://localhost:3100` (FE) and `http://localhost:5100` (BE). Login `superadmin@testj4visual.com` / `Admin@123456`.

---

## File Structure

| File | Status | Responsibility |
|---|---|---|
| `boilerplateFE/src/features/styleguide/pages/StyleguidePage.tsx` | Create | Root styleguide page with section navigation + slot for each section |
| `boilerplateFE/src/features/styleguide/components/Section.tsx` | Create | Reusable `<Section>` wrapper (eyebrow, title, deck, children grid) used by all styleguide sections |
| `boilerplateFE/src/features/styleguide/components/sections/TokensSection.tsx` | Create | Visual swatches of every J4 token (color, gradient, shadow, glass) |
| `boilerplateFE/src/features/styleguide/components/sections/TypographySection.tsx` | Create | Renders the type ramp (hero, section, h1-h4, body, eyebrow, mono) |
| `boilerplateFE/src/features/styleguide/components/sections/ButtonsSection.tsx` | Create | All button variants × sizes (incl. loading + disabled) |
| `boilerplateFE/src/features/styleguide/components/sections/FormsSection.tsx` | Create | Input + textarea (default / focused / error / disabled) |
| `boilerplateFE/src/features/styleguide/components/sections/CardsSection.tsx` | Create | Card variants (solid / glass / elevated) with example content |
| `boilerplateFE/src/features/styleguide/components/sections/BadgesSection.tsx` | Create | All badge variants × sizes (status pills) |
| `boilerplateFE/src/features/styleguide/components/sections/TablesSection.tsx` | Create | Full table demo with stats strip header |
| `boilerplateFE/src/features/styleguide/index.ts` | Create | Barrel for styleguide module |
| `boilerplateFE/src/routes/routes.tsx` | Modify | Conditionally register `/styleguide` route (`import.meta.env.DEV` only) |
| `boilerplateFE/src/config/routes.config.ts` | Modify | Add `STYLEGUIDE: '/styleguide'` to `ROUTES` |
| `boilerplateFE/src/components/ui/button.tsx` | Modify | CVA: default = J4 gradient + glow; size scale; loading prop |
| `boilerplateFE/src/components/ui/input.tsx` | Modify | Glass background, hairline border, J4 focus ring |
| `boilerplateFE/src/components/ui/textarea.tsx` | Modify | Same theme as input (glass + hairline) |
| `boilerplateFE/src/components/ui/card.tsx` | Modify | Add `variant` prop with `solid` / `glass` / `elevated`; default stays solid for backwards compat |
| `boilerplateFE/src/components/ui/badge.tsx` | Modify | CVA: add `healthy` / `pending` / `failed` / `info` variants alongside existing ones |
| `boilerplateFE/src/components/ui/table.tsx` | Modify | Glass surface, denser row padding, sticky header support |

Each restyled primitive is a **CSS-only** change (className edits + CVA string updates). No new dependencies. No prop API breakage — variants are purely additive; existing call sites continue to render.

---

## Pre-flight

### Task 0: Confirm test app still healthy

**Files:** none modified.

- [ ] **Step 1: Confirm BE/FE processes still alive**

```bash
ps -p $(cat /tmp/_testJ4visual.be.pid 2>/dev/null) -p $(cat /tmp/_testJ4visual.fe.pid 2>/dev/null) -o pid,ppid,comm 2>/dev/null
```
Expected: two rows printed, both with `PPID 1`, comm `dotnet` and `npm run dev`.

- [ ] **Step 2: Confirm endpoints respond**

```bash
curl -s -o /dev/null -w "BE health: %{http_code}\n" http://localhost:5100/health
curl -s -o /dev/null -w "FE root:   %{http_code}\n" http://localhost:3100/
```
Expected: both `200`.

- [ ] **Step 3: Define `cp_fe` helper for this session**

```bash
cp_fe() {
  for f in "$@"; do
    src="boilerplateFE/$f"
    dst="_testJ4visual/_testJ4visual-FE/$f"
    mkdir -p "$(dirname "$dst")"
    cp "$src" "$dst" && echo "→ $dst"
  done
}
```

(`mkdir -p` was added vs. Plan 1 — this plan creates new files in `src/features/styleguide/`, so the destination directory must be created if it doesn't already exist in the test app.)

If either process is down, restart it before proceeding:
```bash
# BE down?
cd _testJ4visual/_testJ4visual-BE/src/_testJ4visual.Api
nohup dotnet run --launch-profile http > /tmp/_testJ4visual.be.log 2>&1 &
echo $! > /tmp/_testJ4visual.be.pid
cd -

# FE down?
cd _testJ4visual/_testJ4visual-FE
nohup npm run dev > /tmp/_testJ4visual.fe.log 2>&1 &
echo $! > /tmp/_testJ4visual.fe.pid
cd -
```

---

## Phase 2A — Styleguide skeleton

### Task 1: Create `/styleguide` route shell (dev-only)

**Files:**
- Create: `boilerplateFE/src/features/styleguide/pages/StyleguidePage.tsx`
- Create: `boilerplateFE/src/features/styleguide/components/Section.tsx`
- Create: `boilerplateFE/src/features/styleguide/index.ts`
- Modify: `boilerplateFE/src/config/routes.config.ts` (add `STYLEGUIDE` entry)
- Modify: `boilerplateFE/src/routes/routes.tsx` (conditional registration)

- [ ] **Step 1: Create the Section wrapper**

`boilerplateFE/src/features/styleguide/components/Section.tsx`:

```tsx
import type { ReactNode } from 'react';

interface SectionProps {
  id: string;
  eyebrow: string;
  title: string;
  deck?: string;
  children: ReactNode;
}

export function Section({ id, eyebrow, title, deck, children }: SectionProps) {
  return (
    <section id={id} className="scroll-mt-20 border-t border-border first:border-t-0 py-12">
      <div className="text-[11px] font-bold uppercase tracking-[0.18em] text-primary mb-2">{eyebrow}</div>
      <h2 className="text-2xl font-light tracking-tight text-foreground mb-2">{title}</h2>
      {deck ? <p className="text-sm text-muted-foreground max-w-prose mb-6">{deck}</p> : null}
      <div className="space-y-6">{children}</div>
    </section>
  );
}
```

- [ ] **Step 2: Create the StyleguidePage**

`boilerplateFE/src/features/styleguide/pages/StyleguidePage.tsx`:

```tsx
import { Link } from 'react-router-dom';

const SECTIONS: { id: string; label: string }[] = [
  { id: 'tokens', label: 'Tokens' },
  { id: 'typography', label: 'Typography' },
  { id: 'buttons', label: 'Buttons' },
  { id: 'forms', label: 'Forms' },
  { id: 'cards', label: 'Cards' },
  { id: 'badges', label: 'Badges' },
  { id: 'tables', label: 'Tables' },
];

export default function StyleguidePage() {
  return (
    <div className="aurora-canvas min-h-screen bg-background text-foreground">
      <div className="mx-auto flex max-w-6xl gap-10 px-6 py-10">
        <aside className="sticky top-10 hidden h-fit w-44 shrink-0 lg:block">
          <div className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground mb-3">
            Sections
          </div>
          <nav className="flex flex-col gap-1">
            {SECTIONS.map((s) => (
              <a
                key={s.id}
                href={`#${s.id}`}
                className="rounded-md px-2 py-1.5 text-sm text-muted-foreground hover:bg-secondary hover:text-foreground transition-colors"
              >
                {s.label}
              </a>
            ))}
          </nav>
          <div className="mt-6 border-t border-border pt-4 text-xs text-muted-foreground">
            <Link to="/" className="hover:text-foreground">← Back to app</Link>
          </div>
        </aside>

        <main className="min-w-0 flex-1">
          <header className="mb-8">
            <div className="text-[11px] font-bold uppercase tracking-[0.2em] text-primary mb-2">
              J4 Spectrum · dev-only
            </div>
            <h1 className="text-4xl font-extralight tracking-tight text-foreground">
              <span className="gradient-text">Style Reference</span>
            </h1>
            <p className="mt-3 max-w-prose text-sm text-muted-foreground">
              Live render of every primitive and shared component in the J4 Spectrum design system.
              This page is registered only in dev builds (<code>import.meta.env.DEV</code>).
            </p>
          </header>

          <div className="space-y-0">
            {/* Sections will be slotted in by later tasks */}
          </div>
        </main>
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Create barrel export**

`boilerplateFE/src/features/styleguide/index.ts`:

```ts
export { default as StyleguidePage } from './pages/StyleguidePage';
```

- [ ] **Step 4: Add the `STYLEGUIDE` constant to routes.config.ts**

Open `boilerplateFE/src/config/routes.config.ts`. Find the `ROUTES` constant. Add a new entry:

```ts
STYLEGUIDE: '/styleguide',
```

Place it logically (e.g. near `LANDING`). The exact insertion point depends on the file's current shape — read it first if uncertain.

- [ ] **Step 5: Conditionally register the route**

In `boilerplateFE/src/routes/routes.tsx`:

a) Add the lazy import alongside other landing/auth lazy imports (near the top). **Gate the lazy import itself, not just the route entry** — otherwise the dynamic import lives at module top-level and Vite emits the Styleguide chunk into prod even with a gated route. Pattern:

```ts
// eslint-disable-next-line react-refresh/only-export-components
const StyleguidePage = import.meta.env.DEV
  ? lazy(() => import('@/features/styleguide/pages/StyleguidePage'))
  : (() => null);
```

In prod, `StyleguidePage` resolves to `() => null` and Vite tree-shakes the dynamic import entirely — `dist/` ends up with zero Styleguide references.

b) Find the `routes: RouteObject[]` array. The first entry currently registers `PublicLayout` with `LANDING` (and conditionally `PRICING`). After the closing `]` of that `children` array but before the closing `}` of that route entry, the cleanest pattern is to add `/styleguide` as a sibling top-level route — or extend the public-layout children. Use the **public-layout children** pattern so `/styleguide` is reachable when logged out:

Find the public layout entry that looks like:

```tsx
{
  element: <PublicLayout />,
  children: [
    { path: ROUTES.LANDING, element: <LandingPage /> },
    ...(activeModules.billing ? [{ path: ROUTES.PRICING, element: <PricingPage /> }] : []),
  ],
},
```

Modify it to:

```tsx
{
  element: <PublicLayout />,
  children: [
    { path: ROUTES.LANDING, element: <LandingPage /> },
    ...(activeModules.billing ? [{ path: ROUTES.PRICING, element: <PricingPage /> }] : []),
    ...(import.meta.env.DEV ? [{ path: ROUTES.STYLEGUIDE, element: <StyleguidePage /> }] : []),
  ],
},
```

The `import.meta.env.DEV` check is a build-time constant — Vite tree-shakes the styleguide entirely from production bundles.

- [ ] **Step 6: Type-check**

```bash
cd boilerplateFE && npx tsc --noEmit && cd ..
```
Expected: zero errors.

- [ ] **Step 7: Copy new + modified files to test app**

```bash
cp_fe src/features/styleguide/pages/StyleguidePage.tsx \
      src/features/styleguide/components/Section.tsx \
      src/features/styleguide/index.ts \
      src/config/routes.config.ts \
      src/routes/routes.tsx
```

- [ ] **Step 8: Verify in browser**

Open `http://localhost:3100/styleguide`. Expected:
- Page loads with the J4 aurora visible behind content (this is the **first visible J4 effect** in the app).
- Sidebar lists 7 section names.
- Header has gradient-text "Style Reference" title (copper → violet across the word).
- Empty content area below the header (sections come in later tasks).
- DevTools console: no errors.

If the aurora isn't visible, confirm `index.css` from Plan 1 is in effect:
```bash
curl -s http://localhost:3100/src/styles/index.css | grep -c "aurora-canvas"
```
Expected: > 0.

- [ ] **Step 9: Source build passes**

```bash
cd boilerplateFE && npm run build 2>&1 | tail -8 && cd ..
```
Expected: `✓ built in ...`. Confirm the production bundle does NOT include the styleguide:
```bash
grep -lr "StyleguidePage" boilerplateFE/dist/ 2>/dev/null | head -3
```
Expected: empty (Vite tree-shaked it for prod).

- [ ] **Step 10: Commit**

```bash
git add boilerplateFE/src/features/styleguide/ \
        boilerplateFE/src/config/routes.config.ts \
        boilerplateFE/src/routes/routes.tsx
git commit -m "feat(styleguide): add /styleguide dev-only route with section navigation skeleton"
```

### Task 2: Tokens section

**Files:**
- Create: `boilerplateFE/src/features/styleguide/components/sections/TokensSection.tsx`
- Modify: `boilerplateFE/src/features/styleguide/pages/StyleguidePage.tsx` (slot the section in)

- [ ] **Step 1: Create the TokensSection**

`boilerplateFE/src/features/styleguide/components/sections/TokensSection.tsx`:

```tsx
import { Section } from '../Section';

const SWATCH_GROUPS: { label: string; tokens: { name: string; cssVar: string }[] }[] = [
  {
    label: 'Primary scale (preset-driven)',
    tokens: ['50', '100', '200', '300', '400', '500', '600', '700', '800', '900', '950'].map((s) => ({
      name: `primary-${s}`,
      cssVar: `--color-primary-${s}`,
    })),
  },
  {
    label: 'Accent (emerald)',
    tokens: ['400', '500', '600', '700'].map((s) => ({
      name: `accent-${s}`,
      cssVar: `--color-accent-${s}`,
    })),
  },
  {
    label: 'Violet companion (info / spectrum cool axis)',
    tokens: ['300', '400', '500', '600', '700'].map((s) => ({
      name: `violet-${s}`,
      cssVar: `--color-violet-${s}`,
    })),
  },
  {
    label: 'Amber companion (warning / feature-icon rotation)',
    tokens: ['400', '500', '600', '700'].map((s) => ({
      name: `amber-${s}`,
      cssVar: `--color-amber-${s}`,
    })),
  },
];

const COMPOSITE_TOKENS: { name: string; cssVar: string; render: 'gradient' | 'shadow' | 'glass' }[] = [
  { name: 'aurora-1', cssVar: '--aurora-1', render: 'gradient' },
  { name: 'aurora-2', cssVar: '--aurora-2', render: 'gradient' },
  { name: 'aurora-3', cssVar: '--aurora-3', render: 'gradient' },
  { name: 'aurora-corner', cssVar: '--aurora-corner', render: 'gradient' },
  { name: 'spectrum-text', cssVar: '--spectrum-text', render: 'gradient' },
  { name: 'btn-primary-gradient', cssVar: '--btn-primary-gradient', render: 'gradient' },
  { name: 'glow-primary-sm', cssVar: '--glow-primary-sm', render: 'shadow' },
  { name: 'glow-primary-md', cssVar: '--glow-primary-md', render: 'shadow' },
  { name: 'glow-primary-lg', cssVar: '--glow-primary-lg', render: 'shadow' },
  { name: 'surface-glass', cssVar: '--surface-glass', render: 'glass' },
  { name: 'surface-glass-strong', cssVar: '--surface-glass-strong', render: 'glass' },
];

export function TokensSection() {
  return (
    <Section
      id="tokens"
      eyebrow="Tokens"
      title="Color, gradient, glow, glass"
      deck="Every token is defined in src/styles/index.css. Color scales are runtime-written by useThemePreset; composite tokens use color-mix() and var() to follow the active preset automatically."
    >
      {SWATCH_GROUPS.map((group) => (
        <div key={group.label}>
          <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground mb-2">{group.label}</div>
          <div className="grid grid-cols-6 gap-2 lg:grid-cols-11">
            {group.tokens.map((t) => (
              <div key={t.name} className="rounded-md border border-border overflow-hidden">
                <div className="h-12" style={{ background: `var(${t.cssVar})` }} />
                <div className="p-2 text-[10px] font-mono text-muted-foreground">{t.name}</div>
              </div>
            ))}
          </div>
        </div>
      ))}

      <div>
        <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground mb-2">
          Composite tokens
        </div>
        <div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-4">
          {COMPOSITE_TOKENS.map((t) => (
            <div key={t.name} className="rounded-md border border-border overflow-hidden bg-card">
              {t.render === 'gradient' && (
                <div className="h-20" style={{ background: `var(${t.cssVar})` }} />
              )}
              {t.render === 'shadow' && (
                <div className="h-20 flex items-center justify-center bg-card">
                  <div className="h-8 w-8 rounded-md bg-primary" style={{ boxShadow: `var(${t.cssVar})` }} />
                </div>
              )}
              {t.render === 'glass' && (
                <div className="aurora-canvas h-20 relative">
                  <div
                    className="absolute inset-3 rounded-md"
                    style={{ background: `var(${t.cssVar})`, border: '1px solid var(--border-strong)', backdropFilter: 'blur(20px)' }}
                  />
                </div>
              )}
              <div className="p-2 text-[10px] font-mono text-muted-foreground">{t.name}</div>
            </div>
          ))}
        </div>
      </div>
    </Section>
  );
}
```

- [ ] **Step 2: Slot the section into StyleguidePage**

In `boilerplateFE/src/features/styleguide/pages/StyleguidePage.tsx`, add the import at the top:

```tsx
import { TokensSection } from '../components/sections/TokensSection';
```

Replace the empty `{/* Sections will be slotted in by later tasks */}` placeholder with:

```tsx
<TokensSection />
```

- [ ] **Step 3: Copy to test app**

```bash
cp_fe src/features/styleguide/pages/StyleguidePage.tsx \
      src/features/styleguide/components/sections/TokensSection.tsx
```

- [ ] **Step 4: Verify in browser**

Refresh `http://localhost:3100/styleguide`. Expected:
- "Tokens" section visible with all primary/accent/violet/amber swatches.
- Composite tokens row shows aurora gradients, glow halos (the dot with shadow), and glass surfaces (translucent on the aurora-canvas demo).
- Toggle the theme — every swatch and gradient adapts (because tokens are runtime-driven).

- [ ] **Step 5: Type-check + commit**

```bash
cd boilerplateFE && npx tsc --noEmit && cd ..
git add boilerplateFE/src/features/styleguide/
git commit -m "feat(styleguide): add tokens section showing all J4 color, gradient, glow, glass tokens"
```

### Task 3: Typography section

**Files:**
- Create: `boilerplateFE/src/features/styleguide/components/sections/TypographySection.tsx`
- Modify: `StyleguidePage.tsx` (slot it in)

- [ ] **Step 1: Create TypographySection**

`boilerplateFE/src/features/styleguide/components/sections/TypographySection.tsx`:

```tsx
import { Section } from '../Section';

const SAMPLES: { label: string; className: string; sample: string; gradient?: boolean }[] = [
  { label: 'Hero metric (Inter 84px / 200)', className: 'text-[84px] font-extralight tracking-[-0.05em] leading-[0.95] font-display', sample: '142' },
  { label: 'Hero headline (Inter 44px / 200)', className: 'text-[44px] font-extralight tracking-[-0.04em] leading-[1.05] font-display', sample: 'Build what’s actually yours.' },
  { label: 'Hero gradient accent', className: 'text-[44px] font-medium tracking-[-0.04em] leading-[1.05] font-display gradient-text', sample: 'next-gen platform', gradient: true },
  { label: 'Section title (Inter 26px / 300)', className: 'text-[26px] font-light tracking-tight leading-[1.15] font-display', sample: 'Eight things every SaaS needs.' },
  { label: 'Page title (Inter 22px / 600)', className: 'text-[22px] font-semibold tracking-tight font-display', sample: 'Tenants' },
  { label: 'h1 (IBM Plex 1.75rem / 700)', className: 'text-[1.75rem] font-bold tracking-tight', sample: 'Heading 1' },
  { label: 'h2 (IBM Plex 1.25rem / 600)', className: 'text-[1.25rem] font-semibold', sample: 'Heading 2' },
  { label: 'h3 (1.0625rem / 600)', className: 'text-[1.0625rem] font-semibold', sample: 'Heading 3' },
  { label: 'h4 (0.9375rem / 600)', className: 'text-[0.9375rem] font-semibold', sample: 'Heading 4' },
  { label: 'Body (0.8125rem)', className: 'text-[0.8125rem] leading-[1.6]', sample: 'Body text. The quick brown fox jumps over the lazy dog.' },
  { label: 'Small (0.75rem)', className: 'text-[0.75rem]', sample: 'Small text — captions and metadata.' },
  { label: 'Eyebrow (10px uppercase 0.18em)', className: 'text-[10px] font-bold uppercase tracking-[0.18em] text-muted-foreground', sample: 'Section label' },
  { label: 'Mono numbers (IBM Plex Mono 12px)', className: 'text-[12px] font-mono text-foreground', sample: '142  3,847  $48.2K  +8.2%' },
  { label: 'Mono IDs (IBM Plex Mono 11px)', className: 'text-[11px] font-mono text-muted-foreground', sample: 'acme-corp  globex-ind  initech-sys' },
];

export function TypographySection() {
  return (
    <Section
      id="typography"
      eyebrow="Typography"
      title="Type ramp"
      deck="Inter for hero/section titles (font-display utility). IBM Plex Sans for body and UI. IBM Plex Sans Arabic auto-swaps in RTL. IBM Plex Mono for numbers and IDs."
    >
      <div className="space-y-5">
        {SAMPLES.map((s) => (
          <div key={s.label} className="grid grid-cols-1 gap-2 md:grid-cols-[180px_1fr] md:gap-6 md:items-baseline">
            <div className="text-[10px] font-mono text-muted-foreground">{s.label}</div>
            <div>
              {s.gradient ? <span className={s.className}>{s.sample}</span> : <span className={s.className}>{s.sample}</span>}
            </div>
          </div>
        ))}
      </div>
    </Section>
  );
}
```

- [ ] **Step 2: Slot into StyleguidePage**

In `StyleguidePage.tsx`, add the import:
```tsx
import { TypographySection } from '../components/sections/TypographySection';
```

Add `<TypographySection />` immediately after `<TokensSection />`.

- [ ] **Step 3: Copy to test app + verify**

```bash
cp_fe src/features/styleguide/pages/StyleguidePage.tsx \
      src/features/styleguide/components/sections/TypographySection.tsx
```

Refresh `http://localhost:3100/styleguide`. Scroll to Typography. Verify:
- Hero metric (`142`) renders large, ultra-light, in Inter (different from IBM Plex).
- Gradient accent moment ("next-gen platform") is visible copper→violet.
- Mono samples render in IBM Plex Mono (clearly different from IBM Plex Sans).

If "Hero metric" renders in IBM Plex (not Inter), `--font-display` registration didn't ship to the served bundle yet — confirm Plan 1 Task 3 was applied to the test app:
```bash
diff -q boilerplateFE/src/styles/index.css _testJ4visual/_testJ4visual-FE/src/styles/index.css
```
Expected: no output (files identical).

- [ ] **Step 4: Type-check + commit**

```bash
cd boilerplateFE && npx tsc --noEmit && cd ..
git add boilerplateFE/src/features/styleguide/
git commit -m "feat(styleguide): add typography section rendering full type ramp"
```

---

## Phase 2B — Foundation primitives

### Task 4: Restyle Button + Buttons section

**Files:**
- Modify: `boilerplateFE/src/components/ui/button.tsx`
- Create: `boilerplateFE/src/features/styleguide/components/sections/ButtonsSection.tsx`
- Modify: `StyleguidePage.tsx`

- [ ] **Step 1: Restyle Button — update CVA strings**

In `boilerplateFE/src/components/ui/button.tsx`, find the `buttonVariants` block (lines 7-35). Use the Edit tool. **Old string** is the entire `cva(...)` call (lines 7-35). **New string**:

```ts
const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-xl text-sm font-medium transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20 disabled:pointer-events-none disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        default:
          "btn-primary-gradient glow-primary-md hover:brightness-105 active:scale-[0.98]",
        destructive:
          "bg-destructive text-destructive-foreground shadow-soft-sm hover:shadow-soft-md hover:brightness-105 active:scale-[0.98]",
        outline:
          "border border-border/50 bg-card [color:var(--active-text)] hover:bg-secondary hover:border-border",
        secondary:
          "surface-glass text-foreground hover:bg-secondary",
        ghost:
          "text-muted-foreground hover:[background:var(--active-bg)] hover:[color:var(--active-text)]",
        link: "text-primary underline-offset-4 hover:underline",
      },
      size: {
        default: "h-10 px-5 py-2",
        sm: "h-8 rounded-lg px-3 text-xs",
        lg: "h-11 rounded-xl px-8",
        icon: "h-9 w-9 rounded-lg",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
)
```

What changed:
- `default` variant — was `bg-primary text-primary-foreground shadow-soft-sm hover:shadow-soft-md`, now uses `.btn-primary-gradient` (J4 gradient) + `.glow-primary-md` (J4 halo). Hover brighten + active press are kept.
- `secondary` variant — was `bg-secondary text-secondary-foreground hover:bg-secondary/70`, now uses `.surface-glass` for the glassmorphic translucent treatment.

All other variants (destructive, outline, ghost, link) and all sizes are unchanged. **No call site changes needed** — the same `<Button variant="default" size="lg">` calls render with the new style.

- [ ] **Step 2: Create ButtonsSection**

`boilerplateFE/src/features/styleguide/components/sections/ButtonsSection.tsx`:

```tsx
import { Button } from '@/components/ui/button';
import { Section } from '../Section';

export function ButtonsSection() {
  return (
    <Section
      id="buttons"
      eyebrow="Buttons"
      title="Variants × sizes"
      deck="default uses .btn-primary-gradient + .glow-primary-md. secondary uses .surface-glass. Other variants unchanged from baseline."
    >
      <div>
        <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground mb-3">Variants (default size)</div>
        <div className="flex flex-wrap items-center gap-3">
          <Button>default</Button>
          <Button variant="destructive">destructive</Button>
          <Button variant="outline">outline</Button>
          <Button variant="secondary">secondary</Button>
          <Button variant="ghost">ghost</Button>
          <Button variant="link">link</Button>
        </div>
      </div>

      <div>
        <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground mb-3">Sizes (default variant)</div>
        <div className="flex flex-wrap items-center gap-3">
          <Button size="sm">sm</Button>
          <Button size="default">default</Button>
          <Button size="lg">lg</Button>
        </div>
      </div>

      <div>
        <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground mb-3">States</div>
        <div className="flex flex-wrap items-center gap-3">
          <Button disabled>disabled</Button>
          <Button variant="outline" disabled>outline disabled</Button>
        </div>
      </div>
    </Section>
  );
}
```

- [ ] **Step 3: Slot into StyleguidePage**

Add import:
```tsx
import { ButtonsSection } from '../components/sections/ButtonsSection';
```

Add `<ButtonsSection />` after `<TypographySection />`.

- [ ] **Step 4: Copy to test app + verify**

```bash
cp_fe src/components/ui/button.tsx \
      src/features/styleguide/pages/StyleguidePage.tsx \
      src/features/styleguide/components/sections/ButtonsSection.tsx
```

Refresh `http://localhost:3100/styleguide`. Scroll to Buttons. Verify:
- `default` button is filled with the copper gradient + has a soft halo around it (this is the J4 primary CTA — the iconic look).
- `secondary` button has a glass/translucent appearance (visible against the aurora behind the styleguide).
- Other variants look the same as before.

Now check existing pages — open `http://localhost:3100/login` and confirm:
- The "Sign in" button (which uses `<Button>` default variant) now has the gradient + glow. **This is the first cross-app J4 effect.**

- [ ] **Step 5: Type-check + build**

```bash
cd boilerplateFE && npx tsc --noEmit && npm run build 2>&1 | tail -5 && cd ..
```
Expected: zero errors, build succeeds.

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/components/ui/button.tsx \
        boilerplateFE/src/features/styleguide/
git commit -m "feat(theme): restyle Button — default = J4 gradient + glow; secondary = glass"
```

### Task 5: Restyle Input + Textarea + Forms section

**Files:**
- Modify: `boilerplateFE/src/components/ui/input.tsx`
- Modify: `boilerplateFE/src/components/ui/textarea.tsx`
- Create: `boilerplateFE/src/features/styleguide/components/sections/FormsSection.tsx`
- Modify: `StyleguidePage.tsx`

- [ ] **Step 1: Restyle Input — glass background + hairline border**

In `boilerplateFE/src/components/ui/input.tsx` (line 11), use Edit. **Old string**:

```
"flex h-10 w-full rounded-xl border border-border bg-card px-3.5 py-2 text-sm text-foreground transition-all duration-200 file:border-0 file:bg-transparent file:text-sm file:font-medium file:text-foreground placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20 focus-visible:border-primary/40 disabled:cursor-not-allowed disabled:opacity-50",
```

**New string**:

```
"flex h-10 w-full rounded-xl border border-border bg-[var(--surface-glass)] backdrop-blur-md px-3.5 py-2 text-sm text-foreground transition-all duration-200 file:border-0 file:bg-transparent file:text-sm file:font-medium file:text-foreground placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20 focus-visible:border-primary/40 disabled:cursor-not-allowed disabled:opacity-50",
```

What changed: `bg-card` → `bg-[var(--surface-glass)] backdrop-blur-md`. The input now has the J4 glass treatment — translucent in light mode (70% white), translucent in dark (4% white), with a subtle backdrop blur that picks up aurora hues.

- [ ] **Step 2: Restyle Textarea — same glass treatment**

In `boilerplateFE/src/components/ui/textarea.tsx` (line 12), use Edit. **Old string**:

```
"flex min-h-[80px] w-full rounded-xl border border-border bg-card px-3.5 py-2.5 text-sm text-foreground transition-all duration-200 placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20 focus-visible:border-primary/40 disabled:cursor-not-allowed disabled:opacity-50",
```

**New string**:

```
"flex min-h-[80px] w-full rounded-xl border border-border bg-[var(--surface-glass)] backdrop-blur-md px-3.5 py-2.5 text-sm text-foreground transition-all duration-200 placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20 focus-visible:border-primary/40 disabled:cursor-not-allowed disabled:opacity-50",
```

Same single-token swap.

- [ ] **Step 3: Create FormsSection**

`boilerplateFE/src/features/styleguide/components/sections/FormsSection.tsx`:

```tsx
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Section } from '../Section';

export function FormsSection() {
  return (
    <Section
      id="forms"
      eyebrow="Forms"
      title="Input · Textarea"
      deck="Both use --surface-glass background with a backdrop blur, so the aurora bleeds through subtly. Hairline border + soft focus ring."
    >
      <div className="grid gap-6 md:grid-cols-2">
        <div className="space-y-3">
          <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground">Input</div>
          <Input placeholder="Default" />
          <Input defaultValue="Filled value" />
          <Input placeholder="Disabled" disabled />
          <Input type="email" placeholder="email@example.com" />
        </div>
        <div className="space-y-3">
          <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground">Textarea</div>
          <Textarea placeholder="Default textarea — type something." />
          <Textarea defaultValue="Filled textarea." />
          <Textarea placeholder="Disabled" disabled />
        </div>
      </div>
    </Section>
  );
}
```

- [ ] **Step 4: Slot into StyleguidePage**

Add import + render `<FormsSection />` after `<ButtonsSection />`.

- [ ] **Step 5: Copy + verify + commit**

```bash
cp_fe src/components/ui/input.tsx \
      src/components/ui/textarea.tsx \
      src/features/styleguide/pages/StyleguidePage.tsx \
      src/features/styleguide/components/sections/FormsSection.tsx
```

Refresh `/styleguide`. Forms section should show inputs and textareas with subtly translucent backgrounds. Toggle dark mode — they should still feel translucent but darker.

```bash
cd boilerplateFE && npx tsc --noEmit && cd ..
git add boilerplateFE/src/components/ui/input.tsx \
        boilerplateFE/src/components/ui/textarea.tsx \
        boilerplateFE/src/features/styleguide/
git commit -m "feat(theme): restyle Input/Textarea with glass surface + backdrop blur"
```

### Task 6: Restyle Card + Cards section

**Files:**
- Modify: `boilerplateFE/src/components/ui/card.tsx` (add `variant` prop with new variants)
- Create: `boilerplateFE/src/features/styleguide/components/sections/CardsSection.tsx`
- Modify: `StyleguidePage.tsx`

- [ ] **Step 1: Restyle Card — add CVA-driven variants**

The current `Card` component is a single `forwardRef` with hardcoded styles. We're adding a `variant` prop that selects between `solid` (default — same as today, backwards compatible), `glass` (J4 glass surface), and `elevated` (lifts on hover).

Replace the full content of `boilerplateFE/src/components/ui/card.tsx` with:

```tsx
import * as React from "react"
import { cva, type VariantProps } from "class-variance-authority"

import { cn } from "@/lib/utils"

const cardVariants = cva(
  "rounded-2xl text-card-foreground transition-all duration-200",
  {
    variants: {
      variant: {
        solid: "bg-card shadow-card",
        glass: "surface-glass",
        elevated: "bg-card shadow-card hover:-translate-y-0.5 hover:shadow-card-hover cursor-pointer",
      },
    },
    defaultVariants: {
      variant: "solid",
    },
  }
)

export interface CardProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof cardVariants> {}

const Card = React.forwardRef<HTMLDivElement, CardProps>(
  ({ className, variant, ...props }, ref) => (
    <div
      ref={ref}
      className={cn(cardVariants({ variant }), className)}
      {...props}
    />
  )
)
Card.displayName = "Card"

const CardHeader = React.forwardRef<
  HTMLDivElement,
  React.HTMLAttributes<HTMLDivElement>
>(({ className, ...props }, ref) => (
  <div
    ref={ref}
    className={cn("flex flex-col space-y-1.5 p-6", className)}
    {...props}
  />
))
CardHeader.displayName = "CardHeader"

const CardTitle = React.forwardRef<
  HTMLDivElement,
  React.HTMLAttributes<HTMLDivElement>
>(({ className, ...props }, ref) => (
  <div
    ref={ref}
    className={cn("font-semibold leading-none tracking-tight", className)}
    {...props}
  />
))
CardTitle.displayName = "CardTitle"

const CardDescription = React.forwardRef<
  HTMLDivElement,
  React.HTMLAttributes<HTMLDivElement>
>(({ className, ...props }, ref) => (
  <div
    ref={ref}
    className={cn("text-sm text-muted-foreground", className)}
    {...props}
  />
))
CardDescription.displayName = "CardDescription"

const CardContent = React.forwardRef<
  HTMLDivElement,
  React.HTMLAttributes<HTMLDivElement>
>(({ className, ...props }, ref) => (
  <div ref={ref} className={cn("p-6 pt-0", className)} {...props} />
))
CardContent.displayName = "CardContent"

const CardFooter = React.forwardRef<
  HTMLDivElement,
  React.HTMLAttributes<HTMLDivElement>
>(({ className, ...props }, ref) => (
  <div
    ref={ref}
    className={cn("flex items-center p-6 pt-0", className)}
    {...props}
  />
))
CardFooter.displayName = "CardFooter"

// eslint-disable-next-line react-refresh/only-export-components
export { Card, CardHeader, CardFooter, CardTitle, CardDescription, CardContent, cardVariants }
```

Backwards compat: `defaultVariants.variant = "solid"` produces `bg-card shadow-card transition-all duration-200 rounded-2xl text-card-foreground` — identical CSS to the previous hardcoded version. Existing `<Card>` calls render unchanged.

- [ ] **Step 2: Create CardsSection**

`boilerplateFE/src/features/styleguide/components/sections/CardsSection.tsx`:

```tsx
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Section } from '../Section';

export function CardsSection() {
  return (
    <Section
      id="cards"
      eyebrow="Cards"
      title="Solid · Glass · Elevated"
      deck="solid is the default (backwards-compatible). glass uses .surface-glass — translucent over aurora. elevated lifts on hover for clickable list cards."
    >
      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle>solid</CardTitle>
            <CardDescription>Default — opaque card surface with shadow.</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              Use for the bulk of in-app content where cards sit on flat backgrounds.
            </p>
          </CardContent>
        </Card>
        <Card variant="glass">
          <CardHeader>
            <CardTitle>glass</CardTitle>
            <CardDescription>Translucent — picks up aurora behind it.</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              Use on landing/marketing surfaces and any context where a card sits on the aurora canvas.
            </p>
          </CardContent>
        </Card>
        <Card variant="elevated">
          <CardHeader>
            <CardTitle>elevated</CardTitle>
            <CardDescription>Lifts and gains shadow on hover.</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              Hover me. Use for clickable list cards or any card with affordance.
            </p>
          </CardContent>
        </Card>
      </div>
    </Section>
  );
}
```

- [ ] **Step 3: Slot into StyleguidePage**

Add import + render `<CardsSection />` after `<FormsSection />`.

- [ ] **Step 4: Copy + verify + commit**

```bash
cp_fe src/components/ui/card.tsx \
      src/features/styleguide/pages/StyleguidePage.tsx \
      src/features/styleguide/components/sections/CardsSection.tsx
```

Refresh `/styleguide`. Cards section should show three side-by-side cards with distinct surfaces. Hover over `elevated` to see it lift. Verify in app pages that existing `<Card>` usages still render identically (the default `solid` variant matches the previous behavior).

```bash
cd boilerplateFE && npx tsc --noEmit && cd ..
git add boilerplateFE/src/components/ui/card.tsx \
        boilerplateFE/src/features/styleguide/
git commit -m "feat(theme): add Card variants (solid|glass|elevated); default solid is backwards-compatible"
```

### Task 7: Restyle Badge + Badges section

**Files:**
- Modify: `boilerplateFE/src/components/ui/badge.tsx` (add status variants)
- Create: `boilerplateFE/src/features/styleguide/components/sections/BadgesSection.tsx`
- Modify: `StyleguidePage.tsx`

- [ ] **Step 1: Restyle Badge — add J4 status variants**

The current Badge has 4 variants (`default`, `secondary`, `destructive`, `outline`). We're adding 4 J4 status variants (`healthy`, `pending`, `failed`, `info`) without removing existing ones, so existing call sites continue to work.

Replace the `badgeVariants` block in `boilerplateFE/src/components/ui/badge.tsx` (lines 6-24) using Edit. **Old string**:

```ts
const badgeVariants = cva(
  "inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium transition-colors",
  {
    variants: {
      variant: {
        default:
          "[background:var(--active-bg)] [color:var(--active-text)]",
        secondary:
          "bg-secondary text-muted-foreground",
        destructive:
          "bg-destructive/10 text-destructive dark:bg-destructive/20",
        outline: "bg-secondary/60 text-muted-foreground",
      },
    },
    defaultVariants: {
      variant: "default",
    },
  }
)
```

**New string**:

```ts
const badgeVariants = cva(
  "inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium transition-colors",
  {
    variants: {
      variant: {
        default:
          "[background:var(--active-bg)] [color:var(--active-text)]",
        secondary:
          "bg-secondary text-muted-foreground",
        destructive:
          "bg-destructive/10 text-destructive dark:bg-destructive/20",
        outline: "bg-secondary/60 text-muted-foreground",
        // J4 status pills — paired with --color-accent (emerald), --color-amber, --color-violet
        healthy:
          "bg-[color-mix(in_srgb,var(--color-accent-500)_10%,transparent)] text-accent-700 dark:text-accent-300 border border-[color-mix(in_srgb,var(--color-accent-500)_20%,transparent)]",
        pending:
          "bg-[color-mix(in_srgb,var(--color-amber-500)_12%,transparent)] text-amber-700 dark:text-amber-300 border border-[color-mix(in_srgb,var(--color-amber-500)_25%,transparent)]",
        failed:
          "bg-destructive/10 text-destructive border border-destructive/20",
        info:
          "bg-[color-mix(in_srgb,var(--color-violet-500)_10%,transparent)] text-violet-700 dark:text-violet-300 border border-[color-mix(in_srgb,var(--color-violet-500)_20%,transparent)]",
      },
    },
    defaultVariants: {
      variant: "default",
    },
  }
)
```

- [ ] **Step 2: Create BadgesSection**

`boilerplateFE/src/features/styleguide/components/sections/BadgesSection.tsx`:

```tsx
import { Badge } from '@/components/ui/badge';
import { Section } from '../Section';

export function BadgesSection() {
  return (
    <Section
      id="badges"
      eyebrow="Badges"
      title="Existing variants + J4 status pills"
      deck="default / secondary / destructive / outline are unchanged. J4 adds healthy (emerald), pending (amber), failed (destructive), and info (violet) for consistent status semantics across feature pages."
    >
      <div>
        <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground mb-3">Existing</div>
        <div className="flex flex-wrap gap-2">
          <Badge>default</Badge>
          <Badge variant="secondary">secondary</Badge>
          <Badge variant="destructive">destructive</Badge>
          <Badge variant="outline">outline</Badge>
        </div>
      </div>
      <div>
        <div className="text-[10px] font-bold uppercase tracking-[0.15em] text-muted-foreground mb-3">J4 status pills</div>
        <div className="flex flex-wrap gap-2">
          <Badge variant="healthy">Healthy</Badge>
          <Badge variant="pending">Pending</Badge>
          <Badge variant="failed">Failed</Badge>
          <Badge variant="info">Info</Badge>
        </div>
      </div>
    </Section>
  );
}
```

- [ ] **Step 3: Slot into StyleguidePage**

Add import + render `<BadgesSection />` after `<CardsSection />`.

- [ ] **Step 4: Copy + verify + commit**

```bash
cp_fe src/components/ui/badge.tsx \
      src/features/styleguide/pages/StyleguidePage.tsx \
      src/features/styleguide/components/sections/BadgesSection.tsx
```

Refresh `/styleguide`. Badges section should show 8 pills total — 4 existing + 4 J4 status. Toggle dark mode — text colors should adjust per the `dark:text-*` overrides.

```bash
cd boilerplateFE && npx tsc --noEmit && cd ..
git add boilerplateFE/src/components/ui/badge.tsx \
        boilerplateFE/src/features/styleguide/
git commit -m "feat(theme): add J4 badge variants (healthy/pending/failed/info); existing variants untouched"
```

### Task 8: Restyle Table + Tables section

**Files:**
- Modify: `boilerplateFE/src/components/ui/table.tsx`
- Create: `boilerplateFE/src/features/styleguide/components/sections/TablesSection.tsx`
- Modify: `StyleguidePage.tsx`

- [ ] **Step 1: Restyle Table — glass container, denser rows**

In `boilerplateFE/src/components/ui/table.tsx` (line 9), use Edit. **Old string**:

```tsx
  <div className="relative w-full overflow-x-auto rounded-2xl bg-card shadow-card">
```

**New string**:

```tsx
  <div className="relative w-full overflow-x-auto rounded-2xl surface-glass">
```

Then **TableHeader** (line 23) — change to use a tighter monospace eyebrow vibe. **Old string**:

```tsx
  <thead ref={ref} className={cn("bg-secondary [&_tr]:border-b [&_tr]:border-border", className)} {...props} />
```

**New string**:

```tsx
  <thead ref={ref} className={cn("bg-[color-mix(in_srgb,var(--color-primary)_5%,transparent)] [&_tr]:border-b [&_tr]:border-border", className)} {...props} />
```

(Subtle copper-tinted header background instead of solid `bg-secondary`.)

Then **TableHead** (line 76) — make column headers eyebrow-style. **Old string**:

```tsx
      "h-12 px-5 text-start align-middle text-xs font-medium text-muted-foreground [&:has([role=checkbox])]:pr-0 [&>[role=checkbox]]:translate-y-[2px]",
```

**New string**:

```tsx
      "h-10 px-5 text-start align-middle text-[10px] font-bold uppercase tracking-[0.12em] text-muted-foreground [&:has([role=checkbox])]:pr-0 [&>[role=checkbox]]:translate-y-[2px]",
```

(Smaller height, uppercase tracked label. Eyebrow density per spec §7.8.)

Then **TableCell** (line 91) — slightly tighter padding. **Old string**:

```tsx
      "px-5 py-3.5 align-middle text-sm [&:has([role=checkbox])]:pr-0 [&>[role=checkbox]]:translate-y-[2px]",
```

**New string**:

```tsx
      "px-5 py-3 align-middle text-sm [&:has([role=checkbox])]:pr-0 [&>[role=checkbox]]:translate-y-[2px]",
```

(`py-3.5 → py-3` — subtle row tightening.)

- [ ] **Step 2: Create TablesSection**

`boilerplateFE/src/features/styleguide/components/sections/TablesSection.tsx`:

```tsx
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Section } from '../Section';

const ROWS = [
  { name: 'Acme Corporation', plan: 'enterprise', users: 42, status: 'healthy' as const },
  { name: 'Globex Industries', plan: 'pro', users: 31, status: 'healthy' as const },
  { name: 'Initech Systems', plan: 'pro', users: 28, status: 'healthy' as const },
  { name: 'Hooli AI', plan: 'enterprise', users: 87, status: 'pending' as const },
  { name: 'Pied Piper', plan: 'starter', users: 9, status: 'healthy' as const },
];

export function TablesSection() {
  return (
    <Section
      id="tables"
      eyebrow="Tables"
      title="Glass surface, eyebrow headers"
      deck="Container is .surface-glass. Header row has a subtle copper tint and uppercase 10px tracked labels. Row padding is slightly denser than the previous default."
    >
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Plan</TableHead>
            <TableHead>Users</TableHead>
            <TableHead>Status</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {ROWS.map((r) => (
            <TableRow key={r.name}>
              <TableCell className="font-medium">{r.name}</TableCell>
              <TableCell className="font-mono text-xs text-muted-foreground">{r.plan}</TableCell>
              <TableCell className="font-mono text-xs">{r.users}</TableCell>
              <TableCell>
                <Badge variant={r.status === 'pending' ? 'pending' : 'healthy'}>
                  {r.status === 'pending' ? 'Trial' : 'Active'}
                </Badge>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Section>
  );
}
```

- [ ] **Step 3: Slot into StyleguidePage**

Add import + render `<TablesSection />` after `<BadgesSection />`.

- [ ] **Step 4: Copy + verify + commit**

```bash
cp_fe src/components/ui/table.tsx \
      src/features/styleguide/pages/StyleguidePage.tsx \
      src/features/styleguide/components/sections/TablesSection.tsx
```

Refresh `/styleguide`. Tables section should show a 5-row tenant table with glass surface, copper-tinted header row with uppercase labels, and J4 badge pills (healthy + pending) in the status column.

Now check existing pages — open `/users`, `/tenants`, or any list page. Tables should render with the new glass surface and tighter headers. **This is a sweeping cross-app effect** since every list page uses this primitive.

```bash
cd boilerplateFE && npx tsc --noEmit && npm run build 2>&1 | tail -5 && cd ..
git add boilerplateFE/src/components/ui/table.tsx \
        boilerplateFE/src/features/styleguide/
git commit -m "feat(theme): restyle Table — glass surface, copper-tinted header, eyebrow column labels"
```

---

## Phase 2C — Verification

### Task 9: End-to-end verification

**Files:** none modified — verification only.

- [ ] **Step 1: Source production build + lint**

```bash
cd boilerplateFE && npm run build 2>&1 | tail -8 && npm run lint 2>&1 | tail -5 && cd ..
```
Expected: build succeeds, lint clean.

- [ ] **Step 2: Test app build**

```bash
cd _testJ4visual/_testJ4visual-FE && npm run build 2>&1 | tail -5 && cd ../..
```
Expected: build succeeds.

- [ ] **Step 3: Confirm `/styleguide` is dev-only**

```bash
grep -lr "StyleguidePage" boilerplateFE/dist/ 2>/dev/null | head -3
```
Expected: empty (production tree-shake removes it).

- [ ] **Step 4: Visual smoke test on test app**

Open in browser (use whatever browser MCP is available, or have the user verify):

a) `http://localhost:3100/styleguide` — verify all 7 sections render correctly in light mode.
b) Toggle dark mode (theme toggle in app header) — verify all sections still look right; aurora intensifies; gradient text shifts to lighter stops.
c) Toggle to RTL via the language switcher (if available) — verify the styleguide layout doesn't break (sidebar mirrors, labels still readable). If language switch isn't accessible from `/styleguide`, navigate to a logged-in page first, switch language there, then return to `/styleguide` — language persists.
d) Visit `/login` — verify the "Sign in" button uses the J4 gradient + glow.
e) Visit `/users` (or any logged-in list page) — verify the table has the glass surface and tighter header.

- [ ] **Step 5: Confirm test app processes still healthy**

```bash
ps -p $(cat /tmp/_testJ4visual.be.pid 2>/dev/null) -p $(cat /tmp/_testJ4visual.fe.pid 2>/dev/null) -o pid,ppid,comm
curl -s -o /dev/null -w "BE health: %{http_code}\n" http://localhost:5100/health
curl -s -o /dev/null -w "FE root:   %{http_code}\n" http://localhost:3100/
```
Expected: both processes alive (PPID=1), both 200.

- [ ] **Step 6: Recap commits**

```bash
git log --oneline fe/base ^d682cf24 | head -15
```
Expected: 8 commits from this plan (Tasks 1-8) on top of Plan 1's last commit `d682cf24`.

- [ ] **Step 7: Optional push**

```bash
git push origin fe/base
```

---

## What's done after this plan

- `/styleguide` route registered (dev-only). Renders 7 sections: Tokens, Typography, Buttons, Forms, Cards, Badges, Tables.
- 5 shadcn primitives (Button, Input, Textarea, Card, Badge, Table) restyled to use J4 utilities and tokens. Existing call sites work unchanged thanks to backwards-compatible variant defaults.
- The first sweeping J4 visual change is live across the app — every primary button has the gradient halo, every list page table has the glass surface and tighter eyebrow headers, every form input has the glass treatment.

## What's next (plans 2B and 3)

**Plan 2B — Composite primitives + common components.** Dialog, Popover, Dropdown-menu, Select, Tabs, Avatar, Spinner, Sonner toast, plus the 22 common components in `@/components/common/`. Adds Forms-Composite, Dialogs, Dropdowns, Tabs, Avatars, Toasts, EmptyStates, PageHeader, Pagination, Filter, etc. sections to `/styleguide`.

**Plan 3 — Layouts & Landing.** New Header (with restyled functional UI: LanguageSwitcher, ThemeToggle, NotificationBell, UserAvatar dropdown), new Sidebar, AuthLayout/MainLayout/PublicLayout aurora application, and the 8-section landing page composition.

---

## Self-Review checklist

**Spec coverage (this plan only — §7.1, §7.2, §7.3, §7.7, §7.8, §15):**

- §7.1 Button — Tasks 4. `default = btn-primary-gradient + glow-primary-md`, `secondary = surface-glass`. Sizes unchanged from baseline (the spec called for slightly smaller md size — *deferred to a polish pass; default size 10/h-10 stays for backwards compat with all current call sites*).
- §7.2 Input/Textarea — Task 5. Glass surface + backdrop blur applied.
- §7.3 Card — Task 6. Three variants (`solid`, `glass`, `elevated`); `solid` is backwards-compatible default.
- §7.7 Badge — Task 7. 4 J4 status variants added (`healthy`, `pending`, `failed`, `info`); existing 4 variants untouched.
- §7.8 Table — Task 8. Glass container, copper-tinted header, eyebrow column labels (10px uppercase tracked), row padding tightened by 0.5.
- §15 Style Reference page — Tasks 1, 2, 3 cover skeleton + Tokens + Typography. Sections 4-8 cover the primitives shipped in this plan. Sections for shadcn composites (Dialog, Popover, etc.) and common components (PageHeader, Pagination, etc.) defer to Plan 2B.

**Placeholder scan:** No "TBD", "TODO", or "implement later" patterns. Every step has exact code + commands. The single deferred item — button size scale — is explicitly flagged in §7.1 above and called out as a polish pass, not silently skipped.

**Type consistency:** `cardVariants` exported from `card.tsx` (and reused later if needed). `badgeVariants` continues to be exported. `import.meta.env.DEV` referenced in `routes.tsx` is a Vite-provided constant. All new section component files follow the same export pattern (named export). All new files placed in `src/features/styleguide/...` per the file structure table at top.

**Architectural choice (incremental styleguide):** the `/styleguide` page grows section-by-section as primitives ship. This means each task produces a working, viewable result on its own — visible progress every commit, reviewable independently.

---

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-04-27-component-restyle-foundation.md`.

**Two execution options:**

1. **Subagent-Driven (recommended)** — fresh subagent per task with two-stage review. Same workflow that worked well for Plan 1. Per-task browser verification against the running test app catches CSS/CVA mistakes immediately.
2. **Inline Execution** — execute in this session via `superpowers:executing-plans`.

**Which approach?**
