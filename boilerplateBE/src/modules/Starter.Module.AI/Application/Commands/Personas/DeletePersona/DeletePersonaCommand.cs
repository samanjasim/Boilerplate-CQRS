using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.DeletePersona;

public sealed record DeletePersonaCommand(Guid Id) : IRequest<Result>;
