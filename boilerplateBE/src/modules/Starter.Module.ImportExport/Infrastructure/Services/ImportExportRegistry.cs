using Starter.Abstractions.Capabilities;

namespace Starter.Module.ImportExport.Infrastructure.Services;

public sealed class ImportExportRegistry : IImportExportRegistry
{
    private readonly Dictionary<string, EntityImportExportDefinition> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(EntityImportExportDefinition definition)
    {
        _definitions[definition.EntityType] = definition;
    }

    public EntityImportExportDefinition? GetDefinition(string entityType)
    {
        _definitions.TryGetValue(entityType, out var definition);
        return definition;
    }

    public IReadOnlyList<EntityImportExportDefinition> GetAll() =>
        _definitions.Values.ToList().AsReadOnly();

    public IReadOnlyList<string> GetExportableTypes() =>
        _definitions.Values
            .Where(d => d.SupportsExport)
            .Select(d => d.EntityType)
            .ToList()
            .AsReadOnly();

    public IReadOnlyList<string> GetImportableTypes() =>
        _definitions.Values
            .Where(d => d.SupportsImport)
            .Select(d => d.EntityType)
            .ToList()
            .AsReadOnly();
}
