namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Reason a user is watching an entity for comment/activity notifications.
/// </summary>
public enum WatchReason
{
    Explicit,
    Participated,
    Mentioned,
    Created
}
