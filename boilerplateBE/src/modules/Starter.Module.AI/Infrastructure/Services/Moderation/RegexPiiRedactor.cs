using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Services.Moderation;

/// <summary>
/// Default <see cref="IPiiRedactor"/> implementation backed by a small set of
/// source-generated regular expressions. Replaces matches with the literal
/// token <c>[REDACTED]</c> and reports per-category hit counts so the caller
/// can emit <c>AiContentModeratedEvent</c> telemetry. The card detector applies
/// a Luhn check to avoid scrubbing arbitrary 13–19 digit runs that happen to
/// look like card numbers.
/// </summary>
internal sealed partial class RegexPiiRedactor(ILogger<RegexPiiRedactor> logger) : IPiiRedactor
{
    private const string Token = "[REDACTED]";

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\+?[1-9]\d{1,14}", RegexOptions.Compiled)]
    private static partial Regex E164PhoneRegex();

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex SsnRegex();

    [GeneratedRegex(@"\b(?:\d[ -]?){13,19}\b", RegexOptions.Compiled)]
    private static partial Regex CardCandidateRegex();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}[A-Z0-9]{1,30}\b", RegexOptions.Compiled)]
    private static partial Regex IbanRegex();

    public Task<RedactionResult> RedactAsync(string text, ResolvedSafetyProfile profile, CancellationToken ct)
    {
        if (!profile.RedactPii || string.IsNullOrEmpty(text))
            return Task.FromResult(new RedactionResult(ModerationOutcome.Allowed, text, new Dictionary<string, int>()));

        try
        {
            var hits = new Dictionary<string, int>();
            var working = text;

            working = ReplaceWithCount(working, EmailRegex(), "pii-email", hits);
            working = ReplaceWithCount(working, SsnRegex(), "pii-ssn", hits);
            working = ReplaceWithCount(working, IbanRegex(), "pii-iban", hits);
            working = ReplaceCards(working, hits);
            // Phone last so it doesn't mis-match digit runs that were card numbers
            working = ReplaceWithCount(working, E164PhoneRegex(), "pii-phone", hits);

            var outcome = hits.Count > 0 ? ModerationOutcome.Redacted : ModerationOutcome.Allowed;
            return Task.FromResult(new RedactionResult(outcome, working, hits));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PII redactor threw; returning text unmodified.");
            return Task.FromResult(new RedactionResult(
                ModerationOutcome.Allowed, text, new Dictionary<string, int>(), Failed: true));
        }
    }

    private static string ReplaceWithCount(string input, Regex pattern, string label, Dictionary<string, int> hits)
    {
        var matches = pattern.Matches(input);
        if (matches.Count == 0) return input;
        hits[label] = (hits.TryGetValue(label, out var n) ? n : 0) + matches.Count;
        return pattern.Replace(input, Token);
    }

    private static string ReplaceCards(string input, Dictionary<string, int> hits)
    {
        return CardCandidateRegex().Replace(input, m =>
        {
            var digits = new string(m.Value.Where(char.IsDigit).ToArray());
            if (digits.Length is < 13 or > 19) return m.Value;
            if (!LuhnValid(digits)) return m.Value;
            hits["pii-card"] = (hits.TryGetValue("pii-card", out var n) ? n : 0) + 1;
            return Token;
        });
    }

    private static bool LuhnValid(string digits)
    {
        var sum = 0;
        var alt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (alt) { d *= 2; if (d > 9) d -= 9; }
            sum += d;
            alt = !alt;
        }
        return sum % 10 == 0;
    }
}
