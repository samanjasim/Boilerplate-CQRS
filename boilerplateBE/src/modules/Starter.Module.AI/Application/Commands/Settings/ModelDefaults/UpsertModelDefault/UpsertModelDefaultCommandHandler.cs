using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.ModelDefaults.UpsertModelDefault;

internal sealed class UpsertModelDefaultCommandHandler(
    AiDbContext db,
    ICurrentUserService currentUser,
    IAiEntitlementResolver entitlements) : IRequestHandler<UpsertModelDefaultCommand, Result<AiModelDefaultDto>>
{
    public async Task<Result<AiModelDefaultDto>> Handle(UpsertModelDefaultCommand request, CancellationToken ct)
    {
        var tenantId = currentUser.TenantId ?? request.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result.Failure<AiModelDefaultDto>(
                Error.Validation("AiSettings.TenantIdRequired", "A tenant id is required to update AI model defaults."));

        var resolvedEntitlements = await entitlements.ResolveAsync(ct);
        if (!IsProviderAllowed(resolvedEntitlements.AllowedProviders, request.Provider))
            return Result.Failure<AiModelDefaultDto>(AiSettingsErrors.ProviderNotAllowed(request.Provider.ToString()));

        if (!IsModelAllowed(resolvedEntitlements.AllowedModels, request.Provider, request.Model))
            return Result.Failure<AiModelDefaultDto>(AiSettingsErrors.ModelNotAllowed(request.Model));

        var row = await db.AiModelDefaults
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.TenantId == tenantId.Value && d.AgentClass == request.AgentClass, ct);

        if (row is null)
        {
            row = AiModelDefault.Create(
                tenantId.Value,
                request.AgentClass,
                request.Provider,
                request.Model,
                request.MaxTokens,
                request.Temperature);
            db.AiModelDefaults.Add(row);
        }
        else
        {
            row.Update(request.Provider, request.Model, request.MaxTokens, request.Temperature);
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(ToDto(row));
    }

    private static AiModelDefaultDto ToDto(AiModelDefault row) =>
        new(row.Id, row.TenantId, row.AgentClass, row.Provider, row.Model, row.MaxTokens, row.Temperature);

    private static bool IsProviderAllowed(IReadOnlyList<string> allowedProviders, AiProviderType provider) =>
        allowedProviders.Count == 0 ||
        allowedProviders.Any(p => string.Equals(p, provider.ToString(), StringComparison.OrdinalIgnoreCase));

    private static bool IsModelAllowed(
        IReadOnlyList<string> allowedModels,
        AiProviderType provider,
        string model)
    {
        var trimmed = model.Trim();
        return allowedModels.Count == 0 ||
               allowedModels.Any(m =>
                   string.Equals(m, trimmed, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(m, $"{provider}:{trimmed}", StringComparison.OrdinalIgnoreCase));
    }
}
