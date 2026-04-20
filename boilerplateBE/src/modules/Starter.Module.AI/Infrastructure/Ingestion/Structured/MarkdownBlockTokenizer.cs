using System.Text.RegularExpressions;

namespace Starter.Module.AI.Infrastructure.Ingestion.Structured;

internal sealed class MarkdownBlockTokenizer
{
    private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex QuoteRegex = new(@"^\s*>\s?(.*)$", RegexOptions.Compiled);
    private static readonly Regex CodeFenceRegex = new(@"^```(\w*)\s*$", RegexOptions.Compiled);
    private static readonly Regex MathDisplayRegex = new(@"^\s*\$\$\s*$", RegexOptions.Compiled);
    private static readonly Regex BeginEquationRegex = new(@"^\s*\\begin\{equation\}\s*$", RegexOptions.Compiled);
    private static readonly Regex EndEquationRegex = new(@"^\s*\\end\{equation\}\s*$", RegexOptions.Compiled);

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

            // Math blocks
            var math = TryReadMath(lines, ref i);
            if (math is not null) { blocks.Add(math); continue; }

            // Code fence — must check before heading
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
                   && !CodeFenceRegex.IsMatch(lines[i])
                   && !MathDisplayRegex.IsMatch(lines[i])
                   && !BeginEquationRegex.IsMatch(lines[i]))
            {
                body.Add(lines[i]);
                i++;
            }
            blocks.Add(new MarkdownBlock(BlockType.Body, string.Join('\n', body)));
        }

        // Post-pass: merge adjacent math blocks
        var merged = new List<MarkdownBlock>(blocks.Count);
        foreach (var b in blocks)
        {
            if (b.Type == BlockType.Math && merged.Count > 0 && merged[^1].Type == BlockType.Math)
            {
                merged[^1] = merged[^1] with { Text = merged[^1].Text + "\n\n" + b.Text };
                continue;
            }
            merged.Add(b);
        }
        return merged;
    }

    private static MarkdownBlock? TryReadMath(string[] lines, ref int i)
    {
        if (MathDisplayRegex.IsMatch(lines[i]))
        {
            i++;
            var buf = new List<string>();
            while (i < lines.Length && !MathDisplayRegex.IsMatch(lines[i]))
            {
                buf.Add(lines[i]); i++;
            }
            if (i < lines.Length) i++;
            return new MarkdownBlock(BlockType.Math, string.Join('\n', buf));
        }
        if (BeginEquationRegex.IsMatch(lines[i]))
        {
            i++;
            var buf = new List<string>();
            while (i < lines.Length && !EndEquationRegex.IsMatch(lines[i]))
            {
                buf.Add(lines[i]); i++;
            }
            if (i < lines.Length) i++;
            return new MarkdownBlock(BlockType.Math, string.Join('\n', buf));
        }
        return null;
    }
}
