using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Modularity;
using Xunit;

namespace Starter.Api.Tests.Architecture;

public class ModuleLoaderTests
{
    [Fact]
    public void ResolveOrder_throws_when_a_declared_dependency_is_not_installed()
    {
        var moduleA = new FakeModule("A", dependencies: ["B"]);
        var modules = new List<IModule> { moduleA };

        var act = () => ModuleLoader.ResolveOrder(modules);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'A'*'B'*not installed*");
    }

    [Fact]
    public void ResolveOrder_succeeds_when_all_declared_dependencies_are_installed()
    {
        var moduleA = new FakeModule("A", dependencies: ["B"]);
        var moduleB = new FakeModule("B");
        var modules = new List<IModule> { moduleA, moduleB };

        var ordered = ModuleLoader.ResolveOrder(modules);

        ordered.Select(m => m.Name).Should().Equal("B", "A");
    }

    [Fact]
    public void ResolveOrder_includes_installed_module_names_in_error_message()
    {
        var moduleA = new FakeModule("A", dependencies: ["Missing"]);
        var moduleB = new FakeModule("B");
        var modules = new List<IModule> { moduleA, moduleB };

        var act = () => ModuleLoader.ResolveOrder(modules);

        act.Should().Throw<InvalidOperationException>()
            .Where(e => e.Message.Contains("A") && e.Message.Contains("B"));
    }

    [Fact]
    public void ResolveOrder_throws_helpful_error_when_two_modules_share_a_name()
    {
        var modules = new List<IModule>
        {
            new FakeModule("DuplicateName"),
            new FakeModule("DuplicateName"),
        };

        var act = () => ModuleLoader.ResolveOrder(modules);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicate Name*'DuplicateName'*");
    }

    private sealed class FakeModule : IModule
    {
        public FakeModule(string name, params string[] dependencies)
        {
            Name = name;
            Dependencies = dependencies;
        }

        public string Name { get; }
        public string DisplayName => Name;
        public string Version => "1.0.0";
        public IReadOnlyList<string> Dependencies { get; }

        public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
            => services;

        public IEnumerable<(string Name, string Description, string Module)> GetPermissions() => [];

        public IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions() => [];
    }
}
