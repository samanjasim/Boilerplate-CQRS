using System.Text.Json;
using MediatR;
using Starter.Abstractions.Capabilities;
using Starter.Module.Products.Domain.Events;

namespace Starter.Module.Products.Application.EventHandlers;

internal sealed class RecordProductActivity(IActivityService activityService)
    : INotificationHandler<ProductCreatedEvent>
{
    public async Task Handle(ProductCreatedEvent notification, CancellationToken cancellationToken)
    {
        await activityService.RecordAsync(
            "Product",
            notification.ProductId,
            notification.TenantId,
            "created",
            actorId: null,
            metadataJson: JsonSerializer.Serialize(new { notification.Name, notification.Slug }),
            description: $"Product \"{notification.Name}\" was created",
            cancellationToken);
    }
}
