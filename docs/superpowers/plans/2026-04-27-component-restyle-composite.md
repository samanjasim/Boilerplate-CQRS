# Visual Foundation — Phase 2B (Composite + Functional UI)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Tasks use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle composite shadcn primitives (Dialog, Popover, DropdownMenu, Avatar, Spinner, Sonner, Separator), the most-used common components (PageHeader, Pagination, EmptyState), and the four functional UI elements the user explicitly flagged earlier (LanguageSwitcher, ThemeToggle, NotificationBell, UserAvatar). After this plan ships, **every header in the app reflects J4** — including the user-facing controls visible on every authenticated page.

**Architecture:** Same pattern as Plan 2 — each task restyles a primitive (or related cluster) and adds its showcase section to `/styleguide`. Test app gets per-task file copies for live HMR verification. Branch stays `fe/base`.

**Tech Stack:** React 19, Tailwind 4, TypeScript, class-variance-authority, shadcn/ui, Radix UI primitives, lucide-react.

**Spec reference:** [docs/superpowers/specs/2026-04-26-phase-0-visual-foundation-design.md](../specs/2026-04-26-phase-0-visual-foundation-design.md) §7.4–7.13 (composite primitives), §8 (common components — LanguageSwitcher⚡, ThemeToggle⚡, NotificationBell⚡, UserAvatar⚡, PageHeader, Pagination, EmptyState).

**Plan position:** Plan **2B of 3** in Phase 0. Plan 1 (Tokens) and Plan 2 (Foundation primitives) shipped on `fe/base`. Plan 3 (Layouts + Landing) follows after this.

**Test app:** `_testJ4visual` running. BE 5100, FE 3100. Login `superadmin@testj4visual.com` / `Admin@123456`.

---

## File Structure

| File | Status | Responsibility |
|---|---|---|
| `boilerplateFE/src/components/ui/dialog.tsx` | Modify | Glass-strong content, tighter overlay |
| `boilerplateFE/src/components/ui/popover.tsx` | Modify | `surface-glass` content, hairline border |
| `boilerplateFE/src/components/ui/dropdown-menu.tsx` | Modify | `surface-glass` content, copper-tinted item focus |
| `boilerplateFE/src/components/ui/avatar.tsx` | Modify | Square-radius, copper gradient bg for fallback |
| `boilerplateFE/src/components/ui/spinner.tsx` | Modify | Copper tint, optional gradient stroke |
| `boilerplateFE/src/components/ui/sonner.tsx` | Modify | Glass card style with left accent strip |
| `boilerplateFE/src/components/ui/separator.tsx` | (Audit) | Likely no change needed; verify |
| `boilerplateFE/src/components/common/PageHeader.tsx` | Modify | J4 typography (Inter font-display, larger size) |
| `boilerplateFE/src/components/common/Pagination.tsx` | Modify | Active page = primary gradient; tighter buttons |
| `boilerplateFE/src/components/common/EmptyState.tsx` | Modify | Glass icon tile with copper glow |
| `boilerplateFE/src/components/common/ThemeToggle.tsx` | Modify | Smaller button size + ring on active mode |
| `boilerplateFE/src/components/common/LanguageSwitcher.tsx` | Modify | Glass dropdown surface, copper tint on active |
| `boilerplateFE/src/components/common/NotificationBell.tsx` | Modify | Glow on unread count badge, glass dropdown |
| `boilerplateFE/src/components/common/UserAvatar.tsx` | Modify | Match Avatar primitive restyle (copper gradient) |
| `boilerplateFE/src/features/styleguide/components/sections/DialogsSection.tsx` | Create | Live demo of Dialog + variants |
| `boilerplateFE/src/features/styleguide/components/sections/DropdownsSection.tsx` | Create | Popover + DropdownMenu demos |
| `boilerplateFE/src/features/styleguide/components/sections/AvatarsSection.tsx` | Create | Avatar + spinner + toast demos |
| `boilerplateFE/src/features/styleguide/components/sections/CommonSection.tsx` | Create | PageHeader + Pagination + EmptyState demos |
| `boilerplateFE/src/features/styleguide/components/sections/FunctionalSection.tsx` | Create | The four flagged controls live |
| `boilerplateFE/src/features/styleguide/pages/StyleguidePage.tsx` | Modify | Slot in 5 new sections + extend nav array |

---

## Pre-flight

### Task 0: Confirm test app still healthy

(Inline — controller verifies, no subagent.)

```bash
ps -p $(cat /tmp/_testJ4visual.be.pid 2>/dev/null) -p $(cat /tmp/_testJ4visual.fe.pid 2>/dev/null) -o pid,ppid,comm
curl -s -o /dev/null -w "BE %{http_code}  FE %{http_code}\n" http://localhost:5100/health http://localhost:3100/
```
Expected: both PPID=1, both 200.

---

## Phase 2B-A — Composite primitives

### Task 1: Restyle Dialog + Dialogs section

**Files:**
- Modify: `boilerplateFE/src/components/ui/dialog.tsx`
- Create: `boilerplateFE/src/features/styleguide/components/sections/DialogsSection.tsx`
- Modify: `boilerplateFE/src/features/styleguide/pages/StyleguidePage.tsx` (add nav entry + slot)

- [ ] **Step 1: Restyle DialogContent — use surface-glass-strong**

In `boilerplateFE/src/components/ui/dialog.tsx`, find the DialogContent block. The current className for DialogContent is on line 38-39. Use Edit.

**old_string:**
```
"fixed left-[50%] top-[50%] z-50 grid w-full max-w-lg translate-x-[-50%] translate-y-[-50%] gap-4 border-0 bg-card p-6 shadow-float duration-200 rounded-2xl data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[state=closed]:slide-out-to-left-1/2 data-[state=closed]:slide-out-to-top-[48%] data-[state=open]:slide-in-from-left-1/2 data-[state=open]:slide-in-from-top-[48%]",
```

**new_string:**
```
"fixed left-[50%] top-[50%] z-50 grid w-full max-w-lg translate-x-[-50%] translate-y-[-50%] gap-4 surface-glass-strong p-6 shadow-float duration-200 rounded-2xl data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[state=closed]:slide-out-to-left-1/2 data-[state=closed]:slide-out-to-top-[48%] data-[state=open]:slide-in-from-left-1/2 data-[state=open]:slide-in-from-top-[48%]",
```

(Replaced `border-0 bg-card` with `surface-glass-strong`. The surface-glass-strong utility includes its own border via `--border-strong`.)

- [ ] **Step 2: Create DialogsSection**

`boilerplateFE/src/features/styleguide/components/sections/DialogsSection.tsx`:

```tsx
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Section } from '../Section';

export function DialogsSection() {
  const [open, setOpen] = useState(false);
  return (
    <Section
      id="dialogs"
      eyebrow="Dialogs"
      title="Modal surface"
      deck="Glass-strong content over a blurred dark overlay. Title + description follow the type ramp; footer holds primary + ghost actions."
    >
      <div className="flex flex-wrap gap-3">
        <Dialog>
          <DialogTrigger asChild>
            <Button>Open dialog</Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Confirm action</DialogTitle>
              <DialogDescription>
                Glass-strong content surface picks up the page aurora behind it. Backdrop is dark + blurred so this card always reads.
              </DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <Button variant="ghost">Cancel</Button>
              <Button>Confirm</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild>
            <Button variant="outline">Destructive variant</Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Delete this tenant?</DialogTitle>
              <DialogDescription>
                This permanently removes the tenant and all associated data. Audit logs retain a record.
              </DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <Button variant="ghost" onClick={() => setOpen(false)}>Cancel</Button>
              <Button variant="destructive" onClick={() => setOpen(false)}>Delete</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>
    </Section>
  );
}
```

- [ ] **Step 3: Update StyleguidePage**

In `boilerplateFE/src/features/styleguide/pages/StyleguidePage.tsx`:

(a) Add import below existing TablesSection import:
```tsx
import { DialogsSection } from '../components/sections/DialogsSection';
```

(b) Find the `SECTIONS` array. Add a new entry after `tables`:
```ts
  { id: 'dialogs', label: 'Dialogs' },
```

(c) Add `<DialogsSection />` immediately after `<TablesSection />` in the JSX.

- [ ] **Step 4: Copy + verify + commit**

```bash
cp_fe() {
  for f in "$@"; do
    src="boilerplateFE/$f"; dst="_testJ4visual/_testJ4visual-FE/$f"
    mkdir -p "$(dirname "$dst")"; cp "$src" "$dst" && echo "→ $dst"
  done
}

cp_fe src/components/ui/dialog.tsx \
      src/features/styleguide/pages/StyleguidePage.tsx \
      src/features/styleguide/components/sections/DialogsSection.tsx

cd boilerplateFE && npx tsc --noEmit && cd ..
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3100/styleguide

git add boilerplateFE/src/components/ui/dialog.tsx \
        boilerplateFE/src/features/styleguide/
git commit -m "feat(theme): restyle Dialog content with surface-glass-strong"
```

### Task 2: Restyle Popover + DropdownMenu + Dropdowns section

**Files:**
- Modify: `boilerplateFE/src/components/ui/popover.tsx`
- Modify: `boilerplateFE/src/components/ui/dropdown-menu.tsx`
- Create: `boilerplateFE/src/features/styleguide/components/sections/DropdownsSection.tsx`
- Modify: `boilerplateFE/src/features/styleguide/pages/StyleguidePage.tsx`

- [ ] **Step 1: Restyle PopoverContent**

In `boilerplateFE/src/components/ui/popover.tsx` (line 22), use Edit.

**old_string:**
```
"z-50 w-72 rounded-xl border border-border/30 bg-popover p-4 text-popover-foreground shadow-float outline-none data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2",
```

**new_string:**
```
"z-50 w-72 rounded-xl surface-glass p-4 text-popover-foreground shadow-float outline-none data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2",
```

(Replaced `border border-border/30 bg-popover` with `surface-glass` — utility includes border via `--border-strong`.)

- [ ] **Step 2: Restyle DropdownMenuContent**

In `boilerplateFE/src/components/ui/dropdown-menu.tsx` (line 66), use Edit.

**old_string:**
```
"z-50 min-w-[8rem] overflow-hidden rounded-xl border border-border/30 bg-popover p-1.5 text-popover-foreground shadow-float data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2",
```

**new_string:**
```
"z-50 min-w-[8rem] overflow-hidden rounded-xl surface-glass p-1.5 text-popover-foreground shadow-float data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2",
```

DropdownMenuItem already uses `focus:bg-primary/[0.06]` for copper-tinted hover — leave that as-is, it's correct.

- [ ] **Step 3: Create DropdownsSection**

`boilerplateFE/src/features/styleguide/components/sections/DropdownsSection.tsx`:

```tsx
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Section } from '../Section';

export function DropdownsSection() {
  return (
    <Section
      id="dropdowns"
      eyebrow="Dropdowns & popovers"
      title="Glass content surfaces"
      deck="Both Popover and DropdownMenu use .surface-glass with a backdrop blur. DropdownMenu items have a copper-tinted focus background (primary/0.06)."
    >
      <div className="flex flex-wrap gap-3">
        <Popover>
          <PopoverTrigger asChild>
            <Button variant="outline">Open popover</Button>
          </PopoverTrigger>
          <PopoverContent>
            <div className="space-y-2">
              <h4 className="font-semibold text-sm">Popover content</h4>
              <p className="text-xs text-muted-foreground">
                Translucent surface picks up aurora behind it. Use for forms, inline help, contextual filters.
              </p>
            </div>
          </PopoverContent>
        </Popover>

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="outline">Open menu</Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="start" className="w-48">
            <DropdownMenuLabel>Workspace</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem>Profile</DropdownMenuItem>
            <DropdownMenuItem>Sessions</DropdownMenuItem>
            <DropdownMenuItem>Settings</DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem className="text-destructive focus:text-destructive">
              Sign out
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </Section>
  );
}
```

- [ ] **Step 4: StyleguidePage update**

(a) Import: `import { DropdownsSection } from '../components/sections/DropdownsSection';`
(b) SECTIONS array — add `{ id: 'dropdowns', label: 'Dropdowns' }` after `dialogs`.
(c) JSX — render `<DropdownsSection />` after `<DialogsSection />`.

- [ ] **Step 5: Copy + verify + commit**

```bash
cp_fe src/components/ui/popover.tsx \
      src/components/ui/dropdown-menu.tsx \
      src/features/styleguide/pages/StyleguidePage.tsx \
      src/features/styleguide/components/sections/DropdownsSection.tsx

cd boilerplateFE && npx tsc --noEmit && cd ..
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3100/styleguide

git add boilerplateFE/src/components/ui/popover.tsx \
        boilerplateFE/src/components/ui/dropdown-menu.tsx \
        boilerplateFE/src/features/styleguide/
git commit -m "feat(theme): restyle Popover and DropdownMenu content with surface-glass"
```

### Task 3: Restyle Avatar + Spinner + Sonner; verify Separator

**Files:**
- Modify: `boilerplateFE/src/components/ui/avatar.tsx`
- Modify: `boilerplateFE/src/components/ui/spinner.tsx`
- Modify: `boilerplateFE/src/components/ui/sonner.tsx`
- Audit: `boilerplateFE/src/components/ui/separator.tsx` (likely no change)
- Create: `boilerplateFE/src/features/styleguide/components/sections/AvatarsSection.tsx`
- Modify: `boilerplateFE/src/features/styleguide/pages/StyleguidePage.tsx`

- [ ] **Step 1: Read each of the 4 files first** so the diffs you write are anchored to the actual current text.

```bash
cat boilerplateFE/src/components/ui/avatar.tsx
cat boilerplateFE/src/components/ui/spinner.tsx
cat boilerplateFE/src/components/ui/sonner.tsx
cat boilerplateFE/src/components/ui/separator.tsx
```

- [ ] **Step 2: Avatar — square radius (matches J4 logo mark) + copper gradient fallback**

The Avatar component is built on Radix Avatar primitive. Two key className changes:
- `AvatarFallback`: replace `bg-muted` with `btn-primary-gradient text-primary-foreground` so initials render with the copper gradient.
- `Avatar` root: change from `rounded-full` to `rounded-lg` (square-ish, matches the brand mark style in §7).

Use grep to locate the className strings, then Edit each. If shadcn/ui's avatar has the typical structure, the fallback className contains `bg-muted` and the root contains `rounded-full`.

Apply the smallest change that hits the spec. If your implementation differs structurally, match the spec intent (square radius + copper gradient fallback) and document any deviation in your report.

- [ ] **Step 3: Spinner — copper tint**

The spinner color comes from a `border-current` or `text-primary` reference. If it currently uses a neutral color, swap it to `text-primary` (preset-driven). Make sure the spinner remains a 2px stroke, conic-gradient or simple border-spin per current implementation.

- [ ] **Step 4: Sonner — glass card with accent strip**

Sonner is the toast component from the `sonner` package, wrapped by shadcn. The wrapper typically configures `toastOptions.classNames`. Update the toast root class to include `surface-glass shadow-float` and add a 3px left accent border that signals variant (success=accent-500, error=destructive, info=violet-500, warning=amber-500). If Sonner exposes a `classNames` configuration object, set the variants there. If your wrapper is a simple `<Toaster />` re-export, replace the export with a configured one. Be careful not to change the toast API consumed by call sites.

- [ ] **Step 5: Separator — verify no change needed**

If the current Separator is a simple 1px hairline at `--border`, leave it alone. If it has any `bg-primary` or `bg-secondary` reference inconsistent with §7.13 ("1px hairline at --border"), update.

- [ ] **Step 6: Create AvatarsSection**

`boilerplateFE/src/features/styleguide/components/sections/AvatarsSection.tsx`:

```tsx
import { toast } from 'sonner';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { Section } from '../Section';

export function AvatarsSection() {
  return (
    <Section
      id="avatars"
      eyebrow="Avatars · Spinners · Toasts · Separator"
      title="Small primitives"
      deck="Avatar fallback uses the copper gradient and square-ish radius matching the brand mark. Toast surfaces are glass with a left accent strip per variant."
    >
      <div className="flex flex-wrap items-center gap-4">
        <Avatar><AvatarFallback>SJ</AvatarFallback></Avatar>
        <Avatar><AvatarFallback>AB</AvatarFallback></Avatar>
        <Avatar>
          <AvatarImage src="https://i.pravatar.cc/40" alt="" />
          <AvatarFallback>—</AvatarFallback>
        </Avatar>
        <Separator orientation="vertical" className="h-8" />
        <div className="flex items-center gap-2">
          <span className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-current border-r-transparent text-primary" />
          <span className="text-sm text-muted-foreground">spinning…</span>
        </div>
      </div>
      <Separator />
      <div className="flex flex-wrap gap-2">
        <Button variant="outline" onClick={() => toast.success('Saved successfully')}>toast.success</Button>
        <Button variant="outline" onClick={() => toast.error('Something went wrong')}>toast.error</Button>
        <Button variant="outline" onClick={() => toast.info('FYI — long action queued')}>toast.info</Button>
        <Button variant="outline" onClick={() => toast.warning('Approaching quota')}>toast.warning</Button>
      </div>
    </Section>
  );
}
```

- [ ] **Step 7: StyleguidePage update**

(a) Import.
(b) SECTIONS array — `{ id: 'avatars', label: 'Avatars + small' }` after `dropdowns`.
(c) JSX — render after `<DropdownsSection />`.

- [ ] **Step 8: Copy + verify + commit**

```bash
cp_fe src/components/ui/avatar.tsx \
      src/components/ui/spinner.tsx \
      src/components/ui/sonner.tsx \
      src/components/ui/separator.tsx \
      src/features/styleguide/pages/StyleguidePage.tsx \
      src/features/styleguide/components/sections/AvatarsSection.tsx

cd boilerplateFE && npx tsc --noEmit && cd ..
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3100/styleguide

git add boilerplateFE/src/components/ui/avatar.tsx \
        boilerplateFE/src/components/ui/spinner.tsx \
        boilerplateFE/src/components/ui/sonner.tsx \
        boilerplateFE/src/components/ui/separator.tsx \
        boilerplateFE/src/features/styleguide/
git commit -m "feat(theme): restyle Avatar (copper gradient), Spinner (primary), Sonner (glass + accent strip)"
```

---

## Phase 2B-B — Common components

### Task 4: Restyle PageHeader + Pagination + EmptyState; common section

**Files:**
- Modify: `boilerplateFE/src/components/common/PageHeader.tsx`
- Modify: `boilerplateFE/src/components/common/Pagination.tsx`
- Modify: `boilerplateFE/src/components/common/EmptyState.tsx`
- Create: `boilerplateFE/src/features/styleguide/components/sections/CommonSection.tsx`
- Modify: `boilerplateFE/src/features/styleguide/pages/StyleguidePage.tsx`

- [ ] **Step 1: Restyle PageHeader**

In `boilerplateFE/src/components/common/PageHeader.tsx`:

**old_string:**
```tsx
        {title && <h1 className="text-2xl font-bold tracking-tight [color:var(--active-text)]">{title}</h1>}
        {subtitle && <p className="text-sm text-muted-foreground mt-0.5">{subtitle}</p>}
```

**new_string:**
```tsx
        {title && <h1 className="text-[26px] font-light tracking-[-0.025em] leading-[1.15] font-display text-foreground">{title}</h1>}
        {subtitle && <p className="text-sm text-muted-foreground mt-1">{subtitle}</p>}
```

Why: J4 type ramp's "Section title" (Inter 26px / 300 / -0.025em) per spec §4. Color shifts from primary-tinted to plain foreground — the gradient moments live elsewhere (hero, eyebrow), not on every page header.

- [ ] **Step 2: Restyle Pagination**

In `boilerplateFE/src/components/common/Pagination.tsx`, find the active-page button styling around line 91-95.

**old_string:**
```tsx
              className={cn(
                'flex h-8 w-8 items-center justify-center rounded-lg text-sm font-medium transition-colors',
                page === pageNumber
                  ? 'bg-primary text-primary-foreground'
                  : 'text-muted-foreground hover:bg-secondary'
              )}
```

**new_string:**
```tsx
              className={cn(
                'flex h-8 w-8 items-center justify-center rounded-lg text-sm font-medium transition-all',
                page === pageNumber
                  ? 'btn-primary-gradient glow-primary-sm text-primary-foreground'
                  : 'text-muted-foreground hover:bg-secondary'
              )}
```

Active page now uses J4 gradient + small glow halo.

- [ ] **Step 3: Restyle EmptyState**

In `boilerplateFE/src/components/common/EmptyState.tsx`, find the icon tile around line 19-23.

**old_string:**
```tsx
      {Icon && (
        <div className="mb-6 flex h-20 w-20 items-center justify-center rounded-3xl bg-secondary">
          <Icon className="h-9 w-9 text-muted-foreground/60" />
        </div>
      )}
```

**new_string:**
```tsx
      {Icon && (
        <div className="mb-6 flex h-20 w-20 items-center justify-center rounded-3xl surface-glass glow-primary-sm">
          <Icon className="h-9 w-9 text-primary" />
        </div>
      )}
```

Why: glass tile with copper glow + copper-tinted icon — the empty state becomes a gentle moment of brand presence rather than a muted "nothing here."

- [ ] **Step 4: Create CommonSection**

`boilerplateFE/src/features/styleguide/components/sections/CommonSection.tsx`:

```tsx
import { Inbox } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/common/EmptyState';
import { PageHeader } from '@/components/common/PageHeader';
import { Pagination } from '@/components/common/Pagination';
import { Section } from '../Section';

export function CommonSection() {
  const [page, setPage] = useState(3);
  return (
    <Section
      id="common"
      eyebrow="Common components"
      title="PageHeader · Pagination · EmptyState"
      deck="The core list-page chrome. PageHeader uses J4 section-title typography. Pagination's active page renders with the gradient + glow primary. EmptyState becomes a glass tile with copper accent."
    >
      <div className="surface-glass rounded-2xl p-6">
        <PageHeader
          title="Tenants"
          subtitle="142 active · 8 added this month"
          actions={<Button>+ New tenant</Button>}
        />
      </div>

      <div className="surface-glass rounded-2xl p-6">
        <Pagination
          pagination={{
            pageNumber: page,
            pageSize: 20,
            totalPages: 8,
            totalCount: 156,
            hasNextPage: page < 8,
            hasPreviousPage: page > 1,
          }}
          onPageChange={setPage}
          onPageSizeChange={() => {}}
        />
      </div>

      <div className="surface-glass rounded-2xl">
        <EmptyState
          icon={Inbox}
          title="No notifications yet"
          description="When something happens that needs your attention, it'll show up here."
          action={{ label: 'Configure preferences', onClick: () => {} }}
        />
      </div>
    </Section>
  );
}
```

- [ ] **Step 5: StyleguidePage update**

(a) Import.
(b) `{ id: 'common', label: 'Common' }` after `avatars`.
(c) Render after `<AvatarsSection />`.

- [ ] **Step 6: Copy + verify + commit**

```bash
cp_fe src/components/common/PageHeader.tsx \
      src/components/common/Pagination.tsx \
      src/components/common/EmptyState.tsx \
      src/features/styleguide/pages/StyleguidePage.tsx \
      src/features/styleguide/components/sections/CommonSection.tsx

cd boilerplateFE && npx tsc --noEmit && cd ..
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3100/styleguide
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3100/users

git add boilerplateFE/src/components/common/PageHeader.tsx \
        boilerplateFE/src/components/common/Pagination.tsx \
        boilerplateFE/src/components/common/EmptyState.tsx \
        boilerplateFE/src/features/styleguide/
git commit -m "feat(theme): restyle PageHeader (J4 typography), Pagination (gradient active), EmptyState (glass tile)"
```

---

## Phase 2B-C — Functional UI

### Task 5: Restyle ThemeToggle + LanguageSwitcher

**Files:**
- Modify: `boilerplateFE/src/components/common/ThemeToggle.tsx`
- Modify: `boilerplateFE/src/components/common/LanguageSwitcher.tsx`

These two are the "header right cluster" controls (along with NotificationBell + UserAvatar in Task 6). They need to feel cohesive — same button size, same hover treatment, same dropdown-or-popover behavior.

The ThemeToggle and LanguageSwitcher already use the `<Button variant="ghost">` (which auto-restyled in Plan 2). They render correctly already. The remaining work is about the **dropdown surfaces** and the **active-state treatment** for the language list.

- [ ] **Step 1: Read both files**

```bash
cat boilerplateFE/src/components/common/ThemeToggle.tsx
cat boilerplateFE/src/components/common/LanguageSwitcher.tsx
```

(You should already have these read into context from prior tasks — re-read if uncertain.)

- [ ] **Step 2: ThemeToggle — confirm no change needed**

The ThemeToggle is a single button that toggles the theme. With Plan 2's Button restyle the `variant="ghost"` button already uses the J4 ghost styling. **No change needed.** Note this in your report.

- [ ] **Step 3: LanguageSwitcher — restyle the inline dropdown**

The LanguageSwitcher in this codebase uses an **inline `<div>` dropdown** (not Radix Popover). It has its own hand-rolled surface. Find the `<div>` with class `absolute end-0 top-full mt-1.5 z-50 w-36 rounded-xl border border-border/30 bg-popover p-1.5 shadow-float` (text variant) and the analogous one for the ghost variant.

**Edit 1 — text variant dropdown (around line 40):**

old_string:
```
          <div className="absolute end-0 top-full mt-1.5 z-50 w-36 rounded-xl border border-border/30 bg-popover p-1.5 shadow-float">
```

new_string:
```
          <div className="absolute end-0 top-full mt-1.5 z-50 w-36 rounded-xl surface-glass p-1.5 shadow-float">
```

**Edit 2 — ghost variant dropdown (around line 68):**

old_string:
```
        <div className="absolute ltr:right-0 rtl:left-0 mt-1.5 w-36 rounded-xl border border-border/30 bg-popover p-1.5 shadow-float z-50">
```

new_string:
```
        <div className="absolute ltr:right-0 rtl:left-0 mt-1.5 w-36 rounded-xl surface-glass p-1.5 shadow-float z-50">
```

Active-language item already uses `bg-primary/10 text-primary` which is fine — that's the existing semantic active state. Leave it.

- [ ] **Step 4: Copy + verify + commit**

```bash
cp_fe src/components/common/ThemeToggle.tsx \
      src/components/common/LanguageSwitcher.tsx

cd boilerplateFE && npx tsc --noEmit && cd ..
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3100/

git add boilerplateFE/src/components/common/ThemeToggle.tsx \
        boilerplateFE/src/components/common/LanguageSwitcher.tsx
git commit -m "feat(theme): restyle LanguageSwitcher dropdown with surface-glass; ThemeToggle inherits Button"
```

(If ThemeToggle truly needed no change, you can omit it from the `git add` and the commit. Adjust the message in that case.)

### Task 6: Restyle NotificationBell + UserAvatar; FunctionalSection

**Files:**
- Modify: `boilerplateFE/src/components/common/NotificationBell.tsx`
- Modify: `boilerplateFE/src/components/common/UserAvatar.tsx`
- Create: `boilerplateFE/src/features/styleguide/components/sections/FunctionalSection.tsx`
- Modify: `boilerplateFE/src/features/styleguide/pages/StyleguidePage.tsx`

- [ ] **Step 1: Read both files**

```bash
cat boilerplateFE/src/components/common/NotificationBell.tsx
cat boilerplateFE/src/components/common/UserAvatar.tsx
```

- [ ] **Step 2: NotificationBell**

Two changes per spec §8 ("NotificationBell ⚡"):
1. The unread-count pill should use `btn-primary-gradient` background with mono numerics — find the count badge and apply the gradient.
2. The dropdown surface (popover or inline) should use `surface-glass`.

If the count badge currently uses `bg-primary text-primary-foreground` or similar, swap to `btn-primary-gradient text-primary-foreground` and add `font-mono text-[10px]` if not present.

If the dropdown uses Radix Popover (already restyled in Task 2 — inherits `surface-glass`), no change needed. If it's a hand-rolled `<div>` with `border bg-popover`, swap to `surface-glass` like LanguageSwitcher.

- [ ] **Step 3: UserAvatar**

If `UserAvatar` simply wraps the shadcn Avatar primitive with `<AvatarFallback>`, the J4 gradient fallback from Task 3 already takes effect. **No change needed** for the avatar visual — but check whether UserAvatar is a "user dropdown" or just an avatar display.

If it's a dropdown (with profile/sessions/sign-out menu), make sure the `<DropdownMenuContent>` it uses is the restyled one from Task 2 (it should be — the import path is the same primitive).

If UserAvatar has any custom hover/active state, ensure it uses `--active-bg` semantic tokens, not hardcoded primary tints.

Document in your report what (if anything) you changed in UserAvatar.

- [ ] **Step 4: Create FunctionalSection**

`boilerplateFE/src/features/styleguide/components/sections/FunctionalSection.tsx`:

```tsx
import { LanguageSwitcher } from '@/components/common/LanguageSwitcher';
import { NotificationBell } from '@/components/common/NotificationBell';
import { ThemeToggle } from '@/components/common/ThemeToggle';
import { UserAvatar } from '@/components/common/UserAvatar';
import { Section } from '../Section';

export function FunctionalSection() {
  return (
    <Section
      id="functional"
      eyebrow="Functional UI"
      title="Header right cluster"
      deck="The four user-facing controls present on every authenticated page: notifications, language, theme, and user avatar dropdown. All ride the J4 surface-glass + primary-tinted active state."
    >
      <div className="surface-glass rounded-2xl p-4">
        <div className="flex items-center gap-2 justify-end">
          <NotificationBell />
          <LanguageSwitcher />
          <ThemeToggle />
          <UserAvatar />
        </div>
      </div>
      <p className="text-xs text-muted-foreground mt-2">
        Click each to open its menu. The dropdown surfaces use <code>.surface-glass</code> with backdrop blur.
      </p>
    </Section>
  );
}
```

(If `UserAvatar` requires props that aren't defaulted — e.g., a user object — check its signature and pass minimal mock data. Keep the styleguide demo functional but lightweight.)

- [ ] **Step 5: StyleguidePage update**

(a) Import.
(b) `{ id: 'functional', label: 'Functional UI' }` after `common`.
(c) Render after `<CommonSection />`.

- [ ] **Step 6: Copy + verify + commit**

```bash
cp_fe src/components/common/NotificationBell.tsx \
      src/components/common/UserAvatar.tsx \
      src/features/styleguide/pages/StyleguidePage.tsx \
      src/features/styleguide/components/sections/FunctionalSection.tsx

cd boilerplateFE && npx tsc --noEmit && cd ..
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3100/styleguide

git add boilerplateFE/src/components/common/NotificationBell.tsx \
        boilerplateFE/src/components/common/UserAvatar.tsx \
        boilerplateFE/src/features/styleguide/
git commit -m "feat(theme): restyle NotificationBell and UserAvatar dropdown with surface-glass"
```

---

## Phase 2B-D — Verification

### Task 7: End-to-end verification

(Inline — controller verifies, no subagent.)

```bash
# Source build + lint
cd boilerplateFE && npm run build 2>&1 | tail -5 && npm run lint 2>&1 | tail -3 && cd ..

# Test app build sanity
cd _testJ4visual/_testJ4visual-FE && npm run build 2>&1 | tail -5 && cd ../..

# Pages still 200
curl -s -o /dev/null -w "/styleguide %{http_code}\n/login %{http_code}\n/users %{http_code}\n/dashboard %{http_code}\n" \
  http://localhost:3100/styleguide http://localhost:3100/login http://localhost:3100/users http://localhost:3100/dashboard

# Plan 2B commit list
git log --oneline fe/base ^048e6df6 | head -10
```
Expected: 6 task commits + 1 plan doc commit on top of Plan 2's last commit.

---

## Self-Review

**Spec coverage (this plan only — §7.4 dialog, §7.5 popover/dropdown, §7.10 tabs out-of-scope, §7.11 avatar, §7.12 spinner/sonner, §7.13 separator, §8 PageHeader/Pagination/EmptyState/LanguageSwitcher⚡/ThemeToggle⚡/NotificationBell⚡/UserAvatar⚡):**

- §7.4 Dialog — Task 1.
- §7.5 Popover / DropdownMenu — Task 2.
- §7.6 Select — DEFERRED (used inside Pagination only at this point; Plan 2C or feature pages will hit it explicitly).
- §7.10 Tabs — DEFERRED to Plan 3 (feature pages).
- §7.11 Avatar — Task 3.
- §7.12 Spinner + Sonner — Task 3.
- §7.13 Separator — Task 3 (audit).
- §8 PageHeader, Pagination, EmptyState — Task 4.
- §8 LanguageSwitcher⚡, ThemeToggle⚡ — Task 5.
- §8 NotificationBell⚡, UserAvatar⚡ — Task 6.

**Common components NOT covered here** (deferred to feature-page passes in Plan 3 or Plan 2C if added):
- ConfirmDialog (inherits from Dialog)
- DateRangePicker (composition)
- ErrorBoundary / RouteErrorBoundary (state pages — Plan 3)
- ExportButton (inherits from Button + Dropdown)
- FileUpload (composition; needs care, save for dedicated task)
- InfoField (typography only)
- ListPageState / ListToolbar (composition)
- LoadingScreen (boot-screen polish — small work, batch with Plan 3)
- VisibilityBadge (inherits from Badge)
- SubjectPicker / SubjectStack (composition)
- OwnershipTransferDialog / ResourceShareDialog (inherit from ConfirmDialog)

**Placeholder scan:** No "TBD" / "implement later" patterns. Tasks 3, 5, 6 use a "read-then-edit" pattern for files whose exact current shape is uncertain — acceptable because the tasks instruct the implementer to read the file first, identify the structure, and apply the spec intent. Each task documents what to do if the structure differs from expectation.

**Type consistency:** All section components are named exports following the existing pattern (`TokensSection`, `ButtonsSection`, etc.). New section files in `boilerplateFE/src/features/styleguide/components/sections/`. Functional UI components (`LanguageSwitcher`, `ThemeToggle`, `NotificationBell`, `UserAvatar`) keep their existing prop signatures — restyle only.

---

## Execution handoff

Plan saved to `docs/superpowers/plans/2026-04-27-component-restyle-composite.md`.

**Two execution options:**
1. **Subagent-Driven (recommended)** — same pattern as Plans 1 and 2.
2. **Inline Execution** — possible but the per-task verification gate matters here, especially for the read-then-edit tasks (Tasks 3, 5, 6).

**Default: subagent-driven, continuing the established cadence.**
