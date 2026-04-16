namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Notifies the Communication module that a domain event has occurred,
/// triggering evaluation of tenant-configured trigger rules.
///
/// This is the primary integration point for other modules. When an event
/// fires in your module (e.g., "leave.approved", "order.shipped"), call
/// <see cref="NotifyAsync"/> with the event name and data. The Communication
/// module will:
/// 1. Query matching trigger rules for the tenant
/// 2. Resolve the recipient and message template
/// 3. Dispatch messages via configured channels (Email, SMS, Push, etc.)
/// 4. Post to configured integrations (Slack, Telegram, etc.)
///
/// When the Communication module is not installed, a Null Object silently
/// logs and returns — callers need no module-awareness.
///
/// Usage:
/// <code>
/// await communicationEventNotifier.NotifyAsync(
///     "leave.approved", tenantId, employeeUserId,
///     new Dictionary&lt;string, object&gt;
///     {
///         ["employeeName"] = "Jane Smith",
///         ["leaveType"] = "Annual",
///         ["startDate"] = "Jan 15, 2026",
///     }, ct);
/// </code>
/// </summary>
public interface ICommunicationEventNotifier : ICapability
{
    /// <summary>
    /// Evaluate trigger rules for the given event and dispatch messages
    /// to matching templates, channels, and integrations.
    /// </summary>
    /// <param name="eventName">Event identifier matching EventRegistration.EventName (e.g., "leave.approved")</param>
    /// <param name="tenantId">The tenant context for rule evaluation</param>
    /// <param name="actorUserId">The user who triggered the event (used for recipient resolution when RecipientMode is "event_user")</param>
    /// <param name="eventData">Variables passed to the message template for rendering</param>
    /// <param name="ct">Cancellation token</param>
    Task NotifyAsync(
        string eventName,
        Guid tenantId,
        Guid? actorUserId,
        Dictionary<string, object> eventData,
        CancellationToken ct = default);
}
