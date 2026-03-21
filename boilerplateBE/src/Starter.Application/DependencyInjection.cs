using System.Reflection;
using Starter.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Starter.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);

            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
