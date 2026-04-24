using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Starter.Module.AI.Application.Services.Personas;

namespace Starter.Module.AI.Infrastructure.Services.Personas;

internal sealed class SlugGenerator : ISlugGenerator
{
    private const int MaxLength = 64;
    private static readonly Regex NonAlphaNum = new("[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex MultiDash = new("-{2,}", RegexOptions.Compiled);

    public string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "untitled";

        var normalised = input.Trim().Normalize(NormalizationForm.FormKD);

        var filtered = new StringBuilder(normalised.Length);
        foreach (var c in normalised)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != UnicodeCategory.NonSpacingMark)
                filtered.Append(c);
        }

        var lower = filtered.ToString().ToLowerInvariant();
        var cleaned = NonAlphaNum.Replace(lower, "-").Trim('-');
        cleaned = MultiDash.Replace(cleaned, "-");

        if (cleaned.Length == 0)
            return "untitled";

        if (cleaned.Length > MaxLength)
            cleaned = cleaned[..MaxLength].TrimEnd('-');

        return cleaned;
    }

    public string EnsureUnique(string slug, ISet<string> taken)
    {
        if (!taken.Contains(slug))
            return slug;

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{slug}-{i}";
            if (candidate.Length > MaxLength)
            {
                var suffix = "-" + i.ToString();
                candidate = slug[..(MaxLength - suffix.Length)] + suffix;
            }
            if (!taken.Contains(candidate))
                return candidate;
        }
        throw new InvalidOperationException("Could not find a unique slug.");
    }
}
