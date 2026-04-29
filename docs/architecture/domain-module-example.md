# D2 — Minimal Domain Module Example

**Status:** Planned. Not yet started.
**Prerequisites:** All relocation work merged (commits `36a2cb5` through `43f3052`). Both `ModTestAll` and `ModTestNone` killer-test variants build cleanly. `AbstractionsPurityTests` pass with `Starter.Abstractions` at zero project references.
**Branch:** Continue on `feature/module-architecture` (or branch off it).

---

## 1. Why D2 exists

Phases 0–5 + the relocation refactor proved **three** things about the existing modules (Billing, Webhooks, ImportExport):

1. They can be removed individually and the build stays clean (killer test passes in both directions).
2. `Starter.Domain/` contains zero module-owned types — all entities, enums, errors, and intra-module events live inside each module's `Domain/` folder.
3. `Starter.Abstractions` has zero project references and cleanly survives `AbstractionsPurityTests` with the tightened rules.

**What it hasn't proved:** that a developer can **add** a brand-new domain module without touching any core code. Billing/Webhooks/ImportExport are all legacy modules that existed in a pre-modular form and were refactored in place. We've never done the "green field" exercise of adding a module from zero.

D2 is that exercise. Build a minimal domain module (e-commerce Products as the proposal, see §2), with the constraint: **every file lives under `src/modules/Starter.Module.Products/` and `src/features/products/`**. Zero files modified anywhere else in the repo.

If D2 succeeds on the first attempt, the architecture is done. If it fails — the developer has to touch a core file to make it work — whatever had to be touched is a leak that needs fixing before the boilerplate can honestly claim the modularity goal.

---

## 2. The example module — Products (minimal e-commerce)

### Why Products

- **Domain is instantly recognizable** to anyone reading the code. "An e-commerce store has products" is self-explanatory; no invented concept to learn.
- **Exercises every interaction pattern** the architecture supports:
  - Pattern 1 (capability contract, in `Starter.Abstractions.Capabilities`): quota check (`IQuotaChecker`), webhook dispatch on product created (`IWebhookPublisher`) — these have Null Object fallbacks so core can call them whether or not the providing module is installed
  - Core application services (always present, in `Starter.Application.Common.Interfaces`): file upload for product images (`IFileService`), notification on low stock (`INotificationService`) — these are not capability contracts; they're core infrastructure that every deployment includes
  - Pattern 2 (integration event via MassTransit `IConsumer<T>`): reacts to `TenantRegisteredEvent` by seeding a demo catalog for new tenants. Use MassTransit when the handler creates durable state that must eventually succeed (e.g., provisioning a subscription). For fire-and-forget side effects (e.g., dispatching a webhook notification), the existing codebase uses MediatR `INotificationHandler<T>` — see the Webhooks module's event handlers for that pattern
  - Pattern 3 (reader service): reads tenant name via `ITenantReader` for display in the admin list
  - Per-module DbContext with isolated migration history
  - Frontend slot registration (a "Products" tab on the tenant detail page OR a new top-level route — see §4)
  - `IUsageMetricCalculator` implementation for a new metric (`products`)
- **Small enough to fit in a session.** One aggregate root (`Product`), one enum (`ProductStatus`), one DbContext with one DbSet, five commands/queries, one controller, one frontend feature folder with a list + create page.
- **Leaves room for extension.** If you later want to add orders, cart, checkout, you add them as additional modules that depend on Products — which tests *module-to-module* capability calls, not just module-to-core.

### What's in scope

Minimum viable feature set for D2:

**Domain** (`src/modules/Starter.Module.Products/Domain/`):
- `Product` aggregate root — id, tenant id, name, slug, description, price, currency, status, image file id, audit fields
- `ProductStatus` enum — `Draft`, `Active`, `Archived`
- `ProductErrors` static class — `NotFound`, `SlugAlreadyExists`, `CannotArchiveDraft`, `QuotaExceeded(limit)`, `ProductsDisabled`
- `ProductCreatedEvent` — intra-module event (raised in the aggregate, consumed by nothing yet; exists as a future-proofing seam)

**Application** (`src/modules/Starter.Module.Products/Application/`):
- `Commands/CreateProduct/` — command + handler + validator + response DTO
- `Commands/UpdateProduct/` — command + handler + validator
- `Commands/ArchiveProduct/` — command + handler
- `Commands/UploadProductImage/` — command + handler (uses `IFileService`)
- `Queries/GetProducts/` — paginated list query + handler + validator (for sort/filter/search)
- `Queries/GetProductById/` — single-product query + handler
- `DTOs/ProductDto.cs`, `ProductMapper.cs` — standard mapper pattern
- `EventHandlers/SeedDemoCatalogOnTenantRegistered.cs` — `IConsumer<TenantRegisteredEvent>` that seeds a small demo catalog for new tenants. Manually idempotent (check for existing products before seeding).

**Infrastructure** (`src/modules/Starter.Module.Products/Infrastructure/`):
- `Persistence/ProductsDbContext.cs` — own DbContext implementing `IModuleDbContext`, own `__EFMigrationsHistory_Products` table
- `Configurations/ProductConfiguration.cs` — `IEntityTypeConfiguration<Product>`
- `Services/ProductsUsageMetricCalculator.cs` — implements `IUsageMetricCalculator` with `Metric = "products"`, counts non-archived products per tenant

**Module entry** (`src/modules/Starter.Module.Products/`):
- `ProductsModule.cs` — implements `IModule` with `ConfigureServices` (registers `ProductsDbContext`, `ProductsUsageMetricCalculator`), `GetPermissions`, `GetDefaultRolePermissions`, `MigrateAsync`, `SeedDataAsync` (no-op for now — real seeds happen per tenant via the event handler)
- `Constants/ProductPermissions.cs` — `View`, `Create`, `Update`, `Delete`
- `Controllers/ProductsController.cs` — inherits `Starter.Abstractions.Web.BaseApiController`, REST endpoints for the 6 commands/queries above

**Frontend** (`boilerplateFE/src/features/products/`):
- `index.ts` — exports `productsModule` object with `register()`. **Two slot registrations:**
  - `tenant-detail-tabs` → "Products" tab showing that tenant's products (platform admin view)
  - A new slot: `dashboard-cards` → a "Products" card showing count of active products (requires adding the slot id to `slot-map.ts` — that's the one place we have to touch a core file, and it's the bootstrap file the rename script already allowlists)
- `api/products.api.ts` + `api/products.queries.ts` — standard TanStack Query hook pattern
- `pages/ProductsListPage.tsx` — list with pagination, search, create button
- `pages/ProductCreatePage.tsx` — form with Zod schema + file upload for image
- `pages/ProductDetailPage.tsx` — edit form + archive button
- `components/TenantProductsTab.tsx` — the slot entry for `tenant-detail-tabs`
- `components/ProductsDashboardCard.tsx` — the slot entry for `dashboard-cards`
- `types/product.types.ts` — TypeScript types mirroring the backend DTOs

**Permissions**:
- `Products.View`, `Products.Create`, `Products.Update`, `Products.Delete` — module-owned constants in `Starter.Module.Products.Constants.ProductPermissions`
- Default role mappings: `SuperAdmin` + `Admin` → all four; `User` → `View` only

**Module catalog** (`modules.catalog.json`):
- Add a new entry: `products` with `backendModule: "Starter.Module.Products"`, `frontendFeature: "products"`, `configKey: "products"`, `required: false`

**Frontend bootstrap** (`boilerplateFE/src/config/modules.config.ts`):
- Add `products: true` to `activeModules`
- Add `import { productsModule } from '@/features/products'` + append to `enabledModules` array

### What's NOT in scope

- **No orders, cart, checkout, or inventory.** Those are separate modules (and each one would be its own D3/D4/D5 exercise).
- **No per-tenant product catalog UI** beyond the basic list/create/edit. No bulk operations, no variants, no categories, no tags. Keep it boring.
- **No image CDN.** Use the existing `IFileService` for image upload; the frontend just displays the file URL.
- **No Stripe integration.** The product has a price, but there's no checkout flow. That's a future module.
- **No SEO.** Products don't need a public-facing page yet.
- **No tests.** D2 is about proving the architecture; unit/integration tests for Products can be added later as a separate deliverable if the patterns are worth formalizing.

---

## 3. The killer test that D2 has to pass

D2 succeeds if and only if **every one of these is true** at the end:

1. **Zero files modified outside `src/modules/Starter.Module.Products/` and `src/features/products/`**, with exactly four allowed exceptions:
   - `boilerplateBE/Starter.sln` — one new `Project` entry for the module
   - `boilerplateBE/src/Starter.Api/Starter.Api.csproj` — one new `<ProjectReference>`
   - `modules.catalog.json` — one new entry
   - `boilerplateFE/src/config/modules.config.ts` — one new import + one `activeModules` flag + one `enabledModules` entry
   - `boilerplateFE/src/lib/extensions/slot-map.ts` — one new slot id (IF you add the `dashboard-cards` slot; if you reuse only existing slots, this is not needed)

   Any other file touched is a leak. If the doc you're reading has to change to explain why you touched something else, stop and treat it as a finding.

2. **`dotnet build`** — zero warnings, zero errors.

3. **`dotnet test --filter AbstractionsPurityTests`** — 2/2 pass. The relocation refactor's architectural invariants are preserved.

4. **`pwsh ./scripts/rename.ps1 -Name "ModTestAll" -OutputDir "c:/tmp"`** followed by build — succeeds, has 4 modules now instead of 3, Products appears in the generated solution.

5. **`pwsh ./scripts/rename.ps1 -Name "ModTestNone" -OutputDir "c:/tmp" -Modules "None"`** followed by build — succeeds, has 0 modules, and specifically the Products module is absent. No `using Starter.Module.Products.*` leaks into core because Products never existed in a core file.

6. **`pwsh ./scripts/rename.ps1 -Name "ModTestProducts" -OutputDir "c:/tmp" -Modules "products"`** followed by build — generates an app with ONLY the Products module enabled (Billing/Webhooks/ImportExport excluded). Build succeeds. This is the case that matters for POS / e-commerce-only deployments.

7. **Manual smoke test**: run the generated `ModTestAll` app. Register a new tenant → verify `SeedDemoCatalogOnTenantRegistered` fires and a demo catalog appears. Log in as the new tenant → create a product → upload an image → verify the image file shows in MinIO → verify the product list refreshes → archive a product → verify the status changes. Navigate to `/tenants/{id}` as superadmin → verify the new "Products" tab is visible.

8. **Quota integration**: temporarily set `products.max_count` feature flag to `2` → create 2 products → third create attempt fails with 422 + `ProductErrors.QuotaExceeded`. This exercises the `IQuotaChecker` capability indirectly.

If ALL eight succeed, D2 is done and the modularity claim is genuinely proven.

---

## 4. Open design questions to resolve at the start of D2

These should be decided in the first 10 minutes of the D2 session; don't wait until you're halfway through:

### Q1 — Where do Products routes live in the frontend?

Two options:

- **Option A: New top-level route** `/products` (sidebar entry, full page). Matches how Billing/Webhooks/ImportExport work today. Needs `activeModules.products && lazy(() => import(...))` in `routes.tsx` — same pattern as the 3 existing modules — which means `routes.tsx` is the 5th allowed exception to the "zero files outside the module" rule.
- **Option B: Slot-only.** Products has no top-level route; it's only accessible via the tenant detail page tab (`tenant-detail-tabs` slot) and a dashboard card (`dashboard-cards` slot). No `routes.tsx` change. Cleaner for the killer test, but a weird product experience — users expect a top-level "Products" nav item.

**Recommendation:** Option A. It matches the existing pattern and honestly exercises the full "module with its own routes" lifecycle. The `routes.tsx` touch is already an accepted modification pattern in `rename.ps1`.

### Q2 — Should Products define a new slot or reuse existing slots?

- **Reuse only**: contribute to `tenant-detail-tabs` (exists) and nothing else. No `slot-map.ts` change. Simplest.
- **Add `dashboard-cards` slot**: gives D2 the exercise of "adding a new slot from scratch", which Billing/Webhooks/ImportExport never did (they only use existing slots). This is architecturally more interesting — it proves the slot infrastructure supports module-driven slot additions.

**Recommendation:** Add the new slot. The whole point of D2 is to exercise capabilities that haven't been exercised yet. `slot-map.ts` is already a thin file; adding one typed slot id is a five-line change and is an allowed modification per §3 rule 1.

### Q3 — How does SeedDemoCatalogOnTenantRegistered handle the idempotency question?

The consumer runs in a background MassTransit scope. Two risks:

- **Risk 1**: it runs more than once for the same tenant (retry after crash, outbox redelivery). Must be idempotent.
- **Risk 2**: it runs before the tenant is actually visible in the Products context (the event fires via the core outbox; Products has its own DbContext with its own migration history).

**Resolution:** Follow the exact pattern from `CreateFreeTierSubscriptionOnTenantRegistered.cs` in the Billing module — manual idempotency check via `ProductsDbContext.Products.AnyAsync(p => p.TenantId == evt.TenantId)` before seeding. If any products exist for the tenant, skip with a debug log. The tenant visibility isn't a problem because Products doesn't read from `ApplicationDbContext`; it just writes new rows into its own DbContext keyed by `tenantId` from the event payload.

### Q4 — What does the demo catalog actually contain?

**Recommendation:** 3 example products, hardcoded in the event handler, each with no image (image upload is tested separately). Names like "Sample Product 1", "Sample Product 2", "Sample Product 3" with prices 9.99, 19.99, 29.99 USD. Status `Active`. This is just enough to show up in the list; the point isn't the data, it's that the seeding pipeline works.

Alternatively, skip the seed entirely — create an empty catalog and let the user create products manually. That's one less file to write and one less thing to test. If D2 is time-constrained, cut the seed handler and save it for a follow-up.

---

## 5. Suggested execution order

Build it in this order so each step leaves the system in a working state:

1. **Skeleton**: create the csproj, folder structure, `ProductsModule.cs`, `ProductPermissions.cs`, `ProductsDbContext.cs`. No entities yet. Wire up `Starter.sln` + `Starter.Api.csproj`. Verify `dotnet build` succeeds.
2. **Domain**: write `Product` aggregate root, `ProductStatus` enum, `ProductErrors` static class. Nothing uses them yet. Build.
3. **EF config + DbContext entity registration**: write `ProductConfiguration` (tenant filter, column types), register in `ProductsDbContext`. `dotnet ef migrations add InitProducts` against the module's own context (specify `--context ProductsDbContext`). Build.
4. **First command**: `CreateProductCommand` + handler + validator + DTO. Inject `ProductsDbContext` + `ICurrentUserService` + `IQuotaChecker` + `IWebhookPublisher`. Handler enforces quota via `IQuotaChecker.CheckAsync(tenantId, "products")`, creates the product, saves, dispatches `"product.created"` webhook. Build.
5. **First query**: `GetProductsQuery` with pagination, search, sort. Use the Billing module's `GetPlansQueryHandler` as a template. Build.
6. **Controller**: `ProductsController` with POST and GET endpoints. Permission attributes. Build.
7. **Usage metric calculator**: `ProductsUsageMetricCalculator` → register in `ProductsModule.ConfigureServices`. Build.
8. **First end-to-end smoke**: start the app, hit POST `/api/v1/products`, verify the row in PostgreSQL. Hit GET `/api/v1/products`, verify it comes back.
9. **Remaining commands/queries**: Update, Archive, UploadImage, GetById. Build + smoke each as you go.
10. **Event consumer**: `SeedDemoCatalogOnTenantRegistered` if doing the demo seed. Build.
11. **Frontend skeleton**: create `src/features/products/` with `index.ts` stub + empty `api/` `pages/` `components/`. Add to `modules.config.ts`. Add to `routes.tsx`. Build `npm run build`.
12. **Frontend list page**: `ProductsListPage` with TanStack Query integration. Sidebar nav entry. Smoke: navigate, see empty list.
13. **Frontend create page**: `ProductCreatePage` with Zod schema + react-hook-form + file upload. Smoke: create a product from the UI.
14. **Frontend detail + archive**: `ProductDetailPage`, archive button. Smoke.
15. **Frontend slot entries**: `TenantProductsTab`, `ProductsDashboardCard`. Add `dashboard-cards` slot to `slot-map.ts`. Smoke: see the tab on the tenant detail page, see the card on the dashboard.
16. **The killer test**: run all 8 checks from §3. If any fails, diagnose, fix, re-run until green.
17. **Cleanup test apps** per `test-cleanup` skill.
18. **Commit** (incremental commits as you go are fine — the whole sequence can be squashed later if desired).

---

## 6. Files and anchors you'll reference constantly

Read these first in the D2 session to orient:

**Architecture reference**:
- [architecture/system-design.md](./architecture/system-design.md) — project graph, folder layout, patterns
- [architecture/module-development.md](./architecture/module-development.md) — Section D (creating a new module) is the step-by-step; Section G (cookbook) has the one-liners
- [architecture/cross-module-communication.md](./architecture/cross-module-communication.md) — which pattern to use for each cross-module interaction

**Existing modules as templates**:
- `boilerplateBE/src/modules/Starter.Module.Billing/` — most complete template; has the full spread of entities, handlers, event consumers, webhook dispatch, DbContext, usage metric calculator, module class. Copy the skeleton, rename, strip what you don't need.
- `boilerplateBE/src/modules/Starter.Module.Webhooks/WebhooksModule.cs` — reference for the `MigrateAsync` implementation
- `boilerplateBE/src/modules/Starter.Module.Billing/Application/EventHandlers/CreateFreeTierSubscriptionOnTenantRegistered.cs` — reference for the `TenantRegisteredEvent` consumer pattern, including the manual idempotency check
- `boilerplateBE/src/modules/Starter.Module.Webhooks/Infrastructure/Services/WebhookUsageMetricCalculator.cs` — reference for `IUsageMetricCalculator` implementation

**Frontend templates**:
- `boilerplateFE/src/features/billing/index.ts` — reference `AppModule` shape + `registerSlot` usage
- `boilerplateFE/src/features/import-export/components/UsersImportButton.tsx` — reference slot-entry component shape
- `boilerplateFE/src/lib/extensions/slot-map.ts` — where to declare new slot ids
- `boilerplateFE/src/config/modules.config.ts` — where to add the new module import + flag + array entry

**Scaffolder**:
- `scripts/rename.ps1` — read the `-Modules` parameter handling (~line 216 onward) to understand what it does when a module is excluded. Your new module must match that pattern so `-Modules None` strips it cleanly.
- `modules.catalog.json` — root module catalog; add your entry matching the schema.

**Architecture rules**:
- `boilerplateBE/tests/Starter.Api.Tests/Architecture/AbstractionsPurityTests.cs` — you'll be running this periodically during D2. It should stay green throughout.
- `boilerplateBE/src/Starter.Abstractions/Capabilities/ICapability.cs` — the doc comment on this file is the authoritative statement of the Abstractions dependency rules.

---

## 7. Success criteria (copy this into the first commit message of D2)

```
D2 complete:
- Starter.Module.Products module added (Domain, Application, Infrastructure, Controllers, frontend feature)
- Zero files modified outside src/modules/Starter.Module.Products/ and src/features/products/
  except the 4-5 expected bootstrap files (sln, csproj, modules.json, modules.config.ts, slot-map.ts)
- dotnet build: 0/0
- dotnet test --filter AbstractionsPurityTests: 2/2 pass
- Killer tests via rename.ps1 (-Modules All, -Modules None, -Modules products): all 3 build + run clean
- Manual smoke: create/edit/archive/image upload/quota enforcement/tenant seeding all work
- modules.json: 4 entries (billing, webhooks, importExport, products)
```

---

## 8. What comes after D2

If D2 succeeds, the boilerplate has proved it genuinely supports "add a domain module without touching core". From there the roadmap branches:

- **D3 — Frontend cookbook**: document the frontend module patterns with the same depth as [architecture/cross-module-communication.md](./architecture/cross-module-communication.md) has for the backend. Slot contributions, capability hooks, route registration, permission gating. Products gives you concrete examples to lift from.
- **D4 — Testing strategy for modules**: per-module test project skeleton (none exist today), integration test fixture that spins up only the module's DbContext, mocking the capability contracts. Products is a good first candidate for this.
- **D5 — Packaging (deferred from the original refactor spec)**: turn Products (or Billing) into a NuGet package + npm package pair. This is the "distribute modules independently" goal. Only attempt this after D4 because testing isolation proves the module's surface area is stable.
- **D6 — CLI scaffold**: a `dotnet new` template or a `pwsh` script that scaffolds a new module from a template. Much easier to build after D2 has documented the exact file layout and bootstrap steps.

These are intentionally loose — revisit the roadmap after D2 lands.

---

## 9. Continuation checklist for a fresh chat session on a new laptop

If you're reading this from a fresh Claude Code session after switching laptops:

1. ✅ `git pull` on `feature/module-architecture` (should have everything through commit `43f3052` or later)
2. ✅ Read [getting-started.md](./getting-started.md) — it lists the local-env, docker, and Claude plugins you need
3. ✅ Read [architecture/system-design.md](./architecture/system-design.md), [module-development.md](./architecture/module-development.md), and [cross-module-communication.md](./architecture/cross-module-communication.md) in that order — probably 20 minutes total
4. ✅ Read this file (you're here)
5. ✅ `cd boilerplateBE && dotnet build` — must succeed before you start
6. ✅ `cd boilerplateBE && dotnet test --filter AbstractionsPurityTests` — must pass
7. ✅ Tell Claude: "Let's start D2. Read docs/developer-guide/domain-module-example.md, then follow §5 execution order starting with the skeleton."

That's enough context for Claude to resume productively without the prior conversation history.
