namespace MerkleTree;

/// <summary>
/// Base class providing shared functionality for Merkle tree construction.
/// </summary>
/// <remarks>
/// This class contains the common logic for building Merkle trees including:
/// <list type="bullet">
/// <item><description>Hash computation</description></item>
/// <item><description>Parent hash generation</description></item>
/// <item><description>Domain-separated padding for odd leaf counts</description></item>
/// </list>
/// </remarks>
public abstract class MerkleTreeBase
{
    /// <summary>
    /// The domain separator used for padding hashes.
    /// </summary>
    protected const string PaddingDomainSeparator = "MERKLE_PADDING";

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
    protected byte[] ComputeHash(byte[] data)
    {
        return _hashFunction.ComputeHash(data);
    }

    /// <summary>
    /// Computes the parent hash from two child hashes: Hash(left || right).
    /// </summary>
    /// <param name="leftHash">The hash of the left child.</param>
    /// <param name="rightHash">The hash of the right child.</param>
    /// <returns>The computed parent hash.</returns>
    protected byte[] ComputeParentHash(byte[] leftHash, byte[] rightHash)
    {
        var combinedHash = leftHash.Concat(rightHash).ToArray();
        return ComputeHash(combinedHash);
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
