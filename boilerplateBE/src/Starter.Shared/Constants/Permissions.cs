namespace Starter.Shared.Constants;

/// <summary>
/// Centralized permission constants used across the application.
///
/// ── NAMING CONVENTION FOR BACKEND DEVELOPERS ──
///
/// Pattern:  {Module}.{Action}
///
/// Standard CRUD actions per module:
///   {Module}.View             → List / read-many
///   {Module}.Show             → Read single / detail
///   {Module}.Create           → Create new resource
///   {Module}.Update           → Update existing resource
///   {Module}.Delete           → Delete resource
///
/// Domain-specific actions (when needed):
///   {Module}.{DomainAction}   → e.g. Users.ManageRoles
///
/// ── HOW TO ADD NEW PERMISSIONS (SEED GUIDE) ──
///
/// 1. Add a new inner class here following the pattern above.
/// 2. Register every permission in <see cref="GetAll"/> so the seeder picks it up.
/// 3. Register all (Permission, Description, Module) tuples in <see cref="GetAllWithMetadata"/>
///    so the seed creates them with correct module grouping and description.
/// 4. Map the new permissions to roles in <see cref="Roles.GetRolePermissions"/>.
/// 5. Mirror the new permission in the frontend.
///
/// ── CURRENT PERMISSION MATRIX ──
///
/// | Module     | View | Show | Create | Update | Delete | Custom Actions           |
/// |------------|------|------|--------|--------|--------|--------------------------|
/// | Users      |  ✓   |  ✓   |   ✓    |   ✓    |   ✓    | ManageRoles              |
/// | Roles      |  ✓   |  ✓   |   ✓    |   ✓    |   ✓    | ManagePermissions        |
/// | System     |      |      |        |        |        | ViewDashboard, ViewAuditLogs, ManageSettings |
/// | Tenants    |  ✓   |  ✓   |   ✓    |   ✓    |   ✓    |                          |
/// </summary>
public static class Permissions
{
    // ─── Users ───────────────────────────────────────
    public static class Users
    {
        public const string View = "Users.View";
        public const string Show = "Users.Show";
        public const string Create = "Users.Create";
        public const string Update = "Users.Update";
        public const string Delete = "Users.Delete";
        public const string ManageRoles = "Users.ManageRoles";
    }

    // ─── Roles ───────────────────────────────────────
    public static class Roles
    {
        public const string View = "Roles.View";
        public const string Show = "Roles.Show";
        public const string Create = "Roles.Create";
        public const string Update = "Roles.Update";
        public const string Delete = "Roles.Delete";
        public const string ManagePermissions = "Roles.ManagePermissions";
    }

    // ─── System ──────────────────────────────────────
    public static class System
    {
        public const string ViewAuditLogs = "System.ViewAuditLogs";
        public const string ManageSettings = "System.ManageSettings";
        public const string ViewDashboard = "System.ViewDashboard";
    }

    // ─── Tenants ─────────────────────────────────────
    public static class Tenants
    {
        public const string View = "Tenants.View";
        public const string Show = "Tenants.Show";
        public const string Create = "Tenants.Create";
        public const string Update = "Tenants.Update";
        public const string Delete = "Tenants.Delete";
    }

    /// <summary>
    /// Returns all permission string values. Used by the DataSeeder to create
    /// Permission entities in the database.
    /// </summary>
    public static IEnumerable<string> GetAll()
    {
        return GetAllWithMetadata().Select(p => p.Name);
    }

    /// <summary>
    /// Returns all permissions with metadata (name, description, module).
    /// Use this in the DataSeeder to create Permission entities with proper
    /// module grouping and human-readable descriptions.
    ///
    /// Example seed usage:
    /// <code>
    /// foreach (var (name, description, module) in Permissions.GetAllWithMetadata())
    /// {
    ///     if (!await dbContext.Permissions.AnyAsync(p => p.Name == name))
    ///     {
    ///         dbContext.Permissions.Add(Permission.Create(name, description, module));
    ///     }
    /// }
    /// </code>
    /// </summary>
    public static IEnumerable<(string Name, string Description, string Module)> GetAllWithMetadata()
    {
        // ─── Users ───
        yield return (Users.View, "View users list", "Users");
        yield return (Users.Show, "View user details", "Users");
        yield return (Users.Create, "Create new users", "Users");
        yield return (Users.Update, "Update existing users", "Users");
        yield return (Users.Delete, "Delete users", "Users");
        yield return (Users.ManageRoles, "Assign and remove roles from users", "Users");

        // ─── Roles ───
        yield return (Roles.View, "View roles list", "Roles");
        yield return (Roles.Show, "View role details", "Roles");
        yield return (Roles.Create, "Create new roles", "Roles");
        yield return (Roles.Update, "Update existing roles", "Roles");
        yield return (Roles.Delete, "Delete roles", "Roles");
        yield return (Roles.ManagePermissions, "Assign and remove permissions from roles", "Roles");

        // ─── System ───
        yield return (System.ViewAuditLogs, "View system audit logs", "System");
        yield return (System.ManageSettings, "Manage system settings", "System");
        yield return (System.ViewDashboard, "View the dashboard", "System");

        // ─── Tenants ───
        yield return (Tenants.View, "View tenants list", "Tenants");
        yield return (Tenants.Show, "View tenant details", "Tenants");
        yield return (Tenants.Create, "Create new tenants", "Tenants");
        yield return (Tenants.Update, "Update existing tenants", "Tenants");
        yield return (Tenants.Delete, "Delete tenants", "Tenants");
    }
}
