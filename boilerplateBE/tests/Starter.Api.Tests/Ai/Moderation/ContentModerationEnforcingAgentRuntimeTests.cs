using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Runtime;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

/// <summary>
/// Acid-flavored tests for the moderation decorator. Each test exercises one branch
/// of the decision tree (allowed pass-through, input-blocked, output-blocked,
/// provider-unavailable + FailClosed, provider-unavailable + FailOpen) using fakes
/// that don't depend on EF migrations or external providers.
/// </summary>
public sealed class ContentModerationEnforcingAgentRuntimeTests
{
    private sealed class FakeRuntime : IAiAgentRuntime
    {
        public AgentRunResult ToReturn { get; set; } = new(
            AgentRunStatus.Completed, "hello world", Array.Empty<AgentStepEvent>(), 1, 1, null);
        public bool Called { get; private set; }
        public Task<AgentRunResult> RunAsync(AgentRunContext ctx, IAgentRunSink sink, CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(ToReturn);
        }
    }

    private sealed class FakeModerator : IContentModerator
    {
        public Func<string, ModerationStage, ResolvedSafetyProfile, ModerationVerdict>? Verdict { get; set; }
        public Task<ModerationVerdict> ScanAsync(string text, ModerationStage stage, ResolvedSafetyProfile profile, string? language, CancellationToken ct) =>
            Task.FromResult(Verdict?.Invoke(text, stage, profile) ?? ModerationVerdict.Allowed(0));
    }

    private sealed class FakeRefusals : IModerationRefusalProvider
    {
        public string GetRefusal(SafetyPreset preset, PersonaAudienceType audience, System.Globalization.CultureInfo culture) => $"refused:{preset}";
        public string GetProviderUnavailable(SafetyPreset preset, System.Globalization.CultureInfo culture) => $"unavailable:{preset}";
    }

    private sealed class FakeRedactor : IPiiRedactor
    {
        public Task<RedactionResult> RedactAsync(string text, ResolvedSafetyProfile profile, CancellationToken ct) =>
            Task.FromResult(new RedactionResult(ModerationOutcome.Allowed, text, new Dictionary<string, int>()));
    }

    private static (AiDbContext db, AiAssistant assistant) Seed(SafetyPreset? overridePreset = null)
    {
        var cu = new Mock<ICurrentUserService>();
        var tenant = Guid.NewGuid();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        var a = AiAssistant.Create(tenant, "Tutor", null, "be safe", Guid.NewGuid());
        if (overridePreset is { } p) a.SetSafetyPreset(p);
        db.AiAssistants.Add(a);
        db.SaveChanges();
        return (db, a);
    }

    private static AgentRunContext Ctx(AiAssistant a, SafetyPreset personaPreset, bool streaming = false)
    {
        var persona = new PersonaContext(
            Id: Guid.NewGuid(),
            Slug: "student",
            Audience: PersonaAudienceType.Internal,
            Safety: personaPreset,
            PermittedAgentSlugs: Array.Empty<string>());
        return new AgentRunContext(
            Messages: new[] { new AiChatMessage("user", "tell me a story") },
            SystemPrompt: "be safe",
            ModelConfig: new AgentModelConfig(AiProviderType.OpenAI, "gpt-4o", 0.7, 100),
            Tools: new ToolResolutionResult(
                ProviderTools: Array.Empty<AiToolDefinitionDto>(),
                DefinitionsByName: new Dictionary<string, IAiToolDefinition>()),
            MaxSteps: 1,
            LoopBreak: LoopBreakPolicy.Default,
            Streaming: streaming,
            Persona: persona,
            AssistantId: a.Id,
            TenantId: a.TenantId);
    }

    private static IConfiguration BuildConfig(bool logAllOutcomes = false) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:Moderation:LogAllOutcomes"] = logAllOutcomes ? "true" : "false"
            })
            .Build();

    private static ContentModerationEnforcingAgentRuntime Wire(
        AiDbContext db, IAiAgentRuntime inner, IContentModerator moderator,
        IConfiguration? configuration = null)
    {
        var profileResolver = new Mock<ISafetyProfileResolver>();
        profileResolver.Setup(r => r.ResolveAsync(It.IsAny<Guid?>(), It.IsAny<AiAssistant>(), It.IsAny<SafetyPreset?>(),
                                                  It.IsAny<ModerationProvider>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync((Guid? t, AiAssistant a, SafetyPreset? p, ModerationProvider mp, CancellationToken c) =>
                       {
                           var preset = a.SafetyPresetOverride ?? p ?? SafetyPreset.Standard;
                           return new ResolvedSafetyProfile(
                               preset,
                               mp,
                               new Dictionary<string, double> { ["sexual"] = 0.5 },
                               new[] { "sexual-minors" },
                               preset == SafetyPreset.Standard ? ModerationFailureMode.FailOpen : ModerationFailureMode.FailClosed,
                               preset == SafetyPreset.ProfessionalModerated);
                       });
        return new ContentModerationEnforcingAgentRuntime(
            inner, moderator, new FakeRedactor(), profileResolver.Object,
            new FakeRefusals(), db, Mock.Of<IWebhookPublisher>(),
            configuration ?? BuildConfig(),
            NullLogger<ContentModerationEnforcingAgentRuntime>.Instance);
    }

    [Fact]
    public async Task Standard_Allowed_Passes_Through()
    {
        var (db, a) = Seed();
        var inner = new FakeRuntime();
        var moderator = new FakeModerator();
        var rt = Wire(db, inner, moderator);

        var sink = new Mock<IAgentRunSink>();
        var result = await rt.RunAsync(Ctx(a, SafetyPreset.Standard), sink.Object, default);

        result.Status.Should().Be(AgentRunStatus.Completed);
        inner.Called.Should().BeTrue();
        result.FinalContent.Should().Be("hello world");
    }

    [Fact]
    public async Task Input_Blocked_Returns_Refusal_And_Skips_Inner()
    {
        var (db, a) = Seed();
        var inner = new FakeRuntime();
        var moderator = new FakeModerator
        {
            Verdict = (text, stage, profile) => stage == ModerationStage.Input
                ? ModerationVerdict.Blocked(new Dictionary<string, double> { ["sexual"] = 0.9 }, "blocked", 5)
                : ModerationVerdict.Allowed(5)
        };
        var rt = Wire(db, inner, moderator);

        var sink = new Mock<IAgentRunSink>();
        var result = await rt.RunAsync(Ctx(a, SafetyPreset.ChildSafe), sink.Object, default);

        result.Status.Should().Be(AgentRunStatus.InputBlocked);
        result.FinalContent.Should().Contain("refused");
        inner.Called.Should().BeFalse();

        // Moderation events are returned via the result for the chat layer to persist.
        result.ModerationEvents.Should().NotBeNull().And.HaveCount(1);
        result.ModerationEvents![0].Stage.Should().Be(ModerationStage.Input);
        result.ModerationEvents[0].Outcome.Should().Be(ModerationOutcome.Blocked);
    }

    [Fact]
    public async Task Output_Blocked_For_ChildSafe_Returns_Refusal_And_Persists_Event()
    {
        var (db, a) = Seed();
        a.SetSafetyPreset(SafetyPreset.ChildSafe);
        await db.SaveChangesAsync();

        var inner = new FakeRuntime();
        var moderator = new FakeModerator
        {
            Verdict = (text, stage, profile) => stage == ModerationStage.Output
                ? ModerationVerdict.Blocked(new Dictionary<string, double> { ["sexual-minors"] = 0.9 }, "always-block:sexual-minors", 5)
                : ModerationVerdict.Allowed(5)
        };
        var rt = Wire(db, inner, moderator);

        var sink = new Mock<IAgentRunSink>();
        var result = await rt.RunAsync(Ctx(a, SafetyPreset.Standard /*persona*/), sink.Object, default);

        result.Status.Should().Be(AgentRunStatus.OutputBlocked);
        result.FinalContent.Should().Contain("refused");
        result.ModerationEvents.Should().NotBeNull().And.HaveCount(1);
        result.ModerationEvents![0].Stage.Should().Be(ModerationStage.Output);
    }

    [Fact]
    public async Task Provider_Unavailable_FailClosed_Returns_Unavailable_For_ChildSafe()
    {
        var (db, a) = Seed(SafetyPreset.ChildSafe);
        var inner = new FakeRuntime();
        var moderator = new FakeModerator { Verdict = (_, _, _) => ModerationVerdict.Unavailable(0) };
        var rt = Wire(db, inner, moderator);
        var sink = new Mock<IAgentRunSink>();

        var result = await rt.RunAsync(Ctx(a, SafetyPreset.ChildSafe), sink.Object, default);

        result.Status.Should().Be(AgentRunStatus.ModerationProviderUnavailable);
        inner.Called.Should().BeFalse();
    }

    [Fact]
    public async Task Provider_Unavailable_FailOpen_Allows_Standard()
    {
        var (db, a) = Seed();
        var inner = new FakeRuntime();
        var moderator = new FakeModerator { Verdict = (_, _, _) => ModerationVerdict.Unavailable(0) };
        var rt = Wire(db, inner, moderator);
        var sink = new Mock<IAgentRunSink>();

        var result = await rt.RunAsync(Ctx(a, SafetyPreset.Standard), sink.Object, default);

        result.Status.Should().Be(AgentRunStatus.Completed);
        inner.Called.Should().BeTrue();
    }

    [Fact]
    public async Task LogAllOutcomes_True_Emits_Allowed_Events_For_Both_Stages()
    {
        // Spec §5.1 — when Ai:Moderation:LogAllOutcomes=true, full-audit tenants get
        // an AiModerationEvent row for Allowed outcomes too (not just Blocked/Redacted).
        var (db, a) = Seed();
        var inner = new FakeRuntime();
        var moderator = new FakeModerator(); // Allowed by default
        var rt = Wire(db, inner, moderator, BuildConfig(logAllOutcomes: true));

        var sink = new Mock<IAgentRunSink>();
        var result = await rt.RunAsync(Ctx(a, SafetyPreset.Standard), sink.Object, default);

        result.Status.Should().Be(AgentRunStatus.Completed);
        result.ModerationEvents.Should().NotBeNull();
        result.ModerationEvents!.Should().HaveCount(2);
        result.ModerationEvents.Should().Contain(e =>
            e.Stage == ModerationStage.Input && e.Outcome == ModerationOutcome.Allowed);
        result.ModerationEvents.Should().Contain(e =>
            e.Stage == ModerationStage.Output && e.Outcome == ModerationOutcome.Allowed);
    }

    [Fact]
    public async Task LogAllOutcomes_False_Default_Does_Not_Emit_Allowed_Events()
    {
        var (db, a) = Seed();
        var inner = new FakeRuntime();
        var moderator = new FakeModerator(); // Allowed by default
        var rt = Wire(db, inner, moderator); // default config: LogAllOutcomes=false

        var sink = new Mock<IAgentRunSink>();
        var result = await rt.RunAsync(Ctx(a, SafetyPreset.Standard), sink.Object, default);

        result.Status.Should().Be(AgentRunStatus.Completed);
        // No moderation events because both scans returned Allowed and the flag is off.
        result.ModerationEvents.Should().BeNull();
    }
}
