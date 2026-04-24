using Starter.Module.AI.Application.Services.Runtime;

namespace Starter.Module.AI.Application.Services.Personas;

/// <summary>
/// Request-scoped holder for the resolved persona. Populated by ChatExecutionService
/// and read by downstream services that need persona awareness (e.g. observability,
/// future moderation adapters, list endpoints filtered by the caller's active persona).
/// </summary>
internal interface IPersonaContextAccessor
{
    PersonaContext? Current { get; }
    void Set(PersonaContext? context);
}
