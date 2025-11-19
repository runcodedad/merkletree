namespace MerkleTree.Cache;

/// <summary>
/// Combines cache data with statistics tracking for Merkle tree operations.
/// </summary>
/// <remarks>
/// This class wraps CacheData and provides automatic statistics tracking
/// when cache lookups are performed during proof generation.
/// </remarks>
public class CacheWithStats
{
    /// <summary>
    /// Gets the underlying cache data.
    /// </summary>
    public CacheData Data { get; }

    /// <summary>
    /// Gets the statistics tracker for cache operations.
    /// </summary>
    public CacheStatistics Statistics { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheWithStats"/> class.
    /// </summary>
    /// <param name="data">The cache data to wrap.</param>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    public CacheWithStats(CacheData data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Statistics = new CacheStatistics();
    }

    /// <summary>
    /// Attempts to get a cached node value and records the lookup result.
    /// </summary>
    /// <param name="level">The level of the node.</param>
    /// <param name="index">The index of the node within the level.</param>
    /// <param name="value">The cached value if found.</param>
    /// <returns>True if the node was found in cache; otherwise, false.</returns>
    /// <remarks>
    /// This method automatically tracks cache hits and misses in the Statistics property.
    /// </remarks>
    public bool TryGetNode(int level, long index, out byte[] value)
    {
        // Check if level exists in cache
        if (Data.Levels.TryGetValue(level, out var cachedLevel))
        {
            // Check if index is valid for this level
            if (index >= 0 && index < cachedLevel.NodeCount)
            {
                value = cachedLevel.GetNode(index);
                Statistics.RecordHit();
                return true;
            }
        }

        value = null!;
        Statistics.RecordMiss();
        return false;
    }
}
