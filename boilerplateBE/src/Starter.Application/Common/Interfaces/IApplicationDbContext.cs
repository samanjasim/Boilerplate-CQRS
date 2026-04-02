using System.Data;
using Starter.Domain.ApiKeys.Entities;
using Starter.Domain.Billing.Entities;
using Starter.Domain.Common;
using Starter.Domain.FeatureFlags.Entities;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Tenants.Entities;
using Starter.Domain.Webhooks.Entities;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<Session> Sessions { get; }
    DbSet<LoginHistory> LoginHistory { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }
    DbSet<FileMetadata> FileMetadata { get; }
    DbSet<ReportRequest> ReportRequests { get; }
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<ApiKey> ApiKeys { get; }
    DbSet<FeatureFlag> FeatureFlags { get; }
    DbSet<TenantFeatureFlag> TenantFeatureFlags { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<TenantSubscription> TenantSubscriptions { get; }
    DbSet<PaymentRecord> PaymentRecords { get; }
    DbSet<PlanPriceHistory> PlanPriceHistories { get; }
    DbSet<WebhookEndpoint> WebhookEndpoints { get; }
    DbSet<WebhookDelivery> WebhookDeliveries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default);
}
