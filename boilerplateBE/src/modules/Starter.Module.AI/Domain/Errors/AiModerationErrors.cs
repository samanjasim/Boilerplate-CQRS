using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Domain.Errors;

public static class AiModerationErrors
{
    public static Error InputBlocked(string categoriesSummary) =>
        Error.Validation("AiModeration.InputBlocked",
            $"Input blocked by content moderation: {categoriesSummary}.");

    public static Error OutputBlocked(string categoriesSummary) =>
        Error.Validation("AiModeration.OutputBlocked",
            $"Output blocked by content moderation: {categoriesSummary}.");

    public static readonly Error ProviderUnavailable =
        Error.Failure("AiModeration.ProviderUnavailable",
            "Content moderation provider is unavailable; safe presets refuse the request.");

    public static Error PresetProfileNotFound(ModerationProvider provider, SafetyPreset preset) =>
        Error.NotFound("AiModeration.PresetProfileNotFound",
            $"No safety profile configured for preset '{preset}' on provider '{provider}'.");
}
