using Starter.Domain.ImportExport.Enums;

namespace Starter.Application.Common.Models;

public sealed record ImportRowResult(ImportRowStatus Status, string? EntityId = null, string? ErrorMessage = null);
