using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.ImportExport.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.ImportExport.Queries.GetEntityTypes;

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
                Fields: d.Fields.Select(f => f.Name).ToArray()))
            .ToList();

        return Task.FromResult(Result.Success(dtos));
    }
}
