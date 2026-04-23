using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetAssistantById;

public sealed record GetAssistantByIdQuery(Guid Id) : IRequest<Result<AiAssistantDto>>;
