using FluentAssertions;
using Starter.Abstractions.Ai.Events;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Approvals;

public sealed class AiPendingApprovalTests
{
    private static AiPendingApproval Make(Guid? convId = null, Guid? taskId = null) =>
        AiPendingApproval.Create(
            tenantId: Guid.NewGuid(),
            assistantId: Guid.NewGuid(),
            assistantName: "Tutor",
            agentPrincipalId: Guid.NewGuid(),
            conversationId: convId ?? Guid.NewGuid(),
            agentTaskId: taskId,
            requestingUserId: Guid.NewGuid(),
            toolName: "DeleteAllUsers",
            commandTypeName: "Some.Module.DeleteAllUsersCommand, Some.Module",
            argumentsJson: """{"confirm":true}""",
            reasonHint: "Mass user deletion",
            expiresAt: DateTime.UtcNow.AddHours(24));

    [Fact]
    public void Create_Requires_Conversation_Or_Task()
    {
        var act = () => AiPendingApproval.Create(
            tenantId: null,
            assistantId: Guid.NewGuid(),
            assistantName: "x",
            agentPrincipalId: Guid.NewGuid(),
            conversationId: null,
            agentTaskId: null,
            requestingUserId: null,
            toolName: "t",
            commandTypeName: "T, A",
            argumentsJson: "{}",
            reasonHint: null,
            expiresAt: DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_Raises_Pending_Event()
    {
        var pa = Make();
        pa.DomainEvents.Should().ContainSingle(e => e is AgentApprovalPendingEvent);
    }

    [Fact]
    public void TryApprove_Transitions_And_Raises_Event()
    {
        var pa = Make();
        pa.ClearDomainEvents();

        var ok = pa.TryApprove(Guid.NewGuid(), "looks good");

        ok.Should().BeTrue();
        pa.Status.Should().Be(PendingApprovalStatus.Approved);
        pa.DecidedAt.Should().NotBeNull();
        pa.DomainEvents.Should().ContainSingle(e => e is AgentApprovalApprovedEvent);
    }

    [Fact]
    public void TryApprove_Returns_False_If_Already_Decided()
    {
        var pa = Make();
        pa.TryDeny(Guid.NewGuid(), "no");
        pa.ClearDomainEvents();

        var ok = pa.TryApprove(Guid.NewGuid(), null);

        ok.Should().BeFalse();
        pa.Status.Should().Be(PendingApprovalStatus.Denied);
        pa.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void TryDeny_Requires_Reason()
    {
        var pa = Make();
        var act = () => pa.TryDeny(Guid.NewGuid(), "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryExpire_Transitions_And_Raises_Event()
    {
        var pa = Make();
        pa.ClearDomainEvents();
        var ok = pa.TryExpire();
        ok.Should().BeTrue();
        pa.Status.Should().Be(PendingApprovalStatus.Expired);
        pa.DomainEvents.Should().ContainSingle(e => e is AgentApprovalExpiredEvent);
    }
}
