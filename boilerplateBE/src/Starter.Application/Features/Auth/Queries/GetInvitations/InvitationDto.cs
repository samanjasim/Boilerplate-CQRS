namespace Starter.Application.Features.Auth.Queries.GetInvitations;

public sealed record InvitationDto(
    Guid Id,
    string Email,
    string RoleName,
    string InvitedByName,
    DateTime ExpiresAt,
    bool IsAccepted,
    DateTime CreatedAt);
