namespace Starter.Application.Features.Auth.DTOs;

public sealed record Setup2FAResponseDto(string Secret, string QrCodeUri);
