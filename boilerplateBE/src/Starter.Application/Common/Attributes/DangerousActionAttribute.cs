namespace Starter.Application.Common.Attributes;

/// <summary>
/// Marks a MediatR command (or any IRequest type) as a destructive action that must
/// require human approval when invoked from an agent runtime. The check is performed
/// inside AgentToolDispatcher; non-agent send paths ignore the attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DangerousActionAttribute : Attribute
{
    public string? Reason { get; }
    public DangerousActionAttribute(string? reason = null) => Reason = reason;
}
