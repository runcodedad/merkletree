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

        // Step 1: Hash the key to get a fixed-length representation
        // This ensures all keys, regardless of original length, map to the same size
        // For example, "user" and "very-long-username-12345" both become 32-byte hashes with SHA-256
        var keyHash = _hashFunction.ComputeHash(key);

        // Step 2: Convert the key hash to a bit path using the first `Depth` bits
        // The bit path determines the leaf's position in the tree:
        // - bitPath[0] = first bit = root level decision (left=false, right=true)
        // - bitPath[1] = second bit = level 1 decision
        // - bitPath[Depth-1] = last bit = final decision before reaching leaf
        // For depth 256 with SHA-256, all 256 bits are used, giving 2^256 possible positions
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

        // Step 1: Hash the key to get a fixed-length key hash
        // This is the same hash used for bit path computation
        var keyHash = _hashFunction.ComputeHash(key);

        // Step 2: Compute the leaf node's hash using domain-separated hashing
        // Leaf hash format: Hash(0x00 || keyHash || value)
        // The 0x00 prefix (LeafDomainSeparator) prevents collision attacks between:
        // - Leaf nodes (which use 0x00)
        // - Internal nodes (which use 0x01)
        // This is critical for security: without domain separation, an attacker could
        // potentially forge proofs by crafting values that collide with internal node hashes
        var leafData = new byte[1 + keyHash.Length + value.Length];
        leafData[0] = MerkleTreeBase.LeafDomainSeparator; // 0x00
        Array.Copy(keyHash, 0, leafData, 1, keyHash.Length);
        Array.Copy(value, 0, leafData, 1 + keyHash.Length, value.Length);
        var nodeHash = _hashFunction.ComputeHash(leafData);

        // Step 3: Create and return the leaf node
        // includeOriginalKey determines if we store the original key bytes (needed for some proof types)
        // If false, only the key hash is stored to save memory
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

        // Compute internal node hash using domain-separated hashing
        // Internal node hash format: Hash(0x01 || leftHash || rightHash)
        // The 0x01 prefix (InternalNodeDomainSeparator) distinguishes internal nodes from:
        // - Leaf nodes (which use 0x00)
        // This prevents second-preimage attacks where leaf data could be crafted to
        // match an internal node's hash structure
        // The left-to-right ordering is critical: leftHash || rightHash ensures
        // the tree structure is unambiguous and deterministic
        var internalData = new byte[1 + leftHash.Length + rightHash.Length];
        internalData[0] = MerkleTreeBase.InternalNodeDomainSeparator; // 0x01
        Array.Copy(leftHash, 0, internalData, 1, leftHash.Length);
        Array.Copy(rightHash, 0, internalData, 1 + leftHash.Length, rightHash.Length);
        var nodeHash = _hashFunction.ComputeHash(internalData);

        return new SmtInternalNode(leftHash, rightHash, nodeHash);
    }

    /// <summary>
    /// Gets the value associated with a key from the tree.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="rootHash">The root hash of the tree to query.</param>
    /// <param name="nodeReader">The node reader for retrieving nodes from storage.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that resolves to the get result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> or <paramref name="rootHash"/> is empty.</exception>
    /// <remarks>
    /// <para>
    /// This method traverses the tree from the root to find the leaf node containing
    /// the specified key. It returns a result indicating whether the key was found
    /// and provides the value if present.
    /// </para>
    /// <para>
    /// The operation is read-only and does not modify the tree state.
    /// </para>
    /// </remarks>
    public async Task<SmtGetResult> GetAsync(
        byte[] key,
        ReadOnlyMemory<byte> rootHash,
        Persistence.ISmtNodeReader nodeReader,
        CancellationToken cancellationToken = default)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        
        if (nodeReader == null)
            throw new ArgumentNullException(nameof(nodeReader));

        if (key.Length == 0)
            throw new ArgumentException("Key cannot be empty.", nameof(key));

        if (rootHash.IsEmpty)
            throw new ArgumentException("Root hash cannot be empty.", nameof(rootHash));

        // Step 1: Compute the bit path that determines where this key should be located
        // The bit path is derived from the key's hash and specifies left/right at each tree level
        // Use full 256-bit path to support extension nodes beyond configured Depth
        var keyHash = HashKey(key);
        var fullBitPath = Hashing.HashUtils.GetBitPath(keyHash, 256);

        // Step 2: Traverse the tree from root to leaf following the bit path
        // May traverse beyond Depth if extension nodes exist (for collision resolution)
        var currentHash = rootHash;
        for (int level = 0; level < 256; level++)
        {
            // Check if we've reached an empty subtree (indicated by zero-hash)
            // Only check within configured Depth, as extension nodes don't use zero-hash shortcuts
            if (level < Depth && currentHash.Span.SequenceEqual(ZeroHashes[Depth - level]))
            {
                // Current position is empty - key does not exist
                return SmtGetResult.CreateNotFound();
            }

            // Step 3: Read the node at the current position from storage
            var nodeBlob = await nodeReader.ReadNodeByHashAsync(currentHash, cancellationToken);
            if (nodeBlob == null)
            {
                // Node not in storage - key not found
                // This can happen if the tree is incomplete or corrupted
                return SmtGetResult.CreateNotFound();
            }

            // Deserialize the node to determine its type (leaf, internal, or empty)
            var node = SmtNodeSerializer.Deserialize(nodeBlob.SerializedNode);

            // Step 4: If we've reached a leaf, check if it contains our target key
            if (node.NodeType == SmtNodeType.Leaf)
            {
                var leafNode = (SmtLeafNode)node;
                if (leafNode.KeyHash.Span.SequenceEqual(keyHash))
                {
                    // Found the key! Return its value
                    return SmtGetResult.CreateFound(leafNode.Value);
                }
                // Leaf exists but contains a different key - target key not in tree
                return SmtGetResult.CreateNotFound();
            }

            // Step 5: If we've reached an internal node, follow the bit path to the next level
            if (node.NodeType == SmtNodeType.Internal)
            {
                var internalNode = (SmtInternalNode)node;
                // Follow bit path: false (0) = left child, true (1) = right child
                currentHash = fullBitPath[level] ? internalNode.RightHash : internalNode.LeftHash;
            }
            else
            {
                // Empty node encountered during traversal - key not found
                return SmtGetResult.CreateNotFound();
            }
        }

        // Should not reach here if tree depth is correct
        // This would indicate a tree structure issue
        return SmtGetResult.CreateNotFound();
    }

    /// <summary>
    /// Updates or inserts a key-value pair in the tree.
    /// </summary>
    /// <param name="key">The key to update or insert.</param>
    /// <param name="value">The value to associate with the key.</param>
    /// <param name="rootHash">The current root hash of the tree.</param>
    /// <param name="nodeReader">The node reader for retrieving nodes from storage.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that resolves to the update result containing the new root hash and nodes to persist.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/>, <paramref name="value"/>, or <paramref name="rootHash"/> is empty.</exception>
    /// <remarks>
    /// <para>
    /// This method creates a new leaf node and reconstructs the path from the leaf to the root
    /// using copy-on-write semantics. Only the nodes along the update path are recreated.
    /// </para>
    /// <para>
    /// The operation does not modify storage directly. Instead, it returns a list of nodes
    /// that need to be persisted by the caller.
    /// </para>
    /// </remarks>
    public async Task<SmtUpdateResult> UpdateAsync(
        byte[] key,
        byte[] value,
        ReadOnlyMemory<byte> rootHash,
        Persistence.ISmtNodeReader nodeReader,
        CancellationToken cancellationToken = default)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        
        if (nodeReader == null)
            throw new ArgumentNullException(nameof(nodeReader));

        if (key.Length == 0)
            throw new ArgumentException("Key cannot be empty.", nameof(key));

        if (value.Length == 0)
            throw new ArgumentException("Value cannot be empty.", nameof(value));

        if (rootHash.IsEmpty)
            throw new ArgumentException("Root hash cannot be empty.", nameof(rootHash));

        // Initialize the list to collect all nodes that need to be written to storage
        var nodesToPersist = new List<Persistence.SmtNodeBlob>();
        
        // Step 1: Compute the bit path that determines where this key should be located
        var keyHash = HashKey(key);
        var bitPath = GetBitPath(key);
        
        // Step 2: Create the new leaf node with the key-value pair
        // We don't include the original key in the node to save space (only key hash is needed)
        var newLeaf = CreateLeafNode(key, value, includeOriginalKey: false);

        // Step 3: Add the new leaf to the list of nodes to persist
        // The leaf is the starting point for path reconstruction
        var leafBlob = CreateNodeBlob(newLeaf, bitPath);
        nodesToPersist.Add(leafBlob);

        // Step 4: Reconstruct the path from leaf to root using copy-on-write semantics
        // This creates new internal nodes along the path while preserving sibling subtrees
        // Only nodes on the update path are recreated; all other nodes remain unchanged
        var newRootHash = await UpdatePathAsync(
            bitPath,
            newLeaf.Hash,
            rootHash,
            nodeReader,
            nodesToPersist,
            keyHash,
            cancellationToken);

        // Return the new root hash and all nodes that need to be persisted
        // The caller is responsible for writing these nodes to storage
        return new SmtUpdateResult(newRootHash, nodesToPersist);
    }

    /// <summary>
    /// Deletes a key from the tree.
    /// </summary>
    /// <param name="key">The key to delete.</param>
    /// <param name="rootHash">The current root hash of the tree.</param>
    /// <param name="nodeReader">The node reader for retrieving nodes from storage.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that resolves to the update result containing the new root hash and nodes to persist.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> or <paramref name="rootHash"/> is empty.</exception>
    /// <remarks>
    /// <para>
    /// This method removes a key from the tree by replacing the leaf node with an empty node
    /// and reconstructing the path using copy-on-write semantics.
    /// </para>
    /// <para>
    /// If the key does not exist in the tree, the operation succeeds without changes
    /// (idempotent deletion).
    /// </para>
    /// </remarks>
    public async Task<SmtUpdateResult> DeleteAsync(
        byte[] key,
        ReadOnlyMemory<byte> rootHash,
        Persistence.ISmtNodeReader nodeReader,
        CancellationToken cancellationToken = default)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        
        if (nodeReader == null)
            throw new ArgumentNullException(nameof(nodeReader));

        if (key.Length == 0)
            throw new ArgumentException("Key cannot be empty.", nameof(key));

        if (rootHash.IsEmpty)
            throw new ArgumentException("Root hash cannot be empty.", nameof(rootHash));

        var nodesToPersist = new List<Persistence.SmtNodeBlob>();
        var keyHash = HashKey(key);
        var bitPath = GetBitPath(key);
        var emptyNode = CreateEmptyNode(0);

        // Reconstruct the path from empty node to root
        var newRootHash = await UpdatePathAsync(
            bitPath,
            emptyNode.Hash,
            rootHash,
            nodeReader,
            nodesToPersist,
            keyHash,
            cancellationToken);

        return new SmtUpdateResult(newRootHash, nodesToPersist);
    }

    /// <summary>
    /// Applies a batch of updates and deletes to the tree in a deterministic order.
    /// </summary>
    /// <param name="updates">The collection of key-value pairs to apply.</param>
    /// <param name="rootHash">The current root hash of the tree.</param>
    /// <param name="nodeReader">The node reader for retrieving nodes from storage.</param>
    /// <param name="nodeWriter">The node writer for persisting nodes within the batch. Nodes are persisted immediately after each operation so they're available for subsequent operations in the batch.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that resolves to the update result containing the new root hash and nodes to persist.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="updates"/>, <paramref name="nodeReader"/>, or <paramref name="nodeWriter"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="rootHash"/> is empty.</exception>
    /// <remarks>
    /// <para>
    /// This method processes multiple updates and deletes in a single batch operation.
    /// The updates are sorted by key hash to ensure deterministic results regardless
    /// of input order.
    /// </para>
    /// <para>
    /// If multiple updates affect the same key, the last one in the sorted order wins.
    /// This ensures deterministic conflict resolution.
    /// </para>
    /// <para>
    /// The operation uses copy-on-write semantics. Nodes are persisted immediately after
    /// each update via the <paramref name="nodeWriter"/>, ensuring subsequent updates in
    /// the batch can read the newly created nodes. All nodes are also returned in the
    /// result for tracking purposes.
    /// </para>
    /// </remarks>
    public async Task<SmtUpdateResult> BatchUpdateAsync(
        IEnumerable<SmtKeyValue> updates,
        ReadOnlyMemory<byte> rootHash,
        Persistence.ISmtNodeReader nodeReader,
        Persistence.ISmtNodeWriter nodeWriter,
        CancellationToken cancellationToken = default)
    {
        if (updates == null)
            throw new ArgumentNullException(nameof(updates));
        
        if (nodeReader == null)
            throw new ArgumentNullException(nameof(nodeReader));

        if (nodeWriter == null)
            throw new ArgumentNullException(nameof(nodeWriter));

        if (rootHash.IsEmpty)
            throw new ArgumentException("Root hash cannot be empty.", nameof(rootHash));

        // Sort updates by key hash for deterministic ordering
        var sortedUpdates = updates
            .Select(kv => new
            {
                KeyValue = kv,
                KeyHash = HashKey(kv.Key.ToArray()),
                BitPath = GetBitPath(kv.Key.ToArray())
            })
            .OrderBy(x => BitConverter.ToString(x.KeyHash).Replace("-", ""))
            .ToList();

        // Build a dictionary to handle conflicts (last write wins)
        var updatesByKey = new Dictionary<string, (SmtKeyValue KeyValue, bool[] BitPath)>();
        foreach (var item in sortedUpdates)
        {
            var keyHashHex = BitConverter.ToString(item.KeyHash).Replace("-", "");
            updatesByKey[keyHashHex] = (item.KeyValue, item.BitPath);
        }

        // Apply each update sequentially
        var currentRootHash = rootHash;
        var allNodesToPersist = new List<Persistence.SmtNodeBlob>();

        foreach (var (keyValue, bitPath) in updatesByKey.Values)
        {
            SmtUpdateResult result;

            if (keyValue.IsDelete)
            {
                result = await DeleteAsync(
                    keyValue.Key.ToArray(),
                    currentRootHash,
                    nodeReader,
                    cancellationToken);
            }
            else
            {
                result = await UpdateAsync(
                    keyValue.Key.ToArray(),
                    keyValue.Value!.Value.ToArray(),
                    currentRootHash,
                    nodeReader,
                    cancellationToken);
            }

            currentRootHash = result.NewRootHash;
            allNodesToPersist.AddRange(result.NodesToPersist);

            // Persist nodes immediately so they're available for subsequent operations in this batch
            await nodeWriter.WriteBatchAsync(result.NodesToPersist, cancellationToken);
        }

        return new SmtUpdateResult(currentRootHash, allNodesToPersist);
    }

    /// <summary>
    /// Helper method to update the path from a leaf to the root.
    /// </summary>
    /// <remarks>
    /// This method implements copy-on-write semantics for Sparse Merkle Trees:
    /// 1. Traverse down from root to collect sibling hashes at each level
    /// 2. Reconstruct the path bottom-up, creating new internal nodes
    /// 3. Return the new root hash while preserving all unchanged subtrees
    /// 4. Handle collisions by extending the tree beyond configured depth when needed
    /// </remarks>
    private async Task<ReadOnlyMemory<byte>> UpdatePathAsync(
        bool[] bitPath,
        ReadOnlyMemory<byte> leafHash,
        ReadOnlyMemory<byte> rootHash,
        Persistence.ISmtNodeReader nodeReader,
        List<Persistence.SmtNodeBlob> nodesToPersist,
        ReadOnlyMemory<byte> keyHash,
        CancellationToken cancellationToken)
    {
        // Phase 1: Traverse down from root to collect sibling hashes at each level
        // We need sibling hashes to reconstruct parent nodes during the upward pass
        var siblings = new ReadOnlyMemory<byte>[Depth];
        
        // Check if we're starting with an empty tree (zero-hash at full tree height)
        bool treeIsEmpty = rootHash.Span.SequenceEqual(ZeroHashes[Depth]);
        
        if (!treeIsEmpty)
        {
            var traverseHash = rootHash;
            
            // Traverse down the tree following the bit path to collect siblings
            for (int level = 0; level < Depth; level++)
            {
                // Check if current node is empty (zero-hash for this subtree height)
                // At tree level `level` (0=root), current node represents subtree of height (Depth-level)
                // For example: at level 0, we check ZeroHashes[Depth] (full tree)
                //              at level 1, we check ZeroHashes[Depth-1] (one level down)
                if (traverseHash.Span.SequenceEqual(ZeroHashes[Depth - level]))
                {
                    // Rest of path is empty - fill remaining siblings with appropriate zero-hashes
                    // When building at loop level i (bottom up in reconstruction), sibling at i needs
                    // the zero hash for a subtree of height (Depth-1-i)
                    for (int i = level; i < Depth; i++)
                    {
                        siblings[i] = ZeroHashes[Depth - 1 - i];
                    }
                    break;
                }
                
                // Read the current node from storage
                var nodeBlob = await nodeReader.ReadNodeByHashAsync(traverseHash, cancellationToken);
                if (nodeBlob == null)
                {
                    // Node not found in storage - treat rest of path as empty
                    // This can happen when the tree is sparse or partially populated
                    for (int i = level; i < Depth; i++)
                    {
                        siblings[i] = ZeroHashes[Depth - 1 - i];
                    }
                    break;
                }
                
                var node = SmtNodeSerializer.Deserialize(nodeBlob.SerializedNode);
                
                if (node.NodeType == SmtNodeType.Internal)
                {
                    var internalNode = (SmtInternalNode)node;
                    // Get the sibling hash (opposite direction from where we're going)
                    // If bit path says go right (true), the sibling is the left child
                    // If bit path says go left (false), the sibling is the right child
                    siblings[level] = bitPath[level] ? internalNode.LeftHash : internalNode.RightHash;
                    // Move to the child node indicated by the bit path
                    traverseHash = bitPath[level] ? internalNode.RightHash : internalNode.LeftHash;
                }
                else if (node.NodeType == SmtNodeType.Leaf)
                {
                    // Collision detected: reached an existing leaf before Depth
                    // Need to check if paths diverge within configured depth or need extension
                    var existingLeaf = (SmtLeafNode)node;
                    var existingKeyHash = existingLeaf.KeyHash;
                    var existingBitPath = Hashing.HashUtils.GetBitPath(existingKeyHash.ToArray(), Depth);
                    
                    // Check if they diverge within the remaining levels to Depth
                    int divergenceLevel = -1;
                    for (int i = level; i < Depth; i++)
                    {
                        if (bitPath[i] != existingBitPath[i])
                        {
                            divergenceLevel = i;
                            break;
                        }
                    }
                    
                    if (divergenceLevel != -1)
                    {
                        // Paths diverge within configured depth
                        // Fill siblings up to divergence point with zero-hashes
                        for (int i = level; i < divergenceLevel; i++)
                        {
                            siblings[i] = ZeroHashes[Depth - 1 - i];
                        }
                        // At divergence level, the existing leaf becomes the sibling
                        siblings[divergenceLevel] = traverseHash; // The existing leaf's hash
                        // Fill remaining levels with zero-hashes
                        for (int i = divergenceLevel + 1; i < Depth; i++)
                        {
                            siblings[i] = ZeroHashes[Depth - 1 - i];
                        }
                    }
                    else
                    {
                        // Paths are identical through all Depth levels - need extension beyond Depth
                        // Compute full 256-bit paths to find where they actually diverge
                        var fullNewBitPath = Hashing.HashUtils.GetBitPath(keyHash.ToArray(), 256);
                        var fullExistingBitPath = Hashing.HashUtils.GetBitPath(existingKeyHash.ToArray(), 256);
                        
                        // Find divergence in the full path (beyond Depth)
                        int fullDivergenceLevel = -1;
                        for (int i = Depth; i < 256; i++)
                        {
                            if (fullNewBitPath[i] != fullExistingBitPath[i])
                            {
                                fullDivergenceLevel = i;
                                break;
                            }
                        }
                        
                        if (fullDivergenceLevel == -1)
                        {
                            // Identical keys - this should not happen with proper hashing
                            throw new InvalidOperationException("Cannot insert duplicate key with identical hash");
                        }
                        
                        // Build extension chain from divergence down to Depth
                        // At divergence level, create internal node with both leaves
                        var extensionLeftHash = fullNewBitPath[fullDivergenceLevel] ? existingLeaf.Hash.ToArray() : leafHash.ToArray();
                        var extensionRightHash = fullNewBitPath[fullDivergenceLevel] ? leafHash.ToArray() : existingLeaf.Hash.ToArray();
                        var extensionInternal = CreateInternalNode(extensionLeftHash, extensionRightHash);
                        
                        // Store extension internal node with full path up to divergence level
                        var extensionPath = new bool[fullDivergenceLevel + 1];
                        Array.Copy(fullNewBitPath, 0, extensionPath, 0, fullDivergenceLevel + 1);
                        var extensionBlob = CreateNodeBlob(extensionInternal, extensionPath);
                        nodesToPersist.Add(extensionBlob);
                        
                        var extensionCurrentHash = extensionInternal.Hash;
                        
                        // Build internal nodes from fullDivergenceLevel-1 down to Depth
                        for (int extLevel = fullDivergenceLevel - 1; extLevel >= Depth; extLevel--)
                        {
                            var extGoRight = fullNewBitPath[extLevel];
                            var extLeftHash = extGoRight ? ZeroHashes[0].ToArray() : extensionCurrentHash.ToArray();
                            var extRightHash = extGoRight ? extensionCurrentHash.ToArray() : ZeroHashes[0].ToArray();
                            var extInternal = CreateInternalNode(extLeftHash, extRightHash);
                            
                            var extPath = new bool[extLevel + 1];
                            Array.Copy(fullNewBitPath, 0, extPath, 0, extLevel + 1);
                            var extBlob = CreateNodeBlob(extInternal, extPath);
                            nodesToPersist.Add(extBlob);
                            
                            extensionCurrentHash = extInternal.Hash;
                        }
                        
                        // Fill siblings from level to Depth-1 with zero-hashes
                        for (int i = level; i < Depth - 1; i++)
                        {
                            siblings[i] = ZeroHashes[Depth - 1 - i];
                        }
                        // At level Depth-1, the extension chain root becomes the sibling
                        siblings[Depth - 1] = extensionCurrentHash;
                    }
                    break;
                }
                else
                {
                    // Empty node before expected depth - fill rest with zero-hashes
                    for (int i = level; i < Depth; i++)
                    {
                        siblings[i] = ZeroHashes[Depth - 1 - i];
                    }
                    break;
                }
            }
            
            // After traversal through Depth levels, check if we need to continue beyond Depth
            // This handles extension nodes created by previous collision insertions
            if (!traverseHash.Span.SequenceEqual(ZeroHashes[0]))
            {
                // Non-zero hash at depth boundary - check what type of node it is
                var nodeAtDepth = await nodeReader.ReadNodeByHashAsync(traverseHash, cancellationToken);
                if (nodeAtDepth != null)
                {
                    var node = SmtNodeSerializer.Deserialize(nodeAtDepth.SerializedNode);
                    
                    if (node.NodeType == SmtNodeType.Internal)
                    {
                        // Extension node exists - continue traversing beyond Depth
                        // This handles the case of inserting a third+ key with the same Depth-bit prefix
                        var fullNewBitPath = Hashing.HashUtils.GetBitPath(keyHash.ToArray(), 256);
                        var currentExtensionHash = traverseHash;
                        
                        // Traverse through extension chain until we find a leaf or empty spot
                        for (int extLevel = Depth; extLevel < 256; extLevel++)
                        {
                            var extNodeBlob = await nodeReader.ReadNodeByHashAsync(currentExtensionHash, cancellationToken);
                            if (extNodeBlob == null)
                            {
                                // Node not found - shouldn't happen in a valid tree
                                break;
                            }
                            
                            var extNode = SmtNodeSerializer.Deserialize(extNodeBlob.SerializedNode);
                            
                            if (extNode.NodeType == SmtNodeType.Leaf)
                            {
                                var existingLeaf = (SmtLeafNode)extNode;
                                var existingKeyHash = existingLeaf.KeyHash;
                                
                                // Check if this is an update (same key) or collision (different key)
                                if (existingKeyHash.Span.SequenceEqual(keyHash.Span))
                                {
                                    // Update existing key - replace the leaf at this extension level
                                    // Need to rebuild from this point back up to root
                                    // For simplicity, we'll rebuild the entire extension chain
                                    // Store the extension level where we found the match
                                    var matchLevel = extLevel;
                                    
                                    // Collect siblings from Depth to matchLevel during a fresh traversal
                                    var extensionSiblings = new List<ReadOnlyMemory<byte>>();
                                    var retraverseHash = traverseHash;
                                    for (int retraverseLevel = Depth; retraverseLevel < matchLevel; retraverseLevel++)
                                    {
                                        var retraverseBlob = await nodeReader.ReadNodeByHashAsync(retraverseHash, cancellationToken);
                                        if (retraverseBlob == null) break;
                                        
                                        var retraverseNode = SmtNodeSerializer.Deserialize(retraverseBlob.SerializedNode);
                                        if (retraverseNode.NodeType == SmtNodeType.Internal)
                                        {
                                            var retraverseInternal = (SmtInternalNode)retraverseNode;
                                            var goRight = fullNewBitPath[retraverseLevel];
                                            extensionSiblings.Add(goRight ? retraverseInternal.LeftHash : retraverseInternal.RightHash);
                                            retraverseHash = goRight ? retraverseInternal.RightHash : retraverseInternal.LeftHash;
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                    
                                    // Rebuild extension chain from leaf up to Depth
                                    var rebuildHash = leafHash;
                                    for (int rebuildLevel = extensionSiblings.Count - 1; rebuildLevel >= 0; rebuildLevel--)
                                    {
                                        var actualLevel = Depth + rebuildLevel;
                                        var siblingHash = extensionSiblings[rebuildLevel];
                                        var goRight = fullNewBitPath[actualLevel];
                                        
                                        var leftHash = goRight ? siblingHash.ToArray() : rebuildHash.ToArray();
                                        var rightHash = goRight ? rebuildHash.ToArray() : siblingHash.ToArray();
                                        var newInternal = CreateInternalNode(leftHash, rightHash);
                                        
                                        var internalPath = new bool[actualLevel + 1];
                                        Array.Copy(fullNewBitPath, 0, internalPath, 0, actualLevel + 1);
                                        nodesToPersist.Add(CreateNodeBlob(newInternal, internalPath));
                                        
                                        rebuildHash = newInternal.Hash;
                                    }
                                    
                                    // Now rebuild from Depth to root using collected siblings
                                    var updateCurrentHash = rebuildHash;
                                    for (int level = Depth - 1; level >= 0; level--)
                                    {
                                        var siblingHash = siblings[level];
                                        var goRight = bitPath[level];
                                        
                                        var leftHash = goRight ? siblingHash.ToArray() : updateCurrentHash.ToArray();
                                        var rightHash = goRight ? updateCurrentHash.ToArray() : siblingHash.ToArray();
                                        var newInternal = CreateInternalNode(leftHash, rightHash);
                                        
                                        var internalPath = new bool[level + 1];
                                        Array.Copy(bitPath, 0, internalPath, 0, level + 1);
                                        nodesToPersist.Add(CreateNodeBlob(newInternal, internalPath));
                                        
                                        updateCurrentHash = newInternal.Hash;
                                    }
                                    
                                    return updateCurrentHash;
                                }
                                else
                                {
                                    // Collision with different key in extension chain
                                    // Find where the new key diverges from the existing key
                                    var fullExistingBitPath = Hashing.HashUtils.GetBitPath(existingKeyHash.ToArray(), 256);
                                    
                                    int divergenceLevel = -1;
                                    for (int i = extLevel; i < 256; i++)
                                    {
                                        if (fullNewBitPath[i] != fullExistingBitPath[i])
                                        {
                                            divergenceLevel = i;
                                            break;
                                        }
                                    }
                                    
                                    if (divergenceLevel == -1)
                                    {
                                        // Should not happen - identical hashes
                                        throw new InvalidOperationException("Cannot insert duplicate key with identical hash");
                                    }
                                    
                                    // Build new extension from divergence point
                                    var divLeftHash = fullNewBitPath[divergenceLevel] ? existingLeaf.Hash.ToArray() : leafHash.ToArray();
                                    var divRightHash = fullNewBitPath[divergenceLevel] ? leafHash.ToArray() : existingLeaf.Hash.ToArray();
                                    var divInternal = CreateInternalNode(divLeftHash, divRightHash);
                                    
                                    var divPath = new bool[divergenceLevel + 1];
                                    Array.Copy(fullNewBitPath, 0, divPath, 0, divergenceLevel + 1);
                                    nodesToPersist.Add(CreateNodeBlob(divInternal, divPath));
                                    
                                    var divCurrentHash = divInternal.Hash;
                                    
                                    // Build intermediate nodes from divergence-1 down to extLevel
                                    for (int intermediateLevel = divergenceLevel - 1; intermediateLevel >= extLevel; intermediateLevel--)
                                    {
                                        var goRight = fullNewBitPath[intermediateLevel];
                                        var leftHash = goRight ? ZeroHashes[0].ToArray() : divCurrentHash.ToArray();
                                        var rightHash = goRight ? divCurrentHash.ToArray() : ZeroHashes[0].ToArray();
                                        var intermediateNode = CreateInternalNode(leftHash, rightHash);
                                        
                                        var intermediatePath = new bool[intermediateLevel + 1];
                                        Array.Copy(fullNewBitPath, 0, intermediatePath, 0, intermediateLevel + 1);
                                        nodesToPersist.Add(CreateNodeBlob(intermediateNode, intermediatePath));
                                        
                                        divCurrentHash = intermediateNode.Hash;
                                    }
                                    
                                    // Collect siblings from Depth to extLevel
                                    var extensionSiblings = new List<ReadOnlyMemory<byte>>();
                                    var retraverseHash = traverseHash;
                                    for (int retraverseLevel = Depth; retraverseLevel < extLevel; retraverseLevel++)
                                    {
                                        var retraverseBlob = await nodeReader.ReadNodeByHashAsync(retraverseHash, cancellationToken);
                                        if (retraverseBlob == null) break;
                                        
                                        var retraverseNode = SmtNodeSerializer.Deserialize(retraverseBlob.SerializedNode);
                                        if (retraverseNode.NodeType == SmtNodeType.Internal)
                                        {
                                            var retraverseInternal = (SmtInternalNode)retraverseNode;
                                            var goRight = fullNewBitPath[retraverseLevel];
                                            extensionSiblings.Add(goRight ? retraverseInternal.LeftHash : retraverseInternal.RightHash);
                                            retraverseHash = goRight ? retraverseInternal.RightHash : retraverseInternal.LeftHash;
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                    
                                    // Rebuild extension chain from divCurrentHash up to Depth
                                    var rebuildHash = divCurrentHash;
                                    for (int rebuildLevel = extensionSiblings.Count - 1; rebuildLevel >= 0; rebuildLevel--)
                                    {
                                        var actualLevel = Depth + rebuildLevel;
                                        var siblingHash = extensionSiblings[rebuildLevel];
                                        var goRight = fullNewBitPath[actualLevel];
                                        
                                        var leftHash = goRight ? siblingHash.ToArray() : rebuildHash.ToArray();
                                        var rightHash = goRight ? rebuildHash.ToArray() : siblingHash.ToArray();
                                        var newInternal = CreateInternalNode(leftHash, rightHash);
                                        
                                        var internalPath = new bool[actualLevel + 1];
                                        Array.Copy(fullNewBitPath, 0, internalPath, 0, actualLevel + 1);
                                        nodesToPersist.Add(CreateNodeBlob(newInternal, internalPath));
                                        
                                        rebuildHash = newInternal.Hash;
                                    }
                                    
                                    // Rebuild from Depth to root
                                    var collisionCurrentHash2 = rebuildHash;
                                    for (int level = Depth - 1; level >= 0; level--)
                                    {
                                        var siblingHash = siblings[level];
                                        var goRight = bitPath[level];
                                        
                                        var leftHash = goRight ? siblingHash.ToArray() : collisionCurrentHash2.ToArray();
                                        var rightHash = goRight ? collisionCurrentHash2.ToArray() : siblingHash.ToArray();
                                        var newInternal = CreateInternalNode(leftHash, rightHash);
                                        
                                        var internalPath = new bool[level + 1];
                                        Array.Copy(bitPath, 0, internalPath, 0, level + 1);
                                        nodesToPersist.Add(CreateNodeBlob(newInternal, internalPath));
                                        
                                        collisionCurrentHash2 = newInternal.Hash;
                                    }
                                    
                                    return collisionCurrentHash2;
                                }
                            }
                            else if (extNode.NodeType == SmtNodeType.Internal)
                            {
                                // Continue traversing
                                var internalNode = (SmtInternalNode)extNode;
                                currentExtensionHash = fullNewBitPath[extLevel] ? internalNode.RightHash : internalNode.LeftHash;
                            }
                            else
                            {
                                // Empty node in extension chain - shouldn't happen
                                break;
                            }
                        }
                    }
                    else if (node.NodeType == SmtNodeType.Leaf)
                    {
                        // Collision! An existing leaf with same Depth-bit prefix
                        var existingLeaf = (SmtLeafNode)node;
                        var existingKeyHash = existingLeaf.KeyHash;
                        
                        // Compute full 256-bit paths to find where they diverge
                        var fullNewBitPath = Hashing.HashUtils.GetBitPath(keyHash.ToArray(), 256);
                        var fullExistingBitPath = Hashing.HashUtils.GetBitPath(existingKeyHash.ToArray(), 256);
                        
                        // Find divergence beyond Depth
                        int fullDivergenceLevel = -1;
                        for (int i = Depth; i < 256; i++)
                        {
                            if (fullNewBitPath[i] != fullExistingBitPath[i])
                            {
                                fullDivergenceLevel = i;
                                break;
                            }
                        }
                        
                        if (fullDivergenceLevel == -1)
                        {
                            // Identical key hash - this is an update operation, not an insert
                            // No collision extension needed, just replace the leaf normally
                            // Fall through to normal reconstruction
                        }
                        else
                        {
                            // Build extension chain: internal node at divergence with both leaves
                            var extLeftHash = fullNewBitPath[fullDivergenceLevel] ? existingLeaf.Hash.ToArray() : leafHash.ToArray();
                            var extRightHash = fullNewBitPath[fullDivergenceLevel] ? leafHash.ToArray() : existingLeaf.Hash.ToArray();
                            var extInternal = CreateInternalNode(extLeftHash, extRightHash);
                            
                            var extPath = new bool[fullDivergenceLevel + 1];
                            Array.Copy(fullNewBitPath, 0, extPath, 0, fullDivergenceLevel + 1);
                            nodesToPersist.Add(CreateNodeBlob(extInternal, extPath));
                            
                            var extCurrentHash = extInternal.Hash;
                            
                            // Build intermediate nodes from divergence-1 down to Depth
                            for (int extLevel = fullDivergenceLevel - 1; extLevel >= Depth; extLevel--)
                            {
                                var extGoRight = fullNewBitPath[extLevel];
                                var extLeft = extGoRight ? ZeroHashes[0].ToArray() : extCurrentHash.ToArray();
                                var extRight = extGoRight ? extCurrentHash.ToArray() : ZeroHashes[0].ToArray();
                                var extNode = CreateInternalNode(extLeft, extRight);
                                
                                var extNodePath = new bool[extLevel + 1];
                                Array.Copy(fullNewBitPath, 0, extNodePath, 0, extLevel + 1);
                                nodesToPersist.Add(CreateNodeBlob(extNode, extNodePath));
                                
                                extCurrentHash = extNode.Hash;
                            }
                            
                            // The extension chain root replaces the new leaf in normal reconstruction
                            // Reconstruct the path from extension root to tree root (bottom-up)
                            var collisionCurrentHash = extCurrentHash;
                            for (int level = Depth - 1; level >= 0; level--)
                            {
                                var siblingHash = siblings[level];
                                var goRight = bitPath[level];
                                
                                var leftHash = goRight ? siblingHash.ToArray() : collisionCurrentHash.ToArray();
                                var rightHash = goRight ? collisionCurrentHash.ToArray() : siblingHash.ToArray();
                                var newInternal = CreateInternalNode(leftHash, rightHash);
                                
                                var internalPath = new bool[level + 1];
                                Array.Copy(bitPath, 0, internalPath, 0, level + 1);
                                nodesToPersist.Add(CreateNodeBlob(newInternal, internalPath));
                                
                                collisionCurrentHash = newInternal.Hash;
                            }
                            
                            return collisionCurrentHash;
                        }
                    }
                }
            }
        }
        else
        {
            // Starting with an empty tree - all siblings will be zero-hashes
            // During reconstruction (bottom-up), at each level we need the appropriate zero-hash:
            // - At level (Depth-1): creating node just above leaf, sibling is height 0 (leaf level)
            // - At level (Depth-2): creating node two levels up, sibling is height 1
            // - At level 0 (root): creating root node, sibling is height (Depth-1)
            // Formula: siblings[level] = ZeroHashes[Depth - 1 - level]
            for (int level = 0; level < Depth; level++)
            {
                siblings[level] = ZeroHashes[Depth - 1 - level];
            }
        }
        
        // Phase 2: Reconstruct the path from leaf to root (bottom-up)
        // This creates new internal nodes along the update path using copy-on-write
        var currentHash = leafHash;
        
        // Iterate from bottom (near leaf) to top (root)
        // level ranges from (Depth-1) down to 0
        for (int level = Depth - 1; level >= 0; level--)
        {
            var siblingHash = siblings[level];
            var goRight = bitPath[level];
            
            // Create a new internal node with currentHash and its sibling
            // The bit path determines whether currentHash goes on left or right:
            // - If goRight is false, currentHash is the left child, sibling is right
            // - If goRight is true, currentHash is the right child, sibling is left
            var leftHash = goRight ? siblingHash.ToArray() : currentHash.ToArray();
            var rightHash = goRight ? currentHash.ToArray() : siblingHash.ToArray();
            var newInternal = CreateInternalNode(leftHash, rightHash);
            
            // Add the new internal node to the list of nodes to persist
            // Include the path prefix (from root down to this level) for storage indexing
            var internalPath = new bool[level + 1];
            Array.Copy(bitPath, 0, internalPath, 0, level + 1);
            var internalBlob = CreateNodeBlob(newInternal, internalPath);
            nodesToPersist.Add(internalBlob);
            
            // Move up one level - the newly created internal node becomes the current node
            currentHash = newInternal.Hash;
        }
        
        // After traversing all levels, currentHash is now the new root hash
        return currentHash;
    }

    /// <summary>
    /// Helper method to create a node blob from an SMT node.
    /// </summary>
    private Persistence.SmtNodeBlob CreateNodeBlob(SmtNode node, bool[] path)
    {
        var serializedNode = SmtNodeSerializer.Serialize(node);
        return Persistence.SmtNodeBlob.CreateWithPath(
            node.Hash,
            serializedNode,
            new ReadOnlyMemory<bool>(path));
    }

    /// <summary>
    /// Generates an inclusion proof for a key in the tree.
    /// </summary>
    /// <param name="key">The key to generate a proof for.</param>
    /// <param name="rootHash">The root hash of the tree.</param>
    /// <param name="nodeReader">The node reader for retrieving nodes from storage.</param>
    /// <param name="compress">Whether to compress the proof by omitting zero-hash siblings.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An inclusion proof for the key, or null if the key is not in the tree.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when key or root hash is empty.</exception>
    /// <remarks>
    /// <para>
    /// This method traverses the tree from root to leaf, collecting sibling hashes
    /// along the path. The resulting proof can be used to verify that the key-value
    /// pair exists in the tree without requiring access to the entire tree.
    /// </para>
    /// <para>
    /// If compression is enabled, zero-hash siblings are omitted from the proof and
    /// tracked using a bitmask. This reduces proof size for sparse trees.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var proof = await smt.GenerateInclusionProofAsync(key, rootHash, storage);
    /// if (proof != null)
    /// {
    ///     bool isValid = proof.Verify(rootHash, hashFunction, zeroHashes);
    /// }
    /// </code>
    /// </example>
    public async Task<Proofs.SmtInclusionProof?> GenerateInclusionProofAsync(
        byte[] key,
        ReadOnlyMemory<byte> rootHash,
        Persistence.ISmtNodeReader nodeReader,
        bool compress = false,
        CancellationToken cancellationToken = default)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        if (nodeReader == null)
            throw new ArgumentNullException(nameof(nodeReader));

        if (key.Length == 0)
            throw new ArgumentException("Key cannot be empty.", nameof(key));

        if (rootHash.IsEmpty)
            throw new ArgumentException("Root hash cannot be empty.", nameof(rootHash));

        // Step 1: Compute the bit path and key hash for the target key
        var bitPath = GetBitPath(key);
        var keyHash = HashKey(key);

        // Step 2: Initialize data structures for collecting sibling hashes
        // siblings[i] stores the sibling hash at traversal level i
        var siblings = new byte[Depth][];
        // bitmask tracks which siblings are non-zero (for compression)
        // Each bit corresponds to a verification level (bit 0 = level 0, etc.)
        var bitmask = new byte[(Depth + 7) / 8]; // Ceiling division to get byte count

        // Step 3: Traverse the tree from root to leaf following the bit path
        var currentHash = rootHash;
        for (int level = 0; level <= Depth; level++)
        {
            // Check if we've reached an empty subtree (indicated by zero-hash)
            // At level 0 (root), check against ZeroHashes[Depth] (full tree height)
            // At level i, check against ZeroHashes[Depth-i]
            if (level < Depth && currentHash.Span.SequenceEqual(ZeroHashes[Depth - level]))
            {
                // Empty path encountered - key does not exist, cannot generate inclusion proof
                return null;
            }

            // Read the current node
            var nodeBlob = await nodeReader.ReadNodeByHashAsync(currentHash, cancellationToken);
            if (nodeBlob == null)
            {
                // Node not in storage - key not found
                return null;
            }

            var node = SmtNodeSerializer.Deserialize(nodeBlob.SerializedNode);

            // If we've reached a leaf, check if it matches our key
            if (node.NodeType == SmtNodeType.Leaf)
            {
                var leafNode = (SmtLeafNode)node;
                if (!leafNode.KeyHash.Span.SequenceEqual(keyHash))
                {
                    // Key hash doesn't match - key not in tree
                    return null;
                }

                // Found the leaf! We've collected all siblings along the path.
                // Step 6: Pad remaining levels with zero-hashes (for levels not traversed)
                for (int i = level; i < Depth; i++)
                {
                    // At traversal level i, we need zero-hash for height (Depth - 1 - i)
                    siblings[i] = ZeroHashes[Depth - 1 - i];
                    
                    // Update bitmask if this zero-hash should be included in proof
                    if (!compress || !IsZeroHash(siblings[i], Depth - 1 - i))
                    {
                        // Convert traversal level to verification level for bitmask indexing
                        Proofs.SmtProof.SetBit(bitmask, Depth - 1 - i, true);
                    }
                }

                // Step 7: Build the final siblings array for the proof
                // The siblings array is ordered by verification level (bottom-up, 0 to Depth-1)
                // But we collected them by traversal level (top-down, 0 to Depth-1)
                // So we need to reverse the indexing: verification_level = Depth - 1 - traversal_level
                var proofSiblings = new List<byte[]>();
                if (compress)
                {
                    // For compressed proofs: only include siblings marked in bitmask (non-zero hashes)
                    // This reduces proof size for sparse trees by omitting empty subtree hashes
                    for (int verificationLevel = 0; verificationLevel < Depth; verificationLevel++)
                    {
                        // Check if the bit for this verification level is set in the bitmask
                        bool bitSet = (bitmask[verificationLevel / 8] & (1 << (verificationLevel % 8))) != 0;
                        if (bitSet)
                        {
                            // Convert verification level back to traversal level to get the sibling
                            int traversalLevel = Depth - 1 - verificationLevel;
                            proofSiblings.Add(siblings[traversalLevel]);
                        }
                    }
                }
                else
                {
                    // For uncompressed proofs: include all siblings in verification order (bottom-up)
                    // This is simpler but results in larger proofs
                    for (int verificationLevel = 0; verificationLevel < Depth; verificationLevel++)
                    {
                        int traversalLevel = Depth - 1 - verificationLevel;
                        proofSiblings.Add(siblings[traversalLevel]);
                    }
                }

                // Create the proof
                return new Proofs.SmtInclusionProof(
                    keyHash,
                    leafNode.Value.ToArray(),
                    Depth,
                    HashAlgorithmId,
                    proofSiblings.ToArray(),
                    bitmask,
                    compress);
            }

            // Step 5: If we've reached an internal node, collect sibling and continue traversal
            if (node.NodeType == SmtNodeType.Internal)
            {
                // Safety check: ensure we haven't exceeded the tree depth
                if (level >= Depth)
                {
                    // Traversed too deep - malformed tree structure
                    return null;
                }

                var internalNode = (SmtInternalNode)node;
                
                // Determine traversal direction and collect the sibling hash
                bool goRight = bitPath[level];
                // The sibling is the opposite child from where we're going
                var siblingHash = goRight ? internalNode.LeftHash.ToArray() : internalNode.RightHash.ToArray();
                
                // Store sibling at the current traversal level
                siblings[level] = siblingHash;
                
                // Update bitmask for compression (if enabled)
                // For compressed proofs, we only include non-zero siblings
                // Note: Bitmask indexing uses verification level (Depth - 1 - level)
                // because verification proceeds bottom-up while traversal is top-down
                if (!compress || !IsZeroHash(siblingHash, Depth - 1 - level))
                {
                    // Set bit in bitmask to indicate this sibling should be included
                    Proofs.SmtProof.SetBit(bitmask, Depth - 1 - level, true);
                }

                // Move to the child indicated by the bit path
                currentHash = goRight ? internalNode.RightHash : internalNode.LeftHash;
            }
            else
            {
                // Empty node encountered during traversal - key not found
                return null;
            }
        }

        // If we reach here, we've traversed all levels without finding the leaf
        // This shouldn't happen in a properly structured tree
        return null;
    }

    /// <summary>
    /// Generates a non-inclusion proof for a key that is not in the tree.
    /// </summary>
    /// <param name="key">The key to generate a proof for.</param>
    /// <param name="rootHash">The root hash of the tree.</param>
    /// <param name="nodeReader">The node reader for retrieving nodes from storage.</param>
    /// <param name="compress">Whether to compress the proof by omitting zero-hash siblings.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A non-inclusion proof for the key, or null if the key is in the tree.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when key or root hash is empty.</exception>
    /// <remarks>
    /// <para>
    /// This method generates a proof that a key does not exist in the tree.
    /// Two types of proofs can be generated:
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>Empty Path:</strong> The path to the key leads to an empty subtree.</description></item>
    /// <item><description><strong>Leaf Mismatch:</strong> The path leads to a leaf with a different key hash.</description></item>
    /// </list>
    /// <para>
    /// If compression is enabled, zero-hash siblings are omitted from the proof.
    /// </para>
    /// </remarks>
    public async Task<Proofs.SmtNonInclusionProof?> GenerateNonInclusionProofAsync(
        byte[] key,
        ReadOnlyMemory<byte> rootHash,
        Persistence.ISmtNodeReader nodeReader,
        bool compress = false,
        CancellationToken cancellationToken = default)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        if (nodeReader == null)
            throw new ArgumentNullException(nameof(nodeReader));

        if (key.Length == 0)
            throw new ArgumentException("Key cannot be empty.", nameof(key));

        if (rootHash.IsEmpty)
            throw new ArgumentException("Root hash cannot be empty.", nameof(rootHash));

        // Get the bit path and key hash for the key
        var bitPath = GetBitPath(key);
        var keyHash = HashKey(key);

        // Collect sibling hashes along the path - use array indexed by traversal level
        var siblings = new byte[Depth][];
        var bitmask = new byte[(Depth + 7) / 8];

        // Step 3: Traverse the tree from root following the bit path
        var currentHash = rootHash;
        for (int level = 0; level <= Depth; level++)
        {
            // Check if we've reached an empty subtree (zero-hash)
            // This indicates the key's path leads to an unoccupied area of the tree
            if (level < Depth && currentHash.Span.SequenceEqual(ZeroHashes[Depth - level]))
            {
                // Empty Path Proof Case:
                // The path to the target key consists entirely of empty nodes (zero-hashes)
                // This definitively proves the key does not exist in the tree
                
                // Fill remaining levels with appropriate zero-hashes
                for (int i = level; i < Depth; i++)
                {
                    siblings[i] = ZeroHashes[Depth - 1 - i];
                    if (!compress || !IsZeroHash(siblings[i], Depth - 1 - i))
                    {
                        // Mark non-zero siblings in bitmask (uses verification level indexing)
                        Proofs.SmtProof.SetBit(bitmask, Depth - 1 - i, true);
                    }
                }

                // Build the proof siblings array in verification order (bottom-up)
                var proofSiblings = new List<byte[]>();
                if (compress)
                {
                    // Compressed: only include siblings marked in bitmask
                    for (int verificationLevel = 0; verificationLevel < Depth; verificationLevel++)
                    {
                        bool bitSet = (bitmask[verificationLevel / 8] & (1 << (verificationLevel % 8))) != 0;
                        if (bitSet)
                        {
                            int traversalLevel = Depth - 1 - verificationLevel;
                            proofSiblings.Add(siblings[traversalLevel]);
                        }
                    }
                }
                else
                {
                    // Uncompressed: include all siblings
                    for (int verificationLevel = 0; verificationLevel < Depth; verificationLevel++)
                    {
                        int traversalLevel = Depth - 1 - verificationLevel;
                        proofSiblings.Add(siblings[traversalLevel]);
                    }
                }

                // Create and return empty path non-inclusion proof
                return new Proofs.SmtNonInclusionProof(
                    keyHash,
                    Depth,
                    HashAlgorithmId,
                    proofSiblings.ToArray(),
                    bitmask,
                    compress,
                    Proofs.NonInclusionProofType.EmptyPath);
            }

            // Read the current node
            var nodeBlob = await nodeReader.ReadNodeByHashAsync(currentHash, cancellationToken);
            if (nodeBlob == null)
            {
                // Node not in storage - treat as empty path
                for (int i = level; i < Depth; i++)
                {
                    siblings[i] = ZeroHashes[Depth - 1 - i];
                    if (!compress || !IsZeroHash(siblings[i], Depth - 1 - i))
                    {
                        // Bitmask uses verification level
                        Proofs.SmtProof.SetBit(bitmask, Depth - 1 - i, true);
                    }
                }

                // Build proof siblings in verification order
                var proofSiblings = new List<byte[]>();
                if (compress)
                {
                    for (int verificationLevel = 0; verificationLevel < Depth; verificationLevel++)
                    {
                        bool bitSet = (bitmask[verificationLevel / 8] & (1 << (verificationLevel % 8))) != 0;
                        if (bitSet)
                        {
                            int traversalLevel = Depth - 1 - verificationLevel;
                            proofSiblings.Add(siblings[traversalLevel]);
                        }
                    }
                }
                else
                {
                    for (int verificationLevel = 0; verificationLevel < Depth; verificationLevel++)
                    {
                        int traversalLevel = Depth - 1 - verificationLevel;
                        proofSiblings.Add(siblings[traversalLevel]);
                    }
                }

                return new Proofs.SmtNonInclusionProof(
                    keyHash,
                    Depth,
                    HashAlgorithmId,
                    proofSiblings.ToArray(),
                    bitmask,
                    compress,
                    Proofs.NonInclusionProofType.EmptyPath);
            }

            var node = SmtNodeSerializer.Deserialize(nodeBlob.SerializedNode);

            // Step 4: If we've reached a leaf, check if it matches the target key
            if (node.NodeType == SmtNodeType.Leaf)
            {
                var leafNode = (SmtLeafNode)node;
                if (leafNode.KeyHash.Span.SequenceEqual(keyHash))
                {
                    // Key exists in the tree - cannot generate non-inclusion proof
                    // (Should use GenerateInclusionProofAsync instead)
                    return null;
                }

                // Leaf Mismatch Proof Case:
                // The target key's path leads to a leaf containing a DIFFERENT key
                // This proves the target key doesn't exist because another key occupies its position
                // The conflicting leaf's key hash and value will be included in the proof
                
                // Fill remaining levels with zero-hashes (levels not traversed)
                for (int i = level; i < Depth; i++)
                {
                    siblings[i] = ZeroHashes[Depth - 1 - i];
                    if (!compress || !IsZeroHash(siblings[i], Depth - 1 - i))
                    {
                        // Mark non-zero siblings in bitmask (verification level indexing)
                        Proofs.SmtProof.SetBit(bitmask, Depth - 1 - i, true);
                    }
                }

                // Build proof siblings in verification order
                var proofSiblings = new List<byte[]>();
                if (compress)
                {
                    for (int verificationLevel = 0; verificationLevel < Depth; verificationLevel++)
                    {
                        bool bitSet = (bitmask[verificationLevel / 8] & (1 << (verificationLevel % 8))) != 0;
                        if (bitSet)
                        {
                            int traversalLevel = Depth - 1 - verificationLevel;
                            proofSiblings.Add(siblings[traversalLevel]);
                        }
                    }
                }
                else
                {
                    for (int verificationLevel = 0; verificationLevel < Depth; verificationLevel++)
                    {
                        int traversalLevel = Depth - 1 - verificationLevel;
                        proofSiblings.Add(siblings[traversalLevel]);
                    }
                }

                // Create leaf mismatch proof
                return new Proofs.SmtNonInclusionProof(
                    keyHash,
                    Depth,
                    HashAlgorithmId,
                    proofSiblings.ToArray(),
                    bitmask,
                    compress,
                    Proofs.NonInclusionProofType.LeafMismatch,
                    leafNode.KeyHash.ToArray(),
                    leafNode.Value.ToArray());
            }

            // Must be an internal node - get sibling and continue
            if (node.NodeType == SmtNodeType.Internal)
            {
                var internalNode = (SmtInternalNode)node;
                
                // Determine which child to follow and which is the sibling
                bool goRight = bitPath[level];
                var siblingHash = goRight ? internalNode.LeftHash.ToArray() : internalNode.RightHash.ToArray();
                
                // Store sibling at traversal level
                siblings[level] = siblingHash;
                if (!compress || !IsZeroHash(siblingHash, Depth - 1 - level))
                {
                    // Bitmask uses verification level
                    Proofs.SmtProof.SetBit(bitmask, Depth - 1 - level, true);
                }

                // Move to the next level
                currentHash = goRight ? internalNode.RightHash : internalNode.LeftHash;
            }
            else
            {
                // Empty node - treat as empty path
                for (int i = level; i < Depth; i++)
                {
                    siblings[i] = ZeroHashes[Depth - 1 - i];
                    if (!compress || !IsZeroHash(siblings[i], Depth - 1 - i))
                    {
                        // Bitmask uses verification level
                        Proofs.SmtProof.SetBit(bitmask, Depth - 1 - i, true);
                    }
                }

                // Build proof siblings in verification order
                var proofSiblings = new List<byte[]>();
                if (compress)
                {
                    for (int verificationLevel = 0; verificationLevel < Depth; verificationLevel++)
                    {
                        bool bitSet = (bitmask[verificationLevel / 8] & (1 << (verificationLevel % 8))) != 0;
                        if (bitSet)
                        {
                            int traversalLevel = Depth - 1 - verificationLevel;
                            proofSiblings.Add(siblings[traversalLevel]);
                        }
                    }
                }
                else
                {
                    for (int verificationLevel = 0; verificationLevel < Depth; verificationLevel++)
                    {
                        int traversalLevel = Depth - 1 - verificationLevel;
                        proofSiblings.Add(siblings[traversalLevel]);
                    }
                }

                return new Proofs.SmtNonInclusionProof(
                    keyHash,
                    Depth,
                    HashAlgorithmId,
                    proofSiblings.ToArray(),
                    bitmask,
                    compress,
                    Proofs.NonInclusionProofType.EmptyPath);
            }
        }

        // If we reach here, we've traversed all levels without finding a conclusive result
        // This shouldn't happen in a properly structured tree
        return null;
    }

    /// <summary>
    /// Checks if a hash matches the zero-hash for the specified level.
    /// </summary>
    private bool IsZeroHash(byte[] hash, int level)
    {
        return hash.SequenceEqual(ZeroHashes[level]);
    }
}
