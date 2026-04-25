using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Module.ImportExport.Domain.Entities;
using Starter.Module.ImportExport.Domain.Errors;
using Starter.Domain.Identity.Errors;
using Starter.Domain.Tenants.Entities;
using Starter.Module.ImportExport.Application.Messages;
using Starter.Module.ImportExport.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.ImportExport.Application.Commands.StartImport;

internal sealed class StartImportCommandHandler(
    IApplicationDbContext appContext,
    ImportExportDbContext importExportContext,
    ICurrentUserService currentUserService,
    IFeatureFlagService featureFlagService,
    IMessagePublisher messagePublisher,
    IImportExportRegistry registry,
    IStorageService storageService) : IRequestHandler<StartImportCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        StartImportCommand request, CancellationToken cancellationToken)
    {
        if (!await featureFlagService.IsEnabledAsync("imports.enabled", cancellationToken))
            return Result.Failure<Guid>(ImportExportErrors.ImportsDisabled);

        var definition = registry.GetDefinition(request.EntityType);
        if (definition is null)
            return Result.Failure<Guid>(ImportExportErrors.EntityTypeNotFound);

        if (!definition.SupportsImport)
            return Result.Failure<Guid>(ImportExportErrors.ImportNotSupported);

        var fileMetadata = await appContext.Set<FileMetadata>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.FileId, cancellationToken);

        if (fileMetadata is null)
            return Result.Failure<Guid>(ImportExportErrors.FileNotFound);

        Stream fileStream;
        try
        {
            fileStream = await storageService.DownloadAsync(fileMetadata.StorageKey, cancellationToken);
        }
        catch
        {
            return Result.Failure<Guid>(ImportExportErrors.FileNotFound);
        }

        int rowCount;
        using (fileStream)
        using (var reader = new StreamReader(fileStream, Encoding.UTF8))
        {
            // Skip header
            await reader.ReadLineAsync(cancellationToken);

            rowCount = 0;
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    rowCount++;
            }
        }

        var maxRows = await featureFlagService.GetValueAsync<int>("imports.max_rows", cancellationToken);
        if (rowCount > maxRows)
            return Result.Failure<Guid>(ImportExportErrors.RowLimitExceeded(maxRows));

        var userId = currentUserService.UserId;
        if (userId is null)
            return Result.Failure<Guid>(UserErrors.Unauthorized());

        Guid? tenantId;
        if (request.TargetTenantId.HasValue)
        {
            // Only platform admins (no tenantId) can specify a target tenant
            if (currentUserService.TenantId.HasValue)
                return Result.Failure<Guid>(ImportExportErrors.UnauthorizedTenantSelection);

            // Validate tenant exists
            var tenantExists = await appContext.Set<Tenant>().IgnoreQueryFilters()
                .AnyAsync(t => t.Id == request.TargetTenantId.Value, cancellationToken);
            if (!tenantExists)
                return Result.Failure<Guid>(ImportExportErrors.TenantNotFound);

            tenantId = request.TargetTenantId.Value;
        }
        else if (currentUserService.TenantId.HasValue)
        {
            tenantId = currentUserService.TenantId.Value;
        }
        else
        {
            // Platform admin with no target tenant
            if (definition.RequiresTenant)
                return Result.Failure<Guid>(ImportExportErrors.TenantRequired);

            tenantId = null;
        }

        var job = ImportJob.Create(
            tenantId,
            request.EntityType,
            fileMetadata.FileName,
            request.FileId,
            request.ConflictMode,
            userId.Value);

        importExportContext.ImportJobs.Add(job);
        await importExportContext.SaveChangesAsync(cancellationToken);

        // Schedule via collector + flush appContext to commit the outbox row.
        // Two separate commits (ImportExport + Application) — not atomic across
        // both DBs, but ImportJob is durable before this point and a delayed
        // appContext save just defers consumer pickup, never loses the job.
        await messagePublisher.PublishAsync(new ProcessImportMessage(job.Id), cancellationToken);
        await appContext.SaveChangesAsync(cancellationToken);

        return Result.Success(job.Id);
    }
}
