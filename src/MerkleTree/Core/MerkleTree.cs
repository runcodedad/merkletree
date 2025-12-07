using MerkleTree.Hashing;
using MerkleTree.Proofs;

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
        LeafData = leafList;
        var (root, height, leafNodes) = BuildTree(leafList);
        Root = root;
        Height = height;
        LeafNodes = leafNodes;
    }


    /// <summary>
    /// Builds the Merkle tree from the provided leaf data.
    /// </summary>
    /// <param name="leafData">The data for each leaf node.</param>
    /// <returns>A tuple containing the root node, the height of the tree, and the list of leaf nodes.</returns>
    private (MerkleTreeNode root, int height, List<MerkleTreeNode> leafNodes) BuildTree(List<byte[]> leafData)
    {
        // Create leaf nodes at Level 0 with domain-separated leaf hashing
        var currentLevel = leafData.Select(data => new MerkleTreeNode(ComputeLeafHash(data))).ToList();
        var leafNodes = new List<MerkleTreeNode>(currentLevel);

        int height = 0;

        // Build tree bottom-up until we reach the root
        while (currentLevel.Count > 1)
        {
            currentLevel = BuildNextLevel(currentLevel);
            height++;
        }

        return (currentLevel[0], height, leafNodes);
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
    /// <returns>Metadata containing the root node, height, leaf count, and hash algorithm name.</returns>
    public MerkleTreeMetadata GetMetadata()
    {
        return new MerkleTreeMetadata(Root, Height, LeafCount, _hashFunction.Name);
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
                leafIndex,
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

            // Get the sibling hash
            byte[] siblingHash;
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
}
