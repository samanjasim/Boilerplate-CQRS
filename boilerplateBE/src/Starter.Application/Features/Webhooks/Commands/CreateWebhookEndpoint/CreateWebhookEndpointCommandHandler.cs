using System.Text.Json;
using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Domain.Webhooks.Entities;
using Starter.Domain.Webhooks.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Webhooks.Commands.CreateWebhookEndpoint;

public sealed class CreateWebhookEndpointCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    IFeatureFlagService flags,
    IUsageTracker usageTracker)
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

        var endpoint = WebhookEndpoint.Create(
            request.Url,
            request.Description,
            eventsJson,
            tenantId.Value);

        if (!request.IsActive)
            endpoint.Deactivate();

        dbContext.WebhookEndpoints.Add(endpoint);
        await dbContext.SaveChangesAsync(cancellationToken);

        await usageTracker.IncrementAsync(tenantId.Value, "webhooks", ct: cancellationToken);

        return Result.Success(new CreateWebhookEndpointResponse(endpoint.Id, endpoint.Secret));
    }
}
