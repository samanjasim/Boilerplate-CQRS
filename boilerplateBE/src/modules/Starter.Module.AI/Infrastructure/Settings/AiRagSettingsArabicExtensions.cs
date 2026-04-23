using Starter.Module.AI.Infrastructure.Retrieval;

namespace Starter.Module.AI.Infrastructure.Settings;

internal static class AiRagSettingsArabicExtensions
{
    /// <summary>
    /// Projects the Arabic-normalization toggles from <see cref="AiRagSettings"/>
    /// into an <see cref="ArabicNormalizationOptions"/> value. Callers must still
    /// gate on <see cref="AiRagSettings.ApplyArabicNormalization"/> — this only
    /// builds the options for when normalization is enabled.
    /// </summary>
    public static ArabicNormalizationOptions ToArabicOptions(this AiRagSettings settings) =>
        new(
            NormalizeTaMarbuta: settings.NormalizeTaMarbuta,
            NormalizeArabicDigits: settings.NormalizeArabicDigits);
}
