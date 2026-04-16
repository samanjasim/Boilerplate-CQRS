using Microsoft.Extensions.DependencyInjection;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Infrastructure.Providers;

public interface IChannelProviderFactory
{
    IChannelProvider? GetProvider(NotificationChannel channel, Domain.Enums.ChannelProvider provider);
    IChannelProvider? GetDefaultProvider(NotificationChannel channel);
}

internal sealed class ChannelProviderFactory(IServiceProvider serviceProvider) : IChannelProviderFactory
{
    public IChannelProvider? GetProvider(NotificationChannel channel, Domain.Enums.ChannelProvider provider)
    {
        var providers = serviceProvider.GetServices<IChannelProvider>();
        return providers.FirstOrDefault(p => p.Channel == channel && p.ProviderType == provider);
    }

    public IChannelProvider? GetDefaultProvider(NotificationChannel channel)
    {
        var providers = serviceProvider.GetServices<IChannelProvider>();
        return providers.FirstOrDefault(p => p.Channel == channel);
    }
}
