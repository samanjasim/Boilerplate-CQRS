using System.Text.RegularExpressions;

namespace Starter.Module.AI.Infrastructure.Ingestion.Structured;

internal sealed class MarkdownBlockTokenizer
{
    private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex QuoteRegex = new(@"^\s*>\s?(.*)$", RegexOptions.Compiled);

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

            // body: collect until blank line or structural boundary (heading or quote)
            var body = new List<string> { line };
            i++;
            while (i < lines.Length && lines[i].Length > 0
                   && !HeadingRegex.IsMatch(lines[i])
                   && !QuoteRegex.IsMatch(lines[i]))
            {
                body.Add(lines[i]);
                i++;
            }
            blocks.Add(new MarkdownBlock(BlockType.Body, string.Join('\n', body)));
        }

        return blocks;
    }
}
