namespace Starter.Application.Common.Interfaces;

public interface IExportService
{
    byte[] GenerateCsv<T>(IEnumerable<T> data, string[] columnHeaders, Func<T, object[]> rowMapper);
    byte[] GeneratePdf<T>(IEnumerable<T> data, string title, string[] columnHeaders, Func<T, object[]> rowMapper);
}
