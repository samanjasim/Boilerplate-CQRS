# OpenTelemetry Tracing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add distributed tracing, auto-instrumented metrics, and log correlation via OTLP export to Jaeger across the entire .NET backend.

**Architecture:** A single `OpenTelemetryConfiguration` extension registers tracing (ASP.NET, EF Core, HttpClient, Redis, MassTransit) and metrics exporters. A `TracingBehavior` MediatR pipeline behavior creates spans for every CQRS handler. Serilog is bridged to OTLP programmatically. CorrelationIdMiddleware is replaced by W3C traceparent. Jaeger all-in-one runs in Docker.

**Tech Stack:** OpenTelemetry .NET SDK, Jaeger, Serilog.Sinks.OpenTelemetry, StackExchange.Redis instrumentation

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `boilerplateBE/src/Starter.Api/Configurations/OpenTelemetryConfiguration.cs` | OTel SDK registration: tracing, metrics, OTLP export, Redis hosted service, Serilog bridge |
| Create | `boilerplateBE/src/Starter.Application/Common/Behaviors/TracingBehavior.cs` | MediatR pipeline span per command/query |
| Modify | `boilerplateBE/Directory.Packages.props` | Add 8 NuGet package versions |
| Modify | `boilerplateBE/src/Starter.Api/Starter.Api.csproj` | Add 6 package references |
| Modify | `boilerplateBE/src/Starter.Application/Starter.Application.csproj` | Add OpenTelemetry.Api reference |
| Modify | `boilerplateBE/src/Starter.Infrastructure/Starter.Infrastructure.csproj` | Add Redis instrumentation reference |
| Modify | `boilerplateBE/src/Starter.Api/Program.cs` | Call AddOpenTelemetryObservability, remove UseCorrelationId |
| Modify | `boilerplateBE/src/Starter.Application/DependencyInjection.cs` | Register TracingBehavior |
| Modify | `boilerplateBE/src/Starter.Api/Middleware/ExceptionHandlingMiddleware.cs` | Use Activity.Current.TraceId |
| Modify | `boilerplateBE/src/Starter.Api/Middleware/RequestLoggingMiddleware.cs` | Use Activity.Current.TraceId |
| Modify | `boilerplateBE/src/Starter.Api/appsettings.json` | Add OpenTelemetry section (disabled) |
| Modify | `boilerplateBE/src/Starter.Api/appsettings.Development.json` | Add OpenTelemetry section (enabled) |
| Modify | `boilerplateBE/src/Starter.Api/appsettings.Production.json` | Add OpenTelemetry section (enabled, configurable endpoint) |
| Modify | `boilerplateBE/docker-compose.yml` | Add Jaeger container |
| Delete | `boilerplateBE/src/Starter.Api/Middleware/CorrelationIdMiddleware.cs` | Replaced by W3C traceparent |

---

### Task 1: Add NuGet Packages

**Files:**
- Modify: `boilerplateBE/Directory.Packages.props`
- Modify: `boilerplateBE/src/Starter.Api/Starter.Api.csproj`
- Modify: `boilerplateBE/src/Starter.Application/Starter.Application.csproj`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Starter.Infrastructure.csproj`

- [ ] **Step 1: Add package versions to Directory.Packages.props**

Open `boilerplateBE/Directory.Packages.props`. After the `<!-- Health Checks -->` section (line 53-55), before `<!-- Utils -->`, add:

```xml
    <!-- OpenTelemetry -->
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.12.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.12.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.12.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.0.0-beta.12" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.StackExchangeRedis" Version="1.12.0" />
    <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.12.0" />
    <PackageVersion Include="OpenTelemetry.Api" Version="1.12.0" />
    <PackageVersion Include="Serilog.Sinks.OpenTelemetry" Version="5.0.0" />
```

**Note:** After adding, run `dotnet restore` to verify the versions resolve. If a version doesn't exist, find the latest stable on NuGet.org and use that.

- [ ] **Step 2: Add package references to Starter.Api.csproj**

Open `boilerplateBE/src/Starter.Api/Starter.Api.csproj`. After `<PackageReference Include="Serilog.Enrichers.Thread" />` (line 18), add:

```xml
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" />
    <PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
    <PackageReference Include="Serilog.Sinks.OpenTelemetry" />
```

- [ ] **Step 3: Add OpenTelemetry.Api to Starter.Application.csproj**

Open `boilerplateBE/src/Starter.Application/Starter.Application.csproj`. After `<PackageReference Include="Microsoft.EntityFrameworkCore" />` (line 9), add:

```xml
    <PackageReference Include="OpenTelemetry.Api" />
```

- [ ] **Step 4: Add Redis instrumentation to Starter.Infrastructure.csproj**

Open `boilerplateBE/src/Starter.Infrastructure/Starter.Infrastructure.csproj`. After `<PackageReference Include="AspNetCore.HealthChecks.Redis" />` (line 19), add:

```xml
    <PackageReference Include="OpenTelemetry.Instrumentation.StackExchangeRedis" />
```

- [ ] **Step 5: Verify packages restore**

Run:
```bash
cd boilerplateBE && dotnet restore
```
Expected: Restore succeeds with no errors.

- [ ] **Step 6: Verify build**

Run:
```bash
cd boilerplateBE && dotnet build --no-restore
```
Expected: Build succeeds. (No code uses the packages yet — this just confirms versions resolve.)

- [ ] **Step 7: Commit**

```bash
git add boilerplateBE/Directory.Packages.props boilerplateBE/src/Starter.Api/Starter.Api.csproj boilerplateBE/src/Starter.Application/Starter.Application.csproj boilerplateBE/src/Starter.Infrastructure/Starter.Infrastructure.csproj
git commit -m "feat(otel): add OpenTelemetry NuGet packages"
```

---

### Task 2: Create TracingBehavior

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Common/Behaviors/TracingBehavior.cs`
- Modify: `boilerplateBE/src/Starter.Application/DependencyInjection.cs`

- [ ] **Step 1: Create TracingBehavior.cs**

Create `boilerplateBE/src/Starter.Application/Common/Behaviors/TracingBehavior.cs`:

```csharp
using System.Diagnostics;
using MediatR;

namespace Starter.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that creates an OpenTelemetry span for every command/query.
/// </summary>
public sealed class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
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
            var response = await next();
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

- [ ] **Step 2: Register TracingBehavior in DependencyInjection.cs**

Open `boilerplateBE/src/Starter.Application/DependencyInjection.cs`. Replace the MediatR configuration block (lines 15-22):

```csharp
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);

            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TracingBehavior<,>));
            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        });
```

The only change is adding the `TracingBehavior` line after `LoggingBehavior`.

- [ ] **Step 3: Verify build**

Run:
```bash
cd boilerplateBE && dotnet build
```
Expected: Build succeeds. TracingBehavior uses only `System.Diagnostics` (built-in) and `MediatR` — no OTel SDK dependency needed for creating activities.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/Starter.Application/Common/Behaviors/TracingBehavior.cs boilerplateBE/src/Starter.Application/DependencyInjection.cs
git commit -m "feat(otel): add MediatR TracingBehavior pipeline"
```

---

### Task 3: Create OpenTelemetryConfiguration

**Files:**
- Create: `boilerplateBE/src/Starter.Api/Configurations/OpenTelemetryConfiguration.cs`

- [ ] **Step 1: Create OpenTelemetryConfiguration.cs**

Create `boilerplateBE/src/Starter.Api/Configurations/OpenTelemetryConfiguration.cs`:

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;
using StackExchange.Redis;

namespace Starter.Api.Configurations;

/// <summary>
/// OpenTelemetry configuration: tracing, metrics, and Serilog OTLP bridge.
/// </summary>
public static class OpenTelemetryConfiguration
{
    public static IServiceCollection AddOpenTelemetryObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection("OpenTelemetry");
        if (!section.GetValue<bool>("Enabled"))
            return services;

        var serviceName = section.GetValue<string>("ServiceName") ?? "starter-api";
        var otlpEndpoint = section.GetValue<string>("OtlpEndpoint") ?? "http://localhost:4317";
        var endpointUri = new Uri(otlpEndpoint);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = ctx =>
                            !ctx.Request.Path.StartsWithSegments("/health");
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
                    .AddOtlpExporter(options => options.Endpoint = endpointUri);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(options => options.Endpoint = endpointUri);
            });

        // Redis instrumentation hooks into the live connection after DI is built
        services.AddHostedService(sp =>
            new RedisInstrumentationHostedService(sp, endpointUri));

        // Bridge Serilog to OTLP so logs carry trace context
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = otlpEndpoint;
                options.Protocol = OtlpProtocol.Grpc;
            })
            .CreateLogger();

        return services;
    }
}

/// <summary>
/// Hooks StackExchange.Redis instrumentation into the OpenTelemetry TracerProvider after startup.
/// </summary>
internal sealed class RedisInstrumentationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Uri _otlpEndpoint;

    public RedisInstrumentationHostedService(IServiceProvider serviceProvider, Uri otlpEndpoint)
    {
        _serviceProvider = serviceProvider;
        _otlpEndpoint = otlpEndpoint;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var connection = _serviceProvider.GetService<IConnectionMultiplexer>();
        if (connection is null) return Task.CompletedTask;

        // StackExchange.Redis instrumentation uses AddRedisInstrumentation on the tracing builder,
        // but it needs the live connection. We add it via the connection's built-in profiling.
        // The OTel SDK picks up Redis commands via DiagnosticSource automatically when the
        // StackExchange.Redis.OpenTelemetry package is referenced.
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

**Important implementation note:** The `OpenTelemetry.Instrumentation.StackExchangeRedis` package works by subscribing to `DiagnosticSource` events from StackExchange.Redis. Referencing the package and registering the OTel SDK is often sufficient. If traces don't appear, the tracing builder needs `.AddRedisInstrumentation(connection)` — but that requires the live `IConnectionMultiplexer`. The hosted service pattern above provides a hook point for this. During implementation, test whether Redis spans appear automatically; if not, convert the hosted service to call `AddRedisInstrumentation`.

- [ ] **Step 2: Verify build**

Run:
```bash
cd boilerplateBE && dotnet build
```
Expected: Build succeeds. The configuration is not yet called from Program.cs.

- [ ] **Step 3: Commit**

```bash
git add boilerplateBE/src/Starter.Api/Configurations/OpenTelemetryConfiguration.cs
git commit -m "feat(otel): add OpenTelemetry configuration extension"
```

---

### Task 4: Wire Up Program.cs and Remove CorrelationIdMiddleware

**Files:**
- Modify: `boilerplateBE/src/Starter.Api/Program.cs`
- Delete: `boilerplateBE/src/Starter.Api/Middleware/CorrelationIdMiddleware.cs`

- [ ] **Step 1: Add OpenTelemetry to Program.cs**

Open `boilerplateBE/src/Starter.Api/Program.cs`. After line 38 (`builder.Services.AddRateLimitingConfiguration(builder.Configuration);`), add:

```csharp
builder.Services.AddOpenTelemetryObservability(builder.Configuration);
```

- [ ] **Step 2: Remove CorrelationId middleware usage from Program.cs**

In the same file, remove line 49:

```csharp
app.UseCorrelationId();
```

- [ ] **Step 3: Delete CorrelationIdMiddleware.cs**

Delete the file `boilerplateBE/src/Starter.Api/Middleware/CorrelationIdMiddleware.cs`.

- [ ] **Step 4: Verify build**

Run:
```bash
cd boilerplateBE && dotnet build
```
Expected: Build succeeds. No references to `CorrelationIdMiddleware` or `UseCorrelationId` remain.

- [ ] **Step 5: Commit**

```bash
git add boilerplateBE/src/Starter.Api/Program.cs
git rm boilerplateBE/src/Starter.Api/Middleware/CorrelationIdMiddleware.cs
git commit -m "feat(otel): wire up OTel in Program.cs, remove CorrelationIdMiddleware"
```

---

### Task 5: Update Middleware to Use Activity.Current.TraceId

**Files:**
- Modify: `boilerplateBE/src/Starter.Api/Middleware/ExceptionHandlingMiddleware.cs`
- Modify: `boilerplateBE/src/Starter.Api/Middleware/RequestLoggingMiddleware.cs`

- [ ] **Step 1: Update ExceptionHandlingMiddleware.cs**

Open `boilerplateBE/src/Starter.Api/Middleware/ExceptionHandlingMiddleware.cs`.

Add import at top:
```csharp
using System.Diagnostics;
```

Replace the logging line in `HandleExceptionAsync` (line 56-58):

Before:
```csharp
        _logger.LogError(exception,
            "Exception occurred: {Message}. TraceId: {TraceId}",
            exception.Message,
            context.TraceIdentifier);
```

After:
```csharp
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        _logger.LogError(exception,
            "Exception occurred: {Message}. TraceId: {TraceId}",
            exception.Message,
            traceId);
```

- [ ] **Step 2: Update RequestLoggingMiddleware.cs**

Open `boilerplateBE/src/Starter.Api/Middleware/RequestLoggingMiddleware.cs`.

The file already imports `System.Diagnostics` (line 1). Replace `var requestId = context.TraceIdentifier;` (line 24) with:

```csharp
        var requestId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
```

And update both log messages to use `TraceId` instead of `RequestId` as the property name:

Line 26-29, replace:
```csharp
        _logger.LogInformation(
            "Request started: {Method} {Path} | RequestId: {RequestId}",
            context.Request.Method,
            context.Request.Path,
            requestId);
```

With:
```csharp
        _logger.LogInformation(
            "Request started: {Method} {Path} | TraceId: {TraceId}",
            context.Request.Method,
            context.Request.Path,
            requestId);
```

Line 40-45, replace:
```csharp
            _logger.LogInformation(
                "Request completed: {Method} {Path} | Status: {StatusCode} | Duration: {Duration}ms | RequestId: {RequestId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                requestId);
```

With:
```csharp
            _logger.LogInformation(
                "Request completed: {Method} {Path} | Status: {StatusCode} | Duration: {Duration}ms | TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                requestId);
```

- [ ] **Step 3: Verify build**

Run:
```bash
cd boilerplateBE && dotnet build
```
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add boilerplateBE/src/Starter.Api/Middleware/ExceptionHandlingMiddleware.cs boilerplateBE/src/Starter.Api/Middleware/RequestLoggingMiddleware.cs
git commit -m "feat(otel): use Activity.Current.TraceId in middleware"
```

---

### Task 6: Add Configuration to appsettings & Jaeger to Docker

**Files:**
- Modify: `boilerplateBE/src/Starter.Api/appsettings.json`
- Modify: `boilerplateBE/src/Starter.Api/appsettings.Development.json`
- Modify: `boilerplateBE/src/Starter.Api/appsettings.Production.json`
- Modify: `boilerplateBE/docker-compose.yml`

- [ ] **Step 1: Add OpenTelemetry section to appsettings.json**

Open `boilerplateBE/src/Starter.Api/appsettings.json`. Before the `"Serilog"` section (line 102), add:

```json
  "OpenTelemetry": {
    "Enabled": false,
    "ServiceName": "starter-api",
    "OtlpEndpoint": "http://localhost:4317"
  },
```

- [ ] **Step 2: Add OpenTelemetry section to appsettings.Development.json**

Open `boilerplateBE/src/Starter.Api/appsettings.Development.json`. Before the `"Serilog"` section (line 99), add:

```json
  "OpenTelemetry": {
    "Enabled": true
  },
```

- [ ] **Step 3: Add OpenTelemetry section to appsettings.Production.json**

Open `boilerplateBE/src/Starter.Api/appsettings.Production.json`. Before the `"Serilog"` section (line 29), add:

```json
  "OpenTelemetry": {
    "Enabled": true,
    "OtlpEndpoint": "http://otel-collector:4317"
  },
```

- [ ] **Step 4: Add Jaeger to docker-compose.yml**

Open `boilerplateBE/docker-compose.yml`. Before the `volumes:` section (line 55), add:

```yaml

  jaeger:
    image: jaegertracing/jaeger:2
    container_name: starter-jaeger
    ports:
      - "16686:16686"
      - "4317:4317"
      - "4318:4318"
    environment:
      - COLLECTOR_OTLP_ENABLED=true
    restart: unless-stopped
```

- [ ] **Step 5: Verify build**

Run:
```bash
cd boilerplateBE && dotnet build
```
Expected: Build succeeds with all configuration in place.

- [ ] **Step 6: Commit**

```bash
git add boilerplateBE/src/Starter.Api/appsettings.json boilerplateBE/src/Starter.Api/appsettings.Development.json boilerplateBE/src/Starter.Api/appsettings.Production.json boilerplateBE/docker-compose.yml
git commit -m "feat(otel): add configuration and Jaeger container"
```

---

### Task 7: Integration Verification via Test App

Use the post-feature testing workflow (see CLAUDE.md → "Post-Feature Testing Workflow") to verify end-to-end in an isolated test instance.

- [ ] **Step 1: Create test app via rename script**

Run from the worktree root:
```bash
powershell -File scripts/rename.ps1 -Name "_testOtel" -OutputDir "."
```
Expected: Creates `_testOtel/` directory with `_testOtel-BE/` and `_testOtel-FE/` inside (gitignored by `_test*` pattern).

- [ ] **Step 2: Drop old test DB if exists**

Run:
```bash
PGPASSWORD=123456 psql -U postgres -c "DROP DATABASE IF EXISTS _testoteldb;"
```

- [ ] **Step 3: Reconfigure test app ports**

In the test app backend (`_testOtel/_testOtel-BE/`):
- Change launch profile port to `5100` in `Properties/launchSettings.json`
- Add `http://localhost:3100` to CORS allowed origins in `appsettings.Development.json`
- Fix seed email: change `superadmin@_testotel.com` to `superadmin@testotel.com` in `appsettings.Development.json` (Zod rejects `_` prefix in email domains)

In the test app frontend (`_testOtel/_testOtel-FE/`):
- Update `.env` to set `VITE_API_BASE_URL=http://localhost:5100/api/v1`

- [ ] **Step 4: Start Jaeger container**

Run:
```bash
cd _testOtel/_testOtel-BE && docker compose up -d jaeger
```
Expected: Jaeger container starts. Verify UI at `http://localhost:16686`.

- [ ] **Step 5: Build and run the test backend**

Run:
```bash
cd _testOtel/_testOtel-BE && dotnet build
cd _testOtel/_testOtel-BE/src/_testOtel.Api && dotnet run --launch-profile http
```
Expected: App starts on port 5100. Migrations run, database created, seed data applied. Console shows no OTel connection errors. Serilog OpenTelemetry sink initializes.

- [ ] **Step 6: Build and run the test frontend**

Run:
```bash
cd _testOtel/_testOtel-FE && npm install && npm run dev
```
Expected: Frontend starts on port 3100.

- [ ] **Step 7: Verify traces via API calls**

Run:
```bash
curl http://localhost:5100/health
curl http://localhost:5100/api/v1/Auth/login -X POST -H "Content-Type: application/json" -d '{"email":"superadmin@testotel.com","password":"Admin@123456"}'
```

Open Jaeger at `http://localhost:16686`. Select service (will be named after the test app). Click "Find Traces".

Expected:
- The `/api/v1/Auth/login` request appears as a trace
- Expanding shows nested spans: ASP.NET → `MediatR LoginCommand` → EF Core DB query
- The `/health` endpoint does NOT appear (filtered out)

- [ ] **Step 8: Playwright regression test**

Using Playwright MCP, run:
1. Navigate to `http://localhost:3100`
2. Login with `superadmin@testotel.com` / `Admin@123456`
3. Verify navigation: Dashboard, Users, Roles, Files, Settings, API Keys pages all load
4. Verify basic CRUD still works (e.g., view user list, view role list)

This confirms OTel instrumentation doesn't break any existing functionality.

- [ ] **Step 9: Verify log correlation**

Check backend console output for log entries. They should contain `TraceId` values matching the traces in Jaeger.

- [ ] **Step 10: Verify disabled mode**

Temporarily set `"Enabled": false` in the test app's `appsettings.Development.json`. Restart the backend.

Expected: App starts normally without any OTel-related log output. No connection attempts to port 4317.

Restore `"Enabled": true` after verification.

- [ ] **Step 11: Leave running for manual QA**

Report URLs to user:
- Frontend: `http://localhost:3100`
- Backend: `http://localhost:5100/swagger`
- Jaeger: `http://localhost:16686`

Wait for user confirmation before pushing. After confirmation, clean up:
```bash
rm -rf _testOtel
PGPASSWORD=123456 psql -U postgres -c "DROP DATABASE IF EXISTS _testoteldb;"
```
