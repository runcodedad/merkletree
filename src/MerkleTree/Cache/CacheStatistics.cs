namespace MerkleTree.Cache;

/// <summary>
/// Tracks cache hit and miss statistics for Merkle tree operations.
/// </summary>
/// <remarks>
/// This class provides metrics on cache effectiveness during proof generation
/// and other operations that use the cache.
/// </remarks>
public class CacheStatistics
{
    private long _hits;
    private long _misses;
    private readonly object _lock = new object();

    /// <summary>
    /// Gets the number of cache hits.
    /// </summary>
    /// <remarks>
    /// A cache hit occurs when a requested node is found in the cache,
    /// avoiding the need for recomputation.
    /// </remarks>
    public long Hits
    {
        get
        {
            lock (_lock)
            {
                return _hits;
            }
        }
    }

    /// <summary>
    /// Gets the number of cache misses.
    /// </summary>
    /// <remarks>
    /// A cache miss occurs when a requested node is not found in the cache,
    /// requiring recomputation from the original data or lower levels.
    /// </remarks>
    public long Misses
    {
        get
        {
            lock (_lock)
            {
                return _misses;
            }
        }
    }

    /// <summary>
    /// Gets the total number of cache lookups (hits + misses).
    /// </summary>
    public long TotalLookups => Hits + Misses;

    /// <summary>
    /// Gets the cache hit rate as a percentage (0.0 to 100.0).
    /// </summary>
    /// <remarks>
    /// Returns 0.0 if there have been no lookups.
    /// </remarks>
    public double HitRate
    {
        get
        {
            var total = TotalLookups;
            if (total == 0)
                return 0.0;
            return (double)Hits / total * 100.0;
        }
    }

    /// <summary>
    /// Records a cache hit.
    /// </summary>
    internal void RecordHit()
    {
        lock (_lock)
        {
            _hits++;
        }
    }

    /// <summary>
    /// Records a cache miss.
    /// </summary>
    internal void RecordMiss()
    {
        lock (_lock)
        {
            _misses++;
        }
    }

    /// <summary>
    /// Resets all statistics to zero.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _hits = 0;
            _misses = 0;
        }
    }

    /// <summary>
    /// Returns a string representation of the cache statistics.
    /// </summary>
    public override string ToString()
    {
        return $"Cache Stats: {Hits} hits, {Misses} misses, {HitRate:F2}% hit rate ({TotalLookups} total lookups)";
    }
}
