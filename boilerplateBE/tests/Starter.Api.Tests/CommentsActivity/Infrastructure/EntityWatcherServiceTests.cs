using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Module.CommentsActivity.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.CommentsActivity.Infrastructure;

public sealed class EntityWatcherServiceTests
{
    private const string EntityType = "Product";
    private static readonly Guid EntityId = Guid.NewGuid();
    private static readonly Guid AcmeTenant = Guid.NewGuid();
    private static readonly Guid GlobexTenant = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task WatchAsync_NoResolver_PersistsWithCallerTenantId()
    {
        using var db = TestDbContextFactory.InMemory();
        var registry = new Mock<ICommentableEntityRegistry>();
        registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(TestDefinitions.With(EntityType, resolver: null));

        var sut = new EntityWatcherService(
            db, registry.Object, Mock.Of<IServiceProvider>(),
            NullLogger<EntityWatcherService>.Instance);

        await sut.WatchAsync(EntityType, EntityId, AcmeTenant, UserId);

        var watcher = db.EntityWatchers.IgnoreQueryFilters().Single();
        watcher.TenantId.Should().Be(AcmeTenant);
        watcher.UserId.Should().Be(UserId);
    }

    [Fact]
    public async Task WatchAsync_ResolverDisagrees_PersistsWithResolvedTenantId()
    {
        using var db = TestDbContextFactory.InMemory();
        var registry = new Mock<ICommentableEntityRegistry>();
        registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(TestDefinitions.With(EntityType,
                resolver: (_, _, _) => Task.FromResult<Guid?>(AcmeTenant)));

        var sut = new EntityWatcherService(
            db, registry.Object, Mock.Of<IServiceProvider>(),
            NullLogger<EntityWatcherService>.Instance);

        await sut.WatchAsync(EntityType, EntityId, GlobexTenant, UserId);

        var watcher = db.EntityWatchers.IgnoreQueryFilters().Single();
        watcher.TenantId.Should().Be(AcmeTenant);
    }

    [Fact]
    public async Task WatchAsync_AlreadyWatching_IsIdempotent()
    {
        using var db = TestDbContextFactory.InMemory();
        var registry = new Mock<ICommentableEntityRegistry>();
        registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(TestDefinitions.With(EntityType, resolver: null));

        var sut = new EntityWatcherService(
            db, registry.Object, Mock.Of<IServiceProvider>(),
            NullLogger<EntityWatcherService>.Instance);

        await sut.WatchAsync(EntityType, EntityId, AcmeTenant, UserId);
        await sut.WatchAsync(EntityType, EntityId, AcmeTenant, UserId);

        db.EntityWatchers.IgnoreQueryFilters().Count().Should().Be(1);
    }

    [Fact]
    public async Task UnwatchAsync_ResolverAgreesOrAbsent_Removes()
    {
        using var db = TestDbContextFactory.InMemory();
        var registry = new Mock<ICommentableEntityRegistry>();
        registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(TestDefinitions.With(EntityType, resolver: null));

        var sut = new EntityWatcherService(
            db, registry.Object, Mock.Of<IServiceProvider>(),
            NullLogger<EntityWatcherService>.Instance);

        await sut.WatchAsync(EntityType, EntityId, AcmeTenant, UserId);
        await sut.UnwatchAsync(EntityType, EntityId, UserId);

        db.EntityWatchers.IgnoreQueryFilters().Any().Should().BeFalse();
    }

    [Fact]
    public async Task UnwatchAsync_ResolverDisagrees_KeepsWatcher()
    {
        using var db = TestDbContextFactory.InMemory();
        var registry = new Mock<ICommentableEntityRegistry>();
        registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(TestDefinitions.With(EntityType, resolver: null));

        var sut = new EntityWatcherService(
            db, registry.Object, Mock.Of<IServiceProvider>(),
            NullLogger<EntityWatcherService>.Instance);

        // Seed Acme watcher under no-resolver regime.
        await sut.WatchAsync(EntityType, EntityId, AcmeTenant, UserId);

        // Swap in a resolver that now claims Globex ownership.
        registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(TestDefinitions.With(EntityType,
                resolver: (_, _, _) => Task.FromResult<Guid?>(GlobexTenant)));

        await sut.UnwatchAsync(EntityType, EntityId, UserId);

        db.EntityWatchers.IgnoreQueryFilters().Count().Should().Be(1);
    }
}
