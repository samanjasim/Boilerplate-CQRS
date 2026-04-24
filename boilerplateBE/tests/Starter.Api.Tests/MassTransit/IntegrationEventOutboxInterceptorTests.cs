using FluentAssertions;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Starter.Application.Common.Events;
using Starter.Infrastructure.Persistence;
using Starter.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace Starter.Api.Tests.MassTransit;

/// <summary>
/// Unit tests for <see cref="IntegrationEventOutboxInterceptor"/> — the EF Core
/// <see cref="SaveChangesInterceptor"/> that writes pending integration events into
/// the ApplicationDbContext MassTransit outbox table atomically with business data.
/// </summary>
public sealed class IntegrationEventOutboxInterceptorTests
{
    private static TenantRegisteredEvent SampleEvent() =>
        new(Guid.NewGuid(), "Acme", "acme", Guid.NewGuid(), DateTime.UtcNow);

    // DbContextEventData is not used by our override or the base implementation
    // (base just returns the result parameter), so null is safe here.
    private static DbContextEventData NullEventData => null!;

    [Fact]
    public async Task SavingChangesAsync_WithNoPendingEvents_NeverAccessesServiceProvider()
    {
        var collector = new IntegrationEventCollector();
        // Empty collector — no events scheduled

        // Strict mock: any call to IServiceProvider throws, proving we never touch it
        var mockSp = new Mock<IServiceProvider>(MockBehavior.Strict);

        var interceptor = new IntegrationEventOutboxInterceptor(collector, mockSp.Object);

        var act = async () => await interceptor.SavingChangesAsync(
            NullEventData, default, CancellationToken.None);

        await act.Should().NotThrowAsync(
            "no events are pending so the outbox provider must never be resolved");
    }

    [Fact]
    public async Task SavingChangesAsync_WithPendingEvents_ResolvesApplicationDbContextProvider()
    {
        var collector = new IntegrationEventCollector();
        collector.Schedule(SampleEvent());

        // Set up the service provider to return null for the concrete provider type;
        // GetRequiredService<T> throws InvalidOperationException when GetService returns null,
        // which confirms the interceptor tried to resolve the correct type.
        var mockSp = new Mock<IServiceProvider>();
        mockSp
            .Setup(sp => sp.GetService(
                typeof(EntityFrameworkScopedBusContextProvider<IBus, ApplicationDbContext>)))
            .Returns(null!);

        var interceptor = new IntegrationEventOutboxInterceptor(collector, mockSp.Object);

        // The interceptor will throw because the provider resolved to null —
        // that's expected here; we're verifying it *tried* to resolve the right type.
        var act = async () => await interceptor.SavingChangesAsync(
            NullEventData, default, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "GetRequiredService throws when IServiceProvider.GetService returns null");

        mockSp.Verify(
            sp => sp.GetService(
                typeof(EntityFrameworkScopedBusContextProvider<IBus, ApplicationDbContext>)),
            Times.Once,
            "the interceptor must resolve the ApplicationDbContext-specific outbox provider");
    }

    [Fact]
    public async Task SavingChangesAsync_WithPendingEvents_DrainedFromCollectorBeforePublishAttempt()
    {
        // Even when publishing fails (e.g. provider unavailable), TakeAll must have
        // been called first — so a retry of the business transaction starts clean.
        var collector = new IntegrationEventCollector();
        collector.Schedule(SampleEvent());
        collector.Schedule(SampleEvent());

        var mockSp = new Mock<IServiceProvider>();
        mockSp
            .Setup(sp => sp.GetService(
                typeof(EntityFrameworkScopedBusContextProvider<IBus, ApplicationDbContext>)))
            .Returns(null!);

        var interceptor = new IntegrationEventOutboxInterceptor(collector, mockSp.Object);

        try
        {
            await interceptor.SavingChangesAsync(NullEventData, default, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // Expected — provider resolution fails in this isolated unit-test context
        }

        // Collector must be empty: TakeAll was called before any publish attempt
        collector.TakeAll().Should().BeEmpty(
            "TakeAll is called before the provider is resolved, so the collector is " +
            "always drained even if publishing subsequently throws");
    }

    [Fact]
    public async Task SavingChangesAsync_WithNoPendingEvents_ReturnsPassedResult()
    {
        var collector = new IntegrationEventCollector();
        var mockSp = new Mock<IServiceProvider>(MockBehavior.Strict);
        var interceptor = new IntegrationEventOutboxInterceptor(collector, mockSp.Object);

        var expected = InterceptionResult<int>.SuppressWithResult(42);
        var actual = await interceptor.SavingChangesAsync(NullEventData, expected, CancellationToken.None);

        actual.Should().Be(expected,
            "when there is nothing to publish the interceptor must pass the result through unchanged");
    }
}
