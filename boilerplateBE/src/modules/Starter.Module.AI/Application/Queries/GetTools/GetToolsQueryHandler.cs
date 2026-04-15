using MediatR;
using Starter.Application.Common.Models;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetTools;

internal sealed class GetToolsQueryHandler(IAiToolRegistry registry)
    : IRequestHandler<GetToolsQuery, Result<PaginatedList<AiToolDto>>>
{
    public async Task<Result<PaginatedList<AiToolDto>>> Handle(
        GetToolsQuery request,
        CancellationToken cancellationToken)
    {
        var all = await registry.ListAllAsync(cancellationToken);

        IEnumerable<AiToolDto> q = all;

        if (!string.IsNullOrWhiteSpace(request.Category))
            q = q.Where(t => string.Equals(t.Category, request.Category, StringComparison.OrdinalIgnoreCase));

        if (request.IsEnabled is bool enabled)
            q = q.Where(t => t.IsEnabled == enabled);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            q = q.Where(t =>
                t.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var filtered = q.ToList();
        var page = filtered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Result.Success(PaginatedList<AiToolDto>.Create(page, filtered.Count, pageNumber, pageSize));
    }
}
