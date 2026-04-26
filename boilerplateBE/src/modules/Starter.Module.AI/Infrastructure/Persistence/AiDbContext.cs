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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Tenant query filters — 6 entities are tenant-scoped
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
    }
}
