using Starter.Application.Common.Models;

namespace Starter.Application.Common.Interfaces;

public interface IExportDataProvider
{
    Task<ExportDataResult> GetDataAsync(Guid? tenantId, string? filtersJson, CancellationToken ct = default);
}
