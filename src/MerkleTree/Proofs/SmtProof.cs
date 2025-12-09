using System.Buffers.Binary;
using MerkleTree.Hashing;
using MerkleTree.Smt;

namespace MerkleTree.Proofs;

/// <summary>
/// Base class for Sparse Merkle Tree proofs.
/// </summary>
/// <remarks>
/// <para>
/// SMT proofs enable verification that a key-value pair exists (inclusion proof) or
/// doesn't exist (non-inclusion proof) in a Sparse Merkle Tree without requiring
/// access to the entire tree data.
/// </para>
/// <para>
/// SMT proofs use sibling hashes along the path from leaf to root, similar to
/// traditional Merkle proofs, but with support for sparse trees and optional
/// compression to omit zero-hash siblings.
/// </para>
/// </remarks>
public abstract class SmtProof
{
    /// <summary>
    /// Gets the hash of the key being proven.
    /// </summary>
    /// <remarks>
    /// The key hash is used to derive the bit path through the tree.
    /// </remarks>
    public byte[] KeyHash { get; }

    /// <summary>
    /// Gets the tree depth (number of levels).
    /// </summary>
    public int Depth { get; }

    /// <summary>
    /// Gets the hash algorithm identifier used for the tree.
    /// </summary>
    public string HashAlgorithmId { get; }

    /// <summary>
    /// Gets the sibling hashes along the path from leaf to root.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For uncompressed proofs, this contains exactly <see cref="Depth"/> hashes.
    /// For compressed proofs, zero-hash siblings are omitted and tracked in <see cref="SiblingBitmask"/>.
    /// </para>
    /// </remarks>
    public byte[][] SiblingHashes { get; }

    /// <summary>
    /// Gets the bitmask indicating which siblings are included vs. omitted (zero-hashes).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is used for compressed proofs. Each bit corresponds to a level in the tree:
    /// - Bit set (1): sibling hash is included in <see cref="SiblingHashes"/>
    /// - Bit clear (0): sibling is a zero-hash (omitted)
    /// </para>
    /// <para>
    /// For uncompressed proofs, all bits are set (all siblings included).
    /// </para>
    /// </remarks>
    public byte[] SiblingBitmask { get; }

    /// <summary>
    /// Gets whether this proof uses compression (omits zero-hash siblings).
    /// </summary>
    public bool IsCompressed { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtProof"/> class.
    /// </summary>
    /// <param name="keyHash">The hash of the key being proven.</param>
    /// <param name="depth">The tree depth.</param>
    /// <param name="hashAlgorithmId">The hash algorithm identifier.</param>
    /// <param name="siblingHashes">The sibling hashes along the path.</param>
    /// <param name="siblingBitmask">The bitmask for compressed proofs.</param>
    /// <param name="isCompressed">Whether this proof uses compression.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    protected SmtProof(
        byte[] keyHash,
        int depth,
        string hashAlgorithmId,
        byte[][] siblingHashes,
        byte[] siblingBitmask,
        bool isCompressed)
    {
        if (keyHash == null)
            throw new ArgumentNullException(nameof(keyHash));
        if (hashAlgorithmId == null)
            throw new ArgumentNullException(nameof(hashAlgorithmId));
        if (siblingHashes == null)
            throw new ArgumentNullException(nameof(siblingHashes));
        if (siblingBitmask == null)
            throw new ArgumentNullException(nameof(siblingBitmask));

        if (keyHash.Length == 0)
            throw new ArgumentException("Key hash cannot be empty.", nameof(keyHash));
        
        if (depth < 1)
            throw new ArgumentException("Depth must be at least 1.", nameof(depth));

        if (string.IsNullOrWhiteSpace(hashAlgorithmId))
            throw new ArgumentException("Hash algorithm ID cannot be empty.", nameof(hashAlgorithmId));

        // Validate bitmask length
        int expectedBitmaskLength = (depth + 7) / 8;
        if (siblingBitmask.Length != expectedBitmaskLength)
            throw new ArgumentException(
                $"Sibling bitmask length {siblingBitmask.Length} does not match expected {expectedBitmaskLength} for depth {depth}.",
                nameof(siblingBitmask));

        // Validate sibling hashes
        if (isCompressed)
        {
            // Count set bits in bitmask
            int expectedSiblingCount = CountSetBits(siblingBitmask, depth);
            if (siblingHashes.Length != expectedSiblingCount)
                throw new ArgumentException(
                    $"Compressed proof expects {expectedSiblingCount} sibling hashes based on bitmask, got {siblingHashes.Length}.",
                    nameof(siblingHashes));
        }
        else
        {
            if (siblingHashes.Length != depth)
                throw new ArgumentException(
                    $"Uncompressed proof expects {depth} sibling hashes, got {siblingHashes.Length}.",
                    nameof(siblingHashes));
        }

        // Validate all sibling hashes are non-null and same size
        int? hashSize = null;
        for (int i = 0; i < siblingHashes.Length; i++)
        {
            if (siblingHashes[i] == null)
                throw new ArgumentException($"Sibling hash at index {i} is null.", nameof(siblingHashes));
            
            if (hashSize == null)
                hashSize = siblingHashes[i].Length;
            else if (siblingHashes[i].Length != hashSize.Value)
                throw new ArgumentException(
                    $"Sibling hash at index {i} has inconsistent length {siblingHashes[i].Length}, expected {hashSize.Value}.",
                    nameof(siblingHashes));
        }

        KeyHash = keyHash;
        Depth = depth;
        HashAlgorithmId = hashAlgorithmId;
        SiblingHashes = siblingHashes;
        SiblingBitmask = siblingBitmask;
        IsCompressed = isCompressed;
    }

    /// <summary>
    /// Gets the bit path derived from the key hash for tree traversal.
    /// </summary>
    /// <returns>A boolean array where false=left, true=right.</returns>
    public bool[] GetBitPath()
    {
        return HashUtils.GetBitPath(KeyHash, Depth);
    }

    /// <summary>
    /// Gets all sibling hashes including reconstructed zero-hashes for compressed proofs.
    /// </summary>
    /// <param name="zeroHashes">The zero-hash table for this tree.</param>
    /// <returns>An array of exactly <see cref="Depth"/> sibling hashes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="zeroHashes"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when zero-hash table depth doesn't match proof depth.</exception>
    protected byte[][] GetAllSiblingHashes(ZeroHashTable zeroHashes)
    {
        if (zeroHashes == null)
            throw new ArgumentNullException(nameof(zeroHashes));

        if (zeroHashes.Depth != Depth)
            throw new ArgumentException(
                $"Zero-hash table depth {zeroHashes.Depth} does not match proof depth {Depth}.",
                nameof(zeroHashes));

        if (!IsCompressed)
        {
            // All siblings are already included
            return SiblingHashes;
        }

        // Reconstruct full sibling array with zero-hashes
        var allSiblings = new byte[Depth][];
        int siblingIndex = 0;

        for (int level = 0; level < Depth; level++)
        {
            bool isIncluded = GetBit(SiblingBitmask, level);
            if (isIncluded)
            {
                allSiblings[level] = SiblingHashes[siblingIndex];
                siblingIndex++;
            }
            else
            {
                allSiblings[level] = zeroHashes[level];
            }
        }

        return allSiblings;
    }

    /// <summary>
    /// Counts the number of set bits in the bitmask up to the specified depth.
    /// </summary>
    private static int CountSetBits(byte[] bitmask, int depth)
    {
        int count = 0;
        for (int i = 0; i < depth; i++)
        {
            if (GetBit(bitmask, i))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Gets a specific bit from the bitmask.
    /// </summary>
    protected static bool GetBit(byte[] bitmask, int bitIndex)
    {
        int byteIndex = bitIndex / 8;
        int bitPosition = bitIndex % 8;
        return (bitmask[byteIndex] & (1 << bitPosition)) != 0;
    }

    /// <summary>
    /// Sets a specific bit in the bitmask.
    /// </summary>
    public static void SetBit(byte[] bitmask, int bitIndex, bool value)
    {
        int byteIndex = bitIndex / 8;
        int bitPosition = bitIndex % 8;
        if (value)
            bitmask[byteIndex] |= (byte)(1 << bitPosition);
        else
            bitmask[byteIndex] &= (byte)~(1 << bitPosition);
    }

    /// <summary>
    /// Verifies this proof against a given root hash using the specified hash function and zero-hash table.
    /// </summary>
    /// <param name="expectedRootHash">The expected root hash to verify against.</param>
    /// <param name="hashFunction">The hash function to use for verification.</param>
    /// <param name="zeroHashes">The zero-hash table for this tree.</param>
    /// <returns>True if the proof is valid; otherwise, false.</returns>
    public abstract bool Verify(byte[] expectedRootHash, IHashFunction hashFunction, ZeroHashTable zeroHashes);
}
