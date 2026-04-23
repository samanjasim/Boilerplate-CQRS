namespace Starter.Application.Common.Access.Contracts;

public sealed record AccessResolution(
    bool IsAdminBypass,
    IReadOnlyList<Guid> ExplicitGrantedResourceIds);
