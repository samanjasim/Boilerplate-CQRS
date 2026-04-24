namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Excludes a property from the auto-derived JSON Schema of a tool. Apply to fields that
/// exist on the command for non-LLM callers (e.g., superadmin cross-tenant TenantId
/// override) but must not be set by the LLM. The property is left on the type and
/// unchanged during non-LLM dispatch.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class AiParameterIgnoreAttribute : Attribute;
