using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using MerkleTree.Hashing;

namespace MerkleTree.Smt;

/// <summary>
/// Contains metadata about a Sparse Merkle Tree (SMT) for deterministic reproduction and verification.
/// </summary>
/// <remarks>
/// <para>
/// This metadata is essential to reproduce SMT roots and proofs across machines, track upgrades,
/// and provide determinism. The metadata includes version information, hash algorithm details,
/// tree depth configuration, and the precomputed zero-hash table.
/// </para>
/// <para><strong>Versioning Strategy:</strong></para>
/// <list type="bullet">
/// <item><description><strong>SmtCoreVersion</strong>: The version of the SMT core implementation. Changes indicate breaking changes in the tree structure or algorithms.</description></item>
/// <item><description><strong>SerializationFormatVersion</strong>: The version of the binary serialization format. Changes indicate incompatible serialization formats.</description></item>
/// </list>
/// <para><strong>Determinism Guarantees:</strong></para>
/// <para>
/// Given the same hash algorithm, tree depth, and input data, the SMT will always produce:
/// - Identical root hashes across platforms
/// - Identical zero-hash tables
/// - Identical proof structures
/// </para>
/// <para><strong>Migration and Upgrades:</strong></para>
/// <para>
/// When SmtCoreVersion or SerializationFormatVersion changes, old serialized states may not be
/// compatible with new versions. Manual migration may be required. Breaking changes will be
/// documented in release notes with migration guidance.
/// </para>
/// </remarks>
public sealed class SmtMetadata
{
    /// <summary>
    /// Current version of the SMT core implementation.
    /// </summary>
    /// <remarks>
    /// This version tracks the core SMT algorithm and tree structure.
    /// Breaking changes to the tree structure, hashing strategy, or core algorithms
    /// will increment this version.
    /// </remarks>
    public const int CurrentSmtCoreVersion = 1;

    /// <summary>
    /// Current version of the serialization format.
    /// </summary>
    /// <remarks>
    /// This version tracks the binary serialization format for metadata and tree nodes.
    /// Breaking changes to the serialization format will increment this version.
    /// </remarks>
    public const int CurrentSerializationFormatVersion = 1;

    /// <summary>
    /// Gets the hash algorithm identifier.
    /// </summary>
    /// <remarks>
    /// This is the name returned by <see cref="IHashFunction.Name"/> and is used to
    /// identify the hash algorithm when deserializing metadata or verifying proofs.
    /// Examples: "SHA-256", "SHA-512", "BLAKE3"
    /// </remarks>
    public string HashAlgorithmId { get; }

    /// <summary>
    /// Gets the tree depth (number of levels in the tree).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The depth determines the maximum number of keys that can be stored in the tree.
    /// A depth of N supports up to 2^N keys. For example:
    /// - Depth 8 = 256 keys
    /// - Depth 16 = 65,536 keys
    /// - Depth 32 = 4,294,967,296 keys
    /// - Depth 64 = 18,446,744,073,709,551,616 keys
    /// </para>
    /// <para>
    /// Typical depths range from 8 to 256 depending on the application.
    /// Higher depths support more keys but require more computation for proof generation.
    /// </para>
    /// </remarks>
    public int TreeDepth { get; }

    /// <summary>
    /// Gets the precomputed zero-hash table for this tree configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The zero-hash table contains the hash values for empty subtrees at each level.
    /// This enables efficient sparse tree operations by avoiding recomputation of empty branches.
    /// </para>
    /// <para>
    /// The table is indexed by level (0 = leaf level, TreeDepth = root level) and contains
    /// TreeDepth + 1 entries. Each entry is the hash of two child zero-hashes from the level below,
    /// except for level 0 which is the hash of an empty leaf.
    /// </para>
    /// <para>
    /// The zero-hash table is deterministic for a given hash algorithm and tree depth,
    /// and must be computed identically across all implementations.
    /// </para>
    /// </remarks>
    public ZeroHashTable ZeroHashes { get; }

    /// <summary>
    /// Gets the version of the SMT core implementation used to create this metadata.
    /// </summary>
    /// <remarks>
    /// This version number can be used to detect incompatible tree structures or algorithms.
    /// When loading serialized metadata, verify this version matches the expected version.
    /// </remarks>
    public int SmtCoreVersion { get; }

    /// <summary>
    /// Gets the version of the serialization format used to serialize this metadata.
    /// </summary>
    /// <remarks>
    /// This version number can be used to detect incompatible serialization formats.
    /// When deserializing, check this version to ensure compatibility.
    /// </remarks>
    public int SerializationFormatVersion { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtMetadata"/> class.
    /// </summary>
    /// <param name="hashAlgorithmId">The hash algorithm identifier (e.g., "SHA-256").</param>
    /// <param name="treeDepth">The tree depth (number of levels).</param>
    /// <param name="zeroHashes">The precomputed zero-hash table.</param>
    /// <param name="smtCoreVersion">The SMT core version (defaults to current version).</param>
    /// <param name="serializationFormatVersion">The serialization format version (defaults to current version).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hashAlgorithmId"/> or <paramref name="zeroHashes"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="hashAlgorithmId"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="treeDepth"/> is less than 1, or when versions are negative.</exception>
    public SmtMetadata(
        string hashAlgorithmId,
        int treeDepth,
        ZeroHashTable zeroHashes,
        int smtCoreVersion = CurrentSmtCoreVersion,
        int serializationFormatVersion = CurrentSerializationFormatVersion)
    {
        if (hashAlgorithmId == null)
            throw new ArgumentNullException(nameof(hashAlgorithmId));

        if (zeroHashes == null)
            throw new ArgumentNullException(nameof(zeroHashes));

        if (string.IsNullOrWhiteSpace(hashAlgorithmId))
            throw new ArgumentException("Hash algorithm ID cannot be empty or whitespace.", nameof(hashAlgorithmId));

        if (treeDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(treeDepth), "Tree depth must be at least 1.");

        if (smtCoreVersion < 0)
            throw new ArgumentOutOfRangeException(nameof(smtCoreVersion), "SMT core version cannot be negative.");

        if (serializationFormatVersion < 0)
            throw new ArgumentOutOfRangeException(nameof(serializationFormatVersion), "Serialization format version cannot be negative.");

        if (zeroHashes.Depth != treeDepth)
            throw new ArgumentException($"Zero hash table depth ({zeroHashes.Depth}) must match tree depth ({treeDepth}).", nameof(zeroHashes));

        if (zeroHashes.HashAlgorithmId != hashAlgorithmId)
            throw new ArgumentException($"Zero hash table algorithm ({zeroHashes.HashAlgorithmId}) must match hash algorithm ID ({hashAlgorithmId}).", nameof(zeroHashes));

        HashAlgorithmId = hashAlgorithmId;
        TreeDepth = treeDepth;
        ZeroHashes = zeroHashes;
        SmtCoreVersion = smtCoreVersion;
        SerializationFormatVersion = serializationFormatVersion;
    }

    /// <summary>
    /// Serializes the metadata to a binary format.
    /// </summary>
    /// <returns>A byte array containing the serialized metadata.</returns>
    /// <remarks>
    /// <para><strong>Serialization Format (version 1):</strong></para>
    /// <list type="bullet">
    /// <item><description>4 bytes: Serialization format version (little-endian)</description></item>
    /// <item><description>4 bytes: SMT core version (little-endian)</description></item>
    /// <item><description>4 bytes: Tree depth (little-endian)</description></item>
    /// <item><description>4 bytes: Hash algorithm ID string length (little-endian)</description></item>
    /// <item><description>N bytes: Hash algorithm ID string (UTF-8)</description></item>
    /// <item><description>Remaining bytes: Zero-hash table serialized data</description></item>
    /// </list>
    /// <para>
    /// The format is platform-independent and uses little-endian byte order for all numeric values.
    /// </para>
    /// </remarks>
    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        // Write versions and tree depth
        WriteInt32LittleEndian(writer, SerializationFormatVersion);
        WriteInt32LittleEndian(writer, SmtCoreVersion);
        WriteInt32LittleEndian(writer, TreeDepth);

        // Write hash algorithm ID
        var hashAlgBytes = Encoding.UTF8.GetBytes(HashAlgorithmId);
        WriteInt32LittleEndian(writer, hashAlgBytes.Length);
        writer.Write(hashAlgBytes);

        // Write zero-hash table
        var zeroHashBytes = ZeroHashes.Serialize();
        writer.Write(zeroHashBytes);

        return ms.ToArray();
    }

    /// <summary>
    /// Deserializes metadata from a binary format.
    /// </summary>
    /// <param name="data">The serialized metadata bytes.</param>
    /// <returns>A new <see cref="SmtMetadata"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is invalid or corrupted.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the serialization format version is not supported.</exception>
    /// <remarks>
    /// This method validates the serialization format version and will throw an exception
    /// if attempting to deserialize metadata from an unsupported version.
    /// </remarks>
    public static SmtMetadata Deserialize(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (data.Length < 16)
            throw new ArgumentException("Data is too short to contain valid metadata.", nameof(data));

        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        // Read versions and tree depth
        int serializationFormatVersion = ReadInt32LittleEndian(reader);
        if (serializationFormatVersion != CurrentSerializationFormatVersion)
            throw new InvalidOperationException($"Unsupported serialization format version {serializationFormatVersion}. Expected version {CurrentSerializationFormatVersion}.");

        int smtCoreVersion = ReadInt32LittleEndian(reader);
        int treeDepth = ReadInt32LittleEndian(reader);

        // Read hash algorithm ID
        int hashAlgLength = ReadInt32LittleEndian(reader);
        if (hashAlgLength < 1 || hashAlgLength > 256)
            throw new ArgumentException("Invalid hash algorithm ID length.", nameof(data));

        var hashAlgBytes = reader.ReadBytes(hashAlgLength);
        if (hashAlgBytes.Length != hashAlgLength)
            throw new ArgumentException("Unexpected end of stream while reading hash algorithm ID.", nameof(data));

        string hashAlgorithmId = Encoding.UTF8.GetString(hashAlgBytes);

        // Read zero-hash table
        var remainingBytes = new byte[ms.Length - ms.Position];
        _ = reader.Read(remainingBytes, 0, remainingBytes.Length);
        var zeroHashes = ZeroHashTable.Deserialize(remainingBytes);

        return new SmtMetadata(hashAlgorithmId, treeDepth, zeroHashes, smtCoreVersion, serializationFormatVersion);
    }

    /// <summary>
    /// Creates SMT metadata with a freshly computed zero-hash table.
    /// </summary>
    /// <param name="hashFunction">The hash function to use.</param>
    /// <param name="treeDepth">The tree depth.</param>
    /// <returns>A new <see cref="SmtMetadata"/> instance with computed zero-hash table.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hashFunction"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="treeDepth"/> is less than 1.</exception>
    /// <remarks>
    /// This is a convenience factory method that creates metadata with a newly computed zero-hash table.
    /// The zero-hash table is computed deterministically based on the hash function and tree depth.
    /// </remarks>
    /// <example>
    /// <code>
    /// var hashFunction = new Sha256HashFunction();
    /// var metadata = SmtMetadata.Create(hashFunction, treeDepth: 256);
    /// </code>
    /// </example>
    public static SmtMetadata Create(IHashFunction hashFunction, int treeDepth)
    {
        if (hashFunction == null)
            throw new ArgumentNullException(nameof(hashFunction));

        var zeroHashes = ZeroHashTable.Compute(hashFunction, treeDepth);
        return new SmtMetadata(hashFunction.Name, treeDepth, zeroHashes);
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
