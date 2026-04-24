using FluentValidation;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;

namespace Starter.Module.AI.Application.Commands;

/// <summary>Shape shared by Create/Update assistant commands for reuse of validation rules.</summary>
internal interface IAssistantInput
{
    string Name { get; }
    string? Description { get; }
    string SystemPrompt { get; }
    AiProviderType? Provider { get; }
    string? Model { get; }
    double Temperature { get; }
    int MaxTokens { get; }
    AssistantExecutionMode ExecutionMode { get; }
    int MaxAgentSteps { get; }
    IReadOnlyList<string>? EnabledToolNames { get; }
    IReadOnlyList<Guid>? KnowledgeBaseDocIds { get; }
    AiRagScope RagScope { get; }
    string? Slug { get; }
    IReadOnlyList<string>? PersonaTargetSlugs { get; }
}

internal static class AssistantInputRules
{
    public static void Apply<T>(AbstractValidator<T> v) where T : IAssistantInput
    {
        v.RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        v.RuleFor(x => x.Description).MaximumLength(500);
        v.RuleFor(x => x.SystemPrompt).NotEmpty().MaximumLength(20_000);
        v.RuleFor(x => x.Model).MaximumLength(120);
        v.RuleFor(x => x.Temperature).InclusiveBetween(0.0, 2.0);
        v.RuleFor(x => x.MaxTokens).InclusiveBetween(1, 64_000);
        v.RuleFor(x => x.MaxAgentSteps).InclusiveBetween(1, 50);
        v.RuleForEach(x => x.EnabledToolNames!)
            .NotEmpty().MaximumLength(120)
            .When(x => x.EnabledToolNames is not null);
        v.RuleFor(x => x)
            .Must(x => x.RagScope != AiRagScope.SelectedDocuments
                || (x.KnowledgeBaseDocIds is not null && x.KnowledgeBaseDocIds.Count > 0))
            .WithMessage(AiErrors.RagScopeRequiresDocuments.Description)
            .WithErrorCode(AiErrors.RagScopeRequiresDocuments.Code);
        v.RuleFor(x => x.Slug!)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$").MaximumLength(64)
            .When(x => !string.IsNullOrEmpty(x.Slug));
        v.RuleForEach(x => x.PersonaTargetSlugs!)
            .NotEmpty().Matches("^[a-z0-9]+(-[a-z0-9]+)*$").MaximumLength(64)
            .When(x => x.PersonaTargetSlugs is not null);
    }
}
