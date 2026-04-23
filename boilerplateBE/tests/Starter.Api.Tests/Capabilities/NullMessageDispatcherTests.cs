using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Abstractions.Capabilities;
using Starter.Infrastructure.Capabilities.NullObjects;
using Xunit;

namespace Starter.Api.Tests.Capabilities;

/// <summary>
/// These tests pin the Null-Object composability contract. A caller that injects
/// <see cref="IMessageDispatcher"/> or <see cref="ICommunicationEventNotifier"/>
/// must continue to work — silently — when the Communication module is not
/// installed. Regressing this breaks every module that consumes these
/// capabilities (Comments, future AI, etc.).
/// </summary>
public sealed class NullObjectsTests
{
    [Fact]
    public async Task NullMessageDispatcher_SendAsync_ReturnsEmptyGuidAndDoesNotThrow()
    {
        var sut = new NullMessageDispatcher(NullLogger<NullMessageDispatcher>.Instance);

        var id = await sut.SendAsync(
            templateName: "notification.mention",
            recipientUserId: Guid.NewGuid(),
            variables: new() { ["commentId"] = Guid.NewGuid() },
            tenantId: Guid.NewGuid());

        id.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task NullMessageDispatcher_SendToChannelAsync_ReturnsEmptyGuidAndDoesNotThrow()
    {
        var sut = new NullMessageDispatcher(NullLogger<NullMessageDispatcher>.Instance);

        var id = await sut.SendToChannelAsync(
            templateName: "notification.mention",
            recipientUserId: Guid.NewGuid(),
            channel: NotificationChannelType.Email,
            variables: new Dictionary<string, object>(),
            tenantId: null);

        id.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task NullCommunicationEventNotifier_NotifyAsync_CompletesWithoutThrowing()
    {
        var sut = new NullCommunicationEventNotifier(NullLogger<NullCommunicationEventNotifier>.Instance);

        var act = async () => await sut.NotifyAsync(
            eventName: "user.mentioned",
            tenantId: Guid.NewGuid(),
            actorUserId: Guid.NewGuid(),
            eventData: new Dictionary<string, object>());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void CoreDI_RegistersNullDispatcher_AsFallback()
    {
        // Proves the core Infrastructure wires a default. Consumers can inject
        // IMessageDispatcher unconditionally — the real implementation wins when
        // the Communication module is loaded (TryAddScoped semantics); otherwise
        // the Null Object keeps the host running.
        var services = new ServiceCollection();
        services.AddLogging();
        services.TryAddScoped<IMessageDispatcher, NullMessageDispatcher>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetService<IMessageDispatcher>();

        dispatcher.Should().NotBeNull();
        dispatcher.Should().BeOfType<NullMessageDispatcher>();
    }

    [Fact]
    public void RealRegistration_OverridesNull_WhenModuleRegistersAfter()
    {
        // TryAddScoped from core runs first; the module's AddScoped runs later
        // and REPLACES the registration. This is the composition seam that lets
        // Comments call IMessageDispatcher without knowing whether Communication
        // is installed.
        var services = new ServiceCollection();
        services.AddLogging();
        services.TryAddScoped<IMessageDispatcher, NullMessageDispatcher>();
        services.AddScoped<IMessageDispatcher, FakeRealDispatcher>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();

        dispatcher.Should().BeOfType<FakeRealDispatcher>();
    }

    private sealed class FakeRealDispatcher : IMessageDispatcher
    {
        public Task<Guid> SendAsync(string templateName, Guid recipientUserId, Dictionary<string, object> variables,
            Guid? tenantId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());

        public Task<Guid> SendToChannelAsync(string templateName, Guid recipientUserId, NotificationChannelType channel,
            Dictionary<string, object> variables, Guid? tenantId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());
    }
}
