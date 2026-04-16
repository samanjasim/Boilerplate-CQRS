using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Modularity;
using Starter.Application.Common.Interfaces;
using Starter.Module.Communication.Domain.Entities;

namespace Starter.Module.Communication.Infrastructure.Persistence;

public sealed class CommunicationDbContext : DbContext, IModuleDbContext
{
    private readonly ICurrentUserService? _currentUserService;
    private Guid? CurrentTenantId => _currentUserService?.TenantId;

    public CommunicationDbContext(
        DbContextOptions<CommunicationDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<ChannelConfig> ChannelConfigs => Set<ChannelConfig>();
    public DbSet<IntegrationConfig> IntegrationConfigs => Set<IntegrationConfig>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<MessageTemplateOverride> MessageTemplateOverrides => Set<MessageTemplateOverride>();
    public DbSet<TriggerRule> TriggerRules => Set<TriggerRule>();
    public DbSet<TriggerRuleIntegrationTarget> TriggerRuleIntegrationTargets => Set<TriggerRuleIntegrationTarget>();
    public DbSet<DeliveryLog> DeliveryLogs => Set<DeliveryLog>();
    public DbSet<DeliveryAttempt> DeliveryAttempts => Set<DeliveryAttempt>();
    public DbSet<CommunicationNotificationPreference> NotificationPreferences => Set<CommunicationNotificationPreference>();
    public DbSet<RequiredNotification> RequiredNotifications => Set<RequiredNotification>();
    public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Tenant query filters — tenant-scoped entities
        modelBuilder.Entity<ChannelConfig>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<IntegrationConfig>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<MessageTemplateOverride>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<TriggerRule>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<DeliveryLog>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<CommunicationNotificationPreference>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<RequiredNotification>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);

        // MessageTemplate, EventRegistration — no tenant scope (system-wide)
        // DeliveryAttempt, TriggerRuleIntegrationTarget — child entities, filtered via parent
    }
}
