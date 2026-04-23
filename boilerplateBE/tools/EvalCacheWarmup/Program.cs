using EvalCacheWarmup;
using MessagePack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

// ── Replace IReranker with CapturingReranker decorator ─────────────────────────
var innerDesc = builder.Services.Last(d => d.ServiceType == typeof(IReranker));
builder.Services.Remove(innerDesc);

CapturingReranker? capturingInstance = null;
builder.Services.AddScoped<IReranker>(sp =>
{
    if (capturingInstance is null)
    {
        var inner = (IReranker)ActivatorUtilities.CreateInstance(sp, innerDesc.ImplementationType!);
        capturingInstance = new CapturingReranker(inner);
    }
    return capturingInstance;
});

// ── Run ingest + retrieval queries ─────────────────────────────────────────────
using var host = builder.Build();
using var scope = host.Services.CreateScope();
var sp = scope.ServiceProvider;

var ingester = sp.GetRequiredService<EvalFixtureIngester>();
var retrieval = sp.GetRequiredService<IRagRetrievalService>();
var vectors = sp.GetRequiredService<IVectorStore>();

var syntheticTenantId = Guid.NewGuid();
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
    catch { /* best-effort cleanup */ }
}

// ── Serialise captured scores ──────────────────────────────────────────────────
var captured = capturingInstance?.Captured ?? new Dictionary<string, decimal>();
await File.WriteAllBytesAsync(outPath,
    MessagePackSerializer.Serialize(captured,
        MessagePack.Resolvers.ContractlessStandardResolver.Options));

Console.Error.WriteLine($"wrote {captured.Count} entries to {outPath}");
return 0;
