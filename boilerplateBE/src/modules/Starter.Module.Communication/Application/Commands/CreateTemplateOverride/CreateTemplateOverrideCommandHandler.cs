using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Entities;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Module.Communication.Infrastructure.Services;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.CreateTemplateOverride;

internal sealed class CreateTemplateOverrideCommandHandler(
    CommunicationDbContext dbContext,
    ICurrentUserService currentUserService,
    ITemplateEngine templateEngine)
    : IRequestHandler<CreateTemplateOverrideCommand, Result<MessageTemplateOverrideDto>>
{
    public async Task<Result<MessageTemplateOverrideDto>> Handle(
        CreateTemplateOverrideCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUserService.TenantId;
        if (!tenantId.HasValue)
            return Result.Failure<MessageTemplateOverrideDto>(CommunicationErrors.TenantRequired);

        // Verify the system template exists
        var template = await dbContext.MessageTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.MessageTemplateId, cancellationToken);

        if (template is null)
            return Result.Failure<MessageTemplateOverrideDto>(CommunicationErrors.TemplateNotFound);

        // Check for duplicate override
        var exists = await dbContext.MessageTemplateOverrides
            .AnyAsync(o => o.MessageTemplateId == request.MessageTemplateId,
                cancellationToken);
        if (exists)
            return Result.Failure<MessageTemplateOverrideDto>(CommunicationErrors.DuplicateTemplateOverride);

        // Validate template syntax
        if (!templateEngine.Validate(request.BodyTemplate, out var bodyErrors))
            return Result.Failure<MessageTemplateOverrideDto>(CommunicationErrors.TemplateRenderFailed);

        if (request.SubjectTemplate is not null &&
            !templateEngine.Validate(request.SubjectTemplate, out var subjectErrors))
            return Result.Failure<MessageTemplateOverrideDto>(CommunicationErrors.TemplateRenderFailed);

        var templateOverride = MessageTemplateOverride.Create(
            tenantId.Value,
            request.MessageTemplateId,
            request.SubjectTemplate,
            request.BodyTemplate);

        dbContext.MessageTemplateOverrides.Add(templateOverride);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(templateOverride.ToDto());
    }
}
