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
        
        // Process leaves and build Level 0
        var (level0Hashes, leafCount) = await ProcessLeavesAsync(leafData, cancellationToken);
        
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
        
        return new MerkleTreeMetadata(rootHash, height, leafCount);
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
        
        // Process leaves and build Level 0
        var (level0Hashes, leafCount) = ProcessLeaves(leafData);
        
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
        
        return new MerkleTreeMetadata(rootHash, height, leafCount);
    }
    
    /// <summary>
    /// Builds a Merkle tree from a stream of leaf data in batches.
    /// </summary>
    /// <param name="leafData">An enumerable of leaf data.</param>
    /// <param name="batchSize">The number of leaves to process in each batch.</param>
    /// <returns>The Merkle tree metadata including root hash, height, and leaf count.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leafData"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="batchSize"/> is less than 1.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no leaves are provided.</exception>
    public MerkleTreeMetadata BuildInBatches(IEnumerable<byte[]> leafData, int batchSize)
    {
        if (leafData == null)
            throw new ArgumentNullException(nameof(leafData));
        if (batchSize < 1)
            throw new ArgumentException("Batch size must be at least 1.", nameof(batchSize));
        
        // Process leaves in batches and build Level 0
        var (level0Hashes, leafCount) = ProcessLeavesInBatches(leafData, batchSize);
        
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
        
        return new MerkleTreeMetadata(rootHash, height, leafCount);
    }
    
    /// <summary>
    /// Processes leaves asynchronously and computes their hashes.
    /// </summary>
    private async Task<(List<byte[]> hashes, long leafCount)> ProcessLeavesAsync(
        IAsyncEnumerable<byte[]> leafData,
        CancellationToken cancellationToken)
    {
        var hashes = new List<byte[]>();
        long count = 0;
        
        await foreach (var leaf in leafData.WithCancellation(cancellationToken))
        {
            var hash = ComputeHash(leaf);
            hashes.Add(hash);
            count++;
        }
        
        return (hashes, count);
    }
    
    /// <summary>
    /// Processes leaves synchronously and computes their hashes.
    /// </summary>
    private (List<byte[]> hashes, long leafCount) ProcessLeaves(IEnumerable<byte[]> leafData)
    {
        var hashes = new List<byte[]>();
        long count = 0;
        
        foreach (var leaf in leafData)
        {
            var hash = ComputeHash(leaf);
            hashes.Add(hash);
            count++;
        }
        
        return (hashes, count);
    }
    
    /// <summary>
    /// Processes leaves in batches to minimize memory usage.
    /// </summary>
    private (List<byte[]> hashes, long leafCount) ProcessLeavesInBatches(
        IEnumerable<byte[]> leafData,
        int batchSize)
    {
        var hashes = new List<byte[]>();
        long count = 0;
        var batch = new List<byte[]>(batchSize);
        
        foreach (var leaf in leafData)
        {
            batch.Add(leaf);
            count++;
            
            if (batch.Count >= batchSize)
            {
                // Process batch
                foreach (var leafInBatch in batch)
                {
                    var hash = ComputeHash(leafInBatch);
                    hashes.Add(hash);
                }
                batch.Clear();
            }
        }
        
        // Process remaining items in the last batch
        foreach (var leafInBatch in batch)
        {
            var hash = ComputeHash(leafInBatch);
            hashes.Add(hash);
        }
        
        return (hashes, count);
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
    
}
