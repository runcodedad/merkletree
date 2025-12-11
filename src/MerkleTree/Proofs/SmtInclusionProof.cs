using System.Buffers.Binary;
using MerkleTree.Hashing;
using MerkleTree.Smt;

namespace MerkleTree.Proofs;

/// <summary>
/// Represents a Sparse Merkle Tree inclusion proof.
/// </summary>
/// <remarks>
/// <para>
/// An inclusion proof demonstrates that a specific key-value pair exists in the tree
/// by providing the sibling hashes needed to recompute the root hash from the leaf.
/// </para>
/// <para>
/// Inclusion proofs support optional compression where zero-hash siblings are omitted
/// and reconstructed during verification using a zero-hash table.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Generate inclusion proof
/// var proof = await smt.GenerateInclusionProofAsync(key, storage);
/// 
/// // Verify proof
/// bool isValid = proof.Verify(rootHash, hashFunction, zeroHashes);
/// </code>
/// </example>
public sealed class SmtInclusionProof : SmtProof
{
    /// <summary>
    /// Gets the value associated with the key in the tree.
    /// </summary>
    public byte[] Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtInclusionProof"/> class.
    /// </summary>
    /// <param name="keyHash">The hash of the key being proven.</param>
    /// <param name="value">The value associated with the key.</param>
    /// <param name="depth">The tree depth.</param>
    /// <param name="hashAlgorithmId">The hash algorithm identifier.</param>
    /// <param name="siblingHashes">The sibling hashes along the path.</param>
    /// <param name="siblingBitmask">The bitmask for compressed proofs.</param>
    /// <param name="isCompressed">Whether this proof uses compression.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    public SmtInclusionProof(
        byte[] keyHash,
        byte[] value,
        int depth,
        string hashAlgorithmId,
        byte[][] siblingHashes,
        byte[] siblingBitmask,
        bool isCompressed)
        : base(keyHash, depth, hashAlgorithmId, siblingHashes, siblingBitmask, isCompressed)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        Value = value;
    }

    /// <summary>
    /// Verifies this inclusion proof against a given root hash.
    /// </summary>
    /// <param name="expectedRootHash">The expected root hash to verify against.</param>
    /// <param name="hashFunction">The hash function to use for verification.</param>
    /// <param name="zeroHashes">The zero-hash table for this tree.</param>
    /// <returns>True if the proof is valid and produces the expected root hash; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when hash algorithm or depth mismatch.</exception>
    /// <remarks>
    /// <para>
    /// Verification reconstructs the root hash by:
    /// 1. Computing the leaf hash: Hash(0x00 || keyHash || value)
    /// 2. Traversing up the tree, combining with sibling hashes
    /// 3. Comparing the computed root with the expected root
    /// </para>
    /// <para>
    /// For compressed proofs, zero-hash siblings are reconstructed from the zero-hash table.
    /// </para>
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

        // Compute leaf hash with domain separation: Hash(0x00 || keyHash || value)
        var leafData = new byte[1 + KeyHash.Length + Value.Length];
        leafData[0] = 0x00; // Leaf domain separator
        Array.Copy(KeyHash, 0, leafData, 1, KeyHash.Length);
        Array.Copy(Value, 0, leafData, 1 + KeyHash.Length, Value.Length);
        var currentHash = hashFunction.ComputeHash(leafData);

        // Get bit path for tree traversal
        var bitPath = GetBitPath();

        // Traverse from leaf to root
        for (int level = 0; level < Depth; level++)
        {
            var siblingHash = allSiblings[level];
            var isRight = bitPath[Depth - 1 - level];

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
    /// Serializes this inclusion proof to binary format.
    /// </summary>
    /// <returns>A byte array containing the serialized proof.</returns>
    /// <remarks>
    /// <para>
    /// Format specification:
    /// - Version (1 byte): Format version (currently 1)
    /// - Proof Type (1 byte): 0x01 for inclusion proof
    /// - Flags (1 byte): Bit 0 = IsCompressed
    /// - Depth (4 bytes): Tree depth, little-endian
    /// - Hash Algorithm ID Length (4 bytes): Length of algorithm name, little-endian
    /// - Hash Algorithm ID (variable): UTF-8 encoded algorithm name
    /// - Key Hash Length (4 bytes): Length of key hash, little-endian
    /// - Key Hash (variable): Raw key hash bytes
    /// - Value Length (4 bytes): Length of value, little-endian
    /// - Value (variable): Raw value bytes
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
                       4 + KeyHash.Length + // key hash length + data
                       4 + Value.Length + // value length + data
                       4 + SiblingBitmask.Length + // bitmask length + data
                       4 + // sibling count
                       (SiblingHashes.Length * hashSize); // sibling hashes

        var result = new byte[totalSize];
        int offset = 0;

        // Write version
        result[offset++] = 1;

        // Write proof type (0x01 = inclusion)
        result[offset++] = 0x01;

        // Write flags
        byte flags = 0;
        if (IsCompressed) flags |= 0x01;
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

        // Write value
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), Value.Length);
        offset += 4;
        Value.CopyTo(result, offset);
        offset += Value.Length;

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
    /// Deserializes an inclusion proof from binary format.
    /// </summary>
    /// <param name="data">The serialized proof data.</param>
    /// <returns>A new <see cref="SmtInclusionProof"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    /// <exception cref="Exceptions.MalformedProofException">Thrown when data is invalid or corrupted.</exception>
    public static SmtInclusionProof Deserialize(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (data.Length < 3)
            throw new Exceptions.MalformedProofException(
                "Data is too short to be a valid serialized inclusion proof.",
                "INSUFFICIENT_DATA");

        int offset = 0;

        // Read version
        byte version = data[offset++];
        if (version != 1)
            throw new Exceptions.MalformedProofException(
                $"Unsupported serialization version: {version}. Expected version 1.",
                "UNSUPPORTED_VERSION");

        // Read proof type
        byte proofType = data[offset++];
        if (proofType != 0x01)
            throw new Exceptions.MalformedProofException(
                $"Invalid proof type: 0x{proofType:X2}. Expected 0x01 for inclusion proof.",
                "INVALID_PROOF_TYPE");

        // Read flags
        byte flags = data[offset++];
        bool isCompressed = (flags & 0x01) != 0;

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

        // Read value
        if (offset + 4 > data.Length)
            throw new Exceptions.MalformedProofException(
                "Data is too short to contain value length.",
                "INSUFFICIENT_DATA");

        int valueLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
        offset += 4;
        if (valueLength < 0 || offset + valueLength > data.Length)
            throw new Exceptions.MalformedProofException(
                "Invalid value length or insufficient data.",
                "INVALID_VALUE");

        var value = new byte[valueLength];
        Array.Copy(data, offset, value, 0, valueLength);
        offset += valueLength;

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
        if (siblingCount > 0)
        {
            // Determine hash size from remaining data
            int remainingData = data.Length - offset;
            if (remainingData % siblingCount != 0)
                throw new Exceptions.MalformedProofException(
                    "Remaining data length is not evenly divisible by sibling count.",
                    "INVALID_HASH_SIZE");

            int hashSize = remainingData / siblingCount;
            var siblingHashes = new byte[siblingCount][];
            for (int i = 0; i < siblingCount; i++)
            {
                siblingHashes[i] = new byte[hashSize];
                Array.Copy(data, offset, siblingHashes[i], 0, hashSize);
                offset += hashSize;
            }

            return new SmtInclusionProof(keyHash, value, depth, hashAlgorithmId, siblingHashes, siblingBitmask, isCompressed);
        }
        else
        {
            // No sibling hashes (single leaf tree)
            return new SmtInclusionProof(keyHash, value, depth, hashAlgorithmId, Array.Empty<byte[]>(), siblingBitmask, isCompressed);
        }
    }
}
