using System.Text.RegularExpressions;

namespace Starter.Module.AI.Infrastructure.Ingestion.Structured;

internal sealed class MarkdownBlockTokenizer
{
    private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex QuoteRegex = new(@"^\s*>\s?(.*)$", RegexOptions.Compiled);
    private static readonly Regex CodeFenceRegex = new(@"^```(\w*)\s*$", RegexOptions.Compiled);

    public IReadOnlyList<MarkdownBlock> Tokenize(string input)
    {
        var blocks = new List<MarkdownBlock>();
        if (string.IsNullOrEmpty(input)) return blocks;

        var lines = input.Replace("\r\n", "\n").Split('\n');
        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            if (line.Length == 0) { i++; continue; }

            // Code fence — must check before heading (``` could theoretically match if not guarded)
            var fence = CodeFenceRegex.Match(line);
            if (fence.Success)
            {
                var lang = string.IsNullOrWhiteSpace(fence.Groups[1].Value) ? null : fence.Groups[1].Value;
                i++;
                var buf = new List<string>();
                while (i < lines.Length && !CodeFenceRegex.IsMatch(lines[i]))
                {
                    buf.Add(lines[i]);
                    i++;
                }
                if (i < lines.Length) i++; // consume closing fence when present
                blocks.Add(new MarkdownBlock(BlockType.Code, string.Join('\n', buf), CodeLanguage: lang));
                continue;
            }

            var h = HeadingRegex.Match(line);
            if (h.Success)
            {
                blocks.Add(new MarkdownBlock(BlockType.Heading, h.Groups[2].Value, HeadingLevel: h.Groups[1].Length));
                i++;
                continue;
            }

            var q = QuoteRegex.Match(line);
            if (q.Success)
            {
                var buf = new List<string> { q.Groups[1].Value };
                i++;
                while (i < lines.Length)
                {
                    var next = QuoteRegex.Match(lines[i]);
                    if (!next.Success) break;
                    buf.Add(next.Groups[1].Value);
                    i++;
                }
                blocks.Add(new MarkdownBlock(BlockType.Quote, string.Join('\n', buf)));
                continue;
            }

            // body: collect until blank line or structural boundary
            var body = new List<string> { line };
            i++;
            while (i < lines.Length && lines[i].Length > 0
                   && !HeadingRegex.IsMatch(lines[i])
                   && !QuoteRegex.IsMatch(lines[i])
                   && !CodeFenceRegex.IsMatch(lines[i]))
            {
                body.Add(lines[i]);
                i++;
            }
            blocks.Add(new MarkdownBlock(BlockType.Body, string.Join('\n', body)));
        }

        return blocks;
    }
}
