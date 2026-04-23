using Starter.Shared.Results;

namespace Starter.Module.CommentsActivity.Domain.Errors;

public static class CommentErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Comments.NotFound",
            $"The comment with ID '{id}' was not found.");

    public static Error NotCommentable(string entityType) =>
        Error.Validation("Comments.NotCommentable",
            $"The entity type '{entityType}' does not support comments.");

    public static readonly Error CannotReplyToReply = Error.Validation(
        "Comments.CannotReplyToReply",
        "Cannot reply to a reply. Only top-level comments can have replies.");

    public static readonly Error NotAuthor = Error.Forbidden(
        "You can only edit or delete your own comments.");

    public static readonly Error AlreadyDeleted = Error.Validation(
        "Comments.AlreadyDeleted",
        "This comment has already been deleted.");

    public static Error ActivityNotEnabled(string entityType) =>
        Error.Validation("Comments.ActivityNotEnabled",
            $"Activity tracking is not enabled for entity type '{entityType}'.");
}
