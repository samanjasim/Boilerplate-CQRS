namespace Starter.Application.Features.Auth.DTOs;

public sealed record Verify2FAResponseDto(IReadOnlyList<string> BackupCodes);
