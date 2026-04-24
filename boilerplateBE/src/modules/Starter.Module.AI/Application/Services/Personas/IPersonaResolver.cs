using Starter.Module.AI.Application.Services.Runtime;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Services.Personas;

internal interface IPersonaResolver
{
    Task<Result<PersonaContext>> ResolveAsync(Guid? explicitPersonaId, CancellationToken ct);
}
