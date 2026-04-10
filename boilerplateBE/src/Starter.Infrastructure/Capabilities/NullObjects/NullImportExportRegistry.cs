using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

/// <summary>
/// Null implementation of <see cref="IImportExportRegistry"/> registered when
/// the ImportExport module is not installed. Returns no entity types, which
/// makes the import/export UI render as empty rather than crash.
/// </summary>
public sealed class NullImportExportRegistry : IImportExportRegistry
{
    public EntityImportExportDefinition? GetDefinition(string entityType) => null;

    public IReadOnlyList<EntityImportExportDefinition> GetAll() => [];

    public IReadOnlyList<string> GetExportableTypes() => [];

    public IReadOnlyList<string> GetImportableTypes() => [];
}
