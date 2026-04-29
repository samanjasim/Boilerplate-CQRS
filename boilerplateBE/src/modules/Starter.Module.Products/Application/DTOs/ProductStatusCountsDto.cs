namespace Starter.Module.Products.Application.DTOs;

public sealed record ProductStatusCountsDto(
    int Draft,
    int Active,
    int Archived
);
