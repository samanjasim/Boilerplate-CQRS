using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Infrastructure.Persistence;
using Starter.Infrastructure.Services.Access;

namespace Starter.Api.Tests.Access._Helpers;

public static class TestAccessFactory
{
    public static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"acl-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options, currentUserService: null);
    }

    public static ResourceAccessService BuildService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        InMemoryCache? cache = null)
    {
        return new ResourceAccessService(db, cache ?? new InMemoryCache(), currentUser);
    }
}
