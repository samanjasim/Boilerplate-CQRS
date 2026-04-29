using Starter.Module.AI.Application.Services.Settings;

namespace Starter.Module.AI.Infrastructure.Services.Settings;

internal sealed class AiBrandPromptResolver(IAiTenantSettingsResolver tenantSettings) : IAiBrandPromptResolver
{
    public async Task<string?> ResolveClauseAsync(Guid? tenantId, CancellationToken ct = default)
    {
        if (tenantId is not { } tid)
            return null;

        var settings = await tenantSettings.GetOrDefaultAsync(tid, ct);
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.AssistantDisplayName))
            lines.Add($"- Name: {settings.AssistantDisplayName}");
        if (!string.IsNullOrWhiteSpace(settings.Tone))
            lines.Add($"- Tone: {settings.Tone}");
        if (!string.IsNullOrWhiteSpace(settings.BrandInstructions))
            lines.Add($"- Brand guidance: {settings.BrandInstructions}");

        return lines.Count == 0
            ? null
            : "Tenant AI brand profile:\n" + string.Join('\n', lines);
    }
}
