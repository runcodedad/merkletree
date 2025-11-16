namespace MerkleTree;

/// <summary>
/// Builds Merkle trees from streaming/chunked input without requiring the entire dataset in memory.
/// </summary>
/// <remarks>
/// <para>
/// This builder supports processing large datasets that exceed available RAM by:
/// </para>
/// <list type="bullet">
/// <item><description>Accepting leaves as fixed-size binary blobs incrementally</description></item>
/// <item><description>Building Level 0 (leaves) without loading the entire dataset</description></item>
/// <item><description>Processing upper levels incrementally by reading two children, hashing, and emitting parents</description></item>
/// <item><description>Continuing until reaching the root</description></item>
/// </list>
/// <para>
/// The builder uses the same padding strategy as <see cref="MerkleTree"/> for deterministic results.
/// Unlike <see cref="MerkleTree"/>, this class returns only metadata (root hash, height, leaf count) 
/// without building the full tree structure, making it memory-efficient for large datasets.
/// </para>
/// </remarks>
public class MerkleTreeStream : MerkleTreeBase
{

    /// <summary>
    /// Initializes a new instance of the <see cref="MerkleTreeStream"/> class using SHA-256.
    /// </summary>
    public MerkleTreeStream()
        : this(new Sha256HashFunction())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MerkleTreeStream"/> class with the specified hash function.
    /// </summary>
    /// <param name="hashFunction">The hash function to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hashFunction"/> is null.</exception>
    public MerkleTreeStream(IHashFunction hashFunction)
        : base(hashFunction)
    {
    }

    /// <summary>
    /// Builds a Merkle tree from a stream of leaf data asynchronously.
    /// </summary>
    /// <param name="leafData">An async enumerable of leaf data.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that returns the Merkle tree metadata including root hash, height, and leaf count.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leafData"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no leaves are provided.</exception>
    public async Task<MerkleTreeMetadata> BuildAsync(
        IAsyncEnumerable<byte[]> leafData,
        CancellationToken cancellationToken = default)
    {
        if (leafData == null)
            throw new ArgumentNullException(nameof(leafData));

        // Build Level 0 by streaming and hashing leaves
        var level0Hashes = new List<byte[]>();
        long leafCount = 0;

        await foreach (var leaf in leafData.WithCancellation(cancellationToken))
        {
            var hash = ComputeHash(leaf);
            level0Hashes.Add(hash);
            leafCount++;
        }

        if (leafCount == 0)
            throw new InvalidOperationException("At least one leaf is required to build a Merkle tree.");

        // Build tree bottom-up
        var currentLevel = level0Hashes;
        int height = 0;

        while (currentLevel.Count > 1)
        {
            currentLevel = BuildNextLevel(currentLevel);
            height++;
        }

        var rootHash = currentLevel[0];
        var rootNode = new MerkleTreeNode(rootHash);

        return new MerkleTreeMetadata(rootNode, height, leafCount);
    }

    /// <summary>
    /// Builds a Merkle tree from a stream of leaf data synchronously.
    /// </summary>
    /// <param name="leafData">An enumerable of leaf data.</param>
    /// <returns>The Merkle tree metadata including root hash, height, and leaf count.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leafData"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no leaves are provided.</exception>
    public MerkleTreeMetadata Build(IEnumerable<byte[]> leafData)
    {
        if (leafData == null)
            throw new ArgumentNullException(nameof(leafData));

        // Build Level 0 by streaming and hashing leaves
        var level0Hashes = new List<byte[]>();
        long leafCount = 0;

        foreach (var leaf in leafData)
        {
            var hash = ComputeHash(leaf);
            level0Hashes.Add(hash);
            leafCount++;
        }

        if (leafCount == 0)
            throw new InvalidOperationException("At least one leaf is required to build a Merkle tree.");

        // Build tree bottom-up
        var currentLevel = level0Hashes;
        int height = 0;

        while (currentLevel.Count > 1)
        {
            currentLevel = BuildNextLevel(currentLevel);
            height++;
        }

        var rootHash = currentLevel[0];
        var rootNode = new MerkleTreeNode(rootHash);

        return new MerkleTreeMetadata(rootNode, height, leafCount);
    }

    /// <summary>
    /// Builds the next level of the tree from the current level.
    /// </summary>
    private List<byte[]> BuildNextLevel(List<byte[]> currentLevel)
    {
        var nextLevel = new List<byte[]>();

        for (int i = 0; i < currentLevel.Count; i += 2)
        {
            var leftHash = currentLevel[i];
            byte[] rightHash;

            // Check if we have an odd number of nodes (unpaired node at the end)
            if (i + 1 < currentLevel.Count)
            {
                // Normal case: pair with the next node
                rightHash = currentLevel[i + 1];
            }
            else
            {
                // Odd case: create padding hash using domain-separated hashing
                rightHash = CreatePaddingHash(leftHash);
            }

            // Create parent hash: Hash(left || right)
            var parentHash = ComputeParentHash(leftHash, rightHash);
            nextLevel.Add(parentHash);
        }

        return nextLevel;
    }

    /// <summary>
    /// Generates a Merkle proof for the leaf at the specified index using optimized streaming.
    /// </summary>
    /// <param name="leafData">The original leaf data stream (must be provided again for streaming). The stream should be re-enumerable.</param>
    /// <param name="leafIndex">The 0-based index of the leaf to generate a proof for.</param>
    /// <param name="leafCount">The total number of leaves in the dataset.</param>
    /// <param name="cache">Optional cache mapping (level, index) to hash. If provided, hashes are retrieved from cache when available and stored when computed.</param>
    /// <returns>A <see cref="MerkleProof"/> containing all information needed to verify the leaf.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leafData"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the leaf index is invalid.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="leafCount"/> is less than or equal to zero.</exception>
    /// <remarks>
    /// <para>
    /// This method uses an optimized approach that only computes the sibling hashes needed for the proof path,
    /// requiring O(log n) memory instead of O(n). It streams through the data to compute only the necessary hashes
    /// at each level, making it suitable for datasets of any size (even 500GB+ files).
    /// </para>
    /// <para>
    /// If a cache is provided, sibling hashes are retrieved from cache when available, allowing proofs to be generated
    /// without re-streaming the data. The cache can be an in-memory dictionary or a disk-based storage system.
    /// </para>
    /// <para>
    /// For datasets that fit in memory, consider using <see cref="MerkleTree"/> instead,
    /// which provides O(1) proof generation after initial tree construction.
    /// </para>
    /// </remarks>
    public MerkleProof GenerateProof(
        IEnumerable<byte[]> leafData,
        long leafIndex,
        long leafCount,
        Dictionary<(int level, long index), byte[]>? cache = null)
    {
        if (leafData == null)
            throw new ArgumentNullException(nameof(leafData));

        if (leafCount <= 0)
            throw new ArgumentException("Leaf count must be greater than zero.", nameof(leafCount));

        if (leafIndex < 0 || leafIndex >= leafCount)
        {
            throw new ArgumentOutOfRangeException(nameof(leafIndex),
                $"Leaf index must be between 0 and {leafCount - 1}.");
        }

        // Calculate tree height
        int height = CalculateTreeHeight(leafCount);

        // For a single leaf tree
        if (leafCount == 1)
        {
            var singleLeaf = leafData.FirstOrDefault();
            if (singleLeaf == null)
                throw new InvalidOperationException("Leaf data is empty but leaf count was 1.");

            return new MerkleProof(
                singleLeaf,
                0,
                0,
                Array.Empty<byte[]>(),
                Array.Empty<bool>());
        }

        var siblingHashes = new List<byte[]>();
        var siblingIsRight = new List<bool>();
        
        // Store the original leaf value for the proof
        byte[]? originalLeafValue = null;

        // Track the current index as we traverse up the tree
        long currentIndex = leafIndex;

        // Process each level from leaf to root
        for (int level = 0; level < height; level++)
        {
            long levelSize = GetLevelSize(leafCount, level);
            
            // Determine sibling index and position
            long siblingIndex = (currentIndex % 2 == 0) ? currentIndex + 1 : currentIndex - 1;
            bool siblingOnRight = (currentIndex % 2 == 0);

            // Get or compute the sibling hash
            byte[] siblingHash;
            
            // Try to get from cache first
            if (cache != null && cache.TryGetValue((level, siblingIndex), out var cachedHash))
            {
                siblingHash = cachedHash;
            }
            else
            {
                // Need to compute the sibling hash
                if (level == 0)
                {
                    // Level 0: hash the leaf data
                    // We need to stream to the specific leaf index
                    byte[]? targetLeaf = null;
                    byte[]? siblingLeaf = null;
                    long count = 0;

                    foreach (var leaf in leafData)
                    {
                        if (count == currentIndex)
                        {
                            targetLeaf = leaf;
                            originalLeafValue = leaf; // Store original value
                        }
                        if (count == siblingIndex)
                        {
                            siblingLeaf = leaf;
                        }
                        
                        count++;
                        
                        // Optimization: stop once we have both leaves
                        if (targetLeaf != null && (siblingLeaf != null || siblingIndex >= levelSize))
                            break;
                    }

                    if (targetLeaf == null)
                        throw new InvalidOperationException($"Could not find leaf at index {currentIndex}.");

                    // Compute sibling hash
                    if (siblingIndex < levelSize && siblingLeaf != null)
                    {
                        siblingHash = ComputeHash(siblingLeaf);
                        
                        // Also cache the current leaf hash if cache is provided
                        if (cache != null)
                        {
                            cache[(level, currentIndex)] = ComputeHash(targetLeaf);
                            cache[(level, siblingIndex)] = siblingHash;
                        }
                    }
                    else
                    {
                        // No sibling - create padding hash
                        var currentHash = ComputeHash(targetLeaf);
                        siblingHash = CreatePaddingHash(currentHash);
                        
                        if (cache != null)
                        {
                            cache[(level, currentIndex)] = currentHash;
                        }
                    }
                }
                else
                {
                    // Higher levels: compute from previous level
                    // We need the hash at currentIndex from the previous level
                    int prevLevel = level - 1;
                    long prevLevelSize = GetLevelSize(leafCount, prevLevel);
                    
                    // Get the two children of the current node
                    long leftChildIndex = currentIndex * 2;
                    long rightChildIndex = currentIndex * 2 + 1;
                    
                    byte[] leftChildHash = GetHashAtIndex(leafData, prevLevel, leftChildIndex, leafCount, cache);
                    byte[] rightChildHash;
                    
                    if (rightChildIndex < prevLevelSize)
                    {
                        rightChildHash = GetHashAtIndex(leafData, prevLevel, rightChildIndex, leafCount, cache);
                    }
                    else
                    {
                        rightChildHash = CreatePaddingHash(leftChildHash);
                    }
                    
                    // Compute and cache current node
                    byte[] currentHash = ComputeParentHash(leftChildHash, rightChildHash);
                    if (cache != null)
                    {
                        cache[(level, currentIndex)] = currentHash;
                    }
                    
                    // Now compute sibling
                    if (siblingIndex < levelSize)
                    {
                        leftChildIndex = siblingIndex * 2;
                        rightChildIndex = siblingIndex * 2 + 1;
                        
                        leftChildHash = GetHashAtIndex(leafData, prevLevel, leftChildIndex, leafCount, cache);
                        
                        if (rightChildIndex < prevLevelSize)
                        {
                            rightChildHash = GetHashAtIndex(leafData, prevLevel, rightChildIndex, leafCount, cache);
                        }
                        else
                        {
                            rightChildHash = CreatePaddingHash(leftChildHash);
                        }
                        
                        siblingHash = ComputeParentHash(leftChildHash, rightChildHash);
                        
                        if (cache != null)
                        {
                            cache[(level, siblingIndex)] = siblingHash;
                        }
                    }
                    else
                    {
                        // No sibling - use padding
                        siblingHash = CreatePaddingHash(currentHash);
                    }
                }
            }

            siblingHashes.Add(siblingHash);
            siblingIsRight.Add(siblingOnRight);

            // Move to parent level
            currentIndex = currentIndex / 2;
        }

        if (originalLeafValue == null)
        {
            // Fallback: re-stream to get the original value if we didn't capture it
            long count = 0;
            foreach (var leaf in leafData)
            {
                if (count == leafIndex)
                {
                    originalLeafValue = leaf;
                    break;
                }
                count++;
            }
            
            if (originalLeafValue == null)
                throw new InvalidOperationException($"Could not retrieve leaf at index {leafIndex}.");
        }

        return new MerkleProof(
            originalLeafValue,
            leafIndex,
            height,
            siblingHashes.ToArray(),
            siblingIsRight.ToArray());
    }

    /// <summary>
    /// Gets the hash at a specific index in a specific level.
    /// </summary>
    private byte[] GetHashAtIndex(
        IEnumerable<byte[]> leafData,
        long level,
        long index,
        long leafCount,
        Dictionary<(int level, long index), byte[]>? cache)
    {
        // Try cache first
        if (cache != null && cache.TryGetValue(((int)level, index), out var cachedHash))
        {
            return cachedHash;
        }

        // Compute the hash
        byte[] hash;
        
        if (level == 0)
        {
            // Level 0: get from leaf data
            long count = 0;
            byte[]? targetLeaf = null;
            
            foreach (var leaf in leafData)
            {
                if (count == index)
                {
                    targetLeaf = leaf;
                    break;
                }
                count++;
            }
            
            if (targetLeaf == null)
                throw new InvalidOperationException($"Could not find leaf at index {index}.");
            
            hash = ComputeHash(targetLeaf);
        }
        else
        {
            // Higher level: compute from children
            long prevLevelSize = GetLevelSize(leafCount, (int)(level - 1));
            long leftChildIndex = index * 2;
            long rightChildIndex = index * 2 + 1;
            
            byte[] leftChildHash = GetHashAtIndex(leafData, level - 1, leftChildIndex, leafCount, cache);
            byte[] rightChildHash;
            
            if (rightChildIndex < prevLevelSize)
            {
                rightChildHash = GetHashAtIndex(leafData, level - 1, rightChildIndex, leafCount, cache);
            }
            else
            {
                rightChildHash = CreatePaddingHash(leftChildHash);
            }
            
            hash = ComputeParentHash(leftChildHash, rightChildHash);
        }
        
        // Store in cache
        if (cache != null)
        {
            cache[((int)level, index)] = hash;
        }
        
        return hash;
    }

    /// <summary>
    /// Gets the number of nodes at a specific level.
    /// </summary>
    private static long GetLevelSize(long leafCount, int level)
    {
        long size = leafCount;
        for (int i = 0; i < level; i++)
        {
            size = (size + 1) / 2; // Ceiling division
        }
        return size;
    }

    /// <summary>
    /// Calculates the height of a tree with the given number of leaves.
    /// </summary>
    private static int CalculateTreeHeight(long leafCount)
    {
        if (leafCount <= 1)
            return 0;

        int height = 0;
        long currentLevelSize = leafCount;
        
        while (currentLevelSize > 1)
        {
            currentLevelSize = (currentLevelSize + 1) / 2; // Ceiling division
            height++;
        }

        return height;
    }

    /// <summary>
    /// Generates a Merkle proof for the leaf at the specified index asynchronously.
    /// </summary>
    /// <param name="leafData">The original leaf data stream (must be provided again for streaming). The stream should be re-enumerable.</param>
    /// <param name="leafIndex">The 0-based index of the leaf to generate a proof for.</param>
    /// <param name="leafCount">The total number of leaves in the dataset.</param>
    /// <param name="cache">Optional cache mapping (level, index) to hash. If provided, hashes are retrieved from cache when available and stored when computed.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that returns a <see cref="MerkleProof"/> containing all information needed to verify the leaf.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leafData"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the leaf index is invalid.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="leafCount"/> is less than or equal to zero.</exception>
    /// <remarks>
    /// <para>
    /// This method collects the async enumerable into a list, then uses the synchronous optimized implementation.
    /// The synchronous version uses O(log n) memory by computing only sibling hashes along the proof path.
    /// </para>
    /// <para>
    /// For datasets that fit in memory, consider using <see cref="MerkleTree"/> instead,
    /// which provides better performance for proof generation.
    /// </para>
    /// </remarks>
    public async Task<MerkleProof> GenerateProofAsync(
        IAsyncEnumerable<byte[]> leafData,
        long leafIndex,
        long leafCount,
        Dictionary<(int level, long index), byte[]>? cache = null,
        CancellationToken cancellationToken = default)
    {
        if (leafData == null)
            throw new ArgumentNullException(nameof(leafData));

        if (leafCount <= 0)
            throw new ArgumentException("Leaf count must be greater than zero.", nameof(leafCount));

        // Convert to list to allow multiple enumerations
        var leafList = new List<byte[]>();
        await foreach (var leaf in leafData.WithCancellation(cancellationToken))
        {
            leafList.Add(leaf);
        }

        // Use the synchronous optimized implementation
        return GenerateProof(leafList, leafIndex, leafCount, cache);
    }

}
