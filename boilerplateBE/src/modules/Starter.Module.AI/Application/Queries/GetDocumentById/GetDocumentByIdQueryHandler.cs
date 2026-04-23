using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetDocumentById;

internal sealed class GetDocumentByIdQueryHandler(AiDbContext db)
    : IRequestHandler<GetDocumentByIdQuery, Result<AiDocumentDetailDto>>
{
    public async Task<Result<AiDocumentDetailDto>> Handle(
        GetDocumentByIdQuery request, CancellationToken ct)
    {
        var doc = await db.AiDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.Id, ct);
        if (doc is null) return Result.Failure<AiDocumentDetailDto>(AiErrors.DocumentNotFound);

        var previewLimit = Math.Clamp(request.ChunkPreviewLimit, 1, 100);
        var chunks = await db.AiDocumentChunks.AsNoTracking()
            .Where(c => c.DocumentId == doc.Id && c.ChunkLevel == "child")
            .OrderBy(c => c.ChunkIndex)
            .Take(previewLimit)
            .ToListAsync(ct);

        var dto = new AiDocumentDetailDto(
            Document: doc.ToDto(),
            ChunkPreviews: chunks.Select(c => c.ToPreviewDto()).ToList());

        return Result.Success(dto);
    }
}
