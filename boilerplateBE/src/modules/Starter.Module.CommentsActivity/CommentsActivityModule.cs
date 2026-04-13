using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Modularity;
using Starter.Module.CommentsActivity.Constants;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;

namespace Starter.Module.CommentsActivity;

public sealed class CommentsActivityModule : IModule
{
    public string Name => "Starter.Module.CommentsActivity";
    public string DisplayName => "Comments & Activity";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CommentsActivityDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_CommentsActivity");
                    npgsqlOptions.MigrationsAssembly(typeof(CommentsActivityDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: ["40001"]);
                });
        });

        return services;
    }

    public IEnumerable<(string Name, string Description, string Module)> GetPermissions()
    {
        yield return (CommentsActivityPermissions.ViewComments, "View comments", "CommentsActivity");
        yield return (CommentsActivityPermissions.CreateComments, "Create comments", "CommentsActivity");
        yield return (CommentsActivityPermissions.EditComments, "Edit own comments", "CommentsActivity");
        yield return (CommentsActivityPermissions.DeleteComments, "Delete own comments", "CommentsActivity");
        yield return (CommentsActivityPermissions.ManageComments, "Manage all comments", "CommentsActivity");
        yield return (CommentsActivityPermissions.ViewActivity, "View activity feed", "CommentsActivity");
    }

    public IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions()
    {
        yield return ("SuperAdmin", [
            CommentsActivityPermissions.ViewComments,
            CommentsActivityPermissions.CreateComments,
            CommentsActivityPermissions.EditComments,
            CommentsActivityPermissions.DeleteComments,
            CommentsActivityPermissions.ManageComments,
            CommentsActivityPermissions.ViewActivity]);
        yield return ("Admin", [
            CommentsActivityPermissions.ViewComments,
            CommentsActivityPermissions.CreateComments,
            CommentsActivityPermissions.EditComments,
            CommentsActivityPermissions.DeleteComments,
            CommentsActivityPermissions.ManageComments,
            CommentsActivityPermissions.ViewActivity]);
        yield return ("User", [
            CommentsActivityPermissions.ViewComments,
            CommentsActivityPermissions.CreateComments,
            CommentsActivityPermissions.EditComments,
            CommentsActivityPermissions.DeleteComments,
            CommentsActivityPermissions.ViewActivity]);
    }

    public async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CommentsActivityDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
