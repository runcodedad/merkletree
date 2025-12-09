using System.Buffers.Binary;
using MerkleTree.Hashing;
using MerkleTree.Smt;

namespace MerkleTree.Proofs;

/// <summary>
/// Represents a Sparse Merkle Tree non-inclusion proof.
/// </summary>
/// <remarks>
/// <para>
/// A non-inclusion proof demonstrates that a specific key does not exist in the tree.
/// This is proven in one of two ways:
/// </para>
/// <list type="number">
/// <item><description><strong>Empty path proof</strong>: The path to the key leads to an empty subtree (all zero-hashes).</description></item>
/// <item><description><strong>Leaf mismatch proof</strong>: The path leads to a leaf with a different key hash.</description></item>
/// </list>
/// <para>
/// Non-inclusion proofs support optional compression where zero-hash siblings are omitted
/// and reconstructed during verification using a zero-hash table.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Generate non-inclusion proof
/// var proof = await smt.GenerateNonInclusionProofAsync(key, storage);
/// 
/// // Verify proof
/// bool isValid = proof.Verify(rootHash, hashFunction, zeroHashes);
/// </code>
/// </example>
public sealed class SmtNonInclusionProof : SmtProof
{
    /// <summary>
    /// Gets the type of non-inclusion proof.
    /// </summary>
    public NonInclusionProofType ProofType { get; }

    /// <summary>
    /// Gets the key hash of the conflicting leaf (only for leaf mismatch proofs).
    /// </summary>
    /// <remarks>
    /// For empty path proofs, this is null.
    /// For leaf mismatch proofs, this contains the key hash of the existing leaf at the target path.
    /// </remarks>
    public byte[]? ConflictingKeyHash { get; }

    /// <summary>
    /// Gets the value of the conflicting leaf (only for leaf mismatch proofs).
    /// </summary>
    /// <remarks>
    /// For empty path proofs, this is null.
    /// For leaf mismatch proofs, this contains the value of the existing leaf at the target path.
    /// </remarks>
    public byte[]? ConflictingValue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtNonInclusionProof"/> class.
    /// </summary>
    /// <param name="keyHash">The hash of the key being proven absent.</param>
    /// <param name="depth">The tree depth.</param>
    /// <param name="hashAlgorithmId">The hash algorithm identifier.</param>
    /// <param name="siblingHashes">The sibling hashes along the path.</param>
    /// <param name="siblingBitmask">The bitmask for compressed proofs.</param>
    /// <param name="isCompressed">Whether this proof uses compression.</param>
    /// <param name="proofType">The type of non-inclusion proof.</param>
    /// <param name="conflictingKeyHash">The key hash of the conflicting leaf (for leaf mismatch proofs).</param>
    /// <param name="conflictingValue">The value of the conflicting leaf (for leaf mismatch proofs).</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    public SmtNonInclusionProof(
        byte[] keyHash,
        int depth,
        string hashAlgorithmId,
        byte[][] siblingHashes,
        byte[] siblingBitmask,
        bool isCompressed,
        NonInclusionProofType proofType,
        byte[]? conflictingKeyHash = null,
        byte[]? conflictingValue = null)
        : base(keyHash, depth, hashAlgorithmId, siblingHashes, siblingBitmask, isCompressed)
    {
        // Validate proof type consistency
        if (proofType == NonInclusionProofType.LeafMismatch)
        {
            if (conflictingKeyHash == null)
                throw new ArgumentNullException(nameof(conflictingKeyHash), 
                    "Conflicting key hash is required for leaf mismatch proofs.");
            if (conflictingValue == null)
                throw new ArgumentNullException(nameof(conflictingValue),
                    "Conflicting value is required for leaf mismatch proofs.");
        }
        else if (proofType == NonInclusionProofType.EmptyPath)
        {
            if (conflictingKeyHash != null || conflictingValue != null)
                throw new ArgumentException(
                    "Conflicting key hash and value must be null for empty path proofs.");
        }

        ProofType = proofType;
        ConflictingKeyHash = conflictingKeyHash;
        ConflictingValue = conflictingValue;
    }

    /// <summary>
    /// Verifies this non-inclusion proof against a given root hash.
    /// </summary>
    /// <param name="expectedRootHash">The expected root hash to verify against.</param>
    /// <param name="hashFunction">The hash function to use for verification.</param>
    /// <param name="zeroHashes">The zero-hash table for this tree.</param>
    /// <returns>True if the proof is valid and produces the expected root hash; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when hash algorithm or depth mismatch.</exception>
    /// <remarks>
    /// <para>
    /// Verification process depends on the proof type:
    /// </para>
    /// <para><strong>Empty Path Proof:</strong></para>
    /// <list type="number">
    /// <item><description>Start with zero-hash at level 0 (empty leaf)</description></item>
    /// <item><description>Traverse up the tree, combining with sibling hashes</description></item>
    /// <item><description>Verify computed root matches expected root</description></item>
    /// </list>
    /// <para><strong>Leaf Mismatch Proof:</strong></para>
    /// <list type="number">
    /// <item><description>Compute leaf hash of the conflicting leaf</description></item>
    /// <item><description>Verify the conflicting key hash differs from the target key hash</description></item>
    /// <item><description>Traverse up the tree, combining with sibling hashes</description></item>
    /// <item><description>Verify computed root matches expected root</description></item>
    /// </list>
    /// </remarks>
    public override bool Verify(byte[] expectedRootHash, IHashFunction hashFunction, ZeroHashTable zeroHashes)
    {
        if (expectedRootHash == null)
            throw new ArgumentNullException(nameof(expectedRootHash));
        if (hashFunction == null)
            throw new ArgumentNullException(nameof(hashFunction));
        if (zeroHashes == null)
            throw new ArgumentNullException(nameof(zeroHashes));

        if (hashFunction.Name != HashAlgorithmId)
            throw new ArgumentException(
                $"Hash function '{hashFunction.Name}' does not match proof algorithm '{HashAlgorithmId}'.",
                nameof(hashFunction));

        if (zeroHashes.Depth != Depth)
            throw new ArgumentException(
                $"Zero-hash table depth {zeroHashes.Depth} does not match proof depth {Depth}.",
                nameof(zeroHashes));

        // Get all sibling hashes (including reconstructed zero-hashes for compressed proofs)
        var allSiblings = GetAllSiblingHashes(zeroHashes);

        byte[] currentHash;

        if (ProofType == NonInclusionProofType.EmptyPath)
        {
            // Start with zero-hash at level 0 (empty leaf)
            currentHash = zeroHashes[0];
        }
        else // LeafMismatch
        {
            // Verify the conflicting key hash is different from the target key hash
            // Note: ConflictingKeyHash and ConflictingValue are guaranteed non-null for LeafMismatch type
            if (ConflictingKeyHash!.SequenceEqual(KeyHash))
                return false; // Key hashes should be different for a valid non-inclusion proof

            // Compute leaf hash of the conflicting leaf: Hash(0x00 || conflictingKeyHash || conflictingValue)
            var conflictingValue = ConflictingValue!; // Already validated in constructor
            var leafData = new byte[1 + ConflictingKeyHash.Length + conflictingValue.Length];
            leafData[0] = 0x00; // Leaf domain separator
            Array.Copy(ConflictingKeyHash, 0, leafData, 1, ConflictingKeyHash.Length);
            Array.Copy(conflictingValue, 0, leafData, 1 + ConflictingKeyHash.Length, conflictingValue.Length);
            currentHash = hashFunction.ComputeHash(leafData);
        }

        // Get bit path for tree traversal (using the target key hash, not the conflicting one)
        var bitPath = GetBitPath();

        // Traverse from leaf to root
        for (int level = 0; level < Depth; level++)
        {
            var siblingHash = allSiblings[level];
            var isRight = bitPath[level];

            // Compute parent hash with domain separation: Hash(0x01 || left || right)
            byte[] combinedData;
            if (isRight)
            {
                // Current node is on the right, sibling is on the left
                combinedData = new byte[1 + siblingHash.Length + currentHash.Length];
                combinedData[0] = 0x01; // Internal node domain separator
                Array.Copy(siblingHash, 0, combinedData, 1, siblingHash.Length);
                Array.Copy(currentHash, 0, combinedData, 1 + siblingHash.Length, currentHash.Length);
            }
            else
            {
                // Current node is on the left, sibling is on the right
                combinedData = new byte[1 + currentHash.Length + siblingHash.Length];
                combinedData[0] = 0x01; // Internal node domain separator
                Array.Copy(currentHash, 0, combinedData, 1, currentHash.Length);
                Array.Copy(siblingHash, 0, combinedData, 1 + currentHash.Length, siblingHash.Length);
            }

            currentHash = hashFunction.ComputeHash(combinedData);
        }

        // Compare computed root with expected root
        return currentHash.SequenceEqual(expectedRootHash);
    }

    /// <summary>
    /// Serializes this non-inclusion proof to binary format.
    /// </summary>
    /// <returns>A byte array containing the serialized proof.</returns>
    /// <remarks>
    /// <para>
    /// Format specification:
    /// - Version (1 byte): Format version (currently 1)
    /// - Proof Type (1 byte): 0x02 for non-inclusion proof
    /// - Flags (1 byte): Bit 0 = IsCompressed, Bit 1 = IsLeafMismatch
    /// - Depth (4 bytes): Tree depth, little-endian
    /// - Hash Algorithm ID Length (4 bytes): Length of algorithm name, little-endian
    /// - Hash Algorithm ID (variable): UTF-8 encoded algorithm name
    /// - Key Hash Length (4 bytes): Length of key hash, little-endian
    /// - Key Hash (variable): Raw key hash bytes
    /// - [If LeafMismatch] Conflicting Key Hash Length (4 bytes)
    /// - [If LeafMismatch] Conflicting Key Hash (variable)
    /// - [If LeafMismatch] Conflicting Value Length (4 bytes)
    /// - [If LeafMismatch] Conflicting Value (variable)
    /// - Sibling Bitmask Length (4 bytes): Length of bitmask, little-endian
    /// - Sibling Bitmask (variable): Bitmask bytes
    /// - Sibling Count (4 bytes): Number of sibling hashes, little-endian
    /// - Sibling Hashes (variable): Concatenated hash bytes
    /// </para>
    /// </remarks>
    public byte[] Serialize()
    {
        var algorithmIdBytes = System.Text.Encoding.UTF8.GetBytes(HashAlgorithmId);
        int hashSize = SiblingHashes.Length > 0 ? SiblingHashes[0].Length : KeyHash.Length;

        // Calculate total size
        int totalSize = 1 + // version
                       1 + // proof type
                       1 + // flags
                       4 + // depth
                       4 + algorithmIdBytes.Length + // algorithm ID length + data
                       4 + KeyHash.Length; // key hash length + data

        // Add conflicting leaf data if leaf mismatch proof
        if (ProofType == NonInclusionProofType.LeafMismatch)
        {
            totalSize += 4 + ConflictingKeyHash!.Length + // conflicting key hash length + data
                        4 + ConflictingValue!.Length; // conflicting value length + data
        }

        totalSize += 4 + SiblingBitmask.Length + // bitmask length + data
                    4 + // sibling count
                    (SiblingHashes.Length * hashSize); // sibling hashes

        var result = new byte[totalSize];
        int offset = 0;

        // Write version
        result[offset++] = 1;

        // Write proof type (0x02 = non-inclusion)
        result[offset++] = 0x02;

        // Write flags
        byte flags = 0;
        if (IsCompressed) flags |= 0x01;
        if (ProofType == NonInclusionProofType.LeafMismatch) flags |= 0x02;
        result[offset++] = flags;

        // Write depth
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), Depth);
        offset += 4;

        // Write hash algorithm ID
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), algorithmIdBytes.Length);
        offset += 4;
        algorithmIdBytes.CopyTo(result, offset);
        offset += algorithmIdBytes.Length;

        // Write key hash
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), KeyHash.Length);
        offset += 4;
        KeyHash.CopyTo(result, offset);
        offset += KeyHash.Length;

        // Write conflicting leaf data if leaf mismatch
        if (ProofType == NonInclusionProofType.LeafMismatch)
        {
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), ConflictingKeyHash!.Length);
            offset += 4;
            ConflictingKeyHash.CopyTo(result, offset);
            offset += ConflictingKeyHash.Length;

            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), ConflictingValue!.Length);
            offset += 4;
            ConflictingValue.CopyTo(result, offset);
            offset += ConflictingValue.Length;
        }

        // Write sibling bitmask
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), SiblingBitmask.Length);
        offset += 4;
        SiblingBitmask.CopyTo(result, offset);
        offset += SiblingBitmask.Length;

        // Write sibling count
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), SiblingHashes.Length);
        offset += 4;

        // Write sibling hashes
        for (int i = 0; i < SiblingHashes.Length; i++)
        {
            SiblingHashes[i].CopyTo(result, offset);
            offset += hashSize;
        }

        return result;
    }

    /// <summary>
    /// Deserializes a non-inclusion proof from binary format.
    /// </summary>
    /// <param name="data">The serialized proof data.</param>
    /// <returns>A new <see cref="SmtNonInclusionProof"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    /// <exception cref="Exceptions.MalformedProofException">Thrown when data is invalid or corrupted.</exception>
    public static SmtNonInclusionProof Deserialize(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (data.Length < 3)
            throw new Exceptions.MalformedProofException(
                "Data is too short to be a valid serialized non-inclusion proof.",
                "INSUFFICIENT_DATA");

        int offset = 0;

        // Read version
        byte version = data[offset++];
        if (version != 1)
            throw new Exceptions.MalformedProofException(
                $"Unsupported serialization version: {version}. Expected version 1.",
                "UNSUPPORTED_VERSION");

        // Read proof type
        byte proofTypeByte = data[offset++];
        if (proofTypeByte != 0x02)
            throw new Exceptions.MalformedProofException(
                $"Invalid proof type: 0x{proofTypeByte:X2}. Expected 0x02 for non-inclusion proof.",
                "INVALID_PROOF_TYPE");

        // Read flags
        byte flags = data[offset++];
        bool isCompressed = (flags & 0x01) != 0;
        bool isLeafMismatch = (flags & 0x02) != 0;
        var proofType = isLeafMismatch ? NonInclusionProofType.LeafMismatch : NonInclusionProofType.EmptyPath;

        if (offset + 4 > data.Length)
            throw new Exceptions.MalformedProofException(
                "Data is too short to contain depth field.",
                "INSUFFICIENT_DATA");

        // Read depth
        int depth = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
        offset += 4;
        if (depth < 1)
            throw new Exceptions.MalformedProofException(
                $"Invalid depth: {depth}. Must be at least 1.",
                "INVALID_DEPTH");

        // Read hash algorithm ID
        if (offset + 4 > data.Length)
            throw new Exceptions.MalformedProofException(
                "Data is too short to contain hash algorithm ID length.",
                "INSUFFICIENT_DATA");

        int algorithmIdLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
        offset += 4;
        if (algorithmIdLength < 0 || offset + algorithmIdLength > data.Length)
            throw new Exceptions.MalformedProofException(
                "Invalid hash algorithm ID length or insufficient data.",
                "INVALID_ALGORITHM_ID");

        string hashAlgorithmId = System.Text.Encoding.UTF8.GetString(data, offset, algorithmIdLength);
        offset += algorithmIdLength;

        // Read key hash
        if (offset + 4 > data.Length)
            throw new Exceptions.MalformedProofException(
                "Data is too short to contain key hash length.",
                "INSUFFICIENT_DATA");

        int keyHashLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
        offset += 4;
        if (keyHashLength < 0 || offset + keyHashLength > data.Length)
            throw new Exceptions.MalformedProofException(
                "Invalid key hash length or insufficient data.",
                "INVALID_KEY_HASH");

        var keyHash = new byte[keyHashLength];
        Array.Copy(data, offset, keyHash, 0, keyHashLength);
        offset += keyHashLength;

        // Read conflicting leaf data if leaf mismatch
        byte[]? conflictingKeyHash = null;
        byte[]? conflictingValue = null;

        if (isLeafMismatch)
        {
            // Read conflicting key hash
            if (offset + 4 > data.Length)
                throw new Exceptions.MalformedProofException(
                    "Data is too short to contain conflicting key hash length.",
                    "INSUFFICIENT_DATA");

            int conflictingKeyHashLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
            offset += 4;
            if (conflictingKeyHashLength < 0 || offset + conflictingKeyHashLength > data.Length)
                throw new Exceptions.MalformedProofException(
                    "Invalid conflicting key hash length or insufficient data.",
                    "INVALID_CONFLICTING_KEY_HASH");

            conflictingKeyHash = new byte[conflictingKeyHashLength];
            Array.Copy(data, offset, conflictingKeyHash, 0, conflictingKeyHashLength);
            offset += conflictingKeyHashLength;

            // Read conflicting value
            if (offset + 4 > data.Length)
                throw new Exceptions.MalformedProofException(
                    "Data is too short to contain conflicting value length.",
                    "INSUFFICIENT_DATA");

            int conflictingValueLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
            offset += 4;
            if (conflictingValueLength < 0 || offset + conflictingValueLength > data.Length)
                throw new Exceptions.MalformedProofException(
                    "Invalid conflicting value length or insufficient data.",
                    "INVALID_CONFLICTING_VALUE");

            conflictingValue = new byte[conflictingValueLength];
            Array.Copy(data, offset, conflictingValue, 0, conflictingValueLength);
            offset += conflictingValueLength;
        }

        // Read sibling bitmask
        if (offset + 4 > data.Length)
            throw new Exceptions.MalformedProofException(
                "Data is too short to contain sibling bitmask length.",
                "INSUFFICIENT_DATA");

        int bitmaskLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
        offset += 4;
        if (bitmaskLength < 0 || offset + bitmaskLength > data.Length)
            throw new Exceptions.MalformedProofException(
                "Invalid sibling bitmask length or insufficient data.",
                "INVALID_BITMASK");

        var siblingBitmask = new byte[bitmaskLength];
        Array.Copy(data, offset, siblingBitmask, 0, bitmaskLength);
        offset += bitmaskLength;

        // Read sibling count
        if (offset + 4 > data.Length)
            throw new Exceptions.MalformedProofException(
                "Data is too short to contain sibling count.",
                "INSUFFICIENT_DATA");

        int siblingCount = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
        offset += 4;
        if (siblingCount < 0)
            throw new Exceptions.MalformedProofException(
                $"Invalid sibling count: {siblingCount}.",
                "INVALID_SIBLING_COUNT");

        // Read sibling hashes
        byte[][] siblingHashes;
        if (siblingCount > 0)
        {
            // Determine hash size from remaining data
            int remainingData = data.Length - offset;
            if (remainingData % siblingCount != 0)
                throw new Exceptions.MalformedProofException(
                    "Remaining data length is not evenly divisible by sibling count.",
                    "INVALID_HASH_SIZE");

            int hashSize = remainingData / siblingCount;
            siblingHashes = new byte[siblingCount][];
            for (int i = 0; i < siblingCount; i++)
            {
                siblingHashes[i] = new byte[hashSize];
                Array.Copy(data, offset, siblingHashes[i], 0, hashSize);
                offset += hashSize;
            }
        }
        else
        {
            siblingHashes = Array.Empty<byte[]>();
        }

        return new SmtNonInclusionProof(
            keyHash,
            depth,
            hashAlgorithmId,
            siblingHashes,
            siblingBitmask,
            isCompressed,
            proofType,
            conflictingKeyHash,
            conflictingValue);
    }
}

/// <summary>
/// Specifies the type of non-inclusion proof.
/// </summary>
public enum NonInclusionProofType
{
    /// <summary>
    /// The path to the key leads to an empty subtree (all zero-hashes).
    /// </summary>
    EmptyPath = 0,

    /// <summary>
    /// The path leads to a leaf with a different key hash.
    /// </summary>
    LeafMismatch = 1
}
