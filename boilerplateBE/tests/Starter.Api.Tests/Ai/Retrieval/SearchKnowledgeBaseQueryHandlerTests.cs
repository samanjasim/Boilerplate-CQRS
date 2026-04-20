using FluentAssertions;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Queries.SearchKnowledgeBase;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class SearchKnowledgeBaseQueryHandlerTests
{
    private static readonly AiRagSettings DefaultSettings = new() { TopK = 5, RetrievalTopK = 20 };

    [Fact]
    public async Task Returns_Items_Mapped_From_RetrievedContext()
    {
        var child = new RetrievedChunk(
            Guid.NewGuid(), Guid.NewGuid(), "Doc", "content", null, null,
            "child", 0.9m, 0.3m, 0.7m, null, 0);
        var retrieval = new FakeRetrievalService(new RetrievedContext([child], [], 10, false, [], []));
        var handler = BuildHandler(retrieval, Guid.NewGuid());

        var result = await handler.Handle(
            new SearchKnowledgeBaseQuery("q", null, 5, null, true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().NotBeEmpty();
        result.Value.Items[0].ChunkLevel.Should().Be("child");
        result.Value.Items[0].HybridScore.Should().Be(0.7m);
    }

    [Fact]
    public async Task Parent_Item_Has_Null_Scores()
    {
        var parentId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var child = new RetrievedChunk(
            Guid.NewGuid(), docId, "Doc", "child-content", null, null,
            "child", 0.9m, 0.3m, 0.7m, parentId, 0);
        var parent = new RetrievedChunk(
            parentId, docId, "Doc", "parent-content", null, null,
            "parent", 0m, 0m, 0m, null, 0);
        var retrieval = new FakeRetrievalService(new RetrievedContext([child], [parent], 20, false, [], []));
        var handler = BuildHandler(retrieval, Guid.NewGuid());

        var result = await handler.Handle(
            new SearchKnowledgeBaseQuery("q", null, 5, null, true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var parentItem = result.Value!.Items.Single(i => i.ChunkLevel == "parent");
        parentItem.HybridScore.Should().BeNull();
        parentItem.SemanticScore.Should().BeNull();
        parentItem.KeywordScore.Should().BeNull();
    }

    [Fact]
    public async Task Siblings_Are_Surfaced_As_Nearby_Separately_From_Items()
    {
        var docId = Guid.NewGuid();
        var child = new RetrievedChunk(
            Guid.NewGuid(), docId, "Doc", "anchor", null, null,
            "child", 0.9m, 0.3m, 0.7m, null, 0);
        var sibling = new RetrievedChunk(
            Guid.NewGuid(), docId, "Doc", "sibling", null, 1,
            "child", 0m, 0m, 0.35m, null, 1);
        var retrieval = new FakeRetrievalService(new RetrievedContext([child], [], 15, false, [], [sibling]));
        var handler = BuildHandler(retrieval, Guid.NewGuid());

        var result = await handler.Handle(
            new SearchKnowledgeBaseQuery("q", null, 5, null, false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Content.Should().Be("anchor");
        result.Value.Nearby.Should().HaveCount(1);
        result.Value.Nearby[0].Content.Should().Be("sibling");
    }

    [Fact]
    public async Task Returns_Failure_When_Tenant_Is_Null()
    {
        var child = new RetrievedChunk(
            Guid.NewGuid(), Guid.NewGuid(), "Doc", "content", null, null,
            "child", 0.9m, 0.3m, 0.7m, null, 0);
        var retrieval = new FakeRetrievalService(new RetrievedContext([child], [], 10, false, [], []));
        var handler = BuildHandler(retrieval, tenantId: null);

        var result = await handler.Handle(
            new SearchKnowledgeBaseQuery("q", null, 5, null, true),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Ai.SearchRequiresTenant");
    }

    private static SearchKnowledgeBaseQueryHandler BuildHandler(IRagRetrievalService retrieval, Guid? tenantId) =>
        new(retrieval, new FakeCurrentUser(tenantId), Options.Create(DefaultSettings));
}

internal sealed class FakeRetrievalService(RetrievedContext context) : IRagRetrievalService
{
    public Task<RetrievedContext> RetrieveForTurnAsync(
        Starter.Module.AI.Domain.Entities.AiAssistant a,
        string q,
        CancellationToken ct) => throw new NotSupportedException();

    public Task<RetrievedContext> RetrieveForQueryAsync(
        Guid tenantId,
        string q,
        IReadOnlyCollection<Guid>? f,
        int k,
        decimal? m,
        bool p,
        CancellationToken ct) => Task.FromResult(context);
}

internal sealed class FakeCurrentUser : ICurrentUserService
{
    public FakeCurrentUser(Guid? tenant) => TenantId = tenant;
    public Guid? TenantId { get; }
    public Guid? UserId { get; } = Guid.NewGuid();
    public string? Email => null;
    public bool IsAuthenticated => true;
    public IEnumerable<string> Roles => [];
    public IEnumerable<string> Permissions => [];
    public bool IsInRole(string role) => false;
    public bool HasPermission(string permission) => false;
}
