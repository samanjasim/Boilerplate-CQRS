using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Webhooks.Domain.Errors;
using Starter.Module.Webhooks.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Commands.UpdateWebhookEndpoint;

public sealed class UpdateWebhookEndpointCommandHandler(
    WebhooksDbContext dbContext)
    : IRequestHandler<UpdateWebhookEndpointCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(
        UpdateWebhookEndpointCommand request,
        CancellationToken cancellationToken)
    {
        var endpoint = await dbContext.WebhookEndpoints
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (endpoint is null)
            return Result.Failure<Unit>(WebhookErrors.EndpointNotFound);

        var eventsJson = JsonSerializer.Serialize(request.Events);
        endpoint.Update(request.Url, request.Description, eventsJson, request.IsActive);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(Unit.Value);
    }
}
