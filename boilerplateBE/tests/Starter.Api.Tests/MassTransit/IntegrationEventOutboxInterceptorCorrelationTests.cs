using System.Diagnostics;
using FluentAssertions;
using Starter.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace Starter.Api.Tests.MassTransit;

/// <summary>
/// Correlation-propagation tests for <see cref="IntegrationEventOutboxInterceptor"/>.
///
/// The interceptor derives a stable <c>ConversationId</c> from <c>Activity.Current</c>
/// so all events scheduled within the same HTTP request share a correlation
/// identifier visible to consumers, logs, and MT's own tracing. These tests
/// exercise the pure projection without spinning up a real bus.
/// </summary>
public sealed class IntegrationEventOutboxInterceptorCorrelationTests
{
    [Fact]
    public void DeriveConversationIdFromActivity_ReturnsNull_WhenNoAmbientActivity()
    {
        // Fresh Activity.Current (ambient context) is preserved across async flow;
        // use AsyncLocal reset pattern to be safe.
        Activity.Current = null;

        var id = IntegrationEventOutboxInterceptor.DeriveConversationIdFromActivity();

        id.Should().BeNull(
            "background/hosted-service code has no HTTP trace — the interceptor " +
            "falls back to MassTransit's default Guid generation");
    }

    [Fact]
    public void DeriveConversationIdFromActivity_IsStable_WithinTheSameActivity()
    {
        using var activity = new Activity("request").Start();

        var first = IntegrationEventOutboxInterceptor.DeriveConversationIdFromActivity();
        var second = IntegrationEventOutboxInterceptor.DeriveConversationIdFromActivity();

        first.Should().NotBeNull();
        second.Should().Be(first,
            "two events scheduled within the same HTTP request must share a ConversationId");
    }

    [Fact]
    public void DeriveConversationIdFromActivity_DiffersBetweenActivities()
    {
        Guid? first;
        Guid? second;

        using (var a1 = new Activity("req1").Start())
            first = IntegrationEventOutboxInterceptor.DeriveConversationIdFromActivity();

        using (var a2 = new Activity("req2").Start())
            second = IntegrationEventOutboxInterceptor.DeriveConversationIdFromActivity();

        first.HasValue.Should().BeTrue();
        second.HasValue.Should().BeTrue();
        first!.Value.Should().NotBe(second!.Value,
            "each HTTP request must produce a distinct ConversationId so traces don't merge");
    }

    [Fact]
    public void DeriveConversationIdFromTraceId_IsDeterministic()
    {
        var traceId = ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c".AsSpan());

        var first = IntegrationEventOutboxInterceptor.DeriveConversationIdFromTraceId(traceId);
        var second = IntegrationEventOutboxInterceptor.DeriveConversationIdFromTraceId(traceId);

        first.Should().Be(second,
            "projection from TraceId to Guid must be a pure function");
    }

    [Fact]
    public void DeriveConversationIdFromTraceId_DifferentTraceIds_ProduceDifferentGuids()
    {
        var t1 = ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c".AsSpan());
        var t2 = ActivityTraceId.CreateFromString("aaaa1111bbbb2222cccc3333dddd4444".AsSpan());

        var g1 = IntegrationEventOutboxInterceptor.DeriveConversationIdFromTraceId(t1);
        var g2 = IntegrationEventOutboxInterceptor.DeriveConversationIdFromTraceId(t2);

        g1.Should().NotBe(g2);
    }
}
