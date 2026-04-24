using FluentAssertions;
using Starter.Application.Common.Events;
using Starter.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace Starter.Api.Tests.MassTransit;

/// <summary>
/// Unit tests for <see cref="IntegrationEventCollector"/> — the scoped accumulator
/// that handlers use to schedule integration events for transactional outbox delivery.
/// </summary>
public sealed class IntegrationEventCollectorTests
{
    private static IntegrationEventCollector CreateSut() => new();

    [Fact]
    public void Schedule_Adds_Event_And_TakeAll_Returns_It()
    {
        var sut = CreateSut();
        var evt = new TenantRegisteredEvent(
            Guid.NewGuid(), "Acme", "acme", Guid.NewGuid(), DateTime.UtcNow);

        sut.Schedule(evt);
        var result = sut.TakeAll();

        result.Should().HaveCount(1);
        result[0].Event.Should().BeSameAs(evt);
        result[0].EventType.Should().Be(typeof(TenantRegisteredEvent));
    }

    [Fact]
    public void TakeAll_Clears_The_List()
    {
        var sut = CreateSut();
        sut.Schedule(new TenantRegisteredEvent(
            Guid.NewGuid(), "X", "x", Guid.NewGuid(), DateTime.UtcNow));

        _ = sut.TakeAll();
        var second = sut.TakeAll();

        second.Should().BeEmpty();
    }

    [Fact]
    public void Multiple_Scheduled_Events_Returned_In_Order()
    {
        var sut = CreateSut();
        var e1 = new TenantRegisteredEvent(Guid.NewGuid(), "A", "a", Guid.NewGuid(), DateTime.UtcNow);
        var e2 = new TenantRegisteredEvent(Guid.NewGuid(), "B", "b", Guid.NewGuid(), DateTime.UtcNow);

        sut.Schedule(e1);
        sut.Schedule(e2);
        var result = sut.TakeAll();

        result.Should().HaveCount(2);
        result[0].Event.Should().BeSameAs(e1);
        result[1].Event.Should().BeSameAs(e2);
    }

    [Fact]
    public void Schedule_After_TakeAll_Works_For_New_Events()
    {
        var sut = CreateSut();
        sut.Schedule(new TenantRegisteredEvent(
            Guid.NewGuid(), "First", "first", Guid.NewGuid(), DateTime.UtcNow));
        _ = sut.TakeAll();

        var second = new TenantRegisteredEvent(
            Guid.NewGuid(), "Second", "second", Guid.NewGuid(), DateTime.UtcNow);
        sut.Schedule(second);
        var result = sut.TakeAll();

        result.Should().HaveCount(1);
        result[0].Event.Should().BeSameAs(second);
    }

    [Fact]
    public void EventType_Matches_Concrete_Type_Of_Scheduled_Event()
    {
        var sut = CreateSut();
        sut.Schedule(new TenantRegisteredEvent(
            Guid.NewGuid(), "X", "x", Guid.NewGuid(), DateTime.UtcNow));

        var result = sut.TakeAll();

        result[0].EventType.Should().Be(typeof(TenantRegisteredEvent));
    }
}
