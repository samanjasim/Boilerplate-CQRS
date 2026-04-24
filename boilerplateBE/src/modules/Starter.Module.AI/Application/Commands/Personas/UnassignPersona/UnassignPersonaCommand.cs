using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.UnassignPersona;

public sealed record UnassignPersonaCommand(Guid PersonaId, Guid UserId) : IRequest<Result>;
