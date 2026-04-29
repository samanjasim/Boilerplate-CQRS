using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.Products.Application.Queries.GetProductStatusCounts;
using Starter.Module.Products.Domain.Entities;
using Starter.Module.Products.Domain.Enums;
using Starter.Module.Products.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Products;

public sealed class GetProductStatusCountsQueryHandlerTests
{
    [Fact]
    public async Task PlatformAggregate_ReturnsCountsAcrossTenants()
    {
        await using var db = NewDb();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        db.Products.AddRange(
            Product.Create(tenantA, "Draft A", "draft-a", null, 10, "USD"),
            Product.Create(tenantA, "Active A", "active-a", null, 20, "USD", ProductStatus.Active),
            Product.Create(tenantB, "Draft B", "draft-b", null, 30, "USD"),
            Product.Create(tenantB, "Archived B", "archived-b", null, 40, "USD", ProductStatus.Archived)
        );
        await db.SaveChangesAsync();

        var handler = new GetProductStatusCountsQueryHandler(db);
        var result = await handler.Handle(new GetProductStatusCountsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Draft);
        Assert.Equal(1, result.Value.Active);
        Assert.Equal(1, result.Value.Archived);
    }

    [Fact]
    public async Task TenantFilter_ReturnsOnlySelectedTenantCounts()
    {
        await using var db = NewDb();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        db.Products.AddRange(
            Product.Create(tenantA, "Draft A", "draft-a", null, 10, "USD"),
            Product.Create(tenantA, "Active A", "active-a", null, 20, "USD", ProductStatus.Active),
            Product.Create(tenantB, "Archived B", "archived-b", null, 40, "USD", ProductStatus.Archived)
        );
        await db.SaveChangesAsync();

        var handler = new GetProductStatusCountsQueryHandler(db);
        var result = await handler.Handle(new GetProductStatusCountsQuery(tenantA), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Draft);
        Assert.Equal(1, result.Value.Active);
        Assert.Equal(0, result.Value.Archived);
    }

    [Fact]
    public async Task EmptyDatabase_ReturnsZeroCounts()
    {
        await using var db = NewDb();

        var handler = new GetProductStatusCountsQueryHandler(db);
        var result = await handler.Handle(new GetProductStatusCountsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Draft);
        Assert.Equal(0, result.Value.Active);
        Assert.Equal(0, result.Value.Archived);
    }

    private static ProductsDbContext NewDb(Guid? tenantId = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);

        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase($"products-status-counts-{Guid.NewGuid():N}")
            .Options;

        return new ProductsDbContext(options, currentUser.Object);
    }
}
