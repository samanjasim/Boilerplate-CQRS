using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Modularity;
using Starter.Application.Common.Interfaces;
using Starter.Module.CommentsActivity.Domain.Entities;

namespace Starter.Module.CommentsActivity.Infrastructure.Persistence;

/// <summary>
/// Module-owned DbContext for the Comments &amp; Activity module. Uses a separate
/// migration history table (<c>__EFMigrationsHistory_CommentsActivity</c>) so the
/// module can be added or removed without touching core migrations.
/// </summary>
public sealed class CommentsActivityDbContext : DbContext, IModuleDbContext
{
    private readonly ICurrentUserService? _currentUserService;

    private Guid? CurrentTenantId => _currentUserService?.TenantId;

    public CommentsActivityDbContext(
        DbContextOptions<CommentsActivityDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<CommentAttachment> CommentAttachments => Set<CommentAttachment>();
    public DbSet<CommentReaction> CommentReactions => Set<CommentReaction>();
    public DbSet<ActivityEntry> ActivityEntries => Set<ActivityEntry>();
    public DbSet<EntityWatcher> EntityWatchers => Set<EntityWatcher>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<Comment>().HasQueryFilter(c =>
            CurrentTenantId == null || c.TenantId == CurrentTenantId);

        modelBuilder.Entity<ActivityEntry>().HasQueryFilter(a =>
            CurrentTenantId == null || a.TenantId == CurrentTenantId);

        modelBuilder.Entity<EntityWatcher>().HasQueryFilter(w =>
            CurrentTenantId == null || w.TenantId == CurrentTenantId);
    }
}
