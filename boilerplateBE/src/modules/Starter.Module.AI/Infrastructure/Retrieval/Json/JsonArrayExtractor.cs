using System.Text.Json;

namespace Starter.Module.AI.Infrastructure.Retrieval.Json;

internal static class JsonArrayExtractor
{
    public static bool TryExtractStrings(string input, out IReadOnlyList<string> items)
    {
        if (JsonLooseExtractor.TryExtractArray(input, out var array))
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
        if (JsonLooseExtractor.TryExtractArray(input, out var array))
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
}
