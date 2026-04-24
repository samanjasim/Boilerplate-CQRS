using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Personas.GetPersonaAssignments;

public sealed record GetPersonaAssignmentsQuery(Guid PersonaId)
    : IRequest<Result<IReadOnlyList<UserPersonaDto>>>;
