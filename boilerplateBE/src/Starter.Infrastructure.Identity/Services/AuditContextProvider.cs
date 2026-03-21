using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Identity.Services;

public sealed class AuditContextProvider : IAuditContextProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditContextProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent =>
        _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].FirstOrDefault();

    public string? CorrelationId =>
        _httpContextAccessor.HttpContext?.TraceIdentifier;

    public string? UserDisplayName
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var firstName = user?.FindFirstValue("given_name");
            var lastName = user?.FindFirstValue("family_name");

            if (!string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName))
                return $"{firstName} {lastName}";

            return user?.FindFirstValue(ClaimTypes.Name);
        }
    }
}
