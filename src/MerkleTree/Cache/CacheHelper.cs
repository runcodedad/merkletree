namespace MerkleTree.Cache;

/// <summary>
/// Helper class for Merkle tree cache operations.
/// </summary>
/// <remarks>
/// Provides utility methods for loading and converting cache data without coupling
/// to specific tree implementations.
/// </remarks>
public static class CacheHelper
{
    /// <summary>
    /// Loads cache data from a file.
    /// </summary>
    /// <param name="filePath">Path to the cache file.</param>
    /// <returns>The loaded cache data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePath is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the cache file doesn't exist.</exception>
    /// <remarks>
    /// The loaded cache can be used with GenerateProofAsync to accelerate proof generation
    /// by avoiding recomputation of cached nodes.
    /// </remarks>
    public static CacheData LoadCache(string filePath)
    {
        if (filePath == null)
            throw new ArgumentNullException(nameof(filePath));

        var cacheBytes = File.ReadAllBytes(filePath);
        return CacheSerializer.Deserialize(cacheBytes);
    }

    /// <summary>
    /// Converts CacheData to a dictionary format suitable for GenerateProofAsync.
    /// </summary>
    /// <param name="cache">The cache data to convert.</param>
    /// <returns>A dictionary mapping (level, index) to hash values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when cache is null.</exception>
    /// <remarks>
    /// This helper method converts the structured CacheData format into the dictionary format
    /// expected by GenerateProofAsync methods.
    /// </remarks>
    public static Dictionary<(int level, long index), byte[]> CacheToDictionary(CacheData cache)
    {
        if (cache == null)
            throw new ArgumentNullException(nameof(cache));

        var dictionary = new Dictionary<(int level, long index), byte[]>();

        foreach (var kvp in cache.Levels)
        {
            int level = kvp.Key;
            var cachedLevel = kvp.Value;

            for (long i = 0; i < cachedLevel.NodeCount; i++)
            {
                dictionary[(level, i)] = cachedLevel.GetNode(i);
            }
        }

        return dictionary;
    }
}
