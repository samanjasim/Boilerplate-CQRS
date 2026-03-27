using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Commands.RevokeApiKey;

public sealed class RevokeApiKeyCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<RevokeApiKeyCommand, Result>
{
    public async Task<Result> Handle(RevokeApiKeyCommand request, CancellationToken cancellationToken)
    {
        var apiKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken);

        if (apiKey is null)
            return Result.Failure(Error.NotFound("ApiKey.NotFound", "API key not found."));

        if (apiKey.IsRevoked)
            return Result.Failure(Error.Conflict("ApiKey.AlreadyRevoked", "API key is already revoked."));

        apiKey.Revoke();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
