namespace Starter.Abstractions.Readers;

/// <summary>
/// Read-only access to user data from outside the core <c>ApplicationDbContext</c>.
/// See <see cref="ITenantReader"/> for the rationale.
/// </summary>
public interface IUserReader
{
    Task<UserSummary?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSummary>> GetManyAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Plain projection of a user. The display name is pre-formatted so consumers
/// do not need to know about the <c>FullName</c> value object.
/// </summary>
public sealed record UserSummary(
    Guid Id,
    Guid? TenantId,
    string Username,
    string Email,
    string DisplayName,
    string Status);
