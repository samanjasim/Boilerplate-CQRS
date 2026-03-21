using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Services;

public sealed class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
