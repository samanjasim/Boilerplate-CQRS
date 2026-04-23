using System.Text.Json;

namespace Starter.Module.AI.Infrastructure.Retrieval.Json;

/// <summary>
/// Tolerant JSON slicing. LLMs often wrap JSON in prose ("Here is the result: [1,2,3]
/// — hope that helps"); these helpers locate the first well-formed array or object
/// in the input and parse it. Returns false if no balanced delimiter pair parses.
/// </summary>
internal static class JsonLooseExtractor
{
    public static bool TryExtractArray(string input, out JsonElement array)
        => TryExtractBalanced(input, '[', ']', out array)
           && array.ValueKind == JsonValueKind.Array;

    public static bool TryExtractObject(string input, out JsonElement obj)
        => TryExtractBalanced(input, '{', '}', out obj)
           && obj.ValueKind == JsonValueKind.Object;

    private static bool TryExtractBalanced(string input, char open, char close, out JsonElement element)
    {
        element = default;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var start = input.IndexOf(open);
        var end = input.LastIndexOf(close);
        if (start < 0 || end <= start) return false;

        var slice = input.Substring(start, end - start + 1);
        try
        {
            using var doc = JsonDocument.Parse(slice);
            element = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
