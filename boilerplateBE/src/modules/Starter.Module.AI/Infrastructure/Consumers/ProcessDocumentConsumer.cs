using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Messages;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Retrieval;
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

            if (!string.IsNullOrEmpty(doc.ContentHash))
            {
                var match = await db.AiDocuments
                    .IgnoreQueryFilters()
                    .Where(d => d.Id != doc.Id
                             && d.ContentHash == doc.ContentHash
                             && (d.TenantId == doc.TenantId || (d.TenantId == null && doc.TenantId == null))
                             && d.EmbeddingStatus == EmbeddingStatus.Completed)
                    .OrderByDescending(d => d.ProcessedAt)
                    .FirstOrDefaultAsync(ct);

                if (match is not null)
                {
                    var existingChunks = await db.AiDocumentChunks
                        .AsNoTracking()
                        .Where(c => c.DocumentId == match.Id)
                        .ToListAsync(ct);

                    if (existingChunks.Count > 0)
                    {
                        logger.LogInformation(
                            "Document {Id} matches fingerprint of {MatchId}; cloning {Count} chunks and skipping embedding.",
                            doc.Id, match.Id, existingChunks.Count);

                        var oldToNewParentId = new Dictionary<Guid, Guid>();
                        var newParents = new List<AiDocumentChunk>();
                        foreach (var parent in existingChunks.Where(c => c.ChunkLevel == "parent"))
                        {
                            var clonedParent = AiDocumentChunk.Create(
                                documentId: doc.Id,
                                chunkLevel: "parent",
                                content: parent.Content,
                                chunkIndex: parent.ChunkIndex,
                                tokenCount: parent.TokenCount,
                                qdrantPointId: Guid.NewGuid(),
                                parentChunkId: null,
                                sectionTitle: parent.SectionTitle,
                                pageNumber: parent.PageNumber);
                            if (!string.IsNullOrEmpty(parent.NormalizedContent))
                                clonedParent.SetNormalizedContent(parent.NormalizedContent);
                            newParents.Add(clonedParent);
                            oldToNewParentId[parent.Id] = clonedParent.Id;
                        }

                        // Persist parents before any remote calls, mirroring the normal path's
                        // ordering. This prevents the outer catch from flushing half-built clone
                        // state alongside MarkFailed if a downstream remote call throws.
                        db.AiDocumentChunks.AddRange(newParents);
                        await db.SaveChangesAsync(ct);

                        var cloneTenantId = doc.TenantId ?? Guid.Empty;
                        await vectorStore.EnsureCollectionAsync(cloneTenantId, embedder.VectorSize, ct);

                        var childrenToClone = existingChunks.Where(c => c.ChunkLevel == "child").ToList();
                        var childContents = childrenToClone.Select(c => c.Content).ToList();

                        // Batch embed: one API round-trip for all children instead of N.
                        // Pass attribution so the skip path accounts embedding usage the same
                        // way the normal ingestion path does.
                        var cloneAttribution = new EmbedAttribution(
                            TenantId: context.Message.TenantId,
                            UserId: context.Message.InitiatingUserId);
                        var childVectors = childContents.Count > 0
                            ? await embedder.EmbedAsync(childContents, ct, cloneAttribution)
                            : Array.Empty<float[]>();

                        var clonePoints = new List<VectorPoint>(childrenToClone.Count);
                        var newChildren = new List<AiDocumentChunk>(childrenToClone.Count);
                        for (var i = 0; i < childrenToClone.Count; i++)
                        {
                            var child = childrenToClone[i];
                            var parentDbId = child.ParentChunkId is Guid pid && oldToNewParentId.TryGetValue(pid, out var np)
                                ? np
                                : (Guid?)null;
                            var newPointId = Guid.NewGuid();
                            var clonedChild = AiDocumentChunk.Create(
                                documentId: doc.Id,
                                chunkLevel: "child",
                                content: child.Content,
                                chunkIndex: child.ChunkIndex,
                                tokenCount: child.TokenCount,
                                qdrantPointId: newPointId,
                                parentChunkId: parentDbId,
                                sectionTitle: child.SectionTitle,
                                pageNumber: child.PageNumber);
                            if (!string.IsNullOrEmpty(child.NormalizedContent))
                                clonedChild.SetNormalizedContent(child.NormalizedContent);
                            newChildren.Add(clonedChild);

                            clonePoints.Add(new VectorPoint(
                                Id: newPointId,
                                Vector: childVectors[i],
                                Payload: new VectorPayload(
                                    DocumentId: doc.Id,
                                    DocumentName: doc.Name,
                                    ChunkLevel: "child",
                                    ChunkIndex: child.ChunkIndex,
                                    SectionTitle: child.SectionTitle,
                                    PageNumber: child.PageNumber,
                                    ParentChunkId: parentDbId,
                                    TenantId: cloneTenantId)));
                        }

                        await vectorStore.UpsertAsync(cloneTenantId, clonePoints, ct);

                        db.AiDocumentChunks.AddRange(newChildren);
                        doc.MarkCompleted(chunkCount: newChildren.Count);
                        await db.SaveChangesAsync(ct);
                        return;
                    }
                }
            }

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
                ChildOverlapTokens: ragOptions.ChunkOverlap) { ContentType = doc.ContentType });

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

            var attribution = new EmbedAttribution(
                TenantId: context.Message.TenantId,
                UserId: context.Message.InitiatingUserId);
            var vectors = await embedder.EmbedAsync(childTexts, ct, attribution);

            var tenantId = doc.TenantId ?? Guid.Empty;
            await vectorStore.EnsureCollectionAsync(tenantId, embedder.VectorSize, ct);

            var arOpts = ragOptions.ToArabicOptions();

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

            if (ragOptions.ApplyArabicNormalization)
            {
                foreach (var p in parentEntities)
                    p.SetNormalizedContent(ArabicTextNormalizer.Normalize(p.Content, arOpts));
            }

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

                if (ragOptions.ApplyArabicNormalization)
                {
                    var last = childEntities[^1];
                    last.SetNormalizedContent(ArabicTextNormalizer.Normalize(last.Content, arOpts));
                }

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
