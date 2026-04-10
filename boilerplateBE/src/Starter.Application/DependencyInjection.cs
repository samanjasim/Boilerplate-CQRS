using System.Reflection;
using Starter.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Starter.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IReadOnlyList<Assembly>? moduleAssemblies = null)
    {
        var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly() };
        if (moduleAssemblies is not null)
            assemblies.AddRange(moduleAssemblies);

        services.AddMediatR(config =>
        {
            foreach (var assembly in assemblies)
                config.RegisterServicesFromAssembly(assembly);

            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TracingBehavior<,>));
            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        });

        foreach (var assembly in assemblies)
            services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
