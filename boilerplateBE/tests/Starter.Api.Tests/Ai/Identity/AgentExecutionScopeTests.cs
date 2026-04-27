using FluentAssertions;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Infrastructure.Identity.Services;
using Starter.Module.AI.Infrastructure.Runtime;
using Xunit;

namespace Starter.Api.Tests.Ai.Identity;

public sealed class AgentExecutionScopeTests
{
    [Fact]
    public void HasPermission_Intersects_When_Both_Caller_And_Agent_Present()
    {
        using var scope = AgentExecutionScope.Begin(
            userId: Guid.NewGuid(),
            agentPrincipalId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            callerHasPermission: p => p == "Files.Read",
            agentHasPermission: p => p == "Files.Read" || p == "Files.Delete");

        scope.HasPermission("Files.Read").Should().BeTrue();
        scope.HasPermission("Files.Delete").Should().BeFalse(); // caller lacks
        scope.HasPermission("Files.Update").Should().BeFalse(); // both lack
    }

    [Fact]
    public void HasPermission_Operational_Run_Uses_Agent_Only()
    {
        using var scope = AgentExecutionScope.Begin(
            userId: null,
            agentPrincipalId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            callerHasPermission: null,
            agentHasPermission: p => p == "Files.Read");

        scope.HasPermission("Files.Read").Should().BeTrue();
        scope.HasPermission("Files.Delete").Should().BeFalse();
    }

    [Fact]
    public void Scope_Installs_Into_AmbientExecutionContext_And_Restores_On_Dispose()
    {
        AmbientExecutionContext.Current.Should().BeNull();

        using (var scope = AgentExecutionScope.Begin(
                   userId: Guid.NewGuid(),
                   agentPrincipalId: Guid.NewGuid(),
                   tenantId: Guid.NewGuid(),
                   callerHasPermission: _ => true,
                   agentHasPermission: _ => true))
        {
            AmbientExecutionContext.Current.Should().BeSameAs(scope);
        }

        AmbientExecutionContext.Current.Should().BeNull();
    }

    [Fact]
    public void AttachRunId_Sets_AgentRunId_After_Begin()
    {
        var runId = Guid.NewGuid();
        using var scope = AgentExecutionScope.Begin(
            userId: null, agentPrincipalId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(), callerHasPermission: null,
            agentHasPermission: _ => true);

        scope.AgentRunId.Should().BeNull();
        scope.AttachRunId(runId);
        scope.AgentRunId.Should().Be(runId);
    }
}

public sealed class HttpExecutionContextTests
{
    [Fact]
    public void Wraps_CurrentUser_HasPermission_And_Reports_No_AgentPrincipal()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(userId);
        cu.SetupGet(x => x.TenantId).Returns(tenantId);
        cu.Setup(x => x.HasPermission("Files.Read")).Returns(true);

        var sut = new HttpExecutionContext(cu.Object);
        sut.UserId.Should().Be(userId);
        sut.TenantId.Should().Be(tenantId);
        sut.AgentPrincipalId.Should().BeNull();
        sut.HasPermission("Files.Read").Should().BeTrue();
        sut.HasPermission("Files.Delete").Should().BeFalse();
    }
}
