namespace MerkleTree;

/// <summary>
/// Contains metadata about a constructed Merkle tree.
/// </summary>
/// <remarks>
/// This class provides information about the tree structure including its height,
/// the number of leaves, and the root hash.
/// </remarks>
public class MerkleTreeMetadata
{
    /// <summary>
    /// Gets the root hash of the Merkle tree.
    /// </summary>
    public byte[] RootHash { get; }
    
    /// <summary>
    /// Gets the height of the Merkle tree.
    /// </summary>
    /// <remarks>
    /// The height is the number of levels in the tree, where leaves are at level 0.
    /// A single leaf has height 0, two leaves have height 1, etc.
    /// </remarks>
    public int Height { get; }
    
    /// <summary>
    /// Gets the number of leaves in the Merkle tree.
    /// </summary>
    public long LeafCount { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="MerkleTreeMetadata"/> class.
    /// </summary>
    /// <param name="rootHash">The root hash of the tree.</param>
    /// <param name="height">The height of the tree.</param>
    /// <param name="leafCount">The number of leaves in the tree.</param>
    public MerkleTreeMetadata(byte[] rootHash, int height, long leafCount)
    {
        RootHash = rootHash ?? throw new ArgumentNullException(nameof(rootHash));
        Height = height;
        LeafCount = leafCount;
    }
}
