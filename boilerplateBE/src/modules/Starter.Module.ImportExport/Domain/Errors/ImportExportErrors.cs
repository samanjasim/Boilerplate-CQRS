using Starter.Shared.Results;

namespace Starter.Module.ImportExport.Domain.Errors;

public static class ImportExportErrors
{
    public static readonly Error JobNotFound = Error.NotFound(
        "ImportExport.JobNotFound",
        "The specified import job was not found.");

    public static readonly Error EntityTypeNotFound = Error.NotFound(
        "ImportExport.EntityTypeNotFound",
        "The specified entity type was not found.");

    public static readonly Error ImportNotSupported = Error.Validation(
        "ImportExport.ImportNotSupported",
        "Import is not supported for the specified entity type.");

    public static readonly Error ExportNotSupported = Error.Validation(
        "ImportExport.ExportNotSupported",
        "Export is not supported for the specified entity type.");

    public static readonly Error ImportsDisabled = Error.Validation(
        "ImportExport.ImportsDisabled",
        "Imports are not enabled for your plan.");

    public static readonly Error InvalidCsvFormat = Error.Validation(
        "ImportExport.InvalidCsvFormat",
        "The uploaded file is not a valid CSV format.");

    public static readonly Error HeaderMismatch = Error.Validation(
        "ImportExport.HeaderMismatch",
        "The CSV headers do not match the expected columns for this entity type.");

    public static readonly Error FileNotFound = Error.NotFound(
        "ImportExport.FileNotFound",
        "The specified file was not found.");

    public static Error RowLimitExceeded(int limit) =>
        Error.Validation("ImportExport.RowLimitExceeded",
            $"The import file exceeds the maximum allowed row limit ({limit}).");

    public static readonly Error UnauthorizedTenantSelection = Error.Forbidden(
        "Only platform administrators can import into a specific tenant.");

    public static readonly Error TenantNotFound = Error.NotFound(
        "ImportExport.TenantNotFound",
        "The specified tenant was not found.");

    public static readonly Error TenantRequired = Error.Validation(
        "ImportExport.TenantRequired",
        "A target tenant must be selected for this entity type.");
}
