namespace Starter.Module.CommentsActivity.Application.DTOs;

public sealed record ActivityEntryDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Action,
    Guid? ActorId,
    string? ActorName,
    string? MetadataJson,
    string? Description,
    DateTime CreatedAt);
