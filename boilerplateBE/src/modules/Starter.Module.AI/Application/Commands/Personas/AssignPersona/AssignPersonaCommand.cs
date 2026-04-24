using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.AssignPersona;

public sealed record AssignPersonaCommand(
    Guid PersonaId,
    Guid UserId,
    bool MakeDefault) : IRequest<Result>;
