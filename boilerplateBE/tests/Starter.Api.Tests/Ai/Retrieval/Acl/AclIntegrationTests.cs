using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Api.Tests.Ai.Retrieval;
using Starter.Domain.Common.Access.Enums;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Reranking;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval.Acl;

public sealed class AclIntegrationTests
{
    private static AiDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"acl-{Guid.NewGuid():N}").Options,
            currentUserService: null);

    private static AiRagSettings DefaultSettings() => new()
    {
        TopK = 5,
        RetrievalTopK = 10,
        VectorWeight = 1.0m,
        KeywordWeight = 1.0m,
        MaxContextTokens = 4000,
        MinHybridScore = 0.0m,
        RerankStrategy = RerankStrategy.Off,
        StageTimeoutAclResolveMs = 500
    };

    private static RagRetrievalService BuildService(
        AiDbContext db,
        FakeVectorStore vs,
        FakeResourceAccessService accessSvc,
        FakeCurrentUserService currentUser,
        AiRagSettings? settings = null)
    {
        var s = settings ?? DefaultSettings();
        return new RagRetrievalService(
            db, vs, new FakeKeywordSearchService(),
            new FakeEmbeddingService(),
            new NoOpQueryRewriter(),
            new NoOpContextualQueryResolver(),
            new NoOpQuestionClassifier(),
            new NoOpReranker(),
            new RerankStrategySelector(s),
            new NoOpNeighborExpander(),
            new TokenCounter(),
            accessSvc,
            currentUser,
            Options.Create(s),
            NullLogger<RagRetrievalService>.Instance);
    }

    private static (AiAssistant assistant, AiDocumentChunk chunk) SeedAssistantAndChunk(
        AiDbContext db, Guid tenantId)
    {
        var docId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var chunk = AiDocumentChunk.Create(docId, "child", "test content", 0, 10, pointId);
        db.AiDocumentChunks.Add(chunk);
        db.SaveChanges();

        var assistant = AiAssistant.Create(tenantId, "Test", null, "gpt", Guid.NewGuid());
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);
        return (assistant, chunk);
    }

    [Fact]
    public async Task CallerPrincipal_AllTenant_noGrants_passesNonNullFilterToVectorStore()
    {
        var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (assistant, chunk) = SeedAssistantAndChunk(db, tenantId);

        var vs = new FakeVectorStore { HitsToReturn = { new VectorSearchHit(chunk.QdrantPointId, 0.9m) } };
        var accessSvc = new FakeResourceAccessService { ForceAdminBypass = false };
        var currentUser = new FakeCurrentUserService { UserId = userId, TenantId = tenantId };

        var svc = BuildService(db, vs, accessSvc, currentUser);
        await svc.RetrieveForTurnAsync(assistant, "query", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        vs.LastAclFilter.Should().NotBeNull("CallerPrincipal mode must pass an ACL filter");
        vs.LastAclFilter!.UserId.Should().Be(userId);
        vs.LastAclFilter.GrantedFileIds.Should().BeEmpty("no explicit grants were set up");
        accessSvc.ResolveCallCount.Should().Be(1);
    }

    [Fact]
    public async Task CallerPrincipal_AllTenant_withGrant_filterCarriesGrantedFileIds()
    {
        var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grantedFileId = Guid.NewGuid();
        var (assistant, chunk) = SeedAssistantAndChunk(db, tenantId);

        var vs = new FakeVectorStore { HitsToReturn = { new VectorSearchHit(chunk.QdrantPointId, 0.9m) } };
        var accessSvc = new FakeResourceAccessService { ForceAdminBypass = false };
        accessSvc.GrantedResourceIds.Add(grantedFileId);
        var currentUser = new FakeCurrentUserService { UserId = userId, TenantId = tenantId };

        var svc = BuildService(db, vs, accessSvc, currentUser);
        await svc.RetrieveForTurnAsync(assistant, "query", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        vs.LastAclFilter.Should().NotBeNull();
        vs.LastAclFilter!.GrantedFileIds.Should().ContainSingle()
            .Which.Should().Be(grantedFileId);
    }

    [Fact]
    public async Task AssistantPrincipal_AllTenant_ignoresCallerAcl_passesNullFilter()
    {
        var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var (assistant, chunk) = SeedAssistantAndChunk(db, tenantId);
        assistant.SetAccessMode(AssistantAccessMode.AssistantPrincipal);

        var vs = new FakeVectorStore { HitsToReturn = { new VectorSearchHit(chunk.QdrantPointId, 0.9m) } };
        var accessSvc = new FakeResourceAccessService { ForceAdminBypass = false };
        var currentUser = new FakeCurrentUserService { UserId = Guid.NewGuid(), TenantId = tenantId };

        var svc = BuildService(db, vs, accessSvc, currentUser);
        await svc.RetrieveForTurnAsync(assistant, "query", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        vs.LastAclFilter.Should().BeNull("AssistantPrincipal mode must not apply per-caller ACL filter");
        accessSvc.ResolveCallCount.Should().Be(0, "resolver must not be called for AssistantPrincipal");
    }

    [Fact]
    public async Task AdminBypass_callsVectorSearchWithNullAclFilter()
    {
        var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var (assistant, chunk) = SeedAssistantAndChunk(db, tenantId);

        var vs = new FakeVectorStore { HitsToReturn = { new VectorSearchHit(chunk.QdrantPointId, 0.9m) } };
        var accessSvc = new FakeResourceAccessService { ForceAdminBypass = true };
        var currentUser = new FakeCurrentUserService { UserId = Guid.NewGuid(), TenantId = tenantId };

        var svc = BuildService(db, vs, accessSvc, currentUser);
        await svc.RetrieveForTurnAsync(assistant, "query", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        vs.LastAclFilter.Should().BeNull("admin bypass must not restrict results with an ACL filter");
    }

    [Fact]
    public async Task AclResolveDegraded_populatesDegradedStages_andReturnsEmptyContext()
    {
        var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var (assistant, chunk) = SeedAssistantAndChunk(db, tenantId);

        var vs = new FakeVectorStore { HitsToReturn = { new VectorSearchHit(chunk.QdrantPointId, 0.9m) } };
        var accessSvc = new FakeResourceAccessService { ForceAdminBypass = false, ThrowOnResolve = true };
        var currentUser = new FakeCurrentUserService { UserId = Guid.NewGuid(), TenantId = tenantId };

        var svc = BuildService(db, vs, accessSvc, currentUser);
        var ctx = await svc.RetrieveForTurnAsync(assistant, "query", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        ctx.DegradedStages.Should().Contain(s => s.StartsWith("acl-resolve"),
            "a resolver failure must surface as a degraded stage");
        ctx.Children.Should().BeEmpty("fail-closed: no results when ACL resolve fails");
    }

    [Fact]
    public async Task CallerPrincipal_nullUserId_skipsAclFilter()
    {
        var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var (assistant, chunk) = SeedAssistantAndChunk(db, tenantId);

        var vs = new FakeVectorStore { HitsToReturn = { new VectorSearchHit(chunk.QdrantPointId, 0.9m) } };
        var accessSvc = new FakeResourceAccessService { ForceAdminBypass = false };
        var currentUser = new FakeCurrentUserService { UserId = null, TenantId = tenantId };

        var svc = BuildService(db, vs, accessSvc, currentUser);
        await svc.RetrieveForTurnAsync(assistant, "query", Array.Empty<RagHistoryMessage>(), CancellationToken.None);

        vs.LastAclFilter.Should().BeNull("unauthenticated callers (null UserId) must not build an ACL filter");
        accessSvc.ResolveCallCount.Should().Be(0);
    }

    [Fact]
    public async Task DirectQuery_CallerPrincipal_buildsAclFilter()
    {
        var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var docId = Guid.NewGuid();
        var pointId = Guid.NewGuid();
        var chunk = AiDocumentChunk.Create(docId, "child", "content", 0, 10, pointId);
        db.AiDocumentChunks.Add(chunk);
        await db.SaveChangesAsync();

        var vs = new FakeVectorStore { HitsToReturn = { new VectorSearchHit(pointId, 0.9m) } };
        var accessSvc = new FakeResourceAccessService { ForceAdminBypass = false };
        var currentUser = new FakeCurrentUserService { UserId = userId, TenantId = tenantId };

        var svc = BuildService(db, vs, accessSvc, currentUser);
        await svc.RetrieveForQueryAsync(tenantId, "query", null, 5, null, false, CancellationToken.None);

        vs.LastAclFilter.Should().NotBeNull(
            "RetrieveForQueryAsync always runs as CallerPrincipal and must pass ACL filter");
        vs.LastAclFilter!.UserId.Should().Be(userId);
    }
}
