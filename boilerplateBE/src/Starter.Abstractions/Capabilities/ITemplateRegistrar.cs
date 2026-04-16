namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Registers message templates and event definitions with the Communication module.
///
/// Other modules call this during <c>SeedDataAsync</c> to register their
/// templates and events without referencing the Communication module directly.
///
/// When the Communication module is not installed, a Null Object silently
/// returns — no registration occurs, and no error is thrown.
///
/// Usage in a module's SeedDataAsync:
/// <code>
/// var registrar = scope.ServiceProvider.GetRequiredService&lt;ITemplateRegistrar&gt;();
///
/// await registrar.RegisterEventAsync("leave.approved", "Leave", "Leave Approved",
///     "Triggered when a leave request is approved", ct);
///
/// await registrar.RegisterTemplateAsync("leave.approved", "Leave", "Leave Management",
///     "Notification when leave is approved",
///     "Your {{leaveType}} leave has been approved",
///     "Hi {{employeeName}}, your leave from {{startDate}} to {{endDate}} is approved.",
///     NotificationChannelType.Email, ["Email", "Push", "InApp"],
///     variableSchema: new() { ["employeeName"] = "Employee name", ... },
///     ct: ct);
/// </code>
/// </summary>
public interface ITemplateRegistrar : ICapability
{
    /// <summary>
    /// Register a message template. Idempotent — skips if a template with the same name already exists.
    /// </summary>
    Task RegisterTemplateAsync(
        string name,
        string moduleSource,
        string category,
        string? description,
        string? subjectTemplate,
        string bodyTemplate,
        NotificationChannelType defaultChannel,
        string[] availableChannels,
        Dictionary<string, string>? variableSchema = null,
        Dictionary<string, object>? sampleVariables = null,
        CancellationToken ct = default);

    /// <summary>
    /// Register an event definition (metadata for the Trigger Rules UI). Idempotent — skips if already registered.
    /// </summary>
    Task RegisterEventAsync(
        string eventName,
        string moduleSource,
        string displayName,
        string? description = null,
        CancellationToken ct = default);
}
