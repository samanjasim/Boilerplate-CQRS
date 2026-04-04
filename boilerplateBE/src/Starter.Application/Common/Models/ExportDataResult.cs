namespace Starter.Application.Common.Models;

public sealed record ExportDataResult(string[] Headers, IReadOnlyList<string[]> Rows, int TotalCount);
