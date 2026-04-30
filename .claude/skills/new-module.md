# New Module Development

Use this skill when building a new optional module for the Boilerplate-CQRS solution. It covers every layer — backend skeleton, domain, CQRS, controllers, frontend registration, pages, slots, and the full wiring checklist. Follow each phase in order.

## When to Use

When the user asks to add a new feature module (e.g., "add an Orders module", "build Inventory module"). A module is an optional, self-contained vertical slice that can be toggled on/off via `modules.catalog.json` and removed entirely by `rename.ps1 -Modules`.

## Pre-Flight

Before starting, confirm with the user:

1. **Module name** — singular noun, PascalCase (e.g., `Products`, `Inventory`, `Orders`)
2. **Core entities** — what domain objects does this module own?
3. **Slot contributions** — does the module contribute UI to existing pages? (e.g., tenant detail tabs, dashboard cards)
4. **Events consumed** — does it react to core events like `TenantRegisteredEvent`?
5. **Capabilities used** — does it need quota checking, webhook publishing, file uploads?

---

## Phase 1 — Backend Skeleton

### 1.1 Project Structure

Create `boilerplateBE/src/modules/Starter.Module.{Name}/` with this layout:

```
Starter.Module.{Name}/
  Starter.Module.{Name}.csproj
  {Name}Module.cs
  Constants/
    {Name}Permissions.cs
  Domain/
    Entities/
    Enums/
    Errors/
    Events/
  Application/
    Commands/{Action}/
    Queries/{Action}/
    DTOs/
    EventHandlers/
  Infrastructure/
    Persistence/
      {Name}DbContext.cs
    Configurations/
    Services/
  Controllers/
    {Name}Controller.cs
```

### 1.2 Project File (.csproj)

Reference `Starter.Abstractions.Web` (always) and `Starter.Abstractions.Messaging` (only if your module ships at least one `IConsumer<T>` or otherwise needs to register MassTransit infrastructure). Never reference other modules or core Application/Infrastructure/Domain projects.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="MassTransit" />
    <PackageReference Include="MassTransit.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Starter.Abstractions.Web\Starter.Abstractions.Web.csproj" />
    <!-- Add this only if your module ships an IConsumer<T> or registers an
         additional EF outbox. The interface IModuleBusContributor lives here. -->
    <ProjectReference Include="..\..\Starter.Abstractions.Messaging\Starter.Abstractions.Messaging.csproj" />
  </ItemGroup>
</Project>
```

### 1.3 Module Class ({Name}Module.cs)

Implements `IModule` (always) and `IModuleBusContributor` (only if your module ships at least one `IConsumer<T>`). This is the module's entry point — DI registration, permissions, migrations, seed data, and bus wiring.

> **Why `IModuleBusContributor`?** Tier 2.5 Theme 5 removed the host's auto-discovery of consumers from module assemblies. Modules now own their bus surface — `bus.AddConsumers(typeof({Name}Module).Assembly)` opts your assembly into MassTransit's discovery. The architecture test `ModuleRegistryTests.Modules_with_MassTransit_consumers_implement_IModuleBusContributor` fails the build if you forget. Skip the interface if your module has zero consumers.

```csharp
using MassTransit;                          // only if implementing IModuleBusContributor
using Starter.Abstractions.Modularity;

public sealed class {Name}Module : IModule, IModuleBusContributor   // drop the second interface if no consumers
{
    public string Name => "{Name}";
    public string DisplayName => "{Display Name}";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // DbContext with isolated migration history table
        services.AddDbContext<{Name}DbContext>((sp, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_{Name}");
                    npgsqlOptions.MigrationsAssembly(typeof({Name}DbContext).Assembly.FullName);
                });
        });

        // MediatR handlers from this assembly
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof({Name}Module).Assembly));

        // FluentValidation validators from this assembly
        services.AddValidatorsFromAssembly(typeof({Name}Module).Assembly);

        // NOTE: do NOT call services.AddMassTransit here — the host already
        // registers the bus once in Starter.Infrastructure. Consumer registration
        // happens in ConfigureBus below via IModuleBusContributor.

        // Usage metric calculator (if quota tracking needed)
        services.AddScoped<IUsageMetricCalculator, {Name}UsageMetricCalculator>();

        return services;
    }

    // Implement only if your module ships at least one IConsumer<T>.
    // Without this hook, the host won't see your consumers — they will be dead at runtime.
    public void ConfigureBus(IBusRegistrationConfigurator bus)
    {
        bus.AddConsumers(typeof({Name}Module).Assembly);

        // If your module has its own DbContext AND publishes events from inside it,
        // also register a per-DbContext outbox here. Most modules do NOT need this —
        // events published from MediatR handlers run against ApplicationDbContext's
        // outbox via IIntegrationEventCollector. See WorkflowModule for the
        // module-DbContext-outbox example.
    }

    public IEnumerable<(string Name, string Description, string Module)> GetPermissions()
    {
        yield return ({Name}Permissions.View, "View {name}", "{Name}");
        yield return ({Name}Permissions.Create, "Create {name}", "{Name}");
        yield return ({Name}Permissions.Update, "Update {name}", "{Name}");
    }

    public IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions()
    {
        yield return ("SuperAdmin", [
            {Name}Permissions.View, {Name}Permissions.Create, {Name}Permissions.Update]);
        yield return ("Admin", [
            {Name}Permissions.View, {Name}Permissions.Create, {Name}Permissions.Update]);
        yield return ("User", [{Name}Permissions.View]);
    }

    public async Task MigrateAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<{Name}DbContext>();
        await db.Database.MigrateAsync(ct);
    }

    // Override SeedDataAsync if initial data needed
}
```

### 1.4 Permissions Constants

```csharp
public static class {Name}Permissions
{
    public const string View = "{Name}.View";
    public const string Create = "{Name}.Create";
    public const string Update = "{Name}.Update";
    // Add Delete only if hard-delete is supported. Archive-only = no Delete permission.
}
```

### 1.5 DbContext

Each module has its own sealed DbContext. Key rules:

- Implements `IModuleDbContext` (marker interface)
- Accepts `ICurrentUserService` for tenant filtering
- Uses `__EFMigrationsHistory_{Name}` table (not the core table)
- Applies query filter: `CurrentTenantId == null || entity.TenantId == CurrentTenantId`
- Calls `ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly())`

```csharp
public sealed class {Name}DbContext : DbContext, IModuleDbContext
{
    private readonly ICurrentUserService? _currentUserService;
    private Guid? CurrentTenantId => _currentUserService?.TenantId;

    public {Name}DbContext(DbContextOptions<{Name}DbContext> options, ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<{Entity}> {Entities} => Set<{Entity}>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Tenant query filter
        modelBuilder.Entity<{Entity}>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
    }
}
```

### 1.6 Solution Wiring (3 files to modify + 1 catalog edit + codegen)

**Starter.sln** — Add project entry + NestedProjects mapping under the `modules` folder GUID `{FA170C0A-17C4-CD5A-EA12-445AFE5E2D23}`. Easiest path:

```
dotnet sln Starter.sln add src/modules/Starter.Module.{Name}/Starter.Module.{Name}.csproj
```

…then move the new `Project(…)`/`EndProject` block under the `modules` folder by editing the `NestedProjects` section so the new GUID maps to `{FA170C0A-17C4-CD5A-EA12-445AFE5E2D23}`.

**Starter.Api.csproj** — Add one `<ProjectReference>`:

```xml
<ProjectReference Include="..\modules\Starter.Module.{Name}\Starter.Module.{Name}.csproj" />
```

**`modules.catalog.json`** (repo root) — Add the new entry. The catalog feeds three generated artifacts (BE `ModuleRegistry.g.cs`, FE `modules.generated.ts`, mobile `modules.config.dart`, plus `eslint.config.modules.json`). See section [7. Module Manifest](#72-frontend-up-to-8-files) for the full schema.

```json
"{name}": {
  "displayName": "{Display Name}",
  "version": "1.0.0",
  "supportedPlatforms": ["backend", "web"],
  "backendModule": "Starter.Module.{Name}",
  "frontendFeature": "{name}",
  "configKey": "{name}",
  "required": false,
  "dependencies": [],
  "description": "..."
}
```

**Regenerate the bootstrap artifacts** from the catalog:

```bash
npm run generate:modules        # writes ModuleRegistry.g.cs, modules.generated.ts, modules.config.dart, eslint.config.modules.json
```

CI fails on drift (`modules-codegen-drift` job in `.github/workflows/modularity.yml`) so this step is mandatory — never hand-edit the four generated files.

**Checkpoint:** `dotnet build` must pass with 0 warnings, 0 errors.

---

## Phase 2 — Domain Layer

### 2.1 Entity (AggregateRoot)

Every module entity follows this pattern:

- Extends `AggregateRoot` (provides `Guid Id`, domain events, audit fields)
- Implements `ITenantEntity` if multi-tenant
- **Private constructor** for EF, **factory method** `Create()` for creation
- All properties use `private set` — mutations only through named methods
- Factory method normalizes data (`.Trim()`, `.ToLowerInvariant()`)
- Factory method raises domain events via `RaiseDomainEvent()`

```csharp
public sealed class {Entity} : AggregateRoot, ITenantEntity
{
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    // ... other properties with private set

    private {Entity}() { }  // For EF

    private {Entity}(Guid id, Guid? tenantId, string name, ...) : base(id)
    {
        TenantId = tenantId;
        Name = name;
        // ...
    }

    public static {Entity} Create(Guid? tenantId, string name, ...)
    {
        var entity = new {Entity}(Guid.NewGuid(), tenantId, name.Trim(), ...);
        entity.RaiseDomainEvent(new {Entity}CreatedEvent(entity.Id, tenantId));
        return entity;
    }

    public void Update(string name, ...) { Name = name.Trim(); ModifiedAt = DateTime.UtcNow; }
    public void Archive() { Status = {Entity}Status.Archived; ModifiedAt = DateTime.UtcNow; }
}
```

### 2.2 Enums

```csharp
public enum {Entity}Status { Draft, Active, Archived }
```

### 2.3 Domain Errors

Static class with pre-defined `Error` instances:

```csharp
public static class {Entity}Errors
{
    public static readonly Error NotFound = Error.NotFound("{Entity}.NotFound", "{Entity} not found.");
    public static readonly Error SlugAlreadyExists = Error.Conflict("{Entity}.SlugExists", "Slug already exists.");
    public static Error QuotaExceeded(int limit) =>
        Error.Validation("{Entity}.QuotaExceeded", $"Quota exceeded. Limit: {limit}.");
}
```

### 2.4 Domain Events

Sealed records extending `DomainEventBase`. These are future seams — define them even if not consumed yet.

```csharp
public sealed record {Entity}CreatedEvent(Guid {Entity}Id, Guid? TenantId) : DomainEventBase;
```

### 2.5 EF Configuration

- Table name: snake_case plural (e.g., `products`, `orders`)
- Column names: snake_case (e.g., `tenant_id`, `created_at`)
- Status enums: `.HasConversion<string>()` with `MaxLength(20)`
- Decimal prices: `.HasPrecision(18, 2)`
- Unique indexes: composite with `TenantId` (e.g., `new { e.TenantId, e.Slug }`)
- Performance indexes: `new { e.TenantId, e.Status }`

```csharp
public sealed class {Entity}Configuration : IEntityTypeConfiguration<{Entity}>
{
    public void Configure(EntityTypeBuilder<{Entity}> builder)
    {
        builder.ToTable("{entities}");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.Price).HasColumnName("price").HasPrecision(18, 2);

        builder.HasIndex(e => new { e.TenantId, e.Slug }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.Status });
    }
}
```

### 2.6 Migration

After DbContext + Configuration are done, generate migration via a test app:

```bash
# Create test app
pwsh scripts/rename.ps1 -Name "_testModuleName" -OutputDir "."

# Generate migration (from test app backend dir)
dotnet ef migrations add Init{Name} \
  --project src/_testModuleName.Module.{Name} \
  --startup-project src/_testModuleName.Api \
  --context {Name}DbContext
```

Copy the generated migration files back to the boilerplate module, renaming `_testModuleName` prefixes back to `Starter`.

**Checkpoint:** `dotnet build` passes.

---

## Phase 3 — Application Layer (CQRS)

### 3.1 Command Pattern

Every command follows this structure:

```
Application/Commands/{Action}/
  {Action}{Entity}Command.cs       — sealed record : IRequest<Result<T>>
  {Action}{Entity}CommandHandler.cs — sealed class with primary constructor
  {Action}{Entity}CommandValidator.cs — AbstractValidator<T> (optional)
```

**Command:**

```csharp
public sealed record Create{Entity}Command(
    string Name, string Slug, decimal Price, string Currency,
    Guid? TenantId = null) : IRequest<Result<Guid>>;
```

**Handler:**

- Returns `Result<T>` (never throws)
- Injects module's own DbContext + abstraction interfaces (never other modules' types)
- Resolves tenant from `ICurrentUserService.TenantId` ?? `command.TenantId`
- Checks quota via `IQuotaChecker` before create
- Validates uniqueness with `IgnoreQueryFilters()` (cross-tenant check)
- Creates entity via factory method
- Publishes webhook event via `IWebhookPublisher`
- Increments quota after save

```csharp
internal sealed class Create{Entity}CommandHandler(
    {Name}DbContext context,
    ICurrentUserService currentUser,
    IQuotaChecker quotaChecker,
    IWebhookPublisher webhookPublisher) : IRequestHandler<Create{Entity}Command, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(Create{Entity}Command request, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId ?? request.TenantId;

        // 1. Quota check (graceful no-op if Billing module not installed)
        if (tenantId.HasValue)
        {
            var quota = await quotaChecker.CheckAsync(tenantId.Value, "{entities}", cancellationToken: ct);
            if (!quota.Allowed)
                return Result.Failure<Guid>({Entity}Errors.QuotaExceeded(quota.Limit));
        }

        // 2. Uniqueness check (use IgnoreQueryFilters for cross-tenant validation)
        var exists = await context.{Entities}
            .IgnoreQueryFilters()
            .AnyAsync(e => e.TenantId == tenantId && e.Slug == request.Slug.ToLowerInvariant(), ct);
        if (exists)
            return Result.Failure<Guid>({Entity}Errors.SlugAlreadyExists);

        // 3. Create via factory method
        var entity = {Entity}.Create(tenantId, request.Name, request.Slug, ...);
        context.{Entities}.Add(entity);
        await context.SaveChangesAsync(ct);

        // 4. Post-save side effects
        if (tenantId.HasValue)
            await quotaChecker.IncrementAsync(tenantId.Value, "{entities}", cancellationToken: ct);

        await webhookPublisher.PublishAsync(
            "{entity}.created", tenantId,
            new { entity.Id, entity.Name }, ct);

        return Result.Success(entity.Id);
    }
}
```

**Validator:**

```csharp
public sealed class Create{Entity}CommandValidator : AbstractValidator<Create{Entity}Command>
{
    public Create{Entity}CommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(200)
            .Matches(@"^[a-z0-9]([a-z0-9-]*[a-z0-9])?$");
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(3);
    }
}
```

### 3.2 Query Pattern

```
Application/Queries/{Action}/
  Get{Entities}Query.cs         — sealed record : IRequest<Result<PaginatedList<{Entity}Dto>>>
  Get{Entities}QueryHandler.cs  — sealed class with primary constructor
```

**Query:**

```csharp
public sealed record Get{Entities}Query(
    int PageNumber = 1, int PageSize = 20,
    string? SearchTerm = null, string? Status = null,
    Guid? TenantId = null) : IRequest<Result<PaginatedList<{Entity}Dto>>>;
```

**Handler:**

- Uses `.AsNoTracking()` for reads
- Optional tenant override for platform admins
- Filters, searches, sorts
- Paginates via `PaginatedList<T>.CreateAsync()`
- Batch-loads tenant names via `ITenantReader.GetManyAsync()` to avoid N+1
- Maps to DTOs

```csharp
internal sealed class Get{Entities}QueryHandler(
    {Name}DbContext context,
    ITenantReader tenantReader) : IRequestHandler<Get{Entities}Query, Result<PaginatedList<{Entity}Dto>>>
{
    public async Task<Result<PaginatedList<{Entity}Dto>>> Handle(Get{Entities}Query request, CancellationToken ct)
    {
        var query = context.{Entities}.AsNoTracking().AsQueryable();

        if (request.TenantId.HasValue)
            query = query.Where(e => e.TenantId == request.TenantId.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLowerInvariant();
            query = query.Where(e => e.Name.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<{Entity}Status>(request.Status, true, out var status))
            query = query.Where(e => e.Status == status);

        query = query.OrderByDescending(e => e.CreatedAt);

        var page = await PaginatedList<{Entity}>.CreateAsync(query, request.PageNumber, request.PageSize, ct);

        // Batch-load tenant names via reader (cross-context safe)
        var tenantIds = page.Items.Where(e => e.TenantId.HasValue).Select(e => e.TenantId!.Value).Distinct();
        var tenants = await tenantReader.GetManyAsync(tenantIds, ct);
        var tenantNames = tenants.ToDictionary(t => t.Id, t => t.Name);

        var result = page.Map(e => e.ToDto(
            e.TenantId.HasValue && tenantNames.TryGetValue(e.TenantId.Value, out var name) ? name : null));

        return Result.Success(result);
    }
}
```

### 3.3 DTOs and Mapping

```csharp
// Application/DTOs/{Entity}Dto.cs
public sealed record {Entity}Dto(
    Guid Id, string Name, string Slug, decimal Price, string Currency,
    string Status, string? TenantName, Guid? ImageFileId,
    DateTime CreatedAt, DateTime? ModifiedAt);

// Extension method or static mapper
public static class {Entity}Mapper
{
    public static {Entity}Dto ToDto(this {Entity} entity, string? tenantName) =>
        new(entity.Id, entity.Name, entity.Slug, entity.Price, entity.Currency,
            entity.Status.ToString(), tenantName, entity.ImageFileId,
            entity.CreatedAt, entity.ModifiedAt);
}
```

### 3.4 Usage Metric Calculator

If the module tracks a countable resource against quotas:

```csharp
internal sealed class {Name}UsageMetricCalculator({Name}DbContext db) : IUsageMetricCalculator
{
    public string Metric => "{entities}";  // lowercase plural, matches quota key

    public async Task<long> CalculateAsync(Guid tenantId, CancellationToken ct = default) =>
        await db.{Entities}
            .IgnoreQueryFilters()
            .CountAsync(e => e.TenantId == tenantId && e.Status != {Entity}Status.Archived, ct);
}
```

---

## Phase 4 — Controller

Inherits `BaseApiController(ISender)`. Route is auto-derived from `[controller]`.

```csharp
public sealed class {Entities}Controller(ISender mediator) : BaseApiController(mediator)
{
    [HttpPost]
    [Authorize(Policy = {Name}Permissions.Create)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] Create{Entity}Command command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleCreatedResult(result, nameof(GetById),
            new { id = result.IsSuccess ? result.Value : (Guid?)null });
    }

    [HttpGet]
    [Authorize(Policy = {Name}Permissions.View)]
    [ProducesResponseType(typeof(PagedApiResponse<{Entity}Dto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null, [FromQuery] string? status = null,
        [FromQuery] Guid? tenantId = null, CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new Get{Entities}Query(pageNumber, pageSize, searchTerm, status, tenantId), ct);
        return HandlePagedResult(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = {Name}Permissions.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new Get{Entity}ByIdQuery(id), ct);
        return HandleResult(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = {Name}Permissions.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] Update{Entity}Command command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command with { Id = id }, ct);
        return HandleResult(result);
    }
}
```

**Result handler mapping:**
- `HandleResult(result)` — returns 200 OK or error (400/404/403/409)
- `HandlePagedResult(result)` — returns 200 OK with pagination metadata
- `HandleCreatedResult(result, actionName, routeValues)` — returns 201 Created with Location header

**Checkpoint:** `dotnet build` passes with 0W/0E.

---

## Phase 5 — Event Handlers (Cross-Module)

### 5.1 Consuming Core Events

If the module reacts to `TenantRegisteredEvent` (e.g., seeding demo data):

```csharp
internal sealed class Seed{Name}OnTenantRegistered(
    {Name}DbContext context,
    ILogger<Seed{Name}OnTenantRegistered> logger) : IConsumer<TenantRegisteredEvent>
{
    public async Task Consume(ConsumeContext<TenantRegisteredEvent> ctx)
    {
        var evt = ctx.Message;

        // MANDATORY idempotency check — MT delivers at-least-once, so every
        // consumer must tolerate duplicates. Use a domain-uniqueness key
        // (TenantId here) queried against this module's own DbContext.
        if (await context.{Entities}.IgnoreQueryFilters().AnyAsync(e => e.TenantId == evt.TenantId))
            return;

        // Seed initial data
        var entities = new[]
        {
            {Entity}.Create(evt.TenantId, "Sample 1", "sample-1", ...),
            {Entity}.Create(evt.TenantId, "Sample 2", "sample-2", ...),
        };

        context.{Entities}.AddRange(entities);
        await context.SaveChangesAsync(ctx.CancellationToken);

        logger.LogInformation("Seeded {Count} {entities} for tenant {TenantId}",
            entities.Length, evt.TenantId);
    }
}
```

**Key rules for event handlers:**

- Use `IConsumer<T>` from MassTransit (auto-registered via `AddConsumers()`)
- **Always** check idempotency: `IgnoreQueryFilters().AnyAsync(<natural-key>)` at the top
- Use the module's own DbContext, never `ApplicationDbContext`
- **Throw on transient failures** (DB blip, dependency 5xx). The default policy wrapped around every endpoint gives 3 retries at 1 s / 5 s / 15 s, then routes to the `_error` dead-letter queue. Swallowing exceptions is how messages get lost.
- **Return quietly** on idempotency hits and non-retryable business conditions (unknown tenant, feature-off).
- Log what you did at Info level for debugging.

### 5.2 Publishing Integration Events (if the module emits cross-module events)

If the module needs to broadcast something other modules may react to — e.g. `{Name}Created` — do **NOT** inject `IPublishEndpoint`. The boilerplate has a transactional-outbox interceptor wired to `ApplicationDbContext`; direct `IPublishEndpoint` use from an HTTP-request handler routes through the last-registered `IScopedBusContextProvider<IBus>` and silently drops events when two outboxes are registered. An architecture test (`MessagingArchitectureTests`) fails the build if `Starter.Application` gains a dependency on MassTransit.

**Correct pattern — handler in core emitting an event:**

```csharp
// Starter.Application/Features/{Feature}/Commands/.../{X}CommandHandler.cs
internal sealed class Create{Entity}CommandHandler(
    IApplicationDbContext context,
    IIntegrationEventCollector eventCollector)  // ← inject the collector, not the bus
    : IRequestHandler<Create{Entity}Command, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(Create{Entity}Command cmd, CancellationToken ct)
    {
        var entity = {Entity}.Create(cmd.Name, /* ... */);
        context.{Entities}.Add(entity);

        // Scheduled into a scoped in-memory collector. The
        // IntegrationEventOutboxInterceptor drains it during SavingChangesAsync
        // and writes an outbox row on the same DbContext transaction.
        eventCollector.Schedule(new {Entity}CreatedEvent(entity.Id, cmd.TenantId, DateTime.UtcNow));

        await context.SaveChangesAsync(ct);  // business data + outbox row commit atomically
        return Result.Success(entity.Id);
    }
}
```

**Inside a MassTransit consumer, `IPublishEndpoint` is the correct API** — MT's own outbox context is already in scope and targets the right DbContext. The rule only applies to MediatR/HTTP-request code.

**Event contract:**

- Define the event in `Starter.Application/Common/Events/{Name}Event.cs` as a `record` implementing `IDomainEvent`.
- Treat it as a public contract the moment it's published once.
- **Additive changes only.** Renaming or typing a property = create `{Name}EventV2` and migrate consumers gradually. Never change the CLR namespace + type name on a live event — MT uses that string as the routing key.

For the full reference (dead-letter tuning, correlation propagation, outbox lag health check, when to use capabilities instead), see [docs/architecture/cross-module-communication.md § Pattern 2](../../docs/architecture/cross-module-communication.md).

---

## Phase 6 — Frontend Module

### 6.1 Module Entry Point

`boilerplateFE/src/features/{name}/index.ts`:

```typescript
import { lazy } from 'react';
import { registerSlot } from '@/lib/extensions';

const Tenant{Name}Tab = lazy(() =>
  import('./components/Tenant{Name}Tab').then((m) => ({ default: m.Tenant{Name}Tab })),
);

export const {name}Module = {
  name: '{name}',
  register(): void {
    registerSlot('tenant-detail-tabs', {
      id: '{name}.tenant-{name}',
      module: '{name}',
      order: 40,  // After billing (30)
      label: () => '{Display Name}',
      permission: '{Name}.View',
      component: Tenant{Name}Tab,
    });

    // Add dashboard-cards slot if appropriate
  },
};
```

### 6.2 API Layer

**`api/{name}.api.ts`** — Raw axios calls:

```typescript
import { apiClient } from '@/lib/axios';
import { API_ENDPOINTS } from '@/config';

export const {name}Api = {
  getAll: (params?: Record<string, unknown>) =>
    apiClient.get(API_ENDPOINTS.{NAME}.LIST, { params }).then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<{ data: {Entity} }>(API_ENDPOINTS.{NAME}.DETAIL(id)).then((r) => r.data.data),

  create: (data: Create{Entity}Data) =>
    apiClient.post(API_ENDPOINTS.{NAME}.LIST, data).then((r) => r.data),

  update: (data: Update{Entity}Data) =>
    apiClient.put(API_ENDPOINTS.{NAME}.DETAIL(data.id), data).then((r) => r.data),

  // File uploads use FormData with Content-Type: multipart/form-data
  uploadImage: (id: string, file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return apiClient.post(API_ENDPOINTS.{NAME}.IMAGE(id), formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }).then((r) => r.data);
  },
};
```

**`api/{name}.queries.ts`** — TanStack Query hooks:

```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import i18n from '@/i18n';
import { queryKeys } from '@/lib/query/keys';
import { {name}Api } from './{name}.api';

export function use{Entities}(params?: Record<string, unknown>) {
  return useQuery({
    queryKey: queryKeys.{name}.list(params),
    queryFn: () => {name}Api.getAll(params),
  });
}

export function use{Entity}(id: string) {
  return useQuery({
    queryKey: queryKeys.{name}.detail(id),
    queryFn: () => {name}Api.getById(id),
    enabled: !!id,
  });
}

export function useCreate{Entity}() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Create{Entity}Data) => {name}Api.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.{name}.all });
      toast.success(i18n.t('{name}.created', '{Entity} created'));
    },
  });
}

export function useUpdate{Entity}() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Update{Entity}Data) => {name}Api.update(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.{name}.all });
      toast.success(i18n.t('{name}.updated', '{Entity} updated'));
    },
  });
}
```

**`api/index.ts`** — Re-exports:

```typescript
export * from './{name}.api';
export * from './{name}.queries';
```

### 6.3 Pages

Follow existing page patterns:

**List page** — `PageHeader` + search/filter bar + `Table` with `Pagination` + `EmptyState`. Platform admins see a tenant column + tenant filter. Use `useBackNavigation` on detail/create pages.

**Detail page** — Wrapper/inner pattern to avoid StrictMode timing issues:

```typescript
export default function {Entity}DetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data: entity, isLoading } = use{Entity}(id!);
  useBackNavigation(ROUTES.{NAME}.LIST, t('{name}.title'));

  if (isLoading || !entity) {
    return <div className="flex items-center justify-center py-12 text-muted-foreground">Loading...</div>;
  }
  return <{Entity}DetailForm {entity}={entity} />;
}

function {Entity}DetailForm({ {entity} }: { {entity}: {Entity} }) {
  // useForm initializes with real data — no useEffect sync needed
  const { register, handleSubmit, ... } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: {entity}.name,
      // ... map all fields from entity
    },
  });
  // ...
}
```

**Create page** — Standard form with `useForm` + `zodResolver`. `useBackNavigation` for back button.

### 6.4 Slot Components

**Tenant tab** — Receives `{ tenantId, tenantName }` props from the slot:

```typescript
export function Tenant{Name}Tab({ tenantId }: { tenantId: string }) {
  const { data, isLoading } = use{Entities}({ tenantId, pageSize: 10 });
  // Render table with entity data for this tenant
}
```

**Dashboard card** — Self-contained with a link to the list page. Use semantic tokens (not hardcoded colors):

```typescript
export function {Name}DashboardCard() {
  const { data } = use{Entities}({ pageSize: 1, status: 'Active' });
  const total = data?.pagination?.totalCount ?? 0;
  return (
    <Link to={ROUTES.{NAME}.LIST}>
      <Card className="hover-lift">
        <CardContent className="py-6">
          <div className="flex items-center gap-4">
            <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-primary/10">
              <{Icon} className="h-6 w-6 text-primary" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">{t('dashboard.active{Entities}')}</p>
              <p className="text-2xl font-bold">{total}</p>
            </div>
          </div>
        </CardContent>
      </Card>
    </Link>
  );
}
```

### 6.5 Types

`src/types/{entity}.types.ts`:

```typescript
export interface {Entity} {
  id: string;
  tenantId?: string;
  tenantName?: string;
  name: string;
  slug: string;
  description?: string;
  price: number;
  currency: string;
  status: {Entity}Status;
  imageFileId?: string;
  createdAt: string;
  modifiedAt?: string;
}

export type {Entity}Status = 'Draft' | 'Active' | 'Archived';

export interface Create{Entity}Data {
  name: string;
  slug: string;
  description?: string;
  price: number;
  currency: string;
  tenantId?: string;
}
```

---

## Phase 7 — Bootstrap File Wiring

These are the **only** files outside the module folders that should be modified. Track them carefully.

### 7.1 Backend (2 files)

Already covered in Phase 1.6:
1. `boilerplateBE/Starter.sln` — Project entry + NestedProjects
2. `boilerplateBE/src/Starter.Api/Starter.Api.csproj` — ProjectReference

### 7.2 Frontend (up to 7 files)

3. **`modules.catalog.json`** (repo root) — Add module entry. This is the single source of truth that the codegen consumes for BE registry, FE generated config, mobile config, and ESLint patterns. Always run `npm run generate:modules` after editing.

```json
"{name}": {
  "displayName": "{Display Name}",
  "version": "1.0.0",
  "supportedPlatforms": ["backend", "web"],
  "backendModule": "Starter.Module.{Name}",
  "frontendFeature": "{name}",
  "configKey": "{name}",
  "required": false,
  "dependencies": [],
  "description": "Brief description of what this module provides"
}
```

> **Do NOT hand-edit** `src/config/modules.config.ts`, `src/config/modules.generated.ts`, `boilerplateBE/src/Starter.Api/Modularity/ModuleRegistry.g.cs`, `boilerplateMobile/lib/app/modules.config.dart`, or `eslint.config.modules.json`. They are regenerated from the catalog by `npm run generate:modules`. CI fails on drift.

5. **`src/config/api.config.ts`** — Add endpoint constants:
```typescript
{NAME}: {
  LIST: '/{Entities}',
  DETAIL: (id: string) => `/{Entities}/${id}`,
  // ... action endpoints
},
```

6. **`src/config/routes.config.ts`** — Add route paths:
```typescript
{NAME}: {
  LIST: '/{entities}',
  CREATE: '/{entities}/new',
  DETAIL: '/{entities}/:id',
  getDetail: (id: string) => `/{entities}/${id}`,
},
```

7. **`src/routes/routes.tsx`** — Add lazy imports + route entries with `activeModules.{name}` guard and `PermissionGuard`

8. **`src/constants/permissions.ts`** — Mirror backend permissions:
```typescript
{Name}: {
  View: '{Name}.View',
  Create: '{Name}.Create',
  Update: '{Name}.Update',
},
```

9. **`src/lib/query/keys.ts`** — Add query key structure:
```typescript
{name}: {
  all: ['{name}'] as const,
  lists: () => ['{name}', 'list'] as const,
  list: (params?: Record<string, unknown>) => ['{name}', 'list', params] as const,
  details: () => ['{name}', 'detail'] as const,
  detail: (id: string) => ['{name}', 'detail', id] as const,
},
```

10. **`src/components/layout/MainLayout/Sidebar.tsx`** — Add nav item with `activeModules.{name}` + `hasPermission()` guard

11. **`src/lib/extensions/slot-map.ts`** — Add new slot types if contributing to a new slot (not needed if only using existing slots like `tenant-detail-tabs`)

**Checkpoint:** `npm run build` passes with 0 errors.

---

## Boundary Rules (CRITICAL)

### Backend — Never Cross These Lines

| Rule | Details |
|------|---------|
| **No direct module-to-module references** | Never `using Starter.Module.Billing` from Products. Use abstraction interfaces. |
| **No core project references** | Module `.csproj` only references `Starter.Abstractions.Web` (always) and `Starter.Abstractions.Messaging` (only if implementing `IModuleBusContributor`). Never `Starter.Application`, `Starter.Infrastructure`, `Starter.Domain`, `Starter.Abstractions` directly — those flow transitively. |
| **No shared DbContext** | Each module has its own `DbContext`. Never inject or reference `ApplicationDbContext`. |
| **Cross-module data via Readers** | Need tenant names? Use `ITenantReader`. Need user info? Use `IUserReader`. Never join across contexts. |
| **Cross-module side effects via Capabilities** | Need webhooks? Use `IWebhookPublisher`. Need quota check? Use `IQuotaChecker`. These are null-safe (no-op if provider module not installed). |
| **Events via MassTransit** | Consume events from core domain. Never call another module's handler directly. |
| **Multi-tenancy via query filters** | Always configure tenant query filter in `OnModelCreating`. Use `IgnoreQueryFilters()` only for explicit cross-tenant checks (uniqueness, idempotency). |

### Frontend — Pragmatic Coupling

| Rule | Details |
|------|---------|
| **Feature folder isolation** | All module files under `src/features/{name}/`. |
| **Core feature imports OK** | Importing hooks from `@/features/tenants/api` or `@/features/files/api` is acceptable — these are core (non-optional) features. |
| **No module-to-module imports** | Never import from `@/features/billing/` in a products component. Use slot system instead. |
| **Slot system for cross-module UI** | Contribute UI to other pages via `registerSlot()`, not direct component imports. |
| **Conditional rendering** | Routes, sidebar items, and slot entries are gated by `activeModules.{name}` and `hasPermission()`. |

---

## Available Abstractions Reference

### Capabilities (Optional — null-safe fallbacks)

| Interface | Method | Purpose |
|-----------|--------|---------|
| `IQuotaChecker` | `CheckAsync(tenantId, metric)` | Pre-create quota validation |
| `IQuotaChecker` | `IncrementAsync(tenantId, metric)` | Post-create quota tracking |
| `IWebhookPublisher` | `PublishAsync(event, tenantId, data)` | Emit webhook events |
| `IBillingProvider` | `CreateSubscriptionAsync(...)` | Payment integration |
| `IUsageMetricCalculator` | `CalculateAsync(tenantId)` | Metric value computation |
| `IImportExportRegistry` | `Register(definition)` | Import/export support |

### Readers (Cross-Context Data Access)

| Interface | Returns | Purpose |
|-----------|---------|---------|
| `ITenantReader` | `TenantSummary` (Id, Name, Status) | Get tenant names for DTOs |
| `IUserReader` | `UserSummary` (Id, Username, Email, DisplayName) | Get user info |
| `IRoleReader` | `RoleSummary` (Id, Name, TenantId) | Get role info |

### Core Services (Always Available)

| Interface | Purpose |
|-----------|---------|
| `ICurrentUserService` | Current user's Id, TenantId, Email, Permissions |
| `IFileService` | Upload, download (signed URLs), delete files |
| `IUsageTracker` | Get/set/increment Redis-backed usage counters |

### Frontend Extension Points

| System | Functions | Purpose |
|--------|-----------|---------|
| Slots | `registerSlot()`, `getSlotEntries()`, `hasSlotEntries()` | Multi-component extension points |
| Capabilities | `registerCapability()`, `getCapability()` | Single-function service registration |
| `<Slot>` component | `<Slot id="..." props={...} />` | Render registered slot entries with permission filtering |

### Available Slot IDs

| Slot ID | Props | Used By |
|---------|-------|---------|
| `tenant-detail-tabs` | `{ tenantId, tenantName }` | Billing (subscription), Products (tenant products) |
| `dashboard-cards` | `Record<string, never>` | Products (active count) |
| `users-list-toolbar` | `{ onRefresh }` | ImportExport (import button) |

---

## UI/UX Checklist

- [ ] **PageHeader** — Use `PageHeader` component with title, subtitle, and action buttons
- [ ] **Back navigation** — Use `useBackNavigation(path, label)` on detail/create pages
- [ ] **Empty states** — Use `<EmptyState>` component with icon, title, description, and action
- [ ] **Pagination** — Use `<Pagination>` component. Initialize page size with `getPersistedPageSize()`
- [ ] **Tables** — Use `<Table>` component (has built-in card styling, don't wrap in Card)
- [ ] **Loading states** — Show loading indicator while data fetches
- [ ] **Search** — Debounced search input that resets page to 1
- [ ] **Status badges** — Use `<Badge>` with variant mapping (default=Active, secondary=Draft, outline=Archived)
- [ ] **Confirm dialogs** — Use `<ConfirmDialog>` for destructive actions (archive, delete)
- [ ] **Toast notifications** — Use `toast.success()` on mutation success
- [ ] **Form validation** — Zod schema + `zodResolver` + react-hook-form
- [ ] **RTL support** — Use `ltr:/rtl:` prefixes for directional padding/margins. Use `text-start` not `text-left`.
- [ ] **i18n** — All user-facing strings via `t()` with default fallbacks
- [ ] **Semantic colors** — Use `bg-primary`, `text-primary`, `bg-primary/10` — never hardcode color shades
- [ ] **Platform admin UI** — Show tenant column/filter in lists when `!user?.tenantId`
- [ ] **Permission guards** — Gate create/edit/delete buttons with `hasPermission()`
- [ ] **Detail page pattern** — Wrapper loads data, inner form renders with real defaults (avoids StrictMode timing issues)
- [ ] **File uploads** — Hidden `<input type="file">` triggered by button click, show preview after upload
- [ ] **Image display** — Use `useFileUrl(fileId)` from `@/features/files/api` for signed URL resolution

---

## Killer Test (Validation)

After completing the module, run these checks:

1. **Zero files outside module folders** — `git diff --stat` should only show files inside `src/modules/Starter.Module.{Name}/` and `src/features/{name}/` plus the allowed bootstrap files
2. **`dotnet build`** — 0 warnings, 0 errors
3. **`npm run build`** — 0 errors
4. **`rename.ps1 -Modules All`** — Build succeeds with all modules
5. **`rename.ps1 -Modules None`** — Build succeeds with zero modules
6. **`rename.ps1 -Modules {name}`** — Build succeeds with only this module
7. **Smoke test** — Register tenant -> seed data appears, CRUD works, image upload works
8. **Quota test** — If using quotas, limit to N, verify N+1 fails

---

## Common Pitfalls

| Pitfall | Prevention |
|---------|-----------|
| Module references core Domain/Application | Only reference `Starter.Abstractions.Web` |
| Shared migration table | Use `__EFMigrationsHistory_{Name}` |
| Missing tenant query filter | Always add in `OnModelCreating` |
| N+1 on tenant names | Batch-load via `ITenantReader.GetManyAsync()` |
| useForm not syncing with data | Use wrapper/inner component pattern for detail pages |
| Radix Select showing empty | Ensure `value` prop matches a `SelectItem` value exactly |
| Dashboard card hardcoded colors | Use `bg-primary/10` + `text-primary` semantic tokens |
| File URL not resolving | Use `useFileUrl(id)` with `enabled: !!id` guard |
| Slug uniqueness scoped wrong | Check with `IgnoreQueryFilters()` + `TenantId == tenantId` |
| Event handler runs twice | Always check idempotency with `AnyAsync()` before creating |
| Module not in `ModuleRegistry.All()` | Add the entry to `modules.catalog.json` and run `npm run generate:modules`. The CI drift gate fails if you forget. |
| Module's consumers don't fire at runtime | Module ships `IConsumer<T>` but doesn't implement `IModuleBusContributor` + `bus.AddConsumers(typeof({Name}Module).Assembly)`. Architecture test `ModuleRegistryTests.Modules_with_MassTransit_consumers_implement_IModuleBusContributor` catches this. |
| Routes 404 after module disabled | Replace lazy imports with `NullPage` stub in `routes.tsx` |
