using FluentAssertions;
using NetArchTest.Rules;
using Starter.Application.Common.Interfaces;
using Xunit;

namespace Starter.Api.Tests.Architecture;

/// <summary>
/// Architectural invariants for the messaging / integration-event layer.
/// These rules exist to stop the dual-outbox class of bug from recurring.
///
/// The deal is:
/// <list type="bullet">
///   <item>
///     <b>Application</b> layer is infrastructure-free — it declares event
///     contracts (<see cref="IIntegrationEventCollector"/>, event records) and
///     nothing else related to transport.
///   </item>
///   <item>
///     <b>Infrastructure</b> layer is the only place that knows about
///     MassTransit. The interceptor resolves the correct outbox provider
///     at runtime; Application-layer handlers must never call
///     <c>IPublishEndpoint</c> or <c>IBus</c> directly.
///   </item>
/// </list>
/// A handler that sneaks <c>using MassTransit</c> into Application bypasses
/// the outbox and reintroduces the silent-drop bug these tests prevent.
/// </summary>
public sealed class MessagingArchitectureTests
{
    private static readonly System.Reflection.Assembly ApplicationAssembly =
        typeof(IIntegrationEventCollector).Assembly;

    [Fact]
    public void ApplicationAssembly_MustNotDependOn_MassTransit()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny("MassTransit", "MassTransit.EntityFrameworkCoreIntegration")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Application handlers must publish events via IIntegrationEventCollector. " +
            "A direct MassTransit reference would bypass the transactional outbox and " +
            "silently drop events when two EF outboxes are registered. " +
            "Offending types: " + FormatFailingTypes(result));
    }

    // NOTE: We intentionally do NOT ban Microsoft.EntityFrameworkCore from the
    // Application layer. Handlers use IApplicationDbContext + EF Core LINQ
    // extensions (IgnoreQueryFilters, AnyAsync, FirstOrDefaultAsync, etc.) for
    // querying. DbContext CONFIGURATION (connection strings, provider choice,
    // interceptors) remains in Infrastructure. That's the line this architecture
    // draws, and it's deliberate.

    private static string FormatFailingTypes(TestResult result) =>
        result.FailingTypeNames is null || result.FailingTypeNames.Count == 0
            ? "(none)"
            : string.Join(", ", result.FailingTypeNames);
}
