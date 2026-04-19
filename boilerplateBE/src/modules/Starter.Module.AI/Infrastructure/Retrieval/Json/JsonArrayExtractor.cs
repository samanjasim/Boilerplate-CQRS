using System.Text.Json;

namespace Starter.Module.AI.Infrastructure.Retrieval.Json;

internal static class JsonArrayExtractor
{
    public static bool TryExtractStrings(string input, out IReadOnlyList<string> items)
    {
        if (TryExtractArrayElement(input, out var array))
        {
            var list = new List<string>(array.GetArrayLength());
            foreach (var el in array.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String)
                    list.Add(el.GetString() ?? string.Empty);
            }
            items = list;
            return list.Count > 0;
        }
        items = Array.Empty<string>();
        return false;
    }

    public static bool TryExtractInts(string input, out IReadOnlyList<int> items)
    {
        if (TryExtractArrayElement(input, out var array))
        {
            var list = new List<int>(array.GetArrayLength());
            foreach (var el in array.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i))
                    list.Add(i);
            }
            items = list;
            return list.Count > 0;
        }
        items = Array.Empty<int>();
        return false;
    }

    private static bool TryExtractArrayElement(string input, out JsonElement array)
    {
        array = default;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var start = input.IndexOf('[');
        var end = input.LastIndexOf(']');
        if (start < 0 || end <= start) return false;

        var slice = input.AsSpan(start, end - start + 1).ToString();
        try
        {
            using var doc = JsonDocument.Parse(slice);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return false;
            array = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
