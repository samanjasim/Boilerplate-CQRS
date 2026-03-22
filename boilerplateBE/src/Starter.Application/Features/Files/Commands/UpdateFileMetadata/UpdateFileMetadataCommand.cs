using Starter.Domain.Common.Enums;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Files.Commands.UpdateFileMetadata;

public sealed record UpdateFileMetadataCommand(
    Guid Id,
    string? Description,
    FileCategory? Category,
    string[]? Tags) : IRequest<Result<FileDto>>;
