using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetMessageTemplateById;

internal sealed class GetMessageTemplateByIdQueryHandler(
    CommunicationDbContext context)
    : IRequestHandler<GetMessageTemplateByIdQuery, Result<MessageTemplateDetailDto>>
{
    public async Task<Result<MessageTemplateDetailDto>> Handle(
        GetMessageTemplateByIdQuery request,
        CancellationToken cancellationToken)
    {
        var template = await context.MessageTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (template is null)
            return Result.Failure<MessageTemplateDetailDto>(CommunicationErrors.TemplateNotFound);

        var tenantOverride = await context.MessageTemplateOverrides
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.MessageTemplateId == request.Id, cancellationToken);

        return Result.Success(template.ToDetailDto(tenantOverride));
    }
}
