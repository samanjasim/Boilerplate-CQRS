using Starter.Api.Configurations;
using Starter.Api.Middleware;
using Starter.Application;
using Starter.Infrastructure;
using Starter.Infrastructure.Identity;
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

// Add layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIdentityInfrastructure(builder.Configuration);

// API Configuration
builder.Services.AddControllers();
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

// Database initialization
var seedData = builder.Configuration.GetValue<bool>("DatabaseSettings:SeedDataOnStartup");
if (seedData)
{
    Log.Information("Seeding database...");
    await DataSeeder.SeedAsync(app.Services);
    Log.Information("Database seeding completed");
}

Log.Information("Starter API started");

app.Run();
