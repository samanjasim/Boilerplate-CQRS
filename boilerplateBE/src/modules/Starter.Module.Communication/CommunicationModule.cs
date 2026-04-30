using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Modularity;
using Starter.Application.Common.Interfaces;
using Starter.Module.Communication.Constants;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Module.Communication.Infrastructure.Persistence.Seed;
using Starter.Module.Communication.Infrastructure.Providers;
using Starter.Module.Communication.Infrastructure.Services;

namespace Starter.Module.Communication;

public sealed class CommunicationModule : IModule, IModuleBusContributor
{
    public string Name => "Starter.Module.Communication";
    public string DisplayName => "Multi-Channel Communication";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CommunicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor>());
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_Communication");
                    npgsqlOptions.MigrationsAssembly(typeof(CommunicationDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: ["40001"]);
                });
        });

        services.AddHttpClient();

        services.AddDataProtection();
        services.AddSingleton<ICredentialEncryptionService, CredentialEncryptionService>();
        services.AddSingleton<ITemplateEngine, StubbleTemplateEngine>();

        // Channel providers
        services.AddScoped<IChannelProvider, SmtpEmailProvider>();
        services.AddScoped<IChannelProvider, InAppProvider>();
        services.AddScoped<IChannelProviderFactory, ChannelProviderFactory>();

        // Integration providers (Slack, Telegram, Discord, Microsoft Teams)
        services.AddScoped<IIntegrationProvider, SlackProvider>();
        services.AddScoped<IIntegrationProvider, TelegramProvider>();
        services.AddScoped<IIntegrationProvider, DiscordProvider>();
        services.AddScoped<IIntegrationProvider, MicrosoftTeamsProvider>();
        services.AddScoped<IIntegrationProviderFactory, IntegrationProviderFactory>();

        // Core services
        services.AddScoped<IRecipientResolver, RecipientResolver>();
        services.AddScoped<IMessageDispatcher, MessageDispatcher>();
        services.AddScoped<TriggerRuleEvaluator>();
        services.AddScoped<ITriggerRuleEvaluator>(sp => sp.GetRequiredService<TriggerRuleEvaluator>());
        services.AddScoped<ICommunicationEventNotifier>(sp => sp.GetRequiredService<TriggerRuleEvaluator>());
        services.AddScoped<ITemplateRegistrar, TemplateRegistrarService>();

        // Usage metric calculator
        services.AddScoped<IUsageMetricCalculator, CommunicationUsageMetricCalculator>();

        // Background jobs
        services.AddHostedService<DeliveryLogCleanupJob>();

        return services;
    }

    public void ConfigureBus(IBusRegistrationConfigurator bus)
    {
        // Modules own their own bus surface — the host no longer auto-discovers
        // consumers from module assemblies (Tier 2.5 Theme 5 Phase D).
        bus.AddConsumers(typeof(CommunicationModule).Assembly);
    }

    public IEnumerable<(string Name, string Description, string Module)> GetPermissions()
    {
        yield return (CommunicationPermissions.View, "View communication channels, templates, and delivery logs", "Communication");
        yield return (CommunicationPermissions.ManageChannels, "Configure notification channel providers", "Communication");
        yield return (CommunicationPermissions.ManageIntegrations, "Configure team integrations (Slack, Telegram, etc.)", "Communication");
        yield return (CommunicationPermissions.ManageTemplates, "Create and edit message template overrides", "Communication");
        yield return (CommunicationPermissions.ManageTriggerRules, "Create and manage trigger rules for automated messaging", "Communication");
        yield return (CommunicationPermissions.ViewDeliveryLog, "View message delivery history and status", "Communication");
        yield return (CommunicationPermissions.Resend, "Resend failed message deliveries", "Communication");
        yield return (CommunicationPermissions.ManageQuotas, "View and manage messaging usage quotas", "Communication");
    }

    public IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions()
    {
        yield return ("SuperAdmin", [
            CommunicationPermissions.View,
            CommunicationPermissions.ManageChannels,
            CommunicationPermissions.ManageIntegrations,
            CommunicationPermissions.ManageTemplates,
            CommunicationPermissions.ManageTriggerRules,
            CommunicationPermissions.ViewDeliveryLog,
            CommunicationPermissions.Resend,
            CommunicationPermissions.ManageQuotas
        ]);
        yield return ("Admin", [
            CommunicationPermissions.View,
            CommunicationPermissions.ManageChannels,
            CommunicationPermissions.ManageIntegrations,
            CommunicationPermissions.ManageTemplates,
            CommunicationPermissions.ManageTriggerRules,
            CommunicationPermissions.ViewDeliveryLog,
            CommunicationPermissions.Resend
        ]);
        yield return ("User", [
            CommunicationPermissions.View,
            CommunicationPermissions.ViewDeliveryLog
        ]);
    }

    public async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CommunicationDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    public async Task SeedDataAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CommunicationDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("Communication.Seed");
        await SystemTemplateRegistrar.SeedAsync(context, logger);
        await EventRegistrar.SeedAsync(context, logger);
        // Plan 5d-2 Task F2: ensure every existing tenant has the four AI agent
        // approval categories marked as required InApp notifications. New tenants
        // pick up the rows via CommunicationTenantEventHandler.Handle(TenantCreatedEvent).
        await RequiredNotificationSeed.SeedAllTenantsAsync(context, appDb, logger, cancellationToken);
    }
}
