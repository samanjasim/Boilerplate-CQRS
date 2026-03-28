using Starter.Application.Common.Interfaces;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Files.Commands.UploadFile;

internal sealed class UploadFileCommandHandler(
    IFileService fileService,
    IApplicationDbContext context,
    IFeatureFlagService flags) : IRequestHandler<UploadFileCommand, Result<FileDto>>
{
    public async Task<Result<FileDto>> Handle(UploadFileCommand request, CancellationToken cancellationToken)
    {
        // Check single file size limit
        var maxSizeMb = await flags.GetValueAsync<int>("files.max_upload_size_mb", cancellationToken);
        var fileSizeMb = (int)(request.Size / (1024 * 1024));
        if (fileSizeMb > maxSizeMb)
            return Result.Failure<FileDto>(FeatureFlagErrors.QuotaExceeded($"MB per file (max {maxSizeMb}MB)", maxSizeMb));

        // Check total storage limit
        var maxStorageMb = await flags.GetValueAsync<int>("files.max_storage_mb", cancellationToken);
        var usedBytes = await context.FileMetadata.SumAsync(f => f.Size, cancellationToken);
        var usedMb = (int)(usedBytes / (1024 * 1024));
        if (usedMb + fileSizeMb > maxStorageMb)
            return Result.Failure<FileDto>(FeatureFlagErrors.QuotaExceeded($"MB storage (max {maxStorageMb}MB)", maxStorageMb));

        var metadata = await fileService.UploadAsync(
            request.Stream,
            request.FileName,
            request.ContentType,
            request.Size,
            request.Category,
            request.EntityId,
            request.EntityType,
            request.Description,
            request.Tags,
            request.IsPublic,
            cancellationToken);

        var url = await fileService.GetUrlAsync(metadata.Id, cancellationToken);

        return Result.Success(metadata.ToDto(url));
    }
}
