using Amazon.S3;
using QuestPDF.Infrastructure;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.ImportExport.Definitions;
using Starter.Infrastructure.Email.Templates;
using Starter.Infrastructure.Consumers;
using Starter.Infrastructure.Persistence;
using Starter.Infrastructure.Persistence.Interceptors;
using Starter.Infrastructure.Services;
using Starter.Infrastructure.Settings;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Starter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        services
            .AddPersistence(configuration)
            .AddCaching(configuration)
            .AddMessaging(configuration)
            .AddServices()
            .AddEmailServices(configuration)
            .AddSmsServices(configuration)
            .AddRealtimeServices(configuration)
            .AddStorageServices(configuration)
            .AddExportServices()
            .AddImportExportServices()
            .AddHealthChecks(configuration);

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
        IConfiguration configuration)
    {
        var rabbitMqEnabled = configuration.GetValue("RabbitMQ:Enabled", true);

        services.AddMassTransit(busConfigurator =>
        {
            busConfigurator.SetKebabCaseEndpointNameFormatter();

            busConfigurator.AddConsumer<GenerateReportConsumer>();
            busConfigurator.AddConsumer<DeliverWebhookConsumer>();

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

                cfg.ReceiveEndpoint("deliver-webhook", e =>
                {
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromMinutes(1),
                        TimeSpan.FromMinutes(5),
                        TimeSpan.FromMinutes(30),
                        TimeSpan.FromHours(2),
                        TimeSpan.FromHours(24)));
                    e.ConfigureConsumer<DeliverWebhookConsumer>(context);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddHttpClient(); // Required for DeliverWebhookConsumer
        services.AddSingleton<IDateTimeService, DateTimeService>();
        services.AddScoped<IMessagePublisher, MassTransitMessagePublisher>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<ISettingsProvider, SettingsProvider>();
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();
        services.AddScoped<IPermissionHierarchyService, PermissionHierarchyService>();
        services.AddScoped<IUsageTracker, UsageTrackerService>();
        services.AddScoped<IBillingProvider, MockBillingProvider>();
        services.AddScoped<IWebhookPublisher, WebhookPublisher>();

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
        services.AddHostedService<WebhookDeliveryCleanupJob>();

        return services;
    }

    private static IServiceCollection AddExportServices(this IServiceCollection services)
    {
        services.AddScoped<IExportService, ExportService>();

        return services;
    }

    private static IServiceCollection AddImportExportServices(this IServiceCollection services)
    {
        services.AddSingleton<IImportExportRegistry>(sp =>
        {
            var registry = new ImportExportRegistry();
            registry.Register(UserImportExportDefinition.Create());
            registry.Register(RoleImportExportDefinition.Create());
            return registry;
        });

        services.AddScoped<UserExportDataProvider>();
        services.AddScoped<UserImportRowProcessor>();
        services.AddScoped<RoleExportDataProvider>();
        services.AddScoped<RoleImportRowProcessor>();

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
