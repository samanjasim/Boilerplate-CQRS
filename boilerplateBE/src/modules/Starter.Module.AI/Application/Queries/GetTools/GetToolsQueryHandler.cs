using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetTools;

internal sealed class GetToolsQueryHandler(IAiToolRegistry registry)
    : IRequestHandler<GetToolsQuery, Result<IReadOnlyList<AiToolDto>>>
{
    public async Task<Result<IReadOnlyList<AiToolDto>>> Handle(
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

        return Result.Success<IReadOnlyList<AiToolDto>>(q.ToList());
    }
}
