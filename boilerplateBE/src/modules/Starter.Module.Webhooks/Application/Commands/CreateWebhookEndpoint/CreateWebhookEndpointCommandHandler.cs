using System.Text.Json;
using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Module.Webhooks.Domain.Entities;
using Starter.Module.Webhooks.Domain.Errors;
using Starter.Module.Webhooks.Infrastructure.Persistence;
using Starter.Module.Webhooks.Infrastructure.Services;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Commands.CreateWebhookEndpoint;

public sealed class CreateWebhookEndpointCommandHandler(
    WebhooksDbContext dbContext,
    ICurrentUserService currentUserService,
    IFeatureFlagService flags,
    IUsageTracker usageTracker,
    IWebhookSecretProtector secretProtector)
    : IRequestHandler<CreateWebhookEndpointCommand, Result<CreateWebhookEndpointResponse>>
{
    public async Task<Result<CreateWebhookEndpointResponse>> Handle(
        CreateWebhookEndpointCommand request,
        CancellationToken cancellationToken)
    {
        if (!await flags.IsEnabledAsync("webhooks.enabled", cancellationToken))
            return Result.Failure<CreateWebhookEndpointResponse>(FeatureFlagErrors.FeatureDisabled("Webhooks"));

        var tenantId = currentUserService.TenantId;
        if (!tenantId.HasValue)
            return Result.Failure<CreateWebhookEndpointResponse>(WebhookErrors.EndpointNotFound);

        var maxCount = await flags.GetValueAsync<int>("webhooks.max_count", cancellationToken);
        var currentCount = await usageTracker.GetAsync(tenantId.Value, "webhooks", cancellationToken);

        if (currentCount >= maxCount)
            return Result.Failure<CreateWebhookEndpointResponse>(WebhookErrors.QuotaExceeded(maxCount));

        var eventsJson = JsonSerializer.Serialize(request.Events);

        var plaintextSecret = WebhookEndpoint.GenerateSecret();
        var protectedSecret = secretProtector.Protect(plaintextSecret);

        var endpoint = WebhookEndpoint.Create(
            request.Url,
            request.Description,
            protectedSecret,
            eventsJson,
            tenantId.Value);

        if (!request.IsActive)
            endpoint.Deactivate();

        dbContext.WebhookEndpoints.Add(endpoint);
        await dbContext.SaveChangesAsync(cancellationToken);

        await usageTracker.IncrementAsync(tenantId.Value, "webhooks", ct: cancellationToken);

        // Return the plaintext once to the caller; it is never shown again.
        return Result.Success(new CreateWebhookEndpointResponse(endpoint.Id, plaintextSecret));
    }
}
