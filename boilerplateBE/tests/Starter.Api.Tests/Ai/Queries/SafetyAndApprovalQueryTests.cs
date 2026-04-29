using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Queries.Approvals.GetPendingApprovalById;
using Starter.Module.AI.Application.Queries.Approvals.GetPendingApprovals;
using Starter.Module.AI.Application.Queries.Safety.GetModerationEvents;
using Starter.Module.AI.Application.Queries.Safety.GetSafetyPresetProfiles;
using Starter.Module.AI.Constants;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Queries;

public sealed class SafetyAndApprovalQueryTests
{
    private static (AiDbContext db, Mock<ICurrentUserService> cu) NewDb(
        Guid? tenantId,
        Guid? userId = null,
        bool canApprove = false)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenantId);
        cu.SetupGet(x => x.UserId).Returns(userId ?? Guid.NewGuid());
        cu.Setup(x => x.HasPermission(AiPermissions.AgentsApproveAction)).Returns(canApprove);
        cu.Setup(x => x.HasPermission(It.Is<string>(s => s != AiPermissions.AgentsApproveAction))).Returns(false);

        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        return (db, cu);
    }

    // ---------- GetSafetyPresetProfilesQuery ----------

    [Fact]
    public async Task SafetyProfiles_Tenant_Admin_Sees_PlatformDefaults_And_Own_Overrides_Only()
    {
        var myTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var (db, cu) = NewDb(myTenant);

        // Platform default (TenantId == null) — visible to everyone.
        db.AiSafetyPresetProfiles.Add(AiSafetyPresetProfile.Create(
            tenantId: null,
            preset: SafetyPreset.Standard,
            provider: ModerationProvider.OpenAi,
            thresholdsJson: "{}",
            blockedCategoriesJson: "[]",
            failureMode: ModerationFailureMode.FailClosed,
            redactPii: false));
        // My tenant override.
        db.AiSafetyPresetProfiles.Add(AiSafetyPresetProfile.Create(
            tenantId: myTenant,
            preset: SafetyPreset.ChildSafe,
            provider: ModerationProvider.OpenAi,
            thresholdsJson: "{}",
            blockedCategoriesJson: "[]",
            failureMode: ModerationFailureMode.FailClosed,
            redactPii: false));
        // Another tenant's override — must be hidden from me.
        db.AiSafetyPresetProfiles.Add(AiSafetyPresetProfile.Create(
            tenantId: otherTenant,
            preset: SafetyPreset.ProfessionalModerated,
            provider: ModerationProvider.OpenAi,
            thresholdsJson: "{}",
            blockedCategoriesJson: "[]",
            failureMode: ModerationFailureMode.FailClosed,
            redactPii: false));
        await db.SaveChangesAsync();

        var handler = new GetSafetyPresetProfilesQueryHandler(db, cu.Object);
        var result = await handler.Handle(new GetSafetyPresetProfilesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Should().OnlyContain(p =>
            p.TenantId == null || p.TenantId == myTenant);
        // Platform default should sort first.
        result.Value.Items[0].TenantId.Should().BeNull();
    }

    // ---------- GetModerationEventsQuery ----------

    [Fact]
    public async Task ModerationEvents_Filters_By_Outcome_And_Tenant_Scope()
    {
        var myTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var (db, cu) = NewDb(myTenant, userId: Guid.NewGuid());

        db.AiModerationEvents.AddRange(
            AiModerationEvent.Create(
                tenantId: myTenant, assistantId: null, agentPrincipalId: null,
                conversationId: null, agentTaskId: null, messageId: null,
                stage: ModerationStage.Input, preset: SafetyPreset.Standard,
                outcome: ModerationOutcome.Allowed, categoriesJson: "{}",
                provider: ModerationProvider.OpenAi, latencyMs: 10),
            AiModerationEvent.Create(
                tenantId: myTenant, assistantId: null, agentPrincipalId: null,
                conversationId: null, agentTaskId: null, messageId: null,
                stage: ModerationStage.Input, preset: SafetyPreset.Standard,
                outcome: ModerationOutcome.Blocked, categoriesJson: "{}",
                provider: ModerationProvider.OpenAi, latencyMs: 12,
                blockedReason: "violence"),
            AiModerationEvent.Create(
                tenantId: otherTenant, assistantId: null, agentPrincipalId: null,
                conversationId: null, agentTaskId: null, messageId: null,
                stage: ModerationStage.Input, preset: SafetyPreset.Standard,
                outcome: ModerationOutcome.Blocked, categoriesJson: "{}",
                provider: ModerationProvider.OpenAi, latencyMs: 12));
        await db.SaveChangesAsync();

        var handler = new GetModerationEventsQueryHandler(db, cu.Object);
        var result = await handler.Handle(
            new GetModerationEventsQuery(Outcome: ModerationOutcome.Blocked), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].TenantId.Should().Be(myTenant);
        result.Value.Items[0].BlockedReason.Should().Be("violence");
    }

    // ---------- GetPendingApprovalsQuery ----------

    [Fact]
    public async Task ViewApprovals_Only_Sees_Own_Rows()
    {
        var tenant = Guid.NewGuid();
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var (db, cu) = NewDb(tenant, userId: me, canApprove: false);

        db.AiPendingApprovals.AddRange(
            AiPendingApproval.Create(
                tenantId: tenant, assistantId: Guid.NewGuid(), assistantName: "x",
                agentPrincipalId: Guid.NewGuid(),
                conversationId: Guid.NewGuid(), agentTaskId: null,
                requestingUserId: me,
                toolName: "T1", commandTypeName: "X.Y, X", argumentsJson: "{}",
                reasonHint: null, expiresAt: DateTime.UtcNow.AddHours(1)),
            AiPendingApproval.Create(
                tenantId: tenant, assistantId: Guid.NewGuid(), assistantName: "x",
                agentPrincipalId: Guid.NewGuid(),
                conversationId: Guid.NewGuid(), agentTaskId: null,
                requestingUserId: other,
                toolName: "T2", commandTypeName: "X.Y, X", argumentsJson: "{}",
                reasonHint: null, expiresAt: DateTime.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();

        var handler = new GetPendingApprovalsQueryHandler(db, cu.Object);
        var result = await handler.Handle(new GetPendingApprovalsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().ToolName.Should().Be("T1");
    }

    [Fact]
    public async Task Approver_Sees_All_Tenant_Rows()
    {
        var tenant = Guid.NewGuid();
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var (db, cu) = NewDb(tenant, userId: me, canApprove: true);

        db.AiPendingApprovals.AddRange(
            AiPendingApproval.Create(
                tenantId: tenant, assistantId: Guid.NewGuid(), assistantName: "x",
                agentPrincipalId: Guid.NewGuid(),
                conversationId: Guid.NewGuid(), agentTaskId: null,
                requestingUserId: me,
                toolName: "T1", commandTypeName: "X.Y, X", argumentsJson: "{}",
                reasonHint: null, expiresAt: DateTime.UtcNow.AddHours(1)),
            AiPendingApproval.Create(
                tenantId: tenant, assistantId: Guid.NewGuid(), assistantName: "x",
                agentPrincipalId: Guid.NewGuid(),
                conversationId: Guid.NewGuid(), agentTaskId: null,
                requestingUserId: other,
                toolName: "T2", commandTypeName: "X.Y, X", argumentsJson: "{}",
                reasonHint: null, expiresAt: DateTime.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();

        var handler = new GetPendingApprovalsQueryHandler(db, cu.Object);
        var result = await handler.Handle(new GetPendingApprovalsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
    }

    // ---------- GetPendingApprovalByIdQuery ----------

    [Fact]
    public async Task GetById_ViewApprovals_Cannot_See_Other_Users_Row()
    {
        var tenant = Guid.NewGuid();
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        var (db, cu) = NewDb(tenant, userId: me, canApprove: false);

        var otherRow = AiPendingApproval.Create(
            tenantId: tenant, assistantId: Guid.NewGuid(), assistantName: "x",
            agentPrincipalId: Guid.NewGuid(),
            conversationId: Guid.NewGuid(), agentTaskId: null,
            requestingUserId: other,
            toolName: "OtherTool", commandTypeName: "X.Y, X", argumentsJson: "{}",
            reasonHint: null, expiresAt: DateTime.UtcNow.AddHours(1));
        db.AiPendingApprovals.Add(otherRow);
        await db.SaveChangesAsync();

        var handler = new GetPendingApprovalByIdQueryHandler(db, cu.Object);
        var result = await handler.Handle(new GetPendingApprovalByIdQuery(otherRow.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Error.Forbidden");
    }

    [Fact]
    public async Task GetById_Returns_NotFound_For_Missing_Id()
    {
        var tenant = Guid.NewGuid();
        var (db, cu) = NewDb(tenant, userId: Guid.NewGuid(), canApprove: true);

        var handler = new GetPendingApprovalByIdQueryHandler(db, cu.Object);
        var result = await handler.Handle(new GetPendingApprovalByIdQuery(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PendingApproval.NotFound");
    }
}
