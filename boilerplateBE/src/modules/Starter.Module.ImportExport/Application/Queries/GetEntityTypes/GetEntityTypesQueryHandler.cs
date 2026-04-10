using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Module.ImportExport.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.ImportExport.Application.Queries.GetEntityTypes;

internal sealed class GetEntityTypesQueryHandler(
    IImportExportRegistry registry) : IRequestHandler<GetEntityTypesQuery, Result<List<EntityTypeDto>>>
{
    public Task<Result<List<EntityTypeDto>>> Handle(
        GetEntityTypesQuery request, CancellationToken cancellationToken)
    {
        var dtos = registry.GetAll()
            .Select(d => new EntityTypeDto(
                EntityType: d.EntityType,
                DisplayName: d.DisplayNameKey,
                SupportsExport: d.SupportsExport,
                SupportsImport: d.SupportsImport,
                Fields: d.Fields.Select(f => f.Name).ToArray(),
                RequiresTenant: d.RequiresTenant))
            .ToList();

        return Task.FromResult(Result.Success(dtos));
    }
}
