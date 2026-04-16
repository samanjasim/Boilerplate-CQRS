using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Module.CommentsActivity.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.CommentsActivity.Infrastructure;

public sealed class CommentServiceTests
{
    private const string EntityType = "Product";
    private static readonly Guid EntityId = Guid.NewGuid();
    private static readonly Guid AcmeTenant = Guid.NewGuid();
    private static readonly Guid GlobexTenant = Guid.NewGuid();
    private static readonly Guid AuthorId = Guid.NewGuid();

    [Fact]
    public async Task AddCommentAsync_NoResolver_PersistsWithCallerTenantId()
    {
        using var db = TestDbContextFactory.InMemory();
        var registry = new Mock<ICommentableEntityRegistry>();
        registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(TestDefinitions.With(EntityType, resolver: null));

        var sut = new CommentService(db, registry.Object, Mock.Of<IServiceProvider>(), NullLogger<CommentService>.Instance);

        var id = await sut.AddCommentAsync(
            EntityType, EntityId, AcmeTenant, AuthorId,
            body: "hello", mentionsJson: null, attachmentFileIds: null);

        var stored = await db.Comments.FindAsync(id);
        stored.Should().NotBeNull();
        stored!.TenantId.Should().Be(AcmeTenant);
    }

    [Fact]
    public async Task AddCommentAsync_ResolverDisagrees_PersistsWithResolvedTenantId()
    {
        using var db = TestDbContextFactory.InMemory();
        var registry = new Mock<ICommentableEntityRegistry>();
        registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(TestDefinitions.With(EntityType,
                resolver: (_, _, _) => Task.FromResult<Guid?>(AcmeTenant)));

        var sut = new CommentService(db, registry.Object, Mock.Of<IServiceProvider>(), NullLogger<CommentService>.Instance);

        var id = await sut.AddCommentAsync(
            EntityType, EntityId, GlobexTenant, AuthorId,
            body: "cross-tenant attempt", mentionsJson: null, attachmentFileIds: null);

        var stored = await db.Comments.FindAsync(id);
        stored.Should().NotBeNull();
        stored!.TenantId.Should().Be(AcmeTenant);
    }

    [Fact]
    public async Task EditCommentAsync_ResolverDisagreesWithPersisted_RejectsEdit()
    {
        using var db = TestDbContextFactory.InMemory();
        var registry = new Mock<ICommentableEntityRegistry>();

        // Seed with Acme; resolver later claims ownership is Globex.
        registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(TestDefinitions.With(EntityType, resolver: null));

        var sut = new CommentService(db, registry.Object, Mock.Of<IServiceProvider>(), NullLogger<CommentService>.Instance);
        var id = await sut.AddCommentAsync(
            EntityType, EntityId, AcmeTenant, AuthorId,
            body: "original", mentionsJson: null, attachmentFileIds: null);

        registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(TestDefinitions.With(EntityType,
                resolver: (_, _, _) => Task.FromResult<Guid?>(GlobexTenant)));

        await sut.EditCommentAsync(id, "edited body", null, AuthorId);

        var stored = await db.Comments.FindAsync(id);
        stored!.Body.Should().Be("original");
    }

    [Fact]
    public async Task EditCommentAsync_ResolverAgreesOrAbsent_AppliesEdit()
    {
        using var db = TestDbContextFactory.InMemory();
        var registry = new Mock<ICommentableEntityRegistry>();
        registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(TestDefinitions.With(EntityType, resolver: null));

        var sut = new CommentService(db, registry.Object, Mock.Of<IServiceProvider>(), NullLogger<CommentService>.Instance);
        var id = await sut.AddCommentAsync(
            EntityType, EntityId, AcmeTenant, AuthorId,
            body: "original", mentionsJson: null, attachmentFileIds: null);

        await sut.EditCommentAsync(id, "edited body", null, AuthorId);

        var stored = await db.Comments.FindAsync(id);
        stored!.Body.Should().Be("edited body");
    }

    [Fact]
    public async Task DeleteCommentAsync_ResolverDisagreesWithPersisted_RejectsDelete()
    {
        using var db = TestDbContextFactory.InMemory();
        var registry = new Mock<ICommentableEntityRegistry>();
        registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(TestDefinitions.With(EntityType, resolver: null));

        var sut = new CommentService(db, registry.Object, Mock.Of<IServiceProvider>(), NullLogger<CommentService>.Instance);
        var id = await sut.AddCommentAsync(
            EntityType, EntityId, AcmeTenant, AuthorId,
            body: "original", mentionsJson: null, attachmentFileIds: null);

        registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(TestDefinitions.With(EntityType,
                resolver: (_, _, _) => Task.FromResult<Guid?>(GlobexTenant)));

        await sut.DeleteCommentAsync(id, AuthorId);

        var stored = await db.Comments.FindAsync(id);
        stored!.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteCommentAsync_ResolverAgreesOrAbsent_SoftDeletes()
    {
        using var db = TestDbContextFactory.InMemory();
        var registry = new Mock<ICommentableEntityRegistry>();
        registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(TestDefinitions.With(EntityType, resolver: null));

        var sut = new CommentService(db, registry.Object, Mock.Of<IServiceProvider>(), NullLogger<CommentService>.Instance);
        var id = await sut.AddCommentAsync(
            EntityType, EntityId, AcmeTenant, AuthorId,
            body: "original", mentionsJson: null, attachmentFileIds: null);

        await sut.DeleteCommentAsync(id, AuthorId);

        var stored = await db.Comments.FindAsync(id);
        stored!.IsDeleted.Should().BeTrue();
    }
}
