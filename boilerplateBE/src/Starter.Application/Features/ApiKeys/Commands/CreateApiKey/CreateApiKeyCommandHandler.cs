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

public sealed class CreateApiKeyCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    IPasswordService passwordService,
    IFeatureFlagService flags)
    : IRequestHandler<CreateApiKeyCommand, Result<CreateApiKeyResponse>>
{
    public async Task<Result<CreateApiKeyResponse>> Handle(
        CreateApiKeyCommand request,
        CancellationToken cancellationToken)
    {
        if (!await flags.IsEnabledAsync("api_keys.enabled", cancellationToken))
            return Result.Failure<CreateApiKeyResponse>(FeatureFlagErrors.FeatureDisabled("API Keys"));

        var maxKeys = await flags.GetValueAsync<int>("api_keys.max_count", cancellationToken);
        var currentCount = await dbContext.ApiKeys.CountAsync(k => !k.IsRevoked, cancellationToken);
        if (currentCount >= maxKeys)
            return Result.Failure<CreateApiKeyResponse>(FeatureFlagErrors.QuotaExceeded("API keys", maxKeys));

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

        var prefixExists = await dbContext.ApiKeys
            .IgnoreQueryFilters()
            .AnyAsync(k => k.KeyPrefix == keyPrefix, cancellationToken);

        if (prefixExists)
            return Result.Failure<CreateApiKeyResponse>(
                Error.Conflict("ApiKey.PrefixCollision", "Key generation collision. Please try again."));

        var keyHash = await passwordService.HashPasswordAsync(fullKey);

        var apiKey = ApiKey.Create(
            tenantId,
            request.Name,
            keyPrefix,
            keyHash,
            request.Scopes,
            request.ExpiresAt,
            currentUserService.UserId);

        dbContext.ApiKeys.Add(apiKey);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateApiKeyResponse(
            apiKey.Id, apiKey.Name, apiKey.KeyPrefix, fullKey,
            apiKey.Scopes, apiKey.ExpiresAt, apiKey.CreatedAt));
    }
}
