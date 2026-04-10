using MediatR;
using Starter.Module.ImportExport.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.ImportExport.Application.Queries.GetEntityTypes;

public sealed record GetEntityTypesQuery : IRequest<Result<List<EntityTypeDto>>>;
