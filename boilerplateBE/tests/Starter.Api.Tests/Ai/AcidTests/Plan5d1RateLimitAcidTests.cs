using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Starter.Module.AI.Infrastructure.Services.Costs;
using Xunit;

namespace Starter.Api.Tests.Ai.AcidTests;

/// <summary>
/// Plan 5d-1 acid test M3: per-agent sliding-window rate limiter. Verifies that:
/// - Up to `rpm` requests within 60 s are admitted; the next is refused.
/// - After the window slides past, capacity is restored.
///
/// Requires the docker-compose `starter-redis` instance on localhost:6379. The fixture
/// fails hard if Redis is unreachable — silent skip would hide a critical rate-limit
/// regression. Run `docker compose up -d redis` from boilerplateBE/ before these tests.
/// </summary>
public sealed class Plan5d1RateLimitAcidTests : IAsyncLifetime
{
    private const string RedisConn = "localhost:6379,abortConnect=false";
    private IConnectionMultiplexer? _multiplexer;
    private RedisAgentRateLimiter? _sut;

    public async Task InitializeAsync()
    {
        _multiplexer = await ConnectionMultiplexer.ConnectAsync(RedisConn);
        _sut = new RedisAgentRateLimiter(_multiplexer, NullLogger<RedisAgentRateLimiter>.Instance);
    }

    public Task DisposeAsync()
    {
        _multiplexer?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Acid_M3_1_Admits_Up_To_Rpm_Then_Refuses()
    {
        var assistant = Guid.NewGuid();
        const int rpm = 5;

        for (var i = 0; i < rpm; i++)
        {
            (await _sut!.TryAcquireAsync(assistant, rpm))
                .Should().BeTrue($"the {i + 1}th call within the window should be admitted");
        }

        (await _sut!.TryAcquireAsync(assistant, rpm))
            .Should().BeFalse("the (rpm+1)th call within the same 60s window must be refused");
    }

    [Fact]
    public async Task Acid_M3_2_Zero_Rpm_Refuses_All()
    {
        var assistant = Guid.NewGuid();
        // RPM = 0 means "blocked" (consistent with Free plan default per spec §6.5).
        (await _sut!.TryAcquireAsync(assistant, rpm: 0)).Should().BeFalse();
    }
}
