# Visual Foundation — Phase 1 (Tokens & Utilities)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land all design tokens, gradient/glow utility classes, and the Inter font for the J4 Spectrum visual foundation. After this plan ships, **no visual change is yet visible** to end users — components still render in their old style. This phase only adds the *vocabulary* the rest of Phase 0 will speak.

**Architecture:** Two-layer separation:

1. **Color values stay in `theme.config.ts`** as preset properties (per-preset overridable). Violet + amber companion scales added as optional preset fields with global defaults applied by `useThemePreset` at runtime.
2. **J4 composition rules live in `index.css`** as static structure (gradient angles, ellipse positions, blur amounts, opacity ratios) but **reference colors via CSS vars and `color-mix()`** — never hardcoding RGBA literals. Net effect: changing `activePreset` cascades through aurora, spectrum text, glow halos, and gradient buttons automatically.

Work happens **on `fe/base`** (no new branch). Verification happens against a **persistent test app** created once via `post-feature-testing` skill, then iterated by file-copy (no regenerate per change).

**Tech Stack:** Tailwind 4 (CSS-first config via `@theme` block), CSS custom properties + `color-mix()`, TypeScript, Inter (Google Fonts), pwsh + `rename.ps1` for test app generation.

**Spec reference:** [docs/superpowers/specs/2026-04-26-phase-0-visual-foundation-design.md](../specs/2026-04-26-phase-0-visual-foundation-design.md) — sections §3 (color tokens), §4 (typography), §6 (shadow & glow).

**Phase position:** This is **plan 1 of 3** in Phase 0. Plans 2 (Component restyle + Style Reference page) and 3 (Layouts & Landing) depend on this plan landing first.

---

## File Structure

| File | Status | Responsibility |
|---|---|---|
| `boilerplateFE/index.html` | Modify | Add Inter + IBM Plex Mono to existing Google Fonts import |
| `boilerplateFE/src/config/theme.config.ts` | Modify | Add optional `accentVioletScale` + `accentAmberScale` to `ThemePreset` interface; populate them on the `warm-copper` preset |
| `boilerplateFE/src/styles/index.css` | Modify | Add violet/amber/`font-display` to `@theme`; add J4 tokens (aurora, spectrum text, glow, glass) to `:root` and `.dark` using `var()` + `color-mix()`; add J4 utilities (`.aurora-canvas`, `.aurora-grid`, `.gradient-text`, `.surface-glass`, `.surface-glass-strong`, `.glow-primary-{sm,md,lg}`, `.pulse-dot`, `.btn-primary-gradient`) |
| `boilerplateFE/src/hooks/useThemePreset.ts` | Modify | Write violet + amber scale values at runtime with a global fallback; J4 tokens cascade automatically through the new var-based recipes |

No new files in this plan.

---

## Pre-flight

### Task 0: Stand up a persistent test app

**Files:** `_testJ4visual/` directory tree (created by `rename.ps1`).

The test app is the verification harness for **all of Phase 0** (this plan + plans 2 + 3). Generated **once**; per-iteration we copy modified source files into it instead of regenerating.

- [ ] **Step 1: Verify clean working tree on `fe/base`**

```bash
git status
git branch --show-current
```
Expected: working tree clean, branch is `fe/base`. Spec commit `494ad8eb` is on this branch.

- [ ] **Step 2: Confirm Docker services are running**

```bash
docker compose -f boilerplateBE/docker-compose.yml ps --format 'table {{.Name}}\t{{.Status}}'
```
Expected: at minimum `mailpit`, `redis`, `minio`, `rabbitmq` listed as running (`healthy` or `Up`). If not:
```bash
docker compose -f boilerplateBE/docker-compose.yml up -d
```

- [ ] **Step 3: Pick free port pair**

```bash
for p in 5100 5101 5102 5103 5104 5105; do lsof -i :$p >/dev/null 2>&1 || { echo "BE_PORT=$p"; break; }; done
for p in 3100 3101 3102 3103 3104 3105; do lsof -i :$p >/dev/null 2>&1 || { echo "FE_PORT=$p"; break; }; done
```
Expected: prints e.g. `BE_PORT=5100` and `FE_PORT=3100`. Note both numbers — referred to below as `<BE>` and `<FE>`.

- [ ] **Step 4: Drop any stale test DB**

```bash
PGPASSWORD=123456 psql -U postgres -h localhost -c "DROP DATABASE IF EXISTS _testj4visualdb;"
```
Expected: `DROP DATABASE` (or `NOTICE: database does not exist, skipping`).

- [ ] **Step 5: Generate the test app**

```bash
pwsh -File scripts/rename.ps1 -Name "_testJ4visual" -OutputDir "."
```
Expected: creates `_testJ4visual/` containing `_testJ4visual-BE/`, `_testJ4visual-FE/`, `_testJ4visual-Mobile/`. Output ends with a success line.

- [ ] **Step 6: Fix the underscore-prefix traps**

In `_testJ4visual/_testJ4visual-BE/src/_testJ4visual.Api/appsettings.Development.json`, fix two fields:

```bash
# Show the lines we're going to change
grep -n -E '"Email"|"BucketName"' _testJ4visual/_testJ4visual-BE/src/_testJ4visual.Api/appsettings.Development.json
```

Edit the file:
- `SeedSettings.SuperAdmin.Email`: `superadmin@_testj4visual.com` → `superadmin@testj4visual.com`
- `StorageSettings.BucketName`: `_testj4visual-files` → `testj4visual-files`

(Use the Edit tool on `_testJ4visual/_testJ4visual-BE/src/_testJ4visual.Api/appsettings.Development.json`.)

- [ ] **Step 7: Configure ports + CORS**

In `_testJ4visual/_testJ4visual-BE/src/_testJ4visual.Api/Properties/launchSettings.json`: the https profile encodes both ports in a single semicolon-joined `applicationUrl` string (`https://localhost:5001;http://localhost:5000`). Edit the entire string so `5000` becomes `<BE>` and `5001` becomes `<BE>+1`. Do the same for the http profile (single port).

In `_testJ4visual/_testJ4visual-BE/src/_testJ4visual.Api/appsettings.Development.json`:
- `Cors.AllowedOrigins` array — add `"http://localhost:<FE>"`
- `AppSettings.FrontendUrl` — set to `"http://localhost:<FE>"`

In `_testJ4visual/_testJ4visual-FE/vite.config.ts`: change the `port:` value to `<FE>`.

Create `_testJ4visual/_testJ4visual-FE/.env`:
```
VITE_API_BASE_URL=http://localhost:<BE>/api/v1
```

(Replace `<BE>` and `<FE>` with the actual numbers from Step 3.)

- [ ] **Step 8: Generate migrations for all DbContexts**

```bash
cd _testJ4visual/_testJ4visual-BE
dotnet restore

SP=src/_testJ4visual.Api

# Core
dotnet ef migrations add InitialCreate \
  --context ApplicationDbContext \
  --project src/_testJ4visual.Infrastructure \
  --startup-project $SP

# Modules — current branch has 8 modules, all with their own DbContext
for CTX in \
  "ImportExportDbContext:src/modules/_testJ4visual.Module.ImportExport" \
  "BillingDbContext:src/modules/_testJ4visual.Module.Billing" \
  "WebhooksDbContext:src/modules/_testJ4visual.Module.Webhooks" \
  "ProductsDbContext:src/modules/_testJ4visual.Module.Products" \
  "AiDbContext:src/modules/_testJ4visual.Module.AI" \
  "CommunicationDbContext:src/modules/_testJ4visual.Module.Communication" \
  "WorkflowDbContext:src/modules/_testJ4visual.Module.Workflow" \
  "CommentsActivityDbContext:src/modules/_testJ4visual.Module.CommentsActivity"
do
  NAME="${CTX%%:*}"; PROJ="${CTX##*:}"
  dotnet ef migrations add InitialCreate --context "$NAME" --project "$PROJ" --startup-project $SP
done

cd ../..
```

Expected: each `migrations add` reports `Done.` (9 total: core + 8 modules). If a new module has been added in your branch since this plan was written, run `dotnet ef dbcontext list --project src/_testJ4visual.Infrastructure --startup-project src/_testJ4visual.Api` from inside `_testJ4visual-BE/` and append it to the loop. **Do not skip any DbContext** — the BE will fail to start if migrations are missing for any registered context.

- [ ] **Step 9: Build BE + install FE deps**

```bash
cd _testJ4visual/_testJ4visual-BE && dotnet build && cd ../..
cd _testJ4visual/_testJ4visual-FE && npm install && cd ../..
```
Expected: BE build succeeds (0 warnings, 0 errors). `npm install` completes.

- [ ] **Step 10: Start BE in the background — must persist beyond the running shell**

```bash
cd _testJ4visual/_testJ4visual-BE/src/_testJ4visual.Api
nohup dotnet run --launch-profile http > /tmp/_testJ4visual.be.log 2>&1 &
echo $! > /tmp/_testJ4visual.be.pid
cd -
```
`nohup ... > log 2>&1 &` is enough on its own — the process inherits PPID=1 from `nohup`. (zsh emits a `disown: job not found` warning if you also `disown` after — harmless but noisy. The end state is the same.)

Wait for healthy:
```bash
for i in $(seq 1 30); do
  curl -s http://localhost:<BE>/health >/dev/null 2>&1 && { echo "BE healthy"; break; }
  sleep 2
done
```
Expected: `BE healthy` within ~60s. If not, `tail -100 /tmp/_testJ4visual.be.log` to diagnose.

- [ ] **Step 11: Start FE in the background — must persist beyond the running shell**

```bash
cd _testJ4visual/_testJ4visual-FE
nohup npm run dev > /tmp/_testJ4visual.fe.log 2>&1 &
echo $! > /tmp/_testJ4visual.fe.pid
cd -
sleep 5
curl -s http://localhost:<FE>/ | grep -q '<div id="root"' && echo "FE up" || { echo "FE NOT UP"; tail -50 /tmp/_testJ4visual.fe.log; }
```
Expected: `FE up`. If not, the log tail prints the failure reason.

Confirm both processes have detached cleanly:
```bash
ps -p $(cat /tmp/_testJ4visual.be.pid) -p $(cat /tmp/_testJ4visual.fe.pid) -o pid,ppid,comm
```
Both processes should report `PPID 1`.

- [ ] **Step 12: Smoke-test login**

Open `http://localhost:<FE>` in browser. Login with:
- Email: `superadmin@testj4visual.com`
- Password: `Admin@123456`

Expected: dashboard loads with current warm-copper theme. This is the visual baseline — every later task verifies that the baseline render is **unchanged** (this plan is purely additive).

- [ ] **Step 13: Define a `cp-fe` helper for fast iteration**

We'll be copy-pasting source FE files to the test app many times. Define a shell function:

```bash
# Run from repo root
cp_fe() {
  for f in "$@"; do
    src="boilerplateFE/$f"
    dst="_testJ4visual/_testJ4visual-FE/$f"
    cp "$src" "$dst" && echo "→ $dst"
  done
}
```

Add it to your shell session (paste into terminal). All later "copy to test app" steps use `cp_fe <relative-path>`.

This function relies on the fact that the source files in this plan (`index.html`, `src/styles/index.css`, `src/config/theme.config.ts`, `src/hooks/useThemePreset.ts`) have **no `Starter`/`starter` strings** that would have been renamed in the test app — direct copy works without substitution.

---

## Phase 1A — Inter font

### Task 1: Add Inter and IBM Plex Mono to the Google Fonts import

**Files:**
- Modify: `boilerplateFE/index.html` (line 9 — the existing `<link rel="stylesheet">` for Google Fonts)

- [ ] **Step 1: Replace the font link**

Open `boilerplateFE/index.html`. Replace this line:

```html
    <link href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:ital,wght@0,400;0,500;0,600;0,700;1,400&family=IBM+Plex+Sans+Arabic:wght@400;500;600;700&display=swap" rel="stylesheet" />
```

With:

```html
    <link href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:ital,wght@0,400;0,500;0,600;0,700;1,400&family=IBM+Plex+Sans+Arabic:wght@400;500;600;700&family=IBM+Plex+Mono:wght@400;500&family=Inter:wght@200;300;500;600&display=swap" rel="stylesheet" />
```

This adds Inter (200/300/500/600, ~60KB) and IBM Plex Mono (400/500, ~25KB).

- [ ] **Step 2: Copy to test app**

```bash
cp_fe index.html
```
Expected: `→ _testJ4visual/_testJ4visual-FE/index.html`.

- [ ] **Step 3: Verify in browser**

Refresh `http://localhost:<FE>`. Open DevTools → Network → filter `fonts.gstatic.com`. Confirm Inter (`L29...`) and IBM Plex Mono (`P9w...`) appear in the request waterfall.

Smoke-test rendering — in DevTools console:
```js
document.body.style.fontFamily = "Inter, sans-serif";
```
Expected: visible font shift (Inter is more geometric than IBM Plex Sans). Reload the page to revert.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/index.html
git commit -m "feat(theme): load Inter and IBM Plex Mono fonts for J4 visual foundation"
```

---

## Phase 1B — Theme preset companions

### Task 2: Add `accentVioletScale` + `accentAmberScale` to `ThemePreset`

**Files:**
- Modify: `boilerplateFE/src/config/theme.config.ts`

- [ ] **Step 1: Extend the `ThemePreset` interface**

In `boilerplateFE/src/config/theme.config.ts` lines 12-19, replace:

```ts
interface ThemePreset {
  name: ThemePresetName;
  label: string;
  light: ThemeMode;
  dark: ThemeMode;
  primaryScale: ColorScale;
  accentScale: ColorScale;
}
```

With:

```ts
interface ThemePreset {
  name: ThemePresetName;
  label: string;
  light: ThemeMode;
  dark: ThemeMode;
  primaryScale: ColorScale;
  accentScale: ColorScale;
  /** Violet companion — drives the cool axis of J4 Spectrum gradients & info-state pills.
   *  Optional: presets that omit it inherit a global default in `useThemePreset`. */
  accentVioletScale?: ColorScale;
  /** Amber companion — drives the warm axis of J4 feature-card icons & warning-state pills.
   *  Optional: presets that omit it inherit a global default in `useThemePreset`. */
  accentAmberScale?: ColorScale;
}
```

- [ ] **Step 2: Add violet + amber scales to the `warm-copper` preset**

Find the `warm-copper` preset (starts around line 22). Inside that object, after the closing `}` of `accentScale` and before the closing `}` of the preset, insert:

```ts
    accentVioletScale: {
      '50': '#eef2ff',
      '100': '#e0e7ff',
      '200': '#c7d2fe',
      '300': '#a5b4fc',
      '400': '#818cf8',
      '500': '#6366f1',
      '600': '#4f46e5',
      '700': '#4338ca',
      '800': '#3730a3',
      '900': '#312e81',
      '950': '#1e1b4b',
    },
    accentAmberScale: {
      '50': '#fffbeb',
      '100': '#fef3c7',
      '200': '#fde68a',
      '300': '#fcd34d',
      '400': '#fbbf24',
      '500': '#f59e0b',
      '600': '#d97706',
      '700': '#b45309',
      '800': '#92400e',
      '900': '#78350f',
      '950': '#451a03',
    },
```

(Don't add to the other 5 presets — they'll fall back to the same default values via `useThemePreset` in Task 7.)

- [ ] **Step 3: Type-check**

```bash
cd boilerplateFE && npx tsc --noEmit
cd ..
```
Expected: zero errors.

- [ ] **Step 4: Copy to test app**

```bash
cp_fe src/config/theme.config.ts
```
Expected: `→ _testJ4visual/_testJ4visual-FE/src/config/theme.config.ts`.

- [ ] **Step 5: Verify HMR picks it up**

In the browser, open `http://localhost:<FE>`. Vite HMR should silently apply the change (no visible effect — the new fields aren't read yet). DevTools console should show no errors.

- [ ] **Step 6: Commit**

```bash
git add boilerplateFE/src/config/theme.config.ts
git commit -m "feat(theme): add optional violet/amber companion scales to ThemePreset; populate warm-copper"
```

---

## Phase 1C — `@theme` registration

### Task 3: Register violet/amber color scales + Inter in the `@theme` block

**Files:**
- Modify: `boilerplateFE/src/styles/index.css` (the `@theme { ... }` block, around line 95)

This step makes `bg-violet-500`, `text-amber-700`, `font-display`, etc. available as Tailwind utilities at build time. The runtime overrides in Task 7 keep the same names but allow per-preset variation.

- [ ] **Step 1: Find the end of the `@theme` block**

The current `@theme` ends around line 169 (just after the `--transition-slow: 300ms;` line, before the `}` that closes `@theme`).

- [ ] **Step 2: Insert before the closing `}` of `@theme`**

After `--transition-slow: 300ms;` and before `}`, insert:

```css

  /* ── J4 Companion accent scales — violet (info / spectrum cool axis) ── */
  --color-violet-50: #eef2ff;
  --color-violet-100: #e0e7ff;
  --color-violet-200: #c7d2fe;
  --color-violet-300: #a5b4fc;
  --color-violet-400: #818cf8;
  --color-violet-500: #6366f1;
  --color-violet-600: #4f46e5;
  --color-violet-700: #4338ca;
  --color-violet-800: #3730a3;
  --color-violet-900: #312e81;
  --color-violet-950: #1e1b4b;

  /* ── J4 Companion accent scales — amber (warning / feature-icon rotation) ── */
  --color-amber-50: #fffbeb;
  --color-amber-100: #fef3c7;
  --color-amber-200: #fde68a;
  --color-amber-300: #fcd34d;
  --color-amber-400: #fbbf24;
  --color-amber-500: #f59e0b;
  --color-amber-600: #d97706;
  --color-amber-700: #b45309;
  --color-amber-800: #92400e;
  --color-amber-900: #78350f;
  --color-amber-950: #451a03;

  /* ── J4 Display font — used for hero metric and section titles only ── */
  --font-display: 'Inter', 'IBM Plex Sans', system-ui, sans-serif;
```

- [ ] **Step 3: Copy to test app**

```bash
cp_fe src/styles/index.css
```

- [ ] **Step 4: Verify in browser**

> ⚠ **Tailwind 4 tree-shake gotcha:** the `@theme` block emits CSS custom properties **only for tokens whose utilities are actually used in source files**. After this task, `--color-amber-*` will appear in the served CSS (existing components use `bg-amber-*`/`text-amber-*`), but `--color-violet-*` and `--font-display` **will not** appear until Plan 2 introduces a consumer (e.g. an info pill using `bg-violet-100`, or a hero title using `font-display`). This is correct — the tokens are *registered*; Tailwind ships them on demand.
>
> For the J4 utilities in Tasks 4-6 that reference `var(--color-violet-300)`, **Task 7 (`useThemePreset` runtime writes)** is the safety net: it sets every shade of violet/amber on `:root` directly, bypassing the tree-shaker. So J4 utility classes work whether or not Tailwind has emitted the `@theme` shade.

Refresh `http://localhost:<FE>`. In DevTools console:
```js
getComputedStyle(document.documentElement).getPropertyValue('--color-amber-500')
// expected: " #f59e0b" (utility-referenced, ships unconditionally)
getComputedStyle(document.documentElement).getPropertyValue('--color-violet-500')
// expected: empty string until Task 7 runs OR Plan 2 adds a violet utility
getComputedStyle(document.documentElement).getPropertyValue('--font-display')
// expected: empty string until something uses font-display utility
```

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/styles/index.css
git commit -m "feat(theme): register violet/amber scales and Inter display font in @theme"
```

---

## Phase 1D — Preset-aware J4 tokens

### Task 4: Add J4 tokens to `:root` (light mode) — preset-aware via `var()` + `color-mix()`

**Files:**
- Modify: `boilerplateFE/src/styles/index.css` (`:root` block in `@layer base`, around lines 13-55)

Note the architectural shift from the previous plan revision: **no hardcoded copper RGBA**. Aurora layers, glow halos, button gradient, and spectrum text all derive from `var(--color-primary)`, `var(--color-accent-*)`, and `var(--color-violet-*)`. Switch the active preset → J4 follows.

- [ ] **Step 1: Find the closing `}` of `:root`**

The current block ends with `--gradient-to: var(--color-primary-500);` followed by `}`.

- [ ] **Step 2: Insert J4 token block before the closing `}` of `:root`**

After the existing `--gradient-to: ...` line and before the `}` of `:root`, insert:

```css

    /* ── J4 Spectrum tokens (light) ──
     * All colors derived from preset scales via color-mix() — no hardcoded RGBA.
     * Switching activePreset cascades through aurora, spectrum text, glow, and gradient buttons.
     * Reference: docs/superpowers/specs/2026-04-26-phase-0-visual-foundation-design.md §3
     */
    --aurora-1: radial-gradient(ellipse 700px 300px at 30% 0%,
      color-mix(in srgb, var(--color-accent-400) 30%, transparent),
      transparent 60%);
    --aurora-2: radial-gradient(ellipse 600px 400px at 80% 30%,
      color-mix(in srgb, var(--color-primary) 25%, transparent),
      transparent 55%);
    --aurora-3: radial-gradient(ellipse 550px 500px at 50% 100%,
      color-mix(in srgb, var(--color-violet-300) 35%, transparent),
      transparent 60%);
    --aurora-corner: radial-gradient(circle at 100% 0%,
      color-mix(in srgb, var(--color-primary) 20%, transparent) 0%,
      transparent 50%);

    --spectrum-text: linear-gradient(135deg,
      var(--color-primary-800) 0%,
      var(--color-primary) 50%,
      var(--color-violet-700) 100%);

    --btn-primary-gradient: linear-gradient(135deg,
      var(--color-primary-400),
      var(--color-primary));

    --glow-primary-sm: 0 0 12px color-mix(in srgb, var(--color-primary) 40%, transparent);
    --glow-primary-md: 0 4px 16px color-mix(in srgb, var(--color-primary) 30%, transparent);
    --glow-primary-lg: 0 1px 0 rgba(255,255,255,0.30) inset,
                       0 6px 16px color-mix(in srgb, var(--color-primary) 28%, transparent);
    --glow-emerald-sm: 0 0 0 3px color-mix(in srgb, var(--color-accent-500) 18%, transparent);

    --surface-glass: rgba(255,255,255,0.70);
    --surface-glass-strong: rgba(255,255,255,0.85);
    --border-strong: color-mix(in srgb, var(--color-primary-900) 12%, transparent);
```

Token name choices:
- `--glow-primary-*` (not `--glow-copper-*`) — preset-neutral.
- `--surface-glass*` is achromatic (white-on-light, white-on-dark) — pure RGBA is correct here, no `color-mix`.
- `--border-strong` mixes the deepest primary shade with transparent so the border picks up a subtle warmth from any preset.

- [ ] **Step 3: Copy to test app**

```bash
cp_fe src/styles/index.css
```

- [ ] **Step 4: Verify**

Refresh `http://localhost:<FE>`. In DevTools console:
```js
getComputedStyle(document.documentElement).getPropertyValue('--aurora-2')
// expected: a radial-gradient(...) string with color-mix in it
getComputedStyle(document.documentElement).getPropertyValue('--spectrum-text')
// expected: a linear-gradient(135deg, #784528 0%, #c67a52 50%, #4338ca 100%)
//   (the var()s should already be resolved at this read level)
```

Visual check: existing pages render identically — no aurora is yet visible because no element uses `aurora-canvas` class. ✅ no regression.

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/styles/index.css
git commit -m "feat(theme): add preset-aware J4 tokens to :root (aurora, spectrum, glow, glass)"
```

### Task 5: Add J4 dark mode token overrides

**Files:**
- Modify: `boilerplateFE/src/styles/index.css` (`.dark` block, around lines 57-90)

In dark mode the structural rules stay (same gradient ellipse positions, same blur, same `color-mix()` recipe) but **opacity ratios shift**. The aurora needs higher color intensity against the deep background; the violet companion shifts brighter (`--color-violet-300` rather than `--color-violet-700`); the destructive glow gains a slight inner highlight.

- [ ] **Step 1: Find the closing `}` of `.dark`**

The current `.dark` block ends with `--gradient-to: var(--color-primary-600);` followed by `}`.

- [ ] **Step 2: Insert J4 dark overrides before the closing `}` of `.dark`**

After the existing `--gradient-to: ...` line and before the `}`, insert:

```css

    /* ── J4 Spectrum tokens (dark overrides) ──
     * Same structural recipe as :root, but with higher color-mix percentages
     * to balance the deeper canvas, and lighter spectrum-text stops for legibility.
     */
    --aurora-1: radial-gradient(ellipse 800px 400px at 30% 0%,
      color-mix(in srgb, var(--color-accent-400) 18%, transparent),
      transparent 60%);
    --aurora-2: radial-gradient(ellipse 700px 500px at 80% 30%,
      color-mix(in srgb, var(--color-primary) 32%, transparent),
      transparent 55%);
    --aurora-3: radial-gradient(ellipse 600px 600px at 50% 100%,
      color-mix(in srgb, var(--color-violet-400) 22%, transparent),
      transparent 60%);
    --aurora-corner: radial-gradient(circle at 100% 0%,
      color-mix(in srgb, var(--color-primary) 45%, transparent) 0%,
      color-mix(in srgb, var(--color-primary) 18%, transparent) 30%,
      transparent 60%);

    --spectrum-text: linear-gradient(135deg,
      var(--color-primary-300) 0%,
      var(--color-primary) 50%,
      var(--color-violet-300) 100%);

    --btn-primary-gradient: linear-gradient(135deg,
      var(--color-primary-400),
      var(--color-primary-700));

    --glow-primary-sm: 0 0 12px color-mix(in srgb, var(--color-primary) 50%, transparent);
    --glow-primary-md: 0 4px 16px color-mix(in srgb, var(--color-primary) 30%, transparent);
    --glow-primary-lg: 0 0 0 1px color-mix(in srgb, var(--color-primary) 40%, transparent),
                       0 8px 24px color-mix(in srgb, var(--color-primary) 35%, transparent),
                       inset 0 1px 0 rgba(255,255,255,0.18);
    --glow-emerald-sm: 0 0 8px var(--color-accent-300),
                       0 0 0 3px color-mix(in srgb, var(--color-accent-300) 18%, transparent);

    --surface-glass: rgba(255,255,255,0.04);
    --surface-glass-strong: rgba(255,255,255,0.06);
    --border-strong: rgba(255,255,255,0.10);
```

- [ ] **Step 3: Copy to test app**

```bash
cp_fe src/styles/index.css
```

- [ ] **Step 4: Verify in browser (both modes)**

Toggle dark mode via the existing app theme toggle (top right). In DevTools console:
```js
getComputedStyle(document.documentElement).getPropertyValue('--aurora-2')
// dark mode value should differ from light mode value (32% mix vs 25% mix)
getComputedStyle(document.documentElement).getPropertyValue('--spectrum-text')
// dark mode should reference lighter primary stops (--color-primary-300 vs --color-primary-800)
```

Toggle back to light. Confirm values revert. Existing pages still render identically in both modes.

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/styles/index.css
git commit -m "feat(theme): add J4 dark-mode token overrides with shifted opacity and stops"
```

---

## Phase 1E — Utility classes

### Task 6: Add J4 utility classes to `@layer utilities`

**Files:**
- Modify: `boilerplateFE/src/styles/index.css` (the `@layer utilities` block, around lines 214-262)

These are the "verbs" that compose the tokens above into actual CSS. Components apply them; tokens stay in CSS vars.

- [ ] **Step 1: Find the existing `.glass` utility**

The block currently ends (around line 257-261):
```css
  .glass {
    background-color: hsl(var(--card) / 0.85);
    @apply backdrop-blur-xl;
  }
}
```

- [ ] **Step 2: Insert J4 utilities before the closing `}` of `@layer utilities`**

After the existing `.glass` rule and before the closing `}`, insert:

```css

  /* ── J4 Spectrum utilities ──
   * Apply via className. Recipes use the J4 tokens defined in :root and .dark above,
   * so they automatically follow the active preset and theme mode.
   */

  /* Full aurora canvas — for landing/marketing/hero surfaces.
   * Generates three radial blooms from a positioned ::before. Apply on a
   * positioned ancestor (the rule sets isolation:isolate for stacking safety). */
  .aurora-canvas {
    position: relative;
    isolation: isolate;
  }
  .aurora-canvas::before {
    content: '';
    position: absolute;
    inset: -100px;
    background: var(--aurora-1), var(--aurora-2), var(--aurora-3);
    filter: blur(20px);
    pointer-events: none;
    z-index: -1;
  }

  /* Single corner bloom — for dense list pages where full aurora would compete with data.
   * Activate by adding data-page-style="dense" on the canvas container. */
  [data-page-style="dense"].aurora-canvas::before {
    background: var(--aurora-corner);
    filter: none;
  }

  /* 40px grid texture — adds structure on top of aurora.
   * Masked into a center vignette so it never reaches the data. */
  .aurora-grid::after {
    content: '';
    position: absolute;
    inset: 0;
    background-image:
      linear-gradient(currentColor 1px, transparent 1px),
      linear-gradient(90deg, currentColor 1px, transparent 1px);
    background-size: 40px 40px;
    opacity: 0.025;
    mask-image: radial-gradient(ellipse 1200px 600px at center top, black 30%, transparent 80%);
    pointer-events: none;
    z-index: -1;
  }

  /* Spectrum text gradient — one moment of accent text per viewport. */
  .gradient-text {
    background: var(--spectrum-text);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
    color: transparent;
  }

  /* Glass surfaces — translucent + backdrop-blur. */
  .surface-glass {
    background-color: var(--surface-glass);
    border: 1px solid var(--border-strong);
    backdrop-filter: blur(20px);
    -webkit-backdrop-filter: blur(20px);
  }
  .surface-glass-strong {
    background-color: var(--surface-glass-strong);
    border: 1px solid var(--border-strong);
    backdrop-filter: blur(24px);
    -webkit-backdrop-filter: blur(24px);
  }

  /* Primary-colored glow halos — for the primary CTA and brand mark. */
  .glow-primary-sm { box-shadow: var(--glow-primary-sm); }
  .glow-primary-md { box-shadow: var(--glow-primary-md); }
  .glow-primary-lg { box-shadow: var(--glow-primary-lg); }

  /* Pulse dot — the live/healthy/online signal.
   * Uses the existing subtle-pulse keyframe at the bottom of this file. */
  .pulse-dot {
    width: 6px;
    height: 6px;
    border-radius: 9999px;
    background: var(--color-accent-500);
    box-shadow: var(--glow-emerald-sm);
    animation: subtle-pulse 2.4s ease-in-out infinite;
  }
  @media (prefers-reduced-motion: reduce) {
    .pulse-dot { animation: none; }
  }

  /* Primary gradient button background — pair with .glow-primary-md (light) or .glow-primary-lg (dark). */
  .btn-primary-gradient {
    background: var(--btn-primary-gradient);
    color: hsl(var(--primary-foreground));
  }
```

- [ ] **Step 3: Copy to test app**

```bash
cp_fe src/styles/index.css
```

- [ ] **Step 4: Smoke-test the utilities live**

Refresh `http://localhost:<FE>`. In DevTools, find the `<body>` element and add `class="aurora-canvas"`. Expected: a faint multi-color bloom appears behind the page (visible most clearly in dark mode).

Pick any visible text node, wrap a fragment in `<span class="gradient-text">`. Expected: that span renders with the spectrum gradient.

Add a `<button class="btn-primary-gradient glow-primary-md" style="padding: 9px 18px; border-radius: 10px; border: 0;">Test CTA</button>` somewhere visible. Expected: gradient copper button with a soft halo.

Toggle to dark mode. Expected: the same elements still look right; aurora intensifies, gradient shifts.

Remove all the test injections. Existing pages still render identically (utilities are applied only via the inspector test, no source change to existing components).

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/styles/index.css
git commit -m "feat(theme): add J4 utilities (aurora-canvas/grid, gradient-text, surface-glass, glow-primary, pulse-dot, btn-primary-gradient)"
```

---

## Phase 1F — Runtime token writes

### Task 7: Update `useThemePreset` to write violet/amber scales with global fallback

**Files:**
- Modify: `boilerplateFE/src/hooks/useThemePreset.ts`

This is the bridge between preset config (Task 2) and `@theme` registration (Task 3). At runtime, when the active preset changes (or the theme mode flips), we write the violet/amber values from the preset (or fall back to defaults) to CSS vars — overriding the static `@theme` values when needed.

- [ ] **Step 1: Find the existing `accentScale` loop**

The hook currently has at lines 29-31:

```ts
      for (const [shade, hex] of Object.entries(preset.accentScale)) {
        root.setProperty(`--color-accent-${shade}`, hex);
      }
```

- [ ] **Step 2: Insert violet + amber writes immediately after that loop**

Right after the `accentScale` loop closes (line 31 — between that `}` and the surface-color block at line 34), insert:

```ts
      // Violet (info / spectrum cool axis) and amber (warning / feature-icon rotation).
      // Optional per-preset override; presets that don't declare them inherit a global default
      // identical to the warm-copper preset's values, so every preset gets a sensible spectrum
      // even without explicit configuration.
      const violetDefault: ColorScale = {
        '50': '#eef2ff', '100': '#e0e7ff', '200': '#c7d2fe', '300': '#a5b4fc',
        '400': '#818cf8', '500': '#6366f1', '600': '#4f46e5', '700': '#4338ca',
        '800': '#3730a3', '900': '#312e81', '950': '#1e1b4b',
      };
      const amberDefault: ColorScale = {
        '50': '#fffbeb', '100': '#fef3c7', '200': '#fde68a', '300': '#fcd34d',
        '400': '#fbbf24', '500': '#f59e0b', '600': '#d97706', '700': '#b45309',
        '800': '#92400e', '900': '#78350f', '950': '#451a03',
      };
      const violet = preset.accentVioletScale ?? violetDefault;
      const amber = preset.accentAmberScale ?? amberDefault;
      for (const [shade, hex] of Object.entries(violet)) {
        root.setProperty(`--color-violet-${shade}`, hex);
      }
      for (const [shade, hex] of Object.entries(amber)) {
        root.setProperty(`--color-amber-${shade}`, hex);
      }
```

- [ ] **Step 3: Import the `ColorScale` type if not already imported**

Check the top of `useThemePreset.ts`. The current imports are:
```ts
import { useEffect } from 'react';
import { activePreset, presets } from '@/config/theme.config';
import { useUIStore, selectTheme } from '@/stores';
```

`ColorScale` is currently a non-exported type. **Update** `boilerplateFE/src/config/theme.config.ts` to export it — change line 10 from:

```ts
type ColorScale = Record<ColorScaleShade, string>;
```

to:

```ts
export type ColorScale = Record<ColorScaleShade, string>;
```

Then update the import in `useThemePreset.ts` to:

```ts
import { activePreset, presets, type ColorScale } from '@/config/theme.config';
```

- [ ] **Step 4: Type-check**

```bash
cd boilerplateFE && npx tsc --noEmit
cd ..
```
Expected: zero errors.

- [ ] **Step 5: Copy both files to test app**

```bash
cp_fe src/config/theme.config.ts src/hooks/useThemePreset.ts
```

- [ ] **Step 6: Verify runtime writes**

Refresh `http://localhost:<FE>`. In DevTools console:
```js
getComputedStyle(document.documentElement).getPropertyValue('--color-violet-500')
// expected: " #6366f1" (set by useThemePreset, overrides the @theme default — same value here)
getComputedStyle(document.documentElement).getPropertyValue('--color-amber-500')
// expected: " #f59e0b"
```

Toggle theme between light and dark. Re-run the same query. Values should remain identical (violet/amber don't have light/dark variants; this is by design).

- [ ] **Step 7: Commit**

```bash
git add boilerplateFE/src/config/theme.config.ts boilerplateFE/src/hooks/useThemePreset.ts
git commit -m "feat(theme): export ColorScale type and write violet/amber scales at runtime in useThemePreset"
```

---

## Phase 1G — End-to-end verification

### Task 8: Full verification on the test app

**Files:** none modified — verification only.

- [ ] **Step 1: Production build of source**

```bash
cd boilerplateFE && npm run build
cd ..
```
Expected: build succeeds with zero TypeScript or Vite errors. Output: CSS bundle ~+2-3KB from new tokens/utilities, font payload ~+85KB (Inter 200/300/500/600 + IBM Plex Mono 400/500).

- [ ] **Step 2: Lint source**

```bash
cd boilerplateFE && npm run lint
cd ..
```
Expected: zero new warnings or errors. (Pre-existing lint state, if any, is untouched.)

- [ ] **Step 3: Production build of test app — sanity check**

```bash
cd _testJ4visual/_testJ4visual-FE && npm run build
cd ../..
```
Expected: same outcome as Step 1. This confirms the test app stays buildable.

- [ ] **Step 4: Browser smoke test**

The test app FE dev server is still running from Task 0. Refresh `http://localhost:<FE>`. Verify:

- Login page renders identically to the pre-Phase-1 baseline (no aurora, no glass, no gradient text — those are only visible when explicitly applied in plan 2).
- Dashboard, Tenants list, Users list, Settings — all render identically.
- DevTools → Console → no errors.
- DevTools → Network → fonts include Inter (4 weights) and IBM Plex Mono (2 weights).

In the DevTools console, run:
```js
const t = (n) => getComputedStyle(document.documentElement).getPropertyValue(n).trim();
const tokens = {
  '--aurora-1': t('--aurora-1'),
  '--aurora-2': t('--aurora-2'),
  '--aurora-3': t('--aurora-3'),
  '--spectrum-text': t('--spectrum-text'),
  '--btn-primary-gradient': t('--btn-primary-gradient'),
  '--glow-primary-md': t('--glow-primary-md'),
  '--surface-glass': t('--surface-glass'),
  '--border-strong': t('--border-strong'),
  '--color-violet-500': t('--color-violet-500'),
  '--color-amber-500': t('--color-amber-500'),
  '--font-display': t('--font-display'),
};
console.table(tokens);
```

Expected: every row has a non-empty value. Aurora rows contain `radial-gradient(...)`. Spectrum and btn-primary rows contain `linear-gradient(135deg, ...)`. Violet/amber resolve to hex. `--font-display` starts with `'Inter'`.

Toggle to dark mode. Re-run the snippet. Expected: aurora and spectrum-text values change (dark recipes); violet/amber/font-display unchanged.

- [ ] **Step 5: Test app health re-check**

```bash
curl -s http://localhost:<BE>/health | head -c 200
```
Expected: returns a JSON `{"status":"Healthy", ...}` (or `Degraded` is fine — outbox lag is OK).

- [ ] **Step 6: Push the branch**

```bash
git push origin fe/base
```
Expected: pushed cleanly.

If you'd rather hold the push for review, skip this step — every commit so far is local and reversible.

- [ ] **Step 7: Leave test app running**

Do **not** stop the BE or FE processes. Plan 2 will continue against the same running instance. The user may also want to do manual QA before plan 2 starts.

Report URLs to the user:
- Frontend: `http://localhost:<FE>` (login: `superadmin@testj4visual.com` / `Admin@123456`)
- Backend Swagger: `http://localhost:<BE>/swagger`
- Mailpit: `http://localhost:8025`

---

## What's done after this plan

- Inter and IBM Plex Mono fonts load.
- All J4 Spectrum tokens (aurora, spectrum text, glow halos, glass surfaces, gradient button) live in CSS as **preset-aware recipes** using `var()` + `color-mix()`. Switching `activePreset` rebrands J4 automatically.
- Violet + amber companion scales registered in `@theme` (build-time utilities) and overridable per-preset via `accentVioletScale` / `accentAmberScale` (runtime writes by `useThemePreset` with global fallbacks).
- Inter registered as `--font-display`.
- All J4 utility classes available: `.aurora-canvas`, `.aurora-grid`, `.gradient-text`, `.surface-glass`, `.surface-glass-strong`, `.glow-primary-{sm,md,lg}`, `.pulse-dot`, `.btn-primary-gradient`.
- **Zero existing pages have any visual change.** Apps continue to render identically. Visual change starts in plan 2.
- Test app `_testJ4visual` running on `<BE>` / `<FE>` ports for the rest of Phase 0.

## What's next (plans 2 & 3)

- **Plan 2 — Component restyle + `/styleguide` page** (development-only). Apply J4 styling to every shadcn primitive and every common component. Build the live Style Reference page.
- **Plan 3 — Layouts & Landing.** New Header, Sidebar, AuthLayout/MainLayout/PublicLayout, and the 8-section landing page composition.

Both should be brainstormed → spec'd-as-needed → planned in their own cycles. They reference the tokens this plan introduces and run their iterations against the same persistent test app set up in Task 0.

---

## Self-Review checklist

**Spec coverage (this plan only — §3, §4 typography prereq, §6 shadow & glow):**
- §3.1 Primary/accent/destructive — already in place; no change.
- §3.1 Violet/amber companions — Task 2 (preset config), Task 3 (`@theme` defaults), Task 7 (runtime writes with fallback).
- §3.2 Surfaces (`--surface-glass`, `--surface-glass-strong`, `--border-strong`) — Tasks 4 & 5.
- §3.3 Aurora & glow tokens — Tasks 4 & 5 (now preset-aware via `color-mix()` + `var()` instead of hardcoded RGBA).
- §3.4 Status pill colors — needed in plan 2 (component-level); the underlying tokens (violet, amber, emerald, destructive) all exist after this plan.
- §4 Typography — Inter loaded (Task 1), `--font-display` registered (Task 3). Type-ramp utility application is plan 2.
- §6 Shadow & glow — `.glow-primary-{sm,md,lg}`, `.glow-emerald-sm`, `.btn-primary-gradient` in Tasks 4, 5, 6.

**Placeholder scan:** No "TBD", "TODO", "implement later", "fill in details", or "similar to Task N" patterns. Each step has exact file paths, exact code, exact commands.

**Type consistency:** Token names used in Task 6 utilities (`--aurora-1/2/3`, `--aurora-corner`, `--spectrum-text`, `--btn-primary-gradient`, `--glow-primary-{sm,md,lg}`, `--glow-emerald-sm`, `--surface-glass`, `--surface-glass-strong`, `--border-strong`) all match the names introduced in Tasks 4 & 5. `accentVioletScale` / `accentAmberScale` field names match between Task 2 (interface + warm-copper preset) and Task 7 (runtime fallback). `ColorScale` type export added in Task 7 Step 3.

**Architectural choice (preset-aware tokens):** documented in plan header `Architecture` block; reflected in Tasks 4 & 5 commit messages and the spec self-review crosswalk above.

---

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-04-27-visual-foundation-tokens.md`.

**Two execution options:**

1. **Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration. Best for this plan because per-task browser smoke tests against the persistent test app catch CSS/HMR issues immediately.

2. **Inline Execution** — Execute tasks in this session using `superpowers:executing-plans`, batch execution with checkpoints for review.

**Which approach?**
