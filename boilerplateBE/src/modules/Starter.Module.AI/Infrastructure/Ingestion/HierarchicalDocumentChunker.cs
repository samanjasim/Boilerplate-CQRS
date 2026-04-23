using Starter.Module.AI.Application.Services.Ingestion;

namespace Starter.Module.AI.Infrastructure.Ingestion;

internal sealed class HierarchicalDocumentChunker(TokenCounter counter) : IDocumentChunker
{
    public HierarchicalChunks Chunk(ExtractedDocument document, ChunkingOptions options)
    {
        var parents = new List<ChunkDraft>();
        var children = new List<ChunkDraft>();

        foreach (var page in document.Pages)
        {
            if (string.IsNullOrWhiteSpace(page.Text)) continue;

            var parentPieces = counter.Split(page.Text, options.ParentTokens).ToList();
            foreach (var parentText in parentPieces)
            {
                var parentIndex = parents.Count;
                parents.Add(new ChunkDraft(
                    Index: parentIndex,
                    Content: parentText,
                    TokenCount: counter.Count(parentText),
                    ParentIndex: null,
                    SectionTitle: page.SectionTitle,
                    PageNumber: page.PageNumber));

                var step = Math.Max(1, options.ChildTokens - options.ChildOverlapTokens);
                foreach (var slice in counter.SlideWindow(parentText, options.ChildTokens, step))
                {
                    children.Add(new ChunkDraft(
                        Index: children.Count,
                        Content: slice,
                        TokenCount: counter.Count(slice),
                        ParentIndex: parentIndex,
                        SectionTitle: page.SectionTitle,
                        PageNumber: page.PageNumber));
                }
            }
        }

        return new HierarchicalChunks(parents, children);
    }
}
