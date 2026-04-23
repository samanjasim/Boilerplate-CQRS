using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Events;
using Starter.Domain.FeatureFlags.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Files.Commands.UploadFile;

internal sealed class UploadFileCommandHandler(
    IFileService fileService,
    IFeatureFlagService flags,
    ICurrentUserService currentUser,
    IUsageTracker usageTracker,
    IPublisher publisher) : IRequestHandler<UploadFileCommand, Result<FileDto>>
{
    public async Task<Result<FileDto>> Handle(UploadFileCommand request, CancellationToken cancellationToken)
    {
        // Check single file size limit
        var maxSizeMb = await flags.GetValueAsync<int>("files.max_upload_size_mb", cancellationToken);
        var fileSizeMb = (int)(request.Size / (1024 * 1024));
        if (fileSizeMb > maxSizeMb)
            return Result.Failure<FileDto>(FeatureFlagErrors.QuotaExceeded($"MB per file (max {maxSizeMb}MB)", maxSizeMb));

        // Check total storage limit
        var tenantId = currentUser.TenantId;
        if (tenantId.HasValue)
        {
            var maxStorageMb = await flags.GetValueAsync<int>("files.max_storage_mb", cancellationToken);
            var usedBytes = await usageTracker.GetAsync(tenantId.Value, "storage_bytes", cancellationToken);
            var usedMb = (int)(usedBytes / (1024 * 1024));
            var requestedFileSizeMb = (int)(request.Size / (1024 * 1024));
            if (usedMb + requestedFileSizeMb > maxStorageMb)
                return Result.Failure<FileDto>(FeatureFlagErrors.QuotaExceeded("storage", maxStorageMb));
        }

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
            request.Visibility,
            cancellationToken);

        var url = await fileService.GetUrlAsync(metadata.Id, cancellationToken);

        if (tenantId.HasValue)
            await usageTracker.IncrementAsync(tenantId.Value, "storage_bytes", request.Size, cancellationToken);

        await publisher.Publish(
            new FileUploadedEvent(metadata.Id, metadata.TenantId, metadata.FileName, metadata.Size, metadata.ContentType),
            cancellationToken);

        return Result.Success(metadata.ToDto(url));
    }
}
