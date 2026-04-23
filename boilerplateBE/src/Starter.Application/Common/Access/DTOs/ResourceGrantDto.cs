using Starter.Domain.Common.Access.Enums;

namespace Starter.Application.Common.Access.DTOs;

public sealed record ResourceGrantDto(
    Guid Id,
    string ResourceType,
    Guid ResourceId,
    GrantSubjectType SubjectType,
    Guid SubjectId,
    string? SubjectDisplayName,
    AccessLevel Level,
    Guid GrantedByUserId,
    DateTime GrantedAt);
