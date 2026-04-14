using MediatR;
using Starter.Application.Common.Models;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetConversations;

public sealed record GetConversationsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    Guid? AssistantId = null) : IRequest<Result<PaginatedList<AiConversationDto>>>;
