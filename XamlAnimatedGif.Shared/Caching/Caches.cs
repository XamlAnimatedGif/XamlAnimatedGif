/// This source file is derived from https://github.com/launchdarkly/dotnet-cache/
/// Under the terms of Apache 2.0 License.

namespace XamlAnimatedGif.Caching
{
    /// <summary>
    /// Methods for building caches.
    /// </summary>
    public abstract class Caches
    {
        /// <summary>
        /// Starts constructing a key-value cache.
        /// </summary>
        /// <typeparam name="K">the key type</typeparam>
        /// <typeparam name="V">the value type</typeparam>
        /// <returns>a builder</returns>
        public static CacheBuilder<K, V> KeyValue<K, V>()
        {
            return new CacheBuilder<K, V>();
        }
    }
}
