using System.Security.Cryptography;

namespace MerkleTree;

/// <summary>
/// SHA-512 hash function implementation using System.Security.Cryptography.
/// </summary>
/// <remarks>
/// <para>
/// SHA-512 is a cryptographic hash function from the SHA-2 family,
/// producing a 512-bit (64-byte) hash output. It provides higher security margins
/// than SHA-256 at the cost of larger hash sizes.
/// </para>
/// <para><strong>Security Properties:</strong></para>
/// <list type="bullet">
/// <item><description>Collision-resistant: No known practical collision attacks</description></item>
/// <item><description>Preimage-resistant: Computationally infeasible to reverse</description></item>
/// <item><description>Constant-time: .NET's SHA512 implementation uses constant-time operations where feasible</description></item>
/// </list>
/// </remarks>
public class Sha512HashFunction : IHashFunction
{
    /// <inheritdoc/>
    public string Name => "SHA-512";

    /// <inheritdoc/>
    public int HashSizeInBytes => 64;

    /// <inheritdoc/>
    public byte[] ComputeHash(byte[] data)
    {
        using var sha512 = SHA512.Create();
        return sha512.ComputeHash(data);
    }
}
