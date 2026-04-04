# Import/Export System — Design Specification

## Overview

A registry-based import/export system where adding support for a new entity is a single registration class. Exports extend the existing async report generation pipeline. Imports use the existing temp file upload → MassTransit processing → notification pattern. Both share a common `IImportExportRegistry` that defines field schemas, validation rules, and conflict resolution keys per entity type.

**Design principle:** Build the mechanism, not the catalog. The system should be entity-agnostic — adding Users export/import and adding Roles export/import should require the same effort: one definition class.

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Export pipeline | Extend existing ReportRequest + GenerateReportConsumer | No new infrastructure — same async pattern, same caching, same download flow |
| Import pipeline | New ImportJob entity + ProcessImportConsumer via MassTransit | Mirrors report pattern. Always async for consistency and scalability |
| File upload for import | Reuse existing `/files/upload-temp` | Temp file gets auto-cleaned by OrphanFileCleanupService if abandoned. Promoted to permanent on success. Zero new upload code |
| Field definitions | Registry pattern (`IImportExportRegistry`) | Single source of truth for both import validation and export column generation. Adding an entity = one class |
| Conflict handling | Configurable per import (Skip or Upsert) | User chooses before starting. Skip = safe default, Upsert = power user option |
| Notification | Ably real-time (when enabled) + in-app notification + polling fallback | Matches existing report notification pattern exactly |
| Formats | CSV for import/export, PDF for export only | CSV is the universal interchange format. PDF for presentation-quality exports. Registry defines both |

## Architecture

### IImportExportRegistry (Central Schema Registry)

```csharp
public interface IImportExportRegistry
{
    void Register<TEntity>(EntityImportExportDefinition definition);
    EntityImportExportDefinition? GetDefinition(string entityType);
    IReadOnlyList<EntityImportExportDefinition> GetAll();
    IReadOnlyList<string> GetExportableTypes();
    IReadOnlyList<string> GetImportableTypes();
}

public sealed record EntityImportExportDefinition
{
    public string EntityType { get; init; }              // "Users", "Roles", etc.
    public string DisplayName { get; init; }             // Localized display name key
    public bool SupportsExport { get; init; }
    public bool SupportsImport { get; init; }
    public string[] ConflictKeys { get; init; }          // Fields for uniqueness check: ["Email"]
    public FieldDefinition[] Fields { get; init; }       // Schema definition
    public Type? ExportDataProvider { get; init; }       // IExportDataProvider<T> implementation
    public Type? ImportRowProcessor { get; init; }       // IImportRowProcessor implementation
}

public sealed record FieldDefinition
{
    public string Name { get; init; }                    // "Email", "FirstName"
    public string DisplayName { get; init; }             // i18n key or label
    public FieldType Type { get; init; }                 // String, Integer, Boolean, DateTime, Enum
    public bool Required { get; init; }
    public bool ExportOnly { get; init; }                // Include in export but not in import template
    public bool ImportOnly { get; init; }                // Include in import but not in export
    public string? ValidationRegex { get; init; }        // Optional regex validation
    public string[]? EnumOptions { get; init; }          // For FieldType.Enum
    public int? MaxLength { get; init; }
}

public enum FieldType { String, Integer, Decimal, Boolean, DateTime, Enum, Email }
```

### Entity Definition Example

```csharp
public sealed class UserImportExportDefinition : IEntityDefinitionRegistration
{
    public void Register(IImportExportRegistry registry)
    {
        registry.Register<User>(new EntityImportExportDefinition
        {
            EntityType = "Users",
            DisplayName = "importExport.entityTypes.users",
            SupportsExport = true,
            SupportsImport = true,
            ConflictKeys = ["Email"],
            ExportDataProvider = typeof(UserExportDataProvider),
            ImportRowProcessor = typeof(UserImportRowProcessor),
            Fields =
            [
                new() { Name = "Email", DisplayName = "Email", Type = FieldType.Email, Required = true },
                new() { Name = "FirstName", DisplayName = "First Name", Type = FieldType.String, Required = true, MaxLength = 100 },
                new() { Name = "LastName", DisplayName = "Last Name", Type = FieldType.String, Required = true, MaxLength = 100 },
                new() { Name = "Username", DisplayName = "Username", Type = FieldType.String, Required = true, MaxLength = 50 },
                new() { Name = "Status", DisplayName = "Status", Type = FieldType.Enum, EnumOptions = ["Active", "Suspended", "Deactivated"], ExportOnly = true },
                new() { Name = "Roles", DisplayName = "Roles", Type = FieldType.String, ExportOnly = true },
                new() { Name = "CreatedAt", DisplayName = "Created", Type = FieldType.DateTime, ExportOnly = true },
            ]
        });
    }
}
```

### Data Provider Interface (Export)

```csharp
public interface IExportDataProvider
{
    Task<ExportDataResult> GetDataAsync(string? filtersJson, CancellationToken ct = default);
}

public sealed record ExportDataResult(
    string[] Headers,
    IReadOnlyList<string[]> Rows,
    int TotalCount);
```

Each entity's data provider queries the database and maps results to flat string arrays matching the field order. The existing `GenerateReportConsumer` resolves the provider from the registry by entity type and calls `GetDataAsync`.

### Row Processor Interface (Import)

```csharp
public interface IImportRowProcessor
{
    Task<ImportRowResult> ProcessRowAsync(
        Dictionary<string, string> row,
        ConflictMode conflictMode,
        Guid tenantId,
        CancellationToken ct = default);
}

public sealed record ImportRowResult(
    ImportRowStatus Status,       // Created, Updated, Skipped, Failed
    string? EntityId,             // ID of created/updated entity
    string? ErrorMessage);        // Reason for skip/failure

public enum ImportRowStatus { Created, Updated, Skipped, Failed }
public enum ConflictMode { Skip, Upsert }
```

Each entity's processor handles one row: validates, checks conflicts, creates or updates. The processor has full access to DI (DbContext, services) and handles the domain logic.

## Export System (Enhanced)

### Changes to Existing Export

The existing `ReportType` enum and `GenerateReportConsumer` are extended, not replaced:

1. **ReportType becomes driven by the registry** — instead of a hardcoded switch in the consumer, it resolves the `IExportDataProvider` from the registry by entity type string.

2. **ExportButton becomes generic** — any page passes `entityType` string + current filters. The button renders if the entity type exists in the registry with `SupportsExport = true`.

3. **Template download endpoint** — `GET /api/v1/import-export/{entityType}/template` returns a blank CSV with headers from the registry (import-capable fields only). Helps users build import files.

### Export Flow (unchanged pattern)

```
1. User clicks ExportButton on any list page
2. Frontend calls POST /api/v1/Reports with { reportType: entityType, format, filters }
3. RequestReportCommandHandler:
   a. Compute FilterHash for caching
   b. Check cache — return existing if fresh
   c. Create ReportRequest (Pending) → publish GenerateReportMessage
4. GenerateReportConsumer:
   a. Resolve IExportDataProvider from registry by entityType
   b. Call provider.GetDataAsync(filters)
   c. Generate CSV or PDF via IExportService
   d. Upload to S3 via IFileService.CreateSystemFileAsync
   e. Mark ReportRequest as Completed
   f. Notify user (in-app + Ably)
5. Frontend shows notification → user downloads via signed URL
```

## Import System (New)

### ImportJob Entity (AggregateRoot, TenantId)

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| TenantId | Guid | FK → Tenant |
| EntityType | string | "Users", "Roles", etc. |
| FileName | string | Original file name |
| FileId | Guid | FK → FileMetadata (temp file) |
| ConflictMode | ConflictMode | Skip or Upsert |
| Status | ImportJobStatus | Pending, Validating, Processing, Completed, PartialSuccess, Failed |
| TotalRows | int | Total rows in CSV (excluding header) |
| ProcessedRows | int | Rows processed so far |
| CreatedCount | int | Successfully created |
| UpdatedCount | int | Successfully updated (upsert mode) |
| SkippedCount | int | Skipped due to conflict |
| FailedCount | int | Failed validation/processing |
| ResultsFileId | Guid? | FK → FileMetadata for error report CSV |
| ErrorMessage | string? | Top-level error (e.g., invalid CSV format) |
| RequestedBy | Guid | User who initiated |
| StartedAt | DateTime? | When processing began |
| CompletedAt | DateTime? | When processing finished |
| CreatedAt | DateTime | |

### ImportJobStatus Enum

```csharp
public enum ImportJobStatus
{
    Pending = 0,        // Queued, not yet picked up
    Validating = 1,     // Checking headers, row count
    Processing = 2,     // Processing rows
    Completed = 3,      // All rows processed successfully
    PartialSuccess = 4, // Some rows failed/skipped
    Failed = 5          // Top-level failure (bad file, wrong entity type)
}
```

### Import Flow

```
1. User navigates to Import/Export page or clicks "Import" on a list page
2. Upload step:
   a. Upload CSV via existing POST /api/v1/Files/upload-temp (returns FileId)
   b. Select entity type (if not pre-selected from list page context)
   c. Select conflict mode: Skip or Upsert
3. Preview step:
   a. Frontend calls GET /api/v1/import-export/preview?fileId={id}&entityType={type}
   b. Backend:
      - Downloads file from S3
      - Parses first 5 rows
      - Validates headers against registry field definitions
      - Returns: headers[], previewRows[], validationErrors[], totalRowCount
   c. Frontend shows preview table with highlighted errors
4. Confirm step:
   a. User reviews preview, clicks "Start Import"
   b. Frontend calls POST /api/v1/import-export/import
      { fileId, entityType, conflictMode }
   c. Backend:
      - Creates ImportJob (Pending)
      - Publishes ProcessImportMessage to MassTransit
      - Returns ImportJob ID
5. Processing (async via MassTransit):
   a. ProcessImportConsumer picks up message
   b. Marks job as Validating → validates full file
   c. Marks job as Processing
   d. For each row:
      - Parse fields against registry definition
      - Validate required fields, types, formats
      - Check conflict keys against existing data
      - If conflict: skip or upsert based on ConflictMode
      - Call IImportRowProcessor.ProcessRowAsync
      - Track result (Created/Updated/Skipped/Failed)
      - Update ProcessedRows counter periodically (every 10 rows)
   e. If any failed/skipped: generate error report CSV
      - Same headers as import + "Status" + "Error" columns
      - Upload as system file, store ResultsFileId on ImportJob
   f. Mark job as Completed, PartialSuccess, or Failed
   g. Notify user (in-app + Ably)
6. Result:
   a. Frontend shows summary: X created, Y updated, Z skipped, W failed
   b. If errors exist: "Download Error Report" button
   c. Error report CSV lets user fix rows and re-import
```

### ProcessImportConsumer

```csharp
public sealed class ProcessImportConsumer : IConsumer<ProcessImportMessage>
```

**Scalability considerations:**
- Processes rows in batches (configurable, default 50) with `SaveChangesAsync` per batch
- Updates `ImportJob.ProcessedRows` every batch (not every row) to reduce DB writes
- Uses `IServiceScopeFactory` for proper DI scope management
- Row processing is sequential within a job (preserves order, simplifies conflict detection)
- Multiple jobs can process concurrently across different tenants
- Memory: streams CSV line-by-line, never loads entire file into memory

**Error handling:**
- Per-row errors are caught and recorded, processing continues
- Top-level errors (file not found, invalid format) fail the entire job
- Timeout: configurable per system setting (default 30 minutes)

### Retry Policy

Import jobs use a different retry policy than webhooks — failures at the job level are not retried (to avoid duplicate inserts). Per-row failures are recorded, not retried.

```csharp
cfg.ReceiveEndpoint("process-import", e =>
{
    e.UseMessageRetry(r => r.Interval(1, TimeSpan.FromSeconds(30)));
    // Only 1 retry at 30s — covers transient DB connection issues
    // Row-level errors are handled inside the consumer, not retried
    e.ConfigureConsumer<ProcessImportConsumer>(context);
});
```

## Notification Strategy

### When Ably is Enabled
1. `ProcessImportConsumer` completes → calls `INotificationService.CreateAsync()`
2. `NotificationService` saves to DB + publishes to Ably channel `user-{userId}`
3. Frontend `useAblyNotifications` hook receives event → invalidates import queries → shows toast

### When Ably is Disabled (Polling Fallback)
1. Frontend `useImportJobs` hook uses `refetchInterval` (same pattern as `useReports`):
   ```typescript
   refetchInterval: (query) => {
     const jobs = query.state.data?.data ?? [];
     const hasActive = jobs.some(j => ['Pending','Validating','Processing'].includes(j.status));
     return hasActive ? 5000 : false; // 5s polling while active
   }
   ```
2. When job status changes to Completed/PartialSuccess/Failed, polling stops

### Notification Types
- `import_completed` — "Your Users import completed. 45 created, 0 skipped."
- `import_partial` — "Your Users import completed with issues. 40 created, 5 skipped. Download error report."
- `import_failed` — "Your Users import failed. Invalid CSV format."

## API Endpoints

| Method | Path | Permission | Purpose |
|--------|------|-----------|---------|
| GET | `/api/v1/import-export/types` | System.ExportData | List available entity types (from registry) |
| GET | `/api/v1/import-export/{entityType}/template` | System.ExportData | Download blank CSV template with headers |
| POST | `/api/v1/import-export/preview` | System.ImportData | Preview CSV: headers, first 5 rows, validation |
| POST | `/api/v1/import-export/import` | System.ImportData | Start import job |
| GET | `/api/v1/import-export/imports` | System.ImportData | List import jobs (paginated) |
| GET | `/api/v1/import-export/imports/{id}` | System.ImportData | Get import job details + progress |
| GET | `/api/v1/import-export/imports/{id}/errors` | System.ImportData | Download error report CSV |
| DELETE | `/api/v1/import-export/imports/{id}` | System.ImportData | Delete import job + associated files |

**Note:** Export uses the existing `/api/v1/Reports` endpoints — no new export endpoints needed.

## Permissions

```csharp
public static class ImportExport
{
    public const string Export = "System.ExportData";     // Already exists
    public const string Import = "System.ImportData";     // NEW
    public const string ForceExport = "System.ForceExport"; // Already exists
}
```

**Role mapping:**
- Admin: Export + Import
- User: Export only (read-only, no data mutation via import)
- SuperAdmin: All (automatic)

## Frontend

### Import/Export Page (`/import-export`)

Dedicated page accessible from sidebar (gated by Export OR Import permission):

**Two tabs:**

**Exports tab** (existing, relocated from /reports):
- Shows ReportRequest list (all export jobs)
- Existing ExportButton + ReportsPage table functionality
- Status badges, download links, delete actions

**Imports tab:**
- Shows ImportJob list with columns:
  - Entity Type (badge)
  - File Name
  - Status (animated badge: Pending=gray, Validating=blue pulse, Processing=blue with progress, Completed=green, PartialSuccess=amber, Failed=red)
  - Progress (X/Y rows — live updating via polling or Ably)
  - Results (Created: X, Updated: Y, Skipped: Z, Failed: W)
  - Actions (View Details, Download Errors, Delete)
- "Start Import" button opens import wizard

### Import Wizard (Modal/Dialog)

**Step 1: Upload**
- Drag-and-drop zone OR file picker (CSV only)
- Entity type selector (dropdown from registry, pre-filled if opened from a list page)
- Conflict mode: radio group — "Skip duplicates" / "Update existing records"
- File validation: size limit, .csv extension, UTF-8 encoding check
- Uses existing `POST /files/upload-temp`

**Step 2: Preview**
- Shows first 5 rows in a table
- Header row highlighted with field type indicators
- Validation errors highlighted in red per cell
- Column mapping verification: "Email → Email ✓", "first_name → FirstName ✓"
- Row count summary: "245 rows found"
- Unrecognized columns shown as warnings (will be ignored)

**Step 3: Confirm**
- Summary card: Entity type, row count, conflict mode, file name
- "Start Import" button
- "Back" to change settings

**Step 4: Progress (after submit)**
- Progress bar: X / Y rows
- Live counters: Created, Updated, Skipped, Failed
- Status transitions animated
- When complete: summary with "Download Error Report" if applicable

### ExportButton Enhancement

The existing `ExportButton` component stays the same but becomes more discoverable. Each list page that has a registered entity type shows the export button:

```typescript
// Any list page:
<ExportButton
  entityType="Users"        // Registry lookup
  filters={currentFilters}  // Current page filters
/>
```

### Sidebar Navigation

```typescript
...(hasPermission(PERMISSIONS.System.ExportData) || hasPermission(PERMISSIONS.System.ImportData)
  ? [{ label: t('nav.importExport'), icon: ArrowLeftRight, path: ROUTES.IMPORT_EXPORT }]
  : []),
```

### UX Details

**Progress feedback:**
- Processing status shows row-level progress bar (e.g., "Processing 127/245")
- Counters update in real-time (Created: 120, Skipped: 7, Failed: 0)
- Pulsing animation on status badge during active processing

**Error report:**
- Downloadable CSV with original data + Status + Error columns
- Each failed/skipped row shows the reason: "Email already exists", "Required field missing: LastName"
- User can fix the error report and re-upload as a new import

**Template download:**
- Blank CSV with column headers matching the registry definition
- Includes one example row with placeholder values
- Column order matches the import parser expectation

## Feature Flag Integration

| Flag | Type | Free | Starter | Pro | Enterprise |
|------|------|------|---------|-----|------------|
| `imports.enabled` | Boolean | false | true | true | true |
| `imports.max_rows` | Integer | 0 | 500 | 5000 | 50000 |

Export uses existing `reports.*` flags. Import adds its own flags for row limits per plan.

## Seed Data

Add to DataSeeder:
- `imports.enabled` (Boolean, default: false, category: System)
- `imports.max_rows` (Integer, default: 0, category: System)

Update plan features JSON:
- Free: imports.enabled=false, imports.max_rows=0
- Starter: imports.enabled=true, imports.max_rows=500
- Pro: imports.enabled=true, imports.max_rows=5000
- Enterprise: imports.enabled=true, imports.max_rows=50000

## Initial Entity Registrations

Ship with these registrations to demonstrate the mechanism:

| Entity | Export | Import | Conflict Key | Notes |
|--------|--------|--------|-------------|-------|
| Users | ✅ | ✅ | Email | Already has export via ReportType.Users |
| Roles | ✅ | ✅ | Name + TenantId | Custom roles only (system roles excluded from import) |

The existing AuditLogs and Files report types are migrated to use the registry pattern. More entities added by downstream projects as needed.

## Performance & Scalability

### Import Processing
- **Streaming CSV parser** — reads line-by-line, never loads full file into memory
- **Batch commits** — `SaveChangesAsync` every 50 rows (configurable), not per row
- **Progress updates** — `ImportJob.ProcessedRows` updated every batch, not every row
- **Concurrent job isolation** — each job processes in its own scope, no shared state
- **Row limit enforcement** — checked before processing starts, not during

### Export Processing
- **Existing caching** — FilterHash prevents redundant generation
- **Streaming generation** — CSV written to stream directly, PDF uses QuestPDF document model
- **S3 signed URLs** — no file data passes through the API on download

### Database
- **No N+1 queries in import** — conflict checks batched per commit batch
- **Bulk operations** where possible (`AddRange` for batch inserts)
- **Usage counter** — `IUsageTracker` incremented in bulk after batch commit

## Testing Checklist

- [ ] Download CSV template for Users entity — correct headers, example row
- [ ] Upload CSV with 10 valid users → all created, progress tracked
- [ ] Upload CSV with duplicate email → skip mode: row skipped with reason
- [ ] Upload CSV with duplicate email → upsert mode: existing user updated
- [ ] Upload CSV with validation errors → failed rows in error report
- [ ] Large import (1000 rows) → progress bar updates smoothly
- [ ] Import job notification via Ably (when enabled)
- [ ] Import job polling fallback (when Ably disabled)
- [ ] Error report download — CSV with original data + Status + Error columns
- [ ] Export button on Users page uses registry → same async flow
- [ ] Template download for Roles entity — correct headers
- [ ] Feature flag gate: Free plan can't import
- [ ] Row limit: Starter plan capped at 500 rows
- [ ] Abandoned import file auto-cleaned by OrphanFileCleanupService
- [ ] Tenant isolation: tenant A can't see tenant B's import jobs
