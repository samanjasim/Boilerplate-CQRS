using Starter.Domain.Common;

namespace Starter.Module.Products.Domain.Events;

public sealed record ProductCreatedEvent(
    Guid ProductId,
    Guid? TenantId,
    string Name,
    string Slug) : DomainEventBase;
