using Starter.Domain.Primitives;

namespace Starter.Domain.Common.Enums;

public sealed class ReportFormat : Enumeration<ReportFormat>
{
    public static readonly ReportFormat Csv = new(1, nameof(Csv));
    public static readonly ReportFormat Pdf = new(2, nameof(Pdf));

    private ReportFormat(int value, string name) : base(value, name) { }
}
