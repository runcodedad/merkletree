using System;
using MerkleTree.Core;
using MerkleTree.Hashing;

namespace MerkleTree.Smt;

/// <summary>
/// Represents a Sparse Merkle Tree with configurable depth and pluggable key-to-path mapping.
/// </summary>
/// <remarks>
/// <para>
/// A Sparse Merkle Tree (SMT) is a cryptographic data structure that efficiently represents
/// a large key-value store while maintaining a compact proof size. Unlike traditional Merkle trees,
/// SMTs can handle a vast key space (e.g., 2^256 keys) by only storing non-empty leaves and
/// using canonical zero-hashes for empty subtrees.
/// </para>
/// <para><strong>Key Features:</strong></para>
/// <list type="bullet">
/// <item><description><strong>Configurable Depth</strong>: Tree depth determines key space size (2^depth possible keys)</description></item>
/// <item><description><strong>Arbitrary Keys</strong>: Accepts keys of any length, hashed to fixed-length bit paths</description></item>
/// <item><description><strong>Pluggable Hashing</strong>: Uses injected hash function for key mapping and node hashing</description></item>
/// <item><description><strong>Storage-Agnostic</strong>: Core model contains no persistence logic</description></item>
/// <item><description><strong>Deterministic</strong>: Same inputs produce identical trees across platforms</description></item>
/// </list>
/// <para><strong>Key-to-Path Mapping:</strong></para>
/// <para>
/// Keys are mapped to tree positions using a two-step process:
/// 1. Hash the key using the configured hash function
/// 2. Convert the hash to a bit path (each bit determines left/right traversal)
/// </para>
/// <para><strong>Tree Depth:</strong></para>
/// <para>
/// The depth determines the maximum key space:
/// - Depth 8 → 256 keys
/// - Depth 16 → 65,536 keys
/// - Depth 32 → 4,294,967,296 keys
/// - Depth 256 → 2^256 keys (default for SHA-256)
/// </para>
/// <para>
/// Higher depths support more keys but require longer bit paths for traversal.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create SMT with SHA-256 (default depth 256)
/// var hashFunction = new Sha256HashFunction();
/// var smt = new SparseMerkleTree(hashFunction);
/// 
/// // Create SMT with custom depth
/// var smallTree = new SparseMerkleTree(hashFunction, depth: 16);
/// 
/// // Map a key to its bit path
/// var key = Encoding.UTF8.GetBytes("my-key");
/// var bitPath = smt.GetBitPath(key);
/// // bitPath is a 256-bit boolean array determining the key's position
/// </code>
/// </example>
public sealed class SparseMerkleTree
{
    private readonly IHashFunction _hashFunction;

    /// <summary>
    /// Gets the metadata for this Sparse Merkle Tree.
    /// </summary>
    /// <remarks>
    /// The metadata contains hash algorithm ID, tree depth, zero-hash table,
    /// and version information for deterministic reproduction.
    /// </remarks>
    public SmtMetadata Metadata { get; }

    /// <summary>
    /// Gets the tree depth (number of levels).
    /// </summary>
    /// <remarks>
    /// The depth determines the maximum number of keys: 2^Depth.
    /// This is a convenience property that returns <see cref="SmtMetadata.TreeDepth"/>.
    /// </remarks>
    public int Depth => Metadata.TreeDepth;

    /// <summary>
    /// Gets the zero-hash table for efficient empty node operations.
    /// </summary>
    /// <remarks>
    /// The zero-hash table contains precomputed hashes for empty subtrees at each level.
    /// This is a convenience property that returns <see cref="SmtMetadata.ZeroHashes"/>.
    /// </remarks>
    public ZeroHashTable ZeroHashes => Metadata.ZeroHashes;

    /// <summary>
    /// Gets the hash algorithm identifier.
    /// </summary>
    /// <remarks>
    /// This is a convenience property that returns <see cref="SmtMetadata.HashAlgorithmId"/>.
    /// </remarks>
    public string HashAlgorithmId => Metadata.HashAlgorithmId;

    /// <summary>
    /// Initializes a new instance of the <see cref="SparseMerkleTree"/> class with default depth.
    /// </summary>
    /// <param name="hashFunction">The hash function to use for key mapping and node hashing.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hashFunction"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// The default depth is determined by the hash function's output size:
    /// - SHA-256: 256 bits → depth 256
    /// - SHA-512: 512 bits → depth 512
    /// - BLAKE3: 256 bits → depth 256
    /// </para>
    /// <para>
    /// This ensures the full hash output is used for the bit path, maximizing the key space.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var hashFunction = new Sha256HashFunction();
    /// var smt = new SparseMerkleTree(hashFunction);
    /// // Depth is automatically set to 256 (32 bytes * 8 bits)
    /// </code>
    /// </example>
    public SparseMerkleTree(IHashFunction hashFunction)
        : this(hashFunction, hashFunction?.HashSizeInBytes * 8 ?? throw new ArgumentNullException(nameof(hashFunction)))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SparseMerkleTree"/> class with custom depth.
    /// </summary>
    /// <param name="hashFunction">The hash function to use for key mapping and node hashing.</param>
    /// <param name="depth">The tree depth (number of levels).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hashFunction"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="depth"/> is less than 1 or exceeds the hash function's bit output size.</exception>
    /// <remarks>
    /// <para>
    /// The depth must be between 1 and the hash function's output size in bits.
    /// For example, with SHA-256 (32 bytes = 256 bits), depth can be 1 to 256.
    /// </para>
    /// <para>
    /// Lower depths reduce the key space but may improve performance for applications
    /// that don't need a large key space.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var hashFunction = new Sha256HashFunction();
    /// var smt = new SparseMerkleTree(hashFunction, depth: 16);
    /// // Tree supports up to 2^16 = 65,536 keys
    /// </code>
    /// </example>
    public SparseMerkleTree(IHashFunction hashFunction, int depth)
    {
        if (hashFunction == null)
            throw new ArgumentNullException(nameof(hashFunction));

        if (depth < 1)
            throw new ArgumentOutOfRangeException(nameof(depth), "Depth must be at least 1.");

        int maxDepth = hashFunction.HashSizeInBytes * 8;
        if (depth > maxDepth)
            throw new ArgumentOutOfRangeException(nameof(depth),
                $"Depth {depth} exceeds hash function output size of {maxDepth} bits.");

        _hashFunction = hashFunction;
        Metadata = SmtMetadata.Create(hashFunction, depth);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SparseMerkleTree"/> class with existing metadata.
    /// </summary>
    /// <param name="hashFunction">The hash function to use for key mapping and node hashing.</param>
    /// <param name="metadata">The metadata describing the tree configuration.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hashFunction"/> or <paramref name="metadata"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the hash function doesn't match the metadata.</exception>
    /// <remarks>
    /// <para>
    /// This constructor is useful when loading a tree from persistent storage.
    /// The hash function must match the one specified in the metadata.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Load metadata from storage
    /// var metadata = SmtMetadata.Deserialize(serializedBytes);
    /// 
    /// // Create hash function matching metadata
    /// var hashFunction = GetHashFunction(metadata.HashAlgorithmId);
    /// 
    /// // Create tree with loaded metadata
    /// var smt = new SparseMerkleTree(hashFunction, metadata);
    /// </code>
    /// </example>
    public SparseMerkleTree(IHashFunction hashFunction, SmtMetadata metadata)
    {
        if (hashFunction == null)
            throw new ArgumentNullException(nameof(hashFunction));
        
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        if (hashFunction.Name != metadata.HashAlgorithmId)
            throw new ArgumentException(
                $"Hash function '{hashFunction.Name}' does not match metadata algorithm '{metadata.HashAlgorithmId}'.",
                nameof(hashFunction));

        _hashFunction = hashFunction;
        Metadata = metadata;
    }

    /// <summary>
    /// Converts an arbitrary-length key to a fixed-length bit path for tree traversal.
    /// </summary>
    /// <param name="key">The key to convert.</param>
    /// <returns>A boolean array representing the bit path (false = left, true = right).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is empty.</exception>
    /// <remarks>
    /// <para>
    /// The key-to-path mapping is deterministic and consists of two steps:
    /// 1. Hash the key using the configured hash function
    /// 2. Convert the hash to a bit path using the first <see cref="Depth"/> bits
    /// </para>
    /// <para>
    /// The bit path determines the leaf's position in the tree. Each bit specifies
    /// whether to traverse left (false/0) or right (true/1) at each level.
    /// </para>
    /// <para>
    /// This method is thread-safe and can be called concurrently.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var smt = new SparseMerkleTree(new Sha256HashFunction(), depth: 8);
    /// var key = Encoding.UTF8.GetBytes("user123");
    /// var path = smt.GetBitPath(key);
    /// // path.Length == 8
    /// // path[0] determines left/right at level 0 (root)
    /// // path[7] determines left/right at level 7 (near leaf)
    /// </code>
    /// </example>
    public bool[] GetBitPath(byte[] key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));

        if (key.Length == 0)
            throw new ArgumentException("Key cannot be empty.", nameof(key));

        // Hash the key to get a fixed-length representation
        var keyHash = _hashFunction.ComputeHash(key);

        // Convert hash to bit path using tree depth
        return HashUtils.GetBitPath(keyHash, Depth);
    }

    /// <summary>
    /// Converts an arbitrary-length key to a fixed-length bit path for tree traversal.
    /// </summary>
    /// <param name="key">The key to convert.</param>
    /// <returns>A boolean array representing the bit path (false = left, true = right).</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is empty.</exception>
    /// <remarks>
    /// This overload accepts <see cref="ReadOnlyMemory{T}"/> for better memory efficiency
    /// in scenarios where the key is already in memory.
    /// </remarks>
    public bool[] GetBitPath(ReadOnlyMemory<byte> key)
    {
        if (key.Length == 0)
            throw new ArgumentException("Key cannot be empty.", nameof(key));

        // Hash the key to get a fixed-length representation
        var keyHash = _hashFunction.ComputeHash(key.ToArray());

        // Convert hash to bit path using tree depth
        return HashUtils.GetBitPath(keyHash, Depth);
    }

    /// <summary>
    /// Hashes a key to produce a fixed-length key hash used for bit path derivation.
    /// </summary>
    /// <param name="key">The key to hash.</param>
    /// <returns>The hash of the key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is empty.</exception>
    /// <remarks>
    /// <para>
    /// This is a utility method that exposes the first step of key-to-path mapping.
    /// The key hash is used internally for bit path derivation and can also be used
    /// for creating leaf nodes.
    /// </para>
    /// <para>
    /// This method is thread-safe and can be called concurrently.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var smt = new SparseMerkleTree(new Sha256HashFunction());
    /// var key = Encoding.UTF8.GetBytes("user123");
    /// var keyHash = smt.HashKey(key);
    /// // keyHash is a 32-byte SHA-256 hash
    /// </code>
    /// </example>
    public byte[] HashKey(byte[] key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));

        if (key.Length == 0)
            throw new ArgumentException("Key cannot be empty.", nameof(key));

        return _hashFunction.ComputeHash(key);
    }

    /// <summary>
    /// Hashes a key to produce a fixed-length key hash used for bit path derivation.
    /// </summary>
    /// <param name="key">The key to hash.</param>
    /// <returns>The hash of the key.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is empty.</exception>
    /// <remarks>
    /// This overload accepts <see cref="ReadOnlyMemory{T}"/> for better memory efficiency.
    /// </remarks>
    public byte[] HashKey(ReadOnlyMemory<byte> key)
    {
        if (key.Length == 0)
            throw new ArgumentException("Key cannot be empty.", nameof(key));

        return _hashFunction.ComputeHash(key.ToArray());
    }

    /// <summary>
    /// Creates an empty node for the specified level.
    /// </summary>
    /// <param name="level">The level of the empty node (0 = leaf level).</param>
    /// <returns>An <see cref="SmtEmptyNode"/> with the canonical zero-hash for the level.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="level"/> is negative or exceeds tree depth.</exception>
    /// <remarks>
    /// <para>
    /// Empty nodes use canonical zero-hashes from the zero-hash table to represent
    /// unoccupied subtrees without storing them explicitly.
    /// </para>
    /// <para>
    /// This method is thread-safe and can be called concurrently.
    /// </para>
    /// </remarks>
    public SmtEmptyNode CreateEmptyNode(int level)
    {
        if (level < 0 || level > Depth)
            throw new ArgumentOutOfRangeException(nameof(level),
                $"Level must be between 0 and {Depth} inclusive.");

        var zeroHash = ZeroHashes[level];
        return new SmtEmptyNode(level, zeroHash);
    }

    /// <summary>
    /// Creates a leaf node with the specified key and value.
    /// </summary>
    /// <param name="key">The key for the leaf.</param>
    /// <param name="value">The value to store in the leaf.</param>
    /// <param name="includeOriginalKey">Whether to include the original key in the node (for proof generation).</param>
    /// <returns>An <see cref="SmtLeafNode"/> with computed hash.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> or <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> or <paramref name="value"/> is empty.</exception>
    /// <remarks>
    /// <para>
    /// The leaf hash is computed as: Hash(0x00 || keyHash || value)
    /// where 0x00 is the leaf domain separator.
    /// </para>
    /// <para>
    /// If <paramref name="includeOriginalKey"/> is true, the original key is stored in the node
    /// for proof generation. Otherwise, only the key hash is retained.
    /// </para>
    /// <para>
    /// This method is thread-safe and can be called concurrently.
    /// </para>
    /// </remarks>
    public SmtLeafNode CreateLeafNode(byte[] key, byte[] value, bool includeOriginalKey = false)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        if (key.Length == 0)
            throw new ArgumentException("Key cannot be empty.", nameof(key));

        if (value.Length == 0)
            throw new ArgumentException("Value cannot be empty.", nameof(value));

        var keyHash = _hashFunction.ComputeHash(key);

        // Compute leaf hash: Hash(0x00 || keyHash || value)
        var leafData = new byte[1 + keyHash.Length + value.Length];
        leafData[0] = MerkleTreeBase.LeafDomainSeparator;
        Array.Copy(keyHash, 0, leafData, 1, keyHash.Length);
        Array.Copy(value, 0, leafData, 1 + keyHash.Length, value.Length);
        var nodeHash = _hashFunction.ComputeHash(leafData);

        return new SmtLeafNode(
            keyHash,
            value,
            nodeHash,
            includeOriginalKey ? (ReadOnlyMemory<byte>?)key : null);
    }

    /// <summary>
    /// Creates an internal node with the specified child hashes.
    /// </summary>
    /// <param name="leftHash">The hash of the left child.</param>
    /// <param name="rightHash">The hash of the right child.</param>
    /// <returns>An <see cref="SmtInternalNode"/> with computed hash.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leftHash"/> or <paramref name="rightHash"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="leftHash"/> or <paramref name="rightHash"/> is empty.</exception>
    /// <remarks>
    /// <para>
    /// The internal node hash is computed as: Hash(0x01 || leftHash || rightHash)
    /// where 0x01 is the internal node domain separator.
    /// </para>
    /// <para>
    /// This method is thread-safe and can be called concurrently.
    /// </para>
    /// </remarks>
    public SmtInternalNode CreateInternalNode(byte[] leftHash, byte[] rightHash)
    {
        if (leftHash == null)
            throw new ArgumentNullException(nameof(leftHash));
        
        if (rightHash == null)
            throw new ArgumentNullException(nameof(rightHash));

        if (leftHash.Length == 0)
            throw new ArgumentException("Left hash cannot be empty.", nameof(leftHash));

        if (rightHash.Length == 0)
            throw new ArgumentException("Right hash cannot be empty.", nameof(rightHash));

        // Compute internal node hash: Hash(0x01 || leftHash || rightHash)
        var internalData = new byte[1 + leftHash.Length + rightHash.Length];
        internalData[0] = MerkleTreeBase.InternalNodeDomainSeparator;
        Array.Copy(leftHash, 0, internalData, 1, leftHash.Length);
        Array.Copy(rightHash, 0, internalData, 1 + leftHash.Length, rightHash.Length);
        var nodeHash = _hashFunction.ComputeHash(internalData);

        return new SmtInternalNode(leftHash, rightHash, nodeHash);
    }
}
