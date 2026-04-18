using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Modularity;
using Starter.Module.CommentsActivity.Constants;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;
using Starter.Module.CommentsActivity.Infrastructure.Services;

namespace Starter.Module.CommentsActivity;

public sealed class CommentsActivityModule : IModule
{
    public string Name => "Starter.Module.CommentsActivity";
    public string DisplayName => "Comments & Activity";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CommentsActivityDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());

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

        // Capability services — override Null Object fallbacks
        services.AddSingleton<ICommentableEntityRegistry>(sp =>
        {
            var registrations = sp.GetServices<ICommentableEntityRegistration>();
            return new CommentableEntityRegistry(registrations);
        });
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IEntityWatcherService, EntityWatcherService>();

        services.AddHealthChecks()
            .AddDbContextCheck<CommentsActivityDbContext>(
                name: "commentsactivity-db",
                tags: ["db", "comments-activity"]);

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

    public async Task SeedDataAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();

        var templateRegistrar = scope.ServiceProvider.GetRequiredService<ITemplateRegistrar>();

        // Register the mention email template. When the Communication module is not
        // installed, NullTemplateRegistrar silently no-ops — the template is never
        // needed because NullMessageDispatcher also no-ops.
        await templateRegistrar.RegisterTemplateAsync(
            name: "notification.comment-mention",
            moduleSource: "CommentsActivity",
            category: "comments",
            description: "Email sent to a user when they are @mentioned in a comment",
            subjectTemplate: "{{mentionerName}} mentioned you in a comment",
            bodyTemplate: "Hi,\n\n{{mentionerName}} mentioned you in a comment on {{entityType}}.\n\n\"{{commentBody}}\"\n\nView it in the app: {{appUrl}}",
            defaultChannel: NotificationChannelType.Email,
            availableChannels: ["Email", "InApp"],
            variableSchema: new()
            {
                ["mentionerName"] = "Display name of the comment author",
                ["entityType"] = "Type of entity the comment is on (e.g. Product)",
                ["entityId"] = "ID of the entity",
                ["commentBody"] = "First 200 characters of the comment",
                ["appUrl"] = "Base URL of the application",
            },
            sampleVariables: new()
            {
                ["mentionerName"] = "Saman Jasim",
                ["entityType"] = "Product",
                ["entityId"] = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                ["commentBody"] = "Great progress on this! Let's discuss in the next standup.",
                ["appUrl"] = "https://app.example.com",
            },
            ct: cancellationToken);

        await templateRegistrar.RegisterEventAsync(
            eventName: "comment.user-mentioned",
            moduleSource: "CommentsActivity",
            displayName: "User Mentioned in Comment",
            description: "Fires when a user is @mentioned in a comment",
            ct: cancellationToken);
    }
}
