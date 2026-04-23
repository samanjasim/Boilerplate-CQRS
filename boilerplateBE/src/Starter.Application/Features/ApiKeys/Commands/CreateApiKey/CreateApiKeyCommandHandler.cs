using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Domain.ApiKeys.Entities;
using Starter.Domain.ApiKeys.Errors;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Commands.CreateApiKey;

internal sealed class CreateApiKeyCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    IPasswordService passwordService,
    IFeatureFlagService flags,
    IUsageTracker usageTracker)
    : IRequestHandler<CreateApiKeyCommand, Result<CreateApiKeyResponse>>
{
    public async Task<Result<CreateApiKeyResponse>> Handle(
        CreateApiKeyCommand request,
        CancellationToken cancellationToken)
    {
        // Scope ceiling: a key can never grant more than the creating user holds.
        // SuperAdmin (TenantId=null) holds every permission implicitly, so this only
        // bites tenant-scoped callers. Comparison is case-insensitive to match the
        // Permissions claim shape.
        if (currentUserService.TenantId.HasValue && request.Scopes.Count > 0)
        {
            var heldScopes = new HashSet<string>(
                currentUserService.Permissions,
                StringComparer.OrdinalIgnoreCase);
            var missingScopes = request.Scopes
                .Where(s => !heldScopes.Contains(s))
                .ToList();
            if (missingScopes.Count > 0)
                return Result.Failure<CreateApiKeyResponse>(ApiKeyErrors.ScopeEscalation(missingScopes));
        }

        var apiKeyTenantId = currentUserService.TenantId;

        if (apiKeyTenantId.HasValue)
        {
            if (!await flags.IsEnabledAsync("api_keys.enabled", cancellationToken))
                return Result.Failure<CreateApiKeyResponse>(FeatureFlagErrors.FeatureDisabled("API Keys"));

            var maxKeys = await flags.GetValueAsync<int>("api_keys.max_count", cancellationToken);
            var currentCount = await usageTracker.GetAsync(apiKeyTenantId.Value, "api_keys", cancellationToken);
            if (currentCount >= maxKeys)
                return Result.Failure<CreateApiKeyResponse>(FeatureFlagErrors.QuotaExceeded("API keys", maxKeys));
        }

        // Determine tenant scope
        Guid? tenantId;
        if (currentUserService.TenantId.HasValue)
        {
            // Tenant user — always scoped to their tenant
            if (request.IsPlatformKey)
                return Result.Failure<CreateApiKeyResponse>(ApiKeyErrors.TenantCannotCreatePlatformKey);

            tenantId = currentUserService.TenantId.Value;
        }
        else
        {
            // Platform admin — must explicitly create platform key
            if (!request.IsPlatformKey)
                return Result.Failure<CreateApiKeyResponse>(ApiKeyErrors.PlatformAdminMustBeExplicit);

            tenantId = null;
        }

        // Generate key
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var randomPart = Convert.ToBase64String(randomBytes)
            .Replace("+", "").Replace("/", "").Replace("=", "");
        if (randomPart.Length > 32) randomPart = randomPart[..32];

        var fullKey = $"sk_live_{randomPart}";
        var keyPrefix = $"sk_live_{randomPart[..8]}";

        var prefixExists = await dbContext.Set<ApiKey>()
            .IgnoreQueryFilters()
            .AnyAsync(k => k.KeyPrefix == keyPrefix, cancellationToken);

        if (prefixExists)
            return Result.Failure<CreateApiKeyResponse>(
                Error.Conflict("ApiKey.PrefixCollision", "Key generation collision. Please try again."));

        var keyHash = await passwordService.HashPasswordAsync(fullKey);

        // Normalize ExpiresAt to UTC. JSON deserialization yields DateTime
        // with Kind=Unspecified unless the payload includes a Z suffix or
        // explicit offset, and Npgsql rejects non-UTC values when writing
        // to `timestamp with time zone` columns.
        var expiresAtUtc = request.ExpiresAt.HasValue
            ? DateTime.SpecifyKind(request.ExpiresAt.Value, DateTimeKind.Utc)
            : (DateTime?)null;

        var apiKey = ApiKey.Create(
            tenantId,
            request.Name,
            keyPrefix,
            keyHash,
            request.Scopes,
            expiresAtUtc,
            currentUserService.UserId);

        dbContext.Set<ApiKey>().Add(apiKey);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (apiKeyTenantId.HasValue)
            await usageTracker.IncrementAsync(apiKeyTenantId.Value, "api_keys", ct: cancellationToken);

        return Result.Success(new CreateApiKeyResponse(
            apiKey.Id, apiKey.Name, apiKey.KeyPrefix, fullKey,
            apiKey.Scopes, apiKey.ExpiresAt, apiKey.CreatedAt));
    }
}
