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

        var baseDomain = configuration.GetValue<string>("AppSettings:BaseDomain");

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                if (!string.IsNullOrEmpty(baseDomain))
                {
                    policy.SetIsOriginAllowed(origin =>
                    {
                        // Check explicit allowlist first (case-insensitive)
                        if (allowedOrigins.Any(o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase)))
                            return true;

                        // Check if origin host matches baseDomain or is a subdomain of it
                        if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                        {
                            var host = uri.Host;
                            if (string.Equals(host, baseDomain, StringComparison.OrdinalIgnoreCase))
                                return true;
                            if (host.EndsWith($".{baseDomain}", StringComparison.OrdinalIgnoreCase))
                                return true;
                        }

                        return false;
                    });
                }
                else
                {
                    policy.WithOrigins(allowedOrigins);
                }

                policy.AllowAnyMethod()
                    .WithHeaders("Content-Type", "Authorization", "X-Tenant-Id", "X-Correlation-Id", "X-Api-Version", "X-Refresh-Token")
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
