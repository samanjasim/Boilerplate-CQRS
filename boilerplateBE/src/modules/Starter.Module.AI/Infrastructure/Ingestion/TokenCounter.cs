using SharpToken;

namespace Starter.Module.AI.Infrastructure.Ingestion;

public sealed class TokenCounter
{
    // cl100k_base is the tokenizer used by OpenAI text-embedding-3-* and GPT-4.
    // It is a reasonable cross-provider approximation for chunk sizing.
    private readonly GptEncoding _encoding = GptEncoding.GetEncoding("cl100k_base");

    public int Count(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return _encoding.Encode(text).Count;
    }

    public IEnumerable<string> Split(string text, int maxTokens)
    {
        if (maxTokens <= 0) throw new ArgumentOutOfRangeException(nameof(maxTokens));
        if (string.IsNullOrEmpty(text)) yield break;

        var tokens = _encoding.Encode(text);
        for (var i = 0; i < tokens.Count; i += maxTokens)
        {
            var slice = tokens.GetRange(i, Math.Min(maxTokens, tokens.Count - i));
            yield return _encoding.Decode(slice);
        }
    }

    public IEnumerable<string> SlideWindow(string text, int windowTokens, int stepTokens)
    {
        if (windowTokens <= 0) throw new ArgumentOutOfRangeException(nameof(windowTokens));
        if (stepTokens <= 0) throw new ArgumentOutOfRangeException(nameof(stepTokens));
        if (string.IsNullOrEmpty(text)) yield break;

        var tokens = _encoding.Encode(text);
        if (tokens.Count == 0) yield break;

        for (var i = 0; i < tokens.Count; i += stepTokens)
        {
            var size = Math.Min(windowTokens, tokens.Count - i);
            yield return _encoding.Decode(tokens.GetRange(i, size));
            if (i + size >= tokens.Count) yield break;
        }
    }
}
