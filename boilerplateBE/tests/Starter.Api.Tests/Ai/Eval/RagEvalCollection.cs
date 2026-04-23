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

    private async Task CleanupOrphanCollectionsAsync()
    {
        var collections = await Qdrant.ListCollectionsAsync();
        foreach (var c in collections)
        {
            if (!c.StartsWith("eval-") && !c.StartsWith("warmup-")) continue;
            try { await Qdrant.DeleteCollectionAsync(c); } catch { }
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
