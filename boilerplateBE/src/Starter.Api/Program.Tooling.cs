using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Application;
using Starter.Infrastructure;
using Starter.Infrastructure.Identity;
using Starter.Infrastructure.Modularity;

namespace Starter.Api;

public partial class Program
{
    /// <summary>
    /// Registers the same service graph as the main API host without the
    /// web/HTTP-specific middleware (controllers, Swagger, rate limiting, etc.).
    /// Intended for use by out-of-process tooling such as EvalCacheWarmup that
    /// need access to application services (retrieval, embeddings, vector store)
    /// without spinning up a full ASP.NET Core web server.
    /// </summary>
    public static void ConfigureServicesForTooling(
        IServiceCollection services,
        IConfiguration config)
    {
        var modules = Starter.Abstractions.Modularity.ModuleLoader.DiscoverModules();
        var orderedModules = Starter.Abstractions.Modularity.ModuleLoader.ResolveOrder(modules);
        var moduleAssemblies = orderedModules.Select(m => m.GetType().Assembly).Distinct().ToList();

        services.AddSingleton<IReadOnlyList<Starter.Abstractions.Modularity.IModule>>(orderedModules);
        services.AddApplication(moduleAssemblies);
        services.AddInfrastructure(
            config,
            moduleAssemblies,
            configureBus: bus =>
            {
                foreach (var contributor in orderedModules.OfType<IModuleBusContributor>())
                {
                    contributor.ConfigureBus(bus);
                }
            });
        services.AddIdentityInfrastructure(config);

        foreach (var module in orderedModules)
            module.ConfigureServices(services, config);
    }
}
