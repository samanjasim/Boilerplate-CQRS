using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetMessageTemplates;

internal sealed class GetMessageTemplatesQueryHandler(
    CommunicationDbContext context)
    : IRequestHandler<GetMessageTemplatesQuery, Result<List<MessageTemplateDto>>>
{
    public async Task<Result<List<MessageTemplateDto>>> Handle(
        GetMessageTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.MessageTemplates.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(t => t.Category == request.Category);

        var templates = await query
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        // Get override indicators for current tenant
        var templateIds = templates.Select(t => t.Id).ToList();
        var overrideTemplateIds = await context.MessageTemplateOverrides
            .AsNoTracking()
            .Where(o => templateIds.Contains(o.MessageTemplateId))
            .Select(o => o.MessageTemplateId)
            .ToHashSetAsync(cancellationToken);

        var dtos = templates.Select(t => t.ToDto(hasOverride: overrideTemplateIds.Contains(t.Id))).ToList();

        return Result.Success(dtos);
    }
}
