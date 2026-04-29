namespace Starter.Application.Common.Interfaces;

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default);
    Task<T> GetValueAsync<T>(string key, CancellationToken cancellationToken = default);
    Task<T> GetValueForTenantAsync<T>(string key, Guid? tenantId, CancellationToken cancellationToken = default);
    Task<Dictionary<string, string>> GetAllResolvedAsync(CancellationToken cancellationToken = default);
    Task<Dictionary<string, string>> GetAllResolvedForTenantAsync(Guid? tenantId, CancellationToken cancellationToken = default);
    Task InvalidateCacheAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);
}
