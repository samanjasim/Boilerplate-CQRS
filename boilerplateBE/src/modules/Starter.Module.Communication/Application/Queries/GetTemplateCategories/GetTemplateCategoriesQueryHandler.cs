using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetTemplateCategories;

internal sealed class GetTemplateCategoriesQueryHandler(
    CommunicationDbContext context)
    : IRequestHandler<GetTemplateCategoriesQuery, Result<List<string>>>
{
    public async Task<Result<List<string>>> Handle(
        GetTemplateCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var categories = await context.MessageTemplates
            .AsNoTracking()
            .Select(t => t.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

        return Result.Success(categories);
    }
}
