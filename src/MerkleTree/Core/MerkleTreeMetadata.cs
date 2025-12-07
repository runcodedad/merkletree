namespace MerkleTree.Core;

/// <summary>
/// Contains metadata about a constructed Merkle tree.
/// </summary>
/// <remarks>
/// This class provides information about the tree structure including its height,
/// the number of leaves, the root node, and the hash algorithm used.
/// </remarks>
public class MerkleTreeMetadata
{
    /// <summary>
    /// Gets the root node of the Merkle tree.
    /// </summary>
    public MerkleTreeNode Root { get; }

    /// <summary>
    /// Gets the root hash of the Merkle tree.
    /// </summary>
    /// <remarks>
    /// This is a convenience property that returns the hash from the Root node.
    /// </remarks>
    public byte[] RootHash => Root.Hash ?? Array.Empty<byte>();

    /// <summary>
    /// Gets the height of the Merkle tree.
    /// </summary>
    /// <remarks>
    /// The height is the number of levels in the tree, where leaves are at level 0.
    /// A single leaf has height 0, two leaves have height 1, etc.
    /// </remarks>
    public int Height { get; }

    /// <summary>
    /// Gets the number of leaves in the Merkle tree.
    /// </summary>
    public long LeafCount { get; }

    /// <summary>
    /// Gets the name/identifier of the hash algorithm used to construct this tree.
    /// </summary>
    /// <remarks>
    /// This identifier is used for format identification when serializing or deserializing
    /// tree data, and helps ensure compatibility when verifying proofs or comparing trees.
    /// Examples: "SHA-256", "SHA-512", "BLAKE3"
    /// </remarks>
    public string HashAlgorithmName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MerkleTreeMetadata"/> class.
    /// </summary>
    /// <param name="root">The root node of the tree.</param>
    /// <param name="height">The height of the tree.</param>
    /// <param name="leafCount">The number of leaves in the tree.</param>
    /// <param name="hashAlgorithmName">The name/identifier of the hash algorithm used.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="root"/> or <paramref name="hashAlgorithmName"/> is null.</exception>
    public MerkleTreeMetadata(MerkleTreeNode root, int height, long leafCount, string hashAlgorithmName)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        Height = height;
        LeafCount = leafCount;
        HashAlgorithmName = hashAlgorithmName ?? throw new ArgumentNullException(nameof(hashAlgorithmName));
    }

    /// <summary>
    /// Serializes the root hash to a fixed-size binary form.
    /// </summary>
    /// <returns>The root hash as a byte array.</returns>
    /// <remarks>
    /// This is a convenience method that serializes the root node's hash.
    /// The size of the returned array depends on the hash function used to create the tree.
    /// </remarks>
    public byte[] SerializeRoot()
    {
        return Root.Serialize();
    }

    /// <summary>
    /// Deserializes a root hash from its binary representation.
    /// </summary>
    /// <param name="data">The binary data to deserialize.</param>
    /// <returns>A new <see cref="MerkleTreeNode"/> representing the root.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is empty.</exception>
    /// <remarks>
    /// This is a convenience method that deserializes a root node from binary data.
    /// </remarks>
    public static MerkleTreeNode DeserializeRoot(byte[] data)
    {
        return MerkleTreeNode.Deserialize(data);
    }
}
