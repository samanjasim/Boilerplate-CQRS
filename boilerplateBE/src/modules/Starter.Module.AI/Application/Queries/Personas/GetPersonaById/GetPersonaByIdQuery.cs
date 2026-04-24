using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Personas.GetPersonaById;

public sealed record GetPersonaByIdQuery(Guid Id) : IRequest<Result<AiPersonaDto>>;
