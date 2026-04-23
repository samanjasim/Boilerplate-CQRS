using Starter.Domain.Common.Access.Enums;

namespace Starter.Module.AI.Application.Services.Retrieval;

public sealed record AclPayloadFilter(
    Guid UserId,
    ResourceVisibility MinVisibilityTenantWide,
    IReadOnlyCollection<Guid> GrantedFileIds);
