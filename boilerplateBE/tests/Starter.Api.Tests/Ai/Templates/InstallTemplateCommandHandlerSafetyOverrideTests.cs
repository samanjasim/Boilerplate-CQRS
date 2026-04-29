using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.InstallTemplate;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Constants;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class InstallTemplateCommandHandlerSafetyOverrideTests
{
    [Fact]
    public async Task Sets_assistant_override_when_template_has_explicit_value()
    {
        var (handler, db, _) = Setup(SafetyPreset.ChildSafe);

        var result = await handler.Handle(
            new InstallTemplateCommand("safety_test"), default);

        result.IsSuccess.Should().BeTrue();
        var assistant = await db.AiAssistants.SingleAsync();
        assistant.SafetyPresetOverride.Should().Be(SafetyPreset.ChildSafe);
    }

    [Fact]
    public async Task Leaves_assistant_override_null_when_template_override_is_null()
    {
        var (handler, db, _) = Setup(safetyOverride: null);

        var result = await handler.Handle(
            new InstallTemplateCommand("safety_test"), default);

        result.IsSuccess.Should().BeTrue();
        var assistant = await db.AiAssistants.SingleAsync();
        assistant.SafetyPresetOverride.Should().BeNull();
    }

    private static (
        InstallTemplateCommandHandler handler,
        AiDbContext db,
        Mock<ICurrentUserService> currentUser)
        Setup(SafetyPreset? safetyOverride)
    {
        var tenantId = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        cu.SetupGet(x => x.TenantId).Returns(tenantId);
        cu.Setup(x => x.IsInRole(Roles.SuperAdmin)).Returns(false);

        var dbOpts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"safety-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AiDbContext(dbOpts, cu.Object);

        db.AiPersonas.Add(AiPersona.CreateDefault(tenantId, Guid.NewGuid()));
        db.SaveChanges();

        var template = new TestTemplate(
            slug: "safety_test",
            personas: new[] { "default" },
            safetyOverride: safetyOverride);
        var registry = new AiAgentTemplateRegistry(new[] { (IAiAgentTemplate)template });

        var toolReg = new Mock<IAiToolRegistry>();
        var ff = new Mock<IFeatureFlagService>();
        ff.Setup(x => x.GetValueAsync<int>("ai.agents.max_count", It.IsAny<CancellationToken>()))
            .ReturnsAsync(int.MaxValue);

        var handler = new InstallTemplateCommandHandler(
            db, registry, toolReg.Object, cu.Object, ff.Object);
        return (handler, db, cu);
    }
}
