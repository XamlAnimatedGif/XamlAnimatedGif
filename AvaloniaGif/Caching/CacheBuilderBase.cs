/// This source file is derived from https://github.com/launchdarkly/dotnet-cache/
/// Under the terms of Apache 2.0 License.

using System;

namespace AvaloniaGif.Caching
{
    /// <summary>
    /// Basic builder methods common to all caches.
    /// </summary>
    /// <typeparam name="B">the specific builder subclass</typeparam>
    public class CacheBuilderBase<B> where B : CacheBuilderBase<B>
    {
        internal TimeSpan? Expiration { get; private set; }
        internal TimeSpan? PurgeInterval { get; private set; }

        /// <summary>
        /// Sets the maximum time (TTL) that any value will be retained in the cache. This time is
        /// counted from the time when the value was last written (added or updated).
        /// 
        /// If this is null, values will never expire.
        /// </summary>
        /// <param name="expiration">the expiration time, or null if values should never expire</param>
        /// <returns></returns>
        public B WithExpiration(TimeSpan? expiration)
        {
            Expiration = expiration;
            return (B)this;
        }

        /// <summary>
        /// Sets the interval in between automatic purges of expired values.
        /// 
        /// If this is not null, then a background task will run at that frequency to sweep the cache for
        /// all expired values.
        /// 
        /// If it is null, expired values will be removed only at the time when you try to access them.
        /// 
        /// This value is ignored if the expiration time (<see cref="WithExpiration(TimeSpan?)"/>) is null.
        /// </summary>
        /// <param name="purgeInterval">the purge interval, or null to turn off automatic purging</param>
        /// <returns></returns>
        public B WithBackgroundPurge(TimeSpan? purgeInterval)
        {
            PurgeInterval = purgeInterval;
            return (B)this;
        }
    }
}
