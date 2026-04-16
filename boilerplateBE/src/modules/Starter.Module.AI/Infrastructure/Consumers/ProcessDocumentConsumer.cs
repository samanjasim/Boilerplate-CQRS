using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Messages;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Consumers;

public sealed class ProcessDocumentConsumer(IServiceScopeFactory scopeFactory)
    : IConsumer<ProcessDocumentMessage>
{
    public async Task Consume(ConsumeContext<ProcessDocumentMessage> context)
    {
        var ct = context.CancellationToken;
        using var scope = scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
        var extractors = scope.ServiceProvider.GetRequiredService<IDocumentTextExtractorRegistry>();
        var chunker = scope.ServiceProvider.GetRequiredService<IDocumentChunker>();
        var embedder = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
        var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
        var ragOptions = scope.ServiceProvider.GetRequiredService<IOptions<AiRagSettings>>().Value;
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ProcessDocumentConsumer>>();

        var doc = await db.AiDocuments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == context.Message.DocumentId, ct);

        if (doc is null)
        {
            logger.LogWarning("ProcessDocument: document {Id} not found", context.Message.DocumentId);
            return;
        }

        try
        {
            doc.MarkProcessing();
            await db.SaveChangesAsync(ct);

            var extractor = extractors.Resolve(doc.ContentType)
                ?? throw new InvalidOperationException(
                    $"No extractor registered for content type '{doc.ContentType}'.");

            await using var fileStream = await storage.DownloadAsync(doc.FileRef, ct);
            using var buffered = new MemoryStream();
            await fileStream.CopyToAsync(buffered, ct);
            buffered.Position = 0;

            var extracted = await extractor.ExtractAsync(buffered, ct);

            var chunks = chunker.Chunk(extracted, new ChunkingOptions(
                ParentTokens: ragOptions.ParentChunkSize,
                ChildTokens: ragOptions.ChunkSize,
                ChildOverlapTokens: ragOptions.ChunkOverlap));

            var childTexts = chunks.Children.Select(c => c.Content).ToList();

            if (childTexts.Count == 0)
            {
                doc.MarkCompleted(chunkCount: 0);
                await db.SaveChangesAsync(ct);
                logger.LogInformation(
                    "Document {Id} produced no chunks (empty or whitespace-only); marked Completed.",
                    doc.Id);
                return;
            }

            var vectors = await embedder.EmbedAsync(childTexts, ct);

            var tenantId = doc.TenantId ?? Guid.Empty;
            await vectorStore.EnsureCollectionAsync(tenantId, embedder.VectorSize, ct);

            var parentEntities = chunks.Parents.Select(p => AiDocumentChunk.Create(
                documentId: doc.Id,
                chunkLevel: "parent",
                content: p.Content,
                chunkIndex: p.Index,
                tokenCount: p.TokenCount,
                qdrantPointId: Guid.NewGuid(),
                parentChunkId: null,
                sectionTitle: p.SectionTitle,
                pageNumber: p.PageNumber)).ToList();

            db.AiDocumentChunks.AddRange(parentEntities);
            await db.SaveChangesAsync(ct);

            var parentIds = parentEntities.Select(p => p.Id).ToList();

            var points = new List<VectorPoint>(chunks.Children.Count);
            var childEntities = new List<AiDocumentChunk>(chunks.Children.Count);

            for (var i = 0; i < chunks.Children.Count; i++)
            {
                var draft = chunks.Children[i];
                var pointId = Guid.NewGuid();
                var parentDbId = draft.ParentIndex is int pIdx ? parentIds[pIdx] : (Guid?)null;

                childEntities.Add(AiDocumentChunk.Create(
                    documentId: doc.Id,
                    chunkLevel: "child",
                    content: draft.Content,
                    chunkIndex: draft.Index,
                    tokenCount: draft.TokenCount,
                    qdrantPointId: pointId,
                    parentChunkId: parentDbId,
                    sectionTitle: draft.SectionTitle,
                    pageNumber: draft.PageNumber));

                points.Add(new VectorPoint(
                    Id: pointId,
                    Vector: vectors[i],
                    Payload: new VectorPayload(
                        DocumentId: doc.Id,
                        DocumentName: doc.Name,
                        ChunkLevel: "child",
                        ChunkIndex: draft.Index,
                        SectionTitle: draft.SectionTitle,
                        PageNumber: draft.PageNumber,
                        ParentChunkId: parentDbId,
                        TenantId: tenantId)));
            }

            await vectorStore.UpsertAsync(tenantId, points, ct);

            db.AiDocumentChunks.AddRange(childEntities);
            doc.MarkCompleted(chunkCount: childEntities.Count);
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Processed document {Id}: parents={Parents}, children={Children}, ocr={Ocr}",
                doc.Id, parentEntities.Count, childEntities.Count, extracted.UsedOcr);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process document {Id}", doc.Id);
            doc.MarkFailed(ex.Message);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }
}
