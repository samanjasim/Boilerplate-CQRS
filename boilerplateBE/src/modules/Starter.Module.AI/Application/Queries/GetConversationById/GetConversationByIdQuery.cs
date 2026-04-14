using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetConversationById;

public sealed record GetConversationByIdQuery(Guid Id) : IRequest<Result<AiConversationDetailDto>>;
