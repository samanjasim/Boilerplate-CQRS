using System.Text.Json;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Application.Eval.Faithfulness;

namespace Starter.Module.AI.Infrastructure.Eval.Faithfulness;

public sealed class LlmJudgeFaithfulness(
    IAiService ai,
    ILogger<LlmJudgeFaithfulness> logger) : IFaithfulnessJudge
{
    public async Task<FaithfulnessQuestionResult> JudgeAsync(
        EvalQuestion question,
        string context,
        string answer,
        string? modelOverride,
        CancellationToken ct)
    {
        var detectedLang = DetectLanguage(context, question.Query);
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

        AiCompletionResult? firstResponse;
        try
        {
            firstResponse = await ai.CompleteAsync(prompt, options, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Faithfulness judge call failed for question={QuestionId}; treating as parse failure",
                question.Id);
            return new FaithfulnessQuestionResult(
                question.Id, 0.0, Array.Empty<ClaimVerdict>(), JudgeParseFailed: true);
        }

        var parsed = TryParse(firstResponse?.Content);

        if (parsed is null)
        {
            var nudge = detectedLang == "ar"
                ? FaithfulnessJudgePrompts.RetryNudgeArabic
                : FaithfulnessJudgePrompts.RetryNudgeEnglish;
            try
            {
                var retryResponse = await ai.CompleteAsync(prompt + nudge, options, ct);
                parsed = TryParse(retryResponse?.Content);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Faithfulness judge retry failed for question={QuestionId}", question.Id);
            }
        }

        if (parsed is null)
        {
            logger.LogWarning(
                "Faithfulness judge produced unparseable output for question={QuestionId}", question.Id);
            return new FaithfulnessQuestionResult(
                question.Id, 0.0, Array.Empty<ClaimVerdict>(), JudgeParseFailed: true);
        }

        var supported = parsed.Count(c => c.Verdict == "SUPPORTED");
        var score = parsed.Count == 0 ? 1.0 : (double)supported / parsed.Count;
        return new FaithfulnessQuestionResult(question.Id, score, parsed, JudgeParseFailed: false);
    }

    // Covers Arabic (U+0600–06FF), Arabic Supplement (U+0750–077F),
    // Arabic Extended-A (U+08A0–08FF), and Presentation Forms-A/B (U+FB50–FDFF, U+FE70–FEFF).
    private static string DetectLanguage(string context, string query)
    {
        foreach (var source in new[] { context, query })
        {
            foreach (var c in source)
            {
                if ((c >= 0x0600 && c <= 0x06FF) ||
                    (c >= 0x0750 && c <= 0x077F) ||
                    (c >= 0x08A0 && c <= 0x08FF) ||
                    (c >= 0xFB50 && c <= 0xFDFF) ||
                    (c >= 0xFE70 && c <= 0xFEFF))
                    return "ar";
            }
        }
        return "en";
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
