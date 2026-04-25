using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Messages;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.ReprocessDocument;

internal sealed class ReprocessDocumentCommandHandler(
    AiDbContext db,
    IApplicationDbContext appDb,
    IVectorStore vectors,
    IIntegrationEventCollector eventCollector) : IRequestHandler<ReprocessDocumentCommand, Result>
{
    public async Task<Result> Handle(ReprocessDocumentCommand request, CancellationToken ct)
    {
        var doc = await db.AiDocuments.FirstOrDefaultAsync(d => d.Id == request.Id, ct);
        if (doc is null) return Result.Failure(AiErrors.DocumentNotFound);

        if (doc.EmbeddingStatus == EmbeddingStatus.Processing)
            return Result.Failure(AiErrors.DocumentAlreadyProcessing);

        var tenantId = doc.TenantId ?? Guid.Empty;
        await vectors.DeleteByDocumentAsync(tenantId, doc.Id, ct);

        var chunks = db.AiDocumentChunks.Where(c => c.DocumentId == doc.Id);
        db.AiDocumentChunks.RemoveRange(chunks);

        doc.ResetForReprocessing();
        await db.SaveChangesAsync(ct);

        // Schedule via the outbox collector — appDb.SaveChangesAsync below
        // flushes the message into ApplicationDbContext's outbox atomically.
        // Direct bus.Publish would silently drop here when multiple EF outboxes
        // are registered (last-wins on IScopedBusContextProvider<IBus>).
        eventCollector.Schedule(new ProcessDocumentMessage(doc.Id, doc.TenantId, doc.UploadedByUserId));
        await appDb.SaveChangesAsync(ct);

        return Result.Success();
    }
}
