using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Starter.Application.Common.Interfaces;
using Starter.Infrastructure.Identity.Authentication;
using Starter.Infrastructure.Identity.Authorization;
using Starter.Infrastructure.Identity.Models;
using Starter.Infrastructure.Identity.Services;

namespace Starter.Infrastructure.Identity;

/// <summary>
/// Identity infrastructure dependency injection configuration.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // JWT Settings with validation
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();

        var jwtSettings = new JwtSettings();
        configuration.Bind(JwtSettings.SectionName, jwtSettings);

        // Services
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuditContextProvider, AuditContextProvider>();
        services.AddScoped<HttpExecutionContext>();
        services.AddScoped<IExecutionContext>(sp =>
            AmbientExecutionContext.Current
                ?? (IExecutionContext)sp.GetRequiredService<HttpExecutionContext>());
        services.AddSingleton<ITotpService, TotpService>();

        // Authentication — composite: JWT (default) + API Key
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "CompositeAuth";
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = "CompositeAuth";
        })
        .AddPolicyScheme("CompositeAuth", "JWT or API Key", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                if (context.Request.Headers.ContainsKey(ApiKeyAuthenticationHandler.HeaderName))
                    return ApiKeyAuthenticationHandler.SchemeName;
                return JwtBearerDefaults.AuthenticationScheme;
            };
        })
        .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationHandler.SchemeName, null)
        .AddJwtBearer(options =>
        {
            options.SaveToken = true;
            options.RequireHttpsMetadata = !string.Equals(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                "Development",
                StringComparison.OrdinalIgnoreCase);
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception is SecurityTokenExpiredException)
                    {
                        context.Response.Headers["Token-Expired"] = "true";
                    }
                    return Task.CompletedTask;
                }
            };
        });

        // Authorization - Permission-based
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddAuthorizationBuilder()
            .AddPolicy("RequireAdminRole", policy =>
                policy.RequireRole("SuperAdmin", "Admin"))
            .AddPolicy("RequireSuperAdminRole", policy =>
                policy.RequireRole("SuperAdmin"));

        return services;
    }
}
