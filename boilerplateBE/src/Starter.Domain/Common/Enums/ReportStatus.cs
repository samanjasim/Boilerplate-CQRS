using Starter.Domain.Primitives;

namespace Starter.Domain.Common.Enums;

public sealed class ReportStatus : Enumeration<ReportStatus>
{
    public static readonly ReportStatus Pending = new(1, nameof(Pending));
    public static readonly ReportStatus Processing = new(2, nameof(Processing));
    public static readonly ReportStatus Completed = new(3, nameof(Completed));
    public static readonly ReportStatus Failed = new(4, nameof(Failed));

    private ReportStatus(int value, string name) : base(value, name) { }
}
