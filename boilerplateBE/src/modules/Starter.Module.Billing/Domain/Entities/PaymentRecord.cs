using Starter.Domain.Common;
using Starter.Module.Billing.Domain.Enums;

namespace Starter.Module.Billing.Domain.Entities;

public sealed class PaymentRecord : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Guid TenantSubscriptionId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = default!;
    public PaymentStatus Status { get; private set; }
    public string? ExternalPaymentId { get; private set; }
    public string? InvoiceUrl { get; private set; }
    public string? Description { get; private set; }
    public DateTime? PeriodStart { get; private set; }
    public DateTime? PeriodEnd { get; private set; }

    public TenantSubscription Subscription { get; private set; } = default!;

    private PaymentRecord() { }

    private PaymentRecord(
        Guid id,
        Guid tenantId,
        Guid tenantSubscriptionId,
        decimal amount,
        string currency,
        PaymentStatus status,
        string? externalPaymentId,
        string? invoiceUrl,
        string? description,
        DateTime? periodStart,
        DateTime? periodEnd) : base(id)
    {
        TenantId = tenantId;
        TenantSubscriptionId = tenantSubscriptionId;
        Amount = amount;
        Currency = currency;
        Status = status;
        ExternalPaymentId = externalPaymentId;
        InvoiceUrl = invoiceUrl;
        Description = description;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
    }

    public static PaymentRecord Create(
        Guid tenantId,
        Guid tenantSubscriptionId,
        decimal amount,
        string currency,
        PaymentStatus status,
        string? externalPaymentId,
        string? invoiceUrl,
        string? description,
        DateTime? periodStart,
        DateTime? periodEnd)
    {
        return new PaymentRecord(
            Guid.NewGuid(),
            tenantId,
            tenantSubscriptionId,
            amount,
            currency.Trim().ToUpperInvariant(),
            status,
            externalPaymentId?.Trim(),
            invoiceUrl?.Trim(),
            description?.Trim(),
            periodStart,
            periodEnd);
    }
}
