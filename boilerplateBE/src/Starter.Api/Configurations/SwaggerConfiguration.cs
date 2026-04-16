using System.Reflection;
using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Starter.Api.Configurations;

public static class SwaggerConfiguration
{
    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = """
                    Enter your JWT token to authorize API requests.

                    **How to get a token:**
                    1. Call `POST /api/v1/auth/login` with your credentials
                    2. Copy the `accessToken` from the response
                    3. Paste it below (no need to add "Bearer " prefix)

                    **Test accounts:**
                    - SuperAdmin: `superadmin@starter.com` / `Admin@123456`
                    - Admin: `admin@starter.com` / `Admin@123456`
                    """,
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                Scheme = "Bearer"
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
            });

            // Use full type names as schema IDs to avoid conflicts between
            // core and module types with the same short name
            options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

            // Include XML comments from controllers
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        services.ConfigureOptions<ConfigureSwaggerOptions>();

        return services;
    }

    public static IApplicationBuilder UseSwaggerConfiguration(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            var provider = app.ApplicationServices.GetRequiredService<IApiVersionDescriptionProvider>();

            foreach (var description in provider.ApiVersionDescriptions)
            {
                options.SwaggerEndpoint(
                    $"/swagger/{description.GroupName}/swagger.json",
                    description.GroupName.ToUpperInvariant());
            }

            options.DocumentTitle = "Starter API";
            options.DefaultModelsExpandDepth(1);
            options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
            options.EnableTryItOutByDefault();
        });

        return app;
    }
}

public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    {
        _provider = provider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, CreateInfoForApiVersion(description));
        }
    }

    private static OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description)
    {
        var info = new OpenApiInfo
        {
            Title = "Starter API",
            Version = description.ApiVersion.ToString(),
            Description = """
                Starter boilerplate API.

                ## Authentication
                Most endpoints require a Bearer JWT token. Use `POST /api/v1/auth/login` to obtain one,
                then click the **Authorize** button above to set it.

                ## Multi-Tenancy
                Tenant-scoped endpoints require the `X-Tenant-Id` header with the target tenant's GUID.
                """,
            Contact = new OpenApiContact
            {
                Name = "Starter Team",
                Email = "support@starter.com"
            }
        };

        if (description.IsDeprecated)
        {
            info.Description += "\n\n> **Warning:** This API version has been deprecated.";
        }

        return info;
    }
}
