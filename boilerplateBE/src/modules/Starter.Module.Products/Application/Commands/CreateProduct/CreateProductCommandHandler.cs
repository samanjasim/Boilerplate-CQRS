using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Products.Domain.Entities;
using Starter.Module.Products.Domain.Errors;
using Starter.Module.Products.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Products.Application.Commands.CreateProduct;

internal sealed class CreateProductCommandHandler(
    ProductsDbContext context,
    ICurrentUserService currentUser,
    IQuotaChecker quotaChecker,
    IWebhookPublisher webhookPublisher) : IRequestHandler<CreateProductCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        // Tenant users always use their own tenantId.
        // SuperAdmin (null tenantId) may specify a target tenant via the command.
        var tenantId = currentUser.TenantId ?? request.TenantId;

        if (tenantId.HasValue)
        {
            var quota = await quotaChecker.CheckAsync(tenantId.Value, "products", cancellationToken: cancellationToken);
            if (!quota.Allowed)
                return Result.Failure<Guid>(ProductErrors.QuotaExceeded(quota.Limit));
        }

        // Normalize separately for the uniqueness check — the entity also normalizes
        // in its factory, but we need the normalized value here for the DB query.
        var normalizedSlug = request.Slug.Trim().ToLowerInvariant();
        var slugExists = await context.Products
            .IgnoreQueryFilters()
            .AnyAsync(p => p.TenantId == tenantId && p.Slug == normalizedSlug, cancellationToken);
        if (slugExists)
            return Result.Failure<Guid>(ProductErrors.SlugAlreadyExists);

        var product = Product.Create(
            tenantId,
            request.Name,
            request.Slug,
            request.Description,
            request.Price,
            request.Currency);

        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken);

        if (tenantId.HasValue)
            await quotaChecker.IncrementAsync(tenantId.Value, "products", cancellationToken: cancellationToken);

        await webhookPublisher.PublishAsync(
            "product.created",
            tenantId,
            new { product.Id, product.Name, product.Slug, product.Price, product.Currency },
            cancellationToken);

        return Result.Success(product.Id);
    }
}
