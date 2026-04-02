using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Webhooks.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Webhooks.Commands.DeleteWebhookEndpoint;

public sealed class DeleteWebhookEndpointCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    IUsageTracker usageTracker)
    : IRequestHandler<DeleteWebhookEndpointCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(
        DeleteWebhookEndpointCommand request,
        CancellationToken cancellationToken)
    {
        var endpoint = await dbContext.WebhookEndpoints
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (endpoint is null)
            return Result.Failure<Unit>(WebhookErrors.EndpointNotFound);

        dbContext.WebhookEndpoints.Remove(endpoint);
        await dbContext.SaveChangesAsync(cancellationToken);

        var tenantId = currentUserService.TenantId;
        if (tenantId.HasValue)
            await usageTracker.DecrementAsync(tenantId.Value, "webhooks", ct: cancellationToken);

        return Result.Success(Unit.Value);
    }
}
