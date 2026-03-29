namespace Starter.Shared.Constants;

public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string User = "User";

    public static IEnumerable<string> GetAll()
    {
        yield return SuperAdmin;
        yield return Admin;
        yield return User;
    }

    /// <summary>
    /// Maps each role to its set of permissions.
    /// SuperAdmin receives ALL permissions automatically.
    /// Convention: if a role has {Module}.View it should also have {Module}.Show.
    /// </summary>
    public static IEnumerable<(string Role, string[] Permissions)> GetRolePermissions()
    {
        yield return (SuperAdmin, Constants.Permissions.GetAll().ToArray());

        yield return (Admin, [
            // Users
            Permissions.Users.View,
            Permissions.Users.Show,
            Permissions.Users.Create,
            Permissions.Users.Update,
            // Roles
            Permissions.Roles.View,
            Permissions.Roles.Show,
            // Tenants
            Permissions.Tenants.View,
            Permissions.Tenants.Show,
            Permissions.Tenants.Create,
            Permissions.Tenants.Update,
            // Files
            Permissions.Files.View,
            Permissions.Files.Upload,
            Permissions.Files.Delete,
            Permissions.Files.Manage,
            // API Keys
            Permissions.ApiKeys.View,
            Permissions.ApiKeys.Create,
            Permissions.ApiKeys.Update,
            Permissions.ApiKeys.Delete,
            // Feature Flags
            Permissions.FeatureFlags.View,
            Permissions.FeatureFlags.OptOut,
            // System
            Permissions.System.ViewDashboard,
            Permissions.System.ExportData,
            Permissions.System.ManageSettings
        ]);

        yield return (User, [
            // Files
            Permissions.Files.View,
            Permissions.Files.Upload,
            // System
            Permissions.System.ViewDashboard
        ]);
    }
}
