namespace MerkleTree.Hashing;

/// <summary>
/// Utility methods for hash-related operations in Merkle trees.
/// </summary>
public static class HashUtils
{
    /// <summary>
    /// Derives a bit path from a key (hash or other data) for use in tree traversal.
    /// </summary>
    /// <param name="key">The key to convert to a bit path. Typically a hash output.</param>
    /// <param name="bitLength">The number of bits to extract from the key. Must be positive and not exceed the key size in bits.</param>
    /// <returns>A boolean array where true represents 1 and false represents 0, ordered from most significant to least significant bit.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bitLength"/> is invalid.</exception>
    /// <remarks>
    /// <para>
    /// This method is useful for Sparse Merkle Trees (SMT) where keys are hashed and then
    /// used to determine the path through the tree. Each bit determines whether to traverse
    /// left (0/false) or right (1/true) at each level of the tree.
    /// </para>
    /// <example>
    /// <code>
    /// var hashFunction = new Sha256HashFunction();
    /// var key = Encoding.UTF8.GetBytes("my-key");
    /// var keyHash = hashFunction.ComputeHash(key);
    /// 
    /// // Get first 8 bits as path
    /// var path = HashUtils.GetBitPath(keyHash, 8);
    /// // path[0] = MSB, path[7] = 8th bit
    /// </code>
    /// </example>
    /// </remarks>
    public static bool[] GetBitPath(byte[] key, int bitLength)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        
        if (key.Length == 0)
            throw new ArgumentException("Key cannot be empty.", nameof(key));
        
        if (bitLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(bitLength), "Bit length must be positive.");
        
        int maxBits = key.Length * 8;
        if (bitLength > maxBits)
            throw new ArgumentOutOfRangeException(nameof(bitLength), 
                $"Bit length {bitLength} exceeds key size of {maxBits} bits.");
        
        var path = new bool[bitLength];
        
        for (int i = 0; i < bitLength; i++)
        {
            int byteIndex = i / 8;
            int bitIndex = 7 - (i % 8); // MSB first within each byte
            
            path[i] = (key[byteIndex] & (1 << bitIndex)) != 0;
        }
        
        return path;
    }

    /// <summary>
    /// Derives a bit path from a key (hash or other data) for use in tree traversal,
    /// using the full length of the key.
    /// </summary>
    /// <param name="key">The key to convert to a bit path. Typically a hash output.</param>
    /// <returns>A boolean array where true represents 1 and false represents 0, ordered from most significant to least significant bit.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is empty.</exception>
    /// <remarks>
    /// This is a convenience overload that extracts all bits from the key.
    /// </remarks>
    public static bool[] GetBitPath(byte[] key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        
        if (key.Length == 0)
            throw new ArgumentException("Key cannot be empty.", nameof(key));
        
        return GetBitPath(key, key.Length * 8);
    }
}
