using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Modularity;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Constants;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Services;

namespace Starter.Module.AI;

public sealed class AIModule : IModule
{
    public string Name => "Starter.Module.AI";
    public string DisplayName => "AI Integration";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AiDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_AI");
                    npgsqlOptions.MigrationsAssembly(typeof(AiDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: ["40001"]);
                });
        });

        services.AddScoped<AnthropicAiProvider>();
        services.AddScoped<OpenAiProvider>();
        services.AddScoped<OllamaAiProvider>();
        services.AddScoped<AiProviderFactory>();
        services.AddScoped<IAiService, AiService>();
        services.AddScoped<IUsageMetricCalculator, AiUsageMetricCalculator>();
        services.AddScoped<IChatExecutionService, ChatExecutionService>();

        services.AddSingleton<IAiToolRegistry, AiToolRegistryService>();
        services.AddSingleton<IAiToolDefinition, Infrastructure.Tools.ListMyConversationsAiTool>();
        services.AddHostedService<AiToolRegistrySyncHostedService>();

        return services;
    }

    public IEnumerable<(string Name, string Description, string Module)> GetPermissions()
    {
        yield return (AiPermissions.Chat, "Send messages to AI assistants", "AI");
        yield return (AiPermissions.ViewConversations, "View AI conversation history", "AI");
        yield return (AiPermissions.DeleteConversation, "Delete AI conversations", "AI");
        yield return (AiPermissions.ManageAssistants, "Create and manage AI assistants", "AI");
        yield return (AiPermissions.ManageDocuments, "Upload and manage AI knowledge documents", "AI");
        yield return (AiPermissions.ManageTools, "Create and manage AI tools", "AI");
        yield return (AiPermissions.ManageTriggers, "Create and manage AI agent triggers", "AI");
        yield return (AiPermissions.ViewUsage, "View AI usage statistics", "AI");
        yield return (AiPermissions.RunAgentTasks, "Execute AI agent tasks", "AI");
        yield return (AiPermissions.ManageSettings, "Manage AI module settings", "AI");
    }

    public IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions()
    {
        yield return ("SuperAdmin", [
            AiPermissions.Chat,
            AiPermissions.ViewConversations,
            AiPermissions.DeleteConversation,
            AiPermissions.ManageAssistants,
            AiPermissions.ManageDocuments,
            AiPermissions.ManageTools,
            AiPermissions.ManageTriggers,
            AiPermissions.ViewUsage,
            AiPermissions.RunAgentTasks,
            AiPermissions.ManageSettings
        ]);

        yield return ("Admin", [
            AiPermissions.Chat,
            AiPermissions.ViewConversations,
            AiPermissions.DeleteConversation,
            AiPermissions.ManageAssistants,
            AiPermissions.ManageDocuments,
            AiPermissions.ManageTools,
            AiPermissions.ManageTriggers,
            AiPermissions.ViewUsage,
            AiPermissions.RunAgentTasks
        ]);

        yield return ("User", [
            AiPermissions.Chat,
            AiPermissions.ViewConversations,
            AiPermissions.DeleteConversation
        ]);
    }

    public async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
