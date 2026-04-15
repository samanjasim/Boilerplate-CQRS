using MediatR;
using Starter.Application.Common.Models;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetAssistants;

public sealed record GetAssistantsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    bool? IsActive = null) : IRequest<Result<PaginatedList<AiAssistantDto>>>;
