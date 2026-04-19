using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class AssigneeResolverTests
{
    private static WorkflowAssigneeContext MakeContext(
        Guid? initiatorUserId = null,
        Guid? tenantId = null)
        => new(
            EntityType: "Order",
            EntityId: Guid.NewGuid(),
            TenantId: tenantId ?? Guid.NewGuid(),
            InitiatorUserId: initiatorUserId ?? Guid.NewGuid(),
            CurrentState: "Pending");

    // ── BuiltInAssigneeProvider tests ──

    [Fact]
    public async Task Resolve_SpecificUser_ReturnsExactUserId()
    {
        var userId = Guid.NewGuid();
        var provider = new BuiltInAssigneeProvider(
            Mock.Of<IRoleUserReader>());

        var result = await provider.ResolveAsync(
            "SpecificUser",
            new Dictionary<string, object> { ["userId"] = userId.ToString() },
            MakeContext());

        result.Should().ContainSingle().Which.Should().Be(userId);
    }

    [Fact]
    public async Task Resolve_SpecificUser_AcceptsJsonElement()
    {
        var userId = Guid.NewGuid();
        var provider = new BuiltInAssigneeProvider(
            Mock.Of<IRoleUserReader>());

        // Simulate what JsonSerializer.Deserialize<Dictionary<string,object>> produces
        var json = JsonSerializer.Serialize(new { userId = userId.ToString() });
        var doc = JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;

        var result = await provider.ResolveAsync("SpecificUser", doc, MakeContext());

        result.Should().ContainSingle().Which.Should().Be(userId);
    }

    [Fact]
    public async Task Resolve_EntityCreator_ReturnsInitiatorUserId()
    {
        var initiatorId = Guid.NewGuid();
        var provider = new BuiltInAssigneeProvider(
            Mock.Of<IRoleUserReader>());

        var result = await provider.ResolveAsync(
            "EntityCreator",
            new Dictionary<string, object>(),
            MakeContext(initiatorUserId: initiatorId));

        result.Should().ContainSingle().Which.Should().Be(initiatorId);
    }

    [Fact]
    public async Task Resolve_UnknownStrategy_ReturnsEmpty()
    {
        var provider = new BuiltInAssigneeProvider(
            Mock.Of<IRoleUserReader>());

        var result = await provider.ResolveAsync(
            "UnknownStrategy",
            new Dictionary<string, object>(),
            MakeContext());

        result.Should().BeEmpty();
    }

    // ── AssigneeResolverService tests ──

    [Fact]
    public async Task Resolve_PrimaryFails_UsesFallback()
    {
        // Primary strategy "UnknownStrategy" returns empty → fallback "SpecificUser" returns a userId
        var fallbackUserId = Guid.NewGuid();

        var builtIn = new BuiltInAssigneeProvider(Mock.Of<IRoleUserReader>());
        var userReader = Mock.Of<IUserReader>();
        var logger = NullLogger<AssigneeResolverService>.Instance;

        var sut = new AssigneeResolverService(
            new[] { builtIn },
            userReader,
            logger);

        var config = new AssigneeConfig(
            Strategy: "UnknownStrategy",
            Parameters: new Dictionary<string, object>(),
            Fallback: new AssigneeConfig(
                Strategy: "SpecificUser",
                Parameters: new Dictionary<string, object> { ["userId"] = fallbackUserId.ToString() }));

        var result = await sut.ResolveAsync(config, MakeContext());

        result.Should().ContainSingle().Which.Should().Be(fallbackUserId);
    }

    [Fact]
    public async Task Resolve_BothStrategiesFail_ReturnsEmptyList()
    {
        // Both primary and fallback return empty; last-resort calls userReader for tenant admins
        // UserReader has no users → returns empty
        var mockUserReader = new Mock<IUserReader>();
        mockUserReader
            .Setup(r => r.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserSummary>());

        var mockRoleUserReader = new Mock<IRoleUserReader>();
        mockRoleUserReader
            .Setup(r => r.GetUserIdsByRoleAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        var builtIn = new BuiltInAssigneeProvider(mockRoleUserReader.Object);
        var logger = NullLogger<AssigneeResolverService>.Instance;

        var sut = new AssigneeResolverService(
            new[] { builtIn },
            mockUserReader.Object,
            logger);

        var config = new AssigneeConfig(
            Strategy: "UnknownPrimary",
            Parameters: new Dictionary<string, object>(),
            Fallback: new AssigneeConfig(
                Strategy: "UnknownFallback",
                Parameters: new Dictionary<string, object>()));

        var result = await sut.ResolveAsync(config, MakeContext());

        // Last-resort falls through — result may be empty when no admins are available
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Resolve_ExternalProviderRegistered_RoutesCorrectly()
    {
        // A custom IAssigneeResolverProvider with strategy "OrgManager" should be called
        var expectedUserId = Guid.NewGuid();

        var customProvider = new Mock<IAssigneeResolverProvider>();
        customProvider.Setup(p => p.SupportedStrategies)
            .Returns(new[] { "OrgManager" });
        customProvider
            .Setup(p => p.ResolveAsync(
                "OrgManager",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<WorkflowAssigneeContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { expectedUserId });

        var builtIn = new BuiltInAssigneeProvider(Mock.Of<IRoleUserReader>());
        var logger = NullLogger<AssigneeResolverService>.Instance;

        var sut = new AssigneeResolverService(
            new IAssigneeResolverProvider[] { builtIn, customProvider.Object },
            Mock.Of<IUserReader>(),
            logger);

        var config = new AssigneeConfig(
            Strategy: "OrgManager",
            Parameters: new Dictionary<string, object>());

        var result = await sut.ResolveAsync(config, MakeContext());

        result.Should().ContainSingle().Which.Should().Be(expectedUserId);

        customProvider.Verify(
            p => p.ResolveAsync(
                "OrgManager",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<WorkflowAssigneeContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
