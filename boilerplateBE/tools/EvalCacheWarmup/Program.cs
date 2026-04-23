using System.Text.Json;
using EvalCacheWarmup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Eval.Fixtures;

// ── Argument parsing ───────────────────────────────────────────────────────────
var argMap = new Dictionary<string, string>();
for (var i = 0; i < args.Length - 1; i++)
    if (args[i].StartsWith("--")) argMap[args[i][2..]] = args[i + 1];

if (!argMap.TryGetValue("fixture", out var fixturePath) ||
    !argMap.TryGetValue("out", out var outPath))
{
    Console.Error.WriteLine("usage: EvalCacheWarmup --fixture <path> --out <path>");
    return 1;
}

// ── Load fixture ───────────────────────────────────────────────────────────────
var datasetResult = EvalFixtureLoader.LoadFromFile(fixturePath);
if (datasetResult.IsFailure)
{
    Console.Error.WriteLine($"fixture load failed: {datasetResult.Error.Code}");
    return 2;
}
var dataset = datasetResult.Value;

// ── Build host ─────────────────────────────────────────────────────────────────
var builder = Host.CreateApplicationBuilder(args);
builder.Configuration
    .AddJsonFile("../../src/Starter.Api/appsettings.json", optional: false)
    .AddJsonFile("../../src/Starter.Api/appsettings.Development.json", optional: true);

Starter.Api.Program.ConfigureServicesForTooling(builder.Services, builder.Configuration);

// ── Decorate ICacheService so we can capture what the production rerankers write ──
CacheRecordingService? recorder = null;
var existing = builder.Services.Last(d => d.ServiceType == typeof(ICacheService));
builder.Services.Remove(existing);
builder.Services.AddSingleton<ICacheService>(sp =>
{
    if (recorder is not null) return recorder;
    var inner = (ICacheService)ActivatorUtilities.CreateInstance(sp, existing.ImplementationType!);
    recorder = new CacheRecordingService(inner);
    return recorder;
});

// ── Run ingest + retrieval queries against synthetic tenant ────────────────────
using var host = builder.Build();
using var scope = host.Services.CreateScope();
var sp = scope.ServiceProvider;

var ingester = sp.GetRequiredService<EvalFixtureIngester>();
var retrieval = sp.GetRequiredService<IRagRetrievalService>();
var vectors = sp.GetRequiredService<IVectorStore>();

var syntheticTenantId = Guid.CreateVersion7();
var uploaderId = Guid.NewGuid();

try
{
    Console.Error.WriteLine($"Ingesting {dataset.Documents.Count} document(s) into synthetic tenant {syntheticTenantId}...");
    await ingester.IngestAsync(syntheticTenantId, uploaderId, dataset, CancellationToken.None);

    Console.Error.WriteLine($"Running {dataset.Questions.Count} retrieval query/queries...");
    foreach (var q in dataset.Questions)
        await retrieval.RetrieveForQueryAsync(
            syntheticTenantId, q.Query, null, 20, null, true, CancellationToken.None);
}
finally
{
    try { await vectors.DropCollectionAsync(syntheticTenantId, CancellationToken.None); }
    catch { /* best-effort */ }
}

// ── Persist recorded blob (stable-ordered JSON, byte-exact with production) ────
var captured = recorder?.Captured ?? new Dictionary<string, string>();
var ordered = captured.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                      .ToDictionary(kv => kv.Key, kv => kv.Value);

var outDir = Path.GetDirectoryName(outPath);
if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
await File.WriteAllTextAsync(
    outPath,
    JsonSerializer.Serialize(ordered, new JsonSerializerOptions { WriteIndented = true }));

Console.Error.WriteLine($"wrote {ordered.Count} entries to {outPath}");
return 0;
