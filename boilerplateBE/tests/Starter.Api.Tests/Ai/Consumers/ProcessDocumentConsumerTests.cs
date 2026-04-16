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

        public AiDocument SeedDocument(string fileName, string contentType)
        {
            var doc = AiDocument.Create(
                tenantId: Guid.NewGuid(),
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
