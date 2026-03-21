namespace Starter.Api.Configurations;

/// <summary>
/// CORS configuration for Starter.
/// </summary>
public static class CorsConfiguration
{
    public const string PolicyName = "StarterPolicy";

    public static IServiceCollection AddCorsConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? ["http://localhost:3000"];

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .WithHeaders("Content-Type", "Authorization", "X-Tenant-Id", "X-Correlation-Id", "X-Api-Version")
                    .AllowCredentials()
                    .WithExposedHeaders("X-Correlation-Id", "Token-Expired");
            });
        });

        return services;
    }

    public static IApplicationBuilder UseCorsConfiguration(this IApplicationBuilder app)
    {
        app.UseCors(PolicyName);
        return app;
    }
}
