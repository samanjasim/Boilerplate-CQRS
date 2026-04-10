using Starter.Module.ImportExport.Domain;
using Starter.Module.ImportExport.Domain.Enums;

namespace Starter.Module.ImportExport.Application.Abstractions;

/// <summary>
/// Module-internal contract implemented by per-entity row processors
/// (<c>UserImportRowProcessor</c>, <c>RoleImportRowProcessor</c>). Invoked by
/// <c>ProcessImportConsumer</c> once per row during an import.
///
/// Not a cross-module capability — this interface is private to the
/// ImportExport module and lives here, not in <c>Starter.Abstractions</c>.
/// </summary>
public interface IImportRowProcessor
{
    Task<ImportRowResult> ProcessRowAsync(Dictionary<string, string> row, ConflictMode conflictMode, Guid? tenantId, CancellationToken ct = default);
}
