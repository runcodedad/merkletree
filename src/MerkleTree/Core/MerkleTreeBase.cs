using MerkleTree.Hashing;

namespace MerkleTree.Core;

/// <summary>
/// Base class providing shared functionality for Merkle tree construction.
/// </summary>
/// <remarks>
/// This class contains the common logic for building Merkle trees including:
/// <list type="bullet">
/// <item><description>Hash computation</description></item>
/// <item><description>Parent hash generation</description></item>
/// <item><description>Domain-separated hashing for leaf and internal nodes</description></item>
/// <item><description>Domain-separated padding for odd leaf counts</description></item>
/// </list>
/// <para><strong>Domain Separation Strategy:</strong></para>
/// <para>
/// To prevent collision attacks where an attacker could construct data that produces
/// the same hash as an internal node, this implementation uses domain separation:
/// </para>
/// <list type="bullet">
/// <item><description>Leaf hashes: Hash(0x00 || leaf_data)</description></item>
/// <item><description>Internal node hashes: Hash(0x01 || left_hash || right_hash)</description></item>
/// <item><description>Padding hashes: Hash("MERKLE_PADDING" || unpaired_hash)</description></item>
/// </list>
/// <para>
/// This ensures that:
/// 1. A leaf hash can never equal an internal node hash
/// 2. An internal node hash can never equal a padding hash
/// 3. A leaf hash can never equal a padding hash
/// </para>
/// </remarks>
public abstract class MerkleTreeBase
{
    /// <summary>
    /// Domain separator byte for leaf node hashing (0x00).
    /// </summary>
    /// <remarks>
    /// This prefix is prepended to leaf data before hashing to distinguish leaf hashes
    /// from internal node hashes, preventing collision attacks.
    /// </remarks>
    public const byte LeafDomainSeparator = 0x00;

    /// <summary>
    /// Domain separator byte for internal node hashing (0x01).
    /// </summary>
    /// <remarks>
    /// This prefix is prepended when hashing two child nodes together to create a parent hash,
    /// distinguishing internal node hashes from leaf hashes and padding hashes.
    /// </remarks>
    public const byte InternalNodeDomainSeparator = 0x01;

    /// <summary>
    /// The domain separator used for padding hashes.
    /// </summary>
    public const string PaddingDomainSeparator = "MERKLE_PADDING";

    /// <summary>
    /// The hash function used for computing node hashes.
    /// </summary>
    protected readonly IHashFunction _hashFunction;

    /// <summary>
    /// Gets the hash function used for computing node hashes.
    /// </summary>
    public IHashFunction HashFunction => _hashFunction;

    /// <summary>
    /// Initializes a new instance of the <see cref="MerkleTreeBase"/> class.
    /// </summary>
    /// <param name="hashFunction">The hash function to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hashFunction"/> is null.</exception>
    protected MerkleTreeBase(IHashFunction hashFunction)
    {
        _hashFunction = hashFunction ?? throw new ArgumentNullException(nameof(hashFunction));
    }

    /// <summary>
    /// Computes the hash of the given data using the configured hash function.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The computed hash.</returns>
    /// <remarks>
    /// This is the raw hash function. For Merkle tree operations, prefer using
    /// <see cref="ComputeLeafHash"/> or <see cref="ComputeInternalNodeHash"/> to ensure
    /// proper domain separation.
    /// </remarks>
    protected byte[] ComputeHash(byte[] data)
    {
        return _hashFunction.ComputeHash(data);
    }

    /// <summary>
    /// Computes a leaf hash with domain separation.
    /// </summary>
    /// <param name="leafData">The leaf data to hash.</param>
    /// <returns>The computed leaf hash as Hash(0x00 || leafData).</returns>
    /// <remarks>
    /// Domain separation ensures that leaf hashes cannot collide with internal node hashes
    /// or padding hashes, preventing certain types of attacks.
    /// </remarks>
    protected byte[] ComputeLeafHash(byte[] leafData)
    {
        var prefixedData = new byte[leafData.Length + 1];
        prefixedData[0] = LeafDomainSeparator;
        Array.Copy(leafData, 0, prefixedData, 1, leafData.Length);
        return _hashFunction.ComputeHash(prefixedData);
    }

    /// <summary>
    /// Computes an internal node hash with domain separation from two child hashes.
    /// </summary>
    /// <param name="leftHash">The hash of the left child.</param>
    /// <param name="rightHash">The hash of the right child.</param>
    /// <returns>The computed internal node hash as Hash(0x01 || leftHash || rightHash).</returns>
    /// <remarks>
    /// Domain separation ensures that internal node hashes cannot collide with leaf hashes
    /// or padding hashes, preventing certain types of attacks.
    /// </remarks>
    protected byte[] ComputeInternalNodeHash(byte[] leftHash, byte[] rightHash)
    {
        var combinedData = new byte[1 + leftHash.Length + rightHash.Length];
        combinedData[0] = InternalNodeDomainSeparator;
        Array.Copy(leftHash, 0, combinedData, 1, leftHash.Length);
        Array.Copy(rightHash, 0, combinedData, 1 + leftHash.Length, rightHash.Length);
        return _hashFunction.ComputeHash(combinedData);
    }

    /// <summary>
    /// Computes the parent hash from two child hashes: Hash(0x01 || left || right).
    /// </summary>
    /// <param name="leftHash">The hash of the left child.</param>
    /// <param name="rightHash">The hash of the right child.</param>
    /// <returns>The computed parent hash.</returns>
    /// <remarks>
    /// This method delegates to <see cref="ComputeInternalNodeHash"/> to ensure
    /// proper domain separation for internal nodes.
    /// </remarks>
    protected byte[] ComputeParentHash(byte[] leftHash, byte[] rightHash)
    {
        return ComputeInternalNodeHash(leftHash, rightHash);
    }

    /// <summary>
    /// Creates a padding hash for an unpaired node using domain-separated hashing.
    /// </summary>
    /// <param name="unpairedHash">The hash of the unpaired node.</param>
    /// <returns>A padding hash computed as Hash("MERKLE_PADDING" || unpaired_hash).</returns>
    protected byte[] CreatePaddingHash(byte[] unpairedHash)
    {
        var domainSeparatorBytes = System.Text.Encoding.UTF8.GetBytes(PaddingDomainSeparator);
        return ComputeParentHash(domainSeparatorBytes, unpairedHash);
    }
}
