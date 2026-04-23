using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Starter.Module.AI.Application.Eval;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Application.Eval.Errors;
using Starter.Module.AI.Application.Eval.Faithfulness;
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

    [Fact]
    public async Task Handle_ValidFixture_PassesAssistantPromptAndModelToHarness()
    {
        var db = CreateInMemoryDb();
        var assistant = AiAssistant.Create(
            tenantId: null,
            name: "Eval Assistant",
            description: null,
            systemPrompt: "Answer strictly from the provided passages.",
            createdByUserId: Guid.NewGuid(),
            provider: null,
            model: "claude-opus-4-7",
            temperature: 0.0,
            maxTokens: 1024,
            executionMode: Starter.Module.AI.Domain.Enums.AssistantExecutionMode.Chat,
            maxAgentSteps: 1,
            isActive: true);

        db.AiAssistants.Add(assistant);
        await db.SaveChangesAsync();

        var report = new EvalReport(
            RunAt: DateTime.UtcNow,
            DatasetName: "inline",
            Language: "en",
            QuestionCount: 1,
            Metrics: new EvalMetrics(
                Aggregate: new MetricBucket(
                    new Dictionary<int, double>(),
                    new Dictionary<int, double>(),
                    new Dictionary<int, double>(),
                    new Dictionary<int, double>(),
                    0.0),
                PerLanguage: new Dictionary<string, MetricBucket>(),
                PerTag: new Dictionary<string, MetricBucket>()),
            Latency: new LatencyMetrics(new Dictionary<string, StagePercentiles>()),
            PerQuestion: Array.Empty<PerQuestionResult>(),
            AggregateDegradedStages: Array.Empty<string>(),
            Faithfulness: new FaithfulnessReport(0.9, 0, Array.Empty<FaithfulnessQuestionResult>()));

        EvalRunOptions? captured = null;
        var harness = new Mock<IRagEvalHarness>();
        harness
            .Setup(h => h.RunAsync(It.IsAny<EvalDataset>(), It.IsAny<EvalRunOptions>(), It.IsAny<CancellationToken>()))
            .Callback<EvalDataset, EvalRunOptions, CancellationToken>((_, opts, _) => captured = opts)
            .ReturnsAsync(report);

        var handler = new RunFaithfulnessEvalCommandHandler(
            harness.Object, db, Options.Create(new AiRagEvalSettings()));

        var fixtureJson = JsonSerializer.Serialize(new
        {
            name = "inline",
            language = "en",
            documents = new[] { new { id = Guid.NewGuid(), file_name = "d.md", content = "hello" } },
            questions = new[]
            {
                new
                {
                    id = "q-1",
                    query = "hi?",
                    relevant_document_ids = Array.Empty<Guid>(),
                    tags = Array.Empty<string>(),
                }
            },
        });

        var result = await handler.Handle(
            new RunFaithfulnessEvalCommand(fixtureJson, null, assistant.Id, JudgeModelOverride: "judge-x"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Faithfulness!.AggregateScore.Should().Be(0.9);

        captured.Should().NotBeNull();
        captured!.AssistantId.Should().Be(assistant.Id);
        captured.AssistantSystemPrompt.Should().Be("Answer strictly from the provided passages.");
        captured.AssistantModel.Should().Be("claude-opus-4-7");
        captured.JudgeModelOverride.Should().Be("judge-x");
        captured.IncludeFaithfulness.Should().BeTrue();
    }

    private static AiDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AiDbContext(options, currentUserService: null);
    }
}
