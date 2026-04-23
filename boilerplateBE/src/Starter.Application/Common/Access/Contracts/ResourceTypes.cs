using Starter.Domain.Common.Access.Enums;

namespace Starter.Application.Common.Access.Contracts;

public static class ResourceTypes
{
    public const string File = "File";
    public const string AiAssistant = "AiAssistant";

    public static ResourceVisibility MaxVisibility(string resourceType) => resourceType switch
    {
        File => ResourceVisibility.Public,
        AiAssistant => ResourceVisibility.TenantWide,
        _ => ResourceVisibility.TenantWide,
    };

    public static bool IsKnown(string resourceType) =>
        resourceType is File or AiAssistant;
}
