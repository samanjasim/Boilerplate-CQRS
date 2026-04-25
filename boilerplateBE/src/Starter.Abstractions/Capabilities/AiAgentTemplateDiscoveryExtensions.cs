using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Starter.Abstractions.Capabilities;

public static class AiAgentTemplateDiscoveryExtensions
{
    public static IServiceCollection AddAiAgentTemplatesFromAssembly(
        this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        var moduleSource = DeriveModuleSource(assembly.GetName().Name ?? "Unknown");

        var templateTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(t => typeof(IAiAgentTemplate).IsAssignableFrom(t))
            .Where(t => t.GetConstructor(Type.EmptyTypes) is not null);

        foreach (var type in templateTypes)
        {
            var instance = (IAiAgentTemplate)Activator.CreateInstance(type)!;
            ValidateShape(instance);
            services.AddSingleton<IAiAgentTemplate>(
                new AiAgentTemplateRegistration(instance, moduleSource));
        }

        return services;
    }

    internal static string DeriveModuleSource(string assemblyName)
    {
        if (assemblyName.StartsWith("Starter.Module.", StringComparison.Ordinal))
            return assemblyName["Starter.Module.".Length..];
        if (string.Equals(assemblyName, "Starter.Application", StringComparison.Ordinal))
            return "Core";
        if (assemblyName.StartsWith("Starter.", StringComparison.Ordinal))
            return assemblyName["Starter.".Length..];
        return assemblyName;
    }

    internal static void ValidateShape(IAiAgentTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var typeName = template.GetType().FullName ?? template.GetType().Name;

        if (string.IsNullOrWhiteSpace(template.Slug))
            throw new InvalidOperationException(
                $"Template {typeName} has empty Slug.");
        if (template.Slug.Length > 128)
            throw new InvalidOperationException(
                $"Template {typeName} Slug exceeds 128 characters.");
        if (string.IsNullOrWhiteSpace(template.DisplayName))
            throw new InvalidOperationException(
                $"Template {typeName} has empty DisplayName.");
        if (string.IsNullOrWhiteSpace(template.Description))
            throw new InvalidOperationException(
                $"Template {typeName} has empty Description.");
        if (string.IsNullOrWhiteSpace(template.Category))
            throw new InvalidOperationException(
                $"Template {typeName} has empty Category.");
        if (string.IsNullOrWhiteSpace(template.SystemPrompt))
            throw new InvalidOperationException(
                $"Template {typeName} has empty SystemPrompt.");
        if (string.IsNullOrWhiteSpace(template.Model))
            throw new InvalidOperationException(
                $"Template {typeName} has empty Model.");
        if (template.Temperature is < 0.0 or > 2.0)
            throw new InvalidOperationException(
                $"Template {typeName} Temperature {template.Temperature} out of [0.0, 2.0].");
        if (template.MaxTokens < 1)
            throw new InvalidOperationException(
                $"Template {typeName} MaxTokens must be ≥ 1.");
        if (template.EnabledToolNames is null)
            throw new InvalidOperationException(
                $"Template {typeName} EnabledToolNames is null (use empty list instead).");
        if (template.PersonaTargetSlugs is null)
            throw new InvalidOperationException(
                $"Template {typeName} PersonaTargetSlugs is null (use empty list instead).");
    }
}
