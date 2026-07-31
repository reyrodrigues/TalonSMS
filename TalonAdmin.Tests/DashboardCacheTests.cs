using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using TalonAdmin.Hubs;

namespace TalonAdmin.Tests;

/// <summary>
/// The dashboard summary is expensive enough to be worth holding, and wrong often enough to be
/// worth dropping. Both halves matter: without the caching a dashboard request walks every
/// distribution in the programme, and without the invalidation a redemption does not show up until
/// the entry ages out.
/// </summary>
public class DashboardCacheTests
{
    private static DashboardCache NewCache() =>
        new(new MemoryCache(new MemoryCacheOptions()));

    private static Task<JToken> Summary(string name) =>
        Task.FromResult<JToken>(new JArray(new JObject { ["Program"] = name }));

    [Fact]
    public async Task A_summary_is_built_once_and_then_reused()
    {
        var cache = NewCache();
        var builds = 0;

        for (var i = 0; i < 3; i++)
        {
            await cache.GetOrCreateAsync(1, 1, () => { builds++; return Summary("first"); });
        }

        Assert.Equal(1, builds);
    }

    [Fact]
    public async Task Each_tenant_gets_its_own()
    {
        var cache = NewCache();

        var jordan = await cache.GetOrCreateAsync(1, 2, () => Summary("Jordan"));
        var lebanon = await cache.GetOrCreateAsync(1, 3, () => Summary("Lebanon"));

        // The old file cache keyed on the same pair, so this much always held. What it did not do
        // was key on the database, which is why one country could be served another's figures.
        Assert.Contains("Jordan", jordan.ToString());
        Assert.Contains("Lebanon", lebanon.ToString());
    }

    [Fact]
    public async Task Invalidating_drops_every_tenant_so_a_redemption_is_visible_at_once()
    {
        var cache = NewCache();

        await cache.GetOrCreateAsync(1, 2, () => Summary("before"));
        await cache.GetOrCreateAsync(1, 3, () => Summary("before"));

        cache.Invalidate();

        var jordan = await cache.GetOrCreateAsync(1, 2, () => Summary("after"));
        var lebanon = await cache.GetOrCreateAsync(1, 3, () => Summary("after"));

        Assert.Contains("after", jordan.ToString());
        Assert.Contains("after", lebanon.ToString());
    }

    [Fact]
    public async Task It_keeps_caching_after_an_invalidation()
    {
        var cache = NewCache();

        await cache.GetOrCreateAsync(1, 1, () => Summary("before"));
        cache.Invalidate();

        var builds = 0;
        for (var i = 0; i < 3; i++)
        {
            await cache.GetOrCreateAsync(1, 1, () => { builds++; return Summary("after"); });
        }

        // The token is replaced rather than left cancelled, so entries added afterwards survive.
        Assert.Equal(1, builds);
    }
}
