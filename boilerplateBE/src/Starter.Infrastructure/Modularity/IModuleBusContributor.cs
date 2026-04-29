using MassTransit;

namespace Starter.Infrastructure.Modularity;

/// <summary>
/// Optional module-host contract. Modules that need to register MassTransit infrastructure
/// (e.g. an additional EF Core outbox bound to the module's DbContext, custom consumers,
/// endpoint policies) implement this alongside <see cref="Starter.Abstractions.Modularity.IModule"/>.
/// The module host invokes <see cref="ConfigureBus"/> for every registered contributor inside
/// the bus configuration callback. Core code never references module-specific extension methods.
/// </summary>
public interface IModuleBusContributor
{
    void ConfigureBus(IBusRegistrationConfigurator bus);
}
