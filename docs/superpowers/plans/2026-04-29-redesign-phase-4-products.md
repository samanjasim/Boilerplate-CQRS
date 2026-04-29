# Phase 4 Products Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring Products list, detail, create, dashboard card, and tenant-products tab onto J4 Spectrum with one backend status-counts endpoint and no product-behavior expansion.

**Architecture:** Keep Products as a small vertical feature: one backend query endpoint, one typed frontend hook, one list-page hero component, one extracted detail identity panel, and one SuperAdmin reassign dialog. Reuse existing `MetricCard`, `StatCard`, `STATUS_BADGE_VARIANT`, `Card variant="glass"`, shadcn form controls, TanStack Query keys, and module `ProductsDbContext` patterns instead of adding a new design vocabulary.

**Tech Stack:** React 19, TypeScript, Tailwind 4, shadcn/ui, TanStack Query, react-hook-form, react-i18next, .NET 10, EF Core, MediatR CQRS.

**Spec:** [`docs/superpowers/specs/2026-04-29-redesign-phase-4-products-design.md`](../specs/2026-04-29-redesign-phase-4-products-design.md)

---

## Spec Review Notes

I reviewed the spec against the current code and patched two gaps before writing this plan:

1. **Tenant-filtered hero counts:** The list hero must update when a SuperAdmin selects a tenant. The spec now makes `GET /api/v1/products/status-counts?tenantId={{tenantId}}` explicit.
2. **Localization coverage:** Current Products pages rely heavily on `t(key, fallback)`. The spec now requires locale entries for both new copy and touched existing product UI strings.

Additional plan-stage resolutions:

1. **DbContext name:** The Products module uses `ProductsDbContext`; there is no separate products read-context interface in this module.
2. **Table glass shell:** `boilerplateFE/src/components/ui/table.tsx` already wraps every table in `surface-glass rounded-2xl`; implementation should not add nested glass wrappers unless a screenshot proves the current shell is insufficient.
3. **Tenant reassign semantics:** `TenantReassignDialog` sends the last-saved product values plus the new `tenantId`. It must not silently save dirty edits from the right-column form.
4. **Detail form reset:** After successful save, call `reset(savedValues)` so the sticky footer hides and `Cancel` always returns to the last-saved state.

---

## File Structure

**New (BE):**
- `boilerplateBE/src/modules/Starter.Module.Products/Application/DTOs/ProductStatusCountsDto.cs` — `Draft`, `Active`, `Archived` counts.
- `boilerplateBE/src/modules/Starter.Module.Products/Application/Queries/GetProductStatusCounts/GetProductStatusCountsQuery.cs` — optional `TenantId`.
- `boilerplateBE/src/modules/Starter.Module.Products/Application/Queries/GetProductStatusCounts/GetProductStatusCountsQueryHandler.cs` — groups `ProductsDbContext.Products` by status.
- `boilerplateBE/src/modules/Starter.Module.Products/AssemblyInfo.cs` — test access for internal handlers if absent.
- `boilerplateBE/tests/Starter.Api.Tests/Products/GetProductStatusCountsQueryHandlerTests.cs` — tenant-filter and zero-count coverage.

**Modified (BE):**
- `boilerplateBE/src/modules/Starter.Module.Products/Controllers/ProductsController.cs` — add `[HttpGet("status-counts")]`.

**New (FE):**
- `boilerplateFE/src/features/products/components/ProductStatusHero.tsx` — collapse-when-zero `Drafts / Active / Archived` strip.
- `boilerplateFE/src/features/products/components/ProductIdentityPanel.tsx` — left detail panel: image, price, slug, status, tenant chip, upload, publish/archive actions.
- `boilerplateFE/src/features/products/components/TenantReassignDialog.tsx` — SuperAdmin tenant move dialog.

**Modified (FE):**
- `boilerplateFE/src/types/product.types.ts` — add `ProductStatusCounts`.
- `boilerplateFE/src/config/api.config.ts` — add `PRODUCTS.STATUS_COUNTS`.
- `boilerplateFE/src/lib/query/keys.ts` — add `queryKeys.products.statusCounts(params)`.
- `boilerplateFE/src/features/products/api/products.api.ts` — add `getStatusCounts(params)`.
- `boilerplateFE/src/features/products/api/products.queries.ts` — add `useProductStatusCounts(params)` and invalidate status counts from product mutations.
- `boilerplateFE/src/features/products/pages/ProductsListPage.tsx` — add hero, token sweep tenant chip/status labels.
- `boilerplateFE/src/features/products/pages/ProductDetailPage.tsx` — rewrite layout to 2-column detail + sticky dirty footer.
- `boilerplateFE/src/features/products/pages/ProductCreatePage.tsx` — `Card variant="glass"` + upload hint.
- `boilerplateFE/src/features/products/components/ProductsDashboardCard.tsx` — replace custom orange card with shared `StatCard`.
- `boilerplateFE/src/features/products/components/TenantProductsTab.tsx` — remove heading, use shared badge map, rely on shared table shell.
- `boilerplateFE/src/i18n/locales/en/translation.json`
- `boilerplateFE/src/i18n/locales/ar/translation.json`
- `boilerplateFE/src/i18n/locales/ku/translation.json`

---

## Tasks

> **Path note:** `npm` commands run from `boilerplateFE/`. `dotnet` commands run from `boilerplateBE/`.

### Task 0: Branch and Dependency Check

**Files:** none

- [ ] **Step 1: Confirm branch and working tree**

```bash
git status --short
git rev-parse --abbrev-ref HEAD
```

Expected: branch is `fe/phase-4-design`. Existing uncommitted spec/plan docs are allowed; do not revert unrelated user work.

- [ ] **Step 2: Confirm reused primitives exist**

```bash
ls boilerplateFE/src/components/common/MetricCard.tsx
ls boilerplateFE/src/components/common/StatCard.tsx
grep -n 'STATUS_BADGE_VARIANT' boilerplateFE/src/constants/status.ts
grep -n 'public sealed class ProductsDbContext' boilerplateBE/src/modules/Starter.Module.Products/Infrastructure/Persistence/ProductsDbContext.cs
```

Expected: all files and symbols are present.

---

### Task 1: BE Status-Counts Endpoint

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Products/Application/DTOs/ProductStatusCountsDto.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Products/Application/Queries/GetProductStatusCounts/GetProductStatusCountsQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.Products/Application/Queries/GetProductStatusCounts/GetProductStatusCountsQueryHandler.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.Products/Controllers/ProductsController.cs`

- [ ] **Step 1: Create DTO**

```csharp
namespace Starter.Module.Products.Application.DTOs;

public sealed record ProductStatusCountsDto(
    int Draft,
    int Active,
    int Archived
);
```

- [ ] **Step 2: Create query**

```csharp
using MediatR;
using Starter.Module.Products.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Queries.GetProductStatusCounts;

public sealed record GetProductStatusCountsQuery(Guid? TenantId = null)
    : IRequest<Result<ProductStatusCountsDto>>;
```

- [ ] **Step 3: Create handler**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Products.Application.DTOs;
using Starter.Module.Products.Domain.Enums;
using Starter.Module.Products.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Queries.GetProductStatusCounts;

internal sealed class GetProductStatusCountsQueryHandler(ProductsDbContext context)
    : IRequestHandler<GetProductStatusCountsQuery, Result<ProductStatusCountsDto>>
{
    public async Task<Result<ProductStatusCountsDto>> Handle(
        GetProductStatusCountsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Products.AsNoTracking().AsQueryable();

        if (request.TenantId.HasValue)
            query = query.Where(p => p.TenantId == request.TenantId.Value);

        var counts = await query
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var dict = counts.ToDictionary(x => x.Status, x => x.Count);

        return Result.Success(new ProductStatusCountsDto(
            Draft: dict.GetValueOrDefault(ProductStatus.Draft),
            Active: dict.GetValueOrDefault(ProductStatus.Active),
            Archived: dict.GetValueOrDefault(ProductStatus.Archived)
        ));
    }
}
```

- [ ] **Step 4: Add controller action before `GetById`**

Add this using:

```csharp
using Starter.Module.Products.Application.Queries.GetProductStatusCounts;
```

Add this action after `GetAll` and before `[HttpGet("{id:guid}")]`:

```csharp
[HttpGet("status-counts")]
[Authorize(Policy = ProductPermissions.View)]
[ProducesResponseType(typeof(ApiResponse<ProductStatusCountsDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetStatusCounts(
    [FromQuery] Guid? tenantId = null,
    CancellationToken ct = default)
{
    var result = await Mediator.Send(new GetProductStatusCountsQuery(tenantId), ct);
    return HandleResult(result);
}
```

- [ ] **Step 5: Build backend**

```bash
dotnet build src/Starter.Api
```

Expected: build clean.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Products/Application/DTOs/ProductStatusCountsDto.cs \
        boilerplateBE/src/modules/Starter.Module.Products/Application/Queries/GetProductStatusCounts \
        boilerplateBE/src/modules/Starter.Module.Products/Controllers/ProductsController.cs
git commit -m "feat(be/products): add product status counts"
```

---

### Task 2: BE Handler Tests

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.Products/AssemblyInfo.cs` if it does not exist
- Create: `boilerplateBE/tests/Starter.Api.Tests/Products/GetProductStatusCountsQueryHandlerTests.cs`

- [ ] **Step 1: Expose module internals to tests if needed**

If `boilerplateBE/src/modules/Starter.Module.Products/AssemblyInfo.cs` does not exist, create:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Starter.Api.Tests")]
```

- [ ] **Step 2: Write handler tests**

Create `boilerplateBE/tests/Starter.Api.Tests/Products/GetProductStatusCountsQueryHandlerTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.Products.Application.Queries.GetProductStatusCounts;
using Starter.Module.Products.Domain.Entities;
using Starter.Module.Products.Domain.Enums;
using Starter.Module.Products.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Products;

public sealed class GetProductStatusCountsQueryHandlerTests
{
    [Fact]
    public async Task PlatformAggregate_ReturnsCountsAcrossTenants()
    {
        await using var db = NewDb();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        db.Products.AddRange(
            Product.Create(tenantA, "Draft A", "draft-a", null, 10, "USD"),
            Product.Create(tenantA, "Active A", "active-a", null, 20, "USD", ProductStatus.Active),
            Product.Create(tenantB, "Draft B", "draft-b", null, 30, "USD"),
            Product.Create(tenantB, "Archived B", "archived-b", null, 40, "USD", ProductStatus.Archived)
        );
        await db.SaveChangesAsync();

        var handler = new GetProductStatusCountsQueryHandler(db);
        var result = await handler.Handle(new GetProductStatusCountsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Draft);
        Assert.Equal(1, result.Value.Active);
        Assert.Equal(1, result.Value.Archived);
    }

    [Fact]
    public async Task TenantFilter_ReturnsOnlySelectedTenantCounts()
    {
        await using var db = NewDb();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        db.Products.AddRange(
            Product.Create(tenantA, "Draft A", "draft-a", null, 10, "USD"),
            Product.Create(tenantA, "Active A", "active-a", null, 20, "USD", ProductStatus.Active),
            Product.Create(tenantB, "Archived B", "archived-b", null, 40, "USD", ProductStatus.Archived)
        );
        await db.SaveChangesAsync();

        var handler = new GetProductStatusCountsQueryHandler(db);
        var result = await handler.Handle(new GetProductStatusCountsQuery(tenantA), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Draft);
        Assert.Equal(1, result.Value.Active);
        Assert.Equal(0, result.Value.Archived);
    }

    [Fact]
    public async Task EmptyDatabase_ReturnsZeroCounts()
    {
        await using var db = NewDb();

        var handler = new GetProductStatusCountsQueryHandler(db);
        var result = await handler.Handle(new GetProductStatusCountsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Draft);
        Assert.Equal(0, result.Value.Active);
        Assert.Equal(0, result.Value.Archived);
    }

    private static ProductsDbContext NewDb(Guid? tenantId = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);

        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase($"products-status-counts-{Guid.NewGuid():N}")
            .Options;

        return new ProductsDbContext(options, currentUser.Object);
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter ProductStatusCounts
```

Expected: all new tests pass.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.Products/AssemblyInfo.cs \
        boilerplateBE/tests/Starter.Api.Tests/Products/GetProductStatusCountsQueryHandlerTests.cs
git commit -m "test(be/products): cover status counts query"
```

---

### Task 3: FE API, Types, Query Keys

**Files:**
- Modify: `boilerplateFE/src/types/product.types.ts`
- Modify: `boilerplateFE/src/config/api.config.ts`
- Modify: `boilerplateFE/src/lib/query/keys.ts`
- Modify: `boilerplateFE/src/features/products/api/products.api.ts`
- Modify: `boilerplateFE/src/features/products/api/products.queries.ts`

- [ ] **Step 1: Add type**

```ts
export interface ProductStatusCounts {
  draft: number;
  active: number;
  archived: number;
}
```

- [ ] **Step 2: Add endpoint**

```ts
STATUS_COUNTS: '/Products/status-counts',
```

- [ ] **Step 3: Add query key**

```ts
statusCounts: (params?: Record<string, unknown>) => ['products', 'status-counts', params] as const,
```

- [ ] **Step 4: Add API function**

```ts
import type { Product, CreateProductData, UpdateProductData, ProductStatusCounts } from '@/types';

getStatusCounts: (params?: Record<string, unknown>) =>
  apiClient
    .get<{ data: ProductStatusCounts }>(API_ENDPOINTS.PRODUCTS.STATUS_COUNTS, { params })
    .then((r) => r.data),
```

- [ ] **Step 5: Add hook and invalidations**

```ts
export function useProductStatusCounts(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.products.statusCounts(params),
    queryFn: () => productsApi.getStatusCounts(params),
    select: (r) => r.data,
    staleTime: 30_000,
  });
}
```

In `useCreateProduct`, `useUpdateProduct`, `usePublishProduct`, `useArchiveProduct`, and `useUploadProductImage`, keep the existing `queryKeys.products.all` invalidation and add:

```ts
queryClient.invalidateQueries({ queryKey: queryKeys.products.statusCounts() });
```

- [ ] **Step 6: Type-check frontend**

```bash
npm run build
```

Expected: build clean.

- [ ] **Step 7: Commit**

```bash
git add boilerplateFE/src/types/product.types.ts \
        boilerplateFE/src/config/api.config.ts \
        boilerplateFE/src/lib/query/keys.ts \
        boilerplateFE/src/features/products/api/products.api.ts \
        boilerplateFE/src/features/products/api/products.queries.ts
git commit -m "feat(fe/products): add status counts hook"
```

---

### Task 4: Products List Hero and Table Polish

**Files:**
- Create: `boilerplateFE/src/features/products/components/ProductStatusHero.tsx`
- Modify: `boilerplateFE/src/features/products/pages/ProductsListPage.tsx`

- [ ] **Step 1: Create `ProductStatusHero`**

Component contract:

```ts
interface ProductStatusHeroProps {
  tenantId?: string;
}
```

Implementation rules:
- call `useProductStatusCounts(tenantId ? { tenantId } : undefined)`.
- render nothing when all counts are zero.
- during first load, render three placeholder cards with `"-"`.
- use `MetricCard` with labels `products.hero.drafts`, `products.hero.active`, `products.hero.archived`.
- use `tone="active"` only for Active.
- use responsive grid classes `grid gap-4 sm:grid-cols-2 lg:grid-cols-3`.

- [ ] **Step 2: Wire into `ProductsListPage`**

After `PageHeader`, before `ListToolbar`, add:

```tsx
<ProductStatusHero tenantId={(list.filters.tenantId as string) || undefined} />
```

Only pass `tenantId` for SuperAdmin:

```tsx
const selectedTenantId = isPlatformAdmin ? ((list.filters.tenantId as string) || undefined) : undefined;
```

- [ ] **Step 3: Polish visible table cells**

Use localized status labels:

```tsx
{t(`products.status.${product.status.toLowerCase()}`, product.status)}
```

For the SuperAdmin tenant cell, replace muted text with a chip:

```tsx
<span className="inline-flex rounded-full border border-[var(--border-strong)] bg-[var(--active-bg)] px-2.5 py-1 text-xs font-medium text-[var(--tinted-fg)]">
  {product.tenantName ?? t('common.none', 'None')}
</span>
```

- [ ] **Step 4: Build frontend**

```bash
npm run build
```

Expected: build clean.

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/features/products/components/ProductStatusHero.tsx \
        boilerplateFE/src/features/products/pages/ProductsListPage.tsx
git commit -m "feat(fe/products): add status hero to list"
```

---

### Task 5: Detail Page Components and Reassign Dialog

**Files:**
- Create: `boilerplateFE/src/features/products/components/ProductIdentityPanel.tsx`
- Create: `boilerplateFE/src/features/products/components/TenantReassignDialog.tsx`
- Modify: `boilerplateFE/src/features/products/pages/ProductDetailPage.tsx`

- [ ] **Step 1: Create `TenantReassignDialog`**

Props:

```ts
interface TenantReassignDialogProps {
  isOpen: boolean;
  onClose: () => void;
  product: Product;
}
```

Implementation rules:
- fetch tenants with `useTenants({ pageSize: 100 })`.
- initialize `selectedTenantId` from `product.tenantId ?? ''` whenever the dialog opens.
- disable confirm when selected tenant equals current tenant or mutation is pending.
- call `useUpdateProduct().mutateAsync({ id: product.id, name: product.name, description: product.description, price: product.price, currency: product.currency, tenantId: selectedTenantId })`.
- close after success.
- use `Dialog`, `DialogHeader`, `DialogTitle`, `DialogDescription`, `DialogFooter`.

- [ ] **Step 2: Create `ProductIdentityPanel`**

Props:

```ts
interface ProductIdentityPanelProps {
  product: Product;
  canEdit: boolean;
  isPlatformAdmin: boolean;
  onPublish: () => void;
  onArchive: () => void;
  onUploadImage: (event: React.ChangeEvent<HTMLInputElement>) => Promise<void>;
  publishPending: boolean;
  uploadPending: boolean;
}
```

Implementation rules:
- image tile uses `useFileUrl(product.imageFileId ?? '')`.
- no-image tile uses `Package` icon and `products.noImage`.
- price line is `gradient-text text-3xl font-display tabular-nums`.
- slug line is mono, `text-xs text-muted-foreground`.
- status pill uses `STATUS_BADGE_VARIANT`.
- SuperAdmin tenant chip shows `common.tenant: product.tenantName`.
- SuperAdmin reassign button opens `TenantReassignDialog`.
- upload button triggers a hidden file input and is hidden when `!canEdit`.
- publish button appears only for Draft + canEdit.
- archive button appears only for Active + canEdit and calls `onArchive`.

- [ ] **Step 3: Rewrite detail layout**

In `ProductDetailPage.tsx`:
- remove tenant state from the form; tenant reassignment lives in the dialog.
- import `ProductIdentityPanel`.
- keep `ConfirmDialog`.
- use `reset` from `useForm`.
- after successful save, call:

```ts
reset({
  name: data.name,
  description: data.description ?? '',
  price: data.price,
  currency: data.currency,
});
```

- cancel button calls:

```ts
reset({
  name: product.name,
  description: product.description ?? '',
  price: product.price,
  currency: product.currency,
});
```

- layout shell:

```tsx
<form onSubmit={handleSubmit(onSubmit)} className="grid gap-6 lg:grid-cols-[minmax(280px,0.4fr)_minmax(0,0.6fr)] lg:items-start">
  <ProductIdentityPanel
    product={product}
    canEdit={canEdit}
    isPlatformAdmin={isPlatformAdmin}
    onPublish={handlePublish}
    onArchive={() => setShowArchiveDialog(true)}
    onUploadImage={handleImageUpload}
    publishPending={publishProduct.isPending}
    uploadPending={uploadImage.isPending}
  />
  <div className="min-h-0 space-y-6">
    <Card variant="glass">
      <CardHeader>
        <CardTitle>{t('products.details')}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="space-y-2">
          <Label htmlFor="name">{t('products.name')}</Label>
          <Input id="name" {...register('name')} disabled={!canEdit} />
          {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
        </div>
        <div className="space-y-2">
          <Label htmlFor="description">{t('products.description')}</Label>
          <Textarea id="description" {...register('description')} rows={3} disabled={!canEdit} />
          {errors.description && <p className="text-sm text-destructive">{errors.description.message}</p>}
        </div>
      </CardContent>
    </Card>
    <Card variant="glass">
      <CardHeader>
        <CardTitle>{t('products.pricing')}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="price">{t('products.price')}</Label>
            <Input id="price" type="number" step="0.01" min="0" {...register('price', { valueAsNumber: true })} disabled={!canEdit} />
            {errors.price && <p className="text-sm text-destructive">{errors.price.message}</p>}
          </div>
          <div className="space-y-2">
            <Label htmlFor="currency">{t('products.currency')}</Label>
            <Select value={watch('currency')} onValueChange={(v) => setValue('currency', v, { shouldValidate: true, shouldDirty: true })} disabled={!canEdit}>
              <SelectTrigger>
                <SelectValue placeholder={t('products.selectCurrency')} />
              </SelectTrigger>
              <SelectContent>
                {CURRENCIES.map((currency) => (
                  <SelectItem key={currency} value={currency}>{currency}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            {errors.currency && <p className="text-sm text-destructive">{errors.currency.message}</p>}
          </div>
        </div>
      </CardContent>
    </Card>
    {canEdit && isDirty && (
      <div className="sticky bottom-0 z-10 flex flex-col gap-3 rounded-2xl border border-border/40 surface-glass-strong p-4 shadow-float sm:flex-row sm:items-center sm:justify-between">
        <p className="text-xs font-bold uppercase tracking-[0.14em] text-muted-foreground">
          {t('products.detail.saveFooter')}
        </p>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={handleCancel}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" disabled={isSubmitting || updateProduct.isPending}>
            {t('common.saveChanges')}
          </Button>
        </div>
      </div>
    )}
  </div>
</form>
```

- keep both `Slot` extension surfaces below the grid, full width.

- [ ] **Step 4: Build frontend**

```bash
npm run build
```

Expected: build clean.

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/features/products/components/ProductIdentityPanel.tsx \
        boilerplateFE/src/features/products/components/TenantReassignDialog.tsx \
        boilerplateFE/src/features/products/pages/ProductDetailPage.tsx
git commit -m "feat(fe/products): redesign product detail"
```

---

### Task 6: Product Create Page Polish

**Files:**
- Modify: `boilerplateFE/src/features/products/pages/ProductCreatePage.tsx`

- [ ] **Step 1: Convert cards to glass**

Change all create-page cards to:

```tsx
<Card variant="glass">
```

- [ ] **Step 2: Add upload hint below the form actions**

Below the `Cancel / Create Product` action row:

```tsx
<p className="text-xs text-muted-foreground">
  {t('products.uploadHint')}
</p>
```

- [ ] **Step 3: Keep structure unchanged**

Confirm the page still renders: tenant select for SuperAdmin, Details, Pricing, action row. No dialog, no wizard, no image upload.

- [ ] **Step 4: Build frontend**

```bash
npm run build
```

Expected: build clean.

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/features/products/pages/ProductCreatePage.tsx
git commit -m "style(fe/products): polish create form"
```

---

### Task 7: Ancillary Product Surfaces

**Files:**
- Modify: `boilerplateFE/src/features/products/components/ProductsDashboardCard.tsx`
- Modify: `boilerplateFE/src/features/products/components/TenantProductsTab.tsx`

- [ ] **Step 1: Replace dashboard card with `StatCard`**

Use:

```tsx
<Link to={ROUTES.PRODUCTS.LIST} className="block">
  <StatCard
    icon={Package}
    label={t('dashboard.activeProducts')}
    value={totalActive}
    tone="copper"
  />
</Link>
```

Remove `Card`, `CardContent`, hardcoded orange classes, and `hover-lift`.

- [ ] **Step 2: Polish tenant products tab**

Changes:
- remove the local `<h3>`.
- import `STATUS_BADGE_VARIANT`.
- render status badge via `STATUS_BADGE_VARIANT[product.status] ?? 'secondary'`.
- localize status label via `products.status.*`.
- keep shared `Table`, `Pagination`, and `EmptyState`.

- [ ] **Step 3: Build frontend**

```bash
npm run build
```

Expected: build clean.

- [ ] **Step 4: Commit**

```bash
git add boilerplateFE/src/features/products/components/ProductsDashboardCard.tsx \
        boilerplateFE/src/features/products/components/TenantProductsTab.tsx
git commit -m "style(fe/products): polish ancillary surfaces"
```

---

### Task 8: Localize Products UI

**Files:**
- Modify: `boilerplateFE/src/i18n/locales/en/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ar/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ku/translation.json`

- [ ] **Step 1: Add or complete the `products` block in English**

Include every key used by the touched product files:

```json
"products": {
  "title": "Products",
  "subtitle": "Manage your product catalog",
  "create": "Create Product",
  "createTitle": "Create Product",
  "createSubtitle": "Add a new product to your catalog",
  "allStatuses": "All Statuses",
  "allTenants": "All Tenants",
  "name": "Name",
  "slug": "Slug",
  "price": "Price",
  "currency": "Currency",
  "description": "Description",
  "details": "Product Details",
  "pricing": "Pricing",
  "image": "Product Image",
  "tenant": "Tenant",
  "selectTenant": "Select a tenant for this product",
  "chooseTenant": "Choose a tenant...",
  "assignedTenant": "Assigned Tenant",
  "selectCurrency": "Select currency",
  "generateSlug": "Generate",
  "publish": "Publish",
  "archive": "Archive",
  "archiveTitle": "Archive Product",
  "archiveDescription": "Are you sure you want to archive this product? This action can be reversed.",
  "uploadImage": "Upload image",
  "changeImage": "Change image",
  "imageUploaded": "Image uploaded",
  "created": "Product created",
  "updated": "Product updated",
  "published": "Product published",
  "archived": "Product archived",
  "noImage": "No image uploaded yet",
  "uploadHint": "You can add an image after creating the product",
  "empty": {
    "title": "No products found",
    "description": "Create your first product to get started.",
    "tenantDescription": "This tenant has no products yet."
  },
  "status": {
    "label": "Status",
    "draft": "Draft",
    "active": "Active",
    "archived": "Archived"
  },
  "hero": {
    "drafts": "Drafts",
    "draftsEyebrow": "needs publish",
    "active": "Active",
    "activeEyebrow": "in catalog",
    "archived": "Archived",
    "archivedEyebrow": "retired"
  },
  "detail": {
    "reassignTenant": "Reassign tenant...",
    "reassignTitle": "Move product to a different tenant",
    "reassignDescription": "{{productName}} will move to {{newTenant}} and become invisible to {{oldTenant}} immediately. This is reversible.",
    "reassignConfirm": "Move product",
    "reassignCancel": "Cancel",
    "saveFooter": "Unsaved changes"
  }
}
```

- [ ] **Step 2: Add Arabic and Kurdish equivalents**

Add the same key tree in `ar` and `ku`. Keep interpolation variables exactly as `{{productName}}`, `{{newTenant}}`, and `{{oldTenant}}`.

- [ ] **Step 3: Remove English fallback dependence in touched product files**

For touched products components, prefer:

```tsx
t('products.title')
```

over:

```tsx
t('products.title', 'Products')
```

Leave unrelated feature files alone.

- [ ] **Step 4: Build frontend**

```bash
npm run build
```

Expected: build clean, no JSON parse errors.

- [ ] **Step 5: Commit**

```bash
git add boilerplateFE/src/i18n/locales/en/translation.json \
        boilerplateFE/src/i18n/locales/ar/translation.json \
        boilerplateFE/src/i18n/locales/ku/translation.json \
        boilerplateFE/src/features/products
git commit -m "chore(fe/products): localize redesigned product surfaces"
```

---

### Task 9: Full Verification and Visual Pass

**Files:** none unless verification finds a defect.

- [ ] **Step 1: Run frontend checks**

```bash
npm run lint
npm run build
```

Expected: both clean.

- [ ] **Step 2: Run backend checks**

```bash
dotnet test tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter ProductStatusCounts
dotnet build src/Starter.Api
```

Expected: tests pass and build clean.

- [ ] **Step 3: Live visual check**

Run the app in the existing `_testJ4visual` workflow:
- FE on `3100`
- BE on `5100`
- regenerate or refresh the test app only after the backend endpoint is available.

Check:
- `/products` with no products hides the hero and shows empty state.
- `/products` with mixed statuses shows collapsing hero cards.
- SuperAdmin tenant filter updates both list rows and hero counts.
- `/products/:id` lg+ is asymmetric two-column; left panel does not stretch to right-column height.
- `/products/:id` `<lg` stacks image panel first, form second.
- dirty form shows sticky footer; save and cancel both hide it afterward.
- Tenant admin cannot see tenant chip or reassign action.
- user without `Products.Update` sees disabled fields and no upload/publish/archive actions.
- `/products/new` remains a simple single-column form.
- dashboard products card uses `StatCard`.
- tenant detail products tab has no duplicate heading.

- [ ] **Step 4: RTL pass**

Switch to Arabic and check:
- `/products` hero and table remain coherent.
- `/products/:id` columns flip logically in RTL.
- sticky footer uses logical start/end ordering.
- dialog text interpolates product and tenant names.

- [ ] **Step 5: Spot-check neighboring phases**

Open one page from each cluster:
- Identity: Users or Roles
- Platform admin: Tenants or Audit Logs
- Data: Files or Reports
- Billing: Subscriptions or Billing Plans

Expected: no obvious visual regression from shared components or query invalidations.

- [ ] **Step 6: Final commit if verification fixes were needed**

```bash
git status --short
git add docs/superpowers/specs/2026-04-29-redesign-phase-4-products-design.md \
        docs/superpowers/plans/2026-04-29-redesign-phase-4-products.md \
        boilerplateBE/src/modules/Starter.Module.Products \
        boilerplateBE/tests/Starter.Api.Tests/Products \
        boilerplateFE/src/features/products \
        boilerplateFE/src/types/product.types.ts \
        boilerplateFE/src/config/api.config.ts \
        boilerplateFE/src/lib/query/keys.ts \
        boilerplateFE/src/i18n/locales/en/translation.json \
        boilerplateFE/src/i18n/locales/ar/translation.json \
        boilerplateFE/src/i18n/locales/ku/translation.json
git commit -m "fix(fe/products): address visual verification issues"
```

---

## Review Checklist

- [ ] No backend change beyond status-counts endpoint, DTO, query, handler, tests, and controller action.
- [ ] No new product functionality beyond moving existing status/image/tenant controls into better UI.
- [ ] All touched visible product strings have EN, AR, and KU keys.
- [ ] No hardcoded orange/product color classes remain in `ProductsDashboardCard`.
- [ ] `ProductsListPage` and `TenantProductsTab` use `STATUS_BADGE_VARIANT`.
- [ ] Detail page publish/archive actions live in the identity panel, not the page header.
- [ ] Tenant reassignment is SuperAdmin-only in UI and still enforced by existing backend handler logic.
- [ ] Tables avoid nested `surface-glass` wrappers because the shared `Table` already owns the shell.
