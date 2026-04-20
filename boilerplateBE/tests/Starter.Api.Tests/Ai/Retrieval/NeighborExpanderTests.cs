using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public class NeighborExpanderTests
{
    private static AiDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"neigh-{Guid.NewGuid():N}")
            .Options;
        return new AiDbContext(options, currentUserService: null);
    }

    private static RetrievedChunk BuildAnchor(Guid docId, int chunkIndex, Guid? pointId = null)
        => new(
            ChunkId: pointId ?? Guid.NewGuid(),
            DocumentId: docId,
            DocumentName: "doc",
            Content: "anchor",
            SectionTitle: null,
            PageNumber: null,
            ChunkLevel: "child",
            SemanticScore: 0m,
            KeywordScore: 0m,
            HybridScore: 0.8m,
            ParentChunkId: null,
            ChunkIndex: chunkIndex);

    /// <summary>
    /// Creates and seeds an AiDocument using the public factory, returning the seeded entity
    /// so callers can read its auto-generated Id.
    /// </summary>
    private static AiDocument BuildDoc(Guid tenantId, string? name = null)
        => AiDocument.Create(
            tenantId: tenantId,
            name: name ?? $"doc-{Guid.NewGuid():N}",
            fileName: "test.pdf",
            fileRef: "ref/test.pdf",
            contentType: "application/pdf",
            sizeBytes: 1024,
            uploadedByUserId: Guid.NewGuid());

    // ---- Tests ----

    [Fact]
    public async Task Empty_anchors_returns_empty()
    {
        await using var db = CreateDb();
        var siblings = await new NeighborExpander(db, NullLogger<NeighborExpander>.Instance)
            .ExpandAsync(Guid.NewGuid(), Array.Empty<RetrievedChunk>(), 2, CancellationToken.None);
        siblings.Should().BeEmpty();
    }

    [Fact]
    public async Task Expands_window_excluding_anchor()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();

        var doc = BuildDoc(tenantId);
        db.AiDocuments.Add(doc);
        var docId = doc.Id;

        Guid anchorPointId = Guid.Empty;
        for (int i = 0; i < 10; i++)
        {
            var pid = Guid.NewGuid();
            if (i == 5) anchorPointId = pid;
            db.AiDocumentChunks.Add(TestChunkFactory.Build(
                pointId: pid, documentId: docId, chunkIndex: i, content: $"c{i}", chunkLevel: "child"));
        }
        await db.SaveChangesAsync();

        var anchor = BuildAnchor(docId, 5, anchorPointId);
        var siblings = await new NeighborExpander(db, NullLogger<NeighborExpander>.Instance)
            .ExpandAsync(tenantId, new[] { anchor }, windowSize: 2, CancellationToken.None);

        siblings.Select(s => s.ChunkIndex).Should().Equal(3, 4, 6, 7);
    }

    [Fact]
    public async Task Merges_overlapping_windows_same_document()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();

        var doc = BuildDoc(tenantId);
        db.AiDocuments.Add(doc);
        var docId = doc.Id;

        Guid p3 = Guid.Empty, p5 = Guid.Empty;
        for (int i = 0; i < 10; i++)
        {
            var pid = Guid.NewGuid();
            if (i == 3) p3 = pid;
            if (i == 5) p5 = pid;
            db.AiDocumentChunks.Add(TestChunkFactory.Build(
                pointId: pid, documentId: docId, chunkIndex: i, content: $"c{i}", chunkLevel: "child"));
        }
        await db.SaveChangesAsync();

        var anchors = new[] { BuildAnchor(docId, 3, p3), BuildAnchor(docId, 5, p5) };
        var siblings = await new NeighborExpander(db, NullLogger<NeighborExpander>.Instance)
            .ExpandAsync(tenantId, anchors, 2, CancellationToken.None);

        // Ranges [1..5] and [3..7] → merged [1..7]; minus anchors 3,5 → {1,2,4,6,7}
        siblings.Select(s => s.ChunkIndex).Should().Equal(1, 2, 4, 6, 7);
    }

    [Fact]
    public async Task Cross_document_expansion_handled_separately()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();

        var docA = BuildDoc(tenantId);
        var docB = BuildDoc(tenantId);
        db.AiDocuments.AddRange(docA, docB);

        Guid a2 = Guid.Empty, b2 = Guid.Empty;
        for (int i = 0; i < 5; i++)
        {
            var pidA = Guid.NewGuid();
            if (i == 2) a2 = pidA;
            db.AiDocumentChunks.Add(TestChunkFactory.Build(
                pointId: pidA, documentId: docA.Id, chunkIndex: i, content: $"cA{i}", chunkLevel: "child"));

            var pidB = Guid.NewGuid();
            if (i == 2) b2 = pidB;
            db.AiDocumentChunks.Add(TestChunkFactory.Build(
                pointId: pidB, documentId: docB.Id, chunkIndex: i, content: $"cB{i}", chunkLevel: "child"));
        }
        await db.SaveChangesAsync();

        var anchors = new[] { BuildAnchor(docA.Id, 2, a2), BuildAnchor(docB.Id, 2, b2) };
        var siblings = await new NeighborExpander(db, NullLogger<NeighborExpander>.Instance)
            .ExpandAsync(tenantId, anchors, 1, CancellationToken.None);

        siblings.Should().HaveCount(4);
        siblings.Where(s => s.DocumentId == docA.Id).Select(s => s.ChunkIndex).Should().Equal(1, 3);
        siblings.Where(s => s.DocumentId == docB.Id).Select(s => s.ChunkIndex).Should().Equal(1, 3);
    }

    [Fact]
    public async Task Excludes_parent_chunks()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();

        var doc = BuildDoc(tenantId);
        db.AiDocuments.Add(doc);
        var docId = doc.Id;

        Guid anchorPid = Guid.NewGuid();
        db.AiDocumentChunks.Add(TestChunkFactory.Build(pointId: anchorPid, documentId: docId, chunkIndex: 0, chunkLevel: "child", content: "c0"));
        db.AiDocumentChunks.Add(TestChunkFactory.Build(pointId: Guid.NewGuid(), documentId: docId, chunkIndex: 1, chunkLevel: "parent", content: "p1"));
        db.AiDocumentChunks.Add(TestChunkFactory.Build(pointId: Guid.NewGuid(), documentId: docId, chunkIndex: 2, chunkLevel: "child", content: "c2"));
        await db.SaveChangesAsync();

        var siblings = await new NeighborExpander(db, NullLogger<NeighborExpander>.Instance)
            .ExpandAsync(tenantId, new[] { BuildAnchor(docId, 0, anchorPid) }, 2, CancellationToken.None);

        siblings.Select(s => s.ChunkIndex).Should().Equal(2);
    }
}
