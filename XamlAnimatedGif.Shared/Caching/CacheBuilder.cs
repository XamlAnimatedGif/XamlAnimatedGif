/// This source file is derived from https://github.com/launchdarkly/dotnet-cache/
/// Under the terms of Apache 2.0 License.

using System;

namespace XamlAnimatedGif.Caching
{
    /// <summary>
    /// Builder for a key-value cache.
    /// </summary>
    /// <typeparam name="K">the key type</typeparam>
    /// <typeparam name="V">the value type</typeparam>
    /// <see cref="Caches.KeyValue{K, V}"/>
    internal class CacheBuilder<K, V> : CacheBuilderBase<CacheBuilder<K, V>>
    {
        internal Func<K, V> LoaderFn { get; private set; }
        internal int? InitialCapacity { get; private set; }
        internal int? MaximumEntries { get; private set; }

        /// <summary>
        /// Specifies a value computation function for a read-through cache.
        /// 
        /// If this is not null, then any call to <see cref="ICache{K, V}.Get(K)"/> or
        /// <see cref="ICache{K, V}.TryGetValue(K, out V)"/> with a key that is not already in the
        /// cache will cause the function to be called with that key as an argument; the returned
        /// value will then be stored in the cache and returned to the caller.
        /// 
        /// If the function is null (the default), then the cache will not be a read-through cache
        /// and will only provide values that were explicitly set.
        /// </summary>
        /// <param name="loaderFn">the function for generating values</param>
        /// <returns>the builder</returns>
        public CacheBuilder<K, V> WithLoader(Func<K, V> loaderFn)
        {
            LoaderFn = loaderFn;
            return this;
        }

        /// <summary>
        /// Specifies the initial capacity of the cache.
        /// 
        /// This is the same as the optional constructor parameter for <code>Dictionary</code>.
        /// It does not affect how many entries can be stored, only how soon the backing
        /// dictionary will need to be resized.
        /// </summary>
        /// <param name="initialCapacity">the initial capacity, or null to use the default</param>
        /// <returns>the builder</returns>
        public CacheBuilder<K, V> WithInitialCapacity(int? initialCapacity)
        {
            if (initialCapacity != null && initialCapacity.Value < 0)
            {
                throw new ArgumentException("must be >= 0 if not null", nameof(initialCapacity));
            }
            InitialCapacity = initialCapacity;
            return this;
        }

        /// <summary>
        /// Specifies the maximum number of entries that can be in the cache.
        /// 
        /// If this is not null, then any attempt to add more entries when the cache has reached
        /// this limit will result in existing entries being evicted, in the order that they were
        /// originally added or last updated.
        /// 
        /// If it is null (the default), then there is no such limit.
        /// </summary>
        /// <param name="maximumEntries">the maximum capacity, or null for no limit</param>
        /// <returns>the builder</returns>
        public CacheBuilder<K, V> WithMaximumEntries(int? maximumEntries)
        {
            if (maximumEntries != null && maximumEntries.Value <= 0)
            {
                throw new ArgumentException("must be > 0 if not null", nameof(maximumEntries));
            }
            MaximumEntries = maximumEntries;
            return this;
        }

        /// <summary>
        /// Constructs a cache with the specified properties.
        /// </summary>
        /// <returns>a cache instance</returns>
        public ICache<K, V> Build()
        {
            return new CacheImpl<K, V>(this);
        }
    }
}
