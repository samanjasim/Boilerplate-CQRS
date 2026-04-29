# Phase 4 Billing — Commerce cluster, billing half (5 pages)

**Created:** 2026-04-29
**Branch:** `fe/post-phase-3` (will rename / branch off as work begins)
**Predecessors:** Phase 0 (foundation), Phase 1 (Identity), Phase 2 (Platform admin), Phase 3 (Data, shipped via PR #35).
**Roadmap reference:** [`2026-04-28-post-phase-2-status-and-roadmap.md`](2026-04-28-post-phase-2-status-and-roadmap.md) §5.

Phase 4 ships in two sequential PRs — **Billing first** (this spec, 5 pages), then **Products** (separate spec, 3 pages). The split is by domain rather than by audience: billing's 5 pages share `Usage` / `SubscriptionPlan` / `PaymentRecord` data shapes and want consistent visual review; products is genuinely a separate concern.

---

## 1. Goal

Bring the 5 billing pages onto the J4 Spectrum visual language. The cluster splits along the same hero-vs-polish axis Phase 3 established:

- **Hero pages** (BillingPage, SubscriptionDetailPage, SubscriptionsPage) — open with a stat strip earned by the data: `Usage` ratios for tenant-scoped views, status-distribution counts for the cross-tenant admin view.
- **Polish pages** (BillingPlansPage, PricingPage) — keep their existing card-grid structure; no new hero. Apply J4 tokens (gradient text on prices, glass surfaces, copper highlights on the popular tier and on subscriber-count emphasis), fix any hardcoded primary shades, ensure RTL.

Why this split: BillingPlansPage's per-plan cards already function as their own mini-heroes (each card shows `subscriberCount` and pricing prominently); a page-level hero would compete. PricingPage's gradient hero already exists and the layout question is "polish, not new pattern" (see §3.5). Adding heroes everywhere would dilute, not strengthen.

## 2. Non-goals

Listed up front so they don't get relitigated:

- **MRR / revenue hero on SubscriptionsPage.** Tempting, but the data model has `lockedMonthlyPrice` per subscription with `currency` per subscription, no canonical platform currency. Surfacing MRR implies accuracy; getting it right means a currency-conversion policy decision that's a product call, not a redesign call. Status-distribution gives the *operational* signal (`past-due`) without needing to sum money. Revisit when product wants it.
- **PricingPage comparison table.** Card grid is the dominant pattern at this plan count (3–4 plans). Switching to a features-as-rows table is an IA decision, not a redesign decision.
- **BillingPlansPage page-level hero.** The cards already serve that role.
- **Proration UI, payment-method management, invoice download.** New BE features, not redesign work.
- **Products redesign.** Separate spec / separate PR.
- **Translation deferral.** EN + AR + KU land inline with each component change. Phase 2 deferred and we paid the cost during the post-merge RTL pass; Phase 3 confirmed the inline approach works.

## 3. Pages

### 3.1 BillingPage *(tenant — `/billing`, perm `Billing.View`)*

Today: plan card + 5 `UsageBar` rows + payment history table + Change Plan / Cancel buttons. ~220 LOC.

Redesign:

- **Hero row** — two side-by-side glass containers:
  - **Left ("Your plan"):** plan name as gradient-text headline, status pill (uses existing `STATUS_BADGE_VARIANT` mapping), period dates eyebrow (`Apr 1 – Apr 30, 2026`), locked price next to plan name (`$29 / month` for monthly billing interval). `Change Plan` button inline (right edge of card). Auto-renew flag rendered as a small caption when `false` (`Auto-renew off`).
  - **Right (3-card `MetricCard` strip):** Users (`current / max`) / Storage (`used / max`, formatted via `formatFileSize`) / Webhooks (`current / max`). Each card includes a thin progress bar — `bg-primary` until 90%, `bg-destructive` past 90%. Reuses the progress pattern from Files' StorageHeroStrip.
  - **Cancel Subscription** stays as today (danger button, hidden for free plans, confirm dialog unchanged).
- **Payment history table** — wrap in `surface-glass` container, status badges via existing token map. No structural change.

Hero data is already loaded by `useSubscription()` + `useUsage()`. No new BE.

### 3.2 SubscriptionDetailPage *(platform admin — `/billing/subscriptions/:tenantId`, perm `Billing.ManageTenantSubscriptions`)*

Today: page header + plan card + usage grid + payment history + change-plan dialog. ~258 LOC.

Redesign: **structurally identical to BillingPage's hero**, sourcing from `useTenantSubscription(tenantId)` + `useTenantUsage(tenantId)` + `useTenantPayments(tenantId, ...)` instead of the tenant-scoped hooks. The shared structure is the point — reviewers see the same pattern twice and consistency is exactly what platform admins want when investigating across tenants.

Adjustments:

- Page header gets the tenant breadcrumb (`Subscriptions › Acme Corporation`) and a tenant-name eyebrow on the hero's left card.
- The change-plan dialog (existing) is unchanged.

No new BE.

### 3.3 SubscriptionsPage *(platform admin — `/billing/subscriptions`, perm `Billing.ManageTenantSubscriptions`)*

Today: search + filterable table (tenant, plan, status, usage ratios, payment status) + pagination. ~163 LOC.

Redesign:

- **Status-distribution hero:** 3-card `MetricCard` strip — `Active` / `Trialing` / `Past-due`. **Past-due collapses when zero** (same rule as Reports' Failed in Phase 3); the slot disappears and the other two cards share the row at 50/50. Active uses `tone="active"` (the default tinted treatment); Past-due uses `tone="destructive"` when shown.
- Counts come from a **new BE endpoint** (see §4).
- **Table polish:** wrap in `surface-glass` container, status pills via token map, payment-status pill column polished. No structural change. Inline plan selector (`InlinePlanSelector`) unchanged.

### 3.4 BillingPlansPage *(platform admin — `/billing/plans`, perm `Billing.ViewPlans`)*

Today: plan card grid (`sm:2 / lg:3`) — each card has plan name + slug badge + price + subscriber count + feature highlights + Edit/Resync/Deactivate. ~216 LOC.

Polish only:

- `<Card>` becomes `variant="glass"`.
- Price line uses gradient-text on the figure.
- `subscriberCount` becomes `tabular-nums` and gets a small "subscribers" eyebrow above it.
- Feature highlight bullets get copper checkmarks (`text-primary`).
- Action buttons (Edit / Resync / Deactivate) reviewed for J4 tokens — no hardcoded primary shades.
- "Create Plan" button uses `btn-primary-gradient`.

No new hero, no new structure.

### 3.5 PricingPage *(public/auth — `/pricing`, no permission guard)*

Today: gradient hero with app name/logo, monthly/annual toggle, 4-card grid (`sm:2 / lg:4`), per-card "Get Started" / "Upgrade" / "Current Plan" CTA. ~228 LOC.

Polish:

- Gradient hero — tighten typography, use `btn-primary-gradient` on the top-of-page CTAs.
- Monthly/annual toggle becomes a **segmented pill control** matching the pattern Phase 3 used on the Notifications filter (`pill-active` for the active segment, `state-hover` for the inactive). Annual segment keeps its "save 20%" badge.
- Plan cards:
  - `<Card variant="glass">`.
  - Price headline as gradient-text figure (`text-4xl font-semibold tabular-nums gradient-text`).
  - The "popular" plan (`isPopular` flag if present, else first paid tier) gets a `glow-primary-md` halo and `lg:scale-105` transform with a `transition-all` for the toggle interaction.
  - Feature list bullets — copper check icons, `text-primary` tint.
  - Trial-days badge becomes an outline pill (`border border-primary/40 text-primary bg-primary/5`).
  - CTA buttons use semantic variants: `default` for non-current paid tiers, `outline` for free tier, `ghost` (disabled) for current plan.
- The "v1.0 · production-grade" eyebrow stays.

Public route — RTL must work for unauthenticated visitors who land here from the landing page's pricing link.

## 4. Backend

The redesign is FE-only with one addition:

- **`GET /api/v1/billing/subscriptions/status-counts`** — returns `{ trialing, active, pastDue, canceled, expired }`. Mirrors the `GetReportStatusCountsQuery` pattern from Phase 3 exactly:
  - Query: `GetSubscriptionStatusCountsQuery`
  - DTO: `SubscriptionStatusCountsDto(int Trialing, int Active, int PastDue, int Canceled, int Expired)`
  - Handler: `GROUP BY Status` over the `TenantSubscription` table, converted to the DTO.
  - Controller: `GET /status-counts` action under the existing subscriptions controller (or wherever the platform `useAllSubscriptions` query is served from — confirm during plan).
  - Authorized via the same policy as the list (`Billing.ManageTenantSubscriptions`).
  - Tenant filter: this query intentionally crosses tenants (it's the platform-admin aggregate), so it uses `.IgnoreQueryFilters()` if a global filter applies, or the existing platform-admin pathway.

Two of the five returned fields (`canceled`, `expired`) aren't shown in the hero today but are returned for cheap future flex — the cost of returning all five vs three is zero.

## 5. Translations

All new keys land in **all three locales (EN, AR, KU)** in the same commit as the component change.

Anticipated new keys (canonical EN; AR + KU translated alongside):

```
billing:
  hero:
    yourPlan: "Your plan"
    period: "{{start}} – {{end}}"
    autoRenewOff: "Auto-renew off"
    perMonth: "/ month"
    perYear: "/ year"
  usage:
    users: "Users"
    storage: "Storage"
    webhooks: "Webhooks"
    overLimit: "Over limit"
  subscriptions:
    statusHero:
      active: "Active"
      activeEyebrow: "subscriptions"
      trialing: "Trialing"
      trialingEyebrow: "in trial"
      pastDue: "Past-due"
      pastDueEyebrow: "needs attention"
  plans:
    subscribersEyebrow: "subscribers"
pricing:
  toggle:
    monthly: "Monthly"
    annual: "Annual"
    saveBadge: "save 20%"
  card:
    popular: "Most popular"
    trialDays: "{{count}}-day trial"
    currentPlan: "Current plan"
    getStarted: "Get started"
    upgrade: "Upgrade"
```

Final paths and copy may shift slightly during implementation; the rule is "EN + AR + KU together, in the same commit as the component change".

## 6. Verification

The Phase 2/3 testing routine still applies. For each page:

- `npm run build` clean.
- `npm run lint` clean.
- Live test in `_testJ4visual` (FE on 3100 / BE on 5100). Source-edit + file-copy for FE-only changes; regenerate test app only when the BE endpoint is added.
- **RTL pass (Arabic)** — every Phase 4 billing page exercised in AR. Specifically:
  - PricingPage hero mirrors correctly; monthly/annual segmented control flips direction; "popular" plan halo doesn't break.
  - BillingPage / SubscriptionDetailPage hero progress bars don't reverse direction (progress fill should still flow start → end, not right-to-left fill on a left-to-right semantic; use logical CSS).
  - Gradient-text figures stay Latin digits (i18next default behaviour).
- **Permission matrix:**
  - **Super-admin**: BillingPage redirects (only `Billing.View` perm — super-admin without a tenant has no own subscription); BillingPlansPage / SubscriptionsPage / SubscriptionDetailPage all visible; PricingPage visible.
  - **Tenant admin** (e.g., `acme.admin@acme.com`): BillingPage shows their tenant's subscription + usage; PricingPage visible; the three platform-admin pages 403/redirect.
  - **Regular user**: only PricingPage visible (public).
  - **Unauthenticated visitor**: PricingPage visible; auth-only CTAs route to `/register-tenant` / `/login`.
- **Phase 1/2/3 visual regression** — spot-check Identity (Users, Roles, Tenants, Profile), Platform admin (Audit Logs, Feature Flags, API Keys, Settings), and Data (Files, Reports, Notifications) pages to confirm nothing broke.

## 7. Rollout

One PR, five pages. ~1100–1300 LOC. Subagent-driven execution per the Phase 2/3 cadence with a review checkpoint after each page lands locally. PR title: `feat(fe): Phase 4 redesign — Billing cluster (5 pages)`. Body includes spec + plan links, deferred-list, and the BE addition flagged.

The Products PR (3 pages, separate spec) follows once Billing ships.

## 8. Open questions for the plan stage

- **Subscriptions status-counts endpoint location.** Read the existing platform-admin subscriptions controller at the start of the SubscriptionsPage task and confirm where the new action belongs. Both `BillingController` and a possible `SubscriptionsController` are spec-compatible.
- **`isPopular` flag on `SubscriptionPlan`.** Check whether the existing type carries this flag. If not, the "popular" treatment falls back to "first non-free tier" or "highlight the cheapest paid tier" — a small heuristic decided during the PricingPage task. No BE change either way.
- **`InlinePlanSelector` reuse.** SubscriptionsPage uses an inline plan selector in the table today. Confirm during the SubscriptionsPage task that the existing component still composes cleanly inside the polished glass-table treatment, or whether it needs a small token sweep.
