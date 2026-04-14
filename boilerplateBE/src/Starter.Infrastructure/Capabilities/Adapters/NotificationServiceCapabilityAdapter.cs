using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Capabilities.Adapters;

/// <summary>
/// Forwards <see cref="INotificationServiceCapability"/> calls to the host's
/// <see cref="INotificationService"/>. Lets modules raise notifications without
/// taking a compile-time dependency on the host's notification feature.
/// </summary>
internal sealed class NotificationServiceCapabilityAdapter(
    INotificationService inner) : INotificationServiceCapability
{
    public Task CreateAsync(
        Guid userId,
        Guid? tenantId,
        string type,
        string title,
        string message,
        string? data = null,
        CancellationToken cancellationToken = default) =>
        inner.CreateAsync(userId, tenantId, type, title, message, data, cancellationToken);

    public Task CreateForTenantAdminsAsync(
        Guid tenantId,
        string type,
        string title,
        string message,
        string? data = null,
        CancellationToken cancellationToken = default) =>
        inner.CreateForTenantAdminsAsync(tenantId, type, title, message, data, cancellationToken);
}
