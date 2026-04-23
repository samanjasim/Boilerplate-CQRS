using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

/// <summary>
/// Provides an AI-module Postgres database for integration tests. If the
/// environment variable <c>STARTER_TEST_PG_CONN</c> is set the fixture uses
/// that connection string directly (recommended for local development against
/// a locally-installed Postgres). Otherwise it falls back to a disposable
/// Testcontainers container.
/// </summary>
public sealed class AiPostgresFixture : IAsyncLifetime
{
    private const string EnvConnString = "STARTER_TEST_PG_CONN";

    private readonly PostgreSqlContainer? _container;
    private readonly string? _externalConnString;
    private readonly string? _externalDatabase;

    public AiPostgresFixture()
    {
        var envConn = Environment.GetEnvironmentVariable(EnvConnString);
        if (!string.IsNullOrWhiteSpace(envConn))
        {
            // Namespace each run so parallel test assemblies never collide on the
            // same database. The fixture drops this database in DisposeAsync.
            var builder = new NpgsqlConnectionStringBuilder(envConn);
            _externalDatabase = $"starter_test_{Guid.NewGuid():N}";
            builder.Database = _externalDatabase;
            _externalConnString = builder.ToString();
        }
        else
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("ai_tests")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();
        }
    }

    public string ConnectionString =>
        _externalConnString ?? _container!.GetConnectionString();

    public async Task InitializeAsync()
    {
        if (_container is not null)
        {
            await _container.StartAsync();
        }
        else
        {
            await CreateDatabaseAsync();
        }

        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        if (_externalConnString is not null && _externalDatabase is not null)
            await DropDatabaseAsync();
    }

    public AiDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AiDbContext(options, currentUserService: null);
    }

    public Microsoft.Extensions.Logging.ILogger<T> Logger<T>() => NullLogger<T>.Instance;

    public async Task<(AiDocument Document, AiDocumentChunk Chunk)> SeedChunkAsync(
        Guid tenantId,
        string content,
        string normalizedContent,
        string? sectionTitle = null)
    {
        await using var db = CreateDbContext();
        var doc = AiDocument.Create(
            tenantId: tenantId,
            name: "seed",
            fileName: "seed.txt",
            fileId: Guid.NewGuid(),
            contentType: "text/plain",
            sizeBytes: content.Length,
            uploadedByUserId: Guid.NewGuid());
        db.AiDocuments.Add(doc);
        var chunk = AiDocumentChunk.Create(
            documentId: doc.Id,
            chunkLevel: "child",
            content: content,
            chunkIndex: 0,
            tokenCount: 1,
            qdrantPointId: Guid.NewGuid(),
            sectionTitle: sectionTitle);
        chunk.SetNormalizedContent(normalizedContent);
        db.AiDocumentChunks.Add(chunk);
        await db.SaveChangesAsync();
        return (doc, chunk);
    }

    private async Task CreateDatabaseAsync()
    {
        var admin = new NpgsqlConnectionStringBuilder(_externalConnString!) { Database = "postgres" };
        await using var conn = new NpgsqlConnection(admin.ToString());
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            $"CREATE DATABASE \"{_externalDatabase}\"", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task DropDatabaseAsync()
    {
        var admin = new NpgsqlConnectionStringBuilder(_externalConnString!) { Database = "postgres" };
        await using var conn = new NpgsqlConnection(admin.ToString());
        await conn.OpenAsync();
        // Terminate any leftover sessions so DROP can proceed.
        await using (var kill = new NpgsqlCommand(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
            "WHERE datname = @db AND pid <> pg_backend_pid()", conn))
        {
            kill.Parameters.AddWithValue("db", _externalDatabase!);
            await kill.ExecuteNonQueryAsync();
        }
        await using var cmd = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{_externalDatabase}\"", conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
