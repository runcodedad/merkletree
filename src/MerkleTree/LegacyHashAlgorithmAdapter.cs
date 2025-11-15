using System.Security.Cryptography;

namespace MerkleTree;

/// <summary>
/// Adapter to bridge legacy System.Security.Cryptography.HashAlgorithmName with IHashFunction.
/// </summary>
/// <remarks>
/// This class provides backward compatibility for existing code using HashAlgorithmName.
/// New code should use dedicated IHashFunction implementations like <see cref="Sha256HashFunction"/> instead.
/// </remarks>
internal class LegacyHashAlgorithmAdapter : IHashFunction
{
    private readonly HashAlgorithmName _algorithmName;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="LegacyHashAlgorithmAdapter"/> class.
    /// </summary>
    /// <param name="algorithmName">The hash algorithm name to adapt.</param>
    public LegacyHashAlgorithmAdapter(HashAlgorithmName algorithmName)
    {
        _algorithmName = algorithmName;
    }
    
    /// <inheritdoc/>
    public string Name => _algorithmName.Name ?? "Unknown";
    
    /// <inheritdoc/>
    public int HashSizeInBytes
    {
        get
        {
            if (_algorithmName == HashAlgorithmName.SHA256)
                return 32;
            else if (_algorithmName == HashAlgorithmName.SHA384)
                return 48;
            else if (_algorithmName == HashAlgorithmName.SHA512)
                return 64;
            else if (_algorithmName == HashAlgorithmName.MD5)
                return 16;
            else if (_algorithmName == HashAlgorithmName.SHA1)
                return 20;
            else
                throw new NotSupportedException($"Hash algorithm '{_algorithmName.Name}' size is not defined.");
        }
    }
    
    /// <inheritdoc/>
    public byte[] ComputeHash(byte[] data)
    {
        using var hasher = CreateHashAlgorithm();
        return hasher.ComputeHash(data);
    }
    
    /// <summary>
    /// Creates an instance of the hash algorithm.
    /// </summary>
    /// <returns>A hash algorithm instance.</returns>
    private System.Security.Cryptography.HashAlgorithm CreateHashAlgorithm()
    {
        if (_algorithmName == HashAlgorithmName.SHA256)
            return SHA256.Create();
        else if (_algorithmName == HashAlgorithmName.SHA384)
            return SHA384.Create();
        else if (_algorithmName == HashAlgorithmName.SHA512)
            return SHA512.Create();
        else if (_algorithmName == HashAlgorithmName.MD5)
            return MD5.Create();
        else if (_algorithmName == HashAlgorithmName.SHA1)
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms - user choice
            return SHA1.Create();
#pragma warning restore CA5350
        else
            throw new NotSupportedException($"Hash algorithm '{_algorithmName.Name}' is not supported.");
    }
}
