namespace MerkleTree.Smt;

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
