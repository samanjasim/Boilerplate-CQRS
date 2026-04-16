namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Marker collected via DI to register an entity type that supports comments
/// and/or activity tracking. Modules add these in their
/// <c>ConfigureServices</c>; the <see cref="ICommentableEntityRegistry"/>
/// implementation collects them at startup.
/// </summary>
public interface ICommentableEntityRegistration
{
    CommentableEntityDefinition Definition { get; }
}

/// <summary>
/// Default concrete implementation of <see cref="ICommentableEntityRegistration"/>.
/// </summary>
public sealed record CommentableEntityRegistration(
    CommentableEntityDefinition Definition) : ICommentableEntityRegistration;
