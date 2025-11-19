namespace MerkleTree.Cache;

/// <summary>
/// Helper class for Merkle tree cache operations.
/// </summary>
/// <remarks>
/// Provides utility methods for loading cache data without coupling
/// to specific tree implementations.
/// </remarks>
public static class CacheHelper
{
    /// <summary>
    /// Loads cache data from a file with statistics tracking.
    /// </summary>
    /// <param name="filePath">Path to the cache file.</param>
    /// <returns>A CacheWithStats instance containing the loaded cache data and statistics tracker.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePath is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the cache file doesn't exist.</exception>
    /// <remarks>
    /// The loaded cache can be used with GenerateProofAsync to accelerate proof generation
    /// by avoiding recomputation of cached nodes. Statistics will be tracked automatically.
    /// </remarks>
    public static CacheWithStats LoadCache(string filePath)
    {
        if (filePath == null)
            throw new ArgumentNullException(nameof(filePath));

        var cacheBytes = File.ReadAllBytes(filePath);
        var cacheData = CacheSerializer.Deserialize(cacheBytes);
        return new CacheWithStats(cacheData);
    }
}
