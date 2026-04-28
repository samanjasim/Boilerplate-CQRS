using Starter.Api.Configurations;
using Starter.Api.Middleware;
using Starter.Application;
using Starter.Infrastructure;
using Starter.Infrastructure.Identity;
using Starter.Infrastructure.Modularity;
using Starter.Infrastructure.Persistence.Seeds;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

// Serilog
var loggerConfig = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId();

if (builder.Configuration.GetValue<bool>("OpenTelemetry:Enabled"))
{
    var otlpEndpoint = builder.Configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint") ?? "http://127.0.0.1:4318";
    loggerConfig.WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = $"{otlpEndpoint}/v1/logs";
        options.Protocol = OtlpProtocol.HttpProtobuf;
    });
}

Log.Logger = loggerConfig.CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddHttpContextAccessor();

// Discover and resolve modules
var modules = Starter.Abstractions.Modularity.ModuleLoader.DiscoverModules();
var orderedModules = Starter.Abstractions.Modularity.ModuleLoader.ResolveOrder(modules);
var moduleAssemblies = orderedModules.Select(m => m.GetType().Assembly).Distinct().ToList();

// Register discovered modules list (used by DataSeeder for permission aggregation)
builder.Services.AddSingleton<IReadOnlyList<Starter.Abstractions.Modularity.IModule>>(orderedModules);

// Add layers
builder.Services.AddApplication(moduleAssemblies);
builder.Services.AddInfrastructure(
    builder.Configuration,
    moduleAssemblies,
    configureBus: bus =>
    {
        foreach (var contributor in orderedModules.OfType<IModuleBusContributor>())
        {
            contributor.ConfigureBus(bus);
        }
    });
builder.Services.AddIdentityInfrastructure(builder.Configuration);

// Module-specific services
foreach (var module in orderedModules)
    module.ConfigureServices(builder.Services, builder.Configuration);

// API Configuration
var mvcBuilder = builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
foreach (var asm in moduleAssemblies)
    mvcBuilder.AddApplicationPart(asm);

builder.Services.AddEndpointsApiExplorer();

// Custom configurations
builder.Services.AddApiVersioningConfiguration();
builder.Services.AddSwaggerConfiguration();
builder.Services.AddCorsConfiguration(builder.Configuration);
builder.Services.AddRateLimitingConfiguration(builder.Configuration);
builder.Services.AddOpenTelemetryObservability(builder.Configuration);

var app = builder.Build();

// Middleware pipeline
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

app.UseExceptionHandling();
app.UseRequestLogging();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("EnableSwagger"))
{
    app.UseSwaggerConfiguration();
}

if (!app.Configuration.GetValue<bool>("BehindReverseProxy"))
{
    app.UseHttpsRedirection();
}
app.UseCorsConfiguration();
app.UseRateLimiting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Database initialization — applies core + module migrations and runs seed
// data (core + module SeedDataAsync) in one orchestrated sequence inside
// DataSeeder. Gated by DatabaseSettings:SeedDataOnStartup so non-seeded
// environments (e.g. prod with pre-migrated DBs) skip the whole block.
var seedData = builder.Configuration.GetValue<bool>("DatabaseSettings:SeedDataOnStartup");
if (seedData)
{
    Log.Information("Seeding database...");
    await DataSeeder.SeedAsync(app.Services);
    Log.Information("Database seeding completed");
}

Log.Information("Starter API started");

app.Run();
