using Starter.Application.Common.Models;
using Starter.Domain.ImportExport.Enums;

namespace Starter.Application.Features.ImportExport.Definitions;

public static class RoleImportExportDefinition
{
    public static EntityImportExportDefinition Create() =>
        new(
            EntityType: "Roles",
            DisplayNameKey: "importExport.entityTypes.roles",
            SupportsExport: true,
            SupportsImport: true,
            ConflictKeys: ["Name"],
            Fields:
            [
                new FieldDefinition("Name", "Name", FieldType.String, Required: true, MaxLength: 200),
                new FieldDefinition("Description", "Description", FieldType.String, MaxLength: 500),
                new FieldDefinition("IsActive", "Is Active", FieldType.Boolean, ExportOnly: true)
            ],
            ExportDataProviderType: typeof(RoleExportDataProvider),
            ImportRowProcessorType: typeof(RoleImportRowProcessor));
}
