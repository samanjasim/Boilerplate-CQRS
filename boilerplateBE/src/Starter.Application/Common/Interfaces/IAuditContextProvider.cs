namespace Starter.Application.Common.Interfaces;

public interface IAuditContextProvider
{
    string? IpAddress { get; }
    string? CorrelationId { get; }
    string? UserDisplayName { get; }
}
