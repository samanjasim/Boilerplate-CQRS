using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

/// <summary>
/// Null implementation of <see cref="INotificationServiceCapability"/> registered
/// as a fallback. The host always overrides this with
/// <see cref="Adapters.NotificationServiceCapabilityAdapter"/>, but the null
/// version exists so module code can be exercised in isolation (tests, headless
/// scenarios) without an active notification service.
/// </summary>
public sealed class NullNotificationServiceCapability(
    ILogger<NullNotificationServiceCapability> logger) : INotificationServiceCapability
{
    public Task CreateAsync(
        Guid userId,
        Guid? tenantId,
        string type,
        string title,
        string message,
        string? data = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Notification create skipped — notification capability not bound (user: {UserId}, type: {Type})",
            userId, type);
        return Task.CompletedTask;
    }

    public Task CreateForTenantAdminsAsync(
        Guid tenantId,
        string type,
        string title,
        string message,
        string? data = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Notification create-for-tenant-admins skipped — notification capability not bound (tenant: {TenantId}, type: {Type})",
            tenantId, type);
        return Task.CompletedTask;
    }
}
