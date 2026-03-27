# OpenTelemetry Tracing Design Spec

**Date:** 2026-03-27
**Feature:** OpenTelemetry Tracing (T1, Stream C)
**Scope:** Backend-only distributed tracing, auto-instrumented metrics, and log correlation via OTLP export to Jaeger.

---

## Goals

1. **Distributed tracing** across all backend components: HTTP → MediatR → EF Core → Redis → RabbitMQ
2. **Log correlation** — every Serilog log entry carries TraceId + SpanId, bridged to OTLP
3. **Zero-config dev experience** — `docker compose up` starts Jaeger; traces appear at `http://localhost:16686`
4. **Production-ready** — configurable OTLP endpoint, toggle on/off, zero overhead when disabled

## Non-Goals

- Custom business metrics (counters, gauges, histograms) — deferred; only auto-instrumented metrics from ASP.NET/EF Core
- Frontend tracing or browser spans
- Alerting or dashboards (Jaeger provides trace search/visualization only)
- Custom span annotations in individual command handlers (only the generic TracingBehavior)

---

## Architecture

### Instrumentation Stack

```
┌─────────────────────────────────────────────────────────┐
│  ASP.NET Core (auto)                                    │
│    → TracingBehavior (MediatR span)                     │
│      → EF Core (auto)                                   │
│      → Redis (auto via StackExchange instrumentation)   │
│      → HttpClient (auto)                                │
│      → MassTransit (auto via MassTransit.OpenTelemetry) │
├─────────────────────────────────────────────────────────┤
│  Serilog → OpenTelemetry Sink (log bridge)              │
├─────────────────────────────────────────────────────────┤
│  OTLP gRPC Exporter → Jaeger (localhost:4317)           │
└─────────────────────────────────────────────────────────┘
```

### Trace Flow Example

```
HTTP POST /api/v1/api-keys (ASP.NET span)
  └── MediatR: CreateApiKeyCommand (TracingBehavior span)
      ├── EF Core: INSERT INTO api_keys (auto span)
      ├── Redis: SETEX cache:api-key:... (auto span)
      └── MassTransit: Publish ApiKeyCreatedEvent (auto span)
          └── Consumer: HandleApiKeyCreated (auto span)
              └── EF Core: INSERT INTO audit_logs (auto span)
```

Each span carries: `service.name`, `tenant.id` (from current user), request name, duration, status.

---

## Configuration

### appsettings.json (base)

```json
"OpenTelemetry": {
  "Enabled": false,
  "ServiceName": "starter-api",
  "OtlpEndpoint": "http://localhost:4317"
}
```

### appsettings.Development.json (override)

```json
"OpenTelemetry": {
  "Enabled": true
}
```

### appsettings.Production.json (override)

```json
"OpenTelemetry": {
  "Enabled": true,
  "OtlpEndpoint": "http://otel-collector:4317"
}
```

When `Enabled: false`, no OTel SDK is registered. Zero packages loaded, zero overhead.

---

## NuGet Packages

Added to `Directory.Packages.props`:

| Package | Version | Used By |
|---------|---------|---------|
| `OpenTelemetry.Extensions.Hosting` | 1.12.* | Starter.Api |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.12.* | Starter.Api |
| `OpenTelemetry.Instrumentation.Http` | 1.12.* | Starter.Api |
| `OpenTelemetry.Instrumentation.EntityFrameworkCore` | 1.0.0-beta.* | Starter.Api |
| `OpenTelemetry.Instrumentation.StackExchangeRedis` | 1.12.* | Starter.Infrastructure |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.12.* | Starter.Api |
| `OpenTelemetry.Api` | 1.12.* | Starter.Application (for TracingBehavior) |
| `Serilog.Sinks.OpenTelemetry` | 5.* | Starter.Api |
| `MassTransit.OpenTelemetry` | (bundled with MassTransit 8+) | — |

Note: MassTransit 8+ includes OpenTelemetry instrumentation out of the box via `DiagnosticSource`. No separate package needed — just ensure the OTel SDK is registered and it picks up MassTransit spans automatically.

---

## Files — Create / Modify / Delete

### New Files

#### 1. `Starter.Api/Configurations/OpenTelemetryConfiguration.cs`

Extension method `AddOpenTelemetryObservability(IServiceCollection, IConfiguration)`:

```csharp
public static class OpenTelemetryConfiguration
{
    public static IServiceCollection AddOpenTelemetryObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection("OpenTelemetry");
        if (!settings.GetValue<bool>("Enabled")) return services;

        var serviceName = settings.GetValue<string>("ServiceName") ?? "starter-api";
        var otlpEndpoint = settings.GetValue<string>("OtlpEndpoint") ?? "http://localhost:4317";

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(o =>
                    {
                        o.RecordException = true;
                        o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                    })
                    .AddHttpClientInstrumentation(o => o.RecordException = true)
                    .AddEntityFrameworkCoreInstrumentation(o =>
                    {
                        o.SetDbStatementForText = true;
                        o.SetDbStatementForStoredProcedure = true;
                    })
                    .AddSource("MassTransit")    // MassTransit DiagnosticSource
                    .AddSource("Starter.Api")     // Custom activity sources
                    .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            });

        // Redis instrumentation requires the IConnectionMultiplexer after build
        // Registered as a hosted service that hooks into the connection post-startup
        services.AddHostedService<RedisInstrumentationHostedService>();

        return services;
    }
}
```

Redis instrumentation requires the live `IConnectionMultiplexer` instance. A small hosted service resolves it after DI is built:

```csharp
internal sealed class RedisInstrumentationHostedService : IHostedService
{
    private readonly IConnectionMultiplexer? _connection;
    private readonly TracerProvider? _tracerProvider;

    public RedisInstrumentationHostedService(
        IServiceProvider sp)
    {
        _connection = sp.GetService<IConnectionMultiplexer>();
        _tracerProvider = sp.GetService<TracerProvider>();
    }

    public Task StartAsync(CancellationToken ct)
    {
        if (_connection is not null && _tracerProvider is not null)
        {
            _connection.RegisterProfiler(/* StackExchange profiler */);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

#### 2. `Starter.Application/Common/Behaviors/TracingBehavior.cs`

MediatR pipeline behavior that wraps every command/query in a span:

```csharp
public sealed class TracingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly ActivitySource Source = new("Starter.Api");

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        using var activity = Source.StartActivity(
            $"MediatR {requestName}",
            ActivityKind.Internal);

        activity?.SetTag("mediatr.request_type", requestName);

        try
        {
            var response = await next(cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return response;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }
}
```

### Modified Files

#### 3. `Directory.Packages.props`

Add package versions under `<ItemGroup>`:

```xml
<!-- Use latest stable versions at implementation time via `dotnet add package` -->
<PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
<PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.*" />
<PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.*" />
<PackageVersion Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.*" />
<PackageVersion Include="OpenTelemetry.Instrumentation.StackExchangeRedis" Version="1.*" />
<PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.*" />
<PackageVersion Include="OpenTelemetry.Api" Version="1.*" />
<PackageVersion Include="Serilog.Sinks.OpenTelemetry" Version="5.*" />
```

#### 4. `Starter.Api/Starter.Api.csproj`

Add references:

```xml
<PackageReference Include="OpenTelemetry.Extensions.Hosting" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" />
<PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
<PackageReference Include="Serilog.Sinks.OpenTelemetry" />
```

#### 5. `Starter.Application/Starter.Application.csproj`

Add reference for TracingBehavior:

```xml
<PackageReference Include="OpenTelemetry.Api" />
```

#### 6. `Starter.Infrastructure/Starter.Infrastructure.csproj`

Add reference for Redis instrumentation:

```xml
<PackageReference Include="OpenTelemetry.Instrumentation.StackExchangeRedis" />
```

#### 7. `Program.cs`

```diff
+ builder.Services.AddOpenTelemetryObservability(builder.Configuration);

  // In middleware pipeline:
- app.UseMiddleware<CorrelationIdMiddleware>();
  // (removed — W3C traceparent replaces it)
```

#### 8. `Application/DependencyInjection.cs`

Register TracingBehavior in the pipeline (after LoggingBehavior, before ValidationBehavior):

```diff
  services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
+ services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TracingBehavior<,>));
  services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

#### 9. `ExceptionHandlingMiddleware.cs`

Replace correlation ID usage with Activity.Current.TraceId:

```diff
- var correlationId = context.Items["CorrelationId"]?.ToString();
+ var traceId = Activity.Current?.TraceId.ToString();
  // Use traceId in error response and logging
```

#### 10. `RequestLoggingMiddleware.cs`

Replace correlation ID with trace ID:

```diff
- var requestId = context.Items["CorrelationId"]?.ToString() ?? context.TraceIdentifier;
+ var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
```

#### 11. `docker-compose.yml`

Add Jaeger all-in-one:

```yaml
  jaeger:
    image: jaegertracing/jaeger:2
    container_name: starter-jaeger
    ports:
      - "16686:16686"   # Jaeger UI
      - "4317:4317"     # OTLP gRPC
      - "4318:4318"     # OTLP HTTP
    environment:
      - COLLECTOR_OTLP_ENABLED=true
    restart: unless-stopped
```

### Deleted Files

#### 12. `Starter.Api/Middleware/CorrelationIdMiddleware.cs`

Removed entirely. W3C `traceparent` header replaces `X-Correlation-Id`. `Activity.Current.TraceId` is the correlation ID.

---

## Serilog Log Bridge Details

The bridge works by adding the `Serilog.Sinks.OpenTelemetry` sink programmatically (not in appsettings.json) when OTel is enabled. This avoids the sink attempting to connect when OTel is disabled.

In `OpenTelemetryConfiguration.cs`, after registering the OTel SDK:

```csharp
// Programmatically add OTel sink to Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .WriteTo.OpenTelemetry(o =>
    {
        o.Endpoint = otlpEndpoint;
        o.Protocol = OtlpProtocol.Grpc;
    })
    .CreateLogger();
```

This approach reconfigures the logger after OTel setup, keeping the existing console/file sinks from appsettings.json and adding the OTLP sink on top.

---

## W3C Trace Context

After this change, all HTTP responses include the standard `traceparent` header automatically (added by ASP.NET Core's W3C propagation). Example:

```
traceparent: 00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01
```

Error responses include `traceId` in the problem details body (from ExceptionHandlingMiddleware) for client-side correlation.

---

## Testing Strategy

1. **Build verification** — `dotnet build` succeeds with all new packages
2. **Startup verification** — App starts with OTel enabled, connects to Jaeger
3. **Trace verification** — Make API calls, confirm traces appear in Jaeger UI (http://localhost:16686)
4. **MediatR span verification** — Traces show `MediatR {CommandName}` spans nested under HTTP spans
5. **EF Core span verification** — Database query spans appear under MediatR spans
6. **Log correlation** — Serilog log entries carry TraceId; visible in Jaeger log view
7. **Disabled mode** — Set `Enabled: false`, verify no OTel overhead, app still works normally
8. **Health check filtering** — `/health` endpoint does NOT generate traces (filtered out)

---

## Rollout

- Development: Jaeger container auto-starts with `docker compose up`. Traces available immediately at `http://localhost:16686`.
- Production: Configure `OpenTelemetry:OtlpEndpoint` to point to a hosted Jaeger/OTLP collector. Enable/disable via `OpenTelemetry:Enabled`.
