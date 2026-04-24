namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Optional capability an <see cref="IAiToolDefinition"/> implementation can expose to report
/// the module it originated from. The admin tool catalog uses this to group tools by module.
/// Hand-authored definitions are free to leave this unimplemented — the DTO layer falls back
/// to "Unknown" in that case.
/// </summary>
public interface IAiToolDefinitionModuleSource
{
    /// <summary>Module identifier, e.g. "Products", "AI", "Core". Non-null when implemented.</summary>
    string ModuleSource { get; }
}
