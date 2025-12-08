namespace MerkleTree.Smt;

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
