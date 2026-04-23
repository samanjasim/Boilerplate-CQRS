using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Module.AI.Application.Services.Runtime;

/// <summary>
/// Detects agent loops where the model repeatedly emits the same tool call with the
/// same arguments. Comparison is by (Name, canonical-JSON args) so reordered keys
/// still match. When the last N invocations are identical, ShouldBreak returns true
/// and the runtime terminates with AgentRunStatus.LoopBreak.
/// </summary>
internal sealed class LoopBreakDetector
{
    private readonly LoopBreakPolicy _policy;
    private readonly List<string> _recent = new();

    public LoopBreakDetector(LoopBreakPolicy policy)
    {
        _policy = policy;
    }

    public bool ShouldBreak(AiToolCall call)
    {
        if (!_policy.Enabled) return false;

        var fingerprint = Fingerprint(call);
        _recent.Add(fingerprint);

        // Keep only the window we need.
        var window = _policy.MaxIdenticalRepeats;
        if (_recent.Count > window)
            _recent.RemoveRange(0, _recent.Count - window);

        if (_recent.Count < window) return false;

        // All entries in the window must be identical.
        var first = _recent[0];
        for (var i = 1; i < _recent.Count; i++)
            if (!string.Equals(_recent[i], first, StringComparison.Ordinal))
                return false;

        return true;
    }

    private static string Fingerprint(AiToolCall call)
    {
        var canonical = CanonicalizeJson(call.ArgumentsJson);
        return $"{call.Name}\0{canonical}";
    }

    /// <summary>
    /// Deterministic JSON normalization: parse, sort object keys alphabetically at every
    /// depth, re-serialise with no whitespace. {"b":1,"a":2} and {"a":2,"b":1} produce
    /// the same output. Malformed JSON is passed through verbatim — the detector must
    /// never throw on malformed provider input.
    /// </summary>
    private static string CanonicalizeJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "";
        try
        {
            var node = JsonNode.Parse(json);
            return SerializeSorted(node);
        }
        catch
        {
            return json;
        }
    }

    private static string SerializeSorted(JsonNode? node)
    {
        if (node is null) return "null";
        if (node is JsonValue) return node.ToJsonString();
        if (node is JsonArray arr)
        {
            var sb = new StringBuilder("[");
            for (var i = 0; i < arr.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(SerializeSorted(arr[i]));
            }
            sb.Append(']');
            return sb.ToString();
        }
        if (node is JsonObject obj)
        {
            var sb = new StringBuilder("{");
            var first = true;
            foreach (var kv in obj.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(JsonSerializer.Serialize(kv.Key));
                sb.Append(':');
                sb.Append(SerializeSorted(kv.Value));
            }
            sb.Append('}');
            return sb.ToString();
        }
        return node.ToJsonString();
    }
}
