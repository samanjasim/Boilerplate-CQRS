using Amazon.S3;
using QuestPDF.Infrastructure;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Application.Common.Interfaces;
using Starter.Infrastructure.Capabilities;
using Starter.Infrastructure.Capabilities.Adapters;
using Starter.Infrastructure.Capabilities.MetricCalculators;
using Starter.Infrastructure.Capabilities.NullObjects;
using Starter.Infrastructure.Email.Templates;
using Starter.Infrastructure.Persistence;
using Starter.Infrastructure.Persistence.Interceptors;
using Starter.Infrastructure.Readers;
using Starter.Infrastructure.Services;
using Starter.Infrastructure.Settings;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Starter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyList<System.Reflection.Assembly>? moduleAssemblies = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        services
            .AddPersistence(configuration)
            .AddCaching(configuration)
            .AddMessaging(configuration, moduleAssemblies)
            .AddServices()
            .AddCapabilities()
            .AddEmailServices(configuration)
            .AddSmsServices(configuration)
            .AddRealtimeServices(configuration)
            .AddStorageServices(configuration)
            .AddExportServices()
            .AddHealthChecks(configuration);

        return services;
    }

    /// <summary>
    /// Registers cross-context reader services and Null Object fallbacks for
    /// every capability contract. Modules override these defaults by calling
    /// <c>AddScoped&lt;TService, TImpl&gt;()</c> (or <c>AddSingleton</c>) in
    /// their own <c>ConfigureServices</c> — the last registration wins.
    ///
    /// Each null fallback is registered with the SAME lifetime as its real
    /// counterpart in the owning module, so swapping implementations does not
    /// shift lifetimes:
    /// <list type="bullet">
    ///   <item><c>NullBillingProvider</c> → Scoped (matches <c>MockBillingProvider</c>)</item>
    ///   <item><c>NullWebhookPublisher</c> → Scoped (matches <c>WebhookPublisher</c>)</item>
    ///   <item><c>NullImportExportRegistry</c> → Singleton (matches <c>ImportExportRegistry</c>)</item>
    ///   <item><c>NullQuotaChecker</c> → Singleton (stateless; no module override yet)</item>
    /// </list>
    /// </summary>
    private static IServiceCollection AddCapabilities(this IServiceCollection services)
    {
        // Cross-context readers — always real implementations
        services.AddScoped<ITenantReader, TenantReader>();
        services.AddScoped<IUserReader, UserReader>();
        services.AddScoped<IRoleReader, RoleReader>();
        services.AddScoped<IFileReader, FileReader>();
        services.AddScoped<INotificationPreferenceReader, NotificationPreferenceReaderService>();

        // Null Object fallbacks — lifetimes match the real module implementations
        services.TryAddSingleton<IQuotaChecker, NullQuotaChecker>();
        services.TryAddScoped<IBillingProvider, NullBillingProvider>();
        services.TryAddScoped<IWebhookPublisher, NullWebhookPublisher>();
        services.TryAddSingleton<IImportExportRegistry, NullImportExportRegistry>();
        services.TryAddScoped<IMessageDispatcher, NullMessageDispatcher>();
        services.TryAddScoped<ICommunicationEventNotifier, NullCommunicationEventNotifier>();
        services.TryAddScoped<ITemplateRegistrar, NullTemplateRegistrar>();

        // Comments & Activity — Null Object fallbacks
        services.TryAddScoped<INotificationPreferenceReader, NullNotificationPreferenceReader>();
        services.TryAddSingleton<ICommentableEntityRegistry, NullCommentableEntityRegistry>();
        services.TryAddScoped<ICommentService, NullCommentService>();
        services.TryAddScoped<IActivityService, NullActivityService>();
        services.TryAddScoped<IEntityWatcherService, NullEntityWatcherService>();

        // Notifications capability — registration order matters.
        //
        // Line 1 (TryAddScoped Null): only registers if no INotificationServiceCapability
        // is already bound. Acts as a fallback for isolated module tests that don't
        // wire the host's notification stack.
        //
        // Line 2 (AddScoped Adapter): appends the real adapter. When the container
        // resolves a single INotificationServiceCapability, MSDI returns the LAST
        // registration for that service — so the Adapter wins in the host. The Null
        // remains reachable only via IEnumerable<INotificationServiceCapability>,
        // which nothing currently injects.
        //
        // Do not swap these lines or convert line 2 to TryAddScoped — the Null would
        // win and notifications would silently no-op in production.
        services.TryAddScoped<INotificationServiceCapability, NullNotificationServiceCapability>();
        services.AddScoped<INotificationServiceCapability, NotificationServiceCapabilityAdapter>();

        // Core usage metric calculators — one per core-owned metric. Modules
        // that own their own counted entities (e.g. Webhooks) register
        // additional IUsageMetricCalculator implementations in their own
        // ConfigureServices. UsageTrackerService dispatches to whichever is
        // registered; unknown metrics silently return 0.
        services.AddScoped<IUsageMetricCalculator, UsersMetricCalculator>();
        services.AddScoped<IUsageMetricCalculator, ApiKeysMetricCalculator>();
        services.AddScoped<IUsageMetricCalculator, StorageBytesMetricCalculator>();
        services.AddScoped<IUsageMetricCalculator, ReportsActiveMetricCalculator>();

        return services;
    }

    private static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DomainEventDispatcherInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());

            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: ["40001"]);
                });
        });

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        return services;
    }

    private static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(redisConnectionString));

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "Starter:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddSingleton<ICacheService, CacheService>();

        return services;
    }

    private static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyList<System.Reflection.Assembly>? moduleAssemblies = null)
    {
        var rabbitMqEnabled = configuration.GetValue("RabbitMQ:Enabled", true);

        services.AddMassTransit(busConfigurator =>
        {
            busConfigurator.SetKebabCaseEndpointNameFormatter();

            // Transactional outbox: events published via IPublishEndpoint inside a
            // command handler are saved atomically with the business data in
            // ApplicationDbContext.SaveChangesAsync, then dispatched by a background
            // delivery service. Guarantees at-least-once delivery + idempotent
            // retries even if the broker is unreachable at commit time.
            busConfigurator.AddEntityFrameworkOutbox<ApplicationDbContext>(o =>
            {
                o.QueryDelay = TimeSpan.FromSeconds(1);
                o.UsePostgres();
                o.UseBusOutbox();
            });

            // Auto-discover consumers from core Infrastructure assembly
            busConfigurator.AddConsumers(typeof(DependencyInjection).Assembly);

            // Auto-discover consumers from module assemblies
            if (moduleAssemblies is not null)
            {
                foreach (var asm in moduleAssemblies)
                    busConfigurator.AddConsumers(asm);
            }

            if (!rabbitMqEnabled)
            {
                busConfigurator.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
                return;
            }

            busConfigurator.UsingRabbitMq((context, cfg) =>
            {
                var host = configuration["RabbitMQ:Host"] ?? "localhost";
                var username = configuration["RabbitMQ:Username"] ?? "guest";
                var password = configuration["RabbitMQ:Password"] ?? "guest";
                var virtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/";
                var port = ushort.TryParse(configuration["RabbitMQ:Port"], out var p) ? p : (ushort)5672;

                cfg.Host(host, port, virtualHost, h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeService, DateTimeService>();
        services.AddScoped<IMessagePublisher, MassTransitMessagePublisher>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<ISettingsProvider, SettingsProvider>();
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();
        services.AddScoped<IPermissionHierarchyService, PermissionHierarchyService>();
        services.AddScoped<IUsageTracker, UsageTrackerService>();

        return services;
    }

    private static IServiceCollection AddEmailServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SmtpSettings>(
            configuration.GetSection(SmtpSettings.SectionName));

        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddSingleton<IEmailTemplateService, EmailTemplateService>();

        return services;
    }

    private static IServiceCollection AddSmsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<TwilioSettings>(
            configuration.GetSection(TwilioSettings.SectionName));

        var twilioEnabled = configuration.GetValue($"{TwilioSettings.SectionName}:Enabled", false);

        if (twilioEnabled)
        {
            services.AddHttpClient();
            services.AddScoped<ISmsService, TwilioSmsService>();
        }
        else
        {
            services.AddScoped<ISmsService, LogOnlySmsService>();
        }

        return services;
    }

    private static IServiceCollection AddRealtimeServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AblySettings>(
            configuration.GetSection(AblySettings.SectionName));

        var ablyEnabled = configuration.GetValue($"{AblySettings.SectionName}:Enabled", false);

        if (ablyEnabled)
        {
            services.AddHttpClient<IRealtimeService, AblyRealtimeService>();
        }
        else
        {
            services.AddScoped<IRealtimeService, NoOpRealtimeService>();
        }

        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }

    private static IServiceCollection AddStorageServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection(StorageSettings.SectionName).Get<StorageSettings>() ?? new StorageSettings();
        services.Configure<StorageSettings>(configuration.GetSection(StorageSettings.SectionName));

        var s3Config = new AmazonS3Config
        {
            ForcePathStyle = settings.ForcePathStyle,
        };

        if (!string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            // When using a custom endpoint (MinIO), configure for compatibility:
            // - ServiceURL + AuthenticationRegion instead of RegionEndpoint
            // - Disable request checksums (AWS SDK v4 sends CRC32 by default, MinIO rejects it)
            s3Config.ServiceURL = settings.Endpoint;
            s3Config.AuthenticationRegion = settings.Region;
            s3Config.RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED;
            s3Config.ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED;
        }
        else
        {
            s3Config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(settings.Region);
        }

        var s3Client = new AmazonS3Client(settings.AccessKey, settings.SecretKey, s3Config);
        services.AddSingleton<IAmazonS3>(s3Client);
        services.AddScoped<IStorageService, S3StorageService>();
        services.AddScoped<IFileService, FileService>();

        services.AddHostedService<StorageBucketInitializer>();
        services.AddHostedService<OrphanFileCleanupService>();

        return services;
    }

    private static IServiceCollection AddExportServices(this IServiceCollection services)
    {
        services.AddScoped<IExportService, ExportService>();

        return services;
    }

    private static IServiceCollection AddHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var healthChecksBuilder = services.AddHealthChecks()
            .AddNpgSql(
                configuration.GetConnectionString("DefaultConnection")!,
                name: "postgresql",
                tags: ["db", "sql", "postgresql"]);

        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            healthChecksBuilder.AddRedis(
                redisConnectionString,
                name: "redis",
                tags: ["cache", "redis"]);
        }

        return services;
    }
}
