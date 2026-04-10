using MediatR;
using Starter.Module.ImportExport.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.ImportExport.Application.Commands.StartImport;

public sealed record StartImportCommand(
    Guid FileId,
    string EntityType,
    ConflictMode ConflictMode,
    Guid? TargetTenantId = null) : IRequest<Result<Guid>>;
