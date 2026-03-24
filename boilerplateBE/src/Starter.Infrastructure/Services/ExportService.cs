using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Services;

public sealed class ExportService : IExportService
{
    public byte[] GenerateCsv<T>(IEnumerable<T> data, string[] columnHeaders, Func<T, object[]> rowMapper)
    {
        var sb = new StringBuilder();

        sb.AppendLine(string.Join(",", columnHeaders.Select(EscapeCsvField)));

        foreach (var item in data)
        {
            var values = rowMapper(item);
            sb.AppendLine(string.Join(",", values.Select(v => EscapeCsvField(v?.ToString() ?? string.Empty))));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    public byte[] GeneratePdf<T>(IEnumerable<T> data, string title, string[] columnHeaders, Func<T, object[]> rowMapper)
    {
        var rows = data.Select(rowMapper).ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text(title).FontSize(16).Bold();
                    col.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
                        .FontSize(8).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        for (var i = 0; i < columnHeaders.Length; i++)
                            columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var headerText in columnHeaders)
                        {
                            header.Cell()
                                .Background(Colors.Grey.Lighten3)
                                .Padding(5)
                                .Text(headerText)
                                .Bold()
                                .FontSize(9);
                        }
                    });

                    foreach (var row in rows)
                    {
                        foreach (var cell in row)
                        {
                            table.Cell()
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(4)
                                .Text(cell?.ToString() ?? string.Empty)
                                .FontSize(8);
                        }
                    }
                });

                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
            });
        });

        return document.GeneratePdf();
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}
