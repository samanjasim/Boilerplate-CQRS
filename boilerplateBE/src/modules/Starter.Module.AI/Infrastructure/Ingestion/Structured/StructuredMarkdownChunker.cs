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

                var tokens = counter.Count(block.Text);
                var child = new ChunkDraft(
                    Index: children.Count + parentBuf.Count,
                    Content: block.Text,
                    TokenCount: tokens,
                    ParentIndex: null,
                    SectionTitle: stack.DeepestHeading,
                    PageNumber: page.PageNumber,
                    ChunkType: block.ToChunkType());

                if (parentTokens + tokens > options.ParentTokens && parentBuf.Count > 0)
                    FlushParent();

                parentBuf.Add(child);
                parentTokens += tokens;
            }

            FlushParent();
        }

        return new HierarchicalChunks(parents, children);
    }
}
