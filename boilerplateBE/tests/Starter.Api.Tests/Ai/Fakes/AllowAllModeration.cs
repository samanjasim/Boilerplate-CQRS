using Moq;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Api.Tests.Ai.Fakes;

/// <summary>
/// Test helpers that produce moderation services configured to allow all input/output
/// content. Use in tests that exercise <c>ChatExecutionService</c> via the real runtime
/// factory but care about something other than moderation behaviour (RAG injection,
/// observability, end-to-end happy-path). Each call returns a fresh stubbed instance.
/// </summary>
internal static class AllowAllModeration
{
    public static IContentModerator BuildModerator()
    {
        var m = new Mock<IContentModerator>();
        m.Setup(x => x.ScanAsync(
                It.IsAny<string>(),
                It.IsAny<ModerationStage>(),
                It.IsAny<ResolvedSafetyProfile>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ModerationVerdict.Allowed(0));
        return m.Object;
    }

    public static IPiiRedactor BuildRedactor()
    {
        var m = new Mock<IPiiRedactor>();
        m.Setup(x => x.RedactAsync(
                It.IsAny<string>(),
                It.IsAny<ResolvedSafetyProfile>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, ResolvedSafetyProfile _, CancellationToken __) =>
                new RedactionResult(
                    Outcome: ModerationOutcome.Allowed,
                    Text: text,
                    Hits: new Dictionary<string, int>(),
                    Failed: false));
        return m.Object;
    }

    public static ISafetyProfileResolver BuildResolver(SafetyPreset preset = SafetyPreset.Standard)
    {
        var m = new Mock<ISafetyProfileResolver>();
        m.Setup(x => x.ResolveAsync(
                It.IsAny<Guid?>(),
                It.IsAny<AiAssistant>(),
                It.IsAny<SafetyPreset?>(),
                It.IsAny<ModerationProvider>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? _, AiAssistant _, SafetyPreset? _, ModerationProvider provider, CancellationToken _) =>
                new ResolvedSafetyProfile(
                    Preset: preset,
                    Provider: provider,
                    CategoryThresholds: new Dictionary<string, double>(),
                    BlockedCategories: Array.Empty<string>(),
                    FailureMode: ModerationFailureMode.FailOpen,
                    RedactPii: false));
        return m.Object;
    }
}
