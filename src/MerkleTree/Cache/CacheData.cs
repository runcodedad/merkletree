namespace MerkleTree.Cache;

/// <summary>
/// Represents all cache data for a Merkle tree, including metadata and cached levels.
/// </summary>
/// <remarks>
/// This class aggregates the complete cache information: metadata about the tree
/// and hash function, plus the actual cached node data organized by level.
/// </remarks>
public class CacheData
{
    /// <summary>
    /// Gets the cache metadata.
    /// </summary>
    public CacheMetadata Metadata { get; }

    /// <summary>
    /// Gets the cached levels, indexed by level number.
    /// </summary>
    /// <remarks>
    /// The dictionary keys are level numbers (StartLevel through EndLevel from metadata).
    /// Each value is a CachedLevel containing all nodes at that level.
    /// </remarks>
    public IReadOnlyDictionary<int, CachedLevel> Levels { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheData"/> class.
    /// </summary>
    /// <param name="metadata">The cache metadata.</param>
    /// <param name="levels">The cached levels.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when levels don't match metadata.</exception>
    public CacheData(CacheMetadata metadata, IReadOnlyDictionary<int, CachedLevel> levels)
    {
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));
        if (levels == null)
            throw new ArgumentNullException(nameof(levels));

        // Validate that all expected levels are present
        for (int level = metadata.StartLevel; level <= metadata.EndLevel; level++)
        {
            if (!levels.ContainsKey(level))
                throw new ArgumentException($"Missing level {level} in cache data. Expected levels {metadata.StartLevel} to {metadata.EndLevel}.", nameof(levels));
        }

        // Validate that all levels have the correct hash size
        foreach (var kvp in levels)
        {
            var level = kvp.Value;
            foreach (var node in level.Nodes)
            {
                if (node.Length != metadata.HashSizeInBytes)
                    throw new ArgumentException(
                        $"Node at level {level.Level} has hash size {node.Length}, expected {metadata.HashSizeInBytes}.",
                        nameof(levels));
            }
        }

        Metadata = metadata;
        Levels = levels;
    }

    /// <summary>
    /// Gets the cached level at the specified level number.
    /// </summary>
    /// <param name="level">The level number.</param>
    /// <returns>The cached level.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the level is not in the cache.</exception>
    public CachedLevel GetLevel(int level)
    {
        if (!Levels.ContainsKey(level))
            throw new KeyNotFoundException($"Level {level} is not in the cache. Available levels: {Metadata.StartLevel} to {Metadata.EndLevel}.");
        
        return Levels[level];
    }

    /// <summary>
    /// Gets the hash value of a specific node.
    /// </summary>
    /// <param name="level">The level number.</param>
    /// <param name="index">The node index at that level.</param>
    /// <returns>The hash value of the node.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the level is not in the cache.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
    public byte[] GetNode(int level, long index)
    {
        return GetLevel(level).GetNode(index);
    }
}
