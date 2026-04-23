using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Starter.Application.Common.Interfaces;
using Starter.Shared.Models;

namespace Starter.Abstractions.Web;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireFeatureFlagAttribute(string flagKey) : Attribute, IAsyncAuthorizationFilter
{
    public string FlagKey { get; } = flagKey;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var flags = context.HttpContext.RequestServices.GetRequiredService<IFeatureFlagService>();
        var enabled = await flags.IsEnabledAsync(FlagKey, context.HttpContext.RequestAborted);
        if (!enabled)
        {
            context.Result = new ObjectResult(ApiResponse.Fail(
                $"Feature '{FlagKey}' is not enabled for your tenant or subscription plan."))
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }
    }
}
