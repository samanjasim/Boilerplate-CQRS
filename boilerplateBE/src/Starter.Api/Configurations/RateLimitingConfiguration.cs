using AspNetCoreRateLimit;

namespace Starter.Api.Configurations;

/// <summary>
/// Rate limiting configuration.
/// </summary>
public static class RateLimitingConfiguration
{
    public static IServiceCollection AddRateLimitingConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add memory cache (required for rate limiting)
        services.AddMemoryCache();

        // Load configuration
        services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
        services.Configure<IpRateLimitPolicies>(configuration.GetSection("IpRateLimitPolicies"));

        // Inject counter and rules stores
        services.AddInMemoryRateLimiting();

        // Configure the resolvers
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

        return services;
    }

    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        app.UseIpRateLimiting();
        return app;
    }
}
