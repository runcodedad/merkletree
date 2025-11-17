namespace MerkleTree.Hashing;

/// <summary>
/// Defines an abstraction for cryptographic hash functions used in Merkle tree construction.
/// </summary>
/// <remarks>
/// <para>
/// Hash functions implementing this interface must satisfy the following security properties:
/// </para>
/// <list type="bullet">
/// <item><description><strong>Collision-resistant</strong>: Computationally infeasible to find two different inputs that produce the same hash</description></item>
/// <item><description><strong>Preimage-resistant</strong>: Given a hash output, computationally infeasible to find an input that produces that hash</description></item>
/// <item><description><strong>Constant-time where appropriate</strong>: Implementations should avoid timing side-channels when processing sensitive data</description></item>
/// </list>
/// <para>
/// This abstraction enables swapping hash implementations without changing core Merkle tree logic,
/// and provides metadata for on-disk format identification.
/// </para>
/// </remarks>
public interface IHashFunction
{
    /// <summary>
    /// Gets the name or identifier of the hash function for on-disk format identification.
    /// </summary>
    /// <remarks>
    /// This value is used to identify the hash algorithm used when serializing or
    /// deserializing Merkle tree data structures. It should be unique and stable across versions.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Gets the fixed output size (in bytes) of the hash function.
    /// </summary>
    /// <remarks>
    /// All hash outputs from this function will be exactly this size.
    /// For example, SHA-256 returns 32 bytes, SHA-512 returns 64 bytes.
    /// </remarks>
    int HashSizeInBytes { get; }

    /// <summary>
    /// Computes the hash of the provided data.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The computed hash as a byte array of length <see cref="HashSizeInBytes"/>.</returns>
    /// <remarks>
    /// Implementations should ensure the hash computation is deterministic and follows
    /// the security properties outlined in the interface documentation.
    /// </remarks>
    byte[] ComputeHash(byte[] data);
}
