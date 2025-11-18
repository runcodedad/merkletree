using MerkleTree.Hashing;
using MerkleTree.Proofs;
using MerkleTree.Cache;

namespace MerkleTree.Core;

/// <summary>
/// Represents a binary Merkle tree structure with support for non-power-of-two leaf counts.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses a domain-separated padding hash strategy for odd leaf counts,
/// ensuring a fully deterministic tree structure based on leaf ordering.
/// </para>
/// <para><strong>Tree Structure:</strong></para>
/// <list type="bullet">
/// <item><description>Binary tree with leaves at Level 0</description></item>
/// <item><description>Parent nodes computed as: Hash(left_child || right_child)</description></item>
/// <item><description>Left-to-right ordering: left child is always processed before right child</description></item>
/// </list>
/// <para><strong>Padding Strategy for Odd Leaf Counts:</strong></para>
/// <para>
/// When a level has an odd number of nodes, the unpaired node is paired with a domain-separated
/// padding hash. The padding hash is computed as Hash("MERKLE_PADDING" || unpaired_node_hash),
/// which ensures:
/// </para>
/// <list type="number">
/// <item><description>Deterministic behavior: same input always produces same tree</description></item>
/// <item><description>Security: padding cannot be confused with legitimate data</description></item>
/// <item><description>Transparency: padding nodes are clearly distinguishable from data nodes</description></item>
/// </list>
/// <para><strong>Orientation Rules:</strong></para>
/// <list type="bullet">
/// <item><description>Leaves are processed left-to-right in the order provided</description></item>
/// <item><description>When computing parent hash: Hash(left_child || right_child)</description></item>
/// <item><description>Unpaired nodes become the left child, padding hash becomes the right child</description></item>
/// </list>
/// </remarks>
public class MerkleTree : MerkleTreeBase
{
    /// <summary>
    /// Gets the root node of the Merkle tree.
    /// </summary>
    public MerkleTreeNode Root { get; }

    /// <summary>
    /// Gets the height of the Merkle tree.
    /// </summary>
    private int Height { get; }

    /// <summary>
    /// Gets the number of leaves in the Merkle tree.
    /// </summary>
    private long LeafCount { get; }

    /// <summary>
    /// Gets the leaf nodes in the tree, indexed by their position.
    /// </summary>
    private List<MerkleTreeNode> LeafNodes { get; }

    /// <summary>
    /// Gets the original leaf data values, indexed by their position.
    /// </summary>
    private List<byte[]> LeafData { get; }

    /// <summary>
    /// Gets the cache data for this tree, if caching was enabled during construction.
    /// </summary>
    private CacheData? Cache { get; }

    /// <summary>
    /// Gets the cache statistics for this tree.
    /// </summary>
    public CacheStatistics CacheStatistics { get; } = new CacheStatistics();

    /// <summary>
    /// Initializes a new instance of the <see cref="MerkleTree"/> class with the specified leaf data using SHA-256.
    /// </summary>
    /// <param name="leafData">The data for each leaf node. Must contain at least one element.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leafData"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="leafData"/> is empty.</exception>
    public MerkleTree(IEnumerable<byte[]> leafData)
        : this(leafData, new Sha256HashFunction())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MerkleTree"/> class with the specified leaf data and hash function.
    /// </summary>
    /// <param name="leafData">The data for each leaf node. Must contain at least one element.</param>
    /// <param name="hashFunction">The hash function to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leafData"/> or <paramref name="hashFunction"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="leafData"/> is empty.</exception>
    public MerkleTree(IEnumerable<byte[]> leafData, IHashFunction hashFunction)
        : this(leafData, hashFunction, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MerkleTree"/> class with the specified leaf data, hash function, and cache configuration.
    /// </summary>
    /// <param name="leafData">The data for each leaf node. Must contain at least one element.</param>
    /// <param name="hashFunction">The hash function to use.</param>
    /// <param name="cacheConfig">Optional cache configuration. If null, no cache is built.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leafData"/> or <paramref name="hashFunction"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="leafData"/> is empty.</exception>
    public MerkleTree(IEnumerable<byte[]> leafData, IHashFunction hashFunction, CacheConfiguration? cacheConfig)
        : base(hashFunction)
    {
        if (leafData == null)
            throw new ArgumentNullException(nameof(leafData));

        var leafList = leafData.ToList();
        if (leafList.Count == 0)
            throw new ArgumentException("Leaf data must contain at least one element.", nameof(leafData));

        LeafCount = leafList.Count;
        LeafData = leafList;
        var (root, height, leafNodes, cache) = BuildTree(leafList, cacheConfig);
        Root = root;
        Height = height;
        LeafNodes = leafNodes;
        Cache = cache;
    }


    /// <summary>
    /// Builds the Merkle tree from the provided leaf data.
    /// </summary>
    /// <param name="leafData">The data for each leaf node.</param>
    /// <param name="cacheConfig">Optional cache configuration.</param>
    /// <returns>A tuple containing the root node, the height of the tree, the list of leaf nodes, and optional cache data.</returns>
    private (MerkleTreeNode root, int height, List<MerkleTreeNode> leafNodes, CacheData? cache) BuildTree(List<byte[]> leafData, CacheConfiguration? cacheConfig)
    {
        // Create leaf nodes at Level 0
        var currentLevel = leafData.Select(data => new MerkleTreeNode(ComputeHash(data))).ToList();
        var leafNodes = new List<MerkleTreeNode>(currentLevel);

        int height = 0;
        Dictionary<int, List<MerkleTreeNode>>? levelCache = null;

        // Initialize cache collection if caching is enabled
        if (cacheConfig?.IsEnabled == true)
        {
            levelCache = new Dictionary<int, List<MerkleTreeNode>>();
            
            // Cache level 0 (leaves) if requested
            if (cacheConfig.StartLevel == 0)
            {
                levelCache[0] = new List<MerkleTreeNode>(currentLevel);
            }
        }

        // Build tree bottom-up until we reach the root
        while (currentLevel.Count > 1)
        {
            currentLevel = BuildNextLevel(currentLevel);
            height++;

            // Cache this level if it's within the configured range
            if (levelCache != null && cacheConfig!.StartLevel <= height && height <= cacheConfig.EndLevel)
            {
                levelCache[height] = new List<MerkleTreeNode>(currentLevel);
            }
        }

        // Build cache data if caching was enabled
        CacheData? cache = null;
        if (levelCache != null && cacheConfig != null && levelCache.Count > 0)
        {
            cache = BuildCacheData(levelCache, cacheConfig, height);
        }

        return (currentLevel[0], height, leafNodes, cache);
    }

    /// <summary>
    /// Builds the next level of the tree from the current level.
    /// </summary>
    /// <param name="currentLevel">The nodes at the current level.</param>
    /// <returns>The nodes at the next level (parent level).</returns>
    private List<MerkleTreeNode> BuildNextLevel(List<MerkleTreeNode> currentLevel)
    {
        var nextLevel = new List<MerkleTreeNode>();

        for (int i = 0; i < currentLevel.Count; i += 2)
        {
            var left = currentLevel[i];
            MerkleTreeNode right;

            // Check if we have an odd number of nodes (unpaired node at the end)
            if (i + 1 < currentLevel.Count)
            {
                // Normal case: pair with the next node
                right = currentLevel[i + 1];
            }
            else
            {
                // Odd case: create padding node using domain-separated hash
                right = CreatePaddingNode(left);
            }

            // Create parent node: Hash(left || right)
            var parentHash = ComputeParentHash(left.Hash!, right.Hash!);
            var parentNode = new MerkleTreeNode(parentHash)
            {
                Left = left,
                Right = right
            };

            nextLevel.Add(parentNode);
        }

        return nextLevel;
    }

    /// <summary>
    /// Creates a padding node for an unpaired node using domain-separated hashing.
    /// </summary>
    /// <param name="unpairedNode">The unpaired node that needs padding.</param>
    /// <returns>A padding node with a domain-separated hash.</returns>
    private MerkleTreeNode CreatePaddingNode(MerkleTreeNode unpairedNode)
    {
        // Compute padding hash using base class method
        var paddingHash = CreatePaddingHash(unpairedNode.Hash!);
        return new MerkleTreeNode(paddingHash);
    }

    /// <summary>
    /// Gets the root hash of the Merkle tree.
    /// </summary>
    /// <returns>The hash of the root node.</returns>
    public byte[] GetRootHash()
    {
        return Root.Hash ?? Array.Empty<byte>();
    }

    /// <summary>
    /// Gets the metadata for this Merkle tree.
    /// </summary>
    /// <returns>Metadata containing the root node, height, and leaf count.</returns>
    public MerkleTreeMetadata GetMetadata()
    {
        return new MerkleTreeMetadata(Root, Height, LeafCount);
    }

    /// <summary>
    /// Generates a Merkle proof for the leaf at the specified index.
    /// </summary>
    /// <param name="leafIndex">The 0-based index of the leaf to generate a proof for.</param>
    /// <returns>A <see cref="MerkleProof"/> containing all information needed to verify the leaf.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the leaf index is invalid.</exception>
    /// <remarks>
    /// The proof contains the leaf value, its index, the tree height, and all sibling hashes
    /// with orientation bits needed to recompute the root hash.
    /// </remarks>
    public MerkleProof GenerateProof(long leafIndex)
    {
        if (leafIndex < 0 || leafIndex >= LeafCount)
        {
            throw new ArgumentOutOfRangeException(nameof(leafIndex),
                $"Leaf index must be between 0 and {LeafCount - 1}.");
        }

        // For a single leaf tree, there's no path to traverse
        if (LeafCount == 1)
        {
            return new MerkleProof(
                LeafData[0],
                0,
                0,
                Array.Empty<byte[]>(),
                Array.Empty<bool>());
        }

        var siblingHashes = new List<byte[]>();
        var siblingIsRight = new List<bool>();

        // Start at the leaf level and work our way up
        var currentIndex = leafIndex;
        var currentLevelNodes = LeafNodes;

        // Traverse from leaf to root
        for (int level = 0; level < Height; level++)
        {
            // Determine if current node is on left or right
            bool isLeftChild = currentIndex % 2 == 0;
            long siblingIndex;
            bool siblingOnRight;

            if (isLeftChild)
            {
                // Current node is left child, sibling is on the right
                siblingIndex = currentIndex + 1;
                siblingOnRight = true;
            }
            else
            {
                // Current node is right child, sibling is on the left
                siblingIndex = currentIndex - 1;
                siblingOnRight = false;
            }

            // Try to get sibling hash from cache first
            byte[]? siblingHash = null;
            bool foundInCache = false;

            if (Cache != null && Cache.Levels.ContainsKey(level))
            {
                // Try to get from cache
                try
                {
                    var cachedLevel = Cache.GetLevel(level);
                    if (siblingIndex < cachedLevel.NodeCount)
                    {
                        siblingHash = cachedLevel.GetNode(siblingIndex);
                        foundInCache = true;
                        CacheStatistics.RecordHit();
                    }
                    else
                    {
                        // Sibling doesn't exist in level - compute padding hash
                        var currentHash = currentIndex < cachedLevel.NodeCount 
                            ? cachedLevel.GetNode(currentIndex)
                            : currentLevelNodes[(int)currentIndex].Hash!;
                        siblingHash = CreatePaddingHash(currentHash);
                        foundInCache = true;
                        CacheStatistics.RecordHit();
                    }
                }
                catch (KeyNotFoundException)
                {
                    // Cache miss - will compute below
                    foundInCache = false;
                    CacheStatistics.RecordMiss();
                }
            }
            else if (Cache != null)
            {
                // Cache exists but doesn't have this level
                CacheStatistics.RecordMiss();
            }

            // Fallback to recomputation if not found in cache
            if (!foundInCache)
            {
                if (siblingIndex < currentLevelNodes.Count)
                {
                    // Sibling exists in the tree
                    siblingHash = currentLevelNodes[(int)siblingIndex].Hash!;
                }
                else
                {
                    // No sibling exists - this means we have an odd number of nodes
                    // The sibling is a padding hash
                    siblingHash = CreatePaddingHash(currentLevelNodes[(int)currentIndex].Hash!);
                }
            }

            if (siblingHash == null)
            {
                throw new InvalidOperationException($"Failed to compute sibling hash at level {level}");
            }

            siblingHashes.Add(siblingHash);
            siblingIsRight.Add(siblingOnRight);

            // Move to parent level
            currentIndex = currentIndex / 2;

            // Build the next level to continue traversal
            if (level < Height - 1)
            {
                currentLevelNodes = BuildNextLevel(currentLevelNodes);
            }
        }

        return new MerkleProof(
            LeafData[(int)leafIndex],
            leafIndex,
            Height,
            siblingHashes.ToArray(),
            siblingIsRight.ToArray());
    }

    /// <summary>
    /// Builds cache data from collected level information.
    /// </summary>
    /// <param name="levelCache">Dictionary mapping level numbers to node lists.</param>
    /// <param name="cacheConfig">Cache configuration.</param>
    /// <param name="treeHeight">Height of the tree.</param>
    /// <returns>Cache data containing the specified levels.</returns>
    private CacheData BuildCacheData(Dictionary<int, List<MerkleTreeNode>> levelCache, CacheConfiguration cacheConfig, int treeHeight)
    {
        // Create metadata
        var metadata = new CacheMetadata(
            treeHeight,
            _hashFunction.Name,
            _hashFunction.HashSizeInBytes,
            cacheConfig.StartLevel,
            cacheConfig.EndLevel);

        // Build level dictionary
        var levels = new Dictionary<int, CachedLevel>();
        for (int level = cacheConfig.StartLevel; level <= cacheConfig.EndLevel; level++)
        {
            if (levelCache.TryGetValue(level, out var nodes))
            {
                var hashArray = nodes.Select(n => n.Hash!).ToArray();
                levels[level] = new CachedLevel(level, hashArray);
            }
        }

        return new CacheData(metadata, levels);
    }

    /// <summary>
    /// Saves the cache to a file.
    /// </summary>
    /// <param name="filePath">Path to the cache file.</param>
    /// <exception cref="InvalidOperationException">Thrown when no cache is available to save.</exception>
    /// <exception cref="ArgumentNullException">Thrown when filePath is null.</exception>
    public void SaveCache(string filePath)
    {
        if (filePath == null)
            throw new ArgumentNullException(nameof(filePath));
        if (Cache == null)
            throw new InvalidOperationException("No cache is available. The tree must be built with a cache configuration to save a cache.");

        var serialized = CacheSerializer.Serialize(Cache);
        File.WriteAllBytes(filePath, serialized);
    }

    /// <summary>
    /// Loads a cache from a file and returns a new MerkleTree instance that uses it.
    /// </summary>
    /// <param name="leafData">The data for each leaf node. Must contain at least one element.</param>
    /// <param name="hashFunction">The hash function to use.</param>
    /// <param name="cachePath">Path to the cache file to load.</param>
    /// <returns>A new MerkleTree instance with the loaded cache.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when leafData is empty or cache is incompatible.</exception>
    public static MerkleTree LoadWithCache(IEnumerable<byte[]> leafData, IHashFunction hashFunction, string cachePath)
    {
        if (leafData == null)
            throw new ArgumentNullException(nameof(leafData));
        if (hashFunction == null)
            throw new ArgumentNullException(nameof(hashFunction));
        if (cachePath == null)
            throw new ArgumentNullException(nameof(cachePath));

        var leafList = leafData.ToList();
        if (leafList.Count == 0)
            throw new ArgumentException("Leaf data must contain at least one element.", nameof(leafData));

        // Load cache from file
        var cacheBytes = File.ReadAllBytes(cachePath);
        var cache = CacheSerializer.Deserialize(cacheBytes);

        // Validate cache compatibility
        if (cache.Metadata.HashFunctionName != hashFunction.Name)
        {
            throw new ArgumentException(
                $"Cache hash function '{cache.Metadata.HashFunctionName}' does not match tree hash function '{hashFunction.Name}'.",
                nameof(cachePath));
        }

        if (cache.Metadata.HashSizeInBytes != hashFunction.HashSizeInBytes)
        {
            throw new ArgumentException(
                $"Cache hash size {cache.Metadata.HashSizeInBytes} does not match tree hash size {hashFunction.HashSizeInBytes}.",
                nameof(cachePath));
        }

        // Build tree with loaded cache
        return new MerkleTree(leafList, hashFunction, cache);
    }

    /// <summary>
    /// Internal constructor for creating a tree with a pre-loaded cache.
    /// </summary>
    private MerkleTree(List<byte[]> leafData, IHashFunction hashFunction, CacheData cache)
        : base(hashFunction)
    {
        LeafCount = leafData.Count;
        LeafData = leafData;
        
        // Build tree normally (cache will be used during proof generation, not construction)
        var (root, height, leafNodes, _) = BuildTree(leafData, null);
        Root = root;
        Height = height;
        LeafNodes = leafNodes;
        Cache = cache;

        // Validate cache against built tree
        if (cache.Metadata.TreeHeight != Height)
        {
            throw new ArgumentException(
                $"Cache tree height {cache.Metadata.TreeHeight} does not match built tree height {Height}.",
                nameof(cache));
        }
    }

    /// <summary>
    /// Checks if the tree has a cache available.
    /// </summary>
    /// <returns>True if a cache is available, false otherwise.</returns>
    public bool HasCache()
    {
        return Cache != null;
    }

    /// <summary>
    /// Gets information about the cache, if available.
    /// </summary>
    /// <returns>Cache metadata if cache is available, null otherwise.</returns>
    public CacheMetadata? GetCacheMetadata()
    {
        return Cache?.Metadata;
    }
}
