using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Safety.SetAssistantSafetyPresetOverride;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class SetAssistantSafetyPresetOverrideCommandTests
{
    [Fact]
    public async Task Sets_Override_For_Own_Tenant()
    {
        var tenant = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        var opts = new DbContextOptionsBuilder<AiDbContext>().UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        var assistant = AiAssistant.Create(tenant, "Tutor", null, "p", Guid.NewGuid());
        db.AiAssistants.Add(assistant);
        await db.SaveChangesAsync();

        var handler = new SetAssistantSafetyPresetOverrideCommandHandler(db, cu.Object);
        var result = await handler.Handle(
            new SetAssistantSafetyPresetOverrideCommand(assistant.Id, SafetyPreset.ChildSafe), default);

        result.IsSuccess.Should().BeTrue();
        (await db.AiAssistants.FindAsync(assistant.Id))!.SafetyPresetOverride.Should().Be(SafetyPreset.ChildSafe);
    }
}
