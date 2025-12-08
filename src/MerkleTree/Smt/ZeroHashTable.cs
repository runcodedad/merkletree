using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MerkleTree.Core;
using MerkleTree.Hashing;

namespace MerkleTree.Smt;

/// <summary>
/// Represents a precomputed table of zero-hashes for efficient Sparse Merkle Tree operations.
/// </summary>
/// <remarks>
/// <para>
/// In a Sparse Merkle Tree, most branches are empty (contain no data). Rather than storing
/// empty nodes explicitly, we use a precomputed table of "zero-hashes" that represent empty
/// subtrees at each level of the tree.
/// </para>
/// <para><strong>Zero-Hash Computation:</strong></para>
/// <para>
/// The zero-hash table is computed deterministically as follows:
/// </para>
/// <list type="number">
/// <item><description><strong>Level 0 (leaf)</strong>: Zero hash is Hash(0x00 || empty_value) where empty_value is typically an empty byte array or a specific "null" marker.</description></item>
/// <item><description><strong>Level N (internal)</strong>: Zero hash is Hash(0x01 || zero[N-1] || zero[N-1]) where zero[N-1] is the zero-hash from the level below.</description></item>
/// </list>
/// <para>
/// This computation uses domain-separated hashing (0x00 for leaves, 0x01 for internal nodes)
/// consistent with the Merkle tree hashing strategy to prevent collision attacks.
/// </para>
/// <para><strong>Determinism:</strong></para>
/// <para>
/// Given the same hash algorithm and tree depth, the zero-hash table will be identical
/// across all platforms and implementations. This ensures that Sparse Merkle Tree roots
/// and proofs can be verified consistently.
/// </para>
/// <para><strong>Usage:</strong></para>
/// <para>
/// The zero-hash table is used during tree operations to efficiently represent empty branches
/// without storing or computing them repeatedly. When traversing the tree, if a branch is empty,
/// its hash is looked up from this table instead of being computed.
/// </para>
/// </remarks>
public sealed class ZeroHashTable
{


    private readonly byte[][] _hashes;

    /// <summary>
    /// Gets the hash algorithm identifier used to compute this zero-hash table.
    /// </summary>
    public string HashAlgorithmId { get; }

    /// <summary>
    /// Gets the tree depth (number of levels) for which this zero-hash table was computed.
    /// </summary>
    public int Depth { get; }

    /// <summary>
    /// Gets the hash size in bytes.
    /// </summary>
    public int HashSizeInBytes { get; }

    /// <summary>
    /// Gets the number of hashes in the table (Depth + 1).
    /// </summary>
    /// <remarks>
    /// The table contains one hash per level: levels 0 through Depth inclusive.
    /// </remarks>
    public int Count => _hashes.Length;

    /// <summary>
    /// Gets the zero-hash for the specified level.
    /// </summary>
    /// <param name="level">The tree level (0 = leaf level, Depth = root level).</param>
    /// <returns>The zero-hash for the specified level as a byte array.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="level"/> is out of range.</exception>
    /// <remarks>
    /// The returned array is a copy to prevent external modification of the internal table.
    /// </remarks>
    public byte[] this[int level]
    {
        get
        {
            if (level < 0 || level >= _hashes.Length)
                throw new ArgumentOutOfRangeException(nameof(level), $"Level must be between 0 and {Depth} inclusive.");

            // Return a copy to prevent modification
            return (byte[])_hashes[level].Clone();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ZeroHashTable"/> class.
    /// </summary>
    /// <param name="hashAlgorithmId">The hash algorithm identifier.</param>
    /// <param name="depth">The tree depth.</param>
    /// <param name="hashes">The precomputed zero-hashes (must contain Depth + 1 entries).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hashAlgorithmId"/> or <paramref name="hashes"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="depth"/> is less than 1.</exception>
    private ZeroHashTable(string hashAlgorithmId, int depth, byte[][] hashes)
    {
        if (hashAlgorithmId == null)
            throw new ArgumentNullException(nameof(hashAlgorithmId));

        if (hashes == null)
            throw new ArgumentNullException(nameof(hashes));

        if (string.IsNullOrWhiteSpace(hashAlgorithmId))
            throw new ArgumentException("Hash algorithm ID cannot be empty or whitespace.", nameof(hashAlgorithmId));

        if (depth < 1)
            throw new ArgumentOutOfRangeException(nameof(depth), "Depth must be at least 1.");

        if (hashes.Length != depth + 1)
            throw new ArgumentException($"Hashes array must contain exactly {depth + 1} entries for depth {depth}.", nameof(hashes));

        // Validate all hashes are non-null and same size
        if (hashes.Any(h => h == null))
            throw new ArgumentException("All hashes must be non-null.", nameof(hashes));

        int hashSize = hashes[0].Length;
        if (hashSize < 1)
            throw new ArgumentException("Hash size must be at least 1 byte.", nameof(hashes));

        if (hashes.Any(h => h.Length != hashSize))
            throw new ArgumentException("All hashes must have the same size.", nameof(hashes));

        HashAlgorithmId = hashAlgorithmId;
        Depth = depth;
        HashSizeInBytes = hashSize;
        _hashes = hashes;
    }

    /// <summary>
    /// Computes a zero-hash table for the specified hash function and tree depth.
    /// </summary>
    /// <param name="hashFunction">The hash function to use.</param>
    /// <param name="depth">The tree depth (number of levels).</param>
    /// <returns>A new <see cref="ZeroHashTable"/> instance with computed zero-hashes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hashFunction"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="depth"/> is less than 1.</exception>
    /// <remarks>
    /// <para>
    /// This method computes the zero-hash table deterministically using domain-separated hashing:
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>Level 0</strong>: Hash(0x00 || empty_bytes) where empty_bytes is an empty array</description></item>
    /// <item><description><strong>Level N</strong>: Hash(0x01 || zeroHash[N-1] || zeroHash[N-1]) where zeroHash[N-1] is the zero-hash from the level below</description></item>
    /// </list>
    /// <para>
    /// The computation is guaranteed to be deterministic and will produce identical results
    /// across all platforms for the same hash function and depth.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var hashFunction = new Sha256HashFunction();
    /// var zeroHashes = ZeroHashTable.Compute(hashFunction, depth: 256);
    /// 
    /// // Access zero-hash for level 0 (empty leaf)
    /// byte[] leafZeroHash = zeroHashes[0];
    /// 
    /// // Access zero-hash for level 256 (empty root)
    /// byte[] rootZeroHash = zeroHashes[256];
    /// </code>
    /// </example>
    public static ZeroHashTable Compute(IHashFunction hashFunction, int depth)
    {
        if (hashFunction == null)
            throw new ArgumentNullException(nameof(hashFunction));

        if (depth < 1)
            throw new ArgumentOutOfRangeException(nameof(depth), "Depth must be at least 1.");

        var hashes = new byte[depth + 1][];

        // Level 0: Hash of empty leaf with domain separator
        // Hash(0x00 || empty_array)
        var emptyLeafData = new byte[1] { MerkleTreeBase.LeafDomainSeparator };
        hashes[0] = hashFunction.ComputeHash(emptyLeafData);

        // Levels 1 through depth: Hash of two child zero-hashes with domain separator
        // Hash(0x01 || zeroHash[level-1] || zeroHash[level-1])
        for (int level = 1; level <= depth; level++)
        {
            var childZeroHash = hashes[level - 1];
            var internalNodeData = new byte[1 + childZeroHash.Length * 2];
            internalNodeData[0] = MerkleTreeBase.InternalNodeDomainSeparator;
            Array.Copy(childZeroHash, 0, internalNodeData, 1, childZeroHash.Length);
            Array.Copy(childZeroHash, 0, internalNodeData, 1 + childZeroHash.Length, childZeroHash.Length);
            hashes[level] = hashFunction.ComputeHash(internalNodeData);
        }

        return new ZeroHashTable(hashFunction.Name, depth, hashes);
    }

    /// <summary>
    /// Gets all zero-hashes as a read-only list.
    /// </summary>
    /// <returns>A read-only list of zero-hashes, indexed by level.</returns>
    /// <remarks>
    /// The returned list contains copies of the internal hashes to prevent external modification.
    /// </remarks>
    public IReadOnlyList<byte[]> GetAllHashes()
    {
        return _hashes.Select(h => (byte[])h.Clone()).ToList();
    }

    /// <summary>
    /// Serializes the zero-hash table to a binary format.
    /// </summary>
    /// <returns>A byte array containing the serialized zero-hash table.</returns>
    /// <remarks>
    /// <para><strong>Serialization Format:</strong></para>
    /// <list type="bullet">
    /// <item><description>4 bytes: Depth (little-endian)</description></item>
    /// <item><description>4 bytes: Hash size in bytes (little-endian)</description></item>
    /// <item><description>4 bytes: Hash algorithm ID string length (little-endian)</description></item>
    /// <item><description>N bytes: Hash algorithm ID string (UTF-8)</description></item>
    /// <item><description>For each level (0 to Depth inclusive): hash_size bytes</description></item>
    /// </list>
    /// <para>
    /// The format is platform-independent and uses little-endian byte order for all numeric values.
    /// </para>
    /// </remarks>
    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        // Write depth and hash size
        WriteInt32LittleEndian(writer, Depth);
        WriteInt32LittleEndian(writer, HashSizeInBytes);

        // Write hash algorithm ID
        var hashAlgBytes = Encoding.UTF8.GetBytes(HashAlgorithmId);
        WriteInt32LittleEndian(writer, hashAlgBytes.Length);
        writer.Write(hashAlgBytes);

        // Write all hashes
        foreach (var hash in _hashes)
        {
            writer.Write(hash);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Deserializes a zero-hash table from a binary format.
    /// </summary>
    /// <param name="data">The serialized zero-hash table bytes.</param>
    /// <returns>A new <see cref="ZeroHashTable"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is invalid or corrupted.</exception>
    public static ZeroHashTable Deserialize(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (data.Length < 12)
            throw new ArgumentException("Data is too short to contain valid zero-hash table.", nameof(data));

        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        // Read depth and hash size
        int depth = ReadInt32LittleEndian(reader);
        int hashSize = ReadInt32LittleEndian(reader);

        if (depth < 1)
            throw new ArgumentException("Invalid depth in serialized data.", nameof(data));

        if (hashSize < 1)
            throw new ArgumentException("Invalid hash size in serialized data.", nameof(data));

        // Read hash algorithm ID
        int hashAlgLength = ReadInt32LittleEndian(reader);
        if (hashAlgLength < 1 || hashAlgLength > 256)
            throw new ArgumentException("Invalid hash algorithm ID length.", nameof(data));

        var hashAlgBytes = reader.ReadBytes(hashAlgLength);
        if (hashAlgBytes.Length != hashAlgLength)
            throw new ArgumentException("Unexpected end of stream while reading hash algorithm ID.", nameof(data));

        string hashAlgorithmId = Encoding.UTF8.GetString(hashAlgBytes);

        // Read all hashes
        int expectedHashCount = depth + 1;
        var hashes = new byte[expectedHashCount][];

        for (int i = 0; i < expectedHashCount; i++)
        {
            var hash = reader.ReadBytes(hashSize);
            if (hash.Length != hashSize)
                throw new ArgumentException($"Unexpected end of stream while reading hash at level {i}.", nameof(data));

            hashes[i] = hash;
        }

        return new ZeroHashTable(hashAlgorithmId, depth, hashes);
    }

    /// <summary>
    /// Verifies that this zero-hash table matches the expected values for the given hash function.
    /// </summary>
    /// <param name="hashFunction">The hash function to verify against.</param>
    /// <returns>True if the zero-hash table matches the expected values, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hashFunction"/> is null.</exception>
    /// <remarks>
    /// This method recomputes the zero-hash table and compares it to the stored values.
    /// It is useful for validating deserialized metadata or detecting corruption.
    /// </remarks>
    public bool Verify(IHashFunction hashFunction)
    {
        if (hashFunction == null)
            throw new ArgumentNullException(nameof(hashFunction));

        if (hashFunction.Name != HashAlgorithmId)
            return false;

        if (hashFunction.HashSizeInBytes != HashSizeInBytes)
            return false;

        var expected = Compute(hashFunction, Depth);
        
        for (int level = 0; level <= Depth; level++)
        {
            if (!_hashes[level].AsSpan().SequenceEqual(expected._hashes[level]))
                return false;
        }

        return true;
    }

    private static void WriteInt32LittleEndian(BinaryWriter writer, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        writer.Write(buffer);
    }

    private static int ReadInt32LittleEndian(BinaryReader reader)
    {
        Span<byte> buffer = stackalloc byte[4];
        int bytesRead = reader.Read(buffer);
        if (bytesRead != 4)
            throw new ArgumentException("Unexpected end of stream while reading Int32.");
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }
}
