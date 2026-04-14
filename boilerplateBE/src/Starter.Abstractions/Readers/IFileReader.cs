namespace Starter.Abstractions.Readers;

/// <summary>
/// Read-only access to file metadata from outside the core <c>ApplicationDbContext</c>.
/// See <see cref="ITenantReader"/> for the rationale.
/// </summary>
public interface IFileReader
{
    Task<FileSummary?> GetAsync(Guid fileId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileSummary>> GetManyAsync(
        IEnumerable<Guid> fileIds,
        CancellationToken cancellationToken = default);
}

public sealed record FileSummary(
    Guid Id,
    string FileName,
    string ContentType,
    long Size);
