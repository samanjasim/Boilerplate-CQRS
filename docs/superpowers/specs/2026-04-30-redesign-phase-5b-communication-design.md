# Phase 5b Communication — comms cluster (5 pages + side drawer + 4 new components)

**Created:** 2026-04-30
**Branch:** `fe/phase-5b-design` (off latest `origin/main` — Phase 5a Workflow PR #44 already merged)
**Predecessors:** Phase 0 (foundation), Phase 1 (Identity), Phase 2 (Platform admin), Phase 3 (Data, PR #35), Phase 4 Billing (PR #37), Phase 4 Products (PR #41), Phase 5a Workflow (PR #44).
**Roadmap reference:** [`2026-04-29-redesign-phase-5a-workflow-design.md`](2026-04-29-redesign-phase-5a-workflow-design.md) §12 — "Phase 5b — Communication cluster: TemplatesPage, TriggerRulesPage, ChannelsPage, IntegrationsPage, DeliveryLogPage + 5+ dialogs."

This is the second of three Phase 5 PRs. After 5b ships, the comms cluster is on J4 Spectrum; webhooks, import-export, comments-activity, and onboarding remain in 5c.

---

## 1. Goal

Bring all 5 communication pages onto J4 Spectrum tokens, with **three structural changes earned by data shape**:

1. **DeliveryLog: detail modal → side drawer** + **4-card 7-day status hero**. The page is the daily-driver operational surface for the entire delivery pipeline; today every failure forces a centered-modal context-loss before clicking the next failed row. A right-side drawer keeps the list as the anchor and lets operators sweep through failures rapidly.
2. **Templates: sticky category rail** (left 200px on `lg+`, URL-persisted selection) replacing the chip-row + stacked-headings UI. Earned because tenants accumulate 5+ categories with 30+ templates over time, and today picking a category requires scanning the chip row + scrolling the page.
3. **Channels & Integrations: paired card refresh** — provider logos in card headers, status hero strip, default-channel glow, age-tinted last-tested chip. The two pages are sibling surfaces; treating one without the other would feel inconsistent.

TriggerRules is token-sweep-only with one small polish (channel-sequence chip-arrow chain).

## 2. Non-goals

Locked in at brainstorm; listed up front so they don't get relitigated:

- **No bulk operations** anywhere: no bulk-delete channels, no bulk-toggle trigger rules, no bulk-resend deliveries, no bulk-archive templates.
- **No template preview pane** in TemplatesPage. The editor stays in `TemplateEditorDialog`. A live-rendered preview belongs in a future scope (with token-substitution UI).
- **No drag-to-reorder** on `channelSequence` in TriggerRules. Order is set in `TriggerRuleFormDialog` and persisted as-is.
- **No log export** (CSV/PDF) from DeliveryLogPage — defer.
- **No real-time push** of new delivery rows (SignalR/WebSocket). Page relies on TanStack Query refetch (manual + window focus).
- **No PII-aware search-by-recipient autocomplete** — the existing template-name input is enough for 5b.
- **No webhooks, import-export, onboarding, or comments-activity** — those stay in 5c.
- **No new permissions.** Reuse existing `Communication.View*` / `Communication.Manage*` / `Communication.Resend` / `Communication.ViewDeliveryLog`.
- **No JSON-view for trigger-rule conditions** — defer; `TriggerRuleFormDialog` stays the editing surface.
- **No mobile-specific reflow** beyond standard `lg+`/`<lg` breakpoint stacking. The category rail and side drawer have explicit mobile fallbacks; nothing else is mobile-tuned in this PR.
- **No translation deferral.** EN + AR + KU land inline with each component change.
- **No backend changes beyond the one new status-counts endpoint.** No new entities, no schema migrations, no new permissions, no DTO field additions on the existing list/detail endpoints.
- **No keyboard up/down row navigation** in the DeliveryLog drawer if it shows friction during the plan stage. The drawer is the structural win; keyboard nav is a stretch goal.
- **No structural change** to the 8 existing dialogs (`TemplateEditorDialog`, `TriggerRuleFormDialog`, `ChannelSetupDialog`, `IntegrationSetupDialog`, `NotificationPreferencesPanel`, `RequiredNotificationsManager`, `CommunicationDashboardWidget`) beyond token sweep. `DeliveryDetailModal` is converted to a drawer; its body content is unchanged.

## 3. Pages

### 3.1 DeliveryLogPage *(admins + operators — `/communication/delivery-log`, perm `Communication.ViewDeliveryLog`)*

Today: `<PageHeader>` + filter row (status / channel / template-name) + `<Table>` (timestamp / recipient / template / channel / status / duration / attempts / actions) + `<Pagination>` + `<DeliveryDetailModal>` (centered Dialog, `max-w-2xl max-h-[85vh]`). ~210 LOC. Heavy operational page; failure-sweep is the dominant workflow and today's centered modal forces context loss between rows.

Redesign:

- **4-card status hero** above the filter row, using the shared `<MetricCard>` (Phase 4 / 5a pattern). Cards:
  - `Delivered` — `tone="active"` (the hero metric — successful sends in the window).
  - `Failed` — `tone="destructive"`.
  - `Pending` — `tone="default"` (folds `Pending` + `Queued` + `Sending`).
  - `Bounced` — `tone="destructive"`.
- **Window: last 7 days.** Shown as a small label below the strip (`"Last 7 days"`). Window is fixed at 7 days for 5b; a window selector is a future scope.
- **Collapse-when-zero rule.** Cards with a zero count collapse out of the row; survivors re-flow. When all four are zero (no deliveries in window), the hero hides entirely and the page falls through to the existing `<EmptyState>` (only if no logs exist at all — otherwise the hero just shows survivors).
- **Counts come from a new BE endpoint** (see §4) — `GET /api/v1/communication/deliveries/status-counts?windowDays=7`. Tenant-scoped via existing module DbContext filter.
- **Per-row leading-edge color stripe** — the structural row treatment. `border-inline-start: 3px solid var(--<token>)` keyed by `log.status`:
  - `Delivered` → `var(--color-emerald-500)`
  - `Failed` / `Bounced` → `var(--destructive)`
  - `Pending` / `Queued` / `Sending` → `var(--active-bg)` (copper amber)
  - default fallback → `transparent`
  Bar uses `border-inline-start` so it flips correctly on RTL. If the stripe proves too subtle during test-app QA, swap to a faint full-row tint (`bg-destructive/5` etc.) — decided live, not in spec.
- **Filter row** — keep current shape (status select, channel select, template-name input). Tokens swept. No structural change.
- **Table columns** — unchanged (timestamp / recipient / template / channel / status / duration / attempts / actions).
- **Detail surface — modal → side drawer.** Click on a row opens `<DeliveryDetailDrawer>` (extracted from `DeliveryDetailModal`, body content unchanged). Drawer slides in from the right at 560px width on `lg+`. List remains visible behind it; the clicked row is highlighted with a copper inset shadow (`box-shadow: inset 3px 0 0 var(--active-border)`). Click another row → drawer content swaps in place via TanStack Query keyed on the new `id`. On `<sm`, drawer falls back to a bottom sheet (full width, slides up from bottom, closes via tap-outside or X).
- **Pagination + EmptyState** — already on-pattern; tokens swept.

### 3.2 TemplatesPage *(admins — `/communication/templates`, perm `Communication.ViewTemplates`)*

Today: `<PageHeader>` + chip-row category filter (Button-based, "All categories" + per-category buttons) + space-y-8 stack of category sections, each with its own `<Table>`. ~192 LOC. `useMessageTemplates(selectedCategory)` re-fetches on each category change.

Redesign — **sticky category rail on `lg+`, chip-row fallback on `<lg`.**

#### Layout invariants

These are explicit design constraints the implementation must honor — calling them out so the code review pass has unambiguous criteria:

- **Two columns on `lg+`** — `grid-cols-[200px_minmax(0,1fr)]` with `gap-6`. Rail fixed at 200px; main column flexes.
- **Rail is `sticky top-{shell-header-h}` on `lg+`** — reuses the same `--shell-header-h` CSS var as 5a's instance-detail right rail. Falls back to `position: static` on `<lg`.
- **Rail surface** — `surface-glass` utility on `lg+`; plain `<Card>` on `<md` if rendered there (only fallback is the chip row, so this only applies to `lg+`).
- **"All categories" pseudo-row** — sits at the bottom of the rail with a separator above. Counts the total templates across all categories.
- **Default selection** — `undefined` (= "All categories" pseudo-row active) when the page loads with no `?category=` URL param. Mirrors today's default behavior. If `?category=` is present and matches a known category, that category is selected; if it doesn't match a known category, falls back to "All categories".
- **Persistence** — selected category lives in URL search param (`?category=<name>`). Absence of the param means "All categories". Refresh / share preserves selection. No Zustand store, no localStorage.
- **On `<lg`**, rail collapses to the existing chip-row UI above the table (today's behavior, tokens swept).
- **Counts come from client-side grouping** of the unfiltered template list. Switch from `useMessageTemplates(selectedCategory)` to `useMessageTemplates()` (no param) and group client-side. Templates are bounded (~50/tenant). If a tenant exceeds ~200 templates and pagination becomes necessary, add server-side category counts then; not now.

#### Component shape

- **`<TemplateCategoryRail>`** *(new — see §5.1)* — owns the rail rendering and active state.
- **Main column** — single `<Table>` of templates in the selected category, or all templates grouped by category headings (today's stacked layout) when "All categories" is active. Columns unchanged (Name / Module / Channel / Status / Actions). Templates sorted by name within each category. Header above the table shows `{categoryName}` (or `t('communication.templates.allCategories')`) as `<h3>` + count badge.
- **`<TemplateEditorDialog>`** — unchanged behavior; tokens swept.

#### Mobile (`<lg`)

Single-column stack: chip-row category filter (today's UI, tokens swept) → main column. Selection still drives URL `?category=`; "All categories" is the leading chip. The chip row is rendered by the same `<TemplateCategoryRail>` component with a `variant="chips"` prop on `<lg`, so behavior stays in one place.

### 3.3 ChannelsPage *(admins — `/communication/channels`, perm `Communication.ManageChannels`)*

Today: `<PageHeader>` + grouped channel sections (Email / Sms / Push / WhatsApp / InApp), each section has a 1–3-column card grid. Each `<Card>` shows displayName, provider, status badge, default star, last-tested timestamp, action buttons (test/edit/set-default/delete). ~231 LOC.

Redesign:

- **3-card status hero** above the grouped sections, using shared `<MetricCard>`. One card per `ChannelConfigStatus` enum value:
  - `Active` (`tone="active"`) — count of configs with `status === 'Active'`. Channels currently delivering.
  - `Configured` (`tone="default"`) — count of configs with `status === 'Inactive'`. Set up but disabled by the tenant admin.
  - `Errored` (`tone="destructive"`) — count of configs with `status === 'Error'`.
- **Counts client-derived** from existing `useChannelConfigs()` — no new BE. With ~5–15 configs per tenant, client grouping is trivial.
- **Collapse-when-zero rule** applies; if a tenant has zero configs, hero hides and existing `<EmptyState>` shows.
- **Card refresh** — extracted to `<ChannelConfigCard>` (see §5.1):
  - `<Card variant="elevated">` (lift on hover, already in design system).
  - **Provider header strip** — `<ProviderLogo>` (32×32 inline SVG) + `displayName` + `provider` subtitle. Tinted-initial fallback for unknown providers.
  - **Default star** — gains `brand-halo` utility for soft glow when `isDefault === true`.
  - **Status pill** — `STATUS_BADGE_VARIANT[cfg.status]`. `Error` gets a leading `AlertCircle` icon (4×4) inside the pill.
  - **Last-tested chip** — `formatDistanceToNow(cfg.lastTestedAt)` with age-tinted background:
    - `<1h` → emerald (`bg-[--color-emerald-500]/10 text-[--color-emerald-700]`)
    - `<24h` → muted (`bg-muted text-muted-foreground`)
    - `<7d` → amber (`bg-amber-500/10 text-amber-700` — falls back to inline tokens since no `state-warn` token exists yet; flagged in §11).
    - `>7d` → muted
    - `null` (never tested) → dash chip with neutral muted background and `text-muted-foreground`.
  - **Action buttons** — unchanged behavior (test / edit / set-default / delete). Ghost density tightened.
- **Grouping by channel type** — unchanged (Email / Sms / Push / WhatsApp / InApp). Section header gets the channel icon + name + count badge (today's pattern, tokens swept).
- **`<ChannelSetupDialog>`** — unchanged behavior; tokens swept.

### 3.4 IntegrationsPage *(admins — `/communication/integrations`, perm `Communication.ManageIntegrations`)*

Today: mirror of ChannelsPage but for Slack / Telegram / Discord / MicrosoftTeams. ~206 LOC.

Redesign — **symmetric to Channels; sibling surfaces share visual primitives.**

- **3-card status hero** — `Active` / `Configured` / `Errored`. Client-derived from `useIntegrationConfigs()`.
- **Card refresh** — extracted to `<IntegrationConfigCard>` (see §5.1) — sibling to `<ChannelConfigCard>`. Same visual treatment (provider logo, status pill with `AlertCircle` for Error, age-tinted last-tested chip). No default-star glow (integrations don't have a default concept).
- **Grouping by integration type** — unchanged. Section header gets the integration icon + name + count badge.
- **`<IntegrationSetupDialog>`** — unchanged behavior; tokens swept.

### 3.5 TriggerRulesPage *(admins — `/communication/trigger-rules`, perm `Communication.ManageTriggerRules`)*

Today: `<PageHeader>` + `<Table>` (Name / Event / Template / Channel sequence / Status / Actions) + `<EmptyState>`. ~190 LOC. Channel-sequence cell renders `Badge` chips with `1. Email`, `2. Sms`, etc.

Redesign — **token sweep + small visual polish on the channel-sequence cell.** No hero (5–20 rules per tenant is too small a dataset).

- **Channel-sequence cell** — replace the `1. Email` `<Badge>` chips with a `1 → 2 → 3` chip-arrow chain. Each chip shows the channel name; arrows are inline `ChevronRight` icons (rotate 180° on RTL via `rtl:rotate-180`). Visual polish only; no behavior change.
- **Status badges** — extend `STATUS_BADGE_VARIANT` in `@/constants/status.ts` with `Active` / `Inactive` mappings if not already present (likely already there from 5a; verify and skip if so).
- **Action buttons** — unchanged behavior (edit / toggle / delete). Tokens swept.
- **Table** — uses shared `<Table>` (no extra `<Card>` wrapper).
- **`<TriggerRuleFormDialog>`** — unchanged behavior; tokens swept.

## 4. Backend additions

One new query handler in `Starter.Module.Communication`. Reuses the existing `DeliveryLog` entity; **no schema changes, no migrations.**

### 4.1 `GetDeliveryStatusCountsQuery`

```csharp
public sealed record GetDeliveryStatusCountsQuery(int WindowDays = 7)
    : IRequest<Result<DeliveryStatusCountsDto>>;

public sealed record DeliveryStatusCountsDto(
    int Delivered,
    int Failed,
    int Pending,
    int Bounced,
    int WindowDays);
```

- Handler: `GetDeliveryStatusCountsQueryHandler` in `Application/Features/Communication/DeliveryLog/Queries/`.
- Scoping: filter `DeliveryLogs` by `createdAt >= DateTimeOffset.UtcNow.AddDays(-WindowDays)`. Tenant filter applied by the existing `CommunicationDbContext` global query filter.
- `WindowDays` clamped to `[1, 90]` server-side (defensive; controller-level `[Range]` validator ideally enforces this too).
- Buckets:
  - `Delivered` — `Status == DeliveryStatus.Delivered`
  - `Failed` — `Status == DeliveryStatus.Failed`
  - `Pending` — `Status In (Pending, Queued, Sending)`
  - `Bounced` — `Status == DeliveryStatus.Bounced`
- Endpoint: `GET /api/v1/communication/deliveries/status-counts?windowDays=7` on `CommunicationController`. Auth: `[Authorize(Policy = Permissions.Communication.ViewDeliveryLog)]`.
- No `[AiTool]` decoration — UI-only query.
- The returned `WindowDays` echoes back the (clamped) value so the FE can label the strip correctly even if it sent an invalid value.

### 4.2 No DTO field additions

`DeliveryLogDto`, `ChannelConfigDto`, `IntegrationConfigDto`, `MessageTemplateDto`, `TriggerRuleDto` — all unchanged. No FE-visible BE diff beyond the one new endpoint.

## 5. Frontend components

### 5.1 New components (5)

**`<SideDrawer>`** *(new — `src/components/ui/sheet.tsx`)*
- Radix-Dialog-based primitive (reusing `@radix-ui/react-dialog` already in the dep tree via `<Dialog>`). Anchored to the inline-end edge by default (right in LTR, left in RTL); bottom-sheet variant on `<sm`.
- Props: `open: boolean`, `onOpenChange: (open: boolean) => void`, `side?: 'end' | 'bottom'` (default `'end'`), `width?: 'sm' | 'md' | 'lg'` (default `'lg'`; sm=400px / md=480px / lg=560px), `children: ReactNode`. Header / Title / Description sub-components mirror `<Dialog>`'s API.
- Positioning uses `inset-inline-end-0` so the drawer sticks to the visual trailing edge regardless of writing direction. Animation uses Radix `data-state` attributes + Tailwind `transition-transform`; closed state translates the panel by 100% along the inline axis (sign-flipped on RTL via the `rtl:` variant).
- Accessibility: focus-trap inherited from Radix, Esc-to-close, click-outside-to-close (configurable).
- ~120 LOC.

**`<DeliveryDetailDrawer>`** *(extracted from `DeliveryDetailModal` — `src/features/communication/components/DeliveryDetailDrawer.tsx`)*
- Replaces `<DeliveryDetailModal>`. Body content is unchanged: summary grid + resend button + attempts timeline. Outer wrapper swaps from `<Dialog>` → `<SideDrawer>`.
- Props: `id: string`, `open: boolean`, `onOpenChange: (open: boolean) => void`. Same as today.
- ~190 LOC (mostly the same as today's modal).
- Old `DeliveryDetailModal.tsx` is deleted.

**`<ProviderLogo>`** *(new — `src/features/communication/components/ProviderLogo.tsx`)*
- Inline SVG map for 13 providers: `Smtp`, `SendGrid`, `Ses`, `Twilio`, `Fcm`, `Apns`, `TwilioWhatsApp`, `MetaWhatsApp`, `Ably`, `Slack`, `Telegram`, `Discord`, `MicrosoftTeams`. Falls back to a tinted-initial chip for unknown providers (`<div class="size-8 rounded-md bg-[--active-bg] text-[--active-text]">{initial}</div>`).
- Props: `provider: ChannelProvider | IntegrationType | string`, `size?: 'sm' | 'md'` (default `'md'`, 32×32; `'sm'` = 20×20).
- Logos as inline SVG strings in a sibling `providerLogos.ts` map (no external CDN, no image imports).
- ~80 LOC.

**`<ChannelConfigCard>`** *(new — `src/features/communication/components/ChannelConfigCard.tsx`)*
- Extracted from `ChannelsPage`'s inline card body. Renders the elevated card with provider header, default-star halo, status pill, last-tested chip, action buttons.
- Props: `config: ChannelConfigDto`, `canManage: boolean`, `onTest: () => void`, `onEdit: () => void`, `onSetDefault: () => void`, `onDelete: () => void`, `isTestPending: boolean`, `isSetDefaultPending: boolean`.
- ~110 LOC.

**`<IntegrationConfigCard>`** *(new — `src/features/communication/components/IntegrationConfigCard.tsx`)*
- Sibling to `<ChannelConfigCard>`. Same shape; no default-star prop.
- Props: `config: IntegrationConfigDto`, `canManage: boolean`, `onTest: () => void`, `onEdit: () => void`, `onDelete: () => void`, `isTestPending: boolean`.
- ~100 LOC.

**`<TemplateCategoryRail>`** *(new — `src/features/communication/components/TemplateCategoryRail.tsx`)*
- Renders the category list with active state, counts per category, "All categories" pseudo-row at bottom. Two visual variants: `'rail'` (sticky 200px column for `lg+`) and `'chips'` (horizontal pill row for `<lg`). Both variants share state ownership; the page picks the variant via a Tailwind responsive class on a wrapper.
- Props: `categories: Array<{ name: string; count: number }>`, `selectedCategory: string | undefined` (`undefined` = "All categories"), `onSelect: (category: string | undefined) => void`, `totalCount: number`, `variant?: 'rail' | 'chips'` (default `'rail'`).
- ~90 LOC (covers both variants in one file).

### 5.2 Reused components

- `<MetricCard>` (Phase 4 Billing) — for hero strips on DeliveryLog (4-card), Channels (3-card), Integrations (3-card).
- `<Table>`, `<EmptyState>`, `<PageHeader>`, `<Pagination>`, `<Badge>`, `<Card variant="elevated">`, `<ConfirmDialog>`, `<Spinner>`, `<Select>`, `<Input>`, `<Button>` — all unchanged, tokens swept where used inline.
- `<TemplateEditorDialog>`, `<TriggerRuleFormDialog>`, `<ChannelSetupDialog>`, `<IntegrationSetupDialog>`, `<NotificationPreferencesPanel>`, `<RequiredNotificationsManager>`, `<CommunicationDashboardWidget>` — token sweep only.

### 5.3 New hooks

- `useDeliveryStatusCounts(windowDays?: number)` — wraps `communicationApi.getDeliveryStatusCounts`. TanStack Query, default `windowDays = 7`. Pattern matches 5a's `useInboxStatusCounts` exactly.

No new hooks needed for Channels / Integrations / Templates (counts derived client-side from existing list queries).

## 6. Tokens, styling, J4 utilities

- All hardcoded primary shades swept across the 5 pages + 8 dialogs/components. Audit `bg-primary-{50..950}`, `text-primary-{50..950}`, `border-primary-{50..950}` with grep before commit.
- Drawer slide animation — uses Radix `data-state` attributes + Tailwind transition utilities. RTL: `rtl:translate-x-full` overrides on the closed state for right-side drawer.
- Provider logos — pure inline SVG, no CDN, no external images. 13 SVG strings in `providerLogos.ts`.
- DeliveryLog row leading-edge stripe — `border-inline-start: 3px solid var(--<token>)` per status. `border-inline-start` is RTL-safe (flips automatically).
- Age-tinted last-tested chip thresholds: `<1h` → emerald, `<24h` → muted, `<7d` → amber (inline `amber-*` tokens with comment justifying the absence of a `state-warn` semantic token), `>7d` → muted. If a `state-warn` token is added during this PR's plan stage, swap to it.
- Channel-sequence chip-arrow chain — inline `ChevronRight` icons between chips; `rtl:rotate-180` on the icon for RTL flip.
- Default-star glow — reuses existing `brand-halo` utility from `src/styles/index.css`.
- Hero strip "Last 7 days" label — small caption below the strip, `text-xs text-muted-foreground`.

## 7. Translation scope

EN + AR + KU inline. Estimated ~22 new keys:

- `communication.deliveryLog.statusCounts.{delivered,failed,pending,bounced}` (4)
- `communication.deliveryLog.window.last7Days` (1)
- `communication.deliveryLog.drawer.title` (1)
- `communication.channels.statusCounts.{active,configured,errored}` (3)
- `communication.integrations.statusCounts.{active,configured,errored}` (3)
- `communication.channels.lastTested.{justNow,today,thisWeek,older,never}` (5)
- `communication.templates.categoryCount` (1) *(rail count chip aria-label)*
- `communication.triggerRules.channelSequence.connector` (1) *(arrow chip aria-label)*
- `communication.providers.unknown` (1) *(fallback initial chip aria-label)*
- `communication.deliveryLog.drawer.attemptCount` (1)

`communication.templates.allCategories` already exists in the i18n bundle (today's chip-row uses it) — reuse, don't recreate.

Removed/reassigned: none. Existing `DeliveryDetailModal` translation keys are reused by `DeliveryDetailDrawer` (same body content); only the wrapper changes.

## 8. Backend permissions

No new permissions. The new endpoint reuses `Permissions.Communication.ViewDeliveryLog`, identical to the existing list endpoint. All other surfaces reuse existing `Communication.ViewTemplates` / `ManageTemplates` / `ManageChannels` / `ManageIntegrations` / `ManageTriggerRules` / `Resend`.

## 9. Testing & verification

- **Unit tests** — handler test for the new query handler (`GetDeliveryStatusCountsQueryHandlerTests`) covering tenant scoping, window clamping (1, 7, 90, 0→1, 91→90), bucket boundaries (Delivered / Failed / Pending+Queued+Sending / Bounced), and time-window edge cases (rows at the boundary).
- **Architecture tests** — none new.
- **Frontend lint + typecheck + build** — must pass before commit.
- **Live test in test app** — `_testJ4visual` test app gets the FE diff copy-pasted (per established cadence) and is exercised via Chrome DevTools MCP / Playwright at:
  - **DeliveryLog** — login as a tenant admin with mixed delivery statuses spanning 7 days; verify hero counts match table reality, row stripes render the right colors, drawer slides in correctly, list stays visible, click another row → drawer content swaps, resend button works from drawer, mobile fallback to bottom sheet on `<sm`.
  - **Templates** — login as a tenant admin with 5+ categories totaling 30+ templates; verify rail renders with counts, "All categories" pseudo-row at bottom, URL `?category=` persistence on refresh, mobile fallback to chip row on `<lg`, default category selection is alphabetical.
  - **Channels** — login as a tenant admin; verify hero counts match config reality, provider logos render for all 9 channel providers, default-star glow visible, age-tinted chip thresholds (test by manipulating `lastTestedAt` in DB), Error pill shows AlertCircle icon.
  - **Integrations** — login as a tenant admin; verify hero counts, provider logos for Slack/Telegram/Discord/MS Teams, no default-star (integrations don't have a default).
  - **TriggerRules** — verify channel-sequence chip-arrow chain renders, RTL flips arrows correctly, status badges use new `STATUS_BADGE_VARIANT` mappings.
- **No regression to existing features** — Phases 0–5a surfaces unchanged.

## 10. Branch, PR shape, commit cadence

- **Branch:** `fe/phase-5b-design` (already created off `origin/main` post-5a).
- **Commits:** land directly on the working branch (no per-task feature branches within the plan, matching Phase 0–5a cadence).
- **Final review pass:** `superpowers:code-reviewer` before push.
- **PR title:** `feat(fe): Phase 5b Communication — comms cluster (5 pages + side drawer + 4 new components)`.
- **PR body:** spec + plan links, deferred-list, BE additions flagged (one new query handler + endpoint), test app verification screenshots, and a callout that this is the second of three Phase 5 PRs (workflow shipped 5a / comms 5b / odds-and-ends 5c).

## 11. Open questions for the plan stage

- **Drawer keyboard nav (↑/↓ to step rows).** Stretch goal; drop if friction shows up during implementation. Not blocking the spec.
- **Provider logo licenses.** Verify that small-format SVG marks for Slack / Telegram / Discord / MS Teams are usable as product references. Most brand-asset pages permit this for small UI integrations; confirm during plan and adjust to text-initial fallback if any provider's terms forbid use.
- **`STATUS_BADGE_VARIANT` extensions.** Verify that `Configured` (Inactive), `Errored` (Error), `Bounced`, `Sending`, `Queued` all have correct mappings. Add what's missing.
- **`state-warn` semantic token.** No `state-warn` token exists today (also noted in 5a §11). The age-tinted chip needs an amber tone; for 5b, use inline `amber-*` Tailwind tokens with a comment justifying the exception. If a `state-warn` token is added during the plan stage (preferred — recurring need across 5a + 5b), swap to it.
- **DeliveryLog row stripe vs. row tint.** If the leading-edge stripe is too subtle in test-app QA, swap to a faint full-row tint (`bg-destructive/5` etc.) for failed/bounced rows. Decided live, not in spec.
- **Templates rail "Customized" sub-filter.** A future scope question: should the rail also show counts of `hasOverride === true` templates? Defer.

## 12. After 5b ships

The remaining Phase 5 work, decomposed at the end of 5a:

- **Phase 5c** — Bundled odds-and-ends: Webhooks (3 pages), Import/Export (1 page + `ImportWizard` dialog), Comments-Activity slots (used inline across the app), Onboarding wizard.

Phase 5c gets its own brainstorm → spec → plan → execution cycle, branched off `origin/main` after 5b merges (Phase 0–5a cadence).
