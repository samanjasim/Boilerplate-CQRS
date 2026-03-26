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
                    // O(1) lookup for explicit origins
                    var originsSet = new HashSet<string>(allowedOrigins, StringComparer.OrdinalIgnoreCase);
                    var domainSuffix = $".{baseDomain}";

                    policy.SetIsOriginAllowed(origin =>
                    {
                        if (originsSet.Contains(origin))
                            return true;

                        if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                        {
                            var host = uri.Host;
                            return host.Equals(baseDomain, StringComparison.OrdinalIgnoreCase)
                                || host.EndsWith(domainSuffix, StringComparison.OrdinalIgnoreCase);
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
