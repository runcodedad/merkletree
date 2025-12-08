using System;

namespace MerkleTree.Smt;

/// <summary>
/// Represents the type of node in a Sparse Merkle Tree.
/// </summary>
public enum SmtNodeType
{
    /// <summary>
    /// An empty node representing an unoccupied subtree.
    /// Uses canonical zero-hash from the zero-hash table.
    /// </summary>
    Empty,

    /// <summary>
    /// A leaf node containing a key-value pair.
    /// </summary>
    Leaf,

    /// <summary>
    /// An internal node with left and right children.
    /// </summary>
    Internal
}

/// <summary>
/// Base class for all Sparse Merkle Tree nodes.
/// </summary>
/// <remarks>
/// SMT nodes are immutable and use domain-separated hashing for security.
/// Empty nodes are represented using canonical zero-hashes to save storage space.
/// </remarks>
public abstract class SmtNode
{
    /// <summary>
    /// Gets the type of this node.
    /// </summary>
    public SmtNodeType NodeType { get; }

    /// <summary>
    /// Gets the hash of this node.
    /// </summary>
    /// <remarks>
    /// For empty nodes, this is the canonical zero-hash from the zero-hash table.
    /// For leaf nodes, this is the hash of the key and value.
    /// For internal nodes, this is the hash of the left and right child hashes.
    /// </remarks>
    public ReadOnlyMemory<byte> Hash { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtNode"/> class.
    /// </summary>
    /// <param name="nodeType">The type of the node.</param>
    /// <param name="hash">The hash of the node.</param>
    /// <exception cref="ArgumentException">Thrown when hash is empty.</exception>
    protected SmtNode(SmtNodeType nodeType, ReadOnlyMemory<byte> hash)
    {
        if (hash.Length == 0)
            throw new ArgumentException("Hash cannot be empty.", nameof(hash));

        NodeType = nodeType;
        Hash = hash;
    }
}

/// <summary>
/// Represents an empty node in a Sparse Merkle Tree.
/// </summary>
/// <remarks>
/// <para>
/// Empty nodes represent unoccupied subtrees and use canonical zero-hashes
/// from the zero-hash table. This allows efficient sparse tree operations
/// without storing empty branches explicitly.
/// </para>
/// <para>
/// The zero-hash for an empty node depends on its level in the tree and
/// is retrieved from the <see cref="ZeroHashTable"/>.
/// </para>
/// </remarks>
public sealed class SmtEmptyNode : SmtNode
{
    /// <summary>
    /// Gets the level of this empty node in the tree.
    /// </summary>
    /// <remarks>
    /// Level 0 is the leaf level, and higher levels are closer to the root.
    /// </remarks>
    public int Level { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtEmptyNode"/> class.
    /// </summary>
    /// <param name="level">The level of the empty node in the tree.</param>
    /// <param name="zeroHash">The canonical zero-hash for this level from the zero-hash table.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when level is negative.</exception>
    /// <exception cref="ArgumentException">Thrown when zeroHash is empty.</exception>
    public SmtEmptyNode(int level, ReadOnlyMemory<byte> zeroHash)
        : base(SmtNodeType.Empty, zeroHash)
    {
        if (level < 0)
            throw new ArgumentOutOfRangeException(nameof(level), "Level cannot be negative.");

        Level = level;
    }
}

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
