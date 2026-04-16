using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Module.Communication.Infrastructure.Services;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.PreviewTemplate;

internal sealed class PreviewTemplateCommandHandler(
    CommunicationDbContext dbContext,
    ITemplateEngine templateEngine,
    ILogger<PreviewTemplateCommandHandler> logger)
    : IRequestHandler<PreviewTemplateCommand, Result<TemplatePreviewDto>>
{
    public async Task<Result<TemplatePreviewDto>> Handle(
        PreviewTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var template = await dbContext.MessageTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.MessageTemplateId, cancellationToken);

        if (template is null)
            return Result.Failure<TemplatePreviewDto>(CommunicationErrors.TemplateNotFound);

        // Check for tenant override
        var tenantOverride = await dbContext.MessageTemplateOverrides
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.MessageTemplateId == request.MessageTemplateId && o.IsActive,
                cancellationToken);

        var subjectTemplate = tenantOverride?.SubjectTemplate ?? template.SubjectTemplate;
        var bodyTemplate = tenantOverride?.BodyTemplate ?? template.BodyTemplate;

        // Use provided variables, fall back to sample variables from the template
        var variables = request.Variables ?? (
            string.IsNullOrWhiteSpace(template.SampleVariablesJson)
                ? new Dictionary<string, object>()
                : JsonSerializer.Deserialize<Dictionary<string, object>>(template.SampleVariablesJson) ?? []);

        try
        {
            var renderedSubject = subjectTemplate is not null
                ? templateEngine.Render(subjectTemplate, variables)
                : string.Empty;
            var renderedBody = templateEngine.Render(bodyTemplate, variables);

            return Result.Success(new TemplatePreviewDto(renderedSubject, renderedBody));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Template preview render failed for template {TemplateId}", template.Id);
            return Result.Failure<TemplatePreviewDto>(CommunicationErrors.TemplateRenderFailed);
        }
    }
}
