using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.SetUserDefaultPersona;

public sealed record SetUserDefaultPersonaCommand(Guid PersonaId, Guid UserId) : IRequest<Result>;
