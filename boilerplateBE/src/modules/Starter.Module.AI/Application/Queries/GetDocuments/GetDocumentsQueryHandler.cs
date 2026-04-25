using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Paging;
using Starter.Module.AI.Application.DTOs;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetDocuments;

internal sealed class GetDocumentsQueryHandler(AiDbContext db)
    : IRequestHandler<GetDocumentsQuery, Result<PaginatedList<AiDocumentDto>>>
{
    public async Task<Result<PaginatedList<AiDocumentDto>>> Handle(
        GetDocumentsQuery request, CancellationToken ct)
    {
        var query = db.AiDocuments.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<EmbeddingStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(d => d.EmbeddingStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(d =>
                EF.Functions.ILike(d.Name, $"%{term}%") ||
                EF.Functions.ILike(d.FileName, $"%{term}%"));
        }

        query = query.OrderByDescending(d => d.CreatedAt);

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var total = await query.CountAsync(ct);
        var rows = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = rows.Select(r => r.ToDto()).ToList();
        return Result.Success(PaginatedList<AiDocumentDto>.Create(dtos, total, pageNumber, pageSize));
    }
}
