using System.Text.Json;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Application.Eval.Faithfulness;

namespace Starter.Module.AI.Infrastructure.Eval.Faithfulness;

public sealed class LlmJudgeFaithfulness(IAiService ai) : IFaithfulnessJudge
{
    public async Task<FaithfulnessQuestionResult> JudgeAsync(
        EvalQuestion question,
        string context,
        string answer,
        string? modelOverride,
        CancellationToken ct)
    {
        var detectedLang = context.Any(c => c >= 0x0600 && c <= 0x06FF) ? "ar" : "en";
        var template = detectedLang == "ar"
            ? FaithfulnessJudgePrompts.ArabicPrompt
            : FaithfulnessJudgePrompts.EnglishPrompt;

        var prompt = template
            .Replace("{question}", question.Query)
            .Replace("{context}", context)
            .Replace("{answer}", answer);

        var options = new AiCompletionOptions(
            Model: modelOverride,
            Temperature: 0.0,
            MaxTokens: 1024);

        var firstResponse = await ai.CompleteAsync(prompt, options, ct);
        var parsed = TryParse(firstResponse?.Content);

        if (parsed is null)
        {
            var nudge = detectedLang == "ar"
                ? FaithfulnessJudgePrompts.RetryNudgeArabic
                : FaithfulnessJudgePrompts.RetryNudgeEnglish;
            var retryResponse = await ai.CompleteAsync(prompt + nudge, options, ct);
            parsed = TryParse(retryResponse?.Content);
        }

        if (parsed is null)
            return new FaithfulnessQuestionResult(question.Id, 0.0, Array.Empty<ClaimVerdict>(), JudgeParseFailed: true);

        var supported = parsed.Count(c => c.Verdict == "SUPPORTED");
        var score = parsed.Count == 0 ? 1.0 : (double)supported / parsed.Count;
        return new FaithfulnessQuestionResult(question.Id, score, parsed, JudgeParseFailed: false);
    }

    private static IReadOnlyList<ClaimVerdict>? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        var json = text[start..(end + 1)];

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("claims", out var claimsEl)) return null;
            var result = new List<ClaimVerdict>();
            foreach (var item in claimsEl.EnumerateArray())
            {
                var claim = item.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                var verdict = item.TryGetProperty("verdict", out var v) ? v.GetString() ?? "UNSUPPORTED" : "UNSUPPORTED";
                result.Add(new ClaimVerdict(claim, verdict));
            }
            return result;
        }
        catch (JsonException) { return null; }
    }
}
