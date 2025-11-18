namespace MerkleTree.Cache;

/// <summary>
/// Metadata for a Merkle tree cache file.
/// </summary>
/// <remarks>
/// This class contains information about the tree structure and hash function
/// used to create the cache. It does not contain any actual cached node data.
/// </remarks>
public class CacheMetadata
{
    /// <summary>
    /// Gets the total height of the Merkle tree.
    /// </summary>
    /// <remarks>
    /// Height is measured as the number of levels above the leaves.
    /// A single leaf has height 0, two leaves have height 1, etc.
    /// </remarks>
    public int TreeHeight { get; }

    /// <summary>
    /// Gets the name or identifier of the hash function used.
    /// </summary>
    /// <remarks>
    /// This value is used to identify the hash algorithm (e.g., "SHA256", "SHA512", "BLAKE3").
    /// It should match the Name property of the IHashFunction that created the tree.
    /// </remarks>
    public string HashFunctionName { get; }

    /// <summary>
    /// Gets the size of hash outputs in bytes.
    /// </summary>
    /// <remarks>
    /// This should match the HashSizeInBytes property of the IHashFunction used.
    /// For example, SHA-256 has 32 bytes, SHA-512 has 64 bytes.
    /// </remarks>
    public int HashSizeInBytes { get; }

    /// <summary>
    /// Gets the starting level of cached data (inclusive).
    /// </summary>
    /// <remarks>
    /// Level 0 represents the leaves. Higher levels are closer to the root.
    /// </remarks>
    public int StartLevel { get; }

    /// <summary>
    /// Gets the ending level of cached data (inclusive).
    /// </summary>
    /// <remarks>
    /// Must be greater than or equal to StartLevel and less than TreeHeight.
    /// </remarks>
    public int EndLevel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheMetadata"/> class.
    /// </summary>
    /// <param name="treeHeight">The total height of the tree.</param>
    /// <param name="hashFunctionName">The name of the hash function used.</param>
    /// <param name="hashSizeInBytes">The size of hash outputs in bytes.</param>
    /// <param name="startLevel">The starting level of cached data.</param>
    /// <param name="endLevel">The ending level of cached data.</param>
    /// <exception cref="ArgumentNullException">Thrown when hashFunctionName is null.</exception>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    public CacheMetadata(
        int treeHeight,
        string hashFunctionName,
        int hashSizeInBytes,
        int startLevel,
        int endLevel)
    {
        if (hashFunctionName == null)
            throw new ArgumentNullException(nameof(hashFunctionName));
        if (string.IsNullOrWhiteSpace(hashFunctionName))
            throw new ArgumentException("Hash function name cannot be empty or whitespace.", nameof(hashFunctionName));
        if (treeHeight < 0)
            throw new ArgumentException("Tree height must be non-negative.", nameof(treeHeight));
        if (hashSizeInBytes <= 0)
            throw new ArgumentException("Hash size must be positive.", nameof(hashSizeInBytes));
        if (startLevel < 0)
            throw new ArgumentException("Start level must be non-negative.", nameof(startLevel));
        if (endLevel < startLevel)
            throw new ArgumentException("End level must be greater than or equal to start level.", nameof(endLevel));
        if (endLevel >= treeHeight && treeHeight > 0)
            throw new ArgumentException("End level must be less than tree height.", nameof(endLevel));

        TreeHeight = treeHeight;
        HashFunctionName = hashFunctionName;
        HashSizeInBytes = hashSizeInBytes;
        StartLevel = startLevel;
        EndLevel = endLevel;
    }
}
