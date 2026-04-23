using Microsoft.Extensions.Options;
using Starter.Domain.Common.Access.Enums;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Eval.Fixtures;

/// <summary>
/// Ingests <see cref="EvalDataset"/> fixture documents into Postgres + Qdrant so that
/// the eval harness can issue real retrieval queries against them.
/// Returns a mapping from fixture document IDs to the persisted <see cref="AiDocument.Id"/>
/// values so that metric computation can translate retrieved IDs back to fixture IDs.
/// </summary>
public sealed class EvalFixtureIngester(
    AiDbContext db,
    IDocumentChunker chunker,
    IEmbeddingService embeddings,
    IVectorStore vectors,
    IOptions<AiRagSettings> ragOptions)
{
    private readonly AiRagSettings _rag = ragOptions.Value;

    public async Task<IReadOnlyDictionary<Guid, Guid>> IngestAsync(
        Guid tenantId,
        Guid uploaderUserId,
        EvalDataset dataset,
        CancellationToken ct)
    {
        var idMap = new Dictionary<Guid, Guid>(dataset.Documents.Count);

        // EmbeddingService.VectorSize is populated lazily after the first embed call.
        // Probe with a single token so EnsureCollectionAsync has a dimension to work with
        // without forcing every caller to pre-warm separately.
        var attribution = new EmbedAttribution(TenantId: tenantId, UserId: uploaderUserId);
        _ = await embeddings.EmbedAsync(new[] { "." }, ct, attribution);

        await vectors.EnsureCollectionAsync(tenantId, embeddings.VectorSize, ct);

        foreach (var doc in dataset.Documents)
        {
            // Use the fixture doc Id as a synthetic FileId — eval docs are not
            // stored in the file-storage service; the FileId column just needs a
            // stable non-default Guid so that ACL payload filters work correctly.
            var syntheticFileId = doc.Id;
            var contentType = InferContentType(doc.FileName);

            var entity = AiDocument.Create(
                tenantId: tenantId,
                name: doc.FileName,
                fileName: doc.FileName,
                fileId: syntheticFileId,
                contentType: contentType,
                sizeBytes: System.Text.Encoding.UTF8.GetByteCount(doc.Content),
                uploadedByUserId: uploaderUserId);

            db.AiDocuments.Add(entity);
            idMap[doc.Id] = entity.Id;

            // Build an ExtractedDocument with a single page containing the fixture
            // document's raw text. The chunker treats it like a plain-text extraction.
            var extracted = new ExtractedDocument(
                Pages: [new ExtractedPage(PageNumber: 1, Text: doc.Content)],
                UsedOcr: false);

            var chunks = chunker.Chunk(extracted, new ChunkingOptions(
                ParentTokens: _rag.ParentChunkSize,
                ChildTokens: _rag.ChunkSize,
                ChildOverlapTokens: _rag.ChunkOverlap)
            {
                ContentType = contentType
            });

            // Persist parent chunks first (mirrors ProcessDocumentConsumer ordering).
            var parentEntities = chunks.Parents.Select(p => AiDocumentChunk.Create(
                documentId: entity.Id,
                chunkLevel: "parent",
                content: p.Content,
                chunkIndex: p.Index,
                tokenCount: p.TokenCount,
                qdrantPointId: Guid.NewGuid(),
                parentChunkId: null,
                sectionTitle: p.SectionTitle,
                pageNumber: p.PageNumber,
                chunkType: p.ChunkType,
                fileId: syntheticFileId,
                visibility: ResourceVisibility.TenantWide,
                uploadedByUserId: uploaderUserId)).ToList();

            db.AiDocumentChunks.AddRange(parentEntities);
            await db.SaveChangesAsync(ct);

            var parentIds = parentEntities.Select(p => p.Id).ToList();

            // Embed all child chunk texts in one batch.
            var childTexts = chunks.Children.Select(c => c.Content).ToList();

            float[][] childVectors;
            if (childTexts.Count > 0)
            {
                childVectors = await embeddings.EmbedAsync(childTexts, ct, attribution);
            }
            else
            {
                entity.MarkCompleted(chunkCount: 0);
                await db.SaveChangesAsync(ct);
                continue;
            }

            var points = new List<VectorPoint>(chunks.Children.Count);
            var childEntities = new List<AiDocumentChunk>(chunks.Children.Count);

            for (var i = 0; i < chunks.Children.Count; i++)
            {
                var draft = chunks.Children[i];
                var pointId = Guid.NewGuid();
                var parentDbId = draft.ParentIndex is int pIdx ? parentIds[pIdx] : (Guid?)null;

                var childEntity = AiDocumentChunk.Create(
                    documentId: entity.Id,
                    chunkLevel: "child",
                    content: draft.Content,
                    chunkIndex: draft.Index,
                    tokenCount: draft.TokenCount,
                    qdrantPointId: pointId,
                    parentChunkId: parentDbId,
                    sectionTitle: draft.SectionTitle,
                    pageNumber: draft.PageNumber,
                    chunkType: draft.ChunkType,
                    fileId: syntheticFileId,
                    visibility: ResourceVisibility.TenantWide,
                    uploadedByUserId: uploaderUserId);
                childEntities.Add(childEntity);

                points.Add(new VectorPoint(
                    Id: pointId,
                    Vector: childVectors[i],
                    Payload: new VectorPayload(
                        DocumentId: entity.Id,
                        DocumentName: entity.Name,
                        ChunkLevel: "child",
                        ChunkIndex: draft.Index,
                        SectionTitle: draft.SectionTitle,
                        PageNumber: draft.PageNumber,
                        ParentChunkId: parentDbId,
                        TenantId: tenantId,
                        FileId: syntheticFileId,
                        Visibility: ResourceVisibility.TenantWide,
                        UploadedByUserId: uploaderUserId,
                        ChunkType: draft.ChunkType)));
            }

            await vectors.UpsertAsync(tenantId, points, ct);

            db.AiDocumentChunks.AddRange(childEntities);
            entity.MarkCompleted(chunkCount: childEntities.Count);
            await db.SaveChangesAsync(ct);
        }

        return idMap;
    }

    private static string InferContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".md" or ".markdown" => "text/markdown",
            ".html" or ".htm" => "text/html",
            _ => "text/plain",
        };
    }
}
