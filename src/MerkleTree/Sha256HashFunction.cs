using System.Security.Cryptography;

namespace MerkleTree;

/// <summary>
/// SHA-256 hash function implementation using System.Security.Cryptography.
/// </summary>
/// <remarks>
/// <para>
/// SHA-256 is a widely-used cryptographic hash function from the SHA-2 family,
/// producing a 256-bit (32-byte) hash output. It is considered secure for most
/// cryptographic applications and is commonly used in blockchain and Merkle tree implementations.
/// </para>
/// <para><strong>Security Properties:</strong></para>
/// <list type="bullet">
/// <item><description>Collision-resistant: No known practical collision attacks</description></item>
/// <item><description>Preimage-resistant: Computationally infeasible to reverse</description></item>
/// <item><description>Constant-time: .NET's SHA256 implementation uses constant-time operations where feasible</description></item>
/// </list>
/// </remarks>
public class Sha256HashFunction : IHashFunction
{
    /// <inheritdoc/>
    public string Name => "SHA-256";
    
    /// <inheritdoc/>
    public int HashSizeInBytes => 32;
    
    /// <inheritdoc/>
    public byte[] ComputeHash(byte[] data)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(data);
    }
}
