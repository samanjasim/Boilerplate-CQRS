using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.ReprocessDocument;
using Starter.Module.AI.Application.Messages;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Commands;

public sealed class ReprocessDocumentCommandHandlerTests
{
    [Fact]
    public async Task Removes_Existing_Chunks_And_Calls_Vector_Delete_Before_Publishing()
    {
        var harness = new ReprocessHarness();
        var doc = harness.SeedCompletedDocumentWithChunks(chunkCount: 3);

        var result = await harness.Handle(new ReprocessDocumentCommand(doc.Id));

        result.IsSuccess.Should().BeTrue();

        var remainingChunks = await harness.Db.AiDocumentChunks
            .IgnoreQueryFilters()
            .Where(c => c.DocumentId == doc.Id)
            .ToListAsync();
        remainingChunks.Should().BeEmpty();

        harness.VectorStore.DeleteCalls
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be((doc.TenantId!.Value, doc.Id));

        harness.Bus.Verify(
            b => b.Publish(
                It.Is<ProcessDocumentMessage>(m => m.DocumentId == doc.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // The handler must wipe vectors before publishing the reprocess message,
        // otherwise the consumer can race an in-flight embed against the deletion.
        harness.CallLog.Should().Equal("delete", "publish");

        var reloaded = await harness.Db.AiDocuments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(d => d.Id == doc.Id);
        reloaded.EmbeddingStatus.Should().Be(EmbeddingStatus.Pending);
        reloaded.ChunkCount.Should().Be(0);
    }

    [Fact]
    public async Task Calls_Vector_Delete_With_Empty_Tenant_When_Document_Tenant_Is_Null()
    {
        var harness = new ReprocessHarness();
        var doc = harness.SeedCompletedDocumentWithChunks(chunkCount: 1, nullTenant: true);

        var result = await harness.Handle(new ReprocessDocumentCommand(doc.Id));

        result.IsSuccess.Should().BeTrue();
        harness.VectorStore.DeleteCalls
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be((Guid.Empty, doc.Id));
    }

    [Fact]
    public async Task Returns_Failure_When_Document_Missing()
    {
        var harness = new ReprocessHarness();

        var result = await harness.Handle(new ReprocessDocumentCommand(Guid.NewGuid()));

        result.IsSuccess.Should().BeFalse();
        harness.VectorStore.DeleteCalls.Should().BeEmpty();
        harness.Bus.Verify(
            b => b.Publish(It.IsAny<ProcessDocumentMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Returns_Failure_When_Document_Is_Already_Processing()
    {
        var harness = new ReprocessHarness();
        var doc = harness.SeedCompletedDocumentWithChunks(chunkCount: 1);
        doc.MarkProcessing();
        await harness.Db.SaveChangesAsync();

        var result = await harness.Handle(new ReprocessDocumentCommand(doc.Id));

        result.IsSuccess.Should().BeFalse();
        harness.VectorStore.DeleteCalls.Should().BeEmpty();
        harness.Bus.Verify(
            b => b.Publish(It.IsAny<ProcessDocumentMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class ReprocessHarness
    {
        public AiDbContext Db { get; }
        public Mock<IApplicationDbContext> AppDb { get; } = new();
        public RecordingVectorStore VectorStore { get; }
        public Mock<IPublishEndpoint> Bus { get; } = new();
        public List<string> CallLog { get; } = new();

        public ReprocessHarness()
        {
            var options = new DbContextOptionsBuilder<AiDbContext>()
                .UseInMemoryDatabase($"ai-reprocess-{Guid.NewGuid():N}")
                .Options;
            Db = new AiDbContext(options);
            VectorStore = new RecordingVectorStore(CallLog);

            AppDb.Setup(a => a.SaveChangesAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(0);

            Bus.Setup(b => b.Publish(
                    It.IsAny<ProcessDocumentMessage>(),
                    It.IsAny<CancellationToken>()))
               .Callback(() => CallLog.Add("publish"))
               .Returns(Task.CompletedTask);
        }

        public AiDocument SeedCompletedDocumentWithChunks(int chunkCount, bool nullTenant = false)
        {
            Guid? tenantId = nullTenant ? null : Guid.NewGuid();
            var doc = AiDocument.Create(
                tenantId: tenantId,
                name: "doc.txt",
                fileName: "doc.txt",
                fileRef: $"ai/documents/{Guid.NewGuid():N}/doc.txt",
                contentType: "text/plain",
                sizeBytes: 100,
                uploadedByUserId: Guid.NewGuid());

            doc.MarkCompleted(chunkCount);
            Db.AiDocuments.Add(doc);

            for (var i = 0; i < chunkCount; i++)
            {
                var chunk = AiDocumentChunk.Create(
                    documentId: doc.Id,
                    chunkLevel: "child",
                    content: $"chunk {i}",
                    chunkIndex: i,
                    tokenCount: 4,
                    qdrantPointId: Guid.NewGuid());
                Db.AiDocumentChunks.Add(chunk);
            }

            Db.SaveChanges();
            return doc;
        }

        public Task<Starter.Shared.Results.Result> Handle(ReprocessDocumentCommand command)
        {
            var handler = new ReprocessDocumentCommandHandler(Db, AppDb.Object, VectorStore, Bus.Object);
            return handler.Handle(command, CancellationToken.None);
        }
    }

    private sealed class RecordingVectorStore : IVectorStore
    {
        private readonly List<string> _callLog;

        public RecordingVectorStore(List<string> callLog)
        {
            _callLog = callLog;
        }

        public List<(Guid TenantId, Guid DocumentId)> DeleteCalls { get; } = new();

        public Task EnsureCollectionAsync(Guid tenantId, int vectorSize, CancellationToken ct)
            => Task.CompletedTask;

        public Task UpsertAsync(Guid tenantId, IReadOnlyList<VectorPoint> points, CancellationToken ct)
            => Task.CompletedTask;

        public Task DeleteByDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct)
        {
            DeleteCalls.Add((tenantId, documentId));
            _callLog.Add("delete");
            return Task.CompletedTask;
        }

        public Task DropCollectionAsync(Guid tenantId, CancellationToken ct)
            => Task.CompletedTask;

        public Task<IReadOnlyList<Starter.Module.AI.Application.Services.Retrieval.VectorSearchHit>> SearchAsync(
            Guid tenantId,
            float[] queryVector,
            IReadOnlyCollection<Guid>? documentFilter,
            int limit,
            CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<Starter.Module.AI.Application.Services.Retrieval.VectorSearchHit>>(
                Array.Empty<Starter.Module.AI.Application.Services.Retrieval.VectorSearchHit>());
        }

        public Task<IReadOnlyDictionary<Guid, float[]>> GetVectorsByIdsAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> pointIds,
            CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<Guid, float[]>>(new Dictionary<Guid, float[]>());
    }
}
