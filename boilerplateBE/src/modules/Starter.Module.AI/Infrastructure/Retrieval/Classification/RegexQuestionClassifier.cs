using System.Text.RegularExpressions;
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval.Classification;

internal static partial class RegexQuestionClassifier
{
    [GeneratedRegex(@"^\s*(hi|hello|hey|greetings|good\s+(morning|afternoon|evening))\b", RegexOptions.IgnoreCase)]
    private static partial Regex GreetingEn();

    [GeneratedRegex(@"^\s*(مرحب[اًا]|السلام\s+عليكم|أهلا|اهلا|صباح\s+الخير|مساء\s+الخير)", RegexOptions.IgnoreCase)]
    private static partial Regex GreetingAr();

    [GeneratedRegex(@"\b(what\s+is|define|definition\s+of|meaning\s+of)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DefinitionEn();

    [GeneratedRegex(@"(ما\s+هي|ما\s+هو|تعريف|معنى)", RegexOptions.IgnoreCase)]
    private static partial Regex DefinitionAr();

    [GeneratedRegex(@"\b(list|show|display|give\s+me|enumerate)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ListingEn();

    [GeneratedRegex(@"(اعرض|اسرد|قائمة|اذكر|اعطني)", RegexOptions.IgnoreCase)]
    private static partial Regex ListingAr();

    [GeneratedRegex(@"\b(why|how\s+come|explain|compare)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ReasoningEn();

    [GeneratedRegex(@"(لماذا|كيف|فسر|اشرح|قارن)", RegexOptions.IgnoreCase)]
    private static partial Regex ReasoningAr();

    public static QuestionType? TryClassify(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        if (GreetingEn().IsMatch(query) || GreetingAr().IsMatch(query)) return QuestionType.Greeting;
        if (DefinitionEn().IsMatch(query) || DefinitionAr().IsMatch(query)) return QuestionType.Definition;
        if (ListingEn().IsMatch(query) || ListingAr().IsMatch(query)) return QuestionType.Listing;
        if (ReasoningEn().IsMatch(query) || ReasoningAr().IsMatch(query)) return QuestionType.Reasoning;

        return null;
    }
}
