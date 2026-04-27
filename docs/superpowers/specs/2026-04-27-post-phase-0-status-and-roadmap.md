# Post Phase 0 — Status & Roadmap for Continued Visual Work

**Created:** 2026-04-27
**Status:** Phase 0 shipped on `fe/base` (PR pending merge to `main`).
**Audience:** A future Claude session (or human) picking up this work after Phase 0 lands.

---

## 1. Where things stand

### Phase 0 is shipped

- **Spec:** `docs/superpowers/specs/2026-04-26-phase-0-visual-foundation-design.md`
- **Plans (executed in order):**
  - `docs/superpowers/plans/2026-04-27-visual-foundation-tokens.md` — Tokens & utilities
  - `docs/superpowers/plans/2026-04-27-component-restyle-foundation.md` — Foundation primitives + `/styleguide`
  - `docs/superpowers/plans/2026-04-27-component-restyle-composite.md` — Composite primitives + functional UI
  - `docs/superpowers/plans/2026-04-27-layouts-and-landing.md` — Layouts + 8-section landing
- **Branch:** `fe/base` — 51 commits, +8.7k / −0.5k

### What J4 Spectrum delivers (one-line summary per surface)

- **Token system** — preset-aware via `color-mix()` + `var()`; switch `activePreset` in `theme.config.ts` and the entire J4 system rebrands at runtime
- **18 shadcn primitives** restyled with backwards-compatible variant defaults
- **24 common components** (`@/components/common/`) restyled, including the four functional UI controls (`LanguageSwitcher`, `ThemeToggle`, `NotificationBell`, `UserAvatar`)
- **Three layouts** — `MainLayout` (dense aurora), `AuthLayout` (single-column with full-screen network), `PublicLayout` (full aurora)
- **8-section landing** at `/` — `LandingNav`, `HeroSection`, `TechStrip`, `FeatureGrid`, `AiSection`, `CodeSection`, `ArchitectureSection`, `StatsStrip`, `FooterCta`
- **Dashboard** redesigned as a J4-native page (gradient hero metric, sparkline stat cards, glass activity feed)
- **Auth flow** rebuilt — single-column with full-screen `HeroLinesBackground`, animated brand halo, gradient-text welcome
- **`/styleguide`** dev-only reference page with 11 sections — runs against `import.meta.env.DEV`, tree-shaken from prod

### Verifiable artifacts

- The test app at `_testJ4visual/` (gitignored, created via `pwsh scripts/rename.ps1 -Name "_testJ4visual" -OutputDir "."`) — running on BE 5100 / FE 3100 during the original work, may need re-spinning
- `/styleguide` (dev-only) renders every primitive + every common component
- `npm run build` passes; production bundle excludes the styleguide

---

## 2. Remaining phases — in order of priority

Each phase below is its own brainstorm → spec (if needed) → plan → execute cycle. Pick the next one based on team priority. Each is independently shippable.

### Phase 1 — Feature page polish *(recommended next)*

**Scope:** Apply the J4 system to the 22 feature folders in `src/features/`. The primitive restyle + layout restyle from Phase 0 has already cascaded into every page that uses `Card`, `Button`, `Table`, `PageHeader`, `EmptyState`, etc. Each page now needs:

- Light polish — confirm consistent use of `PageHeader`, `EmptyState`, `Pagination`, `Card` variants
- Hero stat moments where it makes sense (e.g., `UsersListPage` could have a hero metric like the dashboard)
- Replace any remaining `gradient-hero` (the old solid copper) with J4 surfaces
- Apply the dashboard pattern (stat strip + glass content + activity feed) where applicable
- Add per-page character touches following the pattern from the landing (live indicators, sparklines, hover lifts)

**Suggested clusters and order** (each cluster ≈ 1-3 PRs):

1. **Identity** (5 features) — `users`, `roles`, `tenants`, `access`, `profile`. Highest-traffic, sets the tone.
2. **Platform admin** (4) — `settings`, `feature-flags`, `api-keys`, `audit-logs`
3. **Data** (3) — `files`, `reports`, `notifications`
4. **Commerce** (2 modules) — `billing` (plans/subscriptions/payments), `products`
5. **Workflow & comms** (5 modules) — `workflow`, `communication`, `comments-activity`, `import-export`, `webhooks`
6. **Onboarding** (1 module) — `onboarding` wizard

**Approach for each cluster:**
- Brainstorm what character each page should carry (re-use the landing/dashboard patterns)
- Spec only if the cluster introduces new visual decisions; otherwise go directly to a plan
- Execute via `superpowers:subagent-driven-development` (the cadence used in Phase 0)

### Phase 2 — AI module UI

**Backend status:** advanced. See `docs/superpowers/specs/2026-04-23-ai-module-vision-revised-design.md` for vision and Plan 5c-2 (templates) which is the most-recent shipped/in-progress backend work. The backend ships RAG pipeline, agent runtime, `[AiTool]` auto-discovery, persona-aware safety, agent templates.

**Frontend surfaces needed (rough breakdown):**

- **Chat surfaces** — tenant-customizable agent chat, streaming responses, persona context, citation rendering. `AiSection` on the landing already mocks this (`features/landing/components/AiSection.tsx`) — that mock is the design template.
- **Tool registry browser** — list `[AiTool]`-decorated tools auto-discovered from each module
- **Persona × Role admin** — edit personas, assign agents, configure safety presets. The personas matrix in `AiSection.tsx` shows the visual language.
- **Agent template browser + install flow** — module-authored templates that admins install into a tenant
- **RAG ingestion + KB management** — upload docs, manage knowledge bases per tenant
- **RAG eval dashboards** — faithfulness scoring, retrieval quality charts. Reference `docs/modules/ai/features/rag-dashboards.md`.
- **Public widget configuration** — anonymous persona, embed code generation, origin pinning

**Likely:** a separate spec family. Larger than Phase 1.

### Phase 3 — Mobile (Flutter) port

**Scope:** Port J4 to the Flutter app under `boilerplateMobile/`. Different mechanisms — Flutter has no CSS, themes happen via Dart objects.

- Color tokens in `lib/app/theme/app_colors.dart` (manual sync with FE per CLAUDE.md note)
- Material 3 theme override with copper/gradient accents
- Reusable widgets matching the React primitives (Button, Card, EmptyState, Pagination)
- Animations via `flutter_animate` or implicit animations

Estimate: separate plan family, similar size to Phase 0 (probably 3-4 plans).

### Optional follow-on — Marketing site / Public docs

If a standalone marketing site is desired (separate from the app's `/` landing), or a public docs portal — that's a Phase X.

---

## 3. Phase 0 follow-ups (small items deferred from code review)

Tracked here so a future session can pick them up without re-reviewing the diff:

- **AR + KU translations** for new keys — Phase 0 added keys under `dashboard.{live,platform,workspace,liveBadge,delta30d,delta7d,rolesEnabled,allSystems,usersLabel,rolesLabel,eventsLabel,infiniteEvents}` and a new `auth_chrome` namespace. Only `en/translation.json` populated. i18next falls back to en for missing keys — translate AR + KU when a localizer is available.
- **Extend `--tinted-fg`-style semantic tokens to violet + accent** — `AiSection`'s chat preview (persona chips, citations) and `PersonasPreview` matrix still use `text-[var(--color-violet-700)] dark:text-[var(--color-violet-300)]` and similar accent patterns. Pattern to apply: `--violet-tinted-fg` + `--accent-tinted-fg` semantic tokens, then drop the `dark:` overrides.
- **Consolidate `useCountUp`** — currently three implementations (`StatsStrip.tsx`, `DashboardPage.tsx`, inline in `HeroSection.tsx`). Extract to `@/hooks/useCountUp` once a fourth consumer appears.
- **`HeroSection` setTimeout choreography** — works but a `useReducer` would be cleaner for the multi-phase boot sequence.
- **`AiSection` capability list hardcoded English** — the four pills (`RAG · offline eval harness` etc.) are landing copy, exempt from the strict i18n rule but flag if the marketing surface ever needs localization.
- **CLAUDE.md** — already partially updated alongside this doc (Table glass surface, Card variants, Badge J4 variants, J4 utilities listed). If new conventions emerge from Phase 1, append.

---

## 4. How to start a new chat session

A future Claude session (or human) joining this codebase should:

1. **Read in this order (≈30 minutes total):**
   - `CLAUDE.md` (project root) — overall conventions + Frontend Rules
   - `docs/superpowers/specs/2026-04-26-phase-0-visual-foundation-design.md` — the design vocabulary (tokens, components, layouts)
   - This doc — current status + what's next
   - `docs/superpowers/plans/2026-04-27-visual-foundation-tokens.md` — Plan 1 sets the iteration cadence + test-app workflow

2. **Start the test app** (the verification harness used during Phase 0):
   ```bash
   pwsh -File scripts/rename.ps1 -Name "_testJ4visual" -OutputDir "."
   # Then follow .claude/skills/post-feature-testing.md to:
   #   - drop stale DB
   #   - configure ports + CORS + env
   #   - generate migrations across all 8 module DbContexts
   #   - start BE + FE in background with nohup
   ```
   (If the existing `_testJ4visual/` directory still exists, just restart the BE/FE processes — no need to regenerate.)

3. **Verify Phase 0 visually:**
   - `http://localhost:3100/` — landing page (network background, dashboard preview, AI section, etc.)
   - `http://localhost:3100/login` — single-column auth
   - `http://localhost:3100/dashboard` — gradient hero metric, sparkline stat cards
   - `http://localhost:3100/styleguide` — every primitive in J4 form (dev-only)

4. **Pick the next phase** based on priority (Phase 1 Identity cluster recommended):
   - Run `superpowers:brainstorming` to align on scope per cluster
   - Write a spec only if new design decisions are needed
   - Write a plan via `superpowers:writing-plans`
   - Execute via `superpowers:subagent-driven-development`

5. **Workflow conventions established in Phase 0:**
   - Iterate via file-copy from source → test app (do NOT regenerate the test app per change)
   - Commits land directly on the working branch (no per-task feature branches needed within a plan)
   - Each plan does its own subagent-driven execution; reviews happen per-task
   - Final code-review pass before push (dispatch `superpowers:code-reviewer` on the diff)

---

## 5. Open questions for the next session

- **`/styleguide` access in production?** Currently dev-only. If a public design system page is wanted, change the route gating — but understand it'll add the bundle weight.
- **Custom tenant branding extending to spectrum?** Today only `--primary` overrides per tenant. The tri-color spectrum (copper/emerald/violet) is fixed. Phase 1+ may want to expose a `--tenant-spectrum-cool` etc. for fully-branded tenants.
- **Marketing copy localization** for the landing — ⁠hardcoded English currently. If the marketing site goes multi-lingual, the landing copy needs to migrate to translation keys.
- **Light-vs-dark default** — preset is currently warm-copper with both modes supported. Decision on which mode the app boots in (currently follows OS pref via theme toggle's "system" mode).

---

## 6. Acknowledgements

Phase 0 visual foundation was developed across 4 plans + post-ship polish over 51 commits on `fe/base`. The cadence — brainstorm → spec → plan → subagent-driven execution → code review pass — proved out and is the recommended cadence for Phase 1+ work.

The test app harness (`_testJ4visual`) and the source-to-test-app file-copy iteration model proved much faster than re-running the rename script per change. Future phases should preserve this workflow.
