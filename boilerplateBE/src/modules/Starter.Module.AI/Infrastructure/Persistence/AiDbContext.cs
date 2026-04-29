using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Modularity;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Persistence;

public sealed class AiDbContext : DbContext, IModuleDbContext
{
    private readonly ICurrentUserService? _currentUserService;
    private Guid? CurrentTenantId => _currentUserService?.TenantId;

    public AiDbContext(
        DbContextOptions<AiDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<AiAssistant> AiAssistants => Set<AiAssistant>();
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<AiMessage> AiMessages => Set<AiMessage>();
    public DbSet<AiDocument> AiDocuments => Set<AiDocument>();
    public DbSet<AiDocumentChunk> AiDocumentChunks => Set<AiDocumentChunk>();
    public DbSet<AiTool> AiTools => Set<AiTool>();
    public DbSet<AiAgentTask> AiAgentTasks => Set<AiAgentTask>();
    public DbSet<AiAgentTrigger> AiAgentTriggers => Set<AiAgentTrigger>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();
    public DbSet<AiPersona> AiPersonas => Set<AiPersona>();
    public DbSet<UserPersona> UserPersonas => Set<UserPersona>();
    public DbSet<AiRoleMetadata> AiRoleMetadataEntries => Set<AiRoleMetadata>();
    public DbSet<AiAgentPrincipal> AiAgentPrincipals => Set<AiAgentPrincipal>();
    public DbSet<AiAgentRole> AiAgentRoles => Set<AiAgentRole>();
    public DbSet<AiModelPricing> AiModelPricings => Set<AiModelPricing>();
    public DbSet<AiSafetyPresetProfile> AiSafetyPresetProfiles => Set<AiSafetyPresetProfile>();
    public DbSet<AiModerationEvent> AiModerationEvents => Set<AiModerationEvent>();
    public DbSet<AiPendingApproval> AiPendingApprovals => Set<AiPendingApproval>();
    public DbSet<AiTenantSettings> AiTenantSettings => Set<AiTenantSettings>();
    public DbSet<AiProviderCredential> AiProviderCredentials => Set<AiProviderCredential>();
    public DbSet<AiModelDefault> AiModelDefaults => Set<AiModelDefault>();
    public DbSet<AiPublicWidget> AiPublicWidgets => Set<AiPublicWidget>();
    public DbSet<AiWidgetCredential> AiWidgetCredentials => Set<AiWidgetCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Tenant query filters for tenant-scoped AI entities.
        modelBuilder.Entity<AiAssistant>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiConversation>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiDocument>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiAgentTask>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiAgentTrigger>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiUsageLog>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiPersona>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<UserPersona>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiAgentPrincipal>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiModerationEvent>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiPendingApproval>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiTenantSettings>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiProviderCredential>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiModelDefault>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiPublicWidget>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AiWidgetCredential>().HasQueryFilter(e =>
            CurrentTenantId == null || e.TenantId == CurrentTenantId);
    }
}
