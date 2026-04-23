# Plan 4b-3 — Structure-Aware Chunking Design

**Status:** Draft — follow-on to Plan 4b-1. Independent of 4b-2 but 4b-2 will pick up better chunks automatically when this lands.

**Depends on:** Plan 4b-1 (`ArabicTextNormalizer` for chunk-index-time normalization; `NormalizedContent` column already exists).

## Goal

Stop slicing through headings, tables, code blocks, and equations. Preserve the logical structure of source documents in chunk boundaries, and carry enough metadata on each chunk that downstream stages (reranker, retrieval filters, citation display) can use it.

## Why now

`HierarchicalDocumentChunker` is token-window-based: it treats each extracted page as a flat blob and slices it every `ChildTokens` (~512) with overlap. For structured documents — which is most of what our users upload — this produces chunks where:

- A chapter heading lands in one chunk and its body in the next, stranding the heading without context.
- A table row starts in one chunk and ends in another — the LLM sees half a row with no header.
- A multi-step math derivation gets cut at an arbitrary equation.
- Code blocks lose their surrounding language indicator.
- `SectionTitle` on `ChunkDraft` exists but is populated only if the extractor set it (our extractors don't). So it's always null.

Tutor-AI's `chunk_markdown` handles all of this through typed block detection + heading breadcrumbs. We port the approach, adapt to the .NET extractor set, and extend with Arabic heading support.

## Scope

In scope:

- A new `StructuredMarkdownChunker : IDocumentChunker` that consumes markdown-like text, splits into typed blocks, and emits chunks with structural metadata.
- A `ChunkType` enum added to `AiDocumentChunk` (migration needed in consuming apps).
- Heading breadcrumbs prepended to each chunk's searchable text.
- Block-atomic guarantees for tables, code, math, lists.
- Arabic heading + punctuation detection.
- Chunker selection: use the structural chunker when the extracted text is markdown-ish; fall back to the existing token-window chunker otherwise.

Out of scope:

- PDF → markdown converter. PDFs continue using the existing extractor output (plain text). The structural chunker benefits markdown/HTML/docx sources; for plain PDFs it degrades gracefully to the token-window chunker.
- Image/figure linking. If a PDF extractor gains image-per-page output later, we can wire figure candidates like tutor-AI does; not in this plan.
- OCR confidence scoring on a per-chunk basis.

## Components

### 1. `ChunkType` enum

```csharp
public enum ChunkType
{
    Body = 0,       // default / plain paragraph
    Heading = 1,    // # ## ### etc.
    Table = 2,      // | ... | ... |
    Code = 3,       // ```lang ... ```
    Math = 4,       // $$ ... $$ or \begin{equation} ... \end{equation}
    List = 5,       // - * + or 1.
    Quote = 6       // > ...
}
```

Added as a new column on `AiDocumentChunk` (non-nullable, default `Body` for existing rows). Carried through to the `VectorPayload` so Qdrant can filter on it.

### 2. `StructuredMarkdownChunker`

**Contract:** implements `IDocumentChunker` (same as current `HierarchicalDocumentChunker`). Swap-in compatible.

**Pipeline:**

1. **Block tokenization.** Line-by-line scan, state machine, emits typed blocks. Block regexes (multiline-aware):
   - Heading: `^(#{1,6})\s+(.+)$`
   - Code fence start/end: `^```(\w*)$`
   - Math display: `^\s*\$\$\s*$` … `^\s*\$\$\s*$` or `\\begin{equation}` … `\\end{equation}`
   - Table: `^\|.+\|$` contiguous with a separator row
   - List: `^\s*([-*+]|\d+\.)\s+`
   - Quote: `^\s*>\s+`
   - Everything else: `Body`
2. **Heading stack.** Maintain a stack of `(level, text)` updated on each `Heading` block. The breadcrumb for the current position is e.g. `Chapter 1 > Section 1.2 > Pumps`. Persisted on each subsequent chunk's `SectionTitle` and prepended to its `NormalizedContent` for FTS.
3. **Atomic block guarantees.**
   - A `Heading` never stands alone; it attaches to the first following non-heading block.
   - A `Table` is emitted whole — if it exceeds `MaxBlockTokens`, it's split by row-group with the header row replicated.
   - A `Code` block is emitted whole; if too large, split by blank lines inside, each child kept under `MaxBlockTokens`.
   - A `Math` block is emitted whole; adjacent `Math` blocks separated only by whitespace are merged (multi-step derivations stay together).
4. **Token-window fallback** for oversize `Body` runs: apply the existing `HierarchicalDocumentChunker`-style sliding window with overlap.
5. **Parent/child emission** unchanged from 4b: each structural chunk becomes a `child`, and a `parent` wraps `N` adjacent children (fixed ~1536 tokens). `parent.ChunkType = Body` regardless of member types. `child.ParentIndex` links.

**Heading carry-through:** each child's `Content` stays the original block text, but `NormalizedContent` (used for FTS) is prepended with the breadcrumb + single newline:

```
Chapter 1 > Section 1.2 > Pumps
Actual body text here...
```

This materially helps keyword retrieval: a query for "pumps" matches chunks whose body never mentions the word but sit under a "Pumps" heading.

### 3. Arabic structure support

- **Arabic headings.** Markdown `# مقدمة` works identically to `# Introduction`. No Arabic-specific regex needed for heading detection.
- **Arabic punctuation in block splits.** Body block splitting uses sentence boundaries. Default to `. ! ? ؟ ،` (arabic comma) as sentence-ending candidates. The existing English-only regex breaks long Arabic paragraphs into one chunk.
- **RTL-safe breadcrumb join.** Use ` > ` (space-gt-space) — the BiDi algorithm handles mixed LTR/RTL correctly at display and index time. Do NOT use the heavier Unicode separator (` » `) because it's not always indexed consistently by Postgres FTS.
- **Tatweel in headings** (common in decorative Arabic typography): stripped by `ArabicTextNormalizer` in `NormalizedContent` so they don't split the FTS index.

### 4. Content-type based chunker selection

`IDocumentChunker` stays a single DI registration. Introduce a `ChunkerRouter : IDocumentChunker` that dispatches:

- `text/markdown` → `StructuredMarkdownChunker`
- `text/html` → `StructuredMarkdownChunker` (after HTML → markdown preprocessing with `ReverseMarkdown` or similar)
- DOCX/PDF/CSV/TXT → keep `HierarchicalDocumentChunker`

**Heuristic fallback:** if `ContentType` is `text/plain` but the first 4 non-empty lines contain `#` or `##` prefixes, route through `StructuredMarkdownChunker` anyway. A small optimization; users sometimes upload raw markdown as `.txt`.

### 5. Carrying `ChunkType` to Qdrant + retrieval

**Qdrant payload:** add `ChunkType` alongside existing payload. Qdrant indexes it as a keyword field so `chunk_type=code` filters are fast.

**Retrieval filter (for later, not in this plan):** expose `IReadOnlyCollection<ChunkType>? TypeFilter` on `IVectorStore.SearchAsync` + `IKeywordSearchService.SearchAsync`. 4b-3 adds the parameter but leaves the default null; downstream callers get it for free.

**ContextPromptBuilder:** when `ChunkType == Code`, wrap content in triple backticks on re-injection so the model sees it as code. When `ChunkType == Math`, wrap in `$$ ... $$`. Tables pass through (already markdown-formatted).

## Schema changes

- `AiDocumentChunk.ChunkType` — new `ChunkType` (smallint) column, NOT NULL default `Body`.
- Consuming apps regenerate EF migration.
- Backfill: old rows default to `Body` — acceptable since `ChunkType` is a hint, not a filter gate by default.

## Risks & mitigations

| Risk | Mitigation |
|---|---|
| Regex block detection misfires on pathological markdown (deeply nested lists, escaped fences) | Fall back to token-window chunker when any block exceeds `MaxBlockTokens` × 4 (obvious parse error) |
| Heading stack becomes stale across page breaks in multi-page DOCX | Page boundaries reset the stack (configurable); section-continued-across-page is sacrificed for robustness |
| Table replication doubles storage for large tables | Cap header-replication to `TableMaxRows`; oversized tables are emitted as Body with a warning log |
| Arabic documents that don't use markdown headings get no structural benefit | Accepted. PDF → markdown is out of scope; those docs stay on the token-window chunker. |
| ChunkerRouter wrong-routes a plain-text doc with `#` symbols that aren't headings | Heuristic requires first 4 non-empty lines to have `#` prefix AND the rest contain blank lines; false positives rare |

## Estimated effort

| Item | Effort |
|---|---|
| 1. Block tokenizer + heading stack + atomic rules | ~1.5d |
| 2. Parent/child emission + token fallback | ~0.5d |
| 3. `ChunkType` enum + schema wiring + payload + Qdrant field | ~0.5d |
| 4. `ChunkerRouter` + HTML-to-markdown | ~0.5d |
| 5. Arabic sentence boundary + RTL handling + fixtures | ~0.5d |
| 6. Tests (block detection, heading stack, math merge, tables, Arabic) | ~1.5d |
| **Total** | **~5 days** |

## Open questions

- **Nested list handling.** Treat as single `List` block or emit children separately? Start with "single block, split on token overflow" — simplest.
- **`ReverseMarkdown` dependency.** Adds a NuGet package; alternative is regex HTML stripping. Recommend the package for correctness.
- **When the fallback token-window fires** — do we still emit a breadcrumb? Yes — the router knows the heading stack at the boundary, so even fallback chunks carry the last-seen `SectionTitle`.
