using FluentValidation;

namespace Starter.Module.CommentsActivity.Application.Commands.ToggleReaction;

public sealed class ToggleReactionCommandValidator : AbstractValidator<ToggleReactionCommand>
{
    public ToggleReactionCommandValidator()
    {
        RuleFor(x => x.CommentId).NotEmpty();
        RuleFor(x => x.ReactionType).NotEmpty().MaximumLength(50);
    }
}
