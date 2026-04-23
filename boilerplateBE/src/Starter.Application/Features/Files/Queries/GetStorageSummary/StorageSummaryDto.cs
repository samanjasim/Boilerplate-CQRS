namespace Starter.Application.Features.Files.Queries.GetStorageSummary;

public sealed record StorageSummaryDto(
    long TotalBytes,
    IReadOnlyList<CategoryBytes> ByCategory,
    IReadOnlyList<EntityTypeBytes> ByEntityType,
    IReadOnlyList<UploaderBytes> TopUploaders);

public sealed record CategoryBytes(string Category, long Bytes, int FileCount);

public sealed record EntityTypeBytes(string? EntityType, long Bytes, int FileCount);

public sealed record UploaderBytes(Guid UserId, string? UserName, long Bytes, int FileCount);
