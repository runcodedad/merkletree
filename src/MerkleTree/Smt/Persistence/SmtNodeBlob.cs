namespace MerkleTree.Smt.Persistence;

/// <summary>
/// Represents a minimal node blob for SMT persistence operations.
/// </summary>
/// <remarks>
/// <para>
/// This structure contains the essential information needed to persist and retrieve SMT nodes.
/// It is designed to be storage-agnostic and can be used with any persistence adapter.
/// </para>
/// <para>
/// The node blob contains:
/// <list type="bullet">
/// <item><description><strong>Hash</strong>: The unique hash identifier for the node</description></item>
/// <item><description><strong>Path</strong>: Optional bit-path through the tree to this node</description></item>
/// <item><description><strong>SerializedNode</strong>: The serialized node data</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class SmtNodeBlob
{
    /// <summary>
    /// Gets the hash that uniquely identifies this node.
    /// </summary>
    /// <remarks>
    /// This hash is used as the primary key for node retrieval and must be unique within the tree.
    /// The hash is computed using the hash function specified in the SMT metadata.
    /// </remarks>
    public ReadOnlyMemory<byte> Hash { get; }

    /// <summary>
    /// Gets the optional bit-path from root to this node.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The path is a sequence of bits (represented as booleans) that describes the route
    /// from the tree root to this node. Each bit indicates whether to traverse the left (false)
    /// or right (true) child at each level.
    /// </para>
    /// <para>
    /// This field is optional and may be null if the node is retrieved by hash only.
    /// It is primarily used for debugging, path-based queries, and snapshot operations.
    /// </para>
    /// </remarks>
    public ReadOnlyMemory<bool>? Path { get; }

    /// <summary>
    /// Gets the serialized node data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This contains the binary serialized representation of the SMT node, including:
    /// - Node type (leaf, internal, or empty)
    /// - Child hashes (for internal nodes)
    /// - Key and value (for leaf nodes)
    /// - Any other node-specific metadata
    /// </para>
    /// <para>
    /// The serialization format is defined by the SMT implementation and must be
    /// deterministic and platform-independent.
    /// </para>
    /// </remarks>
    public ReadOnlyMemory<byte> SerializedNode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtNodeBlob"/> class.
    /// </summary>
    /// <param name="hash">The hash that uniquely identifies this node.</param>
    /// <param name="serializedNode">The serialized node data.</param>
    /// <param name="path">Optional bit-path from root to this node.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="hash"/> or <paramref name="serializedNode"/> is empty.</exception>
    public SmtNodeBlob(
        ReadOnlyMemory<byte> hash,
        ReadOnlyMemory<byte> serializedNode,
        ReadOnlyMemory<bool>? path = null)
    {
        if (hash.IsEmpty)
            throw new ArgumentException("Hash cannot be empty.", nameof(hash));

        if (serializedNode.IsEmpty)
            throw new ArgumentException("Serialized node cannot be empty.", nameof(serializedNode));

        Hash = hash;
        SerializedNode = serializedNode;
        Path = path;
    }

    /// <summary>
    /// Creates a node blob without a path.
    /// </summary>
    /// <param name="hash">The hash that uniquely identifies this node.</param>
    /// <param name="serializedNode">The serialized node data.</param>
    /// <returns>A new <see cref="SmtNodeBlob"/> instance.</returns>
    public static SmtNodeBlob Create(ReadOnlyMemory<byte> hash, ReadOnlyMemory<byte> serializedNode)
    {
        return new SmtNodeBlob(hash, serializedNode, path: null);
    }

    /// <summary>
    /// Creates a node blob with a path.
    /// </summary>
    /// <param name="hash">The hash that uniquely identifies this node.</param>
    /// <param name="serializedNode">The serialized node data.</param>
    /// <param name="path">Bit-path from root to this node.</param>
    /// <returns>A new <see cref="SmtNodeBlob"/> instance.</returns>
    public static SmtNodeBlob CreateWithPath(
        ReadOnlyMemory<byte> hash,
        ReadOnlyMemory<byte> serializedNode,
        ReadOnlyMemory<bool> path)
    {
        return new SmtNodeBlob(hash, serializedNode, path);
    }
}
