using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Api.Tests.Access._Helpers;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Features.Access.Commands.SetResourceVisibility;
using Starter.Domain.Common.Access.Enums;
using Starter.Module.AI.Application.Commands.SetAssistantAccessMode;
using Starter.Module.AI.Domain.Entities;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Assistants;

/// <summary>
/// Security regression tests for AI assistant ACL gates (spec §7.3).
/// </summary>
public sealed class AssistantAclApiTests
{
    private static AiDbContext CreateAiDb() =>
        new(new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"asst-acl-{Guid.NewGuid():N}").Options,
            currentUserService: null);

    // ── SetResourceVisibility: AiAssistant → Public must be rejected ──────

    [Fact]
    public async Task SetVisibility_Public_onAiAssistant_returns_VisibilityNotAllowed()
    {
        var caller = FakeCurrentUser.For(Guid.NewGuid(), Guid.NewGuid());
        var db = TestAccessFactory.CreateDb();
        var probe = new FakeResourceOwnershipProbe();

        var handler = new SetResourceVisibilityCommandHandler(probe, db, caller);

        var result = await handler.Handle(new SetResourceVisibilityCommand(
            ResourceType: ResourceTypes.AiAssistant,
            ResourceId: Guid.NewGuid(),
            Visibility: ResourceVisibility.Public), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Access.VisibilityNotAllowedForResourceType",
            "AiAssistant max visibility is TenantWide — Public must be rejected");
    }

    [Fact]
    public async Task SetVisibility_TenantWide_onAiAssistant_byOwner_succeeds()
    {
        var owner = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var caller = FakeCurrentUser.For(owner, tenant);
        var db = TestAccessFactory.CreateDb();
        var probe = new FakeResourceOwnershipProbe { OwnerId = owner };

        var handler = new SetResourceVisibilityCommandHandler(probe, db, caller);

        var result = await handler.Handle(new SetResourceVisibilityCommand(
            ResourceType: ResourceTypes.AiAssistant,
            ResourceId: Guid.NewGuid(),
            Visibility: ResourceVisibility.TenantWide), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── SetAssistantAccessMode: non-manager must be blocked ───────────────

    [Fact]
    public async Task SetAccessMode_byNonManager_returns_AssistantNotFound()
    {
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var nonOwnerId = Guid.NewGuid();

        var db = CreateAiDb();
        var assistant = AiAssistant.Create(tenantId, "Test", null, "gpt", ownerId);
        db.AiAssistants.Add(assistant);
        await db.SaveChangesAsync();

        var caller = FakeCurrentUser.For(nonOwnerId, tenantId);
        var coreDb = TestAccessFactory.CreateDb();
        var accessSvc = TestAccessFactory.BuildService(coreDb, caller);

        var handler = new SetAssistantAccessModeCommandHandler(
            db, coreDb, accessSvc, caller);

        var result = await handler.Handle(
            new SetAssistantAccessModeCommand(assistant.Id, AssistantAccessMode.AssistantPrincipal),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue("user without Manager grant must be blocked");
    }

    [Fact]
    public async Task SetAccessMode_byOwnerWithManagerGrant_succeeds()
    {
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var db = CreateAiDb();
        var assistant = AiAssistant.Create(tenantId, "Test", null, "gpt", ownerId);
        db.AiAssistants.Add(assistant);
        await db.SaveChangesAsync();

        var caller = FakeCurrentUser.For(ownerId, tenantId);
        var coreDb = TestAccessFactory.CreateDb();

        // Grant manager access for the owner to their own assistant
        var accessSvc = TestAccessFactory.BuildService(coreDb, caller);
        await accessSvc.GrantAsync(
            ResourceTypes.AiAssistant, assistant.Id,
            GrantSubjectType.User, ownerId, AccessLevel.Manager,
            CancellationToken.None);

        var handler = new SetAssistantAccessModeCommandHandler(
            db, coreDb, accessSvc, caller);

        var result = await handler.Handle(
            new SetAssistantAccessModeCommand(assistant.Id, AssistantAccessMode.AssistantPrincipal),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        assistant.AccessMode.Should().Be(AssistantAccessMode.AssistantPrincipal);
    }

    // ── Cross-tenant: assistant from different tenant must be 404 ─────────

    [Fact]
    public async Task SetAccessMode_forAssistantInDifferentTenant_returns_NotFound()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var ownerA = Guid.NewGuid();
        var callerFromB = Guid.NewGuid();

        var db = CreateAiDb();
        var assistant = AiAssistant.Create(tenantA, "TenantA Bot", null, "gpt", ownerA);
        db.AiAssistants.Add(assistant);
        await db.SaveChangesAsync();

        // Caller is from Tenant B — the assistant belongs to Tenant A
        var caller = FakeCurrentUser.For(callerFromB, tenantB);
        var coreDb = TestAccessFactory.CreateDb();
        var accessSvc = TestAccessFactory.BuildService(coreDb, caller);

        var handler = new SetAssistantAccessModeCommandHandler(
            db, coreDb, accessSvc, caller);

        var result = await handler.Handle(
            new SetAssistantAccessModeCommand(assistant.Id, AssistantAccessMode.AssistantPrincipal),
            CancellationToken.None);

        // The AiDbContext has a global query filter by tenant, so the assistant won't be found
        result.IsFailure.Should().BeTrue("assistant in different tenant must not be found");
    }
}
