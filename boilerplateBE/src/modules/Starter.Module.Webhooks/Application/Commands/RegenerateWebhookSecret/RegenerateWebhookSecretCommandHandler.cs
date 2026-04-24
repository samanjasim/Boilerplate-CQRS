using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Webhooks.Domain.Entities;
using Starter.Module.Webhooks.Domain.Errors;
using Starter.Module.Webhooks.Infrastructure.Persistence;
using Starter.Module.Webhooks.Infrastructure.Services;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Commands.RegenerateWebhookSecret;

public sealed class RegenerateWebhookSecretCommandHandler(
    WebhooksDbContext dbContext,
    IWebhookSecretProtector secretProtector)
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

        var plaintext = WebhookEndpoint.GenerateSecret();
        endpoint.ReplaceSecret(secretProtector.Protect(plaintext));
        await dbContext.SaveChangesAsync(cancellationToken);

        // Return the plaintext once to the caller; it is never shown again.
        return Result.Success(plaintext);
    }
}
