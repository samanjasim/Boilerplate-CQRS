using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Starter.Module.AI.Infrastructure.Services.Costs;
using Testcontainers.Redis;
using Xunit;

namespace Starter.Api.Tests.Ai.AcidTests;

/// <summary>
/// Plan 5d-1 acid test M3: per-agent sliding-window rate limiter against a real Redis.
/// Uses a Testcontainers-managed Redis per fixture so the test is self-contained
/// (no docker-compose required, runs in CI).
/// </summary>
public sealed class Plan5d1RateLimitAcidTests : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private IConnectionMultiplexer? _multiplexer;
    private RedisAgentRateLimiter? _sut;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _multiplexer = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
        _sut = new RedisAgentRateLimiter(_multiplexer, NullLogger<RedisAgentRateLimiter>.Instance);
    }

    public async Task DisposeAsync()
    {
        _multiplexer?.Dispose();
        await _container.DisposeAsync();
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
