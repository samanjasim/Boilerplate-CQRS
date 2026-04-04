using Starter.Application.Common.Models;
using Starter.Domain.ImportExport.Enums;

namespace Starter.Application.Features.ImportExport.Definitions;

public static class UserImportExportDefinition
{
    public static EntityImportExportDefinition Create() =>
        new(
            EntityType: "Users",
            DisplayNameKey: "importExport.entityTypes.users",
            SupportsExport: true,
            SupportsImport: true,
            ConflictKeys: ["Email"],
            Fields:
            [
                new FieldDefinition("Email", "Email", FieldType.Email, Required: true),
                new FieldDefinition("FirstName", "First Name", FieldType.String, Required: true, MaxLength: 100),
                new FieldDefinition("LastName", "Last Name", FieldType.String, Required: true, MaxLength: 100),
                new FieldDefinition("Username", "Username", FieldType.String, Required: true, MaxLength: 50),
                new FieldDefinition("Status", "Status", FieldType.Enum, ExportOnly: true,
                    EnumOptions: ["Active", "Suspended", "Deactivated"]),
                new FieldDefinition("Roles", "Roles", FieldType.String, ExportOnly: true),
                new FieldDefinition("CreatedAt", "Created At", FieldType.DateTime, ExportOnly: true)
            ],
            ExportDataProviderType: typeof(UserExportDataProvider),
            ImportRowProcessorType: typeof(UserImportRowProcessor));
}
