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
    [Fact]
    public async Task Returns_Items_Mapped_From_RetrievedContext()
    {
        var tenantId = Guid.NewGuid();
        var retrieval = new FakeRetrievalService();
        var currentUser = new FakeCurrentUser(tenantId);
        var opts = Options.Create(new AiRagSettings { TopK = 5, RetrievalTopK = 20 });
        var handler = new SearchKnowledgeBaseQueryHandler(retrieval, currentUser, opts);

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
        var tenantId = Guid.NewGuid();
        var retrieval = new FakeRetrievalServiceWithParent();
        var currentUser = new FakeCurrentUser(tenantId);
        var opts = Options.Create(new AiRagSettings { TopK = 5, RetrievalTopK = 20 });
        var handler = new SearchKnowledgeBaseQueryHandler(retrieval, currentUser, opts);

        var result = await handler.Handle(
            new SearchKnowledgeBaseQuery("q", null, 5, null, true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var parent = result.Value!.Items.Single(i => i.ChunkLevel == "parent");
        parent.HybridScore.Should().BeNull();
        parent.SemanticScore.Should().BeNull();
        parent.KeywordScore.Should().BeNull();
    }
}

internal sealed class FakeRetrievalService : IRagRetrievalService
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
        CancellationToken ct)
    {
        var child = new RetrievedChunk(
            Guid.NewGuid(), Guid.NewGuid(), "Doc", "content", null, null,
            "child", 0.9m, 0.3m, 0.7m, null);
        return Task.FromResult(new RetrievedContext([child], [], 10, false));
    }
}

internal sealed class FakeRetrievalServiceWithParent : IRagRetrievalService
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
        CancellationToken ct)
    {
        var parentId = Guid.NewGuid();
        var child = new RetrievedChunk(
            Guid.NewGuid(), Guid.NewGuid(), "Doc", "child-content", null, null,
            "child", 0.9m, 0.3m, 0.7m, parentId);
        var parent = new RetrievedChunk(
            parentId, child.DocumentId, "Doc", "parent-content", null, null,
            "parent", 0m, 0m, 0m, null);
        return Task.FromResult(new RetrievedContext([child], [parent], 20, false));
    }
}

internal sealed class FakeCurrentUser : ICurrentUserService
{
    public FakeCurrentUser(Guid? tenant) => TenantId = tenant;
    public Guid? TenantId { get; }
    public Guid? UserId => Guid.NewGuid();
    public string? Email => null;
    public bool IsAuthenticated => true;
    public IEnumerable<string> Roles => [];
    public IEnumerable<string> Permissions => [];
    public bool IsInRole(string role) => false;
    public bool HasPermission(string permission) => false;
}
