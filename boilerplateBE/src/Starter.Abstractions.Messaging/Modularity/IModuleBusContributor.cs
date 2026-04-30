using MassTransit;

namespace Starter.Abstractions.Modularity;

/// <summary>
/// Optional module-host contract. Modules that need to register MassTransit infrastructure
/// (consumers, an additional EF Core outbox bound to a module DbContext, custom endpoint
/// policies) implement this alongside <see cref="IModule"/>. The module host invokes
/// <see cref="ConfigureBus"/> for every registered contributor inside the bus
/// configuration callback. Core code never references module-specific extension methods.
///
/// Lives in <c>Starter.Abstractions.Messaging</c> (not <c>Starter.Infrastructure</c>) so
/// optional modules can opt in without taking a heavy infrastructure dependency. Keeping
/// the contract in an Abstractions* project means a module package only ships a thin
/// MassTransit dep alongside its own consumers — Tier 3 packages stay light.
/// </summary>
public interface IModuleBusContributor
{
    void ConfigureBus(IBusRegistrationConfigurator bus);
}
