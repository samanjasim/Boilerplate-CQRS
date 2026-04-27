using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Moderations;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Services.Moderation;

/// <summary>
/// Provider-native moderation via OpenAI's Moderations API. Used uniformly regardless
/// of which chat provider the agent runs on. Threshold + always-block-categories come
/// from the resolved profile so per-tenant tuning works without code changes.
/// </summary>
internal sealed class OpenAiContentModerator(
    IModerationKeyResolver keyResolver,
    IHttpClientFactory httpClientFactory,
    ILogger<OpenAiContentModerator> logger) : IContentModerator
{
    private const string DefaultModel = "omni-moderation-latest";

    public async Task<ModerationVerdict> ScanAsync(
        string text, ModerationStage stage, ResolvedSafetyProfile profile,
        string? language, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(text))
            return ModerationVerdict.Allowed(0);

        var apiKey = keyResolver.Resolve();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogDebug("OpenAI moderation key not configured; reporting Unavailable.");
            return ModerationVerdict.Unavailable((int)sw.ElapsedMilliseconds);
        }

        try
        {
            var options = new OpenAIClientOptions
            {
                Transport = new HttpClientPipelineTransport(httpClientFactory.CreateClient(nameof(OpenAiContentModerator)))
            };
            var client = new ModerationClient(DefaultModel, new ApiKeyCredential(apiKey), options);
            var result = await client.ClassifyTextAsync(text, ct).ConfigureAwait(false);

            var moderation = result.Value;
            var scores = ProjectScores(moderation);
            return EvaluateScores(scores, profile, (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OpenAI moderation call failed at stage {Stage}; reporting Unavailable.", stage);
            return ModerationVerdict.Unavailable((int)sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Pure decision function over a category-score dict. Always-block categories trump
    /// threshold checks; the first category to trip wins. Extracted to a static helper
    /// so unit tests can exercise threshold + always-block logic without a live HTTP call
    /// (W1 covers the wire path).
    /// </summary>
    internal static ModerationVerdict EvaluateScores(
        IReadOnlyDictionary<string, double> scores,
        ResolvedSafetyProfile profile,
        int latencyMs)
    {
        foreach (var blocked in profile.BlockedCategories)
            if (scores.TryGetValue(blocked, out var s) && s > 0.0)
                return ModerationVerdict.Blocked(scores, $"category:{blocked} (always-block)", latencyMs);

        foreach (var (category, threshold) in profile.CategoryThresholds)
            if (scores.TryGetValue(category, out var s) && s >= threshold)
                return ModerationVerdict.Blocked(scores, $"category:{category} score:{s:F2}>={threshold:F2}", latencyMs);

        return new ModerationVerdict(ModerationOutcome.Allowed, scores, null, latencyMs);
    }

    /// <summary>
    /// OpenAI 2.2.0's <see cref="ModerationResult"/> exposes one strongly-typed
    /// <see cref="ModerationCategory"/> property per harm category — there is no
    /// enumerable .Categories collection or .Name on each category. Map them to the
    /// canonical OpenAI Moderation API string names (matching the wire JSON keys) so
    /// per-tenant safety profiles can reference categories by stable identifier.
    /// </summary>
    private static IReadOnlyDictionary<string, double> ProjectScores(ModerationResult result)
    {
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["harassment"] = result.Harassment.Score,
            ["harassment/threatening"] = result.HarassmentThreatening.Score,
            ["hate"] = result.Hate.Score,
            ["hate/threatening"] = result.HateThreatening.Score,
            ["illicit"] = result.Illicit.Score,
            ["illicit/violent"] = result.IllicitViolent.Score,
            ["self-harm"] = result.SelfHarm.Score,
            ["self-harm/instructions"] = result.SelfHarmInstructions.Score,
            ["self-harm/intent"] = result.SelfHarmIntent.Score,
            ["sexual"] = result.Sexual.Score,
            ["sexual/minors"] = result.SexualMinors.Score,
            ["violence"] = result.Violence.Score,
            ["violence/graphic"] = result.ViolenceGraphic.Score,
        };
        return scores;
    }
}
