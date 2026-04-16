namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Creates persistent in-app notifications (the bell-icon list) and broadcasts
/// the same payload over the realtime channel. Implemented by the host's
/// notification feature; modules consume this capability rather than depending
/// on the host's <c>INotificationService</c> directly so they remain compilable
/// without a reference to <c>Starter.Application</c>.
///
/// A null implementation is registered as a fallback in core, so callers don't
/// need to check for installation.
/// </summary>
public interface INotificationServiceCapability : ICapability
{
    Task CreateAsync(
        Guid userId,
        Guid? tenantId,
        string type,
        string title,
        string message,
        string? data = null,
        CancellationToken cancellationToken = default);

    Task CreateForTenantAdminsAsync(
        Guid tenantId,
        string type,
        string title,
        string message,
        string? data = null,
        CancellationToken cancellationToken = default);
}
