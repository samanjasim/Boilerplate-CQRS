using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Messages;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Consumers;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Consumers;

public sealed class ProcessDocumentConsumerTests
{
    [Fact]
    public async Task Marks_Document_Completed_And_Upserts_Vectors_On_Happy_Path()
    {
        var harness = new ConsumerHarness();
        var doc = harness.SeedDocument("doc.txt", "text/plain");

        harness.Extractor
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedDocument(
                new[] { new ExtractedPage(1, "Some content for chunking.") },
                UsedOcr: false));

        var children = new[]
        {
            new ChunkDraft(0, "child a", 4, ParentIndex: 0, SectionTitle: null, PageNumber: 1),
            new ChunkDraft(1, "child b", 4, ParentIndex: 0, SectionTitle: null, PageNumber: 1),
        };
        harness.Chunker
            .Setup(c => c.Chunk(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingOptions>()))
            .Returns(new HierarchicalChunks(
                Parents: new[] { new ChunkDraft(0, "parent", 8, null, null, 1) },
                Children: children));

        harness.Embedder
            .Setup(e => e.EmbedAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>(), It.IsAny<EmbedAttribution?>()))
            .ReturnsAsync(new[] { new[] { 0.1f, 0.2f }, new[] { 0.3f, 0.4f } });
        harness.Embedder.SetupGet(e => e.VectorSize).Returns(2);

        await harness.Consume(new ProcessDocumentMessage(doc.Id, doc.TenantId, Guid.NewGuid()));

        var saved = await harness.Db.AiDocuments.IgnoreQueryFilters().AsNoTracking().SingleAsync(d => d.Id == doc.Id);
        saved.EmbeddingStatus.Should().Be(EmbeddingStatus.Completed);
        saved.ChunkCount.Should().Be(2);

        harness.VectorStore.Verify(
            v => v.UpsertAsync(It.IsAny<Guid>(), It.Is<IReadOnlyList<VectorPoint>>(p => p.Count == 2), It.IsAny<CancellationToken>()),
            Times.Once);

        var chunks = await harness.Db.AiDocumentChunks.Where(c => c.DocumentId == doc.Id).ToListAsync();
        chunks.Should().HaveCount(3);
        chunks.Count(c => c.ChunkLevel == "parent").Should().Be(1);
        chunks.Count(c => c.ChunkLevel == "child").Should().Be(2);
    }

    [Fact]
    public async Task Marks_Document_Completed_With_Zero_Chunks_When_Document_Is_Empty()
    {
        var harness = new ConsumerHarness();
        var doc = harness.SeedDocument("blank.txt", "text/plain");

        harness.Extractor
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedDocument(Array.Empty<ExtractedPage>(), UsedOcr: false));

        harness.Chunker
            .Setup(c => c.Chunk(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingOptions>()))
            .Returns(new HierarchicalChunks(
                Parents: Array.Empty<ChunkDraft>(),
                Children: Array.Empty<ChunkDraft>()));

        await harness.Consume(new ProcessDocumentMessage(doc.Id, doc.TenantId, Guid.NewGuid()));

        var saved = await harness.Db.AiDocuments.IgnoreQueryFilters().AsNoTracking().SingleAsync(d => d.Id == doc.Id);
        saved.EmbeddingStatus.Should().Be(EmbeddingStatus.Completed);
        saved.ChunkCount.Should().Be(0);

        harness.Embedder.Verify(
            e => e.EmbedAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>(), It.IsAny<EmbedAttribution?>()),
            Times.Never);
        harness.VectorStore.Verify(
            v => v.UpsertAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<VectorPoint>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Marks_Document_Failed_And_Rethrows_When_Extractor_Throws()
    {
        var harness = new ConsumerHarness();
        var doc = harness.SeedDocument("broken.pdf", "application/pdf");

        harness.Extractor
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated extraction failure"));

        var act = async () => await harness.Consume(new ProcessDocumentMessage(doc.Id, doc.TenantId, Guid.NewGuid()));

        await act.Should().ThrowAsync<InvalidOperationException>();

        var saved = await harness.Db.AiDocuments.IgnoreQueryFilters().AsNoTracking().SingleAsync(d => d.Id == doc.Id);
        saved.EmbeddingStatus.Should().Be(EmbeddingStatus.Failed);
        saved.ErrorMessage.Should().Contain("simulated extraction failure");
    }

    [Fact]
    public async Task Returns_Without_Throwing_When_Document_Does_Not_Exist()
    {
        var harness = new ConsumerHarness();

        await harness.Consume(new ProcessDocumentMessage(Guid.NewGuid(), null, Guid.NewGuid()));

        harness.Extractor.Verify(
            e => e.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Fingerprint_Match_Clones_Chunks_And_Skips_Extraction()
    {
        var harness = new ConsumerHarness();
        var tenantId = Guid.NewGuid();
        const string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        // Seed source (completed) doc + chunks
        var source = harness.SeedDocument("source.txt", "text/plain", tenantId);
        source.SetContentHash(hash);
        source.MarkCompleted(chunkCount: 2);
        await harness.Db.SaveChangesAsync();

        var sourceParent = AiDocumentChunk.Create(
            documentId: source.Id,
            chunkLevel: "parent",
            content: "parent content",
            chunkIndex: 0,
            tokenCount: 8,
            qdrantPointId: Guid.NewGuid(),
            parentChunkId: null,
            sectionTitle: "section-1",
            pageNumber: 1);

        var sourceChild1 = AiDocumentChunk.Create(
            documentId: source.Id,
            chunkLevel: "child",
            content: "child one content",
            chunkIndex: 0,
            tokenCount: 4,
            qdrantPointId: Guid.NewGuid(),
            parentChunkId: sourceParent.Id,
            sectionTitle: "section-1",
            pageNumber: 1);

        var sourceChild2 = AiDocumentChunk.Create(
            documentId: source.Id,
            chunkLevel: "child",
            content: "child two content",
            chunkIndex: 1,
            tokenCount: 4,
            qdrantPointId: Guid.NewGuid(),
            parentChunkId: sourceParent.Id,
            sectionTitle: "section-1",
            pageNumber: 1);

        harness.Db.AiDocumentChunks.AddRange(sourceParent, sourceChild1, sourceChild2);
        await harness.Db.SaveChangesAsync();

        // Seed new Pending doc with same hash + tenant
        var newDoc = harness.SeedDocument("same.txt", "text/plain", tenantId);
        newDoc.SetContentHash(hash);
        await harness.Db.SaveChangesAsync();

        harness.Embedder
            .Setup(e => e.EmbedAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<EmbedAttribution?>(),
                It.IsAny<Starter.Module.AI.Domain.Enums.AiRequestType>()))
            .ReturnsAsync((IReadOnlyList<string> texts, CancellationToken _, EmbedAttribution? _, Starter.Module.AI.Domain.Enums.AiRequestType _) =>
                texts.Select(_ => new[] { 0.42f, 0.43f }).ToArray());
        harness.Embedder.SetupGet(e => e.VectorSize).Returns(2);

        IReadOnlyList<VectorPoint>? capturedPoints = null;
        harness.VectorStore
            .Setup(v => v.UpsertAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<VectorPoint>>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, IReadOnlyList<VectorPoint> p, CancellationToken _) => capturedPoints = p)
            .Returns(Task.CompletedTask);

        await harness.Consume(new ProcessDocumentMessage(newDoc.Id, newDoc.TenantId, Guid.NewGuid()));

        // (i) extractor never called
        harness.Extractor.Verify(
            e => e.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // (ii) new doc Completed with ChunkCount=2
        var saved = await harness.Db.AiDocuments.IgnoreQueryFilters().AsNoTracking().SingleAsync(d => d.Id == newDoc.Id);
        saved.EmbeddingStatus.Should().Be(EmbeddingStatus.Completed);
        saved.ChunkCount.Should().Be(2);

        // (iii) new-doc chunks exist with same content, different Ids/QdrantPointIds
        var newChunks = await harness.Db.AiDocumentChunks
            .AsNoTracking()
            .Where(c => c.DocumentId == newDoc.Id)
            .ToListAsync();
        newChunks.Should().HaveCount(3);

        var newParents = newChunks.Where(c => c.ChunkLevel == "parent").ToList();
        var newChildren = newChunks.Where(c => c.ChunkLevel == "child").OrderBy(c => c.ChunkIndex).ToList();
        newParents.Should().HaveCount(1);
        newChildren.Should().HaveCount(2);

        newParents[0].Content.Should().Be(sourceParent.Content);
        newParents[0].Id.Should().NotBe(sourceParent.Id);
        newParents[0].QdrantPointId.Should().NotBe(sourceParent.QdrantPointId);

        newChildren[0].Content.Should().Be(sourceChild1.Content);
        newChildren[1].Content.Should().Be(sourceChild2.Content);
        newChildren.Select(c => c.Id).Should().NotContain(new[] { sourceChild1.Id, sourceChild2.Id });
        newChildren.Select(c => c.QdrantPointId).Should().NotContain(new[] { sourceChild1.QdrantPointId, sourceChild2.QdrantPointId });

        // (iv) child ParentChunkId points at cloned parent, not original
        var clonedParentId = newParents[0].Id;
        newChildren.Should().AllSatisfy(c => c.ParentChunkId.Should().Be(clonedParentId));
        newChildren.Should().AllSatisfy(c => c.ParentChunkId.Should().NotBe(sourceParent.Id));

        // (v) VectorStore.UpsertAsync was called with the new point ids
        capturedPoints.Should().NotBeNull();
        capturedPoints!.Should().HaveCount(2);
        var expectedPointIds = newChildren.Select(c => c.QdrantPointId).ToHashSet();
        capturedPoints.Select(p => p.Id).Should().BeEquivalentTo(expectedPointIds);

        // (vi) Upserted payloads describe the NEW doc (not the source) and reference
        // the cloned parent id — not the original source parent id.
        capturedPoints.Should().AllSatisfy(pt =>
        {
            pt.Payload.DocumentId.Should().Be(newDoc.Id);
            pt.Payload.TenantId.Should().Be(newDoc.TenantId ?? Guid.Empty);
            pt.Payload.ChunkLevel.Should().Be("child");
            pt.Payload.ParentChunkId.Should().Be(clonedParentId);
            pt.Payload.ParentChunkId.Should().NotBe(sourceParent.Id);
        });
    }

    [Fact]
    public async Task Fingerprint_Match_But_No_Chunks_Falls_Back_To_Processing()
    {
        var harness = new ConsumerHarness();
        var tenantId = Guid.NewGuid();
        const string hash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        // Source doc Completed but no chunks
        var source = harness.SeedDocument("empty-source.txt", "text/plain", tenantId);
        source.SetContentHash(hash);
        source.MarkCompleted(chunkCount: 0);
        await harness.Db.SaveChangesAsync();

        var newDoc = harness.SeedDocument("empty-target.txt", "text/plain", tenantId);
        newDoc.SetContentHash(hash);
        await harness.Db.SaveChangesAsync();

        harness.Extractor
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedDocument(new[] { new ExtractedPage(1, "hello world") }, UsedOcr: false));
        harness.Chunker
            .Setup(c => c.Chunk(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingOptions>()))
            .Returns(new HierarchicalChunks(
                Parents: Array.Empty<ChunkDraft>(),
                Children: Array.Empty<ChunkDraft>()));

        await harness.Consume(new ProcessDocumentMessage(newDoc.Id, newDoc.TenantId, Guid.NewGuid()));

        // Extractor WAS called — fell back to normal path
        harness.Extractor.Verify(
            e => e.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Structural_markdown_chunks_persist_chunk_type_and_breadcrumb_in_normalized_content()
    {
        // Verifies Task 18 of Plan 4b-3: the consumer must persist ChunkType onto
        // AiDocumentChunk rows and prepend the heading breadcrumb (SectionTitle)
        // into NormalizedContent so Postgres FTS matches heading text. The chunker
        // is mocked here (as in the other consumer tests) to isolate consumer
        // behavior — the StructuredMarkdownChunker → SectionTitle plumbing is
        // verified separately in StructuredMarkdownChunkerTests.
        var harness = new ConsumerHarness();
        var doc = harness.SeedDocument("doc.md", "text/markdown");

        harness.Extractor
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedDocument(
                new[] { new ExtractedPage(1, "# H1\n\n## H2\n\n```py\nx=1\n```\n") },
                UsedOcr: false));

        // One parent (Body) wrapping one code child. SectionTitle carries the
        // full breadcrumb "H1 > H2" so the consumer can prepend it verbatim.
        var codeDraft = new ChunkDraft(
            Index: 0,
            Content: "x=1",
            TokenCount: 3,
            ParentIndex: 0,
            SectionTitle: "H1 > H2",
            PageNumber: 1,
            ChunkType: ChunkType.Code);
        var parentDraft = new ChunkDraft(
            Index: 0,
            Content: "x=1",
            TokenCount: 3,
            ParentIndex: null,
            SectionTitle: "H1 > H2",
            PageNumber: 1,
            ChunkType: ChunkType.Body);

        harness.Chunker
            .Setup(c => c.Chunk(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingOptions>()))
            .Returns(new HierarchicalChunks(
                Parents: new[] { parentDraft },
                Children: new[] { codeDraft }));

        harness.Embedder
            .Setup(e => e.EmbedAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>(), It.IsAny<EmbedAttribution?>()))
            .ReturnsAsync(new[] { new[] { 0.1f, 0.2f } });
        harness.Embedder.SetupGet(e => e.VectorSize).Returns(2);

        IReadOnlyList<VectorPoint>? capturedPoints = null;
        harness.VectorStore
            .Setup(v => v.UpsertAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<VectorPoint>>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, IReadOnlyList<VectorPoint> p, CancellationToken _) => capturedPoints = p)
            .Returns(Task.CompletedTask);

        await harness.Consume(new ProcessDocumentMessage(doc.Id, doc.TenantId, Guid.NewGuid()));

        // ChunkType persisted on the child row.
        var codeChunk = await harness.Db.AiDocumentChunks
            .AsNoTracking()
            .Where(c => c.DocumentId == doc.Id && c.ChunkLevel == "child")
            .SingleAsync();
        codeChunk.ChunkType.Should().Be(ChunkType.Code);
        codeChunk.SectionTitle.Should().Be("H1 > H2");
        // Breadcrumb prepended to NormalizedContent. StartsWith allows the
        // Arabic normalizer to reshape the body following the prefix/newline.
        codeChunk.NormalizedContent.Should().NotBeNull();
        codeChunk.NormalizedContent!.Should().StartWith("H1 > H2\n");

        // VectorPayload carries ChunkType through to Qdrant.
        capturedPoints.Should().NotBeNull();
        capturedPoints!.Should().ContainSingle();
        capturedPoints[0].Payload.ChunkType.Should().Be(ChunkType.Code);
    }

    [Fact]
    public async Task Passes_Tenant_And_User_Attribution_From_Message_To_Embedder()
    {
        var harness = new ConsumerHarness();
        var doc = harness.SeedDocument("doc.txt", "text/plain");
        var initiatingUser = Guid.NewGuid();

        harness.Extractor
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedDocument(new[] { new ExtractedPage(1, "x") }, UsedOcr: false));
        harness.Chunker
            .Setup(c => c.Chunk(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingOptions>()))
            .Returns(new HierarchicalChunks(
                Parents: new[] { new ChunkDraft(0, "p", 1, null, null, 1) },
                Children: new[] { new ChunkDraft(0, "c", 1, 0, null, 1) }));
        harness.Embedder
            .Setup(e => e.EmbedAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>(), It.IsAny<EmbedAttribution?>()))
            .ReturnsAsync(new[] { new[] { 0.1f } });
        harness.Embedder.SetupGet(e => e.VectorSize).Returns(1);

        await harness.Consume(new ProcessDocumentMessage(doc.Id, doc.TenantId, initiatingUser));

        harness.Embedder.Verify(
            e => e.EmbedAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>(),
                It.Is<EmbedAttribution?>(a => a != null && a.UserId == initiatingUser && a.TenantId == doc.TenantId)),
            Times.Once);
    }

    private sealed class ConsumerHarness
    {
        public AiDbContext Db { get; }
        public Mock<IStorageService> Storage { get; } = new();
        public Mock<IDocumentTextExtractorRegistry> Registry { get; } = new();
        public Mock<IDocumentTextExtractor> Extractor { get; } = new();
        public Mock<IDocumentChunker> Chunker { get; } = new();
        public Mock<IEmbeddingService> Embedder { get; } = new();
        public Mock<IVectorStore> VectorStore { get; } = new();

        private readonly IServiceScopeFactory _scopeFactory;

        public ConsumerHarness()
        {
            var services = new ServiceCollection();

            var dbName = $"ai-{Guid.NewGuid():N}";
            services.AddDbContext<AiDbContext>(o => o.UseInMemoryDatabase(dbName));
            services.AddSingleton(Storage.Object);
            services.AddSingleton(Registry.Object);
            services.AddSingleton(Chunker.Object);
            services.AddSingleton(Embedder.Object);
            services.AddSingleton(VectorStore.Object);
            services.AddSingleton(Options.Create(new AiRagSettings()));
            services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

            var sp = services.BuildServiceProvider();
            _scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            Db = sp.CreateScope().ServiceProvider.GetRequiredService<AiDbContext>();

            Registry.Setup(r => r.Resolve(It.IsAny<string>())).Returns(Extractor.Object);
            Storage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, CancellationToken _) => new MemoryStream(new byte[] { 1, 2, 3 }));
        }

        public AiDocument SeedDocument(string fileName, string contentType, Guid? tenantId = null)
        {
            var doc = AiDocument.Create(
                tenantId: tenantId ?? Guid.NewGuid(),
                name: fileName,
                fileName: fileName,
                fileRef: $"ai/documents/{Guid.NewGuid():N}/{fileName}",
                contentType: contentType,
                sizeBytes: 100,
                uploadedByUserId: Guid.NewGuid());
            Db.AiDocuments.Add(doc);
            Db.SaveChanges();
            return doc;
        }

        public Task Consume(ProcessDocumentMessage message)
        {
            var consumer = new ProcessDocumentConsumer(_scopeFactory);
            var ctx = new Mock<ConsumeContext<ProcessDocumentMessage>>();
            ctx.SetupGet(c => c.Message).Returns(message);
            ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
            return consumer.Consume(ctx.Object);
        }
    }
}
