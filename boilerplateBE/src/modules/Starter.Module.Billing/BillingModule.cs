using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starter.Abstractions.Capabilities;
using Starter.Module.Billing.Domain.Entities;
using Starter.Abstractions.Modularity;
using Starter.Module.Billing.Constants;
using Starter.Module.Billing.Infrastructure.Persistence;
using Starter.Module.Billing.Infrastructure.Services;

namespace Starter.Module.Billing;

public sealed class BillingModule : IModule
{
    public string Name => "Starter.Module.Billing";
    public string DisplayName => "Billing";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => [];

    public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Module-owned DbContext — same database, isolated migration history.
        // Removing the Billing module also removes this context registration,
        // and the __EFMigrationsHistory_Billing table can be dropped without
        // touching core or other modules.
        services.AddDbContext<BillingDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_Billing");
                    npgsqlOptions.MigrationsAssembly(typeof(BillingDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: ["40001"]);
                });
        });

        services.AddScoped<IBillingProvider, MockBillingProvider>();
        return services;
    }

    public IEnumerable<(string Name, string Description, string Module)> GetPermissions()
    {
        yield return (BillingPermissions.View, "View subscription and usage", "Billing");
        yield return (BillingPermissions.Manage, "Change plan and cancel subscription", "Billing");
        yield return (BillingPermissions.ViewPlans, "View all subscription plans", "Billing");
        yield return (BillingPermissions.ManagePlans, "Create and manage subscription plans", "Billing");
        yield return (BillingPermissions.ManageTenantSubscriptions, "Manage tenant subscriptions", "Billing");
    }

    public IEnumerable<(string Role, string[] Permissions)> GetDefaultRolePermissions()
    {
        yield return ("SuperAdmin", [BillingPermissions.View, BillingPermissions.Manage,
                                     BillingPermissions.ViewPlans, BillingPermissions.ManagePlans,
                                     BillingPermissions.ManageTenantSubscriptions]);
        yield return ("Admin", [BillingPermissions.View, BillingPermissions.Manage]);
        yield return ("User", [BillingPermissions.View]);
    }

    public async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    public async Task SeedDataAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        if (await context.SubscriptionPlans.AnyAsync(cancellationToken))
            return;

        var freeFeatures = JsonSerializer.Serialize(new[]
        {
            new { key = "users.max_count", value = "5", translations = new { en = new { label = "Up to 5 users" }, ar = new { label = "حتى 5 مستخدمين" } } },
            new { key = "files.max_storage_mb", value = "1024", translations = new { en = new { label = "1 GB storage" }, ar = new { label = "1 جيجابايت تخزين" } } },
            new { key = "files.max_upload_size_mb", value = "10", translations = new { en = new { label = "10 MB max upload" }, ar = new { label = "10 ميجابايت حد أقصى للرفع" } } },
            new { key = "reports.enabled", value = "false", translations = new { en = new { label = "Reports disabled" }, ar = new { label = "التقارير معطلة" } } },
            new { key = "reports.pdf_export", value = "false", translations = new { en = new { label = "PDF export disabled" }, ar = new { label = "تصدير PDF معطل" } } },
            new { key = "api_keys.enabled", value = "true", translations = new { en = new { label = "API keys enabled" }, ar = new { label = "مفاتيح API مفعلة" } } },
            new { key = "api_keys.max_count", value = "2", translations = new { en = new { label = "Up to 2 API keys" }, ar = new { label = "حتى 2 مفاتيح API" } } },
            new { key = "users.invitations_enabled", value = "true", translations = new { en = new { label = "User invitations enabled" }, ar = new { label = "دعوات المستخدمين مفعلة" } } },
            new { key = "webhooks.enabled", value = "false", translations = new { en = new { label = "Webhooks disabled" }, ar = new { label = "الويب هوك معطل" } } },
            new { key = "webhooks.max_count", value = "0", translations = new { en = new { label = "No webhooks" }, ar = new { label = "لا ويب هوك" } } },
            new { key = "imports.enabled", value = "false", translations = new { en = new { label = "Imports disabled" }, ar = new { label = "الاستيراد معطل" } } },
            new { key = "imports.max_rows", value = "0", translations = new { en = new { label = "No imports" }, ar = new { label = "لا استيراد" } } },
            new { key = "exports.enabled", value = "false", translations = new { en = new { label = "Exports disabled" }, ar = new { label = "التصدير معطل" } } },
        });

        var starterFeatures = JsonSerializer.Serialize(new[]
        {
            new { key = "users.max_count", value = "25", translations = new { en = new { label = "Up to 25 users" }, ar = new { label = "حتى 25 مستخدم" } } },
            new { key = "files.max_storage_mb", value = "10240", translations = new { en = new { label = "10 GB storage" }, ar = new { label = "10 جيجابايت تخزين" } } },
            new { key = "files.max_upload_size_mb", value = "25", translations = new { en = new { label = "25 MB max upload" }, ar = new { label = "25 ميجابايت حد أقصى للرفع" } } },
            new { key = "reports.enabled", value = "true", translations = new { en = new { label = "Reports enabled" }, ar = new { label = "التقارير مفعلة" } } },
            new { key = "reports.max_concurrent", value = "3", translations = new { en = new { label = "Up to 3 concurrent reports" }, ar = new { label = "حتى 3 تقارير متزامنة" } } },
            new { key = "reports.pdf_export", value = "false", translations = new { en = new { label = "PDF export disabled" }, ar = new { label = "تصدير PDF معطل" } } },
            new { key = "api_keys.enabled", value = "true", translations = new { en = new { label = "API keys enabled" }, ar = new { label = "مفاتيح API مفعلة" } } },
            new { key = "api_keys.max_count", value = "5", translations = new { en = new { label = "Up to 5 API keys" }, ar = new { label = "حتى 5 مفاتيح API" } } },
            new { key = "users.invitations_enabled", value = "true", translations = new { en = new { label = "User invitations enabled" }, ar = new { label = "دعوات المستخدمين مفعلة" } } },
            new { key = "roles.tenant_custom_enabled", value = "true", translations = new { en = new { label = "Custom roles enabled" }, ar = new { label = "الأدوار المخصصة مفعلة" } } },
            new { key = "webhooks.enabled", value = "true", translations = new { en = new { label = "Webhooks enabled" }, ar = new { label = "الويب هوك مفعل" } } },
            new { key = "webhooks.max_count", value = "3", translations = new { en = new { label = "Up to 3 webhooks" }, ar = new { label = "حتى 3 ويب هوك" } } },
            new { key = "imports.enabled", value = "true", translations = new { en = new { label = "Imports enabled" }, ar = new { label = "الاستيراد مفعل" } } },
            new { key = "imports.max_rows", value = "500", translations = new { en = new { label = "Up to 500 rows per import" }, ar = new { label = "حتى 500 صف لكل استيراد" } } },
            new { key = "exports.enabled", value = "true", translations = new { en = new { label = "Data exports" }, ar = new { label = "تصدير البيانات" } } },
        });

        var proFeatures = JsonSerializer.Serialize(new[]
        {
            new { key = "users.max_count", value = "100", translations = new { en = new { label = "Up to 100 users" }, ar = new { label = "حتى 100 مستخدم" } } },
            new { key = "files.max_storage_mb", value = "51200", translations = new { en = new { label = "50 GB storage" }, ar = new { label = "50 جيجابايت تخزين" } } },
            new { key = "files.max_upload_size_mb", value = "50", translations = new { en = new { label = "50 MB max upload" }, ar = new { label = "50 ميجابايت حد أقصى للرفع" } } },
            new { key = "reports.enabled", value = "true", translations = new { en = new { label = "Reports enabled" }, ar = new { label = "التقارير مفعلة" } } },
            new { key = "reports.max_concurrent", value = "5", translations = new { en = new { label = "Up to 5 concurrent reports" }, ar = new { label = "حتى 5 تقارير متزامنة" } } },
            new { key = "reports.pdf_export", value = "true", translations = new { en = new { label = "PDF export enabled" }, ar = new { label = "تصدير PDF مفعل" } } },
            new { key = "api_keys.enabled", value = "true", translations = new { en = new { label = "API keys enabled" }, ar = new { label = "مفاتيح API مفعلة" } } },
            new { key = "api_keys.max_count", value = "20", translations = new { en = new { label = "Up to 20 API keys" }, ar = new { label = "حتى 20 مفتاح API" } } },
            new { key = "users.invitations_enabled", value = "true", translations = new { en = new { label = "User invitations enabled" }, ar = new { label = "دعوات المستخدمين مفعلة" } } },
            new { key = "roles.tenant_custom_enabled", value = "true", translations = new { en = new { label = "Custom roles enabled" }, ar = new { label = "الأدوار المخصصة مفعلة" } } },
            new { key = "webhooks.enabled", value = "true", translations = new { en = new { label = "Webhooks enabled" }, ar = new { label = "الويب هوك مفعل" } } },
            new { key = "webhooks.max_count", value = "10", translations = new { en = new { label = "Up to 10 webhooks" }, ar = new { label = "حتى 10 ويب هوك" } } },
            new { key = "imports.enabled", value = "true", translations = new { en = new { label = "Imports enabled" }, ar = new { label = "الاستيراد مفعل" } } },
            new { key = "imports.max_rows", value = "5000", translations = new { en = new { label = "Up to 5,000 rows per import" }, ar = new { label = "حتى 5,000 صف لكل استيراد" } } },
            new { key = "exports.enabled", value = "true", translations = new { en = new { label = "Data exports" }, ar = new { label = "تصدير البيانات" } } },
        });

        var enterpriseFeatures = JsonSerializer.Serialize(new[]
        {
            new { key = "users.max_count", value = "500", translations = new { en = new { label = "Up to 500 users" }, ar = new { label = "حتى 500 مستخدم" } } },
            new { key = "files.max_storage_mb", value = "204800", translations = new { en = new { label = "200 GB storage" }, ar = new { label = "200 جيجابايت تخزين" } } },
            new { key = "files.max_upload_size_mb", value = "100", translations = new { en = new { label = "100 MB max upload" }, ar = new { label = "100 ميجابايت حد أقصى للرفع" } } },
            new { key = "reports.enabled", value = "true", translations = new { en = new { label = "Reports enabled" }, ar = new { label = "التقارير مفعلة" } } },
            new { key = "reports.max_concurrent", value = "10", translations = new { en = new { label = "Up to 10 concurrent reports" }, ar = new { label = "حتى 10 تقارير متزامنة" } } },
            new { key = "reports.pdf_export", value = "true", translations = new { en = new { label = "PDF export enabled" }, ar = new { label = "تصدير PDF مفعل" } } },
            new { key = "api_keys.enabled", value = "true", translations = new { en = new { label = "API keys enabled" }, ar = new { label = "مفاتيح API مفعلة" } } },
            new { key = "api_keys.max_count", value = "50", translations = new { en = new { label = "Up to 50 API keys" }, ar = new { label = "حتى 50 مفتاح API" } } },
            new { key = "users.invitations_enabled", value = "true", translations = new { en = new { label = "User invitations enabled" }, ar = new { label = "دعوات المستخدمين مفعلة" } } },
            new { key = "roles.tenant_custom_enabled", value = "true", translations = new { en = new { label = "Custom roles enabled" }, ar = new { label = "الأدوار المخصصة مفعلة" } } },
            new { key = "webhooks.enabled", value = "true", translations = new { en = new { label = "Webhooks enabled" }, ar = new { label = "الويب هوك مفعل" } } },
            new { key = "webhooks.max_count", value = "25", translations = new { en = new { label = "Up to 25 webhooks" }, ar = new { label = "حتى 25 ويب هوك" } } },
            new { key = "imports.enabled", value = "true", translations = new { en = new { label = "Imports enabled" }, ar = new { label = "الاستيراد مفعل" } } },
            new { key = "imports.max_rows", value = "50000", translations = new { en = new { label = "Up to 50,000 rows per import" }, ar = new { label = "حتى 50,000 صف لكل استيراد" } } },
            new { key = "exports.enabled", value = "true", translations = new { en = new { label = "Data exports" }, ar = new { label = "تصدير البيانات" } } },
        });

        var plans = new[]
        {
            SubscriptionPlan.Create(
                "Free",
                "free",
                "Get started with basic features",
                "{\"en\":{\"name\":\"Free\",\"description\":\"Get started with basic features\"},\"ar\":{\"name\":\"مجاني\",\"description\":\"ابدأ بالميزات الأساسية\"},\"ku\":{\"name\":\"بەخۆڕایی\",\"description\":\"دەست پێبکە بە تایبەتمەندییە بنەڕەتییەکان\"}}",
                0m,
                0m,
                "USD",
                freeFeatures,
                isFree: true,
                isPublic: true,
                displayOrder: 0,
                trialDays: 0),

            SubscriptionPlan.Create(
                "Basic",
                "basic",
                "For small teams getting started",
                "{\"en\":{\"name\":\"Starter\",\"description\":\"For small teams getting started\"},\"ar\":{\"name\":\"المبتدئ\",\"description\":\"للفرق الصغيرة التي تبدأ للتو\"},\"ku\":{\"name\":\"دەستپێکەر\",\"description\":\"بۆ تیمە بچووکەکان کە دەیانەوێت دەست پێبکەن\"}}",
                29m,
                290m,
                "USD",
                starterFeatures,
                isFree: false,
                isPublic: true,
                displayOrder: 1,
                trialDays: 0),

            SubscriptionPlan.Create(
                "Pro",
                "pro",
                "For growing teams with advanced needs",
                "{\"en\":{\"name\":\"Pro\",\"description\":\"For growing teams with advanced needs\"},\"ar\":{\"name\":\"احترافي\",\"description\":\"للفرق المتنامية ذات الاحتياجات المتقدمة\"},\"ku\":{\"name\":\"پرۆفیشنال\",\"description\":\"بۆ تیمە گەشەکانی کە پێویستییانی پێشکەوتوویان هەیە\"}}",
                99m,
                990m,
                "USD",
                proFeatures,
                isFree: false,
                isPublic: true,
                displayOrder: 2,
                trialDays: 0),

            SubscriptionPlan.Create(
                "Enterprise",
                "enterprise",
                "For large organizations with custom requirements",
                "{\"en\":{\"name\":\"Enterprise\",\"description\":\"For large organizations with custom requirements\"},\"ar\":{\"name\":\"المؤسسي\",\"description\":\"للمؤسسات الكبيرة ذات المتطلبات المخصصة\"},\"ku\":{\"name\":\"کۆمپانیا\",\"description\":\"بۆ ڕێکخراوە گەورەکان کە پێویستییە تایبەتەکانیان هەیە\"}}",
                299m,
                2990m,
                "USD",
                enterpriseFeatures,
                isFree: false,
                isPublic: true,
                displayOrder: 3,
                trialDays: 0),
        };

        context.SubscriptionPlans.AddRange(plans);
        await context.SaveChangesAsync(cancellationToken);

        // Seed price history baselines
        foreach (var plan in plans)
        {
            context.PlanPriceHistories.Add(
                PlanPriceHistory.Create(plan.Id, plan.MonthlyPrice, plan.AnnualPrice, plan.Currency, Guid.Empty, "Initial plan creation"));
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
