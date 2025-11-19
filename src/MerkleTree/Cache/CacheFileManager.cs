namespace MerkleTree.Cache;

/// <summary>
/// Manages cache file operations for Merkle trees.
/// </summary>
/// <remarks>
/// Provides utility methods for loading and building cache files
/// without coupling to specific tree implementations.
/// </remarks>
public static class CacheFileManager
{
    /// <summary>
    /// Loads cache data from a file.
    /// </summary>
    /// <param name="filePath">Path to the cache file.</param>
    /// <returns>A CacheData instance containing the loaded cache data with integrated statistics tracking.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePath is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the cache file doesn't exist.</exception>
    /// <remarks>
    /// The loaded cache can be used with GenerateProofAsync to accelerate proof generation
    /// by avoiding recomputation of cached nodes. Statistics will be tracked automatically
    /// when using TryGetNode.
    /// </remarks>
    public static CacheData LoadCache(string filePath)
    {
        if (filePath == null)
            throw new ArgumentNullException(nameof(filePath));

        var cacheBytes = File.ReadAllBytes(filePath);
        return CacheSerializer.Deserialize(cacheBytes);
    }

    /// <summary>
    /// Builds a cache file from level files on disk.
    /// </summary>
    /// <param name="allLevels">List of all level files with their metadata.</param>
    /// <param name="startLevel">Starting level to include in cache.</param>
    /// <param name="endLevel">Ending level to include in cache.</param>
    /// <param name="treeHeight">Total height of the Merkle tree.</param>
    /// <param name="hashFunctionName">Name of the hash function used.</param>
    /// <param name="hashSizeInBytes">Size of hash values in bytes.</param>
    /// <param name="cacheFilePath">Path where cache file should be written.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes when cache file is built.</returns>
    /// <remarks>
    /// This method reads the selected level files from disk, packages them into a cache
    /// data structure, serializes it, and writes it to the specified cache file path.
    /// </remarks>
    public static async Task BuildCacheFileAsync(
        List<(int level, string filePath, long nodeCount)> allLevels,
        int startLevel,
        int endLevel,
        int treeHeight,
        string hashFunctionName,
        int hashSizeInBytes,
        string cacheFilePath,
        CancellationToken cancellationToken)
    {
        // Read the selected levels from disk and build cache
        var levels = new Dictionary<int, CachedLevel>();

        foreach (var (level, filePath, nodeCount) in allLevels)
        {
            if (level >= startLevel && level <= endLevel)
            {
                // Read all nodes from this level file
                var nodes = new List<byte[]>();
                
                await using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
                using (var reader = new BinaryReader(fileStream))
                {
                    for (long i = 0; i < nodeCount; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int hashLength = reader.ReadInt32();
                        byte[] hash = reader.ReadBytes(hashLength);
                        nodes.Add(hash);
                    }
                }

                levels[level] = new CachedLevel(level, nodes.ToArray());
            }
        }

        if (levels.Count > 0)
        {
            var cacheMetadata = new CacheMetadata(
                treeHeight,
                hashFunctionName,
                hashSizeInBytes,
                startLevel,
                endLevel);

            var cacheData = new CacheData(cacheMetadata, levels);
            var serialized = CacheSerializer.Serialize(cacheData);
            await File.WriteAllBytesAsync(cacheFilePath, serialized, cancellationToken);
        }
    }
}
