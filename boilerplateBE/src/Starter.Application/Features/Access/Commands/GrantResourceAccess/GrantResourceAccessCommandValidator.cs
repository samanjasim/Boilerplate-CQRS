using FluentValidation;

namespace Starter.Application.Features.Access.Commands.GrantResourceAccess;

public sealed class GrantResourceAccessCommandValidator : AbstractValidator<GrantResourceAccessCommand>
{
    public GrantResourceAccessCommandValidator()
    {
        RuleFor(x => x.ResourceType).NotEmpty();
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.SubjectType).IsInEnum();
        RuleFor(x => x.Level).IsInEnum();
    }
}
