using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Personas.GetMePersonas;

public sealed record GetMePersonasQuery : IRequest<Result<MePersonasDto>>;
