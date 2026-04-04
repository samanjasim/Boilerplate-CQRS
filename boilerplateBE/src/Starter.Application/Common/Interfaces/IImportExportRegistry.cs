using Starter.Application.Common.Models;

namespace Starter.Application.Common.Interfaces;

public interface IImportExportRegistry
{
    EntityImportExportDefinition? GetDefinition(string entityType);
    IReadOnlyList<EntityImportExportDefinition> GetAll();
    IReadOnlyList<string> GetExportableTypes();
    IReadOnlyList<string> GetImportableTypes();
}
