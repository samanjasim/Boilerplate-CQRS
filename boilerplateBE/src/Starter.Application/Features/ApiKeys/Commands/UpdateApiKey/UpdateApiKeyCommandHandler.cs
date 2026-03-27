using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Commands.UpdateApiKey;

public sealed class UpdateApiKeyCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpdateApiKeyCommand, Result<ApiKeyDto>>
{
    public async Task<Result<ApiKeyDto>> Handle(UpdateApiKeyCommand request, CancellationToken cancellationToken)
    {
        var apiKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken);

        if (apiKey is null)
            return Result.Failure<ApiKeyDto>(Error.NotFound("ApiKey.NotFound", "API key not found."));

        if (apiKey.IsRevoked)
            return Result.Failure<ApiKeyDto>(Error.Conflict("ApiKey.Revoked", "Cannot update a revoked API key."));

        apiKey.UpdateDetails(request.Name, request.Scopes);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(apiKey.ToDto());
    }
}
