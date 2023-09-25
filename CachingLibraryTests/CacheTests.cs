using CachingLibrary;
using NUnit.Framework;

namespace CachingLibraryTests;

[TestFixture]
public class CacheTests
{
    [Test]
    [TestCase(0)]
    [TestCase(-1)]
    public void TestNonPositiveCapacity(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LeastRecentlyUsedCache<string, int>(capacity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LeastRecentlyUsedCache<string, int>().Capacity = capacity);
    }

    [Test]
    [TestCase(2, 1)]
    [TestCase(2, 2)]
    [TestCase(2, 3)]
    public void TestCapacityLimit(int capacity, int itemsToAdd)
    {
        var cache = new LeastRecentlyUsedCache<string, int>(capacity);

        for (int i = 0; i < itemsToAdd; i++)
        {
            cache.Add($"key{i}", i);
        }

        Assert.That(cache.Count, Is.LessThanOrEqualTo(cache.Capacity));
    }

    [Test]
    [TestCase("key", 1)]
    public void TestAddAndGet(string key, int value)
    {
        var cache = new LeastRecentlyUsedCache<string, int>();
        cache.Add(key, value);

        int result = cache.Get(key);
        Assert.That(result, Is.EqualTo(value));
    }

    [Test]
    public void TestLeastRecentlyUsedEviction()
    {
        var cache = new LeastRecentlyUsedCache<string, string?>(2);
        cache.Add("key0", "value0");
        cache.Add("key1", "value1");
        cache.Get("key0");
        cache.Add("key2", "value2"); // key1 should be evicted

        Assert.Multiple(() =>
        {
            Assert.That(cache.Get("key1"), Is.Null);
            Assert.That(cache.Get("key0"), Is.EqualTo("value0"));
            Assert.That(cache.Get("key2"), Is.EqualTo("value2"));
        });
    }

    [Test]
    public void TestItemEvictedEvent()
    {
        var cache = new LeastRecentlyUsedCache<string, string?>(1);
        cache.ItemEvicted += (sender, args) =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(args.Key, Is.EqualTo("key0"));
                Assert.That(args.Value, Is.EqualTo("value0"));
            });
        };

        cache.Add("key0", "value0");
        cache.Add("key1", "value1");
    }

    [Test]
    public void TestGetThreadSafety()
    {
        const int capacity = 10;

        var cache = new LeastRecentlyUsedCache<int, int>(capacity);
        for (int i = 0; i < 10; i++)
        {
            cache.Add(i, i);
        }

        var tasks = new List<Task>
        {
            Task.Run(GetValues),
            Task.Run(GetValues),
            Task.Run(GetValues)
        };

        void GetValues()
        {
            for (int i = 0; i < capacity; i++)
            {
                cache.Get(i);
            }
        }
    }

    [Test]
    public void TestAddThreadSafety()
    {
        const int capacity = 10;

        var cache = new LeastRecentlyUsedCache<int, int>(capacity);
        AddValues();

        var tasks = new List<Task>
        {
            Task.Run(GetValues),
            Task.Run(GetValues),
            Task.Run(AddValues)
        };

        void GetValues()
        {
            for (int i = 0; i < capacity; i++)
            {
                cache.Get(i);
            }
        }

        void AddValues()
        {
            for (int i = 0; i < capacity; i++)
            {
                cache.Add(i, i);
            }
        }
    }

    [Test]
    [TestCase(1, 2)]
    [TestCase(2, 2)]
    [TestCase(2, 10)]
    public void TestIncreasingCapacity(int initialCapacity, int increasedCapacity)
    {
        var cache = new LeastRecentlyUsedCache<int, int>(initialCapacity);
        for (int i = 0; i < initialCapacity + 1; i++)
        {
            cache.Add(i, i);
        }

        Assert.That(cache.Count, Is.EqualTo(initialCapacity));

        cache.Capacity = increasedCapacity;
        for (int i = 0; i < increasedCapacity + 1; i++)
        {
            cache.Add(i, i);
        }

        Assert.That(cache.Count, Is.EqualTo(increasedCapacity));
    }

    [Test]
    [TestCase(2, 1)]
    [TestCase(10, 4)]
    public void TestDecreasingCapacity(int initialCapacity, int decreasedCapacity)
    {
        var cache = new LeastRecentlyUsedCache<int, int>(initialCapacity);
        for (int i = 0; i < initialCapacity + 1; i++)
        {
            cache.Add(i, i);
        }

        Assert.That(cache.Count, Is.EqualTo(initialCapacity));

        cache.Capacity = decreasedCapacity;
        Assert.That(cache.Count, Is.EqualTo(decreasedCapacity));
    }
}
