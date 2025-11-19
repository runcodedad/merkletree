namespace MerkleTree.Cache;

/// <summary>
/// Configuration for Merkle tree cache generation during streaming.
/// </summary>
/// <remarks>
/// This class defines caching settings for MerkleTreeStream, including the file path
/// and the number of top levels to cache. It simplifies cache configuration by combining
/// related settings into a single object.
/// </remarks>
public class CacheConfiguration
{
    /// <summary>
    /// Gets the file path where the cache should be saved.
    /// </summary>
    /// <remarks>
    /// If null or empty, caching is disabled.
    /// </remarks>
    public string? FilePath { get; }

    /// <summary>
    /// Gets the number of top levels to cache (excluding the root).
    /// </summary>
    /// <remarks>
    /// Higher values cache more levels, improving proof generation speed but using more storage.
    /// The default is 5 levels.
    /// </remarks>
    public int TopLevelsToCache { get; }

    /// <summary>
    /// Gets whether caching is enabled.
    /// </summary>
    /// <remarks>
    /// Caching is enabled when FilePath is not null/empty and TopLevelsToCache is positive.
    /// </remarks>
    public bool IsEnabled => !string.IsNullOrEmpty(FilePath) && TopLevelsToCache > 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheConfiguration"/> class.
    /// </summary>
    /// <param name="filePath">The file path where the cache should be saved.</param>
    /// <param name="topLevelsToCache">The number of top levels to cache (default 5).</param>
    /// <exception cref="ArgumentException">Thrown when topLevelsToCache is negative.</exception>
    public CacheConfiguration(string? filePath, int topLevelsToCache = 5)
    {
        if (topLevelsToCache < 0)
            throw new ArgumentException("Top levels to cache must be non-negative.", nameof(topLevelsToCache));

        FilePath = filePath;
        TopLevelsToCache = topLevelsToCache;
    }

    /// <summary>
    /// Creates a disabled cache configuration.
    /// </summary>
    /// <returns>A cache configuration that disables caching.</returns>
    public static CacheConfiguration Disabled()
    {
        return new CacheConfiguration(null, 0);
    }
}
