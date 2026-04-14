using FluentValidation;

namespace Starter.Module.CommentsActivity.Application.Queries.GetMentionableUsers;

public sealed class GetMentionableUsersQueryValidator : AbstractValidator<GetMentionableUsersQuery>
{
    public GetMentionableUsersQueryValidator()
    {
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}
