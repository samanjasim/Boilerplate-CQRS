using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetAssistantById;

internal sealed class GetAssistantByIdQueryHandler(AiDbContext context)
    : IRequestHandler<GetAssistantByIdQuery, Result<AiAssistantDto>>
{
    public async Task<Result<AiAssistantDto>> Handle(
        GetAssistantByIdQuery request,
        CancellationToken cancellationToken)
    {
        var assistant = await context.AiAssistants.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (assistant is null)
            return Result.Failure<AiAssistantDto>(AiErrors.AssistantNotFound);

        return Result.Success(assistant.ToDto());
    }
}
