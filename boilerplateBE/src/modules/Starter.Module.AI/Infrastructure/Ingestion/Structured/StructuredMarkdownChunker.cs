using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Ingestion.Structured;

internal sealed class StructuredMarkdownChunker(
    TokenCounter counter,
    MarkdownBlockTokenizer tokenizer) : IDocumentChunker
{
    public HierarchicalChunks Chunk(ExtractedDocument document, ChunkingOptions options)
    {
        var parents = new List<ChunkDraft>();
        var children = new List<ChunkDraft>();
        var stack = new HeadingStack();

        foreach (var page in document.Pages)
        {
            if (string.IsNullOrWhiteSpace(page.Text)) continue;
            stack.Reset();

            var blocks = tokenizer.Tokenize(page.Text);

            var parentBuf = new List<ChunkDraft>();
            var parentTokens = 0;

            void FlushParent()
            {
                if (parentBuf.Count == 0) return;
                var parentText = string.Join("\n\n", parentBuf.Select(c => c.Content));
                var parentIndex = parents.Count;
                var first = parentBuf[0];
                parents.Add(new ChunkDraft(
                    Index: parentIndex,
                    Content: parentText,
                    TokenCount: counter.Count(parentText),
                    ParentIndex: null,
                    SectionTitle: first.SectionTitle,
                    PageNumber: page.PageNumber,
                    ChunkType: ChunkType.Body));
                foreach (var c in parentBuf)
                    children.Add(c with { ParentIndex = parentIndex, Index = children.Count });
                parentBuf.Clear();
                parentTokens = 0;
            }

            foreach (var block in blocks)
            {
                if (block.Type == BlockType.Heading)
                {
                    stack.Push(block.HeadingLevel, block.Text);
                    continue;
                }

                foreach (var piece in Split(block, options, counter))
                {
                    var child = new ChunkDraft(
                        Index: children.Count + parentBuf.Count,
                        Content: piece.Text,
                        TokenCount: piece.Tokens,
                        ParentIndex: null,
                        SectionTitle: stack.DeepestHeading,
                        PageNumber: page.PageNumber,
                        ChunkType: piece.Type);

                    if (parentTokens + piece.Tokens > options.ParentTokens && parentBuf.Count > 0)
                        FlushParent();

                    parentBuf.Add(child);
                    parentTokens += piece.Tokens;
                }
            }

            FlushParent();
        }

        return new HierarchicalChunks(parents, children);
    }

    private IEnumerable<(string Text, int Tokens, ChunkType Type)> Split(
        MarkdownBlock block, ChunkingOptions options, TokenCounter counter)
    {
        var tokens = counter.Count(block.Text);
        if (tokens <= options.ChildTokens)
        {
            yield return (block.Text, tokens, block.ToChunkType());
            yield break;
        }

        if (block.Type == BlockType.Code)
        {
            var groups = block.Text.Split(new[] { "\n\n" }, StringSplitOptions.None);
            foreach (var g in groups)
            {
                var t = counter.Count(g);
                if (t <= options.ChildTokens)
                {
                    yield return (g, t, ChunkType.Code);
                }
                else
                {
                    foreach (var slice in SlideText(g, options, counter))
                        yield return (slice.Text, slice.Tokens, ChunkType.Code);
                }
            }
            yield break;
        }

        foreach (var slice in SlideText(block.Text, options, counter))
            yield return (slice.Text, slice.Tokens, block.ToChunkType());
    }

    private static IEnumerable<(string Text, int Tokens)> SlideText(
        string text, ChunkingOptions options, TokenCounter counter)
    {
        var step = Math.Max(1, options.ChildTokens - options.ChildOverlapTokens);
        foreach (var slice in counter.SlideWindow(text, options.ChildTokens, step))
            yield return (slice, counter.Count(slice));
    }
}
