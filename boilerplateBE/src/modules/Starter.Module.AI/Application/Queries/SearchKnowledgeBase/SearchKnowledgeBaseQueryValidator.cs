using FluentValidation;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Application.Queries.SearchKnowledgeBase;

public sealed class SearchKnowledgeBaseQueryValidator : AbstractValidator<SearchKnowledgeBaseQuery>
{
    public SearchKnowledgeBaseQueryValidator(IOptions<AiRagSettings> settings)
    {
        var s = settings.Value;

        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage(AiErrors.SearchQueryRequired.Description)
            .WithErrorCode(AiErrors.SearchQueryRequired.Code);

        RuleFor(x => x.TopK)
            .Must(k => k is null || (k >= 1 && k <= s.RetrievalTopK))
            .WithMessage(AiErrors.SearchTopKOutOfRange(s.RetrievalTopK).Description)
            .WithErrorCode(AiErrors.SearchTopKOutOfRange(s.RetrievalTopK).Code);
    }
}
