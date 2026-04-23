namespace Starter.Infrastructure.Services.Access;

internal static class AclCacheKeys
{
    public static string UserVersion(Guid tenantId, Guid userId) =>
        $"aclv:u:{tenantId:N}:{userId:N}";

    public static string AccessibleIds(Guid tenantId, Guid userId, long version, string resourceType) =>
        $"acl:{tenantId:N}:{userId:N}:v{version}:{resourceType}";
}
