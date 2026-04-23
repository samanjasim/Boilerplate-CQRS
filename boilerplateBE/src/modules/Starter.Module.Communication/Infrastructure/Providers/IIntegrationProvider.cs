using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Infrastructure.Providers;

public interface IIntegrationProvider
{
    IntegrationType Type { get; }
    Task<ProviderDeliveryResult> SendAsync(IntegrationDeliveryRequest request, CancellationToken ct = default);
}

public sealed record IntegrationDeliveryRequest(
    string TargetChannelId,
    string Message,
    Dictionary<string, string> ProviderCredentials,
    Dictionary<string, string>? Metadata = null);
