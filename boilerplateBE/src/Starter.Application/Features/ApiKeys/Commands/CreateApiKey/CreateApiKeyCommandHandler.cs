using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Domain.ApiKeys.Entities;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Commands.CreateApiKey;

public sealed class CreateApiKeyCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    IPasswordService passwordService)
    : IRequestHandler<CreateApiKeyCommand, Result<CreateApiKeyResponse>>
{
    public async Task<Result<CreateApiKeyResponse>> Handle(
        CreateApiKeyCommand request,
        CancellationToken cancellationToken)
    {
        // Generate a cryptographically secure random key
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var randomPart = Convert.ToBase64String(randomBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");
        if (randomPart.Length > 32) randomPart = randomPart[..32];

        var fullKey = $"sk_live_{randomPart}";
        var keyPrefix = $"sk_live_{randomPart[..8]}";

        // Ensure prefix uniqueness
        var prefixExists = await dbContext.ApiKeys
            .IgnoreQueryFilters()
            .AnyAsync(k => k.KeyPrefix == keyPrefix, cancellationToken);

        if (prefixExists)
        {
            // Extremely unlikely collision — retry with new random bytes
            return Result.Failure<CreateApiKeyResponse>(
                Error.Conflict("ApiKey.PrefixCollision", "Key generation collision. Please try again."));
        }

        var keyHash = await passwordService.HashPasswordAsync(fullKey);

        var apiKey = ApiKey.Create(
            currentUserService.TenantId,
            request.Name,
            keyPrefix,
            keyHash,
            request.Scopes,
            request.ExpiresAt,
            currentUserService.UserId);

        dbContext.ApiKeys.Add(apiKey);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateApiKeyResponse(
            apiKey.Id,
            apiKey.Name,
            apiKey.KeyPrefix,
            fullKey,
            apiKey.Scopes,
            apiKey.ExpiresAt,
            apiKey.CreatedAt));
    }
}
