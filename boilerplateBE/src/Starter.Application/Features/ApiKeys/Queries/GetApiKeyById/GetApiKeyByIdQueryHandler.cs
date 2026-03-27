using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Queries.GetApiKeyById;

public sealed class GetApiKeyByIdQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetApiKeyByIdQuery, Result<ApiKeyDto>>
{
    public async Task<Result<ApiKeyDto>> Handle(
        GetApiKeyByIdQuery request,
        CancellationToken cancellationToken)
    {
        var apiKey = await dbContext.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken);

        if (apiKey is null)
            return Result.Failure<ApiKeyDto>(Error.NotFound("ApiKey.NotFound", "API key not found."));

        return Result.Success(apiKey.ToDto());
    }
}
