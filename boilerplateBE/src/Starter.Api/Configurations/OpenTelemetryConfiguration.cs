using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

namespace Starter.Api.Configurations;

/// <summary>
/// OpenTelemetry observability configuration for distributed tracing and metrics.
/// </summary>
public static class OpenTelemetryConfiguration
{
    public static IServiceCollection AddOpenTelemetryObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>("OpenTelemetry:Enabled");
        if (!enabled)
            return services;

        var serviceName = configuration.GetValue<string>("OpenTelemetry:ServiceName") ?? "starter-api";
        var otlpEndpoint = configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint") ?? "http://localhost:4318";

        // Enable unencrypted HTTP/2 for gRPC on localhost (Windows/.NET requirement)
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        // Per-signal AddOtlpExporter with explicit full endpoint paths.
        // The Endpoint property setter disables auto path-appending, so we
        // provide the complete URL including /v1/traces and /v1/metrics.
        var tracesEndpoint = new Uri($"{otlpEndpoint}/v1/traces");
        var metricsEndpoint = new Uri($"{otlpEndpoint}/v1/metrics");

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = ctx =>
                        !ctx.Request.Path.StartsWithSegments("/health");
                    options.RecordException = true;
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = true;
                })
                .AddEntityFrameworkCoreInstrumentation(options =>
                {
                    options.SetDbStatementForText = true;
                    options.SetDbStatementForStoredProcedure = true;
                })
                .AddSource("MassTransit")
                .AddSource("Starter.Api")
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = tracesEndpoint;
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = metricsEndpoint;
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                }));

        // Reconfigure Serilog to also ship logs via OTLP
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = $"{otlpEndpoint}/v1/logs";
                options.Protocol = OtlpProtocol.HttpProtobuf;
            })
            .CreateLogger();

        services.AddHostedService<RedisInstrumentationHostedService>();

        return services;
    }
}

/// <summary>
/// Hosted service hook for Redis instrumentation.
/// StackExchange.Redis auto-instruments via DiagnosticSource when the
/// OpenTelemetry.Instrumentation.StackExchangeRedis package is referenced.
/// This service provides a registration point for future explicit connection wiring.
/// </summary>
internal sealed class RedisInstrumentationHostedService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
