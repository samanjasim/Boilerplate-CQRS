namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Describes an entity type that supports import and/or export via the
/// ImportExport module. Registered by the module at startup via
/// <see cref="IImportExportRegistry"/>.
/// </summary>
public sealed record EntityImportExportDefinition(
    string EntityType, string DisplayNameKey, bool SupportsExport, bool SupportsImport,
    string[] ConflictKeys, FieldDefinition[] Fields,
    Type? ExportDataProviderType, Type? ImportRowProcessorType,
    bool RequiresTenant = false);
