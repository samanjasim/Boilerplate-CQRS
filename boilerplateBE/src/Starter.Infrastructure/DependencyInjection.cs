using Amazon.S3;
using Amazon.S3.Util;
using Starter.Application.Common.Interfaces;
using Starter.Infrastructure.Email.Templates;
using Starter.Infrastructure.Persistence;
using Starter.Infrastructure.Persistence.Interceptors;
using Starter.Infrastructure.Services;
using Starter.Infrastructure.Settings;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Starter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddPersistence(configuration)
            .AddCaching(configuration)
            .AddMessaging(configuration)
            .AddServices()
            .AddEmailServices(configuration)
            .AddSmsServices(configuration)
            .AddRealtimeServices(configuration)
            .AddStorageServices(configuration)
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
                        errorCodesToAdd: null);
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
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(settings.Region),
            ForcePathStyle = settings.ForcePathStyle,
        };

        if (!string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            s3Config.ServiceURL = settings.Endpoint;
        }

        var s3Client = new AmazonS3Client(settings.AccessKey, settings.SecretKey, s3Config);
        services.AddSingleton<IAmazonS3>(s3Client);
        services.AddScoped<IStorageService, S3StorageService>();
        services.AddScoped<IFileService, FileService>();

        // Ensure bucket exists on startup
        Task.Run(async () =>
        {
            try
            {
                var bucketExists = await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, settings.BucketName);
                if (!bucketExists)
                {
                    await s3Client.PutBucketAsync(settings.BucketName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to create storage bucket '{settings.BucketName}': {ex.Message}");
            }
        });

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
