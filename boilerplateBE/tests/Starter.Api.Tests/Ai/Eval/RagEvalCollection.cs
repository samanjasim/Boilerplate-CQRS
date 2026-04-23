using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qdrant.Client;
using Starter.Api.Tests.Ai.Retrieval;
using Starter.Application.Common.Interfaces;
using Xunit;

namespace Starter.Api.Tests.Ai.Eval;

[CollectionDefinition(RagEvalCollectionDef.Name, DisableParallelization = true)]
public sealed class RagEvalCollectionDef : ICollectionFixture<RagEvalFixture>
{
    public const string Name = "RagEval";
}

public sealed class RagEvalFixture : IAsyncLifetime
{
    private static readonly TimeSpan OrphanMaxAge = TimeSpan.FromHours(24);

    private static bool Enabled =>
        Environment.GetEnvironmentVariable("AI_EVAL_ENABLED") == "1";

    private AiPostgresFixture? _postgres;
    private QdrantClient? _qdrant;

    public AiPostgresFixture Postgres => _postgres!;
    public QdrantClient Qdrant => _qdrant!;

    public async Task InitializeAsync()
    {
        if (!Enabled) return;
        _postgres = new AiPostgresFixture();
        _qdrant = new QdrantClient("localhost");
        await _postgres.InitializeAsync();
        await CleanupOrphanCollectionsAsync();
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null) await _postgres.DisposeAsync();
        _qdrant?.Dispose();
    }

    public IServiceProvider BuildEvalServiceProvider()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            // Pull provider API keys from the Starter.Api project's user-secrets
            // store so live eval runs don't require exporting env vars.
            .AddUserSecrets<Starter.Api.Program>(optional: true)
            .AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = Postgres.ConnectionString,
                ["AI:Rag:Eval:Enabled"] = "true"
            })
            .Build();
        Starter.Api.Program.ConfigureServicesForTooling(services, config);
        services.AddSingleton<ICurrentUserService>(new EvalAdminCurrentUser());
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Loads a rerank-cache blob (produced by the EvalCacheWarmup tool) into the
    /// <see cref="IDistributedCache"/> the harness will pick up. Keys and JSON-encoded
    /// values are written directly so the production rerankers see a byte-identical
    /// cache hit and don't need to call the live reranker — making eval runs
    /// deterministic across machines.
    /// </summary>
    public async Task SeedRerankCacheAsync(IServiceProvider sp, string fixtureFileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Ai", "Eval", "fixtures", fixtureFileName);
        if (!File.Exists(path)) return;

        var json = await File.ReadAllTextAsync(path);
        var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (entries is null || entries.Count == 0) return;

        var cache = sp.GetRequiredService<IDistributedCache>();
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
        };
        foreach (var (key, value) in entries)
            await cache.SetStringAsync(key, value, options);
    }

    private async Task CleanupOrphanCollectionsAsync()
    {
        var collections = await Qdrant.ListCollectionsAsync();
        var cutoff = DateTimeOffset.UtcNow - OrphanMaxAge;

        foreach (var c in collections)
        {
            if (!OrphanCollectionFilter.TryParseHarnessCollectionAge(c, out var createdAt)) continue;
            if (createdAt > cutoff) continue;
            try { await Qdrant.DeleteCollectionAsync(c); } catch { /* best-effort */ }
        }
    }

    private sealed class EvalAdminCurrentUser : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? Email => "eval@harness.local";
        public bool IsAuthenticated => true;
        public IEnumerable<string> Roles => ["Admin"];
        public IEnumerable<string> Permissions => [];
        public Guid? TenantId { get; } = Guid.NewGuid();
        public bool IsInRole(string role) => role == "Admin";
        public bool HasPermission(string permission) => true;
    }
}
