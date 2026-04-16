using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Module.CommentsActivity.Infrastructure.Persistence;

namespace Starter.Api.Tests.CommentsActivity.Infrastructure;

internal static class TestDbContextFactory
{
    public static CommentsActivityDbContext InMemory()
    {
        var options = new DbContextOptionsBuilder<CommentsActivityDbContext>()
            .UseInMemoryDatabase(databaseName: $"comments-activity-{Guid.NewGuid()}")
            .Options;
        return new CommentsActivityDbContext(options);
    }
}

internal static class TestDefinitions
{
    public static CommentableEntityDefinition With(
        string entityType,
        Func<Guid, IServiceProvider, CancellationToken, Task<Guid?>>? resolver = null) =>
        new(entityType, "display.key", true, true, [], false, false, resolver);
}
