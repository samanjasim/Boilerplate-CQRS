using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Module.AI.Infrastructure.Services.Costs;
using Xunit;

namespace Starter.Api.Tests.Ai.AcidTests;

/// <summary>
/// Plan 5d-1 acid test M2: atomic cost-cap claims. Verifies the Redis Lua script
/// holds under concurrent load — N parallel claims against a cap of K never exceed K.
///
/// Connects to the docker-compose redis on localhost:6379 (`starter-redis`). Skipped
/// when redis is unreachable (CI without docker).
/// </summary>
public sealed class Plan5d1ConcurrentCostCapAcidTests : IAsyncLifetime
{
    private const string RedisConn = "localhost:6379,abortConnect=false";
    private IConnectionMultiplexer? _multiplexer;
    private RedisCostCapAccountant? _sut;

    public async Task InitializeAsync()
    {
        // Plan 5d-1 acid tests require the docker-compose `starter-redis` instance on
        // localhost:6379. We fail hard if Redis is unreachable rather than silently skip:
        // a missing instance means cap enforcement is untested and CI MUST surface that.
        // Run `docker compose up -d redis` from boilerplateBE/ before these tests.
        _multiplexer = await ConnectionMultiplexer.ConnectAsync(RedisConn);
        _sut = new RedisCostCapAccountant(_multiplexer, NullLogger<RedisCostCapAccountant>.Instance);
    }

    public Task DisposeAsync()
    {
        _multiplexer?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Acid_M2_1_Concurrent_Claims_Never_Exceed_Cap()
    {
        var tenant = Guid.NewGuid();
        var assistant = Guid.NewGuid();
        const decimal cap = 5m;
        const int totalClaims = 10;
        const decimal perClaim = 1m;

        // 10 parallel claims of $1 against a $5 cap → exactly 5 should be granted.
        var tasks = Enumerable.Range(0, totalClaims)
            .Select(_ => _sut!.TryClaimAsync(tenant, assistant, perClaim, CapWindow.Monthly, cap))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        results.Count(r => r.Granted).Should().Be(5, "cap = $5 / claim = $1 → exactly 5 succeed");
        results.Count(r => !r.Granted).Should().Be(5);
        var current = await _sut!.GetCurrentAsync(tenant, assistant, CapWindow.Monthly);
        current.Should().Be(5m, "atomic claim must not exceed the cap");
    }

    [Fact]
    public async Task Acid_M2_2_Rollback_Restores_Counter()
    {
        var tenant = Guid.NewGuid();
        var assistant = Guid.NewGuid();

        var first = await _sut!.TryClaimAsync(tenant, assistant, 1m, CapWindow.Monthly, 5m);
        first.Granted.Should().BeTrue();

        await _sut.RollbackClaimAsync(tenant, assistant, 1m, CapWindow.Monthly);

        var afterRollback = await _sut.GetCurrentAsync(tenant, assistant, CapWindow.Monthly);
        afterRollback.Should().Be(0m);
    }

    [Fact]
    public async Task Acid_M2_3_RecordActual_Adjusts_Counter_By_Delta()
    {
        var tenant = Guid.NewGuid();
        var assistant = Guid.NewGuid();

        await _sut!.TryClaimAsync(tenant, assistant, 2m, CapWindow.Monthly, 10m);
        // Estimated $2, actual $1.50 → delta = -0.50
        await _sut.RecordActualAsync(tenant, assistant, deltaUsd: -0.5m, CapWindow.Monthly);

        var current = await _sut.GetCurrentAsync(tenant, assistant, CapWindow.Monthly);
        current.Should().Be(1.5m);
    }

    [Fact]
    public async Task Acid_M2_4_Refusal_Does_Not_Increment_Counter()
    {
        var tenant = Guid.NewGuid();
        var assistant = Guid.NewGuid();

        await _sut!.TryClaimAsync(tenant, assistant, 4m, CapWindow.Monthly, 5m);
        var refused = await _sut.TryClaimAsync(tenant, assistant, 2m, CapWindow.Monthly, 5m);

        refused.Granted.Should().BeFalse();
        refused.CurrentUsd.Should().Be(4m); // unchanged after refusal
        var current = await _sut.GetCurrentAsync(tenant, assistant, CapWindow.Monthly);
        current.Should().Be(4m);
    }
}
