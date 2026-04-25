using System.Reflection;
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
///     <b>Command + query handlers in any module</b> must publish via
///     <see cref="IIntegrationEventCollector"/> or <c>IMessagePublisher</c>,
///     never <c>IPublishEndpoint</c> / <c>IBus</c> directly. With multiple
///     <c>AddEntityFrameworkOutbox&lt;T&gt;()</c> registrations, the abstract
///     <c>IScopedBusContextProvider&lt;IBus&gt;</c> is replaced by the last
///     call — direct publishes route through the wrong DbContext and vanish.
///   </item>
///   <item>
///     <b>Infrastructure layer + module Infrastructure SERVICES</b> may use
///     <c>IPublishEndpoint</c> when the call site is inside an MT consumer
///     pipeline (where MT's outbox is already in scope). Services that
///     publish from a request-scope (not consumer-scope) must use the
///     collector — this test catches the request-scope offenders.
///   </item>
/// </list>
/// A handler that sneaks <c>using MassTransit</c> into Application bypasses
/// the outbox and reintroduces the silent-drop bug these tests prevent.
/// </summary>
public sealed class MessagingArchitectureTests
{
    private static readonly Assembly ApplicationAssembly =
        typeof(IIntegrationEventCollector).Assembly;

    /// <summary>
    /// Loaded module + Application assemblies. We reference the test project's
    /// dependencies, which transitively load every module assembly the API
    /// composes. Filtering by name keeps the rule scoped to project code.
    /// </summary>
    private static IEnumerable<Assembly> ProjectAssembliesUnderTest =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .Where(a => a.GetName().Name is { } name &&
                (name == "Starter.Application" || name.StartsWith("Starter.Module.")))
            .ToList();

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

    /// <summary>
    /// Every <c>*CommandHandler</c> and <c>*QueryHandler</c> in any module
    /// must publish through the collector, not <c>IPublishEndpoint</c>.
    /// We assert this on the type names rather than the namespaces because
    /// command handlers in modules live under <c>Module.X.Application.Commands.*</c>
    /// — outside <c>Starter.Application</c> — and the original arch test
    /// missed them.
    ///
    /// This invariant is what would have caught the
    /// <c>UploadDocumentCommandHandler</c> and <c>ResendDeliveryCommandHandler</c>
    /// silent-drop bugs at build time instead of in production.
    /// </summary>
    [Fact]
    public void CommandAndQueryHandlers_MustNotDependOn_MassTransit()
    {
        var assemblies = ProjectAssembliesUnderTest.ToArray();
        assemblies.Should().NotBeEmpty(
            "the test must scan at least the Application assembly + module assemblies. " +
            "If this fails, ensure the test project references all module projects " +
            "(transitively, via Starter.Api).");

        var result = Types.InAssemblies(assemblies)
            .That()
            .HaveNameEndingWith("CommandHandler")
            .Or()
            .HaveNameEndingWith("QueryHandler")
            .Should()
            .NotHaveDependencyOnAny("MassTransit", "MassTransit.EntityFrameworkCoreIntegration")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "MediatR handlers run in the HTTP-request DI scope. With multiple EF outboxes " +
            "registered, IPublishEndpoint resolves to whichever DbContext registered last " +
            "(typically the Workflow module), and writes are silently dropped. " +
            "Use IIntegrationEventCollector.Schedule(...) instead. " +
            "Scanned assemblies: [" + string.Join(", ", assemblies.Select(a => a.GetName().Name)) + "]. " +
            "Offending handlers: " + FormatFailingTypes(result));
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
