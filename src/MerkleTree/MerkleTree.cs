namespace MerkleTree;

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
        : base(hashFunction)
    {
        if (leafData == null)
            throw new ArgumentNullException(nameof(leafData));
        
        var leafList = leafData.ToList();
        if (leafList.Count == 0)
            throw new ArgumentException("Leaf data must contain at least one element.", nameof(leafData));
        
        LeafCount = leafList.Count;
        var (root, height) = BuildTree(leafList);
        Root = root;
        Height = height;
    }

    
    /// <summary>
    /// Builds the Merkle tree from the provided leaf data.
    /// </summary>
    /// <param name="leafData">The data for each leaf node.</param>
    /// <returns>A tuple containing the root node and the height of the tree.</returns>
    private (MerkleTreeNode root, int height) BuildTree(List<byte[]> leafData)
    {
        // Create leaf nodes at Level 0
        var currentLevel = leafData.Select(data => new MerkleTreeNode(ComputeHash(data))).ToList();
        
        int height = 0;
        
        // Build tree bottom-up until we reach the root
        while (currentLevel.Count > 1)
        {
            currentLevel = BuildNextLevel(currentLevel);
            height++;
        }
        
        return (currentLevel[0], height);
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
}
