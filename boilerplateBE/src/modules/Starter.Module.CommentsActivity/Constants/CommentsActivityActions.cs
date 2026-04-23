namespace Starter.Module.CommentsActivity.Constants;

/// <summary>
/// Built-in action strings written to <c>ActivityEntry.Action</c>. Module
/// integrators who introduce their own actions declare them via
/// <c>CommentableEntityDefinition.CustomActivityTypes</c>. FE filter chips
/// key off these literals — keep in sync with the FE timeline map until the
/// roadmap's source-generator lands.
/// </summary>
public static class CommentsActivityActions
{
    public const string CommentAdded = "comment_added";
    public const string CommentEdited = "comment_edited";
    public const string CommentDeleted = "comment_deleted";
    public const string ReactionAdded = "reaction_added";
    public const string ReactionRemoved = "reaction_removed";
}
