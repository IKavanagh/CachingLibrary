namespace CachingLibrary;

public class LeastRecentlyUsedCache<TKey, TValue> where TKey : notnull
{
    public event EventHandler<CacheEventArgs>? ItemEvicted;

    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;

    private readonly LinkedList<CacheItem> _leastRecentlyUsed = new();

    private readonly ReaderWriterLockSlim _lock = new();

    private int _capacity;

    public int Capacity
    {
        get => _capacity;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentException("Capacity must be greater than 0", nameof(Capacity));
            }

            if (value == _capacity)
            {
                return;
            }

            if (_capacity > 0 && value < _capacity)
            {
                _lock.EnterWriteLock();
                try
                {
                    while (_cache.Count > value)
                    {
                        EvictLeastRecentlyUsedItem();
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            if (value > _capacity)
            {
                _lock.EnterWriteLock();
                try
                {
                    _cache.EnsureCapacity(value);
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            _capacity = value;
        }
    }

    public int Count => _cache.Count;

    public LeastRecentlyUsedCache(int capacity = 5)
    {
        if (capacity <= 0)
        {
            throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));
        }

        _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);

        Capacity = capacity;
    }

    ~LeastRecentlyUsedCache()
    {
        _lock.Dispose();
    }

    /// <summary>
    /// Adds data to the cache. If the key already exists, the value is updated and the item is
    /// moved to the front of the list.
    /// </summary>
    /// <param name="key">Key to add data for</param>
    /// <param name="value">Data to add to catch</param>
    public void Add(TKey key, TValue value)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_cache.TryGetValue(key, out LinkedListNode<CacheItem>? cacheNode))
            {
                MoveNodeToFirst(cacheNode);

                cacheNode.Value.Value = value;

                return;
            }

            // key doesn't exist
            if (_cache.Count >= Capacity)
            {
                EvictLeastRecentlyUsedItem();
            }

            CacheItem item = new(key, value);
            LinkedListNode<CacheItem> newNode = new(item);

            _cache.Add(key, newNode);
            _leastRecentlyUsed.AddFirst(newNode);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets data from the cache. If the key doesn't exist, the default value is returned.
    /// </summary>
    /// <param name="key">The key to get data for</param>
    /// <returns>The data corresponding to <paramref name="key" /></returns>
    public TValue? Get(TKey key)
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (!_cache.TryGetValue(key, out LinkedListNode<CacheItem>? cacheNode))
            {
                return default;
            }

            _lock.EnterWriteLock();
            try
            {
                MoveNodeToFirst(cacheNode);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            return cacheNode.Value.Value;
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    private void MoveNodeToFirst(LinkedListNode<CacheItem> node)
    {
        _leastRecentlyUsed.Remove(node);
        _leastRecentlyUsed.AddFirst(node);
    }

    private void EvictLeastRecentlyUsedItem()
    {
        CacheItem evictedItem = _leastRecentlyUsed.Last!.Value; // last is never null because capacity must be greater than 0

        _cache.Remove(evictedItem.Key);
        _leastRecentlyUsed.RemoveLast();

        ItemEvicted?.Invoke(this, new CacheEventArgs(evictedItem.Key, evictedItem.Value));
    }

    private class CacheItem
    {
        public TKey Key { get; init; }

        public TValue? Value { get; set; }

        public CacheItem(TKey key, TValue? value)
        {
            Key = key;
            Value = value;
        }
    }
}

public class CacheEventArgs : EventArgs
{
    public object Key { get; }

    public object? Value { get; }

    public CacheEventArgs(object key, object? value)
    {
        Key = key;
        Value = value;
    }
}
