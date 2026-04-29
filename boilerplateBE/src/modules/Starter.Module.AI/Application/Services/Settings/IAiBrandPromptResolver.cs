namespace Starter.Module.AI.Application.Services.Settings;

internal interface IAiBrandPromptResolver
{
    Task<string?> ResolveClauseAsync(Guid? tenantId, CancellationToken ct = default);
}
