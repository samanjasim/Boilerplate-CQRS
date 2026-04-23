using Starter.Application.Common.Interfaces;

namespace EvalCacheWarmup;

/// <summary>
/// Stand-in <see cref="ICurrentUserService"/> for the console-hosted warmup tool.
/// The production implementation depends on <c>IHttpContextAccessor</c>, which is
/// not available outside an ASP.NET request pipeline. The warmup run writes
/// everything under a synthetic tenant so no real user context is needed.
/// </summary>
public sealed class ConsoleCurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; } = Guid.NewGuid();
    public string? Email => "warmup@eval.local";
    public bool IsAuthenticated => true;
    public IEnumerable<string> Roles => ["Admin"];
    public IEnumerable<string> Permissions => [];
    public Guid? TenantId { get; } = Guid.NewGuid();
    public bool IsInRole(string role) => role == "Admin";
    public bool HasPermission(string permission) => true;
}
