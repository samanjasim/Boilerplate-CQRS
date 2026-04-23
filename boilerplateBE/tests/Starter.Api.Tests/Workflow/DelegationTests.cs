using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Module.Workflow.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class DelegationTests : IDisposable
{
    private readonly WorkflowDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _originalAssigneeId = Guid.NewGuid();
    private readonly Guid _delegateId = Guid.NewGuid();

    public DelegationTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WorkflowDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private AssigneeResolverService CreateResolver()
    {
        // SpecificUser provider that returns _originalAssigneeId
        var builtIn = new BuiltInAssigneeProvider(Mock.Of<IRoleUserReader>());
        var userReader = Mock.Of<IUserReader>();
        var logger = NullLogger<AssigneeResolverService>.Instance;

        return new AssigneeResolverService(
            new IAssigneeResolverProvider[] { builtIn },
            userReader,
            logger,
            _db);
    }

    private static WorkflowAssigneeContext MakeContext(Guid? tenantId = null)
        => new(
            EntityType: "Order",
            EntityId: Guid.NewGuid(),
            TenantId: tenantId ?? Guid.NewGuid(),
            InitiatorUserId: Guid.NewGuid(),
            CurrentState: "Pending");

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ActiveDelegation_SwapsAssignee()
    {
        // Arrange: active delegation from originalAssignee to delegate
        var rule = DelegationRule.Create(
            _tenantId,
            _originalAssigneeId,
            _delegateId,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(5));

        _db.DelegationRules.Add(rule);
        await _db.SaveChangesAsync();

        var sut = CreateResolver();
        var config = new AssigneeConfig(
            "SpecificUser",
            new Dictionary<string, object> { ["userId"] = _originalAssigneeId.ToString() });

        // Act
        var result = await sut.ResolveWithDelegationAsync(config, MakeContext(_tenantId));

        // Assert
        result.AssigneeIds.Should().ContainSingle().Which.Should().Be(_delegateId);
        result.DelegationMap.Should().ContainKey(_delegateId);
        result.DelegationMap[_delegateId].Should().Be(_originalAssigneeId);
    }

    [Fact]
    public async Task ResolveAsync_NoDelegation_KeepsOriginal()
    {
        // Arrange: no delegation rules exist
        var sut = CreateResolver();
        var config = new AssigneeConfig(
            "SpecificUser",
            new Dictionary<string, object> { ["userId"] = _originalAssigneeId.ToString() });

        // Act
        var result = await sut.ResolveWithDelegationAsync(config, MakeContext(_tenantId));

        // Assert
        result.AssigneeIds.Should().ContainSingle().Which.Should().Be(_originalAssigneeId);
        result.DelegationMap.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_ExpiredDelegation_KeepsOriginal()
    {
        // Arrange: delegation rule has expired
        var rule = DelegationRule.Create(
            _tenantId,
            _originalAssigneeId,
            _delegateId,
            DateTime.UtcNow.AddDays(-10),
            DateTime.UtcNow.AddDays(-1)); // ended yesterday

        _db.DelegationRules.Add(rule);
        await _db.SaveChangesAsync();

        var sut = CreateResolver();
        var config = new AssigneeConfig(
            "SpecificUser",
            new Dictionary<string, object> { ["userId"] = _originalAssigneeId.ToString() });

        // Act
        var result = await sut.ResolveWithDelegationAsync(config, MakeContext(_tenantId));

        // Assert
        result.AssigneeIds.Should().ContainSingle().Which.Should().Be(_originalAssigneeId);
        result.DelegationMap.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_InactiveDelegation_KeepsOriginal()
    {
        // Arrange: delegation rule is deactivated
        var rule = DelegationRule.Create(
            _tenantId,
            _originalAssigneeId,
            _delegateId,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(5));
        rule.Deactivate();

        _db.DelegationRules.Add(rule);
        await _db.SaveChangesAsync();

        var sut = CreateResolver();
        var config = new AssigneeConfig(
            "SpecificUser",
            new Dictionary<string, object> { ["userId"] = _originalAssigneeId.ToString() });

        // Act
        var result = await sut.ResolveWithDelegationAsync(config, MakeContext(_tenantId));

        // Assert
        result.AssigneeIds.Should().ContainSingle().Which.Should().Be(_originalAssigneeId);
        result.DelegationMap.Should().BeEmpty();
    }
}
