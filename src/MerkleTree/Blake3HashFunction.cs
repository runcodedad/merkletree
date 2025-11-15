#if NET10_0_OR_GREATER
using Blake3;
#endif

namespace MerkleTree;

#if NET10_0_OR_GREATER
/// <summary>
/// BLAKE3 hash function implementation.
/// </summary>
/// <remarks>
/// <para>
/// BLAKE3 is a cryptographic hash function that is significantly faster than MD5, SHA-1, SHA-2, and SHA-3,
/// while providing high security guarantees. It produces a 256-bit (32-byte) hash output by default.
/// </para>
/// <para><strong>Security Properties:</strong></para>
/// <list type="bullet">
/// <item><description>Collision-resistant: Based on the secure BLAKE family of hash functions</description></item>
/// <item><description>Preimage-resistant: Computationally infeasible to reverse</description></item>
/// <item><description>High performance: Significantly faster than SHA-2 family</description></item>
/// <item><description>Parallelizable: Can take advantage of multi-core processors</description></item>
/// </list>
/// </remarks>
public class Blake3HashFunction : IHashFunction
{
    /// <inheritdoc/>
    public string Name => "BLAKE3";
    
    /// <inheritdoc/>
    public int HashSizeInBytes => 32;
    
    /// <inheritdoc/>
    public byte[] ComputeHash(byte[] data)
    {
        using var hasher = Hasher.New();
        hasher.Update(data);
        return hasher.Finalize().AsSpan().ToArray();
    }
}
#else
/// <summary>
/// BLAKE3 hash function (not available on .NET Standard 2.1).
/// </summary>
/// <remarks>
/// BLAKE3 support requires .NET 10.0 or later. This placeholder throws <see cref="PlatformNotSupportedException"/>.
/// </remarks>
public class Blake3HashFunction : IHashFunction
{
    /// <inheritdoc/>
    public string Name => "BLAKE3";
    
    /// <inheritdoc/>
    public int HashSizeInBytes => 32;
    
    /// <inheritdoc/>
    public byte[] ComputeHash(byte[] data)
    {
        throw new PlatformNotSupportedException(
            "BLAKE3 hash function is not available on .NET Standard 2.1. " +
            "Please use .NET 10.0 or later, or use Sha256HashFunction instead.");
    }
}
#endif
