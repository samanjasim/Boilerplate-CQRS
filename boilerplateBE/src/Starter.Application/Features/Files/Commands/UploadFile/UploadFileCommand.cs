using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Enums;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Files.Commands.UploadFile;

public sealed record UploadFileCommand(
    Stream Stream,
    string FileName,
    string ContentType,
    long Size,
    FileCategory Category,
    string? Description,
    string[]? Tags,
    string? EntityType,
    Guid? EntityId,
    ResourceVisibility Visibility) : IRequest<Result<FileDto>>;
