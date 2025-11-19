using System.Text;
using MerkleTree.Serialization;

namespace MerkleTree.Cache;

/// <summary>
/// Provides serialization and deserialization for Merkle tree cache data.
/// </summary>
/// <remarks>
/// This class implements a platform-independent binary format for storing and loading
/// cache data. The format includes a magic number for identification, version information,
/// metadata about the tree structure, and the cached node data organized by level.
/// The format uses little-endian encoding for cross-platform compatibility.
/// </remarks>
public static class CacheSerializer
{
    /// <summary>
    /// Magic number identifying cache files (ASCII "MKTC" = Merkle Tree Cache).
    /// </summary>
    private static readonly byte[] MagicNumber = new byte[] { 0x4D, 0x4B, 0x54, 0x43 }; // "MKTC"

    /// <summary>
    /// Current format version.
    /// </summary>
    private const byte FormatVersion = 1;

    /// <summary>
    /// Serializes cache data to a byte array.
    /// </summary>
    /// <param name="cacheData">The cache data to serialize.</param>
    /// <returns>A byte array containing the serialized cache data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when cacheData is null.</exception>
    /// <remarks>
    /// <para>
    /// Binary format specification (all multi-byte integers are little-endian):
    /// </para>
    /// <list type="bullet">
    /// <item><description>Magic Number (4 bytes): "MKTC" (0x4D, 0x4B, 0x54, 0x43)</description></item>
    /// <item><description>Format Version (1 byte): Current version is 1</description></item>
    /// <item><description>Tree Height (4 bytes): int32</description></item>
    /// <item><description>Hash Function Name Length (4 bytes): int32</description></item>
    /// <item><description>Hash Function Name (variable): UTF-8 encoded string</description></item>
    /// <item><description>Hash Size In Bytes (4 bytes): int32</description></item>
    /// <item><description>Start Level (4 bytes): int32</description></item>
    /// <item><description>End Level (4 bytes): int32</description></item>
    /// <item><description>Number of Levels (4 bytes): int32 (EndLevel - StartLevel + 1)</description></item>
    /// <item><description>For each level (StartLevel to EndLevel):
    ///   <list type="bullet">
    ///     <item><description>Level Number (4 bytes): int32</description></item>
    ///     <item><description>Node Count (8 bytes): int64</description></item>
    ///     <item><description>Nodes (variable): NodeCount × HashSizeInBytes bytes of consecutive hash data</description></item>
    ///   </list>
    /// </description></item>
    /// </list>
    /// <para>
    /// This format supports efficient random access to nodes within each level because
    /// all nodes are stored as fixed-size records (HashSizeInBytes).
    /// </para>
    /// </remarks>
    public static byte[] Serialize(CacheData cacheData)
    {
        if (cacheData == null)
            throw new ArgumentNullException(nameof(cacheData));

        var metadata = cacheData.Metadata;
        
        // Encode hash function name as UTF-8
        byte[] hashFunctionNameBytes = Encoding.UTF8.GetBytes(metadata.HashFunctionName);
        
        // Calculate total size needed
        int headerSize = 4 + // magic number
                        1 + // version
                        4 + // tree height
                        4 + hashFunctionNameBytes.Length + // hash function name length + data
                        4 + // hash size
                        4 + // start level
                        4 + // end level
                        4;  // number of levels

        // Calculate size for all levels
        long levelDataSize = 0;
        for (int level = metadata.StartLevel; level <= metadata.EndLevel; level++)
        {
            var cachedLevel = cacheData.GetLevel(level);
            levelDataSize += 4; // level number
            levelDataSize += 8; // node count
            levelDataSize += cachedLevel.NodeCount * metadata.HashSizeInBytes; // nodes
        }

        if (headerSize + levelDataSize > int.MaxValue)
            throw new InvalidOperationException("Cache data is too large to serialize.");

        int totalSize = headerSize + (int)levelDataSize;
        byte[] result = new byte[totalSize];
        int offset = 0;

        // Write magic number
        MagicNumber.CopyTo(result, offset);
        offset += MagicNumber.Length;

        // Write version
        result[offset++] = FormatVersion;

        // Write tree height
        BinarySerializationHelpers.WriteInt32LittleEndian(metadata.TreeHeight, result, offset);
        offset += 4;

        // Write hash function name length and data
        BinarySerializationHelpers.WriteInt32LittleEndian(hashFunctionNameBytes.Length, result, offset);
        offset += 4;
        hashFunctionNameBytes.CopyTo(result, offset);
        offset += hashFunctionNameBytes.Length;

        // Write hash size
        BinarySerializationHelpers.WriteInt32LittleEndian(metadata.HashSizeInBytes, result, offset);
        offset += 4;

        // Write start level
        BinarySerializationHelpers.WriteInt32LittleEndian(metadata.StartLevel, result, offset);
        offset += 4;

        // Write end level
        BinarySerializationHelpers.WriteInt32LittleEndian(metadata.EndLevel, result, offset);
        offset += 4;

        // Write number of levels
        int numLevels = metadata.EndLevel - metadata.StartLevel + 1;
        BinarySerializationHelpers.WriteInt32LittleEndian(numLevels, result, offset);
        offset += 4;

        // Write level data
        for (int level = metadata.StartLevel; level <= metadata.EndLevel; level++)
        {
            var cachedLevel = cacheData.GetLevel(level);
            
            // Write level number
            BinarySerializationHelpers.WriteInt32LittleEndian(level, result, offset);
            offset += 4;

            // Write node count
            BinarySerializationHelpers.WriteInt64LittleEndian(cachedLevel.NodeCount, result, offset);
            offset += 8;

            // Write all nodes
            foreach (var node in cachedLevel.Nodes)
            {
                node.CopyTo(result, offset);
                offset += metadata.HashSizeInBytes;
            }
        }

        return result;
    }

    /// <summary>
    /// Deserializes cache data from a byte array.
    /// </summary>
    /// <param name="data">The serialized cache data.</param>
    /// <returns>A new <see cref="CacheData"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    /// <exception cref="ArgumentException">Thrown when data is invalid or corrupted.</exception>
    /// <remarks>
    /// The data must have been created by the <see cref="Serialize"/> method.
    /// This method validates the magic number, version, and structure of the data.
    /// </remarks>
    public static CacheData Deserialize(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (data.Length < 4 + 1)
            throw new ArgumentException("Data is too short to be valid cache data.", nameof(data));

        int offset = 0;

        // Read and validate magic number
        for (int i = 0; i < MagicNumber.Length; i++)
        {
            if (data[offset++] != MagicNumber[i])
                throw new ArgumentException("Invalid magic number. This does not appear to be a valid cache file.", nameof(data));
        }

        // Read and validate version
        byte version = data[offset++];
        if (version != FormatVersion)
            throw new ArgumentException($"Unsupported format version: {version}. Expected version {FormatVersion}.", nameof(data));

        // Validate minimum header size
        if (data.Length < offset + 4 + 4 + 4 + 4 + 4 + 4)
            throw new ArgumentException("Data is too short to contain header fields.", nameof(data));

        // Read tree height
        int treeHeight = BinarySerializationHelpers.ReadInt32LittleEndian(data, offset);
        offset += 4;
        if (treeHeight < 0)
            throw new ArgumentException($"Invalid tree height: {treeHeight}. Must be non-negative.", nameof(data));

        // Read hash function name length
        int hashFunctionNameLength = BinarySerializationHelpers.ReadInt32LittleEndian(data, offset);
        offset += 4;
        if (hashFunctionNameLength < 0 || hashFunctionNameLength > 1024)
            throw new ArgumentException($"Invalid hash function name length: {hashFunctionNameLength}. Must be between 0 and 1024.", nameof(data));

        // Validate data contains hash function name
        if (offset + hashFunctionNameLength > data.Length)
            throw new ArgumentException("Data is too short to contain hash function name.", nameof(data));

        // Read hash function name
        string hashFunctionName = Encoding.UTF8.GetString(data, offset, hashFunctionNameLength);
        offset += hashFunctionNameLength;

        // Validate remaining header size
        if (offset + 4 + 4 + 4 + 4 > data.Length)
            throw new ArgumentException("Data is too short to contain remaining header fields.", nameof(data));

        // Read hash size
        int hashSizeInBytes = BinarySerializationHelpers.ReadInt32LittleEndian(data, offset);
        offset += 4;
        if (hashSizeInBytes <= 0)
            throw new ArgumentException($"Invalid hash size: {hashSizeInBytes}. Must be positive.", nameof(data));

        // Read start level
        int startLevel = BinarySerializationHelpers.ReadInt32LittleEndian(data, offset);
        offset += 4;
        if (startLevel < 0)
            throw new ArgumentException($"Invalid start level: {startLevel}. Must be non-negative.", nameof(data));

        // Read end level
        int endLevel = BinarySerializationHelpers.ReadInt32LittleEndian(data, offset);
        offset += 4;
        if (endLevel < startLevel)
            throw new ArgumentException($"Invalid end level: {endLevel}. Must be >= start level {startLevel}.", nameof(data));

        // Read number of levels
        int numLevels = BinarySerializationHelpers.ReadInt32LittleEndian(data, offset);
        offset += 4;
        int expectedNumLevels = endLevel - startLevel + 1;
        if (numLevels != expectedNumLevels)
            throw new ArgumentException($"Number of levels {numLevels} does not match expected {expectedNumLevels}.", nameof(data));

        // Create metadata
        var metadata = new CacheMetadata(treeHeight, hashFunctionName, hashSizeInBytes, startLevel, endLevel);

        // Read level data
        var levels = new Dictionary<int, CachedLevel>();
        for (int i = 0; i < numLevels; i++)
        {
            // Validate data contains level header
            if (offset + 4 + 8 > data.Length)
                throw new ArgumentException($"Data is too short to contain level {i} header.", nameof(data));

            // Read level number
            int levelNumber = BinarySerializationHelpers.ReadInt32LittleEndian(data, offset);
            offset += 4;

            // Validate level number is in expected range
            if (levelNumber < startLevel || levelNumber > endLevel)
                throw new ArgumentException($"Level number {levelNumber} is outside expected range [{startLevel}, {endLevel}].", nameof(data));

            // Read node count
            long nodeCount = BinarySerializationHelpers.ReadInt64LittleEndian(data, offset);
            offset += 8;
            if (nodeCount < 0)
                throw new ArgumentException($"Invalid node count for level {levelNumber}: {nodeCount}. Must be non-negative.", nameof(data));

            // Calculate total size needed for nodes
            long nodesSize = nodeCount * hashSizeInBytes;
            if (offset + nodesSize > data.Length)
                throw new ArgumentException($"Data is too short to contain {nodeCount} nodes for level {levelNumber}.", nameof(data));

            // Read all nodes for this level
            byte[][] nodes = new byte[nodeCount][];
            for (long j = 0; j < nodeCount; j++)
            {
                nodes[j] = new byte[hashSizeInBytes];
                Array.Copy(data, offset, nodes[j], 0, hashSizeInBytes);
                offset += hashSizeInBytes;
            }

            // Create cached level and add to dictionary
            var cachedLevel = new CachedLevel(levelNumber, nodes);
            levels[levelNumber] = cachedLevel;
        }

        // Verify we've consumed all the data
        if (offset != data.Length)
            throw new ArgumentException($"Data contains {data.Length - offset} extra bytes after deserialization.", nameof(data));

        // Create and return cache data
        return new CacheData(metadata, levels);
    }
}
