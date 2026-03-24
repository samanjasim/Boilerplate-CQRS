using Starter.Domain.Primitives;

namespace Starter.Domain.Common.Enums;

public sealed class ReportType : Enumeration<ReportType>
{
    public static readonly ReportType AuditLogs = new(1, nameof(AuditLogs));
    public static readonly ReportType Users = new(2, nameof(Users));
    public static readonly ReportType Files = new(3, nameof(Files));

    private ReportType(int value, string name) : base(value, name) { }
}
