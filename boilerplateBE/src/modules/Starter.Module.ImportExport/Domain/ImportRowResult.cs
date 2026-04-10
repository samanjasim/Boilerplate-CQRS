using Starter.Module.ImportExport.Domain.Enums;

namespace Starter.Module.ImportExport.Domain;

/// <summary>
/// Result of processing a single row during an import. Returned by
/// <see cref="IImportRowProcessor.ProcessRowAsync"/>.
/// </summary>
public sealed record ImportRowResult(ImportRowStatus Status, string? EntityId = null, string? ErrorMessage = null);
