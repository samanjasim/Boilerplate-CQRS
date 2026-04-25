using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetTemplates;

internal sealed class GetTemplatesQueryHandler(IAiAgentTemplateRegistry registry)
    : IRequestHandler<GetTemplatesQuery, Result<IReadOnlyList<AiAgentTemplateDto>>>
{
    public Task<Result<IReadOnlyList<AiAgentTemplateDto>>> Handle(
        GetTemplatesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<AiAgentTemplateDto> dtos = registry
            .GetAll()
            .Select(t => t.ToDto())
            .ToList();
        return Task.FromResult(Result<IReadOnlyList<AiAgentTemplateDto>>.Success(dtos));
    }
}
