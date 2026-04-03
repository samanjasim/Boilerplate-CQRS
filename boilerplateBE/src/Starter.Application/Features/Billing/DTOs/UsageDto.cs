namespace Starter.Application.Features.Billing.DTOs;

public sealed record UsageDto(
    long Users, long StorageBytes, long ApiKeys, long ReportsActive, int Webhooks,
    int MaxUsers, long MaxStorageBytes, int MaxApiKeys, int MaxReports, int MaxWebhooks);
