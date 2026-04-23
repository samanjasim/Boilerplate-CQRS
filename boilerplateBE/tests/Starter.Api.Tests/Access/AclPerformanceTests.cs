using System.Diagnostics;
using FluentAssertions;
using Starter.Api.Tests.Access._Helpers;
using Starter.Application.Common.Access.Contracts;
using Starter.Domain.Common.Access;
using Starter.Domain.Common.Access.Enums;
using Xunit;

namespace Starter.Api.Tests.Access;

/// <summary>
/// Performance guard tests for acl-resolve latency (spec §7.4).
/// These tests run against an in-memory DB + cache; thresholds are set conservatively
/// to catch accidental regressions (e.g. N+1 queries, missing indexes) while tolerating
/// the expected sub-millisecond in-process path.
/// </summary>
public sealed class AclPerformanceTests
{
    private const int Iterations = 100;
    private const double WarmCacheP95Ms = 5.0;
    private const double ColdResolverP95Ms = 25.0;

    private static double P95(List<double> measurements)
    {
        measurements.Sort();
        var idx = (int)Math.Ceiling(0.95 * measurements.Count) - 1;
        return measurements[Math.Clamp(idx, 0, measurements.Count - 1)];
    }

    [Fact]
    public async Task acl_resolve_warmCache_p95_under_5ms()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        await using var db = TestAccessFactory.CreateDb();
        var cache = new InMemoryCache();
        var caller = FakeCurrentUser.For(user, tenant);
        var svc = TestAccessFactory.BuildService(db, caller, cache);

        // Warm the cache with one call
        await svc.ResolveAccessibleResourcesAsync(caller, ResourceTypes.File, CancellationToken.None);

        var timings = new List<double>(Iterations);
        for (var i = 0; i < Iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await svc.ResolveAccessibleResourcesAsync(caller, ResourceTypes.File, CancellationToken.None);
            sw.Stop();
            timings.Add(sw.Elapsed.TotalMilliseconds);
        }

        var p95 = P95(timings);
        p95.Should().BeLessThan(WarmCacheP95Ms,
            $"warm-cache p95 must be < {WarmCacheP95Ms}ms; measured {p95:F3}ms");
    }

    [Fact]
    public async Task acl_resolve_coldResolver_p95_under_25ms()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        await using var db = TestAccessFactory.CreateDb();

        // Seed 50 grants so the resolver has real work to do
        var grants = Enumerable.Range(0, 50)
            .Select(_ => ResourceGrant.Create(
                tenant, ResourceTypes.File, Guid.NewGuid(),
                GrantSubjectType.User, user, AccessLevel.Viewer, Guid.NewGuid()))
            .ToList();
        db.ResourceGrants.AddRange(grants);
        await db.SaveChangesAsync();

        var caller = FakeCurrentUser.For(user, tenant);

        var timings = new List<double>(Iterations);
        for (var i = 0; i < Iterations; i++)
        {
            // Fresh cache each iteration → forces a DB query every call
            var freshCache = new InMemoryCache();
            var svc = TestAccessFactory.BuildService(db, caller, freshCache);

            var sw = Stopwatch.StartNew();
            await svc.ResolveAccessibleResourcesAsync(caller, ResourceTypes.File, CancellationToken.None);
            sw.Stop();
            timings.Add(sw.Elapsed.TotalMilliseconds);
        }

        var p95 = P95(timings);
        p95.Should().BeLessThan(ColdResolverP95Ms,
            $"cold-resolver p95 must be < {ColdResolverP95Ms}ms; measured {p95:F3}ms");
    }

    [Fact]
    public async Task grant_resolve_1000_grants_does_not_cause_NPlusOne()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        await using var db = TestAccessFactory.CreateDb();

        // Seed 1000 grants for this user
        var grants = Enumerable.Range(0, 1000)
            .Select(_ => ResourceGrant.Create(
                tenant, ResourceTypes.File, Guid.NewGuid(),
                GrantSubjectType.User, user, AccessLevel.Viewer, Guid.NewGuid()))
            .ToList();
        db.ResourceGrants.AddRange(grants);
        await db.SaveChangesAsync();

        var caller = FakeCurrentUser.For(user, tenant);
        var svc = TestAccessFactory.BuildService(db, caller, new InMemoryCache());

        var sw = Stopwatch.StartNew();
        var result = await svc.ResolveAccessibleResourcesAsync(caller, ResourceTypes.File, CancellationToken.None);
        sw.Stop();

        result.ExplicitGrantedResourceIds.Should().HaveCount(1000,
            "all 1000 grants must be returned in a single bulk query");

        // Even with 1000 grants, the single bulk query should complete well under 250ms in-memory.
        sw.ElapsedMilliseconds.Should().BeLessThan(250,
            "1000-grant resolve must complete in a single bulk query, not N+1 per grant");
    }
}
