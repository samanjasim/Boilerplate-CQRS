# Import/Export System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a registry-based import/export system where adding support for a new entity is a single definition class, with async import processing via MassTransit, per-row error tracking, and real-time progress notifications.

**Architecture:** A central `IImportExportRegistry` defines field schemas per entity type. Exports extend the existing `GenerateReportConsumer` pipeline by resolving `IExportDataProvider` from the registry. Imports use a new `ImportJob` entity + `ProcessImportConsumer` that streams CSV row-by-row, validates against registry schemas, and supports skip/upsert conflict modes with batch commits.

**Tech Stack:** .NET 10, EF Core, PostgreSQL, MassTransit/RabbitMQ, React 19, TanStack Query, shadcn/ui

**Spec:** `docs/superpowers/specs/2026-04-03-import-export-design.md`

---

## File Structure

### Backend — New Files

```
boilerplateBE/src/Starter.Domain/ImportExport/
├── Entities/
│   └── ImportJob.cs
├── Enums/
│   ├── ImportJobStatus.cs
│   ├── ConflictMode.cs
│   ├── ImportRowStatus.cs
│   └── FieldType.cs
└── Errors/
    └── ImportExportErrors.cs

boilerplateBE/src/Starter.Application/Common/
├── Interfaces/
│   ├── IImportExportRegistry.cs
│   ├── IExportDataProvider.cs
│   └── IImportRowProcessor.cs
├── Messages/
│   └── ProcessImportMessage.cs
└── Models/
    ├── EntityImportExportDefinition.cs
    ├── FieldDefinition.cs
    ├── ExportDataResult.cs
    └── ImportRowResult.cs

boilerplateBE/src/Starter.Application/Features/ImportExport/
├── DTOs/
│   ├── ImportJobDto.cs
│   ├── ImportJobMapper.cs
│   ├── ImportPreviewDto.cs
│   └── EntityTypeDto.cs
├── Commands/
│   ├── StartImport/
│   │   ├── StartImportCommand.cs
│   │   ├── StartImportCommandHandler.cs
│   │   └── StartImportCommandValidator.cs
│   └── DeleteImportJob/
│       ├── DeleteImportJobCommand.cs
│       └── DeleteImportJobCommandHandler.cs
├── Queries/
│   ├── GetEntityTypes/
│   │   ├── GetEntityTypesQuery.cs
│   │   └── GetEntityTypesQueryHandler.cs
│   ├── GetImportTemplate/
│   │   ├── GetImportTemplateQuery.cs
│   │   └── GetImportTemplateQueryHandler.cs
│   ├── PreviewImport/
│   │   ├── PreviewImportQuery.cs
│   │   └── PreviewImportQueryHandler.cs
│   ├── GetImportJobs/
│   │   ├── GetImportJobsQuery.cs
│   │   └── GetImportJobsQueryHandler.cs
│   ├── GetImportJobById/
│   │   ├── GetImportJobByIdQuery.cs
│   │   └── GetImportJobByIdQueryHandler.cs
│   └── GetImportErrorReport/
│       ├── GetImportErrorReportQuery.cs
│       └── GetImportErrorReportQueryHandler.cs
└── Definitions/
    ├── UserImportExportDefinition.cs
    ├── UserExportDataProvider.cs
    ├── UserImportRowProcessor.cs
    ├── RoleImportExportDefinition.cs
    ├── RoleExportDataProvider.cs
    └── RoleImportRowProcessor.cs

boilerplateBE/src/Starter.Infrastructure/
├── Persistence/Configurations/
│   └── ImportJobConfiguration.cs
├── Consumers/
│   └── ProcessImportConsumer.cs
└── Services/
    └── ImportExportRegistry.cs

boilerplateBE/src/Starter.Api/Controllers/
└── ImportExportController.cs
```

### Backend — Modified Files

```
boilerplateBE/src/Starter.Application/Common/Interfaces/IApplicationDbContext.cs    (+1 DbSet)
boilerplateBE/src/Starter.Infrastructure/Persistence/ApplicationDbContext.cs         (+1 DbSet, +1 query filter)
boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs                     (+consumer, +registry, +retry)
boilerplateBE/src/Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs            (+import flags, +plan features)
boilerplateBE/src/Starter.Shared/Constants/Permissions.cs                           (+System.ImportData)
boilerplateBE/src/Starter.Shared/Constants/Roles.cs                                 (+ImportData to Admin)
```

### Frontend — New Files

```
boilerplateFE/src/types/importExport.types.ts
boilerplateFE/src/features/import-export/
├── api/
│   ├── importExport.api.ts
│   ├── importExport.queries.ts
│   └── index.ts
├── pages/
│   └── ImportExportPage.tsx
├── components/
│   ├── ImportsTab.tsx
│   ├── ExportsTab.tsx
│   ├── ImportWizard.tsx
│   └── ImportProgressCard.tsx
└── index.ts
```

### Frontend — Modified Files

```
boilerplateFE/src/config/api.config.ts
boilerplateFE/src/config/routes.config.ts
boilerplateFE/src/routes/routes.tsx
boilerplateFE/src/constants/permissions.ts
boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx
boilerplateFE/src/lib/query/keys.ts
boilerplateFE/src/i18n/locales/{en,ar,ku}/translation.json
```

---

## Task 1: Domain — Enums, ImportJob Entity, Errors

**Files:**
- Create: `boilerplateBE/src/Starter.Domain/ImportExport/Enums/ImportJobStatus.cs`
- Create: `boilerplateBE/src/Starter.Domain/ImportExport/Enums/ConflictMode.cs`
- Create: `boilerplateBE/src/Starter.Domain/ImportExport/Enums/ImportRowStatus.cs`
- Create: `boilerplateBE/src/Starter.Domain/ImportExport/Enums/FieldType.cs`
- Create: `boilerplateBE/src/Starter.Domain/ImportExport/Entities/ImportJob.cs`
- Create: `boilerplateBE/src/Starter.Domain/ImportExport/Errors/ImportExportErrors.cs`

- [ ] **Step 1:** Create enums

```csharp
// ImportJobStatus.cs
namespace Starter.Domain.ImportExport.Enums;
public enum ImportJobStatus { Pending = 0, Validating = 1, Processing = 2, Completed = 3, PartialSuccess = 4, Failed = 5 }

// ConflictMode.cs
namespace Starter.Domain.ImportExport.Enums;
public enum ConflictMode { Skip = 0, Upsert = 1 }

// ImportRowStatus.cs
namespace Starter.Domain.ImportExport.Enums;
public enum ImportRowStatus { Created = 0, Updated = 1, Skipped = 2, Failed = 3 }

// FieldType.cs
namespace Starter.Domain.ImportExport.Enums;
public enum FieldType { String = 0, Integer = 1, Decimal = 2, Boolean = 3, DateTime = 4, Enum = 5, Email = 6 }
```

- [ ] **Step 2:** Create `ImportJob.cs`

AggregateRoot with TenantId. Properties: EntityType, FileName, FileId, ConflictMode, Status (ImportJobStatus), TotalRows, ProcessedRows, CreatedCount, UpdatedCount, SkippedCount, FailedCount, ResultsFileId (Guid?), ErrorMessage, RequestedBy, StartedAt, CompletedAt.

Factory: `Create(tenantId, entityType, fileName, fileId, conflictMode, requestedBy)` sets Status=Pending.

State methods: `MarkValidating()`, `MarkProcessing()`, `UpdateProgress(processedRows, created, updated, skipped, failed)`, `MarkCompleted(resultsFileId?)`, `MarkPartialSuccess(resultsFileId)`, `MarkFailed(errorMessage)`.

Follow exact pattern from `WebhookEndpoint.cs` and `ReportRequest`.

- [ ] **Step 3:** Create `ImportExportErrors.cs`

```csharp
using Starter.Shared.Results;
namespace Starter.Domain.ImportExport.Errors;

public static class ImportExportErrors
{
    public static readonly Error JobNotFound = Error.NotFound("ImportExport.JobNotFound", "The specified import job was not found.");
    public static readonly Error EntityTypeNotFound = Error.NotFound("ImportExport.EntityTypeNotFound", "The specified entity type is not registered for import/export.");
    public static readonly Error ImportNotSupported = Error.Validation("ImportExport.ImportNotSupported", "Import is not supported for this entity type.");
    public static readonly Error ExportNotSupported = Error.Validation("ImportExport.ExportNotSupported", "Export is not supported for this entity type.");
    public static readonly Error ImportsDisabled = Error.Validation("ImportExport.ImportsDisabled", "Imports are not enabled for your plan.");
    public static readonly Error InvalidCsvFormat = Error.Validation("ImportExport.InvalidCsvFormat", "The uploaded file is not a valid CSV.");
    public static readonly Error HeaderMismatch = Error.Validation("ImportExport.HeaderMismatch", "CSV headers do not match the expected format for this entity type.");
    public static readonly Error FileNotFound = Error.NotFound("ImportExport.FileNotFound", "The uploaded file was not found.");
    public static Error RowLimitExceeded(int limit) => Error.Validation("ImportExport.RowLimitExceeded", $"Import exceeds the maximum of {limit} rows for your plan.");
}
```

- [ ] **Step 4:** Verify build, commit

```
feat(domain): add ImportJob entity, enums, and errors for import/export system
```

---

## Task 2: Registry Interfaces + Models

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Common/Interfaces/IImportExportRegistry.cs`
- Create: `boilerplateBE/src/Starter.Application/Common/Interfaces/IExportDataProvider.cs`
- Create: `boilerplateBE/src/Starter.Application/Common/Interfaces/IImportRowProcessor.cs`
- Create: `boilerplateBE/src/Starter.Application/Common/Models/EntityImportExportDefinition.cs`
- Create: `boilerplateBE/src/Starter.Application/Common/Models/FieldDefinition.cs`
- Create: `boilerplateBE/src/Starter.Application/Common/Models/ExportDataResult.cs`
- Create: `boilerplateBE/src/Starter.Application/Common/Models/ImportRowResult.cs`
- Create: `boilerplateBE/src/Starter.Application/Common/Messages/ProcessImportMessage.cs`

- [ ] **Step 1:** Create registry interface

```csharp
namespace Starter.Application.Common.Interfaces;

public interface IImportExportRegistry
{
    EntityImportExportDefinition? GetDefinition(string entityType);
    IReadOnlyList<EntityImportExportDefinition> GetAll();
    IReadOnlyList<string> GetExportableTypes();
    IReadOnlyList<string> GetImportableTypes();
}
```

- [ ] **Step 2:** Create data provider and row processor interfaces

```csharp
// IExportDataProvider.cs
namespace Starter.Application.Common.Interfaces;
public interface IExportDataProvider
{
    Task<ExportDataResult> GetDataAsync(Guid? tenantId, string? filtersJson, CancellationToken ct = default);
}

// IImportRowProcessor.cs
namespace Starter.Application.Common.Interfaces;
public interface IImportRowProcessor
{
    Task<ImportRowResult> ProcessRowAsync(Dictionary<string, string> row, ConflictMode conflictMode, Guid tenantId, CancellationToken ct = default);
}
```

- [ ] **Step 3:** Create model records

```csharp
// EntityImportExportDefinition.cs
public sealed record EntityImportExportDefinition(
    string EntityType, string DisplayNameKey, bool SupportsExport, bool SupportsImport,
    string[] ConflictKeys, FieldDefinition[] Fields,
    Type? ExportDataProviderType, Type? ImportRowProcessorType);

// FieldDefinition.cs
public sealed record FieldDefinition(
    string Name, string DisplayName, FieldType Type, bool Required = false,
    bool ExportOnly = false, bool ImportOnly = false,
    string? ValidationRegex = null, string[]? EnumOptions = null, int? MaxLength = null);

// ExportDataResult.cs
public sealed record ExportDataResult(string[] Headers, IReadOnlyList<string[]> Rows, int TotalCount);

// ImportRowResult.cs
public sealed record ImportRowResult(ImportRowStatus Status, string? EntityId = null, string? ErrorMessage = null);
```

- [ ] **Step 4:** Create `ProcessImportMessage.cs`

```csharp
namespace Starter.Application.Common.Messages;
public sealed record ProcessImportMessage(Guid ImportJobId);
```

- [ ] **Step 5:** Verify build, commit

```
feat(import-export): add registry interfaces, models, and ProcessImportMessage
```

---

## Task 3: EF Configuration + DbContext + Permissions + Seed

**Files:**
- Create: `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/ImportJobConfiguration.cs`
- Modify: `boilerplateBE/src/Starter.Application/Common/Interfaces/IApplicationDbContext.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Persistence/ApplicationDbContext.cs`
- Modify: `boilerplateBE/src/Starter.Shared/Constants/Permissions.cs`
- Modify: `boilerplateBE/src/Starter.Shared/Constants/Roles.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs`
- Modify: `boilerplateFE/src/constants/permissions.ts`

- [ ] **Step 1:** Create `ImportJobConfiguration.cs`

Table: `import_jobs`. Snake_case columns. EntityType max 100, FileName max 500, ErrorMessage max 2000. Indexes: TenantId, (TenantId, Status). Follow WebhookEndpointConfiguration pattern.

- [ ] **Step 2:** Add DbSet to `IApplicationDbContext.cs` and `ApplicationDbContext.cs`

```csharp
DbSet<ImportJob> ImportJobs { get; }
// Query filter:
modelBuilder.Entity<ImportJob>().HasQueryFilter(j => TenantId == null || j.TenantId == TenantId);
```

- [ ] **Step 3:** Add `System.ImportData` permission

In `Permissions.cs` System class:
```csharp
public const string ImportData = "System.ImportData";
```

Add to `GetAllWithMetadata()`:
```csharp
yield return (System.ImportData, "Import data from CSV files", "System");
```

In `Roles.cs`: Admin gets `System.ImportData`. User does not.

In frontend `permissions.ts`:
```typescript
System: { ..., ImportData: 'System.ImportData' }
```

- [ ] **Step 4:** Add import feature flags and plan features to `DataSeeder.cs`

Read existing seed data first. Add 2 feature flags:
```csharp
FeatureFlag.Create("imports.enabled", "Imports Enabled", "Enable data imports", "false", FlagValueType.Boolean, FlagCategory.System, false),
FeatureFlag.Create("imports.max_rows", "Max Import Rows", "Maximum rows per import", "0", FlagValueType.Integer, FlagCategory.System, false),
```

Update plan features JSON: Free (false, 0), Starter (true, 500), Pro (true, 5000), Enterprise (true, 50000).

- [ ] **Step 5:** Verify both builds, commit

```
feat(import-export): add EF config, DbSet, permissions, feature flags, and seed data
```

---

## Task 4: Registry Implementation + Entity Definitions

**Files:**
- Create: `boilerplateBE/src/Starter.Infrastructure/Services/ImportExportRegistry.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/ImportExport/Definitions/UserImportExportDefinition.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/ImportExport/Definitions/UserExportDataProvider.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/ImportExport/Definitions/UserImportRowProcessor.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/ImportExport/Definitions/RoleImportExportDefinition.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/ImportExport/Definitions/RoleExportDataProvider.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/ImportExport/Definitions/RoleImportRowProcessor.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1:** Create `ImportExportRegistry.cs`

Singleton service. Constructor takes `IServiceProvider` for lazy resolution of providers/processors. Stores definitions in a `Dictionary<string, EntityImportExportDefinition>`. Implements `IImportExportRegistry`.

Register definitions via a static `Configure` method called from DI setup, or auto-discover via assembly scanning for `IEntityDefinitionRegistration` marker interface.

- [ ] **Step 2:** Create User definition + providers

**UserImportExportDefinition**: EntityType="Users", ConflictKeys=["Email"], 7 fields (Email, FirstName, LastName, Username required; Status, Roles, CreatedAt export-only).

**UserExportDataProvider**: Implements `IExportDataProvider`. Queries `context.Users` with filters (status, searchTerm). Maps to string[] rows matching field order. Uses `IgnoreQueryFilters()` if tenantId is null (platform admin).

**UserImportRowProcessor**: Implements `IImportRowProcessor`. For each row: validate email format, check uniqueness against existing users, hash a default password. Skip mode: skip if email exists. Upsert mode: update firstName/lastName/username if exists. Creates User via domain factory method.

- [ ] **Step 3:** Create Role definition + providers

**RoleImportExportDefinition**: EntityType="Roles", ConflictKeys=["Name"], fields: Name, Description, IsActive.

**RoleExportDataProvider**: Queries roles. Excludes system roles from export unless platform admin.

**RoleImportRowProcessor**: Creates custom roles (not system roles). Conflict check on Name + TenantId composite.

- [ ] **Step 4:** Register in DI

```csharp
// In DependencyInjection.cs:
services.AddSingleton<IImportExportRegistry>(sp =>
{
    var registry = new ImportExportRegistry(sp);
    new UserImportExportDefinition().Register(registry);
    new RoleImportExportDefinition().Register(registry);
    return registry;
});

// Register providers/processors as scoped
services.AddScoped<UserExportDataProvider>();
services.AddScoped<UserImportRowProcessor>();
services.AddScoped<RoleExportDataProvider>();
services.AddScoped<RoleImportRowProcessor>();
```

- [ ] **Step 5:** Verify build, commit

```
feat(import-export): add registry implementation with User and Role entity definitions
```

---

## Task 5: DTOs + Queries

**Files:** Create under `boilerplateBE/src/Starter.Application/Features/ImportExport/`

- [ ] **Step 1:** Create DTOs

```csharp
// ImportJobDto.cs
public sealed record ImportJobDto(
    Guid Id, string EntityType, string FileName, string ConflictMode,
    string Status, int TotalRows, int ProcessedRows,
    int CreatedCount, int UpdatedCount, int SkippedCount, int FailedCount,
    bool HasErrorReport, string? ErrorMessage,
    DateTime? StartedAt, DateTime? CompletedAt, DateTime CreatedAt);

// ImportPreviewDto.cs
public sealed record ImportPreviewDto(
    string[] Headers, string[][] PreviewRows, string[] ValidationErrors,
    int TotalRowCount, string[] UnrecognizedColumns);

// EntityTypeDto.cs
public sealed record EntityTypeDto(
    string EntityType, string DisplayName, bool SupportsExport, bool SupportsImport,
    string[] Fields);
```

- [ ] **Step 2:** Create queries

**GetEntityTypesQuery** → Returns list of EntityTypeDto from registry.

**GetImportTemplateQuery(EntityType)** → Generates blank CSV with headers from registry (import-capable fields only) + one example row. Returns byte[] as CSV file.

**PreviewImportQuery(FileId, EntityType)** → Downloads file from S3 via IFileService, parses first 5 rows, validates headers against registry, returns ImportPreviewDto.

**GetImportJobsQuery(PageNumber, PageSize)** → Paginated list of ImportJobDto for current tenant.

**GetImportJobByIdQuery(Id)** → Single ImportJobDto.

**GetImportErrorReportQuery(Id)** → Returns signed URL for the error report CSV file.

Each query follows existing patterns (primary constructor injection, IApplicationDbContext, ICurrentUserService).

- [ ] **Step 3:** Verify build, commit

```
feat(import-export): add DTOs and queries (entity types, template, preview, import jobs)
```

---

## Task 6: Commands (StartImport, DeleteImportJob)

**Files:** Create under `boilerplateBE/src/Starter.Application/Features/ImportExport/Commands/`

- [ ] **Step 1:** Create `StartImportCommand` + handler + validator

Command: `record(Guid FileId, string EntityType, ConflictMode ConflictMode) : IRequest<Result<Guid>>`

Handler:
1. Check `imports.enabled` via IFeatureFlagService
2. Check row limit: download file, count rows, compare to `imports.max_rows`
3. Verify entity type exists in registry and supports import
4. Verify file exists (FileId) via IApplicationDbContext
5. Create ImportJob entity (Pending)
6. Publish ProcessImportMessage to MassTransit
7. Return ImportJob.Id

Validator: FileId NotEmpty, EntityType NotEmpty.

- [ ] **Step 2:** Create `DeleteImportJobCommand` + handler

Command: `record(Guid Id) : IRequest<Result<Unit>>`

Handler: Find job, delete associated files (import file + error report if exists), remove job, save.

- [ ] **Step 3:** Verify build, commit

```
feat(import-export): add StartImport and DeleteImportJob commands
```

---

## Task 7: ProcessImportConsumer

**Files:**
- Create: `boilerplateBE/src/Starter.Infrastructure/Consumers/ProcessImportConsumer.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1:** Create `ProcessImportConsumer.cs`

Implements `IConsumer<ProcessImportMessage>`. Uses `IServiceScopeFactory` for scoped access.

**Logic:**
1. Load ImportJob by ID (IgnoreQueryFilters)
2. Mark as Validating → SaveChanges
3. Download CSV from S3 via IStorageService
4. Parse headers, validate against registry definition
5. If header mismatch → MarkFailed, notify, return
6. Mark as Processing → SaveChanges
7. Stream CSV line-by-line (never load full file into memory):
   ```csharp
   using var reader = new StreamReader(stream);
   var headerLine = await reader.ReadLineAsync(ct);
   // ... parse headers
   var batchResults = new List<(int Row, ImportRowResult Result, Dictionary<string,string> Data)>();
   int rowNumber = 0;
   while (await reader.ReadLineAsync(ct) is { } line)
   {
       rowNumber++;
       var fields = ParseCsvLine(line);
       var row = MapToFieldDictionary(headers, fields);
       // Resolve IImportRowProcessor from registry via IServiceProvider
       var result = await processor.ProcessRowAsync(row, conflictMode, tenantId, ct);
       batchResults.Add((rowNumber, result, row));
       if (batchResults.Count >= 50)
       {
           await context.SaveChangesAsync(ct); // Batch commit
           job.UpdateProgress(rowNumber, created, updated, skipped, failed);
           await context.SaveChangesAsync(ct); // Update progress
           batchResults.Clear();
       }
   }
   ```
8. Final SaveChanges for remaining rows
9. If any failed/skipped → generate error report CSV:
   - Same headers + "Status" + "Error" columns
   - Upload as system file via IFileService.CreateSystemFileAsync
   - Store ResultsFileId on ImportJob
10. Mark job Completed, PartialSuccess, or Failed based on counts
11. Notify user via INotificationService

**CSV parsing helper:** Handle quoted fields, commas within quotes, escaped quotes. Simple state machine, not a library.

- [ ] **Step 2:** Register consumer in DI

```csharp
busConfigurator.AddConsumer<ProcessImportConsumer>();

// Specific endpoint with limited retry
cfg.ReceiveEndpoint("process-import", e =>
{
    e.UseMessageRetry(r => r.Interval(1, TimeSpan.FromSeconds(30)));
    e.ConfigureConsumer<ProcessImportConsumer>(context);
});
```

Also add `services.AddHttpClient();` if not already present.

- [ ] **Step 3:** Verify build, commit

```
feat(import-export): add ProcessImportConsumer with streaming CSV, batch commits, and error report
```

---

## Task 8: ImportExportController

**Files:**
- Create: `boilerplateBE/src/Starter.Api/Controllers/ImportExportController.cs`

- [ ] **Step 1:** Create controller

```csharp
public sealed class ImportExportController(ISender mediator) : BaseApiController(mediator)
{
    [HttpGet("types")]
    [Authorize(Policy = Permissions.System.ExportData)]
    // → GetEntityTypesQuery

    [HttpGet("{entityType}/template")]
    [Authorize(Policy = Permissions.System.ImportData)]
    // → GetImportTemplateQuery(entityType) → return File(bytes, "text/csv", filename)

    [HttpPost("preview")]
    [Authorize(Policy = Permissions.System.ImportData)]
    // → PreviewImportQuery(fileId, entityType)

    [HttpPost("import")]
    [Authorize(Policy = Permissions.System.ImportData)]
    // → StartImportCommand(fileId, entityType, conflictMode)

    [HttpGet("imports")]
    [Authorize(Policy = Permissions.System.ImportData)]
    // → GetImportJobsQuery(pageNumber, pageSize) → HandlePagedResult

    [HttpGet("imports/{id:guid}")]
    [Authorize(Policy = Permissions.System.ImportData)]
    // → GetImportJobByIdQuery(id)

    [HttpGet("imports/{id:guid}/errors")]
    [Authorize(Policy = Permissions.System.ImportData)]
    // → GetImportErrorReportQuery(id) → returns signed URL

    [HttpDelete("imports/{id:guid}")]
    [Authorize(Policy = Permissions.System.ImportData)]
    // → DeleteImportJobCommand(id)
}
```

Route: `api/v1/import-export/`

- [ ] **Step 2:** Verify build, commit

```
feat(api): add ImportExportController with all import/export endpoints
```

---

## Task 9: Frontend — Types, API Config, Query Keys

**Files:**
- Create: `boilerplateFE/src/types/importExport.types.ts`
- Modify: `boilerplateFE/src/config/api.config.ts`
- Modify: `boilerplateFE/src/lib/query/keys.ts`

- [ ] **Step 1:** Create types

```typescript
export interface ImportJob {
  id: string; entityType: string; fileName: string; conflictMode: string;
  status: 'Pending' | 'Validating' | 'Processing' | 'Completed' | 'PartialSuccess' | 'Failed';
  totalRows: number; processedRows: number;
  createdCount: number; updatedCount: number; skippedCount: number; failedCount: number;
  hasErrorReport: boolean; errorMessage: string | null;
  startedAt: string | null; completedAt: string | null; createdAt: string;
}

export interface ImportPreview {
  headers: string[]; previewRows: string[][]; validationErrors: string[];
  totalRowCount: number; unrecognizedColumns: string[];
}

export interface EntityType {
  entityType: string; displayName: string;
  supportsExport: boolean; supportsImport: boolean; fields: string[];
}

export interface StartImportData {
  fileId: string; entityType: string; conflictMode: number; // 0=Skip, 1=Upsert
}
```

Export from `types/index.ts`.

- [ ] **Step 2:** Add API endpoints and query keys

API config:
```typescript
IMPORT_EXPORT: {
  TYPES: '/ImportExport/types',
  TEMPLATE: (type: string) => `/ImportExport/${type}/template`,
  PREVIEW: '/ImportExport/preview',
  IMPORT: '/ImportExport/import',
  IMPORTS: '/ImportExport/imports',
  IMPORT_DETAIL: (id: string) => `/ImportExport/imports/${id}`,
  IMPORT_ERRORS: (id: string) => `/ImportExport/imports/${id}/errors`,
},
```

Query keys: `importExport.all`, `importExport.types`, `importExport.imports.list(params)`, `importExport.imports.detail(id)`.

- [ ] **Step 3:** Verify build, commit

```
feat(frontend): add import/export types, API config, and query keys
```

---

## Task 10: Frontend — API Module + Hooks

**Files:**
- Create: `boilerplateFE/src/features/import-export/api/importExport.api.ts`
- Create: `boilerplateFE/src/features/import-export/api/importExport.queries.ts`
- Create: `boilerplateFE/src/features/import-export/api/index.ts`

- [ ] **Step 1:** Create API module

Methods: getEntityTypes, downloadTemplate(entityType), previewImport(fileId, entityType), startImport(data), getImportJobs(params), getImportJobById(id), getImportErrorUrl(id), deleteImportJob(id).

Template download: fetch blob and trigger download via anchor element.

- [ ] **Step 2:** Create React Query hooks

useQuery: useEntityTypes, useImportJobs (with refetchInterval 5s when active), useImportJob(id).
useMutation: useStartImport, useDeleteImportJob, usePreviewImport.

**Import jobs polling pattern (same as reports):**
```typescript
refetchInterval: (query) => {
  const jobs = query.state.data?.data ?? [];
  const hasActive = jobs.some(j => ['Pending','Validating','Processing'].includes(j.status));
  return hasActive ? 5000 : false;
}
```

Also hook into Ably notifications — add `import_completed`, `import_partial`, `import_failed` types to `useAblyNotifications.ts` for query invalidation.

- [ ] **Step 3:** Verify build, commit

```
feat(frontend): add import/export API module and React Query hooks
```

---

## Task 11: Frontend — Routes, Sidebar, i18n

**Files:**
- Modify: `boilerplateFE/src/config/routes.config.ts`
- Modify: `boilerplateFE/src/routes/routes.tsx`
- Modify: `boilerplateFE/src/components/layout/MainLayout/Sidebar.tsx`
- Modify: all 3 translation files

- [ ] **Step 1:** Add route + sidebar

Route: `IMPORT_EXPORT: '/import-export'`

Sidebar: Replace existing "Reports" entry with "Import/Export" using `ArrowLeftRight` icon, gated by `System.ExportData` OR `System.ImportData`.

- [ ] **Step 2:** Add i18n keys to all 3 locales

English `importExport` section: title, subtitle, exports, imports, startImport, uploadFile, selectEntityType, conflictMode, skipDuplicates, updateExisting, preview, previewDescription, confirm, startImportBtn, progress, created, updated, skipped, failed, downloadErrors, downloadTemplate, noImports, noExports, importCompleted, importPartial, importFailed, deleteConfirm, rowsFound, validationErrors, unrecognizedColumns, step1Upload, step2Preview, step3Confirm, step4Progress.

- [ ] **Step 3:** Create placeholder page so build passes

- [ ] **Step 4:** Verify build, commit

```
feat(frontend): add import/export routes, sidebar nav, and i18n translations
```

---

## Task 12: Frontend — ImportExportPage + Components

**Files:**
- Create/Replace: `boilerplateFE/src/features/import-export/pages/ImportExportPage.tsx`
- Create: `boilerplateFE/src/features/import-export/components/ExportsTab.tsx`
- Create: `boilerplateFE/src/features/import-export/components/ImportsTab.tsx`
- Create: `boilerplateFE/src/features/import-export/components/ImportWizard.tsx`
- Create: `boilerplateFE/src/features/import-export/components/ImportProgressCard.tsx`

- [ ] **Step 1:** Create ImportExportPage

Two-tab page (Exports / Imports). Exports tab reuses existing report list logic. Imports tab shows ImportJob list + "Start Import" button.

Read existing FeatureFlagsPage.tsx and ReportsPage.tsx for patterns.

- [ ] **Step 2:** Create ExportsTab

Renders the existing report list table (relocating content from ReportsPage). Uses `useReports()` hook. Shows ExportButton at top for initiating new exports.

- [ ] **Step 3:** Create ImportsTab

Table: Entity Type (badge), File Name, Status (animated badge with color coding), Progress (bar or X/Y), Results (Created/Updated/Skipped/Failed counts), Actions (View, Download Errors, Delete).

"Start Import" button opens ImportWizard dialog.

- [ ] **Step 4:** Create ImportWizard

4-step modal:
1. **Upload**: File drop zone + entity type selector + conflict mode radio (Skip/Upsert). Uses existing file upload API.
2. **Preview**: First 5 rows table, validation error highlights, unrecognized column warnings.
3. **Confirm**: Summary card with row count, entity type, conflict mode.
4. **Progress**: Live progress using ImportProgressCard.

- [ ] **Step 5:** Create ImportProgressCard

Shows progress bar, live counters (Created/Updated/Skipped/Failed), animated status badge. "Download Error Report" button appears on completion if errors exist.

- [ ] **Step 6:** Verify build, commit

```
feat(frontend): add ImportExportPage with exports tab, imports tab, wizard, and progress card
```

---

## Task 13: Build Verification

- [ ] **Step 1:** Full backend build: `dotnet build` — 0 errors
- [ ] **Step 2:** Full frontend build: `npm run build` — 0 errors
- [ ] **Step 3:** Commit if any remaining changes

---

## Execution Notes

- **Do NOT create EF migrations** — only when starting post-feature testing
- **Do NOT mention Claude or Anthropic** in commit messages. No Co-Authored-By tags
- Tasks 1-8 are backend, Tasks 9-12 are frontend, Task 13 is verification
- Task 4 (registry + definitions) is the most architectural — requires careful DI wiring
- Task 7 (ProcessImportConsumer) is the most complex — streaming CSV parser with batch commits
- The consumer needs `IServiceScopeFactory` since MassTransit consumers have different DI lifetime
- `useAblyNotifications` hook needs to be updated in Task 10 to invalidate import queries on notification
- The ExportsTab in Task 12 effectively relocates ReportsPage content — the old /reports route can remain as a redirect or be removed
