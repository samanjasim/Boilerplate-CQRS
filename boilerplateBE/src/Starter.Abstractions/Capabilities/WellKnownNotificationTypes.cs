namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Well-known notification type strings shared between core and modules.
/// Modules use these constants when checking <see cref="INotificationPreferenceReader"/>
/// to avoid string-drift bugs. Core also uses them in
/// <c>Starter.Application.Common.Constants.NotificationType</c> — but modules
/// cannot reference that class, so these constants live here in Abstractions.
/// </summary>
public static class WellKnownNotificationTypes
{
    /// <summary>User was @mentioned in a comment.</summary>
    public const string CommentMentioned = "CommentMentioned";

    /// <summary>A new comment was posted on an entity the user watches.</summary>
    public const string CommentOnWatchedEntity = "CommentOnWatchedEntity";

    /// <summary>A workflow approval task was assigned to the user.</summary>
    public const string WorkflowTaskAssigned = "WorkflowTaskAssigned";
}
