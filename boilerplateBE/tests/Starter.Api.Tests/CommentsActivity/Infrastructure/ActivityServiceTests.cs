using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Events.CommentsActivity;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.CommentsActivity.Infrastructure;

public sealed class ActivityServiceTests
{
    private const string EntityType = "Product";
    private static readonly Guid EntityId = Guid.NewGuid();
    private static readonly Guid AcmeTenant = Guid.NewGuid();
    private static readonly Guid GlobexTenant = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    [Fact]
    public async Task RecordAsync_NoResolver_PersistsWithCallerTenantId()
    {
        using var db = TestDbContextFactory.InMemory();
        var registry = new Mock<ICommentableEntityRegistry>();
        registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(TestDefinitions.With(EntityType, resolver: null));
        var publisher = new Mock<IMessagePublisher>();
        var clock = TimeProvider.System;

        var sut = new ActivityService(
            db, registry.Object, Mock.Of<IServiceProvider>(),
            publisher.Object, clock, NullLogger<ActivityService>.Instance);

        await sut.RecordAsync(EntityType, EntityId, AcmeTenant, "custom_action", ActorId);

        var stored = await db.ActivityEntries.FindAsync(db.ActivityEntries.Single().Id);
        stored!.TenantId.Should().Be(AcmeTenant);
        stored.Action.Should().Be("custom_action");
    }

    [Fact]
    public async Task RecordAsync_ResolverDisagrees_PersistsWithResolvedTenantId()
    {
        using var db = TestDbContextFactory.InMemory();
        var registry = new Mock<ICommentableEntityRegistry>();
        registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(TestDefinitions.With(EntityType,
                resolver: (_, _, _) => Task.FromResult<Guid?>(AcmeTenant)));
        var publisher = new Mock<IMessagePublisher>();
        var clock = TimeProvider.System;

        var sut = new ActivityService(
            db, registry.Object, Mock.Of<IServiceProvider>(),
            publisher.Object, clock, NullLogger<ActivityService>.Instance);

        await sut.RecordAsync(EntityType, EntityId, GlobexTenant, "custom_action", ActorId);

        var stored = db.ActivityEntries.Single();
        stored.TenantId.Should().Be(AcmeTenant);
    }

    [Fact]
    public async Task RecordAsync_PublishesIntegrationEventExactlyOnce()
    {
        using var db = TestDbContextFactory.InMemory();
        var registry = new Mock<ICommentableEntityRegistry>();
        registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(TestDefinitions.With(EntityType, resolver: null));
        var publisher = new Mock<IMessagePublisher>();
        var clock = TimeProvider.System;

        var sut = new ActivityService(
            db, registry.Object, Mock.Of<IServiceProvider>(),
            publisher.Object, clock, NullLogger<ActivityService>.Instance);

        await sut.RecordAsync(
            EntityType, EntityId, AcmeTenant,
            "custom_action", ActorId,
            metadataJson: """{"k":"v"}""",
            description: "did a thing");

        publisher.Verify(
            p => p.PublishAsync(
                It.Is<ActivityRecordedIntegrationEvent>(e =>
                    e.EntityType == EntityType &&
                    e.EntityId == EntityId &&
                    e.TenantId == AcmeTenant &&
                    e.Action == "custom_action" &&
                    e.ActorId == ActorId &&
                    e.MetadataJson == """{"k":"v"}""" &&
                    e.Description == "did a thing"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
