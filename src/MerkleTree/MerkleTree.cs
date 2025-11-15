using System.Security.Cryptography;

namespace MerkleTree;

/// <summary>
/// Represents a binary Merkle tree structure with support for non-power-of-two leaf counts.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses a domain-separated padding hash strategy for odd leaf counts,
/// ensuring a fully deterministic tree structure based on leaf ordering.
/// </para>
/// <para><strong>Tree Structure:</strong></para>
/// <list type="bullet">
/// <item><description>Binary tree with leaves at Level 0</description></item>
/// <item><description>Parent nodes computed as: Hash(left_child || right_child)</description></item>
/// <item><description>Left-to-right ordering: left child is always processed before right child</description></item>
/// </list>
/// <para><strong>Padding Strategy for Odd Leaf Counts:</strong></para>
/// <para>
/// When a level has an odd number of nodes, the unpaired node is paired with a domain-separated
/// padding hash. The padding hash is computed as Hash("MERKLE_PADDING" || unpaired_node_hash),
/// which ensures:
/// </para>
/// <list type="number">
/// <item><description>Deterministic behavior: same input always produces same tree</description></item>
/// <item><description>Security: padding cannot be confused with legitimate data</description></item>
/// <item><description>Transparency: padding nodes are clearly distinguishable from data nodes</description></item>
/// </list>
/// <para><strong>Orientation Rules:</strong></para>
/// <list type="bullet">
/// <item><description>Leaves are processed left-to-right in the order provided</description></item>
/// <item><description>When computing parent hash: Hash(left_child || right_child)</description></item>
/// <item><description>Unpaired nodes become the left child, padding hash becomes the right child</description></item>
/// </list>
/// </remarks>
public class MerkleTree
{
    private const string PaddingDomainSeparator = "MERKLE_PADDING";
    
    private readonly IHashFunction _hashFunction;
    
    /// <summary>
    /// Gets the root node of the Merkle tree.
    /// </summary>
    public MerkleTreeNode Root { get; }
    
    /// <summary>
    /// Gets the hash algorithm used for computing node hashes (legacy property).
    /// </summary>
    /// <remarks>
    /// This property is maintained for backward compatibility. New code should use <see cref="HashFunction"/> instead.
    /// </remarks>
    [Obsolete("Use HashFunction property instead for access to the pluggable hash function abstraction.")]
    public HashAlgorithmName HashAlgorithm { get; }
    
    /// <summary>
    /// Gets the hash function used for computing node hashes.
    /// </summary>
    public IHashFunction HashFunction => _hashFunction;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="MerkleTree"/> class with the specified leaf data using SHA-256.
    /// </summary>
    /// <param name="leafData">The data for each leaf node. Must contain at least one element.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leafData"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="leafData"/> is empty.</exception>
    public MerkleTree(IEnumerable<byte[]> leafData)
        : this(leafData, new Sha256HashFunction())
    {
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="MerkleTree"/> class with the specified leaf data and hash function.
    /// </summary>
    /// <param name="leafData">The data for each leaf node. Must contain at least one element.</param>
    /// <param name="hashFunction">The hash function to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leafData"/> or <paramref name="hashFunction"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="leafData"/> is empty.</exception>
    public MerkleTree(IEnumerable<byte[]> leafData, IHashFunction hashFunction)
    {
        if (leafData == null)
            throw new ArgumentNullException(nameof(leafData));
        if (hashFunction == null)
            throw new ArgumentNullException(nameof(hashFunction));
        
        var leafList = leafData.ToList();
        if (leafList.Count == 0)
            throw new ArgumentException("Leaf data must contain at least one element.", nameof(leafData));
        
        _hashFunction = hashFunction;
#pragma warning disable CS0618 // Type or member is obsolete
        HashAlgorithm = HashAlgorithmName.SHA256; // For backward compatibility
#pragma warning restore CS0618
        Root = BuildTree(leafList);
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="MerkleTree"/> class with the specified leaf data and hash algorithm (legacy constructor).
    /// </summary>
    /// <param name="leafData">The data for each leaf node. Must contain at least one element.</param>
    /// <param name="hashAlgorithm">The hash algorithm to use. Defaults to SHA256.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leafData"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="leafData"/> is empty.</exception>
    /// <remarks>
    /// This constructor is maintained for backward compatibility. New code should use the constructor
    /// accepting <see cref="IHashFunction"/> instead.
    /// </remarks>
    public MerkleTree(IEnumerable<byte[]> leafData, HashAlgorithmName? hashAlgorithm)
        : this(leafData, CreateHashFunctionFromAlgorithmName(hashAlgorithm ?? HashAlgorithmName.SHA256))
    {
#pragma warning disable CS0618 // Type or member is obsolete
        HashAlgorithm = hashAlgorithm ?? HashAlgorithmName.SHA256;
#pragma warning restore CS0618
    }
    
    /// <summary>
    /// Creates an IHashFunction from a HashAlgorithmName for backward compatibility.
    /// </summary>
    private static IHashFunction CreateHashFunctionFromAlgorithmName(HashAlgorithmName algorithmName)
    {
        if (algorithmName == HashAlgorithmName.SHA256)
            return new Sha256HashFunction();
        
        // For other algorithms, create a legacy adapter
        return new LegacyHashAlgorithmAdapter(algorithmName);
    }
    
    /// <summary>
    /// Builds the Merkle tree from the provided leaf data.
    /// </summary>
    /// <param name="leafData">The data for each leaf node.</param>
    /// <returns>The root node of the constructed tree.</returns>
    private MerkleTreeNode BuildTree(List<byte[]> leafData)
    {
        // Create leaf nodes at Level 0
        var currentLevel = leafData.Select(data => new MerkleTreeNode(ComputeHash(data))).ToList();
        
        // Build tree bottom-up until we reach the root
        while (currentLevel.Count > 1)
        {
            currentLevel = BuildNextLevel(currentLevel);
        }
        
        return currentLevel[0];
    }
    
    /// <summary>
    /// Builds the next level of the tree from the current level.
    /// </summary>
    /// <param name="currentLevel">The nodes at the current level.</param>
    /// <returns>The nodes at the next level (parent level).</returns>
    private List<MerkleTreeNode> BuildNextLevel(List<MerkleTreeNode> currentLevel)
    {
        var nextLevel = new List<MerkleTreeNode>();
        
        for (int i = 0; i < currentLevel.Count; i += 2)
        {
            var left = currentLevel[i];
            MerkleTreeNode right;
            
            // Check if we have an odd number of nodes (unpaired node at the end)
            if (i + 1 < currentLevel.Count)
            {
                // Normal case: pair with the next node
                right = currentLevel[i + 1];
            }
            else
            {
                // Odd case: create padding node using domain-separated hash
                right = CreatePaddingNode(left);
            }
            
            // Create parent node: Hash(left || right)
            var parentHash = ComputeParentHash(left.Hash!, right.Hash!);
            var parentNode = new MerkleTreeNode(parentHash)
            {
                Left = left,
                Right = right
            };
            
            nextLevel.Add(parentNode);
        }
        
        return nextLevel;
    }
    
    /// <summary>
    /// Creates a padding node for an unpaired node using domain-separated hashing.
    /// </summary>
    /// <param name="unpairedNode">The unpaired node that needs padding.</param>
    /// <returns>A padding node with a domain-separated hash.</returns>
    private MerkleTreeNode CreatePaddingNode(MerkleTreeNode unpairedNode)
    {
        // Compute padding hash as Hash("MERKLE_PADDING" || unpaired_node_hash)
        var domainSeparatorBytes = System.Text.Encoding.UTF8.GetBytes(PaddingDomainSeparator);
        var paddingHash = ComputeParentHash(domainSeparatorBytes, unpairedNode.Hash!);
        
        return new MerkleTreeNode(paddingHash);
    }
    
    /// <summary>
    /// Computes the hash of the given data using the configured hash function.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The computed hash.</returns>
    private byte[] ComputeHash(byte[] data)
    {
        return _hashFunction.ComputeHash(data);
    }
    
    /// <summary>
    /// Computes the parent hash from two child hashes: Hash(left || right).
    /// </summary>
    /// <param name="leftHash">The hash of the left child.</param>
    /// <param name="rightHash">The hash of the right child.</param>
    /// <returns>The computed parent hash.</returns>
    private byte[] ComputeParentHash(byte[] leftHash, byte[] rightHash)
    {
        var combinedHash = leftHash.Concat(rightHash).ToArray();
        return ComputeHash(combinedHash);
    }
    
    /// <summary>
    /// Gets the root hash of the Merkle tree.
    /// </summary>
    /// <returns>The hash of the root node.</returns>
    public byte[] GetRootHash()
    {
        return Root.Hash ?? Array.Empty<byte>();
    }
}
