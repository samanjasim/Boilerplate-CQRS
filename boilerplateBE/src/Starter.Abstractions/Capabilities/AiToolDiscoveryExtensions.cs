using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Starter.Abstractions.Capabilities;

/// <summary>
/// DI extension methods that scan an assembly for <c>[AiTool]</c>-decorated MediatR request
/// types and register each as a singleton <see cref="IAiToolDefinition"/>. Modules call this
/// once from their <c>ConfigureServices</c>.
/// </summary>
public static class AiToolDiscoveryExtensions
{
    /// <summary>
    /// Scan the supplied assembly for <c>[AiTool]</c>-decorated types and register each as
    /// an <see cref="IAiToolDefinition"/> singleton. Validates each attributed type up-front:
    /// the type must implement <see cref="IBaseRequest"/>, <c>RequiredPermission</c> must be
    /// a non-empty string, and the schema must be generatable. Any failure throws from this
    /// call — the service collection is not partially mutated. Idempotent per-call: calling
    /// twice with the same assembly registers each tool twice, which is a collision detected
    /// later by the registry (see <c>AiToolRegistryService</c>).
    /// </summary>
    public static IServiceCollection AddAiToolsFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        var moduleSource = DeriveModuleSource(assembly);

        var candidates = assembly
            .GetTypes()
            .Where(t => (t.IsClass || t.IsValueType) && !t.IsEnum)
            .Where(t => !t.IsAbstract)
            .Select(t => (Type: t, Attr: t.GetCustomAttribute<AiToolAttribute>(inherit: false)))
            .Where(x => x.Attr is not null)
            .ToList();

        if (candidates.Count == 0)
            return services;

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var adapters = new List<AttributedAiToolDefinition>(candidates.Count);

        foreach (var (type, attr) in candidates)
        {
            ValidateShape(type, attr!);

            if (!seenNames.Add(attr!.Name))
                throw new InvalidOperationException(
                    $"[AiTool] duplicate Name '{attr.Name}' inside assembly '{assembly.GetName().Name}'. " +
                    "Tool names must be unique.");

            var schema = AiToolSchemaGenerator.Generate(type, attr);
            adapters.Add(new AttributedAiToolDefinition(type, attr, schema, moduleSource));
        }

        foreach (var adapter in adapters)
            services.AddSingleton<IAiToolDefinition>(adapter);

        return services;
    }

    private static void ValidateShape(Type type, AiToolAttribute attr)
    {
        if (!typeof(IBaseRequest).IsAssignableFrom(type))
            throw new InvalidOperationException(
                $"[AiTool] on '{type.FullName}': attributed type must implement MediatR.IBaseRequest " +
                "(IRequest or IRequest<T>).");

        if (string.IsNullOrWhiteSpace(attr.Name))
            throw new InvalidOperationException(
                $"[AiTool] on '{type.FullName}': Name is required.");

        if (string.IsNullOrWhiteSpace(attr.Description))
            throw new InvalidOperationException(
                $"[AiTool] on '{type.FullName}': Description is required.");

        if (string.IsNullOrWhiteSpace(attr.Category))
            throw new InvalidOperationException(
                $"[AiTool] on '{type.FullName}': Category is required.");

        if (string.IsNullOrWhiteSpace(attr.RequiredPermission))
            throw new InvalidOperationException(
                $"[AiTool] on '{type.FullName}': RequiredPermission is required.");
    }

    internal static string DeriveModuleSource(Assembly assembly)
    {
        var name = assembly.GetName().Name ?? "Unknown";
        const string modulePrefix = "Starter.Module.";
        const string starterPrefix = "Starter.";

        if (name.StartsWith(modulePrefix, StringComparison.Ordinal))
            return name[modulePrefix.Length..];
        if (name.StartsWith(starterPrefix, StringComparison.Ordinal))
            return name[starterPrefix.Length..];
        return name;
    }
}
