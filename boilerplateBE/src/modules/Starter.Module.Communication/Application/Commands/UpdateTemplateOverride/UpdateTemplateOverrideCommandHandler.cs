using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Module.Communication.Infrastructure.Services;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.UpdateTemplateOverride;

internal sealed class UpdateTemplateOverrideCommandHandler(
    CommunicationDbContext dbContext,
    ITemplateEngine templateEngine)
    : IRequestHandler<UpdateTemplateOverrideCommand, Result<MessageTemplateOverrideDto>>
{
    public async Task<Result<MessageTemplateOverrideDto>> Handle(
        UpdateTemplateOverrideCommand request,
        CancellationToken cancellationToken)
    {
        var templateOverride = await dbContext.MessageTemplateOverrides
            .FirstOrDefaultAsync(o => o.MessageTemplateId == request.MessageTemplateId,
                cancellationToken);

        if (templateOverride is null)
            return Result.Failure<MessageTemplateOverrideDto>(CommunicationErrors.TemplateOverrideNotFound);

        // Validate template syntax
        if (!templateEngine.Validate(request.BodyTemplate, out _))
            return Result.Failure<MessageTemplateOverrideDto>(CommunicationErrors.TemplateRenderFailed);

        if (request.SubjectTemplate is not null &&
            !templateEngine.Validate(request.SubjectTemplate, out _))
            return Result.Failure<MessageTemplateOverrideDto>(CommunicationErrors.TemplateRenderFailed);

        templateOverride.Update(request.SubjectTemplate, request.BodyTemplate);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(templateOverride.ToDto());
    }
}
