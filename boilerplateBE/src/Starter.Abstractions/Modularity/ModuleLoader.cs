using System.Reflection;

namespace Starter.Abstractions.Modularity;

public static class ModuleLoader
{
    public static IReadOnlyList<IModule> DiscoverModules()
    {
        var moduleType = typeof(IModule);
        var modules = new List<IModule>();

        // Load all module assemblies from the application's base directory.
        // .NET lazy-loads assemblies, so referenced module DLLs may not be in
        // AppDomain yet at startup. Scanning the directory ensures we find them.
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var assemblyFiles = Directory.GetFiles(baseDir, "*.Module.*.dll");

        foreach (var file in assemblyFiles)
        {
            try
            {
                var assemblyName = AssemblyName.GetAssemblyName(file);
                if (!AppDomain.CurrentDomain.GetAssemblies()
                        .Any(a => a.GetName().FullName == assemblyName.FullName))
                {
                    Assembly.LoadFrom(file);
                }
            }
            catch
            {
                // Skip DLLs that can't be loaded
            }
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => moduleType.IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

                foreach (var type in types)
                {
                    if (Activator.CreateInstance(type) is IModule module)
                        modules.Add(module);
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Skip assemblies that can't be loaded
            }
        }

        return modules;
    }

    public static IReadOnlyList<IModule> ResolveOrder(IReadOnlyList<IModule> modules)
    {
        var moduleMap = modules.ToDictionary(m => m.Name);
        var sorted = new List<IModule>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        foreach (var module in modules)
            Visit(module, moduleMap, sorted, visited, visiting);

        return sorted;
    }

    private static void Visit(
        IModule module,
        Dictionary<string, IModule> moduleMap,
        List<IModule> sorted,
        HashSet<string> visited,
        HashSet<string> visiting)
    {
        if (visited.Contains(module.Name)) return;

        if (!visiting.Add(module.Name))
            throw new InvalidOperationException(
                $"Circular module dependency detected: {module.Name}");

        foreach (var dep in module.Dependencies)
        {
            if (!moduleMap.TryGetValue(dep, out var depModule))
            {
                throw new InvalidOperationException(
                    $"Module '{module.Name}' declares a dependency on '{dep}', but '{dep}' is not installed. " +
                    $"Installed modules: {string.Join(", ", moduleMap.Keys)}.");
            }

            Visit(depModule, moduleMap, sorted, visited, visiting);
        }

        visiting.Remove(module.Name);
        visited.Add(module.Name);
        sorted.Add(module);
    }
}
