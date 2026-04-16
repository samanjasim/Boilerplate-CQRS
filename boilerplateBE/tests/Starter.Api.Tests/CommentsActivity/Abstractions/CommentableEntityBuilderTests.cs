using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Starter.Abstractions.Capabilities;
using Xunit;

namespace Starter.Api.Tests.CommentsActivity.Abstractions;

public sealed class CommentableEntityBuilderTests
{
    [Fact]
    public void Defaults_MatchRecordConstructorConventions()
    {
        var services = new ServiceCollection();
        services.AddCommentableEntity("Widget", _ => { });

        var registration = services.BuildServiceProvider()
            .GetRequiredService<ICommentableEntityRegistration>();

        registration.Definition.EntityType.Should().Be("Widget");
        registration.Definition.DisplayNameKey.Should().Be("commentsActivity.entityTypes.widget");
        registration.Definition.EnableComments.Should().BeTrue();
        registration.Definition.EnableActivity.Should().BeTrue();
        registration.Definition.CustomActivityTypes.Should().BeEmpty();
        registration.Definition.AutoWatchOnCreate.Should().BeTrue();
        registration.Definition.AutoWatchOnComment.Should().BeTrue();
        registration.Definition.ResolveTenantIdAsync.Should().BeNull();
    }

    [Fact]
    public async Task UseTenantResolver_ResolvesFromFreshScopePerCall()
    {
        var callCounter = new CallCounter();
        var services = new ServiceCollection();
        services.AddSingleton(callCounter);
        services.AddScoped<CountingResolver>();
        services.AddCommentableEntity("Widget", b => b.UseTenantResolver<CountingResolver>());

        var sp = services.BuildServiceProvider();
        var registration = sp.GetRequiredService<ICommentableEntityRegistration>();
        var resolver = registration.Definition.ResolveTenantIdAsync!;

        await resolver(Guid.NewGuid(), sp, CancellationToken.None);
        await resolver(Guid.NewGuid(), sp, CancellationToken.None);

        callCounter.Calls.Should().Be(2);
    }

    [Fact]
    public void Build_EqualsManualRecordConstruction()
    {
        var services = new ServiceCollection();
        services.AddCommentableEntity("Widget", b =>
        {
            b.DisplayNameKey = "custom.key";
            b.EnableComments = false;
            b.CustomActivityTypes = ["foo", "bar"];
            b.AutoWatchOnCreate = false;
        });

        var built = services.BuildServiceProvider()
            .GetRequiredService<ICommentableEntityRegistration>()
            .Definition;

        var manual = new CommentableEntityDefinition(
            "Widget", "custom.key", false, true, ["foo", "bar"],
            AutoWatchOnCreate: false, AutoWatchOnComment: true,
            ResolveTenantIdAsync: null);

        built.EntityType.Should().Be(manual.EntityType);
        built.DisplayNameKey.Should().Be(manual.DisplayNameKey);
        built.EnableComments.Should().Be(manual.EnableComments);
        built.EnableActivity.Should().Be(manual.EnableActivity);
        built.CustomActivityTypes.Should().Equal(manual.CustomActivityTypes);
        built.AutoWatchOnCreate.Should().Be(manual.AutoWatchOnCreate);
        built.AutoWatchOnComment.Should().Be(manual.AutoWatchOnComment);
    }

    private sealed class CallCounter
    {
        public int Calls;
    }

    private sealed class CountingResolver(CallCounter counter) : ITenantResolver
    {
        public Task<Guid?> ResolveTenantIdAsync(Guid entityId, CancellationToken ct)
        {
            Interlocked.Increment(ref counter.Calls);
            return Task.FromResult<Guid?>(Guid.NewGuid());
        }
    }
}
