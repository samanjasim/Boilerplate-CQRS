# Phase 4 Products — Commerce cluster, products half (3 pages + 2 ancillary)

**Created:** 2026-04-29
**Branch:** `fe/phase-4-design` (off latest `origin/main` — Phase 4 Billing PR #37 already merged)
**Predecessors:** Phase 0 (foundation), Phase 1 (Identity), Phase 2 (Platform admin), Phase 3 (Data, PR #35), Phase 4 Billing (PR #37).
**Roadmap reference:** [`2026-04-28-post-phase-2-status-and-roadmap.md`](2026-04-28-post-phase-2-status-and-roadmap.md) §5; [`2026-04-29-redesign-phase-4-billing-design.md`](2026-04-29-redesign-phase-4-billing-design.md) §1 — "Phase 4 ships in two sequential PRs — Billing first, then Products."

This is the second half of Phase 4. Billing shipped 5 pages onto the J4 Spectrum visual language; Products brings the remaining commerce-cluster surfaces onto it. After this PR, the only "pre-J4" feature areas remaining are workflow & comms (Phase 5), AI (Phase 6), and mobile (Phase 7).

---

## 1. Goal

Bring the 3 product pages and the 2 ancillary product surfaces onto J4 Spectrum tokens, with one structural change (asymmetric 2-column detail layout) earned by the data shape. Pure polish posture — no new functionality.

The cluster splits along the same hero-vs-polish axis Phase 3 + Phase 4 Billing established:

- **Hero page** (`ProductsListPage`) — opens with a 3-card status-distribution strip (`Drafts` / `Active` / `Archived`) with collapse-when-zero behaviour. Mirrors Phase 3 Reports and Phase 4 Billing's `SubscriptionsPage`.
- **Restructure page** (`ProductDetailPage`) — moves from a single-column 4-stacked-cards form to an asymmetric 2-column layout (image+identity panel on the left, form on the right). Image is the most identifying attribute of a product; today it sits as the 4th card at the bottom of the page. The asymmetric layout is earned by that.
- **Polish page** (`ProductCreatePage`) — keeps its single-column form. Token sweep only. No image yet (image upload is post-create), so a left column would be empty — the asymmetric layout is *earned* on detail by the image, and create doesn't earn it.
- **Ancillary surfaces** — `ProductsDashboardCard` (replace custom card with shared `StatCard`, fixes a hardcoded-orange FE-rule violation) and `TenantProductsTab` (table token sweep).

## 2. Non-goals

Locked in at brainstorm; listed up front so they don't get relitigated:

- **No new functionality.** No bulk publish/archive selection, no list-view grid toggle, no duplicate-product action, no inline status edit on the list page, no image cropping, no rich-text description editor.
- **No catalog-value (currency-aggregate) hero metric.** Same multi-currency rationale as Billing's MRR non-goal — `Product.Currency` is per-product (`IQD` / `USD`), so a "total catalog value" implies a currency-conversion policy decision that's a product call, not a redesign call.
- **No wizard / dialog conversion of the create flow.** The dedicated `/products/new` route stays. Converting create to a `ProductFormDialog` (matching Phase 4 Billing's `PlanFormDialog`) is a structural shift that deserves its own brainstorm.
- **No `<Tabs>` on the detail page.** The asymmetric 2-col is the structural change. Splitting view/edit/timeline across tabs is a separate IA decision.
- **No mobile-specific reflow.** Single-column stack on `<lg` is the mobile treatment for the 2-col detail page.
- **No translation deferral.** EN + AR + KU land inline with each component change. Phase 2 deferred and we paid the cost during the post-merge RTL pass; Phase 3 + Phase 4 Billing confirmed inline works.

## 3. Pages

### 3.1 ProductsListPage *(tenant + platform admin — `/products`, perm `Products.View`)*

Today: `<PageHeader>` + `<ListToolbar>` (search + status filter + tenant filter for SuperAdmin) + table (Name with thumbnail / Tenant for SuperAdmin / Slug / Price / Status / Created) + pagination + `<EmptyState>`. ~165 LOC.

Redesign:

- **3-card status hero** above the toolbar, using the shared `<MetricCard>` (the same component Phase 3 + Phase 4 Billing used for their status strips). Cards: `Drafts` / `Active` / `Archived`. Tone mapping (from `MetricCard`'s `'default' | 'active' | 'destructive'` enum):
  - `Drafts` — `tone="default"` (neutral; actionable but not urgent — these need someone to either publish or delete them).
  - `Active` — `tone="active"` (the page's hero metric; copper-tinted treatment).
  - `Archived` — `tone="default"` (neutral; visually de-emphasized via lower count + label, not via tone — there's no "muted" tone on `MetricCard` and Archived isn't destructive).
- **Collapse-when-zero rule** (Phase 3 / Phase 4 Billing pattern):
  - Each card with a count of zero collapses out of the row; surviving cards re-flow.
  - When all three are zero (brand-new tenant, no products yet), the hero hides entirely and the page falls through to the existing `<EmptyState>` ("No products found"). No empty-hero rendering.
- **Counts come from a new BE endpoint** (see §4) — `GET /api/v1/products/status-counts?tenantId={{tenantId}}`. Same shape as the existing `useSubscriptionStatusCounts` hook from Phase 4 Billing: a single TanStack Query call alongside the list query, keyed on the same tenant filter so SuperAdmin's tenant-filter selection updates the hero. `tenantId` is optional and only meaningful for SuperAdmin; tenant users stay scoped by the module DbContext query filter.
- **Toolbar / table**: keep the current shape (search, status `<Select>`, tenant `<Select>` for SuperAdmin). Ensure the `<Table>` renders inside the shared `surface-glass` rounded shell; the current shared `Table` primitive already owns that wrapper, so do not nest an extra glass container unless visual QA proves it is needed. Status pills go via `STATUS_BADGE_VARIANT`. SuperAdmin's tenant cell becomes a `text-[var(--tinted-fg)]` chip (matching Phase 2 Platform admin treatment).
- **Pagination + EmptyState** are already shared components — leave them, they're already on-pattern.

### 3.2 ProductDetailPage *(tenant + platform admin — `/products/:id`, perm `Products.View`)*

Today: `<PageHeader>` (title + slug subtitle + status badge + Publish/Archive actions) + a single-column form with 4 stacked cards (Tenant for SuperAdmin / Details / Pricing / Image) + 2 `<Slot>` extensions below the form. `max-w-2xl`. ~340 LOC.

Redesign — asymmetric 2-column layout on `lg+`, single-column stack on `<lg`.

#### Layout invariants

These are explicit design constraints that the implementation must honour. Calling them out so the code-review pass has unambiguous criteria:

1. **Right column drives page height.** The left column is `align-self: start` and `min-height: 0`, so it never stretches past the right column's natural content height. This avoids the "tall whitespace under the form" failure mode on lg+ screens with short forms.
2. **Sticky save footer lives inside the right column**, not at viewport bottom. The footer is `position: sticky; bottom: 0` within the column's scroll context, so the form can scroll independently and the save button stays visible without obscuring left-column content.
3. **Save footer reveals on dirty state.** Clean form → footer hidden (no permanent button row). Dirty form → footer slides in with `Save changes` (default variant) + `Cancel` (ghost variant) and an "Unsaved changes" eyebrow. Cancel resets the form to the last-saved state.
4. **`<lg` breakpoint** stacks the columns vertically: image+identity panel first, form second. The save footer behaviour is identical in both layouts.

#### Left column (~40% on lg+)

The "identity" panel, in this vertical order:

- **Large image preview** — square aspect (1:1), ~280px on lg+. When `imageFileId` set, render the file via `useFileUrl`; when unset, render a copper-tinted glass tile with a `<Package>` icon and the copy `products.noImage` ("No image uploaded yet") underneath. The tile has the same rounded-2xl + glass treatment as Phase 0/Phase 4 hero cards.
- **Price headline** — `gradient-text` figure on the formatted price + currency (e.g., `$29.99 USD` or `15,000 IQD`). `text-3xl`, `tabular-nums`, `font-display`. Sits directly under the image with comfortable spacing.
- **Slug eyebrow** — monospace, `text-xs`, `text-muted-foreground`, `tracking-tight`. Below the price.
- **Status pill** — via `STATUS_BADGE_VARIANT` (already used in `<PageHeader>`; this is a second instance for the panel context).
- **Tenant chip — SuperAdmin only.** Small copper chip rendering `Tenant: {{tenantName}}`. Below the chip: `Reassign tenant…` ghost button → opens `TenantReassignDialog` (new, ~80 LOC). Tenant users never see the chip (they only have one tenant — redundant).
- **Action row at the bottom of the panel:**
  - `Upload image` outline button (when `canEdit`); kept as a hidden `<input type="file">` triggered by the button — same handler as today.
  - **Conditional primary action**:
    - `product.status === 'Draft'` + `canEdit` → `Publish` button (`btn-primary-gradient`, `<Send>` icon).
    - `product.status === 'Active'` + `canEdit` → `Archive` button (`outline`, `<Archive>` icon, opens the existing `<ConfirmDialog>`).
    - `product.status === 'Archived'` → no primary action (terminal state).

The Publish/Archive actions move out of `<PageHeader>` and into the left panel. Rationale: the panel is the "what is this product" surface; status transitions are property-of-the-product actions, not page-chrome actions. The `<PageHeader>` keeps the title + breadcrumbs + status badge.

#### Right column (~60% on lg+)

The form, in this vertical order:

- **Section 1 — Details** (`<Card variant="glass">`): Name (`<Input>`) + Description (`<Textarea>`, 3 rows). Form errors below each field.
- **Section 2 — Pricing** (`<Card variant="glass">`): Price + Currency in a 2-column inner grid. Price is `type="number"` `step="0.01"`. Currency is `<Select>` over `['IQD', 'USD']` (unchanged from today).
- **Sticky save footer** — pinned to the bottom of the column's scroll context. Hidden when form is clean; visible when dirty. Layout: `Unsaved changes` eyebrow on the start, `Cancel` ghost button + `Save changes` primary button on the end.

Notably absent: the **Tenant card** (moved to the left panel as a chip + `TenantReassignDialog`) and the **Image card** (the upload affordance moved to the left panel below the preview).

#### `TenantReassignDialog` *(new, ~80 LOC, lives in `features/products/components/`)*

Triggered by the `Reassign tenant…` ghost button on the left panel.

- `<Dialog>` shell, glass variant.
- Title: `products.detail.reassignTitle` ("Move product to a different tenant").
- Body:
  - Description: `products.detail.reassignDescription` — `{{productName}} will move to {{newTenant}} and become invisible to {{oldTenant}} immediately. This is reversible.` (i18next interpolation; resolved with current and selected tenant names).
  - `<Select>` over the same `useTenants({ pageSize: 100 })` query the existing form uses. Pre-populated with the current `product.tenantId`. Disabled when no change.
- Actions: `Cancel` (ghost, closes dialog) + `Move product` (primary, fires `useUpdateProduct` with the last-saved product fields plus `tenantId: selectedTenantId`, then closes on success). The dialog must not silently persist dirty edits from the right-column form.

The dialog reuses the existing `useUpdateProduct` mutation — no new BE. The reason this is a dialog rather than an inline `<Select>` is restraint: SuperAdmin reassigning a product across tenants is rare and consequential, and the dialog turns it into an explicit decision rather than a casual edit.

#### Below the 2-col block, full width

The two existing extensions stay where they are — they're entity-scoped extension surfaces, not edit fields, and want full width:

```tsx
<Slot id="entity-detail-workflow" props={{ entityType: 'Product', entityId: product.id }} />
<Slot id="entity-detail-timeline" props={{ entityType: 'Product', entityId: product.id, tenantId: product.tenantId }} />
```

### 3.3 ProductCreatePage *(tenant + platform admin — `/products/new`, perm `Products.Create`)*

Today: `<PageHeader>` + single-column form with 3 stacked cards (Tenant for SuperAdmin / Details with slug `Generate` button / Pricing) + Cancel/Create actions. `max-w-2xl`. ~195 LOC.

Polish only:

- All `<Card>`s become `<Card variant="glass">`.
- `<PageHeader>` already provides the gradient-text title via the shared component — no extra work.
- Slug `Generate` button: `variant="outline"` already gets the copper-tinted hover via the established design system — verify; no new work.
- Form structure unchanged: Tenant (SuperAdmin) → Details (Name + Slug + Description) → Pricing (Price + Currency).
- No image upload on create — the existing implicit "upload after creating" behaviour is fine. Optionally surface this as a faint hint below the form: `products.uploadHint` ("You can add an image after creating the product"). One-line `<p class="text-xs text-muted-foreground">`.

The visual result is "the create page looks more like a Phase 2 form page than a Phase 4 product page." That's intentional — detail is the long-lived surface that earns the cinematic treatment; create is a 30-second utility.

## 4. Ancillary surfaces

### 4.1 ProductsDashboardCard

Today (~30 LOC, `features/products/components/ProductsDashboardCard.tsx`): a custom `<Card>` with a hardcoded orange tile (`bg-orange-500/10 text-orange-600`), a `<Package>` icon, the active-product count, and a link to the list page.

The hardcoded orange directly violates the [`CLAUDE.md` § "Theme System"](../../CLAUDE.md) rule:

> Never hardcode primary color shades (`primary-600`, `primary-50`, etc.) in components. Use `bg-primary`, `text-primary`, or semantic tokens.

Fix: **replace the custom card with the shared `StatCard`** — the same component the rest of `DashboardPage` uses (`Users`, `Active Roles`, `Total Roles`, `Platform Status`).

```tsx
<StatCard
  icon={Package}
  label={t('dashboard.activeProducts')}
  value={totalActive}
  tone="copper"
  // 'copper' chosen because there's no other product-related card on the dashboard,
  // so 'copper' (the brand primary) reads cleanly as the products-feature accent.
/>
```

The card lands inside the `<Slot id="dashboard-cards">` extension point that `DashboardPage` already exposes after its 4 built-in stat cards. The Products feature module is the registered renderer for that slot — no changes to the dashboard itself.

Drops:

- The hardcoded orange tile.
- The `hover-lift` custom class (StatCard has its own hover treatment).
- The custom `<Card>` + `<CardContent>` layout (StatCard handles all of it).

### 4.2 TenantProductsTab

Today (~88 LOC, `features/products/components/TenantProductsTab.tsx`): a small embedded view rendered inside `TenantDetailPage`'s tabs. Has its own `<h3>Products</h3>` heading, a basic `<Table>`, and `<Pagination>`.

Polish:

- Wrap the `<Table>` in a `surface-glass` container.
- Status pills via `STATUS_BADGE_VARIANT` (currently uses an inline `variant === 'Active' ? 'default' : ...` ternary — replace with the shared map).
- **Remove the local `<h3>Products</h3>` heading** — `TenantDetailPage`'s tab UI already gives the active tab its own header context, so the heading is redundant.
- `Pagination` is already shared — leave as is.
- Empty state already uses `<EmptyState>` — leave as is.

No structural change.

## 5. Backend

One addition, mirrors `GetSubscriptionStatusCountsQuery` (Phase 4 Billing) which itself mirrors `GetReportStatusCountsQuery` (Phase 3):

- **`GET /api/v1/products/status-counts?tenantId={{tenantId}}`** → `ProductStatusCountsDto(int Draft, int Active, int Archived)`
  - Lives in `boilerplateBE/src/modules/Starter.Module.Products/`.
  - Query: `Application/Queries/GetProductStatusCounts/GetProductStatusCountsQuery.cs` (sealed record, `IRequest<Result<ProductStatusCountsDto>>`).
  - Handler: `Application/Queries/GetProductStatusCounts/GetProductStatusCountsQueryHandler.cs` — `GROUP BY Status` over `Products` `DbSet`, projects to the DTO. Reads via the module's existing `ProductsDbContext`.
  - DTO: `Application/DTOs/ProductStatusCountsDto.cs` (sealed record `(int Draft, int Active, int Archived)`).
  - Controller: new `[HttpGet("status-counts")]` action on the existing `ProductsController`, authorized via `[Authorize(Policy = ProductPermissions.View)]` (same policy as the list), with optional `[FromQuery] Guid? tenantId = null`.
  - **Tenant filter**: tenant users are scoped via the module DbContext's existing global query filter and any query-string `tenantId` is redundant. SuperAdmin (`TenantId == null`) sees the cross-tenant aggregate by default; when `tenantId` is supplied, the handler adds `Where(p => p.TenantId == tenantId.Value)` so the hero matches the list's tenant filter.

This is the only BE change.

## 6. Frontend additions

- **`useProductStatusCounts` hook** — `features/products/api/products.queries.ts`. Mirrors `useSubscriptionStatusCounts` from Phase 4 Billing, with optional params. Query key includes the tenant filter so SuperAdmin's tenant-filter `<Select>` invalidates correctly.
- **`TenantReassignDialog` component** — `features/products/components/TenantReassignDialog.tsx`, ~80 LOC. Reuses the existing `useUpdateProduct` mutation and `useTenants` query.
- **No new routes.** All redesign happens on the existing `/products`, `/products/new`, `/products/:id` routes.

## 7. Translations

EN + AR + KU together, in the same commit as the component change.

The Products feature currently leans on `t(key, fallback)` for many visible strings. This phase should add real locale entries for both the new copy below and the existing visible product-page copy touched by the redesign (`title`, `subtitle`, form labels, status labels, empty states, archive/publish/upload strings, validation-facing labels where they are rendered). After this PR, the touched product surfaces should not depend on English fallbacks for normal UI text.

Anticipated new keys (canonical EN; AR + KU translated alongside). Final paths and copy may shift slightly during implementation; the rule is "EN + AR + KU together":

```yaml
products:
  hero:
    drafts: "Drafts"
    draftsEyebrow: "needs publish"
    active: "Active"
    activeEyebrow: "in catalog"
    archived: "Archived"
    archivedEyebrow: "retired"
  detail:
    reassignTenant: "Reassign tenant…"
    reassignTitle: "Move product to a different tenant"
    reassignDescription: "{{productName}} will move to {{newTenant}} and become invisible to {{oldTenant}} immediately. This is reversible."
    reassignConfirm: "Move product"
    reassignCancel: "Cancel"
    saveFooter: "Unsaved changes"
  noImage: "No image uploaded yet"
  uploadHint: "You can add an image after creating the product"
```

## 8. Verification

Same routine as Phase 4 Billing.

- `npm run build` clean.
- `npm run lint` clean.
- `dotnet build` clean (one new query + handler + DTO + controller action).
- Live test in `_testJ4visual` (FE on 3100 / BE on 5100). Source-edit + file-copy for FE-only changes; regenerate test app only when the BE endpoint is added.
- **RTL pass (Arabic)** — all three pages exercised in AR. Specifically:
  - **`ProductDetailPage` 2-column flips correctly**: image+identity panel ends up on the right, form on the left in RTL. The sticky save footer respects RTL ordering (start/end based on logical CSS, not left/right).
  - Status hero collapse-when-zero behaves identically in RTL.
  - Gradient-text price stays Latin digits (i18next default).
- **Permission matrix:**
  - **SuperAdmin**: list shows cross-tenant products + tenant filter visible; detail shows the tenant chip + reassign action; create shows the tenant select.
  - **Tenant admin** (e.g., `acme.admin@acme.com`): list shows only their tenant's products; detail hides the tenant chip + reassign action; create hides the tenant select; can publish/archive their products.
  - **Tenant user without `Products.Update`**: detail page form fields disabled; image upload hidden; publish/archive hidden.
  - **User without `Products.View`**: 403/redirect on `/products` and `/products/:id`.
- **Phase 0–3 + Phase 4 Billing visual regression** — spot-check Identity (Users, Roles, Tenants, Profile), Platform admin (Audit Logs, Feature Flags, API Keys, Settings), Data (Files, Reports, Notifications), and Commerce/Billing (BillingPage, BillingPlansPage, PricingPage, SubscriptionsPage, SubscriptionDetailPage) pages to confirm nothing broke.

## 9. Rollout

One PR, 3 pages + 2 ancillary surfaces + 1 new dialog component, ~900–1100 LOC. Subagent-driven execution per the Phase 2/3/4-Billing cadence with a review checkpoint after each page lands locally.

PR title: `feat(fe): Phase 4 Products — Commerce cluster (3 pages + dashboard card)`.

Body includes spec + plan links, deferred-list, the BE addition flagged, and a brief callout that this completes the Phase 4 commerce-cluster pair (Billing + Products).

Branch: `fe/phase-4-design` (already created off `origin/main`).

## 10. Open questions for the plan stage

- **Module DbContext for the new query.** Confirmed during plan review: `Starter.Module.Products` uses `ProductsDbContext`. The handler should inject that context directly, matching existing Products query handlers.
- **Aspect ratio of the left-panel image preview.** Spec says square (1:1) at ~280px on lg+. If existing `Product` images skew strongly to non-square aspect ratios in seed/test data, switch to 16:9 or `aspect-square` with `object-contain`. Verify during the detail-page task and adjust if needed — single-line change.
- **Save footer animation.** Slide-in / fade-in / instant-toggle on dirty-state? Default to instant-toggle (no animation) for accessibility; revisit if the page feels jarring during user testing.
