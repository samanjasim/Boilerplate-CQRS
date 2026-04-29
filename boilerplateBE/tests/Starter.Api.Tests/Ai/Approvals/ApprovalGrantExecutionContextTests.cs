using FluentAssertions;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Infrastructure.Runtime;
using Xunit;

namespace Starter.Api.Tests.Ai.Approvals;

public sealed class ApprovalGrantExecutionContextTests
{
    private sealed class StubInner : IExecutionContext
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public Guid? AgentPrincipalId { get; } = Guid.NewGuid();
        public Guid? TenantId { get; } = Guid.NewGuid();
        public Guid? AgentRunId => null;
        public bool DangerousActionApprovalGrant => false;
        public bool HasPermission(string permission) => permission == "ok";
    }

    [Fact]
    public void Wrapper_Sets_Grant_True_And_Delegates_Other_Members()
    {
        var inner = new StubInner();
        var wrapped = new ApprovalGrantExecutionContext(inner);

        wrapped.DangerousActionApprovalGrant.Should().BeTrue();
        wrapped.UserId.Should().Be(inner.UserId);
        wrapped.HasPermission("ok").Should().BeTrue();
        wrapped.HasPermission("nope").Should().BeFalse();
    }
}
