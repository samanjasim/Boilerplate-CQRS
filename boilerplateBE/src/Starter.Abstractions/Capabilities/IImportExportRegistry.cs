namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Registry of entity types that support import/export. The ImportExport
/// module registers definitions at startup; when the module is not installed,
/// a Null Object registered in core returns empty collections so the UI
/// degrades gracefully.
/// </summary>
public interface IImportExportRegistry : ICapability
{
    EntityImportExportDefinition? GetDefinition(string entityType);
    IReadOnlyList<EntityImportExportDefinition> GetAll();
    IReadOnlyList<string> GetExportableTypes();
    IReadOnlyList<string> GetImportableTypes();
}
