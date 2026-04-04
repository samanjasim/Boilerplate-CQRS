using Starter.Application.Common.Models;
using Starter.Domain.ImportExport.Enums;

namespace Starter.Application.Common.Interfaces;

public interface IImportRowProcessor
{
    Task<ImportRowResult> ProcessRowAsync(Dictionary<string, string> row, ConflictMode conflictMode, Guid tenantId, CancellationToken ct = default);
}
