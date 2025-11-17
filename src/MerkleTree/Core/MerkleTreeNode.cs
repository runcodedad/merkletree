namespace MerkleTree.Core;

/// <summary>
/// Represents a node in a Merkle tree structure.
/// </summary>
/// <remarks>
/// This is a placeholder class for the initial library setup.
/// Full implementation will be provided in future releases.
/// </remarks>
public class MerkleTreeNode
{
    /// <summary>
    /// Gets or sets the hash value of this node.
    /// </summary>
    public byte[]? Hash { get; set; }

    /// <summary>
    /// Gets or sets the left child node.
    /// </summary>
    public MerkleTreeNode? Left { get; set; }

    /// <summary>
    /// Gets or sets the right child node.
    /// </summary>
    public MerkleTreeNode? Right { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MerkleTreeNode"/> class.
    /// </summary>
    public MerkleTreeNode()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MerkleTreeNode"/> class with the specified hash.
    /// </summary>
    /// <param name="hash">The hash value for this node.</param>
    public MerkleTreeNode(byte[] hash)
    {
        Hash = hash;
    }

    /// <summary>
    /// Serializes the node's hash to a fixed-size binary form.
    /// </summary>
    /// <returns>The hash value as a byte array.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the hash is null.</exception>
    /// <remarks>
    /// The serialization format is deterministic and consists of the raw hash bytes.
    /// The size of the returned array depends on the hash function used to create this node.
    /// </remarks>
    public byte[] Serialize()
    {
        if (Hash == null)
        {
            throw new InvalidOperationException("Cannot serialize a node with a null hash.");
        }

        return Hash;
    }

    /// <summary>
    /// Deserializes a Merkle tree node from its binary representation.
    /// </summary>
    /// <param name="data">The binary data to deserialize.</param>
    /// <returns>A new <see cref="MerkleTreeNode"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is empty.</exception>
    /// <remarks>
    /// This method creates a node with only the hash value set. It does not restore child references.
    /// The deserialized node represents a root node with the given hash.
    /// </remarks>
    public static MerkleTreeNode Deserialize(byte[] data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data), "Cannot deserialize from null data.");
        }

        if (data.Length == 0)
        {
            throw new ArgumentException("Cannot deserialize from empty data.", nameof(data));
        }

        // Create a new node with a copy of the hash data
        return new MerkleTreeNode(data.ToArray());
    }
}
