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
            // Restrict discovery to assemblies that match the module naming convention
            // (mirrors the *.Module.* DLL glob above). Without this filter, the test
            // assembly's FakeModule helpers and any third-party DLL that happens to
            // implement IModule would be picked up — and a parameterless-ctor failure
            // on a non-module type would surface as a runtime crash with no useful
            // context. Real modules ship as Starter.Module.X, so the glob is enough.
            var assemblyName = assembly.GetName().Name;
            if (assemblyName is null || !assemblyName.Contains(".Module.", StringComparison.Ordinal))
                continue;

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
        var moduleMap = new Dictionary<string, IModule>(modules.Count, StringComparer.Ordinal);
        foreach (var module in modules)
        {
            if (!moduleMap.TryAdd(module.Name, module))
            {
                throw new InvalidOperationException(
                    $"Two modules declare the same duplicate Name '{module.Name}'. " +
                    $"IModule.Name is the lookup key for dependency resolution; duplicates would silently overwrite. " +
                    $"First-registered type: {moduleMap[module.Name].GetType().FullName}; " +
                    $"conflict: {module.GetType().FullName}.");
            }
        }

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
