using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.DeleteDocument;

internal sealed class DeleteDocumentCommandHandler(
    AiDbContext db,
    IFileService fileService,
    IVectorStore vectors,
    ILogger<DeleteDocumentCommandHandler> logger) : IRequestHandler<DeleteDocumentCommand, Result>
{
    public async Task<Result> Handle(DeleteDocumentCommand request, CancellationToken ct)
    {
        var doc = await db.AiDocuments.FirstOrDefaultAsync(d => d.Id == request.Id, ct);
        if (doc is null) return Result.Failure(AiErrors.DocumentNotFound);

        var tenantId = doc.TenantId ?? Guid.Empty;

        try
        {
            await vectors.DeleteByDocumentAsync(tenantId, doc.Id, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete Qdrant points for document {Id}", doc.Id);
        }

        var chunks = db.AiDocumentChunks.Where(c => c.DocumentId == doc.Id);
        db.AiDocumentChunks.RemoveRange(chunks);

        try { await fileService.DeleteManagedFileAsync(doc.FileId, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to delete managed file {FileId} for document {DocId}", doc.FileId, doc.Id); }

        db.AiDocuments.Remove(doc);
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }
}
