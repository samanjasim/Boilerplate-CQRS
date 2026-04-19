namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Read-only check for per-user notification preferences. Used by modules
/// (e.g. CommentsActivity) to decide whether to dispatch email notifications
/// without coupling to <c>Starter.Application</c> or <c>IApplicationDbContext</c>.
///
/// Default when no preference row exists: <c>true</c> (opt-out semantics —
/// high-signal notifications like mentions default to enabled).
/// </summary>
public interface INotificationPreferenceReader : ICapability
{
    Task<bool> IsEmailEnabledAsync(Guid userId, string notificationType, CancellationToken ct = default);
}
