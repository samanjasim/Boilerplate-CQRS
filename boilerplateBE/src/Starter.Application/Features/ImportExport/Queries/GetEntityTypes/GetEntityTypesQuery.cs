using MediatR;
using Starter.Application.Features.ImportExport.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.ImportExport.Queries.GetEntityTypes;

public sealed record GetEntityTypesQuery : IRequest<Result<List<EntityTypeDto>>>;
