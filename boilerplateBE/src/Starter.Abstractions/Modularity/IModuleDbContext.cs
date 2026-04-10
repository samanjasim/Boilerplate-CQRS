namespace Starter.Abstractions.Modularity;

/// <summary>
/// Marker interface for module-owned EF Core DbContexts.
///
/// Each module that needs persistence creates its own DbContext implementing this
/// marker. The module is responsible for migrations, EF configurations, and
/// seeding into its own context — never into <c>ApplicationDbContext</c>.
///
/// This enables true module isolation: removing a module removes its tables,
/// migration history, and all related schema with zero impact on core or other
/// modules.
/// </summary>
public interface IModuleDbContext
{
}
