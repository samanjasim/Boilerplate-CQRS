namespace Starter.Application.Common.Interfaces;

/// <summary>
/// AsyncLocal holder for the currently-active `IExecutionContext`. Used by the DI
/// `IExecutionContext` factory to prefer an installed ambient (e.g., the AI module's
/// agent-run scope) over the default `HttpExecutionContext`. Modules install via
/// the static `Use(...)` method which returns an `IDisposable` scope.
///
/// Lives in core so the Identity DI factory can read it without depending on any
/// specific module.
/// </summary>
public static class AmbientExecutionContext
{
    private static readonly AsyncLocal<IExecutionContext?> _current = new();

    public static IExecutionContext? Current => _current.Value;

    public static IDisposable Use(IExecutionContext ctx)
    {
        var previous = _current.Value;
        _current.Value = ctx;
        return new Restorer(previous);
    }

    private sealed class Restorer(IExecutionContext? previous) : IDisposable
    {
        public void Dispose() => _current.Value = previous;
    }
}
