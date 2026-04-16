using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Module.CommentsActivity.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.CommentsActivity.Infrastructure;

public sealed class TenantResolutionTests
{
    private const string EntityType = "Product";
    private static readonly Guid EntityId = Guid.NewGuid();
    private static readonly Guid AcmeTenant = Guid.NewGuid();
    private static readonly Guid GlobexTenant = Guid.NewGuid();

    private readonly Mock<ICommentableEntityRegistry> _registry = new();
    private readonly Mock<IServiceProvider> _services = new();
    private readonly Mock<ILogger> _logger = new();

    [Fact]
    public async Task NoDefinition_ReturnsCallerValue()
    {
        _registry.Setup(r => r.GetDefinition(EntityType)).Returns((CommentableEntityDefinition?)null);

        var result = await TenantResolution.ResolveEffectiveTenantIdAsync(
            _registry.Object, _services.Object, _logger.Object,
            EntityType, EntityId, AcmeTenant, CancellationToken.None);

        result.Should().Be(AcmeTenant);
        VerifyNoWarning();
    }

    [Fact]
    public async Task DefinitionWithoutResolver_ReturnsCallerValue()
    {
        _registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(DefinitionWith(resolver: null));

        var result = await TenantResolution.ResolveEffectiveTenantIdAsync(
            _registry.Object, _services.Object, _logger.Object,
            EntityType, EntityId, AcmeTenant, CancellationToken.None);

        result.Should().Be(AcmeTenant);
        VerifyNoWarning();
    }

    [Fact]
    public async Task ResolverReturnsNull_FallsBackToCallerValue()
    {
        _registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(DefinitionWith(resolver: (_, _, _) => Task.FromResult<Guid?>(null)));

        var result = await TenantResolution.ResolveEffectiveTenantIdAsync(
            _registry.Object, _services.Object, _logger.Object,
            EntityType, EntityId, AcmeTenant, CancellationToken.None);

        result.Should().Be(AcmeTenant);
        VerifyNoWarning();
    }

    [Fact]
    public async Task ResolverMatchesCaller_ReturnsResolvedValue_NoWarning()
    {
        _registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(DefinitionWith(resolver: (_, _, _) => Task.FromResult<Guid?>(AcmeTenant)));

        var result = await TenantResolution.ResolveEffectiveTenantIdAsync(
            _registry.Object, _services.Object, _logger.Object,
            EntityType, EntityId, AcmeTenant, CancellationToken.None);

        result.Should().Be(AcmeTenant);
        VerifyNoWarning();
    }

    [Fact]
    public async Task ResolverDisagreesWithCaller_ReturnsResolvedValue_LogsWarning()
    {
        _registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(DefinitionWith(resolver: (_, _, _) => Task.FromResult<Guid?>(AcmeTenant)));

        var result = await TenantResolution.ResolveEffectiveTenantIdAsync(
            _registry.Object, _services.Object, _logger.Object,
            EntityType, EntityId, GlobexTenant, CancellationToken.None);

        result.Should().Be(AcmeTenant);
        VerifyWarningLoggedOnce();
    }

    [Fact]
    public async Task CallerNullResolverHasValue_ReturnsResolvedValue_NoWarning()
    {
        _registry.Setup(r => r.GetDefinition(EntityType))
            .Returns(DefinitionWith(resolver: (_, _, _) => Task.FromResult<Guid?>(AcmeTenant)));

        var result = await TenantResolution.ResolveEffectiveTenantIdAsync(
            _registry.Object, _services.Object, _logger.Object,
            EntityType, EntityId, callerTenantId: null, CancellationToken.None);

        result.Should().Be(AcmeTenant);
        VerifyNoWarning();
    }

    private static CommentableEntityDefinition DefinitionWith(
        Func<Guid, IServiceProvider, CancellationToken, Task<Guid?>>? resolver) =>
        new(EntityType, "display.key", true, true, [], false, false, resolver);

    private void VerifyNoWarning() =>
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);

    private void VerifyWarningLoggedOnce() =>
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
}
