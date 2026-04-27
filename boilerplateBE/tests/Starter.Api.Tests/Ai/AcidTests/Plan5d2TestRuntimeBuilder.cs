using Microsoft.EntityFrameworkCore;
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

namespace Starter.Api.Tests.Ai.AcidTests;

/// <summary>
/// Shared wire-up helper for the Phase H (M1–M6) acid tests of Plan 5d-2. Builds a
/// <see cref="ContentModerationEnforcingAgentRuntime"/> wrapped around a caller-supplied
/// inner runtime + moderator, with a stubbed safety profile resolver and an in-memory
/// <see cref="AiDbContext"/>. Each acid test owns its own DbContext + assistant — the
/// builder only assembles the decorator graph.
///
/// Mirrors the patterns in <c>ContentModerationEnforcingAgentRuntimeTests</c> (D2)
/// so the H-tests stay tiny and focused on the assertion that defines the flagship
/// contract for that scenario (output blocked, input blocked, PII redacted, …).
/// </summary>
internal static class Plan5d2TestRuntimeBuilder
{
    /// <summary>
    /// Wire a moderation decorator with a stub profile resolver that hands back the
    /// requested preset / failure mode / redaction flag. The redactor parameter lets
    /// callers swap in the real <c>RegexPiiRedactor</c> for tests that need actual
    /// regex-based redaction (M3) — the default is an allow-all stub for tests that
    /// only care about scan verdicts (M1, M2).
    /// </summary>
    public static ContentModerationEnforcingAgentRuntime Wire(
        AiDbContext db,
        IAiAgentRuntime inner,
        IContentModerator moderator,
        SafetyPreset preset,
        ModerationFailureMode failureMode,
        bool redactPii = false,
        IPiiRedactor? redactor = null)
    {
        var profileResolver = new Mock<ISafetyProfileResolver>();
        profileResolver
            .Setup(r => r.ResolveAsync(
                It.IsAny<Guid?>(),
                It.IsAny<AiAssistant>(),
                It.IsAny<SafetyPreset?>(),
                It.IsAny<ModerationProvider>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? _, AiAssistant a, SafetyPreset? _, ModerationProvider mp, CancellationToken _) =>
                new ResolvedSafetyProfile(
                    Preset: preset,
                    Provider: mp,
                    CategoryThresholds: new Dictionary<string, double> { ["sexual"] = 0.5 },
                    BlockedCategories: new[] { "sexual-minors" },
                    FailureMode: failureMode,
                    RedactPii: redactPii));

        return new ContentModerationEnforcingAgentRuntime(
            inner: inner,
            moderator: moderator,
            redactor: redactor ?? new StubAllowRedactor(),
            profileResolver: profileResolver.Object,
            refusals: new StubRefusals(),
            db: db,
            webhooks: Mock.Of<IWebhookPublisher>(),
            logger: NullLogger<ContentModerationEnforcingAgentRuntime>.Instance);
    }

    /// <summary>
    /// Build an <see cref="AgentRunContext"/> carrying just enough identity for the
    /// decorator to resolve the assistant + persona + safety profile. The single
    /// user message ("tell me a story") is what gets scanned at the input stage.
    /// </summary>
    public static AgentRunContext Ctx(AiAssistant a, SafetyPreset personaPreset, bool streaming = false)
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

    /// <summary>
    /// Spin up a fresh in-memory <see cref="AiDbContext"/> with a seeded assistant
    /// belonging to the given tenant + safety preset override. Returns both so the
    /// caller can keep the DbContext to assert side-effects (e.g. zero usage logs).
    /// </summary>
    public static (AiDbContext Db, AiAssistant Assistant) SeedAssistant(
        Guid tenant, SafetyPreset? presetOverride = null)
    {
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenant);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}")
            .Options;
        var db = new AiDbContext(opts, cu.Object);
        var assistant = AiAssistant.Create(tenant, "Tutor", null, "be safe", Guid.NewGuid());
        if (presetOverride is { } p) assistant.SetSafetyPreset(p);
        db.AiAssistants.Add(assistant);
        db.SaveChanges();
        return (db, assistant);
    }

    private sealed class StubRefusals : IModerationRefusalProvider
    {
        public string GetRefusal(SafetyPreset preset, PersonaAudienceType audience, System.Globalization.CultureInfo culture) =>
            $"refused:{preset}";
        public string GetProviderUnavailable(SafetyPreset preset, System.Globalization.CultureInfo culture) =>
            $"unavailable:{preset}";
    }

    private sealed class StubAllowRedactor : IPiiRedactor
    {
        public Task<RedactionResult> RedactAsync(string text, ResolvedSafetyProfile profile, CancellationToken ct) =>
            Task.FromResult(new RedactionResult(ModerationOutcome.Allowed, text, new Dictionary<string, int>()));
    }
}
