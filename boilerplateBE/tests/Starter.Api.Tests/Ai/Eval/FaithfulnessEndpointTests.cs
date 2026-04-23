using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Starter.Module.AI.Application.Eval;
using Starter.Module.AI.Application.Eval.Errors;
using Starter.Module.AI.Application.Features.Eval.Commands.RunFaithfulnessEval;
using Starter.Module.AI.Constants;
using Starter.Module.AI.Controllers;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Eval;

public sealed class FaithfulnessEndpointTests
{
    [Fact]
    public void RunFaithfulness_HasRunEvalAuthorizeAttribute()
    {
        var method = typeof(AiEvalController).GetMethod("RunFaithfulness")!;
        var attr = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();
        attr.Should().NotBeNull();
        attr!.Policy.Should().Be(AiPermissions.RunEval);
    }

    [Fact]
    public async Task Handle_AssistantNotFound_ReturnsAssistantNotFoundError()
    {
        var db = CreateInMemoryDb();
        var harness = new Mock<IRagEvalHarness>();
        var handler = new RunFaithfulnessEvalCommandHandler(
            harness.Object, db, Options.Create(new AiRagEvalSettings()));

        var result = await handler.Handle(
            new RunFaithfulnessEvalCommand(null, "test", Guid.NewGuid(), null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(EvalErrors.AssistantNotFound.Code);
    }

    [Fact]
    public async Task Handle_NoFixtureOrDatasetName_ReturnsFixtureNotFoundError()
    {
        var db = CreateInMemoryDb();

        var assistant = AiAssistant.Create(
            tenantId: null,
            name: "Test Assistant",
            description: null,
            systemPrompt: "You are a helpful assistant.",
            createdByUserId: Guid.NewGuid());

        db.AiAssistants.Add(assistant);
        await db.SaveChangesAsync();

        var harness = new Mock<IRagEvalHarness>();
        var handler = new RunFaithfulnessEvalCommandHandler(
            harness.Object, db, Options.Create(new AiRagEvalSettings()));

        var result = await handler.Handle(
            new RunFaithfulnessEvalCommand(null, null, assistant.Id, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(EvalErrors.FixtureNotFound.Code);
    }

    private static AiDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AiDbContext(options, currentUserService: null);
    }
}
