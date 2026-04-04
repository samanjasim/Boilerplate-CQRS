namespace Starter.Application.Features.ImportExport.DTOs;

public sealed record EntityTypeDto(
    string EntityType, string DisplayName, bool SupportsExport, bool SupportsImport, string[] Fields,
    bool RequiresTenant);
