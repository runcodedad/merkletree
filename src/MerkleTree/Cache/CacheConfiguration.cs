namespace MerkleTree.Cache;

/// <summary>
/// Configuration for Merkle tree cache generation.
/// </summary>
/// <remarks>
/// This class defines which levels of the Merkle tree should be cached to accelerate
/// proof generation for large datasets. Caching upper levels reduces the need for
/// recomputation when generating proofs.
/// </remarks>
public class CacheConfiguration
{
    /// <summary>
    /// Gets the starting level to cache (inclusive).
    /// </summary>
    /// <remarks>
    /// Level 0 represents the leaves. Higher levels are closer to the root.
    /// Typically, caching starts from a level above the leaves to avoid caching
    /// the entire dataset.
    /// </remarks>
    public int StartLevel { get; }

    /// <summary>
    /// Gets the ending level to cache (inclusive).
    /// </summary>
    /// <remarks>
    /// Must be greater than or equal to StartLevel and less than the tree height.
    /// The root level itself is typically not cached as it's always available.
    /// </remarks>
    public int EndLevel { get; }

    /// <summary>
    /// Gets whether caching is enabled.
    /// </summary>
    public bool IsEnabled => StartLevel >= 0 && EndLevel >= StartLevel;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheConfiguration"/> class.
    /// </summary>
    /// <param name="startLevel">The starting level to cache.</param>
    /// <param name="endLevel">The ending level to cache.</param>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    public CacheConfiguration(int startLevel, int endLevel)
    {
        if (startLevel < 0)
            throw new ArgumentException("Start level must be non-negative.", nameof(startLevel));
        if (endLevel < startLevel)
            throw new ArgumentException("End level must be greater than or equal to start level.", nameof(endLevel));

        StartLevel = startLevel;
        EndLevel = endLevel;
    }

    /// <summary>
    /// Creates a cache configuration for the top N levels of a tree.
    /// </summary>
    /// <param name="treeHeight">The height of the tree.</param>
    /// <param name="topLevels">The number of top levels to cache.</param>
    /// <returns>A cache configuration for the specified top levels.</returns>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    /// <remarks>
    /// This is a convenience method for creating a cache that stores the topmost levels
    /// of the tree, which are most frequently accessed during proof generation.
    /// </remarks>
    public static CacheConfiguration ForTopLevels(int treeHeight, int topLevels)
    {
        if (treeHeight < 0)
            throw new ArgumentException("Tree height must be non-negative.", nameof(treeHeight));
        if (topLevels <= 0)
            throw new ArgumentException("Top levels must be positive.", nameof(topLevels));
        if (topLevels > treeHeight)
            throw new ArgumentException($"Top levels ({topLevels}) cannot exceed tree height ({treeHeight}).", nameof(topLevels));

        // Calculate start level: if we want top 3 levels of a height-5 tree,
        // we cache levels 2, 3, 4 (height-topLevels to height-1)
        int startLevel = Math.Max(0, treeHeight - topLevels);
        int endLevel = treeHeight - 1;

        return new CacheConfiguration(startLevel, endLevel);
    }

    /// <summary>
    /// Creates a disabled cache configuration.
    /// </summary>
    /// <returns>A cache configuration that disables caching.</returns>
    public static CacheConfiguration Disabled()
    {
        return new CacheConfiguration(0, -1);
    }
}
