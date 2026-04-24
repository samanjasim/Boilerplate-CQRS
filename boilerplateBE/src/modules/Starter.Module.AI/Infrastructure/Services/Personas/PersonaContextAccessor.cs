using Starter.Module.AI.Application.Services.Personas;
using Starter.Module.AI.Application.Services.Runtime;

namespace Starter.Module.AI.Infrastructure.Services.Personas;

internal sealed class PersonaContextAccessor : IPersonaContextAccessor
{
    public PersonaContext? Current { get; private set; }
    public void Set(PersonaContext? context) => Current = context;
}
