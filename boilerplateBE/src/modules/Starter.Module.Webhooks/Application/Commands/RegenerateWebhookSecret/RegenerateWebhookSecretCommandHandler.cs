using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Webhooks.Domain.Errors;
using Starter.Module.Webhooks.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Commands.RegenerateWebhookSecret;

public sealed class RegenerateWebhookSecretCommandHandler(
    WebhooksDbContext dbContext)
    : IRequestHandler<RegenerateWebhookSecretCommand, Result<string>>
{
    public async Task<Result<string>> Handle(
        RegenerateWebhookSecretCommand request,
        CancellationToken cancellationToken)
    {
        var endpoint = await dbContext.WebhookEndpoints
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (endpoint is null)
            return Result.Failure<string>(WebhookErrors.EndpointNotFound);

        endpoint.RegenerateSecret();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(endpoint.Secret);
    }
}
