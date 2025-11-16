namespace MerkleTree;

/// <summary>
/// Represents a Merkle proof that can be used to verify a leaf's inclusion in a Merkle tree.
/// </summary>
/// <remarks>
/// A Merkle proof contains all the information needed to recompute the root hash from a leaf,
/// including sibling hashes at each level and orientation bits indicating whether each sibling
/// is on the left or right.
/// </remarks>
public class MerkleProof
{
    /// <summary>
    /// Gets the value of the leaf being proven.
    /// </summary>
    public byte[] LeafValue { get; }

    /// <summary>
    /// Gets the index of the leaf in the tree (0-based).
    /// </summary>
    public long LeafIndex { get; }

    /// <summary>
    /// Gets the total height of the tree.
    /// </summary>
    /// <remarks>
    /// Height is measured as the number of levels above the leaves.
    /// A single leaf has height 0, two leaves have height 1, etc.
    /// </remarks>
    public int TreeHeight { get; }

    /// <summary>
    /// Gets the sibling hashes needed to recompute the root, ordered from leaf to root.
    /// </summary>
    /// <remarks>
    /// Each element in this array is the sibling hash at the corresponding level.
    /// The array has length equal to TreeHeight.
    /// </remarks>
    public byte[][] SiblingHashes { get; }

    /// <summary>
    /// Gets the orientation bits indicating whether each sibling is on the left (false) or right (true).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each element indicates the position of the corresponding sibling hash:
    /// - false: sibling is on the left, current node is on the right
    /// - true: sibling is on the right, current node is on the left
    /// </para>
    /// <para>
    /// When computing parent hash: Hash(left || right)
    /// - If orientation is false: Hash(sibling || current)
    /// - If orientation is true: Hash(current || sibling)
    /// </para>
    /// <para>
    /// The array has length equal to TreeHeight.
    /// </para>
    /// </remarks>
    public bool[] SiblingIsRight { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MerkleProof"/> class.
    /// </summary>
    /// <param name="leafValue">The value of the leaf being proven.</param>
    /// <param name="leafIndex">The index of the leaf in the tree.</param>
    /// <param name="treeHeight">The total height of the tree.</param>
    /// <param name="siblingHashes">The sibling hashes at each level from leaf to root.</param>
    /// <param name="siblingIsRight">The orientation bits for each sibling hash.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when arrays have mismatched lengths or invalid values.</exception>
    public MerkleProof(
        byte[] leafValue,
        long leafIndex,
        int treeHeight,
        byte[][] siblingHashes,
        bool[] siblingIsRight)
    {
        if (leafValue == null)
            throw new ArgumentNullException(nameof(leafValue));
        if (siblingHashes == null)
            throw new ArgumentNullException(nameof(siblingHashes));
        if (siblingIsRight == null)
            throw new ArgumentNullException(nameof(siblingIsRight));
        if (leafIndex < 0)
            throw new ArgumentException("Leaf index must be non-negative.", nameof(leafIndex));
        if (treeHeight < 0)
            throw new ArgumentException("Tree height must be non-negative.", nameof(treeHeight));
        if (siblingHashes.Length != treeHeight)
            throw new ArgumentException($"Expected {treeHeight} sibling hashes, got {siblingHashes.Length}.", nameof(siblingHashes));
        if (siblingIsRight.Length != treeHeight)
            throw new ArgumentException($"Expected {treeHeight} orientation bits, got {siblingIsRight.Length}.", nameof(siblingIsRight));

        LeafValue = leafValue;
        LeafIndex = leafIndex;
        TreeHeight = treeHeight;
        SiblingHashes = siblingHashes;
        SiblingIsRight = siblingIsRight;
    }

    /// <summary>
    /// Verifies this proof against a given root hash using the specified hash function.
    /// </summary>
    /// <param name="expectedRootHash">The expected root hash to verify against.</param>
    /// <param name="hashFunction">The hash function to use for verification.</param>
    /// <returns>True if the proof is valid and produces the expected root hash; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public bool Verify(byte[] expectedRootHash, IHashFunction hashFunction)
    {
        if (expectedRootHash == null)
            throw new ArgumentNullException(nameof(expectedRootHash));
        if (hashFunction == null)
            throw new ArgumentNullException(nameof(hashFunction));

        // Start by hashing the leaf value
        var currentHash = hashFunction.ComputeHash(LeafValue);

        // Traverse from leaf to root, computing parent hashes
        for (int i = 0; i < TreeHeight; i++)
        {
            var siblingHash = SiblingHashes[i];
            var isRight = SiblingIsRight[i];

            // Compute parent hash: Hash(left || right)
            byte[] combinedHash;
            if (isRight)
            {
                // Sibling is on the right, current node is on the left
                combinedHash = currentHash.Concat(siblingHash).ToArray();
            }
            else
            {
                // Sibling is on the left, current node is on the right
                combinedHash = siblingHash.Concat(currentHash).ToArray();
            }

            currentHash = hashFunction.ComputeHash(combinedHash);
        }

        // Compare the computed root hash with the expected root hash
        return currentHash.SequenceEqual(expectedRootHash);
    }
}
