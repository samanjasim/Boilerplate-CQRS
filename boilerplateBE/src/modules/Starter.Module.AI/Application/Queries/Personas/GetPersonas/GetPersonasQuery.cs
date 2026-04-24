using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Personas.GetPersonas;

public sealed record GetPersonasQuery(
    bool IncludeSystem = true,
    bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<AiPersonaDto>>>;
