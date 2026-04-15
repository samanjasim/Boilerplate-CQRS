using MediatR;
using Starter.Application.Common.Models;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetTools;

public sealed record GetToolsQuery(
    int PageNumber = 1,
    int PageSize = 50,
    string? Category = null,
    bool? IsEnabled = null,
    string? SearchTerm = null) : IRequest<Result<PaginatedList<AiToolDto>>>;
