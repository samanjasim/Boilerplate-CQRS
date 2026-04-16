using Microsoft.Extensions.DependencyInjection;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Infrastructure.Providers;

public interface IIntegrationProviderFactory
{
    IIntegrationProvider? GetProvider(IntegrationType type);
}

internal sealed class IntegrationProviderFactory(IServiceProvider serviceProvider) : IIntegrationProviderFactory
{
    public IIntegrationProvider? GetProvider(IntegrationType type)
    {
        var providers = serviceProvider.GetServices<IIntegrationProvider>();
        return providers.FirstOrDefault(p => p.Type == type);
    }
}
