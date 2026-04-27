using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Approvals;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Approvals;

public sealed class PendingApprovalServiceTests
{
    private static (AiDbContext db, IPendingApprovalService svc) Make()
    {
        var cu = new Mock<ICurrentUserService>();
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        return (db, new PendingApprovalService(db, NullLogger<PendingApprovalService>.Instance));
    }

    private static AiAssistant MakeAssistant() =>
        AiAssistant.Create(Guid.NewGuid(), "Tutor", null, "p", Guid.NewGuid());

    [Fact]
    public async Task Create_Persists_Pending_Row()
    {
        var (db, svc) = Make();
        var a = MakeAssistant();
        var entity = await svc.CreateAsync(
            a.TenantId, a.Id, a.Name, Guid.NewGuid(),
            conversationId: Guid.NewGuid(), agentTaskId: null, requestingUserId: Guid.NewGuid(),
            toolName: "DeleteAllUsers",
            commandTypeName: "X.Y, X",
            argumentsJson: "{}",
            reasonHint: null,
            expiresIn: TimeSpan.FromHours(24),
            ct: default);

        entity.Status.Should().Be(PendingApprovalStatus.Pending);
        var loaded = await db.AiPendingApprovals.FirstAsync();
        loaded.Id.Should().Be(entity.Id);
    }

    [Fact]
    public async Task Approve_Of_Already_Denied_Returns_Failure()
    {
        var (db, svc) = Make();
        var a = MakeAssistant();
        var pa = await svc.CreateAsync(
            a.TenantId, a.Id, a.Name, Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(),
            "T", "X.Y, X", "{}", null, TimeSpan.FromHours(1), default);
        await svc.DenyAsync(pa.Id, Guid.NewGuid(), "no", default);

        var result = await svc.ApproveAsync(pa.Id, Guid.NewGuid(), null, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PendingApproval.NotPending");
    }

    [Fact]
    public async Task Deny_Without_Reason_Returns_Failure()
    {
        var (db, svc) = Make();
        var a = MakeAssistant();
        var pa = await svc.CreateAsync(
            a.TenantId, a.Id, a.Name, Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(),
            "T", "X.Y, X", "{}", null, TimeSpan.FromHours(1), default);

        var result = await svc.DenyAsync(pa.Id, Guid.NewGuid(), "", default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PendingApproval.DenyReasonRequired");
    }

    [Fact]
    public async Task CreateAsync_Returns_Existing_Pending_Row_For_Same_Tool_Args()
    {
        // Spec §4.1 — a retry of the same agent+tool+args while a Pending row already
        // exists must reuse the row, not create a duplicate. Otherwise the approver
        // inbox bloats and approving one row leaves siblings dangling.
        var (db, svc) = Make();
        var a = MakeAssistant();
        var assistantId = a.Id;
        var conversationId = Guid.NewGuid();
        var principalId = Guid.NewGuid();

        var first = await svc.CreateAsync(
            a.TenantId, assistantId, a.Name, principalId,
            conversationId: conversationId, agentTaskId: null, requestingUserId: Guid.NewGuid(),
            toolName: "DeleteAllUsers",
            commandTypeName: "X.Y, X",
            argumentsJson: "{\"scope\":\"all\"}",
            reasonHint: null,
            expiresIn: TimeSpan.FromHours(24),
            ct: default);

        // Same assistant + conv + tool + args — should dedup to the existing row.
        var second = await svc.CreateAsync(
            a.TenantId, assistantId, a.Name, principalId,
            conversationId: conversationId, agentTaskId: null, requestingUserId: Guid.NewGuid(),
            toolName: "DeleteAllUsers",
            commandTypeName: "X.Y, X",
            argumentsJson: "{\"scope\":\"all\"}",
            reasonHint: "retry",
            expiresIn: TimeSpan.FromHours(24),
            ct: default);

        second.Id.Should().Be(first.Id);
        var rows = await db.AiPendingApprovals.CountAsync();
        rows.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_Different_Args_Creates_New_Row()
    {
        // Sanity: the dedup is keyed on (assistant, conv, task, tool, args). Different
        // args ⇒ different request ⇒ new row.
        var (db, svc) = Make();
        var a = MakeAssistant();
        var assistantId = a.Id;
        var conversationId = Guid.NewGuid();
        var principalId = Guid.NewGuid();

        var first = await svc.CreateAsync(
            a.TenantId, assistantId, a.Name, principalId,
            conversationId: conversationId, agentTaskId: null, requestingUserId: Guid.NewGuid(),
            toolName: "DeleteUser",
            commandTypeName: "X.Y, X",
            argumentsJson: "{\"id\":\"alice\"}",
            reasonHint: null,
            expiresIn: TimeSpan.FromHours(24),
            ct: default);

        var second = await svc.CreateAsync(
            a.TenantId, assistantId, a.Name, principalId,
            conversationId: conversationId, agentTaskId: null, requestingUserId: Guid.NewGuid(),
            toolName: "DeleteUser",
            commandTypeName: "X.Y, X",
            argumentsJson: "{\"id\":\"bob\"}",
            reasonHint: null,
            expiresIn: TimeSpan.FromHours(24),
            ct: default);

        second.Id.Should().NotBe(first.Id);
        var rows = await db.AiPendingApprovals.CountAsync();
        rows.Should().Be(2);
    }
}
