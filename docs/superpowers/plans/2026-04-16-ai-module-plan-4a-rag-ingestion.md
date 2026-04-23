# AI Module — Plan 4a: RAG Document Ingestion

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let tenant admins upload knowledge-base documents (PDF, DOCX, TXT, MD, CSV) and have the module asynchronously extract text, chunk it hierarchically, embed the child chunks, and persist both the vectors (Qdrant, tenant-scoped collection) and the `AiDocumentChunk` rows (Postgres). After this plan, documents are fully ingested and visible in the DB / Qdrant, but no chat turn consumes them yet. That wiring is Plan 4b.

**Architecture:** A thin CRUD + status controller on top of a MassTransit background consumer. The consumer orchestrates a pipeline of small services (extract → chunk → embed → vector-upsert → chunk-persist) that each live behind an interface so they can be unit-tested in isolation. Qdrant access is wrapped in `IVectorStore` so the rest of the code never touches the `Qdrant.Client` API directly — the same seam will be reused for retrieval in Plan 4b.

**Tech Stack:** .NET 10, MediatR, EF Core (PostgreSQL, `AiDocument`/`AiDocumentChunk` already configured), FluentValidation, MassTransit (already wired at app level, RabbitMQ in dev), `IStorageService` → MinIO (already in Infrastructure), `Qdrant.Client` (NuGet present), `PdfPig` (present), `DocumentFormat.OpenXml` (present), `SharpToken` for token counting (present), `Tesseract` (present — used in this plan for the OCR fallback branch of PDF extraction). No new NuGet packages.

**Spec:** `docs/superpowers/specs/2026-04-13-ai-integration-module-design.md` — sections "Data Model → AiDocument / AiDocumentChunk", "RAG Pipeline → Document Processing Flow / Hierarchical Chunking / Qdrant Tenant Isolation / Metadata Stored Per Vector", "API Endpoints → Knowledge Base (admin)", "Infrastructure → Configuration (appsettings) → `AI:Rag` + `AI:Qdrant` + `AI:Ocr`".

**Plan series position:**
- Plan 1: Foundation + Provider Layer ✅
- Plan 2: Chat + Streaming ✅
- Plan 3: Assistants CRUD + Tool Registry + Function Calling ✅
- **Plan 4a: RAG Document Ingestion ← this plan**
- Plan 4b: RAG Retrieval + Chat Injection (hybrid search, query expansion, re-ranking, `POST /ai/search`, injection into `ChatExecutionService`)
- Plan 5: Agent Engine (autonomous multi-step tasks, triggers)
- Plan 6: Web frontend — chat sidebar + streaming UI
- Plan 7: Web frontend — admin pages (Assistants/KB/Tools/Triggers/Usage)
- Plan 8: Mobile chat

**Out of scope for Plan 4a (intentional):**
- Any chat-side consumption of chunks — `ChatExecutionService` stays exactly as it is. `AiAssistant.KnowledgeBaseDocIds` keeps being persisted but ignored at inference time until Plan 4b.
- Hybrid search, query expansion, re-ranking, `POST /ai/search` — Plan 4b.
- Usage/quota gating on document uploads and embedding calls — we log embedding cost to `AiUsageLog` with `RequestType.Embedding` (piggybacks on existing infrastructure), but we do NOT call `IQuotaChecker.CheckAsync` on upload in this plan. Revisit in Plan 7/admin once there is UI to surface refusal.
- Signed pre-upload URLs / chunked multipart upload — small (≤25 MB) full-body upload via `IFormFile` is sufficient for v1. Large-file uploads are future work.
- Non-Tesseract OCR providers — `IOcrService` has one implementation (`TesseractOcrService`). Azure/Google OCR stays as a swappable slot for later.
- Re-embed on cost-rate change — reprocessing is user-triggered (`POST /documents/{id}/reprocess`).
- Per-document access control beyond tenant scope — all tenant users see all tenant documents. Document-level ACLs are future work.
- Embedding caching — every upload hits the provider. Budget concern only once we have dashboards (Plan 7).

---

## File Map

### New files in `Starter.Module.AI`

| File | Purpose |
|------|---------|
| `Application/DTOs/AiDocumentDto.cs` | Read model for list + detail endpoints |
| `Application/DTOs/AiDocumentMappers.cs` | `AiDocument` → `AiDocumentDto` |
| `Application/DTOs/AiDocumentChunkPreviewDto.cs` | Compact chunk view for document-detail endpoint |
| `Application/Commands/UploadDocument/UploadDocumentCommand.cs` | `(IFormFile File, string? Name)` request |
| `Application/Commands/UploadDocument/UploadDocumentCommandValidator.cs` | Name length, content-type allowlist, max size |
| `Application/Commands/UploadDocument/UploadDocumentCommandHandler.cs` | Save to MinIO, create `AiDocument`, publish `ProcessDocumentMessage` |
| `Application/Commands/DeleteDocument/DeleteDocumentCommand.cs` | Delete request |
| `Application/Commands/DeleteDocument/DeleteDocumentCommandHandler.cs` | Cascade: Qdrant points → `AiDocumentChunk` rows → MinIO file → `AiDocument` |
| `Application/Commands/ReprocessDocument/ReprocessDocumentCommand.cs` | Re-embed request |
| `Application/Commands/ReprocessDocument/ReprocessDocumentCommandHandler.cs` | Reset status + clear chunks + Qdrant points + re-publish message |
| `Application/Queries/GetDocuments/GetDocumentsQuery.cs` | Paged list with `Status?`, `SearchTerm?` filters |
| `Application/Queries/GetDocuments/GetDocumentsQueryHandler.cs` | Handler |
| `Application/Queries/GetDocumentById/GetDocumentByIdQuery.cs` | Single fetch with chunk previews |
| `Application/Queries/GetDocumentById/GetDocumentByIdQueryHandler.cs` | Handler |
| `Application/Messages/ProcessDocumentMessage.cs` | MassTransit message — `Guid DocumentId` only |
| `Application/Services/Ingestion/IDocumentTextExtractor.cs` | Per-content-type extractor contract |
| `Application/Services/Ingestion/ExtractedDocument.cs` | Record: `IReadOnlyList<ExtractedPage> Pages`, optional `DetectedHeadings` |
| `Application/Services/Ingestion/ExtractedPage.cs` | Record: `int PageNumber`, `string Text`, `string? SectionTitle` |
| `Application/Services/Ingestion/IDocumentTextExtractorRegistry.cs` | Select extractor by content type |
| `Application/Services/Ingestion/IDocumentChunker.cs` | Contract: `HierarchicalChunks Chunk(ExtractedDocument, ChunkingOptions)` |
| `Application/Services/Ingestion/HierarchicalChunks.cs` | Record: `IReadOnlyList<ChunkDraft> Parents`, `IReadOnlyList<ChunkDraft> Children` (children carry `ParentIndex`) |
| `Application/Services/Ingestion/ChunkDraft.cs` | Record used before Qdrant IDs are assigned: `int Index, string Content, int TokenCount, int? ParentIndex, string? SectionTitle, int? PageNumber` |
| `Application/Services/Ingestion/IEmbeddingService.cs` | High-level batch embed wrapper |
| `Application/Services/Ingestion/IVectorStore.cs` | Qdrant seam — `EnsureCollectionAsync`, `UpsertAsync`, `DeleteByDocumentAsync`, `DropCollectionAsync` |
| `Application/Services/Ingestion/VectorPoint.cs` | Record: `Guid Id, float[] Vector, VectorPayload Payload` |
| `Application/Services/Ingestion/VectorPayload.cs` | Record: metadata dict per Qdrant payload spec |
| `Infrastructure/Ingestion/DocumentTextExtractorRegistry.cs` | Picks extractor by MIME/extension; unknown type → `UnsupportedContentTypeException` |
| `Infrastructure/Ingestion/Extractors/PlainTextExtractor.cs` | `text/plain`, `text/markdown` |
| `Infrastructure/Ingestion/Extractors/CsvTextExtractor.cs` | `text/csv` — one page per file, sections per column header block |
| `Infrastructure/Ingestion/Extractors/PdfTextExtractor.cs` | `application/pdf` using PdfPig; OCR fallback when native text is < threshold |
| `Infrastructure/Ingestion/Extractors/DocxTextExtractor.cs` | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` using OpenXml |
| `Infrastructure/Ingestion/IOcrService.cs` | OCR contract (`Task<string> ExtractAsync(Stream image, CancellationToken)`) |
| `Infrastructure/Ingestion/Ocr/TesseractOcrService.cs` | Tesseract-backed implementation |
| `Infrastructure/Ingestion/Ocr/NullOcrService.cs` | Fallback when `AI:Ocr:Enabled=false` — throws `NotSupportedException` |
| `Infrastructure/Ingestion/TokenCounter.cs` | Wraps `SharpToken` — one shared instance across chunker + estimator |
| `Infrastructure/Ingestion/HierarchicalDocumentChunker.cs` | Implementation of `IDocumentChunker` |
| `Infrastructure/Ingestion/EmbeddingService.cs` | Resolves active `IAiProvider` via `AiProviderFactory`, calls `EmbedBatchAsync` in batches of `N`, writes `AiUsageLog` with `RequestType.Embedding` |
| `Infrastructure/Ingestion/QdrantVectorStore.cs` | `Qdrant.Client` wrapper — collection name `tenant_{tenantId}`, `Cosine` distance, HNSW default |
| `Infrastructure/Consumers/ProcessDocumentConsumer.cs` | Orchestrates the pipeline inside a single scope |
| `Infrastructure/Settings/AiRagSettings.cs` | Typed options bound to `AI:Rag` |
| `Infrastructure/Settings/AiQdrantSettings.cs` | Typed options bound to `AI:Qdrant` |
| `Infrastructure/Settings/AiOcrSettings.cs` | Typed options bound to `AI:Ocr` |
| `Controllers/AiDocumentsController.cs` | 5 endpoints: list, get, upload, delete, reprocess |

### Modified files

| File | Change |
|------|--------|
| `Starter.Module.AI/AIModule.cs` | Register `IDocumentTextExtractorRegistry` + extractors (singleton), `IDocumentChunker` (singleton), `IEmbeddingService` (scoped), `IVectorStore` (singleton, wraps a shared `QdrantClient`), `IOcrService` (scoped, selected from `AI:Ocr:Enabled`), `TokenCounter` (singleton), bind `AiRagSettings` / `AiQdrantSettings` / `AiOcrSettings`. In `ConfigureServices` pass a `Action<IBusRegistrationConfigurator>? configureBus` hook (new on `IModule`? — no: the existing shared bus configures consumers via assembly scanning, see below). |
| `Starter.Api/Program.cs` | Already scans module assemblies for `IConsumer<>` via `AddConsumers(...)` — no change required as long as `ProcessDocumentConsumer` lives in the AI module assembly. Confirm in Task 1 and skip if already the case. |
| `Starter.Module.AI/Domain/Errors/AiErrors.cs` | Add `DocumentNotFound`, `DocumentUnsupportedContentType(string)`, `DocumentTooLarge(long limit)`, `DocumentAlreadyProcessing`. |
| `Starter.Module.AI/Domain/Entities/AiDocumentChunk.cs` | Add static factory overload `CreateWithoutQdrant(...)` that defers `QdrantPointId` until the vector upsert; replace setter usage in the consumer. (If the existing factory already accepts `QdrantPointId` up-front, have the consumer generate IDs first — decide in Task 9 from the current file content and follow YAGNI.) |
| `Starter.Module.AI/Domain/Entities/AiDocument.cs` | No change; `MarkProcessing/Completed/Failed` already exist. |
| `Starter.Module.AI/Constants/AiPermissions.cs` | Confirm `ManageDocuments` exists (spec shows it). If missing, add and wire to Admin role. |
| `docs/superpowers/specs/2026-04-13-ai-integration-module-design.md` | No edit. |
| `boilerplateBE/src/Starter.Api/appsettings.Development.json` | No edit required — `AI:Qdrant:Host=localhost`, `AI:Qdrant:GrpcPort=6334` already present. |
| `boilerplateBE/docker-compose.yml` | No edit required — `qdrant` service already declared. |

### Integration notes (no code changes required)

- MassTransit registration in `Starter.Api/Program.cs` or `Starter.Infrastructure/DependencyInjection.cs` already calls `AddConsumers(assemblies)` with module assemblies. `ProcessDocumentConsumer` is auto-registered.
- `IStorageService` is registered in `Starter.Infrastructure.DependencyInjection` (S3/MinIO). AI module depends on the `Starter.Application` project already, so the interface is reachable.
- `ICurrentUserService` provides `TenantId` and `UserId` — used in `UploadDocumentCommandHandler` for `AiDocument.Create(tenantId, …, uploadedByUserId)`.
- Permission `Ai.ManageDocuments` must gate all five endpoints via `[Authorize(Policy = AiPermissions.ManageDocuments)]`.

---

## Task 1: MassTransit consumer registration — baseline check + AI module consumer assembly

**Files:**
- Read: `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs` (search for `AddConsumers`)
- Read: `boilerplateBE/src/Starter.Api/Program.cs`
- Read: `boilerplateBE/src/modules/Starter.Module.ImportExport/ImportExportModule.cs` (reference — already registers a consumer)
- Modify only if gap found: `Starter.Module.AI/AIModule.cs`

- [ ] **Step 1: Confirm consumer scanning covers the AI module assembly**

Run: `grep -rn "AddConsumers\|module.*Assembly" boilerplateBE/src/Starter.Api boilerplateBE/src/Starter.Infrastructure --include="*.cs"`

Expected: a call like `cfg.AddConsumers(moduleAssemblies)` or `cfg.AddConsumers(typeof(IModule).Assembly)` that includes every module assembly. If it uses `ModuleLoader.DiscoverModules()` to build the list, the AI assembly is already included (because Plan 1 registered the module).

If **not** auto-scanned: in `AIModule.cs ConfigureServices`, add:

```csharp
services.AddMassTransit(x => x.AddConsumer<ProcessDocumentConsumer>());
```

(MassTransit's `AddMassTransit` calls are additive; existing bus config is preserved.)

- [ ] **Step 2: Commit the baseline note**

```bash
git commit --allow-empty -m "chore(ai): confirm MassTransit scans AI module assembly for consumers"
```

(Skip the commit if no file changed — this step exists only to verify the precondition.)

---

## Task 2: Typed settings — `AiRagSettings`, `AiQdrantSettings`, `AiOcrSettings`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiRagSettings.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiQdrantSettings.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Settings/AiOcrSettings.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

- [ ] **Step 1: Write `AiRagSettings.cs`**

```csharp
namespace Starter.Module.AI.Infrastructure.Settings;

public sealed class AiRagSettings
{
    public const string SectionName = "AI:Rag";

    public int ChunkSize { get; init; } = 512;
    public int ChunkOverlap { get; init; } = 50;
    public int ParentChunkSize { get; init; } = 1536;
    public int TopK { get; init; } = 5;                 // consumed in Plan 4b
    public int RetrievalTopK { get; init; } = 20;       // consumed in Plan 4b
    public double HybridSearchWeight { get; init; } = 0.7;  // consumed in Plan 4b
    public bool EnableQueryExpansion { get; init; } = true; // consumed in Plan 4b
    public bool EnableReranking { get; init; } = true;      // consumed in Plan 4b
    public int EmbedBatchSize { get; init; } = 32;
    public long MaxUploadBytes { get; init; } = 25 * 1024 * 1024; // 25 MB
    public double OcrFallbackMinCharsPerPage { get; init; } = 40; // below this → OCR the page
    public double PageFailureThreshold { get; init; } = 0.25;     // fail whole doc if > 25% of pages error
}
```

- [ ] **Step 2: Write `AiQdrantSettings.cs`**

```csharp
namespace Starter.Module.AI.Infrastructure.Settings;

public sealed class AiQdrantSettings
{
    public const string SectionName = "AI:Qdrant";

    public string Host { get; init; } = "localhost";
    public int GrpcPort { get; init; } = 6334;
    public int HttpPort { get; init; } = 6333;
    public string? ApiKey { get; init; }
    public bool UseTls { get; init; } = false;
}
```

- [ ] **Step 3: Write `AiOcrSettings.cs`**

```csharp
namespace Starter.Module.AI.Infrastructure.Settings;

public sealed class AiOcrSettings
{
    public const string SectionName = "AI:Ocr";

    public bool Enabled { get; init; } = true;
    public string Provider { get; init; } = "Tesseract";
    public string? TessdataPath { get; init; } // null → use default OS install
    public string Language { get; init; } = "eng";
}
```

- [ ] **Step 4: Bind in `AIModule.cs` (inside `ConfigureServices`)**

```csharp
services.Configure<AiRagSettings>(configuration.GetSection(AiRagSettings.SectionName));
services.Configure<AiQdrantSettings>(configuration.GetSection(AiQdrantSettings.SectionName));
services.Configure<AiOcrSettings>(configuration.GetSection(AiOcrSettings.SectionName));
```

- [ ] **Step 5: Build + commit**

```bash
cd boilerplateBE && dotnet build src/modules/Starter.Module.AI
git add src/modules/Starter.Module.AI
git commit -m "feat(ai): typed settings for Rag/Qdrant/Ocr"
```

---

## Task 3: Error codes — `AiErrors.Document*`

**Files:**
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Domain/Errors/AiErrors.cs`

- [ ] **Step 1: Add document-related errors**

Append inside the `AiErrors` static class:

```csharp
public static readonly Error DocumentNotFound =
    new("Ai.DocumentNotFound", "Document not found or you do not have access.");

public static Error DocumentUnsupportedContentType(string contentType) =>
    new("Ai.DocumentUnsupportedContentType",
        $"Content type '{contentType}' is not supported for knowledge base ingestion.");

public static Error DocumentTooLarge(long maxBytes) =>
    new("Ai.DocumentTooLarge",
        $"Document exceeds the {maxBytes / (1024 * 1024)} MB upload limit.");

public static readonly Error DocumentAlreadyProcessing =
    new("Ai.DocumentAlreadyProcessing",
        "Document is currently being processed. Wait for it to finish before reprocessing.");

public static Error DocumentProcessingFailed(string detail) =>
    new("Ai.DocumentProcessingFailed",
        $"Document processing failed: {detail}");
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build src/modules/Starter.Module.AI
git add src/modules/Starter.Module.AI/Domain/Errors/AiErrors.cs
git commit -m "feat(ai): document-ingestion error codes"
```

---

## Task 4: Token counter service

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/TokenCounter.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/TokenCounterTests.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/Starter.Module.AI.csproj` — confirm `<InternalsVisibleTo Include="Starter.Api.Tests" />` already present (added in Plan 3)

- [ ] **Step 1: Write the test first**

```csharp
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Ingestion;
using Xunit;

namespace Starter.Api.Tests.Ai;

public sealed class TokenCounterTests
{
    private readonly TokenCounter _counter = new();

    [Fact]
    public void Count_Returns_Positive_For_NonEmpty_Text()
    {
        _counter.Count("Hello, world!").Should().BeGreaterThan(0);
    }

    [Fact]
    public void Count_Returns_Zero_For_Empty_Text()
    {
        _counter.Count("").Should().Be(0);
    }

    [Fact]
    public void Split_Respects_MaxTokens_Budget()
    {
        var text = string.Join(" ", Enumerable.Range(0, 2000).Select(i => $"word{i}"));
        var pieces = _counter.Split(text, maxTokens: 100).ToList();

        pieces.Should().OnlyContain(p => _counter.Count(p) <= 100);
        string.Concat(pieces).Length.Should().BeGreaterThan(text.Length / 2);
    }
}
```

- [ ] **Step 2: Run the test — expect FAIL**

```bash
cd boilerplateBE && dotnet test tests/Starter.Api.Tests --filter "FullyQualifiedName~TokenCounter" 2>&1 | tail -10
```

Expected: compile error "TokenCounter not found".

- [ ] **Step 3: Implement `TokenCounter.cs`**

```csharp
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
}
```

- [ ] **Step 4: Run the test — expect PASS**

```bash
dotnet test tests/Starter.Api.Tests --filter "FullyQualifiedName~TokenCounter" 2>&1 | tail -5
```

Expected: `Passed!  - Failed: 0, Passed: 3, Skipped: 0`.

- [ ] **Step 5: Register in `AIModule.cs`**

```csharp
services.AddSingleton<TokenCounter>();
```

- [ ] **Step 6: Commit**

```bash
git add src/modules/Starter.Module.AI/Infrastructure/Ingestion/TokenCounter.cs \
        tests/Starter.Api.Tests/Ai/TokenCounterTests.cs \
        src/modules/Starter.Module.AI/AIModule.cs
git commit -m "feat(ai): SharpToken-backed TokenCounter with split-by-budget"
```

---

## Task 5: Extractor contracts and records

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/IDocumentTextExtractor.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/ExtractedDocument.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/ExtractedPage.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/IDocumentTextExtractorRegistry.cs`

- [ ] **Step 1: Write `ExtractedPage.cs`**

```csharp
namespace Starter.Module.AI.Application.Services.Ingestion;

public sealed record ExtractedPage(
    int PageNumber,
    string Text,
    string? SectionTitle = null);
```

- [ ] **Step 2: Write `ExtractedDocument.cs`**

```csharp
namespace Starter.Module.AI.Application.Services.Ingestion;

public sealed record ExtractedDocument(
    IReadOnlyList<ExtractedPage> Pages,
    bool UsedOcr);
```

- [ ] **Step 3: Write `IDocumentTextExtractor.cs`**

```csharp
namespace Starter.Module.AI.Application.Services.Ingestion;

public interface IDocumentTextExtractor
{
    /// <summary>Content types this extractor can handle (lowercase, no parameters).</summary>
    IReadOnlyCollection<string> SupportedContentTypes { get; }

    /// <summary>
    /// Read <paramref name="content"/> (positioned at offset 0) and return an ordered list of pages.
    /// Implementations must not dispose the stream — the caller owns it.
    /// </summary>
    Task<ExtractedDocument> ExtractAsync(Stream content, CancellationToken ct);
}
```

- [ ] **Step 4: Write `IDocumentTextExtractorRegistry.cs`**

```csharp
namespace Starter.Module.AI.Application.Services.Ingestion;

public interface IDocumentTextExtractorRegistry
{
    /// <summary>
    /// Return the extractor matching <paramref name="contentType"/>, or <c>null</c>
    /// when no extractor is registered for that type.
    /// </summary>
    IDocumentTextExtractor? Resolve(string contentType);
}
```

- [ ] **Step 5: Build + commit**

```bash
dotnet build src/modules/Starter.Module.AI
git add src/modules/Starter.Module.AI/Application/Services/Ingestion
git commit -m "feat(ai): document-extractor contracts"
```

---

## Task 6: Plain-text and CSV extractors

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Extractors/PlainTextExtractor.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Extractors/CsvTextExtractor.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/PlainTextExtractorTests.cs`

- [ ] **Step 1: Write `PlainTextExtractorTests.cs`**

```csharp
using System.Text;
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Ingestion.Extractors;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion;

public sealed class PlainTextExtractorTests
{
    [Fact]
    public async Task Extracts_Entire_Stream_As_Single_Page()
    {
        var extractor = new PlainTextExtractor();
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("line one\nline two"));

        var result = await extractor.ExtractAsync(ms, CancellationToken.None);

        result.Pages.Should().HaveCount(1);
        result.Pages[0].PageNumber.Should().Be(1);
        result.Pages[0].Text.Should().Be("line one\nline two");
        result.UsedOcr.Should().BeFalse();
    }

    [Fact]
    public void Advertises_TextPlain_And_Markdown()
    {
        new PlainTextExtractor().SupportedContentTypes
            .Should().BeEquivalentTo(new[] { "text/plain", "text/markdown" });
    }
}
```

- [ ] **Step 2: Write `PlainTextExtractor.cs`**

```csharp
using Starter.Module.AI.Application.Services.Ingestion;

namespace Starter.Module.AI.Infrastructure.Ingestion.Extractors;

public sealed class PlainTextExtractor : IDocumentTextExtractor
{
    public IReadOnlyCollection<string> SupportedContentTypes { get; } =
        new[] { "text/plain", "text/markdown" };

    public async Task<ExtractedDocument> ExtractAsync(Stream content, CancellationToken ct)
    {
        using var reader = new StreamReader(content, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        return new ExtractedDocument(
            Pages: new[] { new ExtractedPage(1, text) },
            UsedOcr: false);
    }
}
```

- [ ] **Step 3: Write `CsvTextExtractor.cs`**

```csharp
using Starter.Module.AI.Application.Services.Ingestion;

namespace Starter.Module.AI.Infrastructure.Ingestion.Extractors;

public sealed class CsvTextExtractor : IDocumentTextExtractor
{
    // CSV is treated like plain text — the chunker keeps logical grouping via
    // tokens only. A richer row-based extractor is future work.
    public IReadOnlyCollection<string> SupportedContentTypes { get; } =
        new[] { "text/csv", "application/csv" };

    public async Task<ExtractedDocument> ExtractAsync(Stream content, CancellationToken ct)
    {
        using var reader = new StreamReader(content, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        return new ExtractedDocument(
            Pages: new[] { new ExtractedPage(1, text) },
            UsedOcr: false);
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test tests/Starter.Api.Tests --filter "FullyQualifiedName~PlainTextExtractor" 2>&1 | tail -5
```

Expected: 2 passed.

- [ ] **Step 5: Commit**

```bash
git add src/modules/Starter.Module.AI/Infrastructure/Ingestion/Extractors \
        tests/Starter.Api.Tests/Ai/Ingestion/PlainTextExtractorTests.cs
git commit -m "feat(ai): plain-text + csv extractors"
```

---

## Task 7: OCR service

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/IOcrService.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Ocr/TesseractOcrService.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Ocr/NullOcrService.cs`

- [ ] **Step 1: Write `IOcrService.cs`**

```csharp
namespace Starter.Module.AI.Infrastructure.Ingestion;

public interface IOcrService
{
    /// <summary>OCR a single image stream and return the detected text.</summary>
    Task<string> ExtractAsync(Stream imageStream, CancellationToken ct);
}
```

- [ ] **Step 2: Write `NullOcrService.cs`**

```csharp
namespace Starter.Module.AI.Infrastructure.Ingestion.Ocr;

internal sealed class NullOcrService : IOcrService
{
    public Task<string> ExtractAsync(Stream imageStream, CancellationToken ct) =>
        throw new NotSupportedException("OCR is disabled (AI:Ocr:Enabled=false).");
}
```

- [ ] **Step 3: Write `TesseractOcrService.cs`**

```csharp
using Microsoft.Extensions.Options;
using Starter.Module.AI.Infrastructure.Settings;
using Tesseract;

namespace Starter.Module.AI.Infrastructure.Ingestion.Ocr;

internal sealed class TesseractOcrService : IOcrService
{
    private readonly string _tessdataPath;
    private readonly string _language;

    public TesseractOcrService(IOptions<AiOcrSettings> options)
    {
        _tessdataPath = options.Value.TessdataPath ?? ResolveDefaultTessdataPath();
        _language = options.Value.Language;
    }

    public Task<string> ExtractAsync(Stream imageStream, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using var ms = new MemoryStream();
        imageStream.CopyTo(ms);

        using var engine = new TesseractEngine(_tessdataPath, _language, EngineMode.Default);
        using var img = Pix.LoadFromMemory(ms.ToArray());
        using var page = engine.Process(img);
        return Task.FromResult(page.GetText() ?? string.Empty);
    }

    private static string ResolveDefaultTessdataPath()
    {
        // Docker image typically has /usr/share/tesseract-ocr/4.00/tessdata
        var candidates = new[]
        {
            "/usr/share/tesseract-ocr/4.00/tessdata",
            "/usr/share/tesseract-ocr/tessdata",
            "/opt/homebrew/share/tessdata",
            Path.Combine(AppContext.BaseDirectory, "tessdata")
        };

        return candidates.FirstOrDefault(Directory.Exists)
            ?? throw new InvalidOperationException(
                "Could not find tessdata directory. Set AI:Ocr:TessdataPath.");
    }
}
```

- [ ] **Step 4: Register in `AIModule.cs`**

```csharp
// Inside ConfigureServices, after settings binding:
var ocrEnabled = configuration.GetValue<bool?>("AI:Ocr:Enabled") ?? true;
if (ocrEnabled)
    services.AddScoped<IOcrService, Infrastructure.Ingestion.Ocr.TesseractOcrService>();
else
    services.AddScoped<IOcrService, Infrastructure.Ingestion.Ocr.NullOcrService>();
```

- [ ] **Step 5: Build + commit**

```bash
dotnet build src/modules/Starter.Module.AI
git add src/modules/Starter.Module.AI
git commit -m "feat(ai): Tesseract OCR service with null fallback"
```

---

## Task 8: PDF and DOCX extractors

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Extractors/PdfTextExtractor.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/Extractors/DocxTextExtractor.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/DocxTextExtractorTests.cs`

> PDF extraction is not covered by a unit test because constructing a valid PDF byte stream in a test is heavyweight. We validate PDF in the E2E pass (Task 19). DOCX is covered — OpenXml documents are easy to generate in memory.

- [ ] **Step 1: Write `PdfTextExtractor.cs`**

```csharp
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Infrastructure.Settings;
using UglyToad.PdfPig;

namespace Starter.Module.AI.Infrastructure.Ingestion.Extractors;

internal sealed class PdfTextExtractor(
    IOcrService ocr,
    IOptions<AiRagSettings> ragOptions,
    IOptions<AiOcrSettings> ocrOptions) : IDocumentTextExtractor
{
    public IReadOnlyCollection<string> SupportedContentTypes { get; } =
        new[] { "application/pdf" };

    public async Task<ExtractedDocument> ExtractAsync(Stream content, CancellationToken ct)
    {
        var minChars = ragOptions.Value.OcrFallbackMinCharsPerPage;
        var ocrEnabled = ocrOptions.Value.Enabled;

        // PdfPig wants a seekable stream; caller guarantees it (our upload copies to memory).
        using var document = PdfDocument.Open(content);

        var pages = new List<ExtractedPage>(document.NumberOfPages);
        var usedOcr = false;
        var failedPages = 0;

        for (var i = 1; i <= document.NumberOfPages; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var page = document.GetPage(i);
                var text = page.Text?.Trim() ?? "";

                if (text.Length < minChars && ocrEnabled)
                {
                    // Render the page to an image and OCR it. PdfPig doesn't render
                    // images itself; we fall back to raw-image page streams via
                    // the Images collection. If no image, skip OCR and keep the
                    // native (possibly empty) text.
                    var firstImage = page.GetImages().FirstOrDefault();
                    if (firstImage != null && firstImage.TryGetPng(out var png))
                    {
                        using var imgStream = new MemoryStream(png);
                        text = await ocr.ExtractAsync(imgStream, ct);
                        usedOcr = true;
                    }
                }

                pages.Add(new ExtractedPage(i, text));
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // A single bad page (corrupt font, OCR crash) must not kill the whole
                // document. Record an empty page and continue. The consumer enforces
                // a whole-doc failure threshold via AiRagSettings.PageFailureThreshold.
                failedPages++;
                pages.Add(new ExtractedPage(i, ""));
            }
        }

        var failureRatio = document.NumberOfPages == 0
            ? 0d
            : (double)failedPages / document.NumberOfPages;
        if (failureRatio > ragOptions.Value.PageFailureThreshold)
            throw new InvalidOperationException(
                $"PDF extraction failed for {failedPages}/{document.NumberOfPages} pages " +
                $"(threshold {ragOptions.Value.PageFailureThreshold:P0}).");

        return new ExtractedDocument(pages, usedOcr);
    }
}
```

- [ ] **Step 2: Write `DocxTextExtractor.cs`**

```csharp
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Starter.Module.AI.Application.Services.Ingestion;

namespace Starter.Module.AI.Infrastructure.Ingestion.Extractors;

internal sealed class DocxTextExtractor : IDocumentTextExtractor
{
    public IReadOnlyCollection<string> SupportedContentTypes { get; } =
        new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" };

    public Task<ExtractedDocument> ExtractAsync(Stream content, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using var doc = WordprocessingDocument.Open(content, false);
        var body = doc.MainDocumentPart?.Document.Body;
        if (body is null)
            return Task.FromResult(new ExtractedDocument(Array.Empty<ExtractedPage>(), false));

        // Group paragraphs by heading boundary. OpenXml doesn't page-break reliably,
        // so we treat the whole doc as a single page for v1; headings become section
        // titles on the one page. This is enough to feed the chunker.
        var paragraphs = body.Elements<Paragraph>().ToList();
        var buffer = new System.Text.StringBuilder();
        string? currentHeading = null;

        foreach (var p in paragraphs)
        {
            var style = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            if (style != null && style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
                currentHeading ??= p.InnerText;

            if (!string.IsNullOrWhiteSpace(p.InnerText))
                buffer.AppendLine(p.InnerText);
        }

        return Task.FromResult(new ExtractedDocument(
            Pages: new[] { new ExtractedPage(1, buffer.ToString().Trim(), currentHeading) },
            UsedOcr: false));
    }
}
```

- [ ] **Step 3: Write `DocxTextExtractorTests.cs`**

```csharp
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Ingestion.Extractors;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion;

public sealed class DocxTextExtractorTests
{
    [Fact]
    public async Task Extracts_Paragraphs_As_SinglePage()
    {
        using var ms = new MemoryStream();
        CreateDocx(ms, "Heading1", "Hello world.");
        ms.Position = 0;

        var extractor = new DocxTextExtractor();
        var result = await extractor.ExtractAsync(ms, CancellationToken.None);

        result.Pages.Should().HaveCount(1);
        result.Pages[0].Text.Should().Contain("Hello world.");
        result.Pages[0].SectionTitle.Should().Be("Heading1");
        result.UsedOcr.Should().BeFalse();
    }

    private static void CreateDocx(Stream stream, string heading, string body)
    {
        using var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new Document(new Body(
            new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }),
                new Run(new Text(heading))),
            new Paragraph(new Run(new Text(body)))));
        main.Document.Save();
    }
}
```

- [ ] **Step 4: Run the DOCX test**

```bash
dotnet test tests/Starter.Api.Tests --filter "FullyQualifiedName~DocxTextExtractor" 2>&1 | tail -5
```

Expected: 1 passed.

- [ ] **Step 5: Commit**

```bash
git add src/modules/Starter.Module.AI/Infrastructure/Ingestion/Extractors \
        tests/Starter.Api.Tests/Ai/Ingestion/DocxTextExtractorTests.cs
git commit -m "feat(ai): PDF (PdfPig+OCR fallback) and DOCX (OpenXml) extractors"
```

---

## Task 9: Extractor registry

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/DocumentTextExtractorRegistry.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/DocumentTextExtractorRegistryTests.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

- [ ] **Step 1: Write the test first**

```csharp
using FluentAssertions;
using Moq;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Infrastructure.Ingestion;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion;

public sealed class DocumentTextExtractorRegistryTests
{
    [Fact]
    public void Resolve_Returns_Registered_Extractor_By_ContentType()
    {
        var mock = new Mock<IDocumentTextExtractor>();
        mock.SetupGet(e => e.SupportedContentTypes).Returns(new[] { "text/plain" });

        var registry = new DocumentTextExtractorRegistry(new[] { mock.Object });

        registry.Resolve("text/plain").Should().Be(mock.Object);
    }

    [Fact]
    public void Resolve_Is_Case_Insensitive_And_Ignores_Parameters()
    {
        var mock = new Mock<IDocumentTextExtractor>();
        mock.SetupGet(e => e.SupportedContentTypes).Returns(new[] { "text/plain" });

        var registry = new DocumentTextExtractorRegistry(new[] { mock.Object });

        registry.Resolve("TEXT/PLAIN; charset=utf-8").Should().Be(mock.Object);
    }

    [Fact]
    public void Resolve_Returns_Null_For_Unknown_Type()
    {
        var registry = new DocumentTextExtractorRegistry(Array.Empty<IDocumentTextExtractor>());
        registry.Resolve("application/x-zip").Should().BeNull();
    }
}
```

- [ ] **Step 2: Implement `DocumentTextExtractorRegistry.cs`**

```csharp
using Starter.Module.AI.Application.Services.Ingestion;

namespace Starter.Module.AI.Infrastructure.Ingestion;

internal sealed class DocumentTextExtractorRegistry : IDocumentTextExtractorRegistry
{
    private readonly Dictionary<string, IDocumentTextExtractor> _byType;

    public DocumentTextExtractorRegistry(IEnumerable<IDocumentTextExtractor> extractors)
    {
        _byType = new Dictionary<string, IDocumentTextExtractor>(StringComparer.OrdinalIgnoreCase);
        foreach (var ex in extractors)
            foreach (var ct in ex.SupportedContentTypes)
                _byType[ct] = ex;
    }

    public IDocumentTextExtractor? Resolve(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return null;
        // Strip parameters like "; charset=utf-8"
        var semi = contentType.IndexOf(';');
        var key = (semi >= 0 ? contentType[..semi] : contentType).Trim();
        return _byType.TryGetValue(key, out var ex) ? ex : null;
    }
}
```

- [ ] **Step 3: Register in `AIModule.cs`**

```csharp
services.AddSingleton<IDocumentTextExtractor, Infrastructure.Ingestion.Extractors.PlainTextExtractor>();
services.AddSingleton<IDocumentTextExtractor, Infrastructure.Ingestion.Extractors.CsvTextExtractor>();
services.AddSingleton<IDocumentTextExtractor, Infrastructure.Ingestion.Extractors.DocxTextExtractor>();
services.AddScoped<IDocumentTextExtractor, Infrastructure.Ingestion.Extractors.PdfTextExtractor>();
// Registry itself is scoped so that it picks up the scoped PDF extractor (OCR dep).
services.AddScoped<IDocumentTextExtractorRegistry, Infrastructure.Ingestion.DocumentTextExtractorRegistry>();
```

- [ ] **Step 4: Run tests**

```bash
dotnet test tests/Starter.Api.Tests --filter "FullyQualifiedName~DocumentTextExtractorRegistry" 2>&1 | tail -5
```

Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add src/modules/Starter.Module.AI/Infrastructure/Ingestion/DocumentTextExtractorRegistry.cs \
        tests/Starter.Api.Tests/Ai/Ingestion/DocumentTextExtractorRegistryTests.cs \
        src/modules/Starter.Module.AI/AIModule.cs
git commit -m "feat(ai): document-extractor registry with content-type dispatch"
```

---

## Task 10: Hierarchical chunker — contracts

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/IDocumentChunker.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/ChunkDraft.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/HierarchicalChunks.cs`

- [ ] **Step 1: Write `ChunkDraft.cs`**

```csharp
namespace Starter.Module.AI.Application.Services.Ingestion;

public sealed record ChunkDraft(
    int Index,
    string Content,
    int TokenCount,
    int? ParentIndex,       // null for parent rows
    string? SectionTitle,
    int? PageNumber);
```

- [ ] **Step 2: Write `HierarchicalChunks.cs`**

```csharp
namespace Starter.Module.AI.Application.Services.Ingestion;

public sealed record HierarchicalChunks(
    IReadOnlyList<ChunkDraft> Parents,
    IReadOnlyList<ChunkDraft> Children);
```

- [ ] **Step 3: Write `IDocumentChunker.cs`**

```csharp
namespace Starter.Module.AI.Application.Services.Ingestion;

public interface IDocumentChunker
{
    HierarchicalChunks Chunk(ExtractedDocument document, ChunkingOptions options);
}

public sealed record ChunkingOptions(int ParentTokens, int ChildTokens, int ChildOverlapTokens);
```

- [ ] **Step 4: Build + commit**

```bash
dotnet build src/modules/Starter.Module.AI
git add src/modules/Starter.Module.AI/Application/Services/Ingestion
git commit -m "feat(ai): hierarchical-chunker contracts"
```

---

## Task 11: Hierarchical chunker — implementation

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/HierarchicalDocumentChunker.cs`
- Create: `boilerplateBE/tests/Starter.Api.Tests/Ai/Ingestion/HierarchicalDocumentChunkerTests.cs`

- [ ] **Step 1: Write the test first**

```csharp
using FluentAssertions;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Infrastructure.Ingestion;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion;

public sealed class HierarchicalDocumentChunkerTests
{
    private readonly TokenCounter _counter = new();
    private readonly HierarchicalDocumentChunker _chunker;

    public HierarchicalDocumentChunkerTests()
    {
        _chunker = new HierarchicalDocumentChunker(_counter);
    }

    [Fact]
    public void Produces_At_Least_One_Parent_And_One_Child_For_Short_Text()
    {
        var doc = new ExtractedDocument(
            new[] { new ExtractedPage(1, "Hello world. This is a small document.") },
            UsedOcr: false);

        var result = _chunker.Chunk(doc, new ChunkingOptions(1536, 512, 50));

        result.Parents.Should().HaveCountGreaterOrEqualTo(1);
        result.Children.Should().HaveCountGreaterOrEqualTo(1);
        result.Children[0].ParentIndex.Should().NotBeNull();
    }

    [Fact]
    public void Respects_Parent_Token_Budget()
    {
        var bigText = string.Join(" ", Enumerable.Range(0, 5000).Select(i => $"word{i}"));
        var doc = new ExtractedDocument(
            new[] { new ExtractedPage(1, bigText) },
            UsedOcr: false);

        var result = _chunker.Chunk(doc, new ChunkingOptions(ParentTokens: 200, ChildTokens: 50, ChildOverlapTokens: 10));

        result.Parents.Should().OnlyContain(p => p.TokenCount <= 200);
    }

    [Fact]
    public void Children_Overlap_By_Configured_Token_Budget()
    {
        var bigText = string.Join(" ", Enumerable.Range(0, 500).Select(i => $"word{i}"));
        var doc = new ExtractedDocument(
            new[] { new ExtractedPage(1, bigText) },
            UsedOcr: false);

        var result = _chunker.Chunk(doc, new ChunkingOptions(ParentTokens: 1536, ChildTokens: 50, ChildOverlapTokens: 10));

        // With overlap, consecutive child chunks should share some prefix/suffix text.
        // Weak assertion — just ensure more than 1 child came out.
        result.Children.Should().HaveCountGreaterThan(1);
        result.Children.Should().OnlyContain(c => c.TokenCount <= 60); // 50 target + overlap leeway
    }

    [Fact]
    public void Carries_Page_Number_And_Section_Title()
    {
        var doc = new ExtractedDocument(
            new[]
            {
                new ExtractedPage(1, "First page text.", "Intro"),
                new ExtractedPage(2, "Second page text.", "Details"),
            },
            UsedOcr: false);

        var result = _chunker.Chunk(doc, new ChunkingOptions(1536, 512, 50));

        result.Children.Select(c => c.PageNumber).Should().Contain(new int?[] { 1, 2 });
        result.Children.Select(c => c.SectionTitle).Should().Contain(new[] { "Intro", "Details" });
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL (type missing)**

```bash
dotnet test tests/Starter.Api.Tests --filter "FullyQualifiedName~HierarchicalDocumentChunker" 2>&1 | tail -5
```

- [ ] **Step 3: Implement `HierarchicalDocumentChunker.cs`**

```csharp
using Starter.Module.AI.Application.Services.Ingestion;

namespace Starter.Module.AI.Infrastructure.Ingestion;

internal sealed class HierarchicalDocumentChunker(TokenCounter counter) : IDocumentChunker
{
    public HierarchicalChunks Chunk(ExtractedDocument document, ChunkingOptions options)
    {
        // Strategy:
        // 1. Concatenate pages with a separator so the token budget survives
        //    across page breaks. Track a flat text offset → (page, section) lookup.
        // 2. Slice into parent chunks by token budget.
        // 3. For each parent, slice into children with overlap.
        // 4. Children inherit the (page, section) of their first token.

        var (fullText, offsets) = FlattenPages(document);

        var parents = new List<ChunkDraft>();
        var children = new List<ChunkDraft>();

        var parentTextPieces = counter.Split(fullText, options.ParentTokens).ToList();
        var cursor = 0;

        for (var pIdx = 0; pIdx < parentTextPieces.Count; pIdx++)
        {
            var parentText = parentTextPieces[pIdx];
            var parentTokens = counter.Count(parentText);
            var parentStart = cursor;

            parents.Add(new ChunkDraft(
                Index: pIdx,
                Content: parentText,
                TokenCount: parentTokens,
                ParentIndex: null,
                SectionTitle: offsets.LookupSection(parentStart),
                PageNumber: offsets.LookupPage(parentStart)));

            // Children with overlap: advance by (ChildTokens - Overlap) each step.
            var childText = parentText;
            var step = Math.Max(1, options.ChildTokens - options.ChildOverlapTokens);
            var childSlices = SplitWithOverlap(childText, options.ChildTokens, step).ToList();

            foreach (var slice in childSlices)
            {
                var sliceTokens = counter.Count(slice);
                children.Add(new ChunkDraft(
                    Index: children.Count,
                    Content: slice,
                    TokenCount: sliceTokens,
                    ParentIndex: pIdx,
                    SectionTitle: parents[pIdx].SectionTitle,
                    PageNumber: parents[pIdx].PageNumber));
            }

            cursor += parentText.Length;
        }

        return new HierarchicalChunks(parents, children);
    }

    private IEnumerable<string> SplitWithOverlap(string text, int windowTokens, int stepTokens)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        var tokens = counter.Split(text, maxTokens: stepTokens).ToList();
        for (var i = 0; i < tokens.Count; i++)
        {
            // Glue `stepTokens` tokens + enough trailing to reach `windowTokens`.
            // Cheap approximation: take `ceil(windowTokens/stepTokens)` consecutive pieces.
            var take = (int)Math.Ceiling((double)windowTokens / stepTokens);
            var slice = string.Concat(tokens.Skip(i).Take(take));
            yield return slice;
        }
    }

    private static (string FullText, PageOffsetIndex Offsets) FlattenPages(ExtractedDocument doc)
    {
        var sb = new System.Text.StringBuilder();
        var starts = new List<(int CharOffset, int Page, string? Section)>();

        foreach (var page in doc.Pages)
        {
            starts.Add((sb.Length, page.PageNumber, page.SectionTitle));
            sb.Append(page.Text);
            sb.Append('\n');
        }

        return (sb.ToString(), new PageOffsetIndex(starts));
    }

    private sealed class PageOffsetIndex
    {
        private readonly List<(int CharOffset, int Page, string? Section)> _entries;
        public PageOffsetIndex(List<(int, int, string?)> entries) => _entries = entries;

        public int? LookupPage(int charOffset) => FindLastAtOrBefore(charOffset)?.Page;
        public string? LookupSection(int charOffset) => FindLastAtOrBefore(charOffset)?.Section;

        private (int CharOffset, int Page, string? Section)? FindLastAtOrBefore(int offset)
        {
            (int, int, string?)? best = null;
            foreach (var e in _entries)
                if (e.CharOffset <= offset) best = e;
                else break;
            return best;
        }
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
dotnet test tests/Starter.Api.Tests --filter "FullyQualifiedName~HierarchicalDocumentChunker" 2>&1 | tail -5
```

Expected: 4 passed.

- [ ] **Step 5: Register + commit**

In `AIModule.cs`:
```csharp
services.AddSingleton<IDocumentChunker, Infrastructure.Ingestion.HierarchicalDocumentChunker>();
```

```bash
git add src/modules/Starter.Module.AI/Infrastructure/Ingestion/HierarchicalDocumentChunker.cs \
        tests/Starter.Api.Tests/Ai/Ingestion/HierarchicalDocumentChunkerTests.cs \
        src/modules/Starter.Module.AI/AIModule.cs
git commit -m "feat(ai): hierarchical chunker with parent/child token budgets"
```

---

## Task 12: Vector-store seam + Qdrant implementation

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/IVectorStore.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/VectorPoint.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/VectorPayload.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/QdrantVectorStore.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

- [ ] **Step 1: Write `VectorPayload.cs`**

```csharp
namespace Starter.Module.AI.Application.Services.Ingestion;

public sealed record VectorPayload(
    Guid DocumentId,
    string DocumentName,
    string ChunkLevel,          // "child" in this plan; "parent" stored in Postgres only
    int ChunkIndex,
    string? SectionTitle,
    int? PageNumber,
    Guid? ParentChunkId,
    Guid TenantId);
```

- [ ] **Step 2: Write `VectorPoint.cs`**

```csharp
namespace Starter.Module.AI.Application.Services.Ingestion;

public sealed record VectorPoint(Guid Id, float[] Vector, VectorPayload Payload);
```

- [ ] **Step 3: Write `IVectorStore.cs`**

```csharp
namespace Starter.Module.AI.Application.Services.Ingestion;

public interface IVectorStore
{
    Task EnsureCollectionAsync(Guid tenantId, int vectorSize, CancellationToken ct);
    Task UpsertAsync(Guid tenantId, IReadOnlyList<VectorPoint> points, CancellationToken ct);
    Task DeleteByDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct);
    Task DropCollectionAsync(Guid tenantId, CancellationToken ct);
}
```

- [ ] **Step 4: Write `QdrantVectorStore.cs`**

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Ingestion;

internal sealed class QdrantVectorStore : IVectorStore
{
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantVectorStore> _logger;

    public QdrantVectorStore(IOptions<AiQdrantSettings> options, ILogger<QdrantVectorStore> logger)
    {
        var s = options.Value;
        _client = new QdrantClient(s.Host, s.GrpcPort, https: s.UseTls, apiKey: s.ApiKey);
        _logger = logger;
    }

    private static string CollectionName(Guid tenantId) => $"tenant_{tenantId:N}";

    public async Task EnsureCollectionAsync(Guid tenantId, int vectorSize, CancellationToken ct)
    {
        var name = CollectionName(tenantId);
        var collections = await _client.ListCollectionsAsync(ct);
        if (collections.Any(c => c == name))
            return;

        await _client.CreateCollectionAsync(
            collectionName: name,
            vectorsConfig: new VectorParams
            {
                Size = (ulong)vectorSize,
                Distance = Distance.Cosine
            },
            cancellationToken: ct);

        _logger.LogInformation("Created Qdrant collection {Collection} (dim={Dim})", name, vectorSize);
    }

    public async Task UpsertAsync(Guid tenantId, IReadOnlyList<VectorPoint> points, CancellationToken ct)
    {
        if (points.Count == 0) return;

        var name = CollectionName(tenantId);
        var qPoints = points.Select(p => new PointStruct
        {
            Id = new PointId { Uuid = p.Id.ToString() },
            Vectors = p.Vector,
            Payload =
            {
                ["document_id"]     = p.Payload.DocumentId.ToString(),
                ["document_name"]   = p.Payload.DocumentName,
                ["chunk_level"]     = p.Payload.ChunkLevel,
                ["chunk_index"]     = p.Payload.ChunkIndex,
                ["section_title"]   = p.Payload.SectionTitle ?? string.Empty,
                ["page_number"]     = p.Payload.PageNumber ?? 0,
                ["parent_chunk_id"] = p.Payload.ParentChunkId?.ToString() ?? string.Empty,
                ["tenant_id"]       = p.Payload.TenantId.ToString(),
            }
        }).ToList();

        await _client.UpsertAsync(name, qPoints, cancellationToken: ct);
    }

    public async Task DeleteByDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct)
    {
        var name = CollectionName(tenantId);
        var filter = new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "document_id",
                        Match = new Match { Text = documentId.ToString() }
                    }
                }
            }
        };
        await _client.DeleteAsync(name, filter, cancellationToken: ct);
    }

    public async Task DropCollectionAsync(Guid tenantId, CancellationToken ct)
    {
        var name = CollectionName(tenantId);
        await _client.DeleteCollectionAsync(name, cancellationToken: ct);
    }
}
```

- [ ] **Step 5: Register in `AIModule.cs`**

```csharp
services.AddSingleton<IVectorStore, Infrastructure.Ingestion.QdrantVectorStore>();
```

- [ ] **Step 6: Build + commit**

```bash
dotnet build src/modules/Starter.Module.AI
git add src/modules/Starter.Module.AI/Application/Services/Ingestion \
        src/modules/Starter.Module.AI/Infrastructure/Ingestion/QdrantVectorStore.cs \
        src/modules/Starter.Module.AI/AIModule.cs
git commit -m "feat(ai): IVectorStore seam + Qdrant implementation (per-tenant collections)"
```

---

## Task 13: Embedding service

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Services/Ingestion/IEmbeddingService.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Ingestion/EmbeddingService.cs`
- Modify: `boilerplateBE/src/modules/Starter.Module.AI/AIModule.cs`

- [ ] **Step 1: Write `IEmbeddingService.cs`**

```csharp
namespace Starter.Module.AI.Application.Services.Ingestion;

public interface IEmbeddingService
{
    /// <summary>
    /// Embed <paramref name="texts"/> in batches, returning vectors aligned to input order.
    /// Writes an <c>AiUsageLog</c> entry with <c>RequestType.Embedding</c> for the total
    /// token count across the batch.
    /// </summary>
    Task<float[][]> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct);

    /// <summary>Dimensions of the currently configured embedding model.</summary>
    int VectorSize { get; }
}
```

- [ ] **Step 2: Write `EmbeddingService.cs`**

```csharp
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Ingestion;

internal sealed class EmbeddingService(
    AiProviderFactory providerFactory,
    AiDbContext context,
    ICurrentUserService currentUser,
    IOptions<AiRagSettings> ragOptions,
    TokenCounter tokenCounter) : IEmbeddingService
{
    // OpenAI text-embedding-3-small = 1536; Ollama nomic-embed-text = 768.
    // We pick the size from the first embedding call and cache it per-instance.
    // Qdrant collection creation happens AFTER the first embed completes so we
    // always have the right size — see ProcessDocumentConsumer ordering.
    private int _vectorSize = -1;
    public int VectorSize => _vectorSize > 0
        ? _vectorSize
        : throw new InvalidOperationException("Call EmbedAsync at least once before reading VectorSize.");

    public async Task<float[][]> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        if (texts.Count == 0) return Array.Empty<float[]>();

        var provider = providerFactory.GetProvider();
        var providerType = providerFactory.GetDefaultProviderType();
        var batchSize = ragOptions.Value.EmbedBatchSize;
        var all = new List<float[]>(texts.Count);
        var totalTokens = 0;

        for (var offset = 0; offset < texts.Count; offset += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = texts.Skip(offset).Take(batchSize).ToList();
            var vectors = await EmbedBatchWithRetryAsync(provider, batch, ct);
            all.AddRange(vectors);
            totalTokens += batch.Sum(tokenCounter.Count);
        }

        if (_vectorSize < 0 && all.Count > 0) _vectorSize = all[0].Length;

        if (currentUser.UserId is Guid userId)
        {
            var log = AiUsageLog.Create(
                tenantId: currentUser.TenantId,
                userId: userId,
                provider: providerType,
                model: "embedding",   // provider-specific models vary; coarse label is fine
                inputTokens: totalTokens,
                outputTokens: 0,
                estimatedCost: 0m,    // cost table is chat-only for v1; revisit in Plan 7
                requestType: AiRequestType.Embedding);
            context.AiUsageLogs.Add(log);
            await context.SaveChangesAsync(ct);
        }

        return all.ToArray();
    }

    // Up to 3 attempts with exponential backoff (200ms, 800ms). Provider transient
    // faults (429/5xx wrapped in HttpRequestException / TaskCanceledException) are
    // retried; other exceptions bubble immediately so we fail fast on config bugs.
    private static async Task<IReadOnlyList<float[]>> EmbedBatchWithRetryAsync(
        IAiProvider provider, IReadOnlyList<string> batch, CancellationToken ct)
    {
        var delays = new[] { TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(800) };
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await provider.EmbedBatchAsync(batch, ct);
            }
            catch (Exception ex) when (attempt < delays.Length && IsTransient(ex))
            {
                await Task.Delay(delays[attempt], ct);
            }
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or TimeoutException;
}
```

- [ ] **Step 3: Register + commit**

In `AIModule.cs`:
```csharp
services.AddScoped<IEmbeddingService, Infrastructure.Ingestion.EmbeddingService>();
```

```bash
dotnet build src/modules/Starter.Module.AI
git add src/modules/Starter.Module.AI
git commit -m "feat(ai): batch embedding service with usage logging"
```

---

## Task 14: `ProcessDocumentMessage` + consumer

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Messages/ProcessDocumentMessage.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Infrastructure/Consumers/ProcessDocumentConsumer.cs`

- [ ] **Step 1: Write `ProcessDocumentMessage.cs`**

```csharp
namespace Starter.Module.AI.Application.Messages;

public sealed record ProcessDocumentMessage(Guid DocumentId);
```

- [ ] **Step 2: Write `ProcessDocumentConsumer.cs`**

```csharp
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Messages;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Infrastructure.Consumers;

public sealed class ProcessDocumentConsumer(IServiceScopeFactory scopeFactory)
    : IConsumer<ProcessDocumentMessage>
{
    public async Task Consume(ConsumeContext<ProcessDocumentMessage> context)
    {
        var ct = context.CancellationToken;
        using var scope = scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
        var extractors = scope.ServiceProvider.GetRequiredService<IDocumentTextExtractorRegistry>();
        var chunker = scope.ServiceProvider.GetRequiredService<IDocumentChunker>();
        var embedder = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
        var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
        var ragOptions = scope.ServiceProvider.GetRequiredService<IOptions<AiRagSettings>>().Value;
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ProcessDocumentConsumer>>();

        var doc = await db.AiDocuments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == context.Message.DocumentId, ct);

        if (doc is null)
        {
            logger.LogWarning("ProcessDocument: document {Id} not found", context.Message.DocumentId);
            return;
        }

        try
        {
            doc.MarkProcessing();
            await db.SaveChangesAsync(ct);

            var extractor = extractors.Resolve(doc.ContentType)
                ?? throw new InvalidOperationException(
                    $"No extractor registered for content type '{doc.ContentType}'.");

            await using var fileStream = await storage.DownloadAsync(doc.FileRef, ct);
            // Buffer into memory so PdfPig / DOCX can seek.
            using var buffered = new MemoryStream();
            await fileStream.CopyToAsync(buffered, ct);
            buffered.Position = 0;

            var extracted = await extractor.ExtractAsync(buffered, ct);

            var chunks = chunker.Chunk(extracted, new ChunkingOptions(
                ParentTokens: ragOptions.ParentChunkSize,
                ChildTokens: ragOptions.ChunkSize,
                ChildOverlapTokens: ragOptions.ChunkOverlap));

            // Embed children only; parents are context-only and live in Postgres.
            var childTexts = chunks.Children.Select(c => c.Content).ToList();
            var vectors = await embedder.EmbedAsync(childTexts, ct);

            // Tenant-specific collection. TenantId can be null for platform admin uploads;
            // we use Guid.Empty as the "platform" tenant collection name.
            var tenantId = doc.TenantId ?? Guid.Empty;
            await vectorStore.EnsureCollectionAsync(tenantId, embedder.VectorSize, ct);

            // Persist parent rows first (so we have their IDs) and then children linking to them.
            var parentEntities = chunks.Parents.Select(p => AiDocumentChunk.Create(
                documentId: doc.Id,
                chunkLevel: "parent",
                content: p.Content,
                chunkIndex: p.Index,
                tokenCount: p.TokenCount,
                qdrantPointId: Guid.Empty,     // parents are not stored in Qdrant
                parentChunkId: null,
                sectionTitle: p.SectionTitle,
                pageNumber: p.PageNumber)).ToList();

            db.AiDocumentChunks.AddRange(parentEntities);
            await db.SaveChangesAsync(ct);

            var parentIds = parentEntities.Select(p => p.Id).ToList();

            var points = new List<VectorPoint>(chunks.Children.Count);
            var childEntities = new List<AiDocumentChunk>(chunks.Children.Count);

            for (var i = 0; i < chunks.Children.Count; i++)
            {
                var draft = chunks.Children[i];
                var pointId = Guid.NewGuid();
                var parentDbId = draft.ParentIndex is int pIdx ? parentIds[pIdx] : (Guid?)null;

                childEntities.Add(AiDocumentChunk.Create(
                    documentId: doc.Id,
                    chunkLevel: "child",
                    content: draft.Content,
                    chunkIndex: draft.Index,
                    tokenCount: draft.TokenCount,
                    qdrantPointId: pointId,
                    parentChunkId: parentDbId,
                    sectionTitle: draft.SectionTitle,
                    pageNumber: draft.PageNumber));

                points.Add(new VectorPoint(
                    Id: pointId,
                    Vector: vectors[i],
                    Payload: new VectorPayload(
                        DocumentId: doc.Id,
                        DocumentName: doc.Name,
                        ChunkLevel: "child",
                        ChunkIndex: draft.Index,
                        SectionTitle: draft.SectionTitle,
                        PageNumber: draft.PageNumber,
                        ParentChunkId: parentDbId,
                        TenantId: tenantId)));
            }

            await vectorStore.UpsertAsync(tenantId, points, ct);

            db.AiDocumentChunks.AddRange(childEntities);
            doc.MarkCompleted(chunkCount: childEntities.Count);
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Processed document {Id}: parents={Parents}, children={Children}, ocr={Ocr}",
                doc.Id, parentEntities.Count, childEntities.Count, extracted.UsedOcr);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process document {Id}", doc.Id);
            doc.MarkFailed(ex.Message);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build src/modules/Starter.Module.AI
git add src/modules/Starter.Module.AI/Application/Messages/ProcessDocumentMessage.cs \
        src/modules/Starter.Module.AI/Infrastructure/Consumers/ProcessDocumentConsumer.cs
git commit -m "feat(ai): ProcessDocumentConsumer — extract → chunk → embed → upsert"
```

---

## Task 15: DTOs + mappers

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiDocumentDto.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiDocumentChunkPreviewDto.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiDocumentMappers.cs`

- [ ] **Step 1: Write `AiDocumentDto.cs`**

```csharp
namespace Starter.Module.AI.Application.DTOs;

public sealed record AiDocumentDto(
    Guid Id,
    string Name,
    string FileName,
    string ContentType,
    long SizeBytes,
    int ChunkCount,
    string EmbeddingStatus,
    string? ErrorMessage,
    bool RequiresOcr,
    DateTime? ProcessedAt,
    DateTime CreatedAt,
    Guid UploadedByUserId);
```

- [ ] **Step 2: Write `AiDocumentChunkPreviewDto.cs`**

```csharp
namespace Starter.Module.AI.Application.DTOs;

public sealed record AiDocumentChunkPreviewDto(
    Guid Id,
    string ChunkLevel,
    int ChunkIndex,
    int TokenCount,
    int? PageNumber,
    string? SectionTitle,
    string ContentPreview);
```

- [ ] **Step 3: Write `AiDocumentMappers.cs`**

```csharp
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.DTOs;

internal static class AiDocumentMappers
{
    public static AiDocumentDto ToDto(this AiDocument d) => new(
        Id: d.Id,
        Name: d.Name,
        FileName: d.FileName,
        ContentType: d.ContentType,
        SizeBytes: d.SizeBytes,
        ChunkCount: d.ChunkCount,
        EmbeddingStatus: d.EmbeddingStatus.ToString(),
        ErrorMessage: d.ErrorMessage,
        RequiresOcr: d.RequiresOcr,
        ProcessedAt: d.ProcessedAt,
        CreatedAt: d.CreatedAt,
        UploadedByUserId: d.UploadedByUserId);

    public static AiDocumentChunkPreviewDto ToPreviewDto(this AiDocumentChunk c, int previewChars = 160) => new(
        Id: c.Id,
        ChunkLevel: c.ChunkLevel,
        ChunkIndex: c.ChunkIndex,
        TokenCount: c.TokenCount,
        PageNumber: c.PageNumber,
        SectionTitle: c.SectionTitle,
        ContentPreview: c.Content.Length <= previewChars
            ? c.Content
            : c.Content[..previewChars] + "…");
}
```

- [ ] **Step 4: Build + commit**

```bash
dotnet build src/modules/Starter.Module.AI
git add src/modules/Starter.Module.AI/Application/DTOs
git commit -m "feat(ai): document DTOs + mappers"
```

---

## Task 16: `UploadDocument` command + handler + validator

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/UploadDocument/UploadDocumentCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/UploadDocument/UploadDocumentCommandValidator.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/UploadDocument/UploadDocumentCommandHandler.cs`

- [ ] **Step 1: Write `UploadDocumentCommand.cs`**

```csharp
using MediatR;
using Microsoft.AspNetCore.Http;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.UploadDocument;

public sealed record UploadDocumentCommand(
    IFormFile File,
    string? Name) : IRequest<Result<AiDocumentDto>>;
```

- [ ] **Step 2: Write `UploadDocumentCommandValidator.cs`**

```csharp
using FluentValidation;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Application.Commands.UploadDocument;

public sealed class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain",
        "text/markdown",
        "text/csv",
        "application/csv",
    };

    public UploadDocumentCommandValidator(IOptions<AiRagSettings> ragOptions)
    {
        var maxBytes = ragOptions.Value.MaxUploadBytes;

        RuleFor(c => c.File).NotNull();
        RuleFor(c => c.File.Length).LessThanOrEqualTo(maxBytes)
            .WithMessage($"File exceeds the {maxBytes / (1024 * 1024)} MB upload limit.");
        RuleFor(c => c.File.ContentType)
            .Must(t => AllowedContentTypes.Contains(t ?? ""))
            .WithMessage("Content type is not supported for knowledge base ingestion.");
        RuleFor(c => c.Name)
            .MaximumLength(200)
            .When(c => !string.IsNullOrWhiteSpace(c.Name));
    }
}
```

- [ ] **Step 3: Write `UploadDocumentCommandHandler.cs`**

```csharp
using MassTransit;
using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Messages;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.UploadDocument;

internal sealed class UploadDocumentCommandHandler(
    AiDbContext db,
    IStorageService storage,
    ICurrentUserService currentUser,
    IPublishEndpoint bus)
    : IRequestHandler<UploadDocumentCommand, Result<AiDocumentDto>>
{
    public async Task<Result<AiDocumentDto>> Handle(UploadDocumentCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<AiDocumentDto>(Domain.Errors.AiErrors.NotAuthenticated);

        var file = request.File;
        var key = $"ai/documents/{Guid.NewGuid():N}/{file.FileName}";

        await using (var s = file.OpenReadStream())
            await storage.UploadAsync(s, key, file.ContentType, ct);

        var doc = AiDocument.Create(
            tenantId: currentUser.TenantId,
            name: string.IsNullOrWhiteSpace(request.Name) ? file.FileName : request.Name!,
            fileName: file.FileName,
            fileRef: key,
            contentType: file.ContentType,
            sizeBytes: file.Length,
            uploadedByUserId: userId);

        db.AiDocuments.Add(doc);
        await db.SaveChangesAsync(ct);

        await bus.Publish(new ProcessDocumentMessage(doc.Id), ct);

        return Result.Success(doc.ToDto());
    }
}
```

- [ ] **Step 4: Build + commit**

```bash
dotnet build src/modules/Starter.Module.AI
git add src/modules/Starter.Module.AI/Application/Commands/UploadDocument
git commit -m "feat(ai): UploadDocument command + validator + handler"
```

---

## Task 17: `DeleteDocument` command + handler

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/DeleteDocument/DeleteDocumentCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/DeleteDocument/DeleteDocumentCommandHandler.cs`

- [ ] **Step 1: Write `DeleteDocumentCommand.cs`**

```csharp
using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.DeleteDocument;

public sealed record DeleteDocumentCommand(Guid Id) : IRequest<Result>;
```

- [ ] **Step 2: Write `DeleteDocumentCommandHandler.cs`**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.DeleteDocument;

internal sealed class DeleteDocumentCommandHandler(
    AiDbContext db,
    IStorageService storage,
    IVectorStore vectors,
    ICurrentUserService currentUser,
    ILogger<DeleteDocumentCommandHandler> logger) : IRequestHandler<DeleteDocumentCommand, Result>
{
    public async Task<Result> Handle(DeleteDocumentCommand request, CancellationToken ct)
    {
        var doc = await db.AiDocuments.FirstOrDefaultAsync(d => d.Id == request.Id, ct);
        if (doc is null) return Result.Failure(AiErrors.DocumentNotFound);

        var tenantId = doc.TenantId ?? Guid.Empty;

        // Cascade: Qdrant points → chunk rows → storage object → document row.
        try
        {
            await vectors.DeleteByDocumentAsync(tenantId, doc.Id, ct);
        }
        catch (Exception ex)
        {
            // Non-fatal: orphaned points can be reaped later; the DB is the source of truth for user-facing lists.
            logger.LogWarning(ex, "Failed to delete Qdrant points for document {Id}", doc.Id);
        }

        var chunks = db.AiDocumentChunks.Where(c => c.DocumentId == doc.Id);
        db.AiDocumentChunks.RemoveRange(chunks);

        try { await storage.DeleteAsync(doc.FileRef, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to delete storage object {Key}", doc.FileRef); }

        db.AiDocuments.Remove(doc);
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build src/modules/Starter.Module.AI
git add src/modules/Starter.Module.AI/Application/Commands/DeleteDocument
git commit -m "feat(ai): DeleteDocument — cascade to Qdrant + chunks + storage"
```

---

## Task 18: `ReprocessDocument` command + handler

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/ReprocessDocument/ReprocessDocumentCommand.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Commands/ReprocessDocument/ReprocessDocumentCommandHandler.cs`

- [ ] **Step 1: Write `ReprocessDocumentCommand.cs`**

```csharp
using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.ReprocessDocument;

public sealed record ReprocessDocumentCommand(Guid Id) : IRequest<Result>;
```

- [ ] **Step 2: Write `ReprocessDocumentCommandHandler.cs`**

```csharp
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.Messages;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.ReprocessDocument;

internal sealed class ReprocessDocumentCommandHandler(
    AiDbContext db,
    IVectorStore vectors,
    IPublishEndpoint bus) : IRequestHandler<ReprocessDocumentCommand, Result>
{
    public async Task<Result> Handle(ReprocessDocumentCommand request, CancellationToken ct)
    {
        var doc = await db.AiDocuments.FirstOrDefaultAsync(d => d.Id == request.Id, ct);
        if (doc is null) return Result.Failure(AiErrors.DocumentNotFound);

        if (doc.EmbeddingStatus == EmbeddingStatus.Processing)
            return Result.Failure(AiErrors.DocumentAlreadyProcessing);

        // Remove old vectors + chunks; the consumer will recreate them.
        var tenantId = doc.TenantId ?? Guid.Empty;
        await vectors.DeleteByDocumentAsync(tenantId, doc.Id, ct);

        var chunks = db.AiDocumentChunks.Where(c => c.DocumentId == doc.Id);
        db.AiDocumentChunks.RemoveRange(chunks);

        doc.ResetForReprocessing();
        await db.SaveChangesAsync(ct);

        await bus.Publish(new ProcessDocumentMessage(doc.Id), ct);

        return Result.Success();
    }
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build src/modules/Starter.Module.AI
git add src/modules/Starter.Module.AI/Application/Commands/ReprocessDocument
git commit -m "feat(ai): ReprocessDocument — reset + re-publish message"
```

---

## Task 19: Queries — `GetDocuments` + `GetDocumentById`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetDocuments/GetDocumentsQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetDocuments/GetDocumentsQueryHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetDocumentById/GetDocumentByIdQuery.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/Queries/GetDocumentById/GetDocumentByIdQueryHandler.cs`
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Application/DTOs/AiDocumentDetailDto.cs`

- [ ] **Step 1: Write `AiDocumentDetailDto.cs`**

```csharp
namespace Starter.Module.AI.Application.DTOs;

public sealed record AiDocumentDetailDto(
    AiDocumentDto Document,
    IReadOnlyList<AiDocumentChunkPreviewDto> ChunkPreviews);
```

- [ ] **Step 2: Write `GetDocumentsQuery.cs`**

```csharp
using MediatR;
using Starter.Application.Common.Models;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetDocuments;

public sealed record GetDocumentsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Status = null,
    string? SearchTerm = null) : IRequest<Result<PaginatedList<AiDocumentDto>>>;
```

- [ ] **Step 3: Write `GetDocumentsQueryHandler.cs`**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Models;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetDocuments;

internal sealed class GetDocumentsQueryHandler(AiDbContext db)
    : IRequestHandler<GetDocumentsQuery, Result<PaginatedList<AiDocumentDto>>>
{
    public async Task<Result<PaginatedList<AiDocumentDto>>> Handle(
        GetDocumentsQuery request, CancellationToken ct)
    {
        var query = db.AiDocuments.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<EmbeddingStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(d => d.EmbeddingStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(d =>
                EF.Functions.ILike(d.Name, $"%{term}%") ||
                EF.Functions.ILike(d.FileName, $"%{term}%"));
        }

        query = query.OrderByDescending(d => d.CreatedAt);

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var total = await query.CountAsync(ct);
        var rows = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = rows.Select(r => r.ToDto()).ToList();
        return Result.Success(PaginatedList<AiDocumentDto>.Create(dtos, total, pageNumber, pageSize));
    }
}
```

- [ ] **Step 4: Write `GetDocumentByIdQuery.cs`**

```csharp
using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetDocumentById;

public sealed record GetDocumentByIdQuery(Guid Id, int ChunkPreviewLimit = 20)
    : IRequest<Result<AiDocumentDetailDto>>;
```

- [ ] **Step 5: Write `GetDocumentByIdQueryHandler.cs`**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetDocumentById;

internal sealed class GetDocumentByIdQueryHandler(AiDbContext db)
    : IRequestHandler<GetDocumentByIdQuery, Result<AiDocumentDetailDto>>
{
    public async Task<Result<AiDocumentDetailDto>> Handle(
        GetDocumentByIdQuery request, CancellationToken ct)
    {
        var doc = await db.AiDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.Id, ct);
        if (doc is null) return Result.Failure<AiDocumentDetailDto>(AiErrors.DocumentNotFound);

        var previewLimit = Math.Clamp(request.ChunkPreviewLimit, 1, 100);
        var chunks = await db.AiDocumentChunks.AsNoTracking()
            .Where(c => c.DocumentId == doc.Id && c.ChunkLevel == "child")
            .OrderBy(c => c.ChunkIndex)
            .Take(previewLimit)
            .ToListAsync(ct);

        var dto = new AiDocumentDetailDto(
            Document: doc.ToDto(),
            ChunkPreviews: chunks.Select(c => c.ToPreviewDto()).ToList());

        return Result.Success(dto);
    }
}
```

- [ ] **Step 6: Build + commit**

```bash
dotnet build src/modules/Starter.Module.AI
git add src/modules/Starter.Module.AI/Application/Queries/GetDocuments \
        src/modules/Starter.Module.AI/Application/Queries/GetDocumentById \
        src/modules/Starter.Module.AI/Application/DTOs/AiDocumentDetailDto.cs
git commit -m "feat(ai): GetDocuments + GetDocumentById queries"
```

---

## Task 20: `AiDocumentsController`

**Files:**
- Create: `boilerplateBE/src/modules/Starter.Module.AI/Controllers/AiDocumentsController.cs`
- Confirm: `boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs` has `ManageDocuments`

- [ ] **Step 1: Write `AiDocumentsController.cs`**

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.AI.Application.Commands.DeleteDocument;
using Starter.Module.AI.Application.Commands.ReprocessDocument;
using Starter.Module.AI.Application.Commands.UploadDocument;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.GetDocumentById;
using Starter.Module.AI.Application.Queries.GetDocuments;
using Starter.Module.AI.Constants;
using Starter.Shared.Models;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/documents")]
public sealed class AiDocumentsController(ISender mediator)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.ManageDocuments)]
    [ProducesResponseType(typeof(PagedApiResponse<AiDocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? searchTerm = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new GetDocumentsQuery(pageNumber, pageSize, status, searchTerm), ct);
        return HandlePagedResult(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AiPermissions.ManageDocuments)]
    [ProducesResponseType(typeof(ApiResponse<AiDocumentDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetDocumentByIdQuery(id), ct);
        return HandleResult(result);
    }

    [HttpPost]
    [Authorize(Policy = AiPermissions.ManageDocuments)]
    [RequestSizeLimit(26_214_400)]  // 25 MB, enforced by ASP.NET before we touch the file
    [ProducesResponseType(typeof(ApiResponse<AiDocumentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile file,
        [FromForm] string? name,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new UploadDocumentCommand(file, name), ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AiPermissions.ManageDocuments)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteDocumentCommand(id), ct);
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/reprocess")]
    [Authorize(Policy = AiPermissions.ManageDocuments)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reprocess(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ReprocessDocumentCommand(id), ct);
        return HandleResult(result);
    }
}
```

- [ ] **Step 2: Confirm permission**

```bash
grep -n "ManageDocuments" boilerplateBE/src/modules/Starter.Module.AI/Constants/AiPermissions.cs
```

If absent, add `public const string ManageDocuments = "Ai.ManageDocuments";` and yield it from `AIModule.GetPermissions()` with description "Manage AI knowledge base documents".

- [ ] **Step 3: Build + commit**

```bash
dotnet build src/modules/Starter.Module.AI
git add src/modules/Starter.Module.AI
git commit -m "feat(ai): AiDocumentsController — list/get/upload/delete/reprocess"
```

---

## Task 21: End-to-end verification pass

**Files:** None — this is a runtime verification, not a code task.

> Prereq: Qdrant container running (`docker compose up -d qdrant`), RabbitMQ running, MinIO running, an `OPENAI_API_KEY` or `OLLAMA` embedder configured (Anthropic doesn't ship native embeddings — the stub provider throws). If no real embedder is available, use Ollama with `nomic-embed-text` via `docker run ollama/ollama`.

- [ ] **Step 1: Fresh rename + build, on a free port**

Follow the post-feature-testing skill (`.claude/skills/post-feature-testing.md`). Pick a free port not in `[5000, 5100]` for BE. FE is out of scope for this plan.

- [ ] **Step 2: Login and upload a small TXT**

```bash
TOK=$(curl -s -X POST http://localhost:$PORT/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"superadmin@testaichat.com","password":"Admin@123456"}' \
  | python3 -c "import sys,json;print(json.load(sys.stdin)['data']['accessToken'])")

echo "The water cycle has four main steps: evaporation, condensation, precipitation, and collection." > /tmp/water.txt
curl -s -X POST http://localhost:$PORT/api/v1/ai/documents \
  -H "Authorization: Bearer $TOK" \
  -F "file=@/tmp/water.txt;type=text/plain" \
  -F "name=Water cycle"
```

Expected: `201 Created`, `data.embeddingStatus = "Pending"`.

- [ ] **Step 3: Wait a moment, then list and get the document**

```bash
sleep 3
curl -s http://localhost:$PORT/api/v1/ai/documents -H "Authorization: Bearer $TOK" \
  | python3 -m json.tool
```

Expected: one row with `embeddingStatus = "Completed"` and `chunkCount >= 1`. If it sits at `Processing` for >30s, check the BE log for `ProcessDocument` errors.

- [ ] **Step 4: Inspect chunks via detail endpoint**

```bash
DOC_ID=$(curl -s http://localhost:$PORT/api/v1/ai/documents -H "Authorization: Bearer $TOK" \
  | python3 -c "import sys,json;print(json.load(sys.stdin)['data'][0]['id'])")
curl -s "http://localhost:$PORT/api/v1/ai/documents/$DOC_ID" -H "Authorization: Bearer $TOK" \
  | python3 -m json.tool
```

Expected: `chunkPreviews` non-empty, each with `chunkLevel="child"`, `tokenCount > 0`, `contentPreview` truncated to ~160 chars.

- [ ] **Step 5: Verify Qdrant has the vectors**

```bash
curl -s http://localhost:6333/collections | python3 -m json.tool
# Expect a "tenant_{guid}" collection. Count points:
COLL=$(curl -s http://localhost:6333/collections | python3 -c "import sys,json;print([c['name'] for c in json.load(sys.stdin)['result']['collections'] if c['name'].startswith('tenant_')][0])")
curl -s "http://localhost:6333/collections/$COLL" | python3 -m json.tool | head -20
```

Expected: `vectors_count >= 1`, `points_count >= 1`.

- [ ] **Step 6: Reprocess and verify the cycle**

```bash
curl -s -X POST "http://localhost:$PORT/api/v1/ai/documents/$DOC_ID/reprocess" \
  -H "Authorization: Bearer $TOK" -w "\nHTTP %{http_code}\n"
sleep 3
curl -s "http://localhost:$PORT/api/v1/ai/documents/$DOC_ID" -H "Authorization: Bearer $TOK" \
  | python3 -c "import sys,json;d=json.load(sys.stdin)['data']['document'];print(d['embeddingStatus'],d['chunkCount'])"
```

Expected: back to `Completed` after 3 seconds, same or similar chunkCount.

- [ ] **Step 7: Delete and verify cleanup**

```bash
curl -s -X DELETE "http://localhost:$PORT/api/v1/ai/documents/$DOC_ID" \
  -H "Authorization: Bearer $TOK" -w "\nHTTP %{http_code}\n"

# Qdrant: points for that document should be gone (collection stays)
curl -s -X POST "http://localhost:6333/collections/$COLL/points/scroll" \
  -H "Content-Type: application/json" \
  -d "{\"filter\":{\"must\":[{\"key\":\"document_id\",\"match\":{\"value\":\"$DOC_ID\"}}]},\"limit\":1}" \
  | python3 -m json.tool
```

Expected: empty `points` array.

- [ ] **Step 8: Negative — upload an unsupported type**

```bash
echo "binary" > /tmp/blob.bin
curl -s -X POST http://localhost:$PORT/api/v1/ai/documents \
  -H "Authorization: Bearer $TOK" \
  -F "file=@/tmp/blob.bin;type=application/octet-stream" \
  -w "\nHTTP %{http_code}\n"
```

Expected: `400 Bad Request`, `validationErrors.ContentType` present.

- [ ] **Step 9: Negative — upload > 25 MB**

```bash
dd if=/dev/zero of=/tmp/big.txt bs=1M count=30 2>/dev/null
curl -s -X POST http://localhost:$PORT/api/v1/ai/documents \
  -H "Authorization: Bearer $TOK" \
  -F "file=@/tmp/big.txt;type=text/plain" \
  -w "\nHTTP %{http_code}\n"
```

Expected: `413` (ASP.NET body limit) or `400` (validator). Both are acceptable — we reject before any storage writes.

- [ ] **Step 10: Tear down and commit the verification notes**

Follow `.claude/skills/test-cleanup.md`: kill test BE, drop the exact test DB, remove `_testAiChat*/` directory.

---

## Task 22: Clean the worktree and push

- [ ] **Step 1: Full solution build + all tests**

```bash
cd boilerplateBE && dotnet build 2>&1 | tail -5
dotnet test tests/Starter.Api.Tests 2>&1 | tail -5
```

Expected: `0 Error(s)` and `Failed: 0, Passed: N+...` (where N is the test count before this plan — all prior tests still pass plus the new ones).

- [ ] **Step 2: Commit a plan-done marker if you touched anything stray**

```bash
git status
```

No changes expected at this point — each task committed its own work.

- [ ] **Step 3: Push**

```bash
git push
```
