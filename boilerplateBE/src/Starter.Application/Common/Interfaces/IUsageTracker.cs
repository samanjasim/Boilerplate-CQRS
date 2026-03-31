namespace Starter.Application.Common.Interfaces;

public interface IUsageTracker
{
    Task<long> GetAsync(Guid tenantId, string metric, CancellationToken ct = default);
    Task IncrementAsync(Guid tenantId, string metric, long amount = 1, CancellationToken ct = default);
    Task DecrementAsync(Guid tenantId, string metric, long amount = 1, CancellationToken ct = default);
    Task SetAsync(Guid tenantId, string metric, long value, CancellationToken ct = default);
    Task<Dictionary<string, long>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
}
