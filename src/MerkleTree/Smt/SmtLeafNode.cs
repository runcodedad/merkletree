namespace MerkleTree.Smt;

/// <summary>
/// Represents a leaf node in a Sparse Merkle Tree containing a key-value pair.
/// </summary>
/// <remarks>
/// <para>
/// Leaf nodes store the actual data in the tree. The key is hashed to determine
/// the leaf's position in the tree (bit path), and the value is the data stored.
/// </para>
/// <para>
/// The node hash is computed as: Hash(0x00 || keyHash || value)
/// where 0x00 is the leaf domain separator, ensuring collision resistance with internal nodes.
/// </para>
/// <para>
/// <strong>Key Storage:</strong>
/// The original key may be retained for proof generation or verification, but only
/// the key hash is used for tree operations and bit-path derivation.
/// </para>
/// </remarks>
public sealed class SmtLeafNode : SmtNode
{
    /// <summary>
    /// Gets the hash of the key (determines the leaf's position in the tree).
    /// </summary>
    /// <remarks>
    /// This is the hashed version of the original key, used to derive the bit path
    /// for tree traversal.
    /// </remarks>
    public ReadOnlyMemory<byte> KeyHash { get; }

    /// <summary>
    /// Gets the value stored in this leaf.
    /// </summary>
    public ReadOnlyMemory<byte> Value { get; }

    /// <summary>
    /// Gets the original key, if retained for proof generation.
    /// </summary>
    /// <remarks>
    /// This is optional and may be null if the original key is not needed.
    /// For proof verification, only the key hash is required.
    /// </remarks>
    public ReadOnlyMemory<byte>? OriginalKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtLeafNode"/> class.
    /// </summary>
    /// <param name="keyHash">The hash of the key.</param>
    /// <param name="value">The value stored in the leaf.</param>
    /// <param name="nodeHash">The computed hash of this leaf node.</param>
    /// <param name="originalKey">Optional original key for proof generation.</param>
    /// <exception cref="ArgumentException">Thrown when keyHash, value, or nodeHash is empty.</exception>
    public SmtLeafNode(
        ReadOnlyMemory<byte> keyHash,
        ReadOnlyMemory<byte> value,
        ReadOnlyMemory<byte> nodeHash,
        ReadOnlyMemory<byte>? originalKey = null)
        : base(SmtNodeType.Leaf, nodeHash)
    {
        if (keyHash.Length == 0)
            throw new ArgumentException("Key hash cannot be empty.", nameof(keyHash));

        if (value.Length == 0)
            throw new ArgumentException("Value cannot be empty.", nameof(value));

        KeyHash = keyHash;
        Value = value;
        OriginalKey = originalKey;
    }
}
