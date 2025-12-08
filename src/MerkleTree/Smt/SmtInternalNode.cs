namespace MerkleTree.Smt;

/// <summary>
/// Represents an internal node in a Sparse Merkle Tree with left and right children.
/// </summary>
/// <remarks>
/// <para>
/// Internal nodes have two children and do not store data directly.
/// They represent the structure of the tree and enable navigation to leaf nodes.
/// </para>
/// <para>
/// The node hash is computed as: Hash(0x01 || leftHash || rightHash)
/// where 0x01 is the internal node domain separator, ensuring collision resistance with leaves.
/// </para>
/// </remarks>
public sealed class SmtInternalNode : SmtNode
{
    /// <summary>
    /// Gets the hash of the left child node.
    /// </summary>
    public ReadOnlyMemory<byte> LeftHash { get; }

    /// <summary>
    /// Gets the hash of the right child node.
    /// </summary>
    public ReadOnlyMemory<byte> RightHash { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtInternalNode"/> class.
    /// </summary>
    /// <param name="leftHash">The hash of the left child.</param>
    /// <param name="rightHash">The hash of the right child.</param>
    /// <param name="nodeHash">The computed hash of this internal node.</param>
    /// <exception cref="ArgumentException">Thrown when leftHash, rightHash, or nodeHash is empty.</exception>
    public SmtInternalNode(
        ReadOnlyMemory<byte> leftHash,
        ReadOnlyMemory<byte> rightHash,
        ReadOnlyMemory<byte> nodeHash)
        : base(SmtNodeType.Internal, nodeHash)
    {
        if (leftHash.Length == 0)
            throw new ArgumentException("Left hash cannot be empty.", nameof(leftHash));

        if (rightHash.Length == 0)
            throw new ArgumentException("Right hash cannot be empty.", nameof(rightHash));

        LeftHash = leftHash;
        RightHash = rightHash;
    }
}
