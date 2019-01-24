/// This source file is derived from https://github.com/launchdarkly/dotnet-cache/
/// Under the terms of Apache 2.0 License.

using System;
using System.Collections.Generic;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaGif.Caching
{
    /// <summary>
    /// A concurrent in-memory cache with optional read-through behavior, an optional TTL, and the
    /// ability to explicitly set values. Expired entries are purged by a background task.
    /// 
    /// A cache hit, or a miss without read-through, requires only one read lock. A cache miss
    /// with read-through requires read and write locks on the cache, and then a write lock on the
    /// individual entry.
    /// 
    /// Loading requests are coalesced, i.e. if multiple threads request the same key at the same
    /// time, only one will call the loader function and the others will wait on it.
    ///
    /// Null values are allowed.
    /// </summary>
    internal sealed class CacheImpl<K, V> : ICache<K, V>
    {
        internal static readonly TimeSpan DefaultPurgeInterval = TimeSpan.FromSeconds(30);

        private readonly Func<K, V> _loaderFn;
        private readonly TimeSpan? _expiration;
        private readonly TimeSpan? _purgeInterval;
        private readonly bool _doSlidingExp;
        private readonly int? _maxEntries;
        private readonly IDictionary<K, CacheEntry<K, V>> _entries;
        private readonly LinkedList<K> _keysInCreationOrder = new LinkedList<K>();
        private readonly ReaderWriterLockSlim _wholeCacheLock = new ReaderWriterLockSlim();
        private volatile bool _disposed = false;

        public CacheImpl(CacheBuilder<K, V> builder)
        {
            if (builder.InitialCapacity != null)
            {
                _entries = new Dictionary<K, CacheEntry<K, V>>(builder.InitialCapacity.Value);
            }
            else
            {
                _entries = new Dictionary<K, CacheEntry<K, V>>();
            }

            _maxEntries = builder.MaximumEntries;
            _loaderFn = builder.LoaderFn;
            _expiration = builder.Expiration;
            _purgeInterval = builder.PurgeInterval;
            _doSlidingExp = builder.DoSlidingExpiration ?? false;
            
            if (_expiration.HasValue && _purgeInterval.HasValue)
            {
                TimeSpan interval = _purgeInterval.Value;
                Task.Run(() => PurgeExpiredEntriesAsync(interval));
            }
        }

        public bool ContainsKey(K key)
        {
            if (_loaderFn != null)
            {
                // Always true because if we didn't already have a value, Get will acquire one.
                return true;
            }

            return TryGetValue(key, out var ignoreValue);
        }

        public bool TryGetValue(K key, out V value)
        {
            _wholeCacheLock.EnterReadLock();
            bool entryExists;
            CacheEntry<K, V> entry;
            try
            {
                entryExists = _entries.TryGetValue(key, out entry);
            }
            finally
            {
                _wholeCacheLock.ExitReadLock();
            }

            if (entryExists)
            {
                // Reset entry expiration when sliding expiration is enabled.
                if (_doSlidingExp & _expiration.HasValue)
                    entry.expirationTime = DateTime.Now.Add(_expiration.Value);

                if (entry.IsExpired())
                {
                    // If _purgeInterval is non-null then we will leave it for the background task to handle.
                    // Likewise, if we have a loader function, then we don't need to explicitly remove it here
                    // because it will get overwritten further down. But if we don't have a loader and we
                    // don't have a background task, then we need to remove the expired entry now.
                    if (_purgeInterval == null && _loaderFn == null)
                    {
                        Remove(key);
                    }
                }
                else
                {
                    // This key exists in the cache, but may or may not have a value yet. If the value property
                    // is non-null then we can use the wrapped value without acquiring a lock, since the value
                    // will never change for a CacheEntry once it's been set.
                    var v = entry.value;
                    if (v != null)
                    {
                        value = v.Value;
                        return true;
                    }

                    if (_loaderFn != null)
                    {
                        value = MaybeComputeValue(key, entry);
                        return true;
                    }

                    // Note that if _computeFn is null, we shouldn't have added a cache entry without a value
                    // in the first place, but if that somehow happens then we want to fall through here and
                    // and treat it as a miss.
                }
            }

            if (_loaderFn == null)
            {
                // This isn't a read-through cache, so it's just a miss.
                value = default(V);
                return false;
            }

            // The entry needs to be added to the cache. First add it without a value, so we can quickly release the
            // lock on the whole cache.
            _wholeCacheLock.EnterWriteLock();
            try
            {
                // Check for the entry again in case someone got in ahead of us
                entry = null;
                if (!_entries.TryGetValue(key, out entry) || entry.IsExpired())
                {
                    if (entry != null) // An entry does exist, but it's expired
                    {
                        _keysInCreationOrder.Remove(entry.node);
                    }

                    DateTime? expTime = null;

                    if (_expiration.HasValue)
                    {
                        expTime = DateTime.Now.Add(_expiration.Value);
                    }

                    var node = new LinkedListNode<K>(key);
                    entry = new CacheEntry<K, V>(expTime, node);
                    _entries[key] = entry;
                    _keysInCreationOrder.AddLast(node);
                    PurgeExcessEntries();
                }
            }
            finally
            {
                _wholeCacheLock.ExitWriteLock();
            }

            // Now proceed as if the entry was already in the cache, computing its value if necessary
            value = MaybeComputeValue(key, entry);
            return true;
        }

        public V Get(K key)
        {
            V value;
            TryGetValue(key, out value);
            return value;
        }

        private V MaybeComputeValue(K key, CacheEntry<K, V> entry)
        {
            // At this point we have a cache entry with no value. Whichever thread acquires the
            // per-entry lock first will compute the value; the others will wait on it.
            lock (entry.entryLock)
            {
                // Check the value field in case someone got in ahead of us
                if (entry.value != null)
                {
                    return entry.value.Value;
                }

                var value = _loaderFn.Invoke(key);
                entry.value = new CacheValue<V>(value);
                return value;
            }
        }

        public void Set(K key, V value)
        {
            _wholeCacheLock.EnterWriteLock();
            try
            {
                if (_entries.TryGetValue(key, out var oldEntry))
                {
                    _keysInCreationOrder.Remove(oldEntry.node);
                }

                DateTime? expTime = null;
                if (_expiration.HasValue)
                {
                    expTime = DateTime.Now.Add(_expiration.Value);
                }

                var node = new LinkedListNode<K>(key);
                var entry = new CacheEntry<K, V>(expTime, node);
                entry.value = new CacheValue<V>(value);
                _entries[key] = entry;
                _keysInCreationOrder.AddLast(node);
                PurgeExcessEntries();
            }
            finally
            {
                _wholeCacheLock.ExitWriteLock();
            }
        }

        public void Remove(K key)
        {
            _wholeCacheLock.EnterWriteLock();
            try
            {
                if (_entries.TryGetValue(key, out var entry))
                {
                    _entries.Remove(key);
                    _keysInCreationOrder.Remove(entry.node);
                }
            }
            finally
            {
                _wholeCacheLock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            _wholeCacheLock.EnterWriteLock();
            try
            {
                _entries.Clear();
            }
            finally
            {
                _wholeCacheLock.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            _disposed = true;
        }

        private void PurgeExcessEntries()
        {
            // must be called under a write lock
            if (!_wholeCacheLock.IsWriteLockHeld)
            {
                return;
            }

            if (_maxEntries != null)
            {
                while (_entries.Count > _maxEntries.Value)
                {
                    var first = _keysInCreationOrder.First;
                    _keysInCreationOrder.RemoveFirst();
                    _entries.Remove(first.Value);
                }
            }
        }

        private void PurgeExpiredEntries()
        {
            _wholeCacheLock.EnterWriteLock();
            try
            {
                while (_keysInCreationOrder.Count > 0 &&
                       _entries[_keysInCreationOrder.First.Value].IsExpired())
                {
                    _entries.Remove(_keysInCreationOrder.First.Value);
                    _keysInCreationOrder.RemoveFirst();
                }
            }
            finally
            {
                _wholeCacheLock.ExitWriteLock();
            }
        }

        private async Task PurgeExpiredEntriesAsync(TimeSpan interval)
        {
            while (!_disposed)
            {
                await Task.Delay(interval);
                PurgeExpiredEntries();
            }
        }
    }

    internal class CacheEntry<K, V>
    {
        public DateTime? expirationTime;
        public readonly object entryLock;
        public readonly LinkedListNode<K> node;
        public volatile CacheValue<V> value;

        public CacheEntry(DateTime? expirationTime, LinkedListNode<K> node)
        {
            this.expirationTime = expirationTime;
            this.node = node;
            entryLock = new object();
        }

        public bool IsExpired()
        {
            return expirationTime.HasValue && expirationTime.Value.CompareTo(DateTime.Now) <= 0;
        }
    }

    // This wrapper class is used so that CacheEntry's value field will always be a reference type
    // and can therefore be volatile no matter what V is. If we have an instance of CacheValue, then
    // the cache key has a value, even if the defined value is null.
    internal class CacheValue<V>
    {
        public readonly V Value;

        public CacheValue(V value)
        {
            Value = value;
        }
    }
}