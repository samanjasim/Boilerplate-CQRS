# Plan 4b-3 — Structure-Aware Chunking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop slicing through headings, tables, code blocks, and equations during ingestion. Emit typed chunks with heading breadcrumbs so retrieval, reranking, and prompt rendering can use structure.

**Architecture:** Add a `StructuredMarkdownChunker` that implements the existing `IDocumentChunker` contract. A `ChunkerRouter` dispatches between the structural chunker (markdown/html/heuristic-text) and the existing `HierarchicalDocumentChunker` (pdf/docx/csv/generic). A `ChunkType` enum flows from chunker → entity → vector payload → retrieved context → `ContextPromptBuilder`. Arabic is first-class: sentence splitting honors `؟ ،`, headings are detected on pure markdown (no locale branch), and breadcrumbs are `LTR > LTR` separator-safe.

**Tech Stack:** .NET 10, xUnit, FluentAssertions, Testcontainers.PostgreSql (already in test project), ReverseMarkdown (new NuGet — HTML → markdown).

**Spec:** [2026-04-19-ai-module-plan-4b-3-structural-chunking-design.md](../specs/2026-04-19-ai-module-plan-4b-3-structural-chunking-design.md)

**Scope reminder — migrations:** Per standing orders, EF Core migrations live in consuming apps, not `boilerplateBE`. The plan adds the entity property + EF configuration but does NOT run `dotnet ef migrations add` against `boilerplateBE`. The post-feature test app (`_test4b3/`) generates its own migration during QA.

---

### Task 1: Add `ChunkType` domain enum

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/ChunkType.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/ChunkTypeTests.cs`

- [ ] **Step 1: Write the failing test**

Create `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/ChunkTypeTests.cs`:

```csharp
using FluentAssertions;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class ChunkTypeTests
{
    [Fact]
    public void Body_is_zero_so_unset_rows_default_to_body()
    {
        ((int)ChunkType.Body).Should().Be(0);
    }

    [Theory]
    [InlineData(ChunkType.Body)]
    [InlineData(ChunkType.Heading)]
    [InlineData(ChunkType.Table)]
    [InlineData(ChunkType.Code)]
    [InlineData(ChunkType.Math)]
    [InlineData(ChunkType.List)]
    [InlineData(ChunkType.Quote)]
    public void All_declared_values_are_distinct(ChunkType type)
    {
        Enum.IsDefined(typeof(ChunkType), type).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~ChunkTypeTests"`
Expected: FAIL with `CS0246 The type or namespace name 'ChunkType' could not be found`.

- [ ] **Step 3: Write minimal implementation**

Create `boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/ChunkType.cs`:

```csharp
namespace Starter.Module.AI.Domain.Enums;

public enum ChunkType
{
    Body = 0,
    Heading = 1,
    Table = 2,
    Code = 3,
    Math = 4,
    List = 5,
    Quote = 6,
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~ChunkTypeTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Domain/Enums/ChunkType.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/ChunkTypeTests.cs
git commit -m "feat(ai): add ChunkType enum for structural chunking"
```

---

### Task 2: Carry `ChunkType` on `ChunkDraft`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/ChunkDraft.cs`

- [ ] **Step 1: Extend the record (no test; this is a data shape)**

Replace the whole file contents with:

```csharp
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Ingestion;

public sealed record ChunkDraft(
    int Index,
    string Content,
    int TokenCount,
    int? ParentIndex,
    string? SectionTitle,
    int? PageNumber,
    ChunkType ChunkType = ChunkType.Body);
```

`ChunkType` is the last positional parameter with a default, so the existing `HierarchicalDocumentChunker` call sites compile unchanged.

- [ ] **Step 2: Verify build**

Run: `dotnet build boilerplateBE/src/Starter.Api/Starter.Api.csproj`
Expected: `Build succeeded`. 0 errors.

- [ ] **Step 3: Re-run chunker unit tests to confirm no regression**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~HierarchicalDocumentChunkerTests"`
Expected: all pre-existing tests still pass.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/ChunkDraft.cs
git commit -m "feat(ai): propagate ChunkType on ChunkDraft"
```

---

### Task 3: Store `ChunkType` on `AiDocumentChunk`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiDocumentChunk.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiDocumentChunkConfiguration.cs`

- [ ] **Step 1: Write the failing test**

Add `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/AiDocumentChunkTypeTests.cs`:

```csharp
using FluentAssertions;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class AiDocumentChunkTypeTests
{
    [Fact]
    public void Create_defaults_chunk_type_to_body()
    {
        var chunk = AiDocumentChunk.Create(
            documentId: Guid.NewGuid(),
            chunkLevel: "child",
            content: "hello",
            chunkIndex: 0,
            tokenCount: 1,
            qdrantPointId: Guid.NewGuid());
        chunk.ChunkType.Should().Be(ChunkType.Body);
    }

    [Fact]
    public void Create_preserves_chunk_type_when_supplied()
    {
        var chunk = AiDocumentChunk.Create(
            documentId: Guid.NewGuid(),
            chunkLevel: "child",
            content: "```\nx\n```",
            chunkIndex: 0,
            tokenCount: 3,
            qdrantPointId: Guid.NewGuid(),
            chunkType: ChunkType.Code);
        chunk.ChunkType.Should().Be(ChunkType.Code);
    }
}
```

- [ ] **Step 2: Run the test and confirm it fails**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~AiDocumentChunkTypeTests"`
Expected: FAIL compile — `ChunkType` property does not exist on `AiDocumentChunk`.

- [ ] **Step 3: Add the property and factory parameter**

In `AiDocumentChunk.cs`:

1. Add `using Starter.Module.AI.Domain.Enums;` at the top.
2. Add `public ChunkType ChunkType { get; private set; }` after `PageNumber`.
3. Extend the private constructor signature with `ChunkType chunkType` as the last parameter and assign `ChunkType = chunkType;` in the body.
4. Extend `Create(...)` with `ChunkType chunkType = ChunkType.Body` as the last parameter and pass it through.

In `AiDocumentChunkConfiguration.cs`, inside `Configure(...)`:

```csharp
builder.Property(c => c.ChunkType)
    .HasConversion<short>()
    .HasDefaultValue(ChunkType.Body)
    .IsRequired();
```

(Add the corresponding `using Starter.Module.AI.Domain.Enums;`.)

- [ ] **Step 4: Verify tests pass**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~AiDocumentChunkTypeTests"`
Expected: 2 tests pass.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Domain/Entities/AiDocumentChunk.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Configurations/AiDocumentChunkConfiguration.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/AiDocumentChunkTypeTests.cs
git commit -m "feat(ai): store ChunkType on AiDocumentChunk"
```

**Note:** EF migration generation is skipped here per standing orders. Consuming apps (including `_test4b3`) generate their own migration during post-feature testing.

---

### Task 4: Carry `ChunkType` on `VectorPayload`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/VectorPayload.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Consumers/ProcessDocumentConsumer.cs` (call sites only)

- [ ] **Step 1: Extend the record**

Replace `VectorPayload.cs`:

```csharp
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Ingestion;

public sealed record VectorPayload(
    Guid DocumentId,
    string DocumentName,
    string ChunkLevel,
    int ChunkIndex,
    string? SectionTitle,
    int? PageNumber,
    Guid? ParentChunkId,
    Guid TenantId,
    ChunkType ChunkType = ChunkType.Body);
```

- [ ] **Step 2: Update both `VectorPayload(...)` constructions in `ProcessDocumentConsumer.cs`**

Both payload sites currently stop at `TenantId: cloneTenantId` / `TenantId: tenantId`. Leave those alone — the default `ChunkType.Body` applies until Task 12 wires the real value. Just make sure the file still compiles (no edits yet to that file).

- [ ] **Step 3: Verify build + Qdrant serialization test**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: 0 errors.

Add `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/VectorPayloadChunkTypeTests.cs`:

```csharp
using FluentAssertions;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class VectorPayloadChunkTypeTests
{
    [Fact]
    public void Chunk_type_defaults_to_body()
    {
        var p = new VectorPayload(Guid.NewGuid(), "doc", "child", 0, null, null, null, Guid.NewGuid());
        p.ChunkType.Should().Be(ChunkType.Body);
    }

    [Fact]
    public void Chunk_type_preserved_when_specified()
    {
        var p = new VectorPayload(Guid.NewGuid(), "doc", "child", 0, null, null, null, Guid.NewGuid(), ChunkType.Code);
        p.ChunkType.Should().Be(ChunkType.Code);
    }
}
```

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~VectorPayloadChunkTypeTests"`
Expected: 2 pass.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/VectorPayload.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/VectorPayloadChunkTypeTests.cs
git commit -m "feat(ai): carry ChunkType on VectorPayload"
```

---

### Task 5: `MarkdownBlock` + `BlockType` data carriers

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/MarkdownBlock.cs`

- [ ] **Step 1: Add the internal data types (no test; they're plain records)**

```csharp
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Ingestion.Structured;

internal enum BlockType { Body, Heading, Code, Math, Table, List, Quote }

internal sealed record MarkdownBlock(
    BlockType Type,
    string Text,
    int HeadingLevel = 0,
    string? CodeLanguage = null)
{
    public ChunkType ToChunkType() => Type switch
    {
        BlockType.Heading => ChunkType.Heading,
        BlockType.Code => ChunkType.Code,
        BlockType.Math => ChunkType.Math,
        BlockType.Table => ChunkType.Table,
        BlockType.List => ChunkType.List,
        BlockType.Quote => ChunkType.Quote,
        _ => ChunkType.Body,
    };
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build boilerplateBE/Starter.sln`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/MarkdownBlock.cs
git commit -m "feat(ai): MarkdownBlock and BlockType carriers for structural chunker"
```

---

### Task 6: `MarkdownBlockTokenizer` — heading, body, quote

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/MarkdownBlockTokenizer.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/MarkdownBlockTokenizerTests.cs`

- [ ] **Step 1: Write the failing tests (heading + body + quote)**

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Ingestion.Structured;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class MarkdownBlockTokenizerTests
{
    private static IReadOnlyList<MarkdownBlock> Tokenize(string input) =>
        new MarkdownBlockTokenizer().Tokenize(input);

    [Fact]
    public void Heading_emits_heading_block_with_level()
    {
        var blocks = Tokenize("# Chapter 1\n");
        blocks.Should().ContainSingle().Which
            .Should().Match<MarkdownBlock>(b => b.Type == BlockType.Heading && b.HeadingLevel == 1 && b.Text == "Chapter 1");
    }

    [Theory]
    [InlineData("## Section", 2)]
    [InlineData("###### Deep", 6)]
    public void Heading_levels_parsed(string line, int level)
    {
        Tokenize(line).Single().HeadingLevel.Should().Be(level);
    }

    [Fact]
    public void Paragraphs_separated_by_blank_lines_become_body_blocks()
    {
        var blocks = Tokenize("first para\n\nsecond para\n");
        blocks.Should().HaveCount(2);
        blocks[0].Type.Should().Be(BlockType.Body);
        blocks[0].Text.Should().Be("first para");
        blocks[1].Text.Should().Be("second para");
    }

    [Fact]
    public void Blockquote_lines_collapse_to_a_single_quote_block()
    {
        var blocks = Tokenize("> one\n> two\n\nafter\n");
        blocks.Should().HaveCount(2);
        blocks[0].Type.Should().Be(BlockType.Quote);
        blocks[0].Text.Should().Be("one\ntwo");
        blocks[1].Type.Should().Be(BlockType.Body);
    }

    [Fact]
    public void Arabic_heading_is_detected_same_as_english()
    {
        var blocks = Tokenize("# مقدمة\n");
        blocks.Should().ContainSingle().Which.Type.Should().Be(BlockType.Heading);
        blocks[0].Text.Should().Be("مقدمة");
    }
}
```

- [ ] **Step 2: Run, confirm fail**

Run: `dotnet test --filter "FullyQualifiedName~MarkdownBlockTokenizerTests"`
Expected: compile error — `MarkdownBlockTokenizer` missing.

- [ ] **Step 3: Implement the minimal tokenizer for heading/body/quote**

Create `MarkdownBlockTokenizer.cs`:

```csharp
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

            // body: collect until blank line or structural boundary
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
```

- [ ] **Step 4: Run, verify pass**

Run: `dotnet test --filter "FullyQualifiedName~MarkdownBlockTokenizerTests"`
Expected: all 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/MarkdownBlockTokenizer.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/MarkdownBlockTokenizerTests.cs
git commit -m "feat(ai): block tokenizer handles heading, body, quote"
```

---

### Task 7: Tokenizer — fenced code blocks

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/MarkdownBlockTokenizer.cs`
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/MarkdownBlockTokenizerTests.cs`

- [ ] **Step 1: Append failing tests**

```csharp
[Fact]
public void Fenced_code_block_with_language_is_atomic()
{
    var md = "```python\nx = 1\nprint(x)\n```\n";
    var blocks = Tokenize(md);
    blocks.Should().ContainSingle().Which.Should().Match<MarkdownBlock>(b =>
        b.Type == BlockType.Code && b.CodeLanguage == "python" && b.Text == "x = 1\nprint(x)");
}

[Fact]
public void Fenced_code_without_language_is_still_code()
{
    var blocks = Tokenize("```\nhello\n```\n");
    blocks.Single().Type.Should().Be(BlockType.Code);
    blocks.Single().CodeLanguage.Should().BeNull();
}

[Fact]
public void Body_before_code_fence_is_its_own_block()
{
    var md = "intro\n\n```\ncode\n```\n";
    var blocks = Tokenize(md);
    blocks.Should().HaveCount(2);
    blocks[0].Type.Should().Be(BlockType.Body);
    blocks[1].Type.Should().Be(BlockType.Code);
}

[Fact]
public void Unterminated_code_fence_is_treated_as_code_to_end_of_input()
{
    var md = "```\nleft open\n";
    var blocks = Tokenize(md);
    blocks.Should().ContainSingle().Which.Type.Should().Be(BlockType.Code);
}
```

- [ ] **Step 2: Run, confirm fail**

Run: `dotnet test --filter "FullyQualifiedName~MarkdownBlockTokenizerTests"`
Expected: 4 new failing assertions.

- [ ] **Step 3: Implement code-fence branch**

Add a compiled regex inside `MarkdownBlockTokenizer`:

```csharp
private static readonly Regex CodeFenceRegex = new(@"^```(\w*)\s*$", RegexOptions.Compiled);
```

Inside the main loop, **before** the heading branch, add:

```csharp
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
```

- [ ] **Step 4: Verify all tokenizer tests pass**

Run: `dotnet test --filter "FullyQualifiedName~MarkdownBlockTokenizerTests"`
Expected: all pass (9 tests).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/MarkdownBlockTokenizer.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/MarkdownBlockTokenizerTests.cs
git commit -m "feat(ai): tokenizer handles fenced code blocks atomically"
```

---

### Task 8: Tokenizer — math blocks (`$$` and `\begin{equation}`)

**Files:**
- Modify: `MarkdownBlockTokenizer.cs`
- Modify: `MarkdownBlockTokenizerTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
[Fact]
public void Dollar_dollar_math_block_is_atomic()
{
    var md = "$$\na = b + c\n$$\n";
    var blocks = Tokenize(md);
    blocks.Should().ContainSingle().Which.Type.Should().Be(BlockType.Math);
    blocks.Single().Text.Should().Be("a = b + c");
}

[Fact]
public void Begin_equation_math_block_is_atomic()
{
    var md = "\\begin{equation}\nE = mc^2\n\\end{equation}\n";
    var blocks = Tokenize(md);
    blocks.Single().Type.Should().Be(BlockType.Math);
}

[Fact]
public void Adjacent_math_blocks_separated_only_by_whitespace_merge()
{
    var md = "$$\na = 1\n$$\n\n$$\nb = 2\n$$\n";
    var blocks = Tokenize(md);
    blocks.Should().ContainSingle().Which.Type.Should().Be(BlockType.Math);
    blocks.Single().Text.Should().Contain("a = 1").And.Contain("b = 2");
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement math branch**

Add regexes:

```csharp
private static readonly Regex MathDisplayRegex = new(@"^\s*\$\$\s*$", RegexOptions.Compiled);
private static readonly Regex BeginEquationRegex = new(@"^\s*\\begin\{equation\}\s*$", RegexOptions.Compiled);
private static readonly Regex EndEquationRegex = new(@"^\s*\\end\{equation\}\s*$", RegexOptions.Compiled);
```

Add a helper to collect one math block and a post-pass that merges adjacent math blocks:

```csharp
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
```

Wire it before the code-fence branch inside the main loop:

```csharp
var math = TryReadMath(lines, ref i);
if (math is not null) { blocks.Add(math); continue; }
```

After the main loop, add a post-pass that merges adjacent math blocks:

```csharp
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
```

- [ ] **Step 4: Verify all tokenizer tests pass**

Run: `dotnet test --filter "FullyQualifiedName~MarkdownBlockTokenizerTests"`
Expected: 12 tests pass.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/MarkdownBlockTokenizer.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/MarkdownBlockTokenizerTests.cs
git commit -m "feat(ai): tokenizer handles math blocks with merging"
```

---

### Task 9: Tokenizer — tables and lists

**Files:**
- Modify: `MarkdownBlockTokenizer.cs`
- Modify: `MarkdownBlockTokenizerTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
[Fact]
public void Pipe_table_with_separator_row_is_atomic()
{
    var md = "| a | b |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |\n";
    var blocks = Tokenize(md);
    blocks.Should().ContainSingle().Which.Type.Should().Be(BlockType.Table);
    blocks.Single().Text.Should().Contain("1 | 2").And.Contain("3 | 4");
}

[Fact]
public void Pipe_rows_without_separator_are_body_not_table()
{
    // No | --- | separator means it's just pipe-separated text, not a markdown table.
    var md = "| stray | line |\nfollowed by prose\n";
    var blocks = Tokenize(md);
    blocks.Single().Type.Should().Be(BlockType.Body);
}

[Fact]
public void Hyphen_list_collapses_to_list_block()
{
    var md = "- one\n- two\n- three\n";
    var blocks = Tokenize(md);
    blocks.Single().Type.Should().Be(BlockType.List);
    blocks.Single().Text.Should().Contain("- one").And.Contain("- three");
}

[Fact]
public void Numbered_list_also_recognized()
{
    var md = "1. first\n2. second\n";
    Tokenize(md).Single().Type.Should().Be(BlockType.List);
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement table and list branches**

Add regexes:

```csharp
private static readonly Regex TableRowRegex = new(@"^\|.+\|\s*$", RegexOptions.Compiled);
private static readonly Regex TableSeparatorRegex = new(@"^\|\s*:?-{1,}:?\s*(\|\s*:?-{1,}:?\s*)+\|\s*$", RegexOptions.Compiled);
private static readonly Regex ListRegex = new(@"^\s*([-*+]|\d+\.)\s+\S", RegexOptions.Compiled);
```

In the main loop (after code-fence, before heading), add:

```csharp
// Table: a pipe row immediately followed by a separator row.
if (TableRowRegex.IsMatch(line) && i + 1 < lines.Length && TableSeparatorRegex.IsMatch(lines[i + 1]))
{
    var buf = new List<string> { line, lines[i + 1] };
    i += 2;
    while (i < lines.Length && TableRowRegex.IsMatch(lines[i]))
    {
        buf.Add(lines[i]); i++;
    }
    blocks.Add(new MarkdownBlock(BlockType.Table, string.Join('\n', buf)));
    continue;
}

if (ListRegex.IsMatch(line))
{
    var buf = new List<string> { line };
    i++;
    while (i < lines.Length && lines[i].Length > 0
           && (ListRegex.IsMatch(lines[i]) || lines[i].StartsWith(' ') || lines[i].StartsWith('\t')))
    {
        buf.Add(lines[i]); i++;
    }
    blocks.Add(new MarkdownBlock(BlockType.List, string.Join('\n', buf)));
    continue;
}
```

Note: list block consumes hanging continuation lines (indented). Stop on blank line (outer loop's guard `lines[i].Length > 0` covers it from the next iteration).

Also: inside the `body` branch, guard against lines that would match list/table so multi-line body doesn't swallow them:

```csharp
while (i < lines.Length && lines[i].Length > 0
       && !HeadingRegex.IsMatch(lines[i])
       && !QuoteRegex.IsMatch(lines[i])
       && !CodeFenceRegex.IsMatch(lines[i])
       && !MathDisplayRegex.IsMatch(lines[i])
       && !BeginEquationRegex.IsMatch(lines[i])
       && !ListRegex.IsMatch(lines[i])
       && !(TableRowRegex.IsMatch(lines[i]) && i + 1 < lines.Length && TableSeparatorRegex.IsMatch(lines[i + 1])))
```

- [ ] **Step 4: Verify tokenizer tests pass**

Run: `dotnet test --filter "FullyQualifiedName~MarkdownBlockTokenizerTests"`
Expected: 16 tests pass.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/MarkdownBlockTokenizer.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/MarkdownBlockTokenizerTests.cs
git commit -m "feat(ai): tokenizer handles tables and lists"
```

---

### Task 10: Heading stack + breadcrumb

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/HeadingStack.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/HeadingStackTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Ingestion.Structured;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class HeadingStackTests
{
    [Fact]
    public void Empty_stack_has_empty_breadcrumb()
    {
        new HeadingStack().Breadcrumb.Should().BeEmpty();
    }

    [Fact]
    public void Push_sets_breadcrumb_to_single_entry()
    {
        var s = new HeadingStack();
        s.Push(1, "Chapter 1");
        s.Breadcrumb.Should().Be("Chapter 1");
    }

    [Fact]
    public void Pushing_deeper_level_appends()
    {
        var s = new HeadingStack();
        s.Push(1, "Chapter 1");
        s.Push(2, "Section 1.2");
        s.Breadcrumb.Should().Be("Chapter 1 > Section 1.2");
    }

    [Fact]
    public void Pushing_same_level_replaces_tail()
    {
        var s = new HeadingStack();
        s.Push(1, "Chapter 1");
        s.Push(2, "Section 1.1");
        s.Push(2, "Section 1.2");
        s.Breadcrumb.Should().Be("Chapter 1 > Section 1.2");
    }

    [Fact]
    public void Pushing_shallower_level_pops_and_replaces()
    {
        var s = new HeadingStack();
        s.Push(1, "Chapter 1");
        s.Push(2, "Section 1.1");
        s.Push(3, "Sub 1.1.1");
        s.Push(2, "Section 1.2");
        s.Breadcrumb.Should().Be("Chapter 1 > Section 1.2");
    }

    [Fact]
    public void Reset_clears_everything()
    {
        var s = new HeadingStack();
        s.Push(1, "A");
        s.Reset();
        s.Breadcrumb.Should().BeEmpty();
    }

    [Fact]
    public void Section_title_returns_deepest_heading()
    {
        var s = new HeadingStack();
        s.Push(1, "Ch1");
        s.Push(2, "Sec1");
        s.DeepestHeading.Should().Be("Sec1");
    }

    [Fact]
    public void Arabic_headings_are_supported_unchanged()
    {
        var s = new HeadingStack();
        s.Push(1, "مقدمة");
        s.Push(2, "البداية");
        s.Breadcrumb.Should().Be("مقدمة > البداية");
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

```csharp
namespace Starter.Module.AI.Infrastructure.Ingestion.Structured;

internal sealed class HeadingStack
{
    private readonly List<(int Level, string Text)> _frames = new();

    public string Breadcrumb => _frames.Count == 0 ? string.Empty : string.Join(" > ", _frames.Select(f => f.Text));
    public string? DeepestHeading => _frames.Count == 0 ? null : _frames[^1].Text;

    public void Push(int level, string text)
    {
        while (_frames.Count > 0 && _frames[^1].Level >= level)
            _frames.RemoveAt(_frames.Count - 1);
        _frames.Add((level, text));
    }

    public void Reset() => _frames.Clear();
}
```

- [ ] **Step 4: Verify tests pass**

Run: `dotnet test --filter "FullyQualifiedName~HeadingStackTests"`
Expected: 8 pass.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/HeadingStack.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/HeadingStackTests.cs
git commit -m "feat(ai): heading stack with breadcrumb"
```

---

### Task 11: Arabic-aware sentence splitter

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/SentenceSplitter.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/SentenceSplitterTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Ingestion.Structured;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class SentenceSplitterTests
{
    [Fact]
    public void English_sentences_split_on_period_bang_question()
    {
        var parts = SentenceSplitter.Split("Hello world. This works! Does it?");
        parts.Should().HaveCount(3);
    }

    [Fact]
    public void Arabic_sentences_split_on_question_and_comma_markers()
    {
        var parts = SentenceSplitter.Split("ما هو الحل؟ الحل بسيط، لكنه فعّال.");
        parts.Should().HaveCountGreaterThan(1);
        parts[0].Should().Contain("ما هو الحل");
    }

    [Fact]
    public void Single_sentence_returns_one_part()
    {
        SentenceSplitter.Split("just one").Should().ContainSingle();
    }

    [Fact]
    public void Whitespace_only_input_returns_empty()
    {
        SentenceSplitter.Split("   \n").Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement**

```csharp
using System.Text.RegularExpressions;

namespace Starter.Module.AI.Infrastructure.Ingestion.Structured;

internal static class SentenceSplitter
{
    // .!? plus Arabic question mark (؟) and Arabic comma (،) as sentence-ending candidates.
    private static readonly Regex Terminators = new(@"(?<=[\.!\?\u061F\u060C])\s+", RegexOptions.Compiled);

    public static IReadOnlyList<string> Split(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        var parts = Terminators.Split(text.Trim());
        return parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
    }
}
```

- [ ] **Step 4: Verify tests pass**

Run: `dotnet test --filter "FullyQualifiedName~SentenceSplitterTests"`
Expected: 4 pass.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/SentenceSplitter.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/SentenceSplitterTests.cs
git commit -m "feat(ai): Arabic-aware sentence splitter"
```

---

### Task 12: `StructuredMarkdownChunker` — simple end-to-end (no fallbacks)

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/StructuredMarkdownChunker.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/StructuredMarkdownChunkerTests.cs`

- [ ] **Step 1: Failing tests (keep small-doc cases here; oversize scenarios go in Task 13)**

```csharp
using FluentAssertions;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Ingestion.Structured;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class StructuredMarkdownChunkerTests
{
    private static StructuredMarkdownChunker NewChunker() =>
        new(new TokenCounter(), new MarkdownBlockTokenizer());

    private static ExtractedDocument OneMarkdownPage(string md) =>
        new([new ExtractedPage(1, md)], UsedOcr: false);

    private static ChunkingOptions Opts(int child = 512, int parent = 1536, int overlap = 50)
        => new(ParentTokens: parent, ChildTokens: child, ChildOverlapTokens: overlap);

    [Fact]
    public void Single_heading_followed_by_body_produces_body_chunk_with_breadcrumb()
    {
        var md = "# Chapter 1\n\nThis is the body of chapter 1.\n";
        var chunks = NewChunker().Chunk(OneMarkdownPage(md), Opts());
        chunks.Children.Should().ContainSingle();
        var child = chunks.Children[0];
        child.ChunkType.Should().Be(ChunkType.Body);
        child.SectionTitle.Should().Be("Chapter 1");
        child.Content.Should().Contain("This is the body");
    }

    [Fact]
    public void Code_block_emits_code_chunk_with_language_preserved_in_text()
    {
        var md = "# A\n\n```python\nx = 1\n```\n";
        var chunks = NewChunker().Chunk(OneMarkdownPage(md), Opts());
        var code = chunks.Children.Should().ContainSingle().Subject;
        code.ChunkType.Should().Be(ChunkType.Code);
    }

    [Fact]
    public void Nested_headings_produce_breadcrumb_path_in_section_title()
    {
        var md = "# Ch1\n\n## Sec A\n\nbody under A\n\n## Sec B\n\nbody under B\n";
        var chunks = NewChunker().Chunk(OneMarkdownPage(md), Opts());
        chunks.Children.Should().HaveCount(2);
        chunks.Children[0].SectionTitle.Should().Be("Sec A");
        chunks.Children[1].SectionTitle.Should().Be("Sec B");
    }

    [Fact]
    public void Empty_markdown_produces_no_chunks()
    {
        var chunks = NewChunker().Chunk(OneMarkdownPage(""), Opts());
        chunks.Children.Should().BeEmpty();
        chunks.Parents.Should().BeEmpty();
    }

    [Fact]
    public void Parent_wraps_adjacent_children()
    {
        var md = "# A\n\nfirst paragraph of body.\n\nsecond paragraph of body.\n";
        var chunks = NewChunker().Chunk(OneMarkdownPage(md), Opts(child: 32, parent: 128, overlap: 0));
        chunks.Parents.Should().HaveCountGreaterOrEqualTo(1);
        chunks.Children.Should().OnlyContain(c => c.ParentIndex.HasValue);
    }

    [Fact]
    public void Arabic_heading_and_body_preserve_order()
    {
        var md = "# مقدمة\n\nهذا هو النص العربي.\n";
        var chunks = NewChunker().Chunk(OneMarkdownPage(md), Opts());
        chunks.Children.Should().ContainSingle().Which.SectionTitle.Should().Be("مقدمة");
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement the core chunker**

```csharp
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
            var parentStart = parents.Count;

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
                    // heading does not stand alone — skip emission; next non-heading block inherits SectionTitle via the stack.
                    continue;
                }

                var tokens = counter.Count(block.Text);
                var child = new ChunkDraft(
                    Index: children.Count + parentBuf.Count,
                    Content: block.Text,
                    TokenCount: tokens,
                    ParentIndex: null, // assigned in FlushParent
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
```

- [ ] **Step 4: Register a minimal DI binding so test project compiles**

No DI change needed yet — the class is constructed directly in tests via `new(...)`.

- [ ] **Step 5: Run structural chunker tests**

Run: `dotnet test --filter "FullyQualifiedName~StructuredMarkdownChunkerTests"`
Expected: 6 pass.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/StructuredMarkdownChunker.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/StructuredMarkdownChunkerTests.cs
git commit -m "feat(ai): StructuredMarkdownChunker core — headings, blocks, parents"
```

---

### Task 13: Oversize body fallback to token-window

**Files:**
- Modify: `StructuredMarkdownChunker.cs`
- Modify: `StructuredMarkdownChunkerTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
[Fact]
public void Oversize_body_block_is_split_by_sliding_window()
{
    var longBody = string.Join(" ", Enumerable.Repeat("word", 2000));
    var md = "# H\n\n" + longBody + "\n";
    var chunks = NewChunker().Chunk(OneMarkdownPage(md), Opts(child: 64, parent: 256, overlap: 8));
    chunks.Children.Should().HaveCountGreaterThan(1);
    chunks.Children.Should().OnlyContain(c => c.ChunkType == ChunkType.Body);
    chunks.Children.Should().OnlyContain(c => c.SectionTitle == "H");
}

[Fact]
public void Oversize_code_block_is_split_by_blank_line_groups()
{
    var codeBody = "block1-a\nblock1-b\n\nblock2-a\nblock2-b\n";
    var fence = "```text\n" + codeBody + "```\n";
    var chunks = NewChunker().Chunk(OneMarkdownPage("# C\n\n" + fence), Opts(child: 4, parent: 64, overlap: 0));
    chunks.Children.Should().HaveCountGreaterThan(1);
    chunks.Children.Should().OnlyContain(c => c.ChunkType == ChunkType.Code);
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement splitting in the chunker**

Inside the `foreach (var block in blocks)` loop, before enqueueing into `parentBuf`, replace the single-child emission with a helper that splits oversized blocks:

```csharp
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
        // Split on blank-line groups.
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

    // Body / Quote / List / Math / Table: sliding window over the raw text.
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
```

Call it inside the block loop:

```csharp
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
```

- [ ] **Step 4: Verify all structured chunker tests pass**

Run: `dotnet test --filter "FullyQualifiedName~StructuredMarkdownChunkerTests"`
Expected: 8 pass.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/StructuredMarkdownChunker.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/StructuredMarkdownChunkerTests.cs
git commit -m "feat(ai): split oversize blocks with token-window fallback"
```

---

### Task 14: Table row-group splitting (header replication)

**Files:**
- Modify: `StructuredMarkdownChunker.cs`
- Modify: `StructuredMarkdownChunkerTests.cs`

- [ ] **Step 1: Failing test**

```csharp
[Fact]
public void Oversize_table_is_split_with_header_replicated()
{
    var header = "| col1 | col2 |\n|---|---|\n";
    var body = string.Concat(Enumerable.Range(1, 200).Select(i => $"| r{i}a | r{i}b |\n"));
    var md = "# T\n\n" + header + body;
    var chunks = NewChunker().Chunk(OneMarkdownPage(md), Opts(child: 64, parent: 256, overlap: 0));
    var tables = chunks.Children.Where(c => c.ChunkType == ChunkType.Table).ToList();
    tables.Should().HaveCountGreaterThan(1);
    tables.Should().OnlyContain(t => t.Content.Contains("col1") && t.Content.Contains("col2"));
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement table-specific branch inside `Split(...)`**

Before the generic body slide, add:

```csharp
if (block.Type == BlockType.Table)
{
    var lines = block.Text.Split('\n');
    if (lines.Length < 2)
    {
        yield return (block.Text, tokens, ChunkType.Table);
        yield break;
    }
    var header = string.Join('\n', lines.Take(2)); // title row + separator
    var dataRows = lines.Skip(2).ToList();
    var buf = new List<string>();
    var bufTokens = counter.Count(header);
    foreach (var row in dataRows)
    {
        var rt = counter.Count(row);
        if (bufTokens + rt > options.ChildTokens && buf.Count > 0)
        {
            yield return (header + "\n" + string.Join('\n', buf), bufTokens, ChunkType.Table);
            buf.Clear();
            bufTokens = counter.Count(header);
        }
        buf.Add(row);
        bufTokens += rt;
    }
    if (buf.Count > 0)
        yield return (header + "\n" + string.Join('\n', buf), bufTokens, ChunkType.Table);
    yield break;
}
```

Place it before the code branch so tables take precedence.

- [ ] **Step 4: Verify tests pass**

Run: `dotnet test --filter "FullyQualifiedName~StructuredMarkdownChunkerTests"`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/StructuredMarkdownChunker.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/StructuredMarkdownChunkerTests.cs
git commit -m "feat(ai): oversize tables split row-group with header replication"
```

---

### Task 15: HTML → markdown converter

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Starter.Module.AI.csproj` — add `ReverseMarkdown` package
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/HtmlToMarkdownConverter.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/HtmlToMarkdownConverterTests.cs`

- [ ] **Step 1: Confirm latest stable version**

Run: `dotnet list boilerplateBE/Starter.sln package --outdated | grep -i reverse` (expect no output; first install). Otherwise pin to current (e.g., `ReverseMarkdown 4.6.0`).

- [ ] **Step 2: Failing tests**

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Ingestion.Structured;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class HtmlToMarkdownConverterTests
{
    private static HtmlToMarkdownConverter New() => new();

    [Fact]
    public void H1_becomes_hash_heading()
    {
        New().Convert("<h1>Chapter 1</h1>").Should().Contain("# Chapter 1");
    }

    [Fact]
    public void Paragraphs_are_preserved_with_blank_lines()
    {
        var md = New().Convert("<p>first</p><p>second</p>");
        md.Should().Contain("first").And.Contain("second");
    }

    [Fact]
    public void Inline_code_is_backticked()
    {
        New().Convert("Call <code>foo()</code>.").Should().Contain("`foo()`");
    }

    [Fact]
    public void Lists_become_markdown_lists()
    {
        var md = New().Convert("<ul><li>a</li><li>b</li></ul>");
        md.Should().MatchRegex(@"[-*]\s+a").And.MatchRegex(@"[-*]\s+b");
    }

    [Fact]
    public void Table_is_rendered_as_markdown_table()
    {
        var md = New().Convert("<table><tr><th>x</th><th>y</th></tr><tr><td>1</td><td>2</td></tr></table>");
        md.Should().Contain("| x | y |").And.Contain("| 1 | 2 |");
    }
}
```

- [ ] **Step 3: Run, confirm fail**

- [ ] **Step 4: Add the package and implementation**

`Starter.Module.AI.csproj` — add inside an `<ItemGroup>` containing `PackageReference`s:

```xml
<PackageReference Include="ReverseMarkdown" Version="4.6.0" />
```

`HtmlToMarkdownConverter.cs`:

```csharp
using ReverseMarkdown;

namespace Starter.Module.AI.Infrastructure.Ingestion.Structured;

internal sealed class HtmlToMarkdownConverter
{
    private readonly Converter _converter = new(new Config
    {
        GithubFlavored = true, // enables tables, fenced code
        RemoveComments = true,
        SmartHrefHandling = true,
    });

    public string Convert(string html) => string.IsNullOrWhiteSpace(html) ? string.Empty : _converter.Convert(html);
}
```

- [ ] **Step 5: Verify tests pass**

Run: `dotnet test --filter "FullyQualifiedName~HtmlToMarkdownConverterTests"`
Expected: 5 pass.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Starter.Module.AI.csproj \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/HtmlToMarkdownConverter.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/HtmlToMarkdownConverterTests.cs
git commit -m "feat(ai): HTML→markdown converter via ReverseMarkdown"
```

---

### Task 16: `ChunkerRouter` — content-type + heuristic dispatch

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/ChunkerRouter.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/ChunkerRouterTests.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/IDocumentChunker.cs` (add `ContentType` to `ChunkingOptions`)
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Consumers/ProcessDocumentConsumer.cs` (pass `doc.ContentType`)

- [ ] **Step 1: Failing tests**

```csharp
using FluentAssertions;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Ingestion.Structured;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class ChunkerRouterTests
{
    private static ChunkerRouter NewRouter()
    {
        var tc = new TokenCounter();
        return new ChunkerRouter(
            structural: new StructuredMarkdownChunker(tc, new MarkdownBlockTokenizer()),
            fallback: new HierarchicalDocumentChunker(tc),
            htmlConverter: new HtmlToMarkdownConverter());
    }

    private static ExtractedDocument Doc(string text) => new([new ExtractedPage(1, text)], false);
    private static ChunkingOptions Opts(string contentType) =>
        new(ParentTokens: 1536, ChildTokens: 512, ChildOverlapTokens: 50) { ContentType = contentType };

    [Fact]
    public void Markdown_content_type_uses_structural()
    {
        var chunks = NewRouter().Chunk(Doc("# H\n\nbody\n"), Opts("text/markdown"));
        chunks.Children.Single().SectionTitle.Should().Be("H");
    }

    [Fact]
    public void Plain_text_without_heading_hints_uses_fallback()
    {
        var chunks = NewRouter().Chunk(Doc("some plain text here."), Opts("text/plain"));
        chunks.Children.Single().SectionTitle.Should().BeNull();
    }

    [Fact]
    public void Plain_text_with_heading_heuristic_uses_structural()
    {
        var md = "# Title\n\n## Sub\n\nbody\n";
        var chunks = NewRouter().Chunk(Doc(md), Opts("text/plain"));
        chunks.Children.Single().SectionTitle.Should().Be("Sub");
    }

    [Fact]
    public void Html_is_converted_then_chunked_structurally()
    {
        var html = "<h1>Doc</h1><p>body text.</p>";
        var chunks = NewRouter().Chunk(Doc(html), Opts("text/html"));
        chunks.Children.Single().SectionTitle.Should().Be("Doc");
    }

    [Fact]
    public void Pdf_content_type_uses_fallback()
    {
        var chunks = NewRouter().Chunk(Doc("raw PDF text without markdown."), Opts("application/pdf"));
        chunks.Children.Should().NotBeEmpty();
        chunks.Children.Should().OnlyContain(c => c.SectionTitle == null);
    }
}
```

- [ ] **Step 2: Extend `ChunkingOptions` to include `ContentType`**

Replace `ChunkingOptions` in `IDocumentChunker.cs`:

```csharp
public sealed record ChunkingOptions(
    int ParentTokens,
    int ChildTokens,
    int ChildOverlapTokens)
{
    public string? ContentType { get; init; }
}
```

- [ ] **Step 3: Run, confirm fail**

Expected: compile error — `ChunkerRouter` missing.

- [ ] **Step 4: Implement `ChunkerRouter`**

```csharp
using Starter.Module.AI.Application.Services.Ingestion;

namespace Starter.Module.AI.Infrastructure.Ingestion.Structured;

internal sealed class ChunkerRouter(
    StructuredMarkdownChunker structural,
    HierarchicalDocumentChunker fallback,
    HtmlToMarkdownConverter htmlConverter) : IDocumentChunker
{
    public HierarchicalChunks Chunk(ExtractedDocument document, ChunkingOptions options)
    {
        var ct = Normalize(options.ContentType);

        if (ct == "text/html")
        {
            var converted = new ExtractedDocument(
                document.Pages.Select(p => new ExtractedPage(p.PageNumber, htmlConverter.Convert(p.Text), p.SectionTitle)).ToList(),
                document.UsedOcr);
            return structural.Chunk(converted, options);
        }

        if (ct == "text/markdown") return structural.Chunk(document, options);

        if (ct == "text/plain" && LooksLikeMarkdown(document)) return structural.Chunk(document, options);

        return fallback.Chunk(document, options);
    }

    private static string? Normalize(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return null;
        var semi = contentType.IndexOf(';');
        return (semi >= 0 ? contentType[..semi] : contentType).Trim().ToLowerInvariant();
    }

    private static bool LooksLikeMarkdown(ExtractedDocument doc)
    {
        foreach (var page in doc.Pages)
        {
            if (string.IsNullOrWhiteSpace(page.Text)) continue;
            var nonEmpty = page.Text.Split('\n').Where(l => l.Length > 0).Take(4).ToArray();
            if (nonEmpty.Length == 0) continue;
            if (nonEmpty.Count(l => l.TrimStart().StartsWith('#')) >= 1) return true;
            break;
        }
        return false;
    }
}
```

- [ ] **Step 5: Verify tests pass**

Run: `dotnet test --filter "FullyQualifiedName~ChunkerRouterTests"`
Expected: 5 pass.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/IDocumentChunker.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/ChunkerRouter.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/ChunkerRouterTests.cs
git commit -m "feat(ai): ChunkerRouter dispatches by content type with markdown heuristic"
```

---

### Task 17: Wire router into DI + consumer

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Consumers/ProcessDocumentConsumer.cs`

- [ ] **Step 1: Register new services in `AIModule.cs`**

Replace line 77 (`services.AddSingleton<IDocumentChunker, HierarchicalDocumentChunker>();`) with:

```csharp
services.AddSingleton<Infrastructure.Ingestion.HierarchicalDocumentChunker>();
services.AddSingleton<Infrastructure.Ingestion.Structured.MarkdownBlockTokenizer>();
services.AddSingleton<Infrastructure.Ingestion.Structured.HtmlToMarkdownConverter>();
services.AddSingleton<Infrastructure.Ingestion.Structured.StructuredMarkdownChunker>();
services.AddSingleton<IDocumentChunker, Infrastructure.Ingestion.Structured.ChunkerRouter>();
```

- [ ] **Step 2: Pass `ContentType` through the consumer**

In `ProcessDocumentConsumer.cs`, where `chunker.Chunk(...)` is called (around line 173), change:

```csharp
var chunks = chunker.Chunk(extracted, new ChunkingOptions(
    ParentTokens: ragOptions.ParentChunkSize,
    ChildTokens: ragOptions.ChunkSize,
    ChildOverlapTokens: ragOptions.ChunkOverlap));
```

to:

```csharp
var chunks = chunker.Chunk(extracted, new ChunkingOptions(
    ParentTokens: ragOptions.ParentChunkSize,
    ChildTokens: ragOptions.ChunkSize,
    ChildOverlapTokens: ragOptions.ChunkOverlap) { ContentType = doc.ContentType });
```

- [ ] **Step 3: Verify build + run all AI tests**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~Ai"`
Expected: all pre-existing tests still pass, new structural tests pass.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Consumers/ProcessDocumentConsumer.cs
git commit -m "feat(ai): wire ChunkerRouter as default IDocumentChunker"
```

---

### Task 18: Persist `ChunkType` + breadcrumb-normalized content during ingestion

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Consumers/ProcessDocumentConsumer.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Consumers/ProcessDocumentConsumerTests.cs` (extend if present; otherwise add new targeted assertion)

- [ ] **Step 1: Failing assertion — consumer writes `ChunkType` and breadcrumb**

Add a test case `Structural_markdown_chunks_persist_chunk_type_and_breadcrumb_in_normalized_content` alongside existing consumer tests. Mirror the existing arrangement (mock storage + extractor returning `text/markdown` fixture `# H1\n\n## H2\n\n```py\nx=1\n```\n`). Assert:
- one chunk has `ChunkType == ChunkType.Code`
- that chunk's `NormalizedContent` starts with `H1 > H2\n` (breadcrumb then newline)
- chunk's `SectionTitle == "H2"`

(If the existing consumer test scaffold lives outside scope because it uses heavy mocks, substitute a smaller integration test that constructs the consumer via `IServiceScopeFactory` with an in-memory `AiDbContext` and stub `IVectorStore`. Use `ProcessDocumentConsumerTests.cs` as the reference for wiring.)

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Update consumer to carry `ChunkType` + prepend breadcrumb into `NormalizedContent`**

In `ProcessDocumentConsumer.cs`, inside the non-clone path (around lines 200–260), change:

- parent creation — pass `chunkType: p.ChunkType`:

```csharp
var parentEntities = chunks.Parents.Select(p => AiDocumentChunk.Create(
    documentId: doc.Id,
    chunkLevel: "parent",
    content: p.Content,
    chunkIndex: p.Index,
    tokenCount: p.TokenCount,
    qdrantPointId: Guid.NewGuid(),
    parentChunkId: null,
    sectionTitle: p.SectionTitle,
    pageNumber: p.PageNumber,
    chunkType: p.ChunkType)).ToList();
```

- before `SetNormalizedContent`, prepend the section title (breadcrumb) once, single newline separator:

```csharp
string BuildNormalized(string? breadcrumb, string content)
{
    var body = ragOptions.ApplyArabicNormalization
        ? ArabicTextNormalizer.Normalize(content, arOpts)
        : content;
    return string.IsNullOrWhiteSpace(breadcrumb) ? body : breadcrumb + "\n" + body;
}

foreach (var p in parentEntities)
    p.SetNormalizedContent(BuildNormalized(p.SectionTitle, p.Content));
```

- child path: pass `chunkType: draft.ChunkType` to `AiDocumentChunk.Create`, and replace the current `SetNormalizedContent` call with `BuildNormalized(...)` as above.

- update both `VectorPayload` constructions (clone + normal path) to pass `ChunkType: <source>.ChunkType` as the new last positional argument.

  - Clone path: use `child.ChunkType` from the cloned `AiDocumentChunk` (which now has the column).
  - Normal path: use `draft.ChunkType`.

- [ ] **Step 4: Verify tests**

Run: `dotnet test --filter "FullyQualifiedName~Ai"`
Expected: all pass (old + new). Old tests that asserted `NormalizedContent == ArabicTextNormalizer.Normalize(content)` must tolerate an optional breadcrumb prefix — adjust the existing assertions to use `.EndsWith(expected)` or add the breadcrumb explicitly.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Consumers/ProcessDocumentConsumer.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Consumers/ProcessDocumentConsumerTests.cs
git commit -m "feat(ai): persist ChunkType + prepend breadcrumb to NormalizedContent"
```

---

### Task 19: Surface `ChunkType` into `RetrievedChunk` + Qdrant payload

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RetrievedChunk.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/QdrantVectorStore.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/PostgresKeywordSearchService.cs`

- [ ] **Step 1: Extend `RetrievedChunk` with `ChunkType`**

Add the new required property as the last positional parameter with a default:

```csharp
using Starter.Module.AI.Domain.Enums;

public sealed record RetrievedChunk(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    string Content,
    string? SectionTitle,
    int? PageNumber,
    string ChunkLevel,
    decimal SemanticScore,
    decimal KeywordScore,
    decimal HybridScore,
    Guid? ParentChunkId,
    int ChunkIndex,
    ChunkType ChunkType = ChunkType.Body);
```

- [ ] **Step 2: Populate `ChunkType` in every construction site**

- `QdrantVectorStore` SearchAsync — read `ChunkType` from the Qdrant payload field (add field write during upsert in Task 4 already; mirror on read):

```csharp
ChunkType: payload.TryGetValue("chunk_type", out var ctRaw) && int.TryParse(ctRaw.IntegerValue.ToString(), out var ctInt)
    ? (ChunkType)ctInt
    : ChunkType.Body
```

- `PostgresKeywordSearchService` SELECT: include `"ChunkType"` and map to the `RetrievedChunk`.

- `RagRetrievalService`: when constructing parents/siblings for the returned `RetrievedContext`, pull `ChunkType` from the `AiDocumentChunk` row.

- [ ] **Step 3: Verify compile + existing tests pass**

Run: `dotnet test --filter "FullyQualifiedName~Ai"`
Expected: all pass.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Retrieval/RetrievedChunk.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/RagRetrievalService.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/QdrantVectorStore.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/PostgresKeywordSearchService.cs
git commit -m "feat(ai): carry ChunkType through retrieval pipeline"
```

---

### Task 20: `ContextPromptBuilder` — wrap code/math

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/ContextPromptBuilder.cs`
- Test: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextPromptBuilderTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
using FluentAssertions;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public class ContextPromptBuilderTests
{
    private static RetrievedChunk Make(string content, ChunkType type) => new(
        ChunkId: Guid.NewGuid(), DocumentId: Guid.NewGuid(), DocumentName: "d", Content: content,
        SectionTitle: "S", PageNumber: 1, ChunkLevel: "child",
        SemanticScore: 0, KeywordScore: 0, HybridScore: 1, ParentChunkId: null, ChunkIndex: 0,
        ChunkType: type);

    [Fact]
    public void Code_chunk_is_wrapped_in_triple_backticks()
    {
        var ctx = new RetrievedContext([Make("x = 1", ChunkType.Code)], [], 10, false, [], []);
        var prompt = ContextPromptBuilder.Build("sys", ctx);
        prompt.Should().Contain("```\nx = 1\n```");
    }

    [Fact]
    public void Math_chunk_is_wrapped_in_dollar_dollar()
    {
        var ctx = new RetrievedContext([Make("a = b + c", ChunkType.Math)], [], 10, false, [], []);
        var prompt = ContextPromptBuilder.Build("sys", ctx);
        prompt.Should().Contain("$$\na = b + c\n$$");
    }

    [Fact]
    public void Body_chunk_is_unwrapped()
    {
        var ctx = new RetrievedContext([Make("hello", ChunkType.Body)], [], 10, false, [], []);
        var prompt = ContextPromptBuilder.Build("sys", ctx);
        prompt.Should().Contain("hello").And.NotContain("```hello");
    }
}
```

- [ ] **Step 2: Run, confirm fail**

- [ ] **Step 3: Implement wrapping**

In `ContextPromptBuilder.cs`, replace the `sb.AppendLine(child.Content);` line with:

```csharp
switch (child.ChunkType)
{
    case ChunkType.Code:
        sb.AppendLine("```");
        sb.AppendLine(child.Content);
        sb.AppendLine("```");
        break;
    case ChunkType.Math:
        sb.AppendLine("$$");
        sb.AppendLine(child.Content);
        sb.AppendLine("$$");
        break;
    default:
        sb.AppendLine(child.Content);
        break;
}
```

Add `using Starter.Module.AI.Domain.Enums;` at the top.

- [ ] **Step 4: Verify tests pass**

Run: `dotnet test --filter "FullyQualifiedName~ContextPromptBuilderTests"`
Expected: 3 pass. Existing `ChatExecutionRagInjectionTests` must still pass (they use Body chunks).

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Retrieval/ContextPromptBuilder.cs \
        boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/ContextPromptBuilderTests.cs
git commit -m "feat(ai): ContextPromptBuilder wraps code and math chunks"
```

---

### Task 21: Keyword-search integration test for heading-only match

**Files:**
- Modify: `boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/PostgresKeywordSearchServiceTests.cs`

- [ ] **Step 1: Failing test**

```csharp
[Fact]
public async Task Keyword_query_matches_chunk_whose_body_lacks_term_but_breadcrumb_has_it()
{
    // Arrange: seed a chunk where Content does NOT contain the word "Pumps",
    // but NormalizedContent starts with breadcrumb "Chapter 1 > Pumps\n".
    await using var fx = await PostgresTestFixture.StartAsync();
    var tenantId = Guid.NewGuid();
    await fx.SeedChunkAsync(tenantId,
        content: "Rotational energy is transferred to the impeller.",
        normalizedContent: "Chapter 1 > Pumps\nRotational energy is transferred to the impeller.");

    var svc = fx.Service;

    // Act
    var results = await svc.SearchAsync(tenantId, "Pumps", topK: 5, CancellationToken.None);

    // Assert
    results.Should().ContainSingle();
    results.Single().Content.Should().Contain("impeller").And.NotContain("Pumps");
}
```

(Extend `PostgresTestFixture` with a helper to seed a chunk with explicit `NormalizedContent`.)

- [ ] **Step 2: Run, confirm fail if helper doesn't exist**

- [ ] **Step 3: Add helper to `PostgresTestFixture`**

```csharp
public async Task SeedChunkAsync(Guid tenantId, string content, string normalizedContent, string? sectionTitle = null)
{
    var doc = AiDocument.Create(
        tenantId: tenantId, ownerId: Guid.NewGuid(), name: "seed.txt",
        contentType: "text/plain", fileRef: "s3://dummy", sizeBytes: content.Length, contentHash: null);
    Db.AiDocuments.Add(doc);
    var chunk = AiDocumentChunk.Create(
        documentId: doc.Id, chunkLevel: "child", content: content,
        chunkIndex: 0, tokenCount: 1, qdrantPointId: Guid.NewGuid(),
        sectionTitle: sectionTitle);
    chunk.SetNormalizedContent(normalizedContent);
    Db.AiDocumentChunks.Add(chunk);
    await Db.SaveChangesAsync();
}
```

- [ ] **Step 4: Verify**

Run: `dotnet test --filter "FullyQualifiedName~PostgresKeywordSearchServiceTests"`
Expected: all pass, including the new breadcrumb test.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/Retrieval/PostgresKeywordSearchServiceTests.cs
git commit -m "test(ai): breadcrumb in NormalizedContent drives heading-only FTS match"
```

---

### Task 22: End-to-end integration test — markdown document, full pipeline

**Files:**
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/StructuralChunkingPipelineTests.cs`

- [ ] **Step 1: Failing test**

Compose extractor → chunker (via router) → `ContextPromptBuilder` using real classes (no mocks for logic under test, only stubs for IO). Feed an English + Arabic mixed-language markdown fixture including heading, body, code, table, math, and list. Assert:

- block count and types are as expected
- Arabic heading produces breadcrumb `قسم 1 > قسم 1.1` chain
- code chunk rendered in prompt with triple backticks
- math chunk rendered with `$$`
- body chunks carry `SectionTitle` from the deepest heading

```csharp
using FluentAssertions;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Ingestion.Structured;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class StructuralChunkingPipelineTests
{
    [Fact]
    public void Mixed_language_document_is_chunked_and_rendered_correctly()
    {
        var tc = new TokenCounter();
        var router = new ChunkerRouter(
            new StructuredMarkdownChunker(tc, new MarkdownBlockTokenizer()),
            new HierarchicalDocumentChunker(tc),
            new HtmlToMarkdownConverter());

        var md = @"# قسم 1

## قسم 1.1

هذا نص عربي.

```python
x = 1
```

$$
E = mc^2
$$
";
        var extracted = new ExtractedDocument(new[] { new ExtractedPage(1, md) }, UsedOcr: false);
        var chunks = router.Chunk(extracted, new ChunkingOptions(1536, 512, 50) { ContentType = "text/markdown" });

        chunks.Children.Should().HaveCountGreaterOrEqualTo(3);
        chunks.Children.Should().Contain(c => c.ChunkType == ChunkType.Body && c.SectionTitle == "قسم 1.1");
        chunks.Children.Should().Contain(c => c.ChunkType == ChunkType.Code);
        chunks.Children.Should().Contain(c => c.ChunkType == ChunkType.Math);

        // Render into prompt:
        var retrieved = chunks.Children.Select(c => new RetrievedChunk(
            ChunkId: Guid.NewGuid(), DocumentId: Guid.NewGuid(), DocumentName: "doc.md",
            Content: c.Content, SectionTitle: c.SectionTitle, PageNumber: c.PageNumber,
            ChunkLevel: "child", SemanticScore: 0, KeywordScore: 0, HybridScore: 1,
            ParentChunkId: null, ChunkIndex: c.Index, ChunkType: c.ChunkType)).ToList();
        var prompt = ContextPromptBuilder.Build("sys", new RetrievedContext(retrieved, [], 100, false, [], []));

        prompt.Should().Contain("```\nx = 1\n```");
        prompt.Should().Contain("$$\nE = mc^2\n$$");
        prompt.Should().Contain("Section: \"قسم 1.1\"");
    }
}
```

- [ ] **Step 2: Run, confirm pass**

Run: `dotnet test --filter "FullyQualifiedName~StructuralChunkingPipelineTests"`
Expected: 1 test passes (all upstream pieces are in place).

- [ ] **Step 3: Run full AI test suite as regression**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~Ai"`
Expected: all pass (new + pre-existing).

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/Structured/StructuralChunkingPipelineTests.cs
git commit -m "test(ai): end-to-end structural chunking pipeline"
```

---

### Task 23: Settings surface + documentation

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs`
- Modify: `boilerplateBE/src/Starter.Api/appsettings.json`
- Modify: `boilerplateBE/src/Starter.Api/appsettings.Development.json`
- Modify: `README-AI.md` (if exists) or `boilerplateBE/src/modules/Starter.Module.AI/README.md`

- [ ] **Step 1: Add knobs**

Add to `AiRagSettings.cs` (alongside existing chunk knobs):

```csharp
public bool EnableStructuralChunking { get; set; } = true;
public bool IncludeBreadcrumbInFts { get; set; } = true;
```

In `ChunkerRouter`, early-return to the fallback when `EnableStructuralChunking == false`:

Pass `AiRagSettings` through constructor injection, guard at the top of `Chunk(...)`:

```csharp
if (!settings.EnableStructuralChunking) return fallback.Chunk(document, options);
```

In `ProcessDocumentConsumer.BuildNormalized`, skip the breadcrumb prefix when `IncludeBreadcrumbInFts == false`.

- [ ] **Step 2: Add config defaults**

In both `appsettings.json` files, under the `AI:Rag` block:

```json
"EnableStructuralChunking": true,
"IncludeBreadcrumbInFts": true,
```

- [ ] **Step 3: Full test run**

Run: `dotnet test boilerplateBE/tests/Starter.Api.Tests/Starter.Api.Tests.csproj --filter "FullyQualifiedName~Ai"`
Expected: all pass.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Structured/ChunkerRouter.cs \
        boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Consumers/ProcessDocumentConsumer.cs \
        boilerplateBE/src/Starter.Api/appsettings.json \
        boilerplateBE/src/Starter.Api/appsettings.Development.json
git commit -m "feat(ai): expose EnableStructuralChunking and IncludeBreadcrumbInFts settings"
```

---

### Task 24: Post-feature live QA on test app `_test4b3`

Follow the standard post-feature testing workflow from `CLAUDE.md`. Summary:

- [ ] **Step 1: Generate test app**

```bash
cd boilerplateBE/..
powershell -File scripts/rename.ps1 -Name "_test4b3" -OutputDir "."
psql -U postgres -c "DROP DATABASE IF EXISTS _test4b3db;"
```

- [ ] **Step 2: Reconfigure to 5100 / 3100 + update appsettings**

- [ ] **Step 3: First migration for the test app only**

```bash
cd _test4b3/_test4b3-BE/src/_test4b3.Infrastructure
dotnet ef migrations add Plan4b3_StructuralChunking --startup-project ../_test4b3.Api
```

Commit path stays inside `_test4b3/` which is gitignored — the migration never enters `boilerplateBE`.

- [ ] **Step 4: Run BE + FE, upload a markdown test document**

Seed a deliberately-structured markdown doc with Arabic + English headings, a code fence, a table, a math block. Run several RAG queries and verify:
- Heading-only FTS query hits the right chunk (e.g., search for a term that appears only in the breadcrumb).
- Code block returns in context wrapped in ``` fences.
- Table rows stay together under their header replication.

- [ ] **Step 5: Fix any findings in the worktree source, regenerate, retest**

- [ ] **Step 6: Report URLs to user; wait for manual QA confirmation before requesting push authorization**

---

### Task 25: Request push authorization

- [ ] **Step 1: Summarize**

After user confirms manual QA passes, summarize what shipped:
- Commits added (range from Task 1 to Task 23).
- Test counts (new + total).
- Any knobs added and their defaults.
- Follow-on work deferred (e.g., `TypeFilter` on search APIs — spec leaves it as later work).

- [ ] **Step 2: Ask for authorization, then push**

```bash
git push origin feature/ai-integration
```

- [ ] **Step 3: Update memory — mark 4b-3 DONE, point 4b-4 as NEXT**

---
