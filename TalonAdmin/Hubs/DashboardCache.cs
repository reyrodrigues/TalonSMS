using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;

namespace TalonAdmin.Hubs
{
    /// <summary>
    /// Holds the computed dashboard summary for a short while, because building it walks every
    /// distribution in a programme and its transaction records.
    ///
    /// It used to be written to <c>{ContentRoot}/Dashboards/{organization}-{country}.json</c>, which
    /// was keyed by tenant but not by database. Anything else sharing the content root wrote into
    /// the same files: running the test suite left its own figures behind, and the next request
    /// against a completely different database served them. Two deployments on one volume, or a
    /// database restored underneath a running instance, had the same problem, and there was no
    /// symptom beyond the numbers being wrong.
    ///
    /// In memory there is nothing to share by accident. A process has one database, so an entry
    /// cannot outlive the data it summarises, and a restart starts clean.
    /// </summary>
    public sealed class DashboardCache
    {
        /// <summary>
        /// A backstop, not the main mechanism: <see cref="Invalidate"/> is what runs when a
        /// redemption lands. This bounds how long a change that does not notify — a distribution
        /// created through Breeze, say — can go unseen.
        /// </summary>
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

        private readonly IMemoryCache _cache;

        /// <summary>
        /// Every entry is linked to this token, so cancelling it drops them all at once. Entries
        /// cannot be enumerated out of <see cref="IMemoryCache"/>, and tracking the keys by hand
        /// would leak one per tenant that ever asked.
        /// </summary>
        private CancellationTokenSource _entries = new CancellationTokenSource();

        public DashboardCache(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<JToken> GetOrCreateAsync(int organizationId, int countryId, Func<Task<JToken>> build)
        {
            var key = $"dashboard:{organizationId}:{countryId}";

            // Read the source before building: if Invalidate runs in between, the entry is added
            // already expired and the next caller rebuilds. Erring towards a rebuild is the safe
            // direction — the other way round would serve figures the invalidation meant to drop.
            var token = Volatile.Read(ref _entries).Token;

            return await _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = Lifetime;
                entry.AddExpirationToken(new CancellationChangeToken(token));

                return await build();
            });
        }

        /// <summary>Drops every tenant's summary. Called when a redemption changes the figures.</summary>
        public void Invalidate()
        {
            var previous = Interlocked.Exchange(ref _entries, new CancellationTokenSource());

            previous.Cancel();
            previous.Dispose();
        }
    }
}
