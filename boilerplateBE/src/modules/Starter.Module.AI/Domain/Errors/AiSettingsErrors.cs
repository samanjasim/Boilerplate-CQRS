using Starter.Shared.Results;

namespace Starter.Module.AI.Domain.Errors;

public static class AiSettingsErrors
{
    public static Error ByokDisabledByPlan =>
        new("AiSettings.ByokDisabledByPlan", "Tenant-owned AI provider keys are not enabled by this tenant's plan.", ErrorType.Forbidden);

    public static Error ProviderNotAllowed(string provider) =>
        Error.Validation("AiSettings.ProviderNotAllowed", $"AI provider '{provider}' is not allowed by this tenant's plan.");

    public static Error ModelNotAllowed(string model) =>
        Error.Validation("AiSettings.ModelNotAllowed", $"AI model '{model}' is not allowed by this tenant's plan.");

    public static Error TenantKeyRequired(string provider) =>
        Error.Validation("AiSettings.TenantKeyRequired", $"A tenant-owned key is required for provider '{provider}'.");

    public static Error SelfLimitExceedsEntitlement(string field) =>
        Error.Validation("AiSettings.SelfLimitExceedsEntitlement", $"AI self-limit '{field}' exceeds the tenant's plan entitlement.");

    public static Error WidgetDisabledByPlan =>
        new("AiSettings.WidgetDisabledByPlan", "Public AI widgets are not enabled by this tenant's plan.", ErrorType.Forbidden);

    public static Error WidgetQuotaExceedsEntitlement(string field) =>
        Error.Validation("AiSettings.WidgetQuotaExceedsEntitlement", $"Widget quota '{field}' exceeds the tenant's plan entitlement.");

    public static Error WidgetLimitExceeded(int limit) =>
        new("AiSettings.WidgetLimitExceeded", $"This tenant can create at most {limit} public AI widgets.", ErrorType.Forbidden);

    public static Error InvalidOrigin(string origin) =>
        Error.Validation("AiSettings.InvalidOrigin", $"Origin '{origin}' is not a valid http or https origin.");

    public static Error ProviderCredentialNotFound =>
        Error.NotFound("AiSettings.ProviderCredentialNotFound", "AI provider credential not found.");

    public static Error WidgetNotFound =>
        Error.NotFound("AiSettings.WidgetNotFound", "AI public widget not found.");
}
