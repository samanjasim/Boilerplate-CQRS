using Starter.Domain.ImportExport.Enums;

namespace Starter.Application.Common.Models;

public sealed record EntityImportExportDefinition(
    string EntityType, string DisplayNameKey, bool SupportsExport, bool SupportsImport,
    string[] ConflictKeys, FieldDefinition[] Fields,
    Type? ExportDataProviderType, Type? ImportRowProcessorType,
    bool RequiresTenant = false);
