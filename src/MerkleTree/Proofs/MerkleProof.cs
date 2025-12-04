using System.Buffers.Binary;
using MerkleTree.Hashing;

namespace MerkleTree.Proofs;

/// <summary>
/// Represents a Merkle proof that can be used to verify a leaf's inclusion in a Merkle tree.
/// </summary>
/// <remarks>
/// A Merkle proof contains all the information needed to recompute the root hash from a leaf,
/// including sibling hashes at each level and orientation bits indicating whether each sibling
/// is on the left or right.
/// </remarks>
public class MerkleProof
{
    /// <summary>
    /// Gets the value of the leaf being proven.
    /// </summary>
    public byte[] LeafValue { get; }

    /// <summary>
    /// Gets the index of the leaf in the tree (0-based).
    /// </summary>
    public long LeafIndex { get; }

    /// <summary>
    /// Gets the total height of the tree.
    /// </summary>
    /// <remarks>
    /// Height is measured as the number of levels above the leaves.
    /// A single leaf has height 0, two leaves have height 1, etc.
    /// </remarks>
    public int TreeHeight { get; }

    /// <summary>
    /// Gets the sibling hashes needed to recompute the root, ordered from leaf to root.
    /// </summary>
    /// <remarks>
    /// Each element in this array is the sibling hash at the corresponding level.
    /// The array has length equal to TreeHeight.
    /// </remarks>
    public byte[][] SiblingHashes { get; }

    /// <summary>
    /// Gets the orientation bits indicating whether each sibling is on the left (false) or right (true).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each element indicates the position of the corresponding sibling hash:
    /// - false: sibling is on the left, current node is on the right
    /// - true: sibling is on the right, current node is on the left
    /// </para>
    /// <para>
    /// When computing parent hash: Hash(left || right)
    /// - If orientation is false: Hash(sibling || current)
    /// - If orientation is true: Hash(current || sibling)
    /// </para>
    /// <para>
    /// The array has length equal to TreeHeight.
    /// </para>
    /// </remarks>
    public bool[] SiblingIsRight { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MerkleProof"/> class.
    /// </summary>
    /// <param name="leafValue">The value of the leaf being proven.</param>
    /// <param name="leafIndex">The index of the leaf in the tree.</param>
    /// <param name="treeHeight">The total height of the tree.</param>
    /// <param name="siblingHashes">The sibling hashes at each level from leaf to root.</param>
    /// <param name="siblingIsRight">The orientation bits for each sibling hash.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when arrays have mismatched lengths or invalid values.</exception>
    public MerkleProof(
        byte[] leafValue,
        long leafIndex,
        int treeHeight,
        byte[][] siblingHashes,
        bool[] siblingIsRight)
    {
        if (leafValue == null)
            throw new ArgumentNullException(nameof(leafValue));
        if (siblingHashes == null)
            throw new ArgumentNullException(nameof(siblingHashes));
        if (siblingIsRight == null)
            throw new ArgumentNullException(nameof(siblingIsRight));
        if (leafIndex < 0)
            throw new ArgumentException("Leaf index must be non-negative.", nameof(leafIndex));
        if (treeHeight < 0)
            throw new ArgumentException("Tree height must be non-negative.", nameof(treeHeight));
        if (siblingHashes.Length != treeHeight)
            throw new ArgumentException($"Expected {treeHeight} sibling hashes, got {siblingHashes.Length}.", nameof(siblingHashes));
        if (siblingIsRight.Length != treeHeight)
            throw new ArgumentException($"Expected {treeHeight} orientation bits, got {siblingIsRight.Length}.", nameof(siblingIsRight));

        // Validate sibling hash consistency
        int? expectedHashSize = null;
        for (int i = 0; i < siblingHashes.Length; i++)
        {
            if (siblingHashes[i] == null)
                throw new ArgumentException($"Sibling hash at index {i} is null.", nameof(siblingHashes));
            
            if (expectedHashSize == null)
            {
                expectedHashSize = siblingHashes[i].Length;
            }
            else if (siblingHashes[i].Length != expectedHashSize.Value)
            {
                throw new ArgumentException(
                    $"Sibling hash at index {i} has inconsistent length {siblingHashes[i].Length}, expected {expectedHashSize.Value}.",
                    nameof(siblingHashes));
            }
        }

        LeafValue = leafValue;
        LeafIndex = leafIndex;
        TreeHeight = treeHeight;
        SiblingHashes = siblingHashes;
        SiblingIsRight = siblingIsRight;
    }

    /// <summary>
    /// Verifies this proof against a given root hash using the specified hash function.
    /// </summary>
    /// <param name="expectedRootHash">The expected root hash to verify against.</param>
    /// <param name="hashFunction">The hash function to use for verification.</param>
    /// <returns>True if the proof is valid and produces the expected root hash; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when sibling hashes are null or empty.</exception>
    public bool Verify(byte[] expectedRootHash, IHashFunction hashFunction)
    {
        if (expectedRootHash == null)
            throw new ArgumentNullException(nameof(expectedRootHash));
        if (hashFunction == null)
            throw new ArgumentNullException(nameof(hashFunction));

        // Start by hashing the leaf value
        var currentHash = hashFunction.ComputeHash(LeafValue);

        // Traverse from leaf to root, computing parent hashes
        for (int i = 0; i < TreeHeight; i++)
        {
            var siblingHash = SiblingHashes[i];
            
            // Validate sibling hash is well-formed
            if (siblingHash == null)
                throw new InvalidOperationException($"Sibling hash at level {i} is null.");
            if (siblingHash.Length == 0)
                throw new InvalidOperationException($"Sibling hash at level {i} is empty.");
            
            var isRight = SiblingIsRight[i];

            // Compute parent hash: Hash(left || right)
            byte[] combinedHash;
            if (isRight)
            {
                // Sibling is on the right, current node is on the left
                combinedHash = currentHash.Concat(siblingHash).ToArray();
            }
            else
            {
                // Sibling is on the left, current node is on the right
                combinedHash = siblingHash.Concat(currentHash).ToArray();
            }

            currentHash = hashFunction.ComputeHash(combinedHash);
        }

        // Compare the computed root hash with the expected root hash
        return currentHash.SequenceEqual(expectedRootHash);
    }

    /// <summary>
    /// Serializes this Merkle proof to a compact binary format.
    /// </summary>
    /// <returns>A byte array containing the serialized proof.</returns>
    /// <remarks>
    /// <para>
    /// The serialization format is deterministic and platform-independent.
    /// It can be deserialized using the <see cref="Deserialize"/> method.
    /// </para>
    /// <para>
    /// Format specification:
    /// - Version (1 byte): Format version number (currently 1)
    /// - Tree Height (4 bytes): int32, little-endian
    /// - Leaf Index (8 bytes): int64, little-endian
    /// - Leaf Value Length (4 bytes): int32, little-endian
    /// - Leaf Value (variable): raw bytes
    /// - Hash Size (4 bytes): int32, little-endian (size of each sibling hash)
    /// - Orientation Bits Length (4 bytes): int32, little-endian (number of bytes for packed bits)
    /// - Orientation Bits (variable): bool[] packed into bytes (8 bits per byte)
    /// - Sibling Hashes (variable): consecutive hash bytes (TreeHeight * HashSize)
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when sibling hashes have inconsistent sizes.</exception>
    public byte[] Serialize()
    {
        // Validate all sibling hashes first before accessing any properties
        for (int i = 0; i < SiblingHashes.Length; i++)
        {
            if (SiblingHashes[i] == null)
                throw new InvalidOperationException($"Sibling hash at index {i} is null.");
        }

        // Determine hash size from first sibling hash, or 0 if no siblings
        int hashSize = TreeHeight > 0 ? SiblingHashes[0].Length : 0;

        // Validate that all sibling hashes have the same size
        for (int i = 0; i < SiblingHashes.Length; i++)
        {
            if (SiblingHashes[i].Length != hashSize)
                throw new InvalidOperationException($"Sibling hash at index {i} has size {SiblingHashes[i].Length}, expected {hashSize}.");
        }

        // Pack orientation bits into bytes
        int orientationBytesLength = TreeHeight > 0 ? (TreeHeight + 7) / 8 : 0;
        byte[] orientationBytes = new byte[orientationBytesLength];
        for (int i = 0; i < TreeHeight; i++)
        {
            if (SiblingIsRight[i])
            {
                int byteIndex = i / 8;
                int bitIndex = i % 8;
                orientationBytes[byteIndex] |= (byte)(1 << bitIndex);
            }
        }

        // Calculate total size
        int totalSize = 1 + // version
                       4 + // tree height
                       8 + // leaf index
                       4 + LeafValue.Length + // leaf value length + data
                       4 + // hash size
                       4 + orientationBytesLength + // orientation bits length + data
                       (TreeHeight * hashSize); // sibling hashes

        byte[] result = new byte[totalSize];
        int offset = 0;

        // Write version
        result[offset++] = 1;

        // Write tree height
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), TreeHeight);
        offset += 4;

        // Write leaf index
        BinaryPrimitives.WriteInt64LittleEndian(result.AsSpan(offset), LeafIndex);
        offset += 8;

        // Write leaf value length and data
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), LeafValue.Length);
        offset += 4;
        LeafValue.CopyTo(result, offset);
        offset += LeafValue.Length;

        // Write hash size
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), hashSize);
        offset += 4;

        // Write orientation bits length and data
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), orientationBytesLength);
        offset += 4;
        orientationBytes.CopyTo(result, offset);
        offset += orientationBytesLength;

        // Write sibling hashes
        for (int i = 0; i < TreeHeight; i++)
        {
            SiblingHashes[i].CopyTo(result, offset);
            offset += hashSize;
        }

        return result;
    }

    /// <summary>
    /// Deserializes a Merkle proof from binary format.
    /// </summary>
    /// <param name="data">The serialized proof data.</param>
    /// <returns>A new <see cref="MerkleProof"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    /// <exception cref="ArgumentException">Thrown when data is invalid or corrupted.</exception>
    /// <remarks>
    /// The data must have been created by the <see cref="Serialize"/> method.
    /// See <see cref="Serialize"/> for the format specification.
    /// </remarks>
    public static MerkleProof Deserialize(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (data.Length < 1)
            throw new ArgumentException("Data is too short to be a valid serialized proof.", nameof(data));

        int offset = 0;

        // Read version
        byte version = data[offset++];
        if (version != 1)
            throw new ArgumentException($"Unsupported serialization version: {version}. Expected version 1.", nameof(data));

        if (data.Length < 1 + 4 + 8 + 4)
            throw new ArgumentException("Data is too short to contain header fields.", nameof(data));

        // Read tree height
        int treeHeight = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
        offset += 4;

        if (treeHeight < 0)
            throw new ArgumentException($"Invalid tree height: {treeHeight}. Must be non-negative.", nameof(data));

        // Read leaf index
        long leafIndex = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(offset));
        offset += 8;

        if (leafIndex < 0)
            throw new ArgumentException($"Invalid leaf index: {leafIndex}. Must be non-negative.", nameof(data));

        // Read leaf value length
        int leafValueLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
        offset += 4;

        if (leafValueLength < 0)
            throw new ArgumentException($"Invalid leaf value length: {leafValueLength}. Must be non-negative.", nameof(data));
        if (offset + leafValueLength > data.Length)
            throw new ArgumentException("Data is too short to contain leaf value.", nameof(data));

        // Read leaf value
        byte[] leafValue = new byte[leafValueLength];
        Array.Copy(data, offset, leafValue, 0, leafValueLength);
        offset += leafValueLength;

        // Read hash size
        if (offset + 4 > data.Length)
            throw new ArgumentException("Data is too short to contain hash size.", nameof(data));
        int hashSize = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
        offset += 4;

        if (hashSize < 0)
            throw new ArgumentException($"Invalid hash size: {hashSize}. Must be non-negative.", nameof(data));

        // Read orientation bits length
        if (offset + 4 > data.Length)
            throw new ArgumentException("Data is too short to contain orientation bits length.", nameof(data));
        int orientationBytesLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
        offset += 4;

        if (orientationBytesLength < 0)
            throw new ArgumentException($"Invalid orientation bits length: {orientationBytesLength}. Must be non-negative.", nameof(data));

        // Validate orientation bytes length matches tree height
        int expectedOrientationBytesLength = treeHeight > 0 ? (treeHeight + 7) / 8 : 0;
        if (orientationBytesLength != expectedOrientationBytesLength)
            throw new ArgumentException($"Orientation bits length {orientationBytesLength} does not match expected {expectedOrientationBytesLength} for tree height {treeHeight}.", nameof(data));

        if (offset + orientationBytesLength > data.Length)
            throw new ArgumentException("Data is too short to contain orientation bits.", nameof(data));

        // Read and unpack orientation bits
        bool[] siblingIsRight = new bool[treeHeight];
        for (int i = 0; i < treeHeight; i++)
        {
            int byteIndex = i / 8;
            int bitIndex = i % 8;
            siblingIsRight[i] = (data[offset + byteIndex] & (1 << bitIndex)) != 0;
        }
        offset += orientationBytesLength;

        // Read sibling hashes
        int totalHashesSize = treeHeight * hashSize;
        if (offset + totalHashesSize > data.Length)
            throw new ArgumentException($"Data is too short to contain {treeHeight} sibling hashes of size {hashSize}.", nameof(data));

        byte[][] siblingHashes = new byte[treeHeight][];
        for (int i = 0; i < treeHeight; i++)
        {
            siblingHashes[i] = new byte[hashSize];
            Array.Copy(data, offset, siblingHashes[i], 0, hashSize);
            offset += hashSize;
        }

        // Verify we've consumed all the data
        if (offset != data.Length)
            throw new ArgumentException($"Data contains {data.Length - offset} extra bytes after deserialization.", nameof(data));

        return new MerkleProof(leafValue, leafIndex, treeHeight, siblingHashes, siblingIsRight);
    }
}
