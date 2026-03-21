namespace Starter.Application.Common.Interfaces;

public interface INotificationService
{
    Task CreateAsync(Guid userId, Guid? tenantId, string type, string title, string message, string? data = null, CancellationToken ct = default);
    Task CreateForTenantAdminsAsync(Guid tenantId, string type, string title, string message, string? data = null, CancellationToken ct = default);
}
