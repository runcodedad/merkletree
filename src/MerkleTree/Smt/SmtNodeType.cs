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
