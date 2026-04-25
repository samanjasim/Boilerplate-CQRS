namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Optional capability surfaced by the template scanner. Authors do not implement
/// this directly — the scanner wraps user-authored <see cref="IAiAgentTemplate"/>
/// instances in a decorator that exposes the source assembly's module name.
/// </summary>
public interface IAiAgentTemplateModuleSource
{
    string ModuleSource { get; }
}
