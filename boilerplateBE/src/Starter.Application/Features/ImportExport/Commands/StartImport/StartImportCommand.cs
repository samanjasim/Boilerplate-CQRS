using MediatR;
using Starter.Domain.ImportExport.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.ImportExport.Commands.StartImport;

public sealed record StartImportCommand(
    Guid FileId,
    string EntityType,
    ConflictMode ConflictMode,
    Guid? TargetTenantId = null) : IRequest<Result<Guid>>;
