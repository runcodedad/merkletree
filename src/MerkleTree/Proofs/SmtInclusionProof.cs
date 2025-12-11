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
        // Validate all required parameters are provided
        if (expectedRootHash == null)
            throw new ArgumentNullException(nameof(expectedRootHash));
        if (hashFunction == null)
            throw new ArgumentNullException(nameof(hashFunction));
        if (zeroHashes == null)
            throw new ArgumentNullException(nameof(zeroHashes));

        // Ensure the hash function matches what was used to generate the proof
        if (hashFunction.Name != HashAlgorithmId)
            throw new ArgumentException(
                $"Hash function '{hashFunction.Name}' does not match proof algorithm '{HashAlgorithmId}'.",
                nameof(hashFunction));

        // Ensure the zero-hash table depth matches the proof depth
        if (zeroHashes.Depth != Depth)
            throw new ArgumentException(
                $"Zero-hash table depth {zeroHashes.Depth} does not match proof depth {Depth}.",
                nameof(zeroHashes));

        // Get all sibling hashes needed for verification
        // For compressed proofs, this reconstructs zero-hash siblings from the zero-hash table
        // For uncompressed proofs, this just returns the stored sibling array
        var allSiblings = GetAllSiblingHashes(zeroHashes);

        // Step 1: Compute the leaf hash using the key hash and value from the proof
        // Leaf hash format: Hash(0x00 || keyHash || value)
        // The 0x00 prefix is a domain separator that distinguishes leaf nodes from internal nodes
        var leafData = new byte[1 + KeyHash.Length + Value.Length];
        leafData[0] = 0x00; // Leaf domain separator
        Array.Copy(KeyHash, 0, leafData, 1, KeyHash.Length);
        Array.Copy(Value, 0, leafData, 1 + KeyHash.Length, Value.Length);
        var currentHash = hashFunction.ComputeHash(leafData);

        // Step 2: Get the bit path that determines traversal direction at each level
        // The bit path is derived from the key hash and specifies left (false) or right (true) at each level
        var bitPath = GetBitPath();

        // Step 3: Traverse from leaf to root, computing parent hashes at each level
        // We iterate through verification levels (0 to Depth-1) from bottom to top
        for (int level = 0; level < Depth; level++)
        {
            // Get the sibling hash at this verification level
            var siblingHash = allSiblings[level];
            
            // Determine if the current node is on the right side of its parent
            // We read from the end of bitPath because:
            // - bitPath[0] is the root level (first decision from root)
            // - bitPath[Depth-1] is the leaf level (last decision before leaf)
            // - level 0 in verification is the leaf level, so we use bitPath[Depth-1-level]
            var isRight = bitPath[Depth - 1 - level];

            // Step 4: Compute parent hash with domain separation
            // Internal node hash format: Hash(0x01 || leftHash || rightHash)
            // The 0x01 prefix distinguishes internal nodes from leaf nodes (0x00)
            byte[] combinedData;
            if (isRight)
            {
                // Current node is on the right, sibling is on the left
                // Parent hash = Hash(0x01 || siblingHash || currentHash)
                combinedData = new byte[1 + siblingHash.Length + currentHash.Length];
                combinedData[0] = 0x01; // Internal node domain separator
                Array.Copy(siblingHash, 0, combinedData, 1, siblingHash.Length);
                Array.Copy(currentHash, 0, combinedData, 1 + siblingHash.Length, currentHash.Length);
            }
            else
            {
                // Current node is on the left, sibling is on the right
                // Parent hash = Hash(0x01 || currentHash || siblingHash)
                combinedData = new byte[1 + currentHash.Length + siblingHash.Length];
                combinedData[0] = 0x01; // Internal node domain separator
                Array.Copy(currentHash, 0, combinedData, 1, currentHash.Length);
                Array.Copy(siblingHash, 0, combinedData, 1 + currentHash.Length, siblingHash.Length);
            }

            // Compute the hash for the parent node at the next level up
            currentHash = hashFunction.ComputeHash(combinedData);
        }

        // Step 5: After traversing all levels, currentHash should be the root hash
        // Compare the computed root with the expected root to verify the proof
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
        // Prepare hash algorithm ID as UTF-8 bytes for serialization
        var algorithmIdBytes = System.Text.Encoding.UTF8.GetBytes(HashAlgorithmId);
        
        // Determine hash size from the first sibling hash if available, otherwise use key hash length
        int hashSize = SiblingHashes.Length > 0 ? SiblingHashes[0].Length : KeyHash.Length;

        // Calculate total size of the serialized proof to allocate the exact buffer size needed
        int totalSize = 1 + // version (1 byte)
                       1 + // proof type (1 byte): 0x01 for inclusion
                       1 + // flags (1 byte): bit 0 = IsCompressed
                       4 + // depth (4 bytes, little-endian int32)
                       4 + algorithmIdBytes.Length + // algorithm ID length (4 bytes) + UTF-8 bytes
                       4 + KeyHash.Length + // key hash length (4 bytes) + hash bytes
                       4 + Value.Length + // value length (4 bytes) + value bytes
                       4 + SiblingBitmask.Length + // bitmask length (4 bytes) + bitmask bytes
                       4 + // sibling count (4 bytes, int32)
                       (SiblingHashes.Length * hashSize); // all sibling hashes concatenated

        var result = new byte[totalSize];
        int offset = 0; // Track current write position in the buffer

        // Write version number (currently 1) - allows for future format changes
        result[offset++] = 1;

        // Write proof type: 0x01 for inclusion proof (0x02 is used for non-inclusion proofs)
        result[offset++] = 0x01;

        // Write flags byte: bit 0 indicates compression, other bits reserved for future use
        byte flags = 0;
        if (IsCompressed) flags |= 0x01; // Set bit 0 if proof uses compression
        result[offset++] = flags;

        // Write tree depth as a 32-bit little-endian integer
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), Depth);
        offset += 4;

        // Write hash algorithm ID: first the length, then the UTF-8 encoded string
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), algorithmIdBytes.Length);
        offset += 4;
        algorithmIdBytes.CopyTo(result, offset);
        offset += algorithmIdBytes.Length;

        // Write key hash: length-prefixed byte array
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), KeyHash.Length);
        offset += 4;
        KeyHash.CopyTo(result, offset);
        offset += KeyHash.Length;

        // Write value: length-prefixed byte array (the value being proven)
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), Value.Length);
        offset += 4;
        Value.CopyTo(result, offset);
        offset += Value.Length;

        // Write sibling bitmask: used for compressed proofs to track which siblings are zero-hashes
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), SiblingBitmask.Length);
        offset += 4;
        SiblingBitmask.CopyTo(result, offset);
        offset += SiblingBitmask.Length;

        // Write count of sibling hashes that follow
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), SiblingHashes.Length);
        offset += 4;

        // Write all sibling hashes sequentially (no length prefix per hash, fixed size)
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

        int offset = 0; // Track current read position in the data buffer

        // Read and validate version number (1 byte)
        byte version = data[offset++];
        if (version != 1)
            throw new Exceptions.MalformedProofException(
                $"Unsupported serialization version: {version}. Expected version 1.",
                "UNSUPPORTED_VERSION");

        // Read and validate proof type (1 byte)
        // Must be 0x01 for inclusion proof
        byte proofType = data[offset++];
        if (proofType != 0x01)
            throw new Exceptions.MalformedProofException(
                $"Invalid proof type: 0x{proofType:X2}. Expected 0x01 for inclusion proof.",
                "INVALID_PROOF_TYPE");

        // Read flags byte and extract compression flag from bit 0
        byte flags = data[offset++];
        bool isCompressed = (flags & 0x01) != 0; // Test bit 0 for compression

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
