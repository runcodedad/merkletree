using Xunit;
using MerkleTree.Cache;

namespace MerkleTree.Tests.Cache;

/// <summary>
/// Tests for cache serialization and deserialization.
/// </summary>
public class CacheSerializerTests
{
    /// <summary>
    /// Helper method to create sample hash data.
    /// </summary>
    private static byte[] CreateHash(int size, byte fillValue)
    {
        var hash = new byte[size];
        for (int i = 0; i < size; i++)
        {
            hash[i] = (byte)(fillValue + i);
        }
        return hash;
    }

    /// <summary>
    /// Helper method to create a simple cache data for testing.
    /// </summary>
    private static CacheData CreateSimpleCacheData(
        string hashFunction = "SHA256",
        int hashSize = 32,
        int treeHeight = 5,
        int startLevel = 1,
        int endLevel = 3)
    {
        var metadata = new CacheMetadata(treeHeight, hashFunction, hashSize, startLevel, endLevel);
        var levels = new Dictionary<int, CachedLevel>();

        // Create sample data for each level
        for (int level = startLevel; level <= endLevel; level++)
        {
            // Create a different number of nodes for each level
            int nodeCount = (int)Math.Pow(2, level);
            var nodes = new byte[nodeCount][];
            for (int i = 0; i < nodeCount; i++)
            {
                nodes[i] = CreateHash(hashSize, (byte)(level * 10 + i));
            }
            levels[level] = new CachedLevel(level, nodes);
        }

        return new CacheData(metadata, levels);
    }

    [Fact]
    public void Serialize_WithValidCacheData_ProducesNonEmptyResult()
    {
        // Arrange
        var cacheData = CreateSimpleCacheData();

        // Act
        var serialized = CacheSerializer.Serialize(cacheData);

        // Assert
        Assert.NotNull(serialized);
        Assert.NotEmpty(serialized);
    }

    [Fact]
    public void Serialize_WithNullCacheData_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CacheSerializer.Serialize(null!));
    }

    [Fact]
    public void Deserialize_WithNullData_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CacheSerializer.Deserialize(null!));
    }

    [Fact]
    public void Deserialize_WithEmptyData_ThrowsArgumentException()
    {
        // Arrange
        var emptyData = Array.Empty<byte>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CacheSerializer.Deserialize(emptyData));
    }

    [Fact]
    public void Deserialize_WithInvalidMagicNumber_ThrowsArgumentException()
    {
        // Arrange
        var invalidData = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x01 };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => CacheSerializer.Deserialize(invalidData));
        Assert.Contains("magic number", ex.Message);
    }

    [Fact]
    public void Deserialize_WithInvalidVersion_ThrowsArgumentException()
    {
        // Arrange - valid magic number but wrong version
        var invalidData = new byte[] { 0x4D, 0x4B, 0x54, 0x43, 0xFF }; // "MKTC" + version 255

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => CacheSerializer.Deserialize(invalidData));
        Assert.Contains("version", ex.Message);
    }

    [Fact]
    public void RoundTrip_WithSHA256SingleLevel_PreservesData()
    {
        // Arrange
        var originalCache = CreateSimpleCacheData(
            hashFunction: "SHA256",
            hashSize: 32,
            treeHeight: 3,
            startLevel: 1,
            endLevel: 1);

        // Act
        var serialized = CacheSerializer.Serialize(originalCache);
        var deserialized = CacheSerializer.Deserialize(serialized);

        // Assert metadata
        Assert.Equal(originalCache.Metadata.TreeHeight, deserialized.Metadata.TreeHeight);
        Assert.Equal(originalCache.Metadata.HashFunctionName, deserialized.Metadata.HashFunctionName);
        Assert.Equal(originalCache.Metadata.HashSizeInBytes, deserialized.Metadata.HashSizeInBytes);
        Assert.Equal(originalCache.Metadata.StartLevel, deserialized.Metadata.StartLevel);
        Assert.Equal(originalCache.Metadata.EndLevel, deserialized.Metadata.EndLevel);

        // Assert levels
        var originalLevel = originalCache.GetLevel(1);
        var deserializedLevel = deserialized.GetLevel(1);
        Assert.Equal(originalLevel.Level, deserializedLevel.Level);
        Assert.Equal(originalLevel.NodeCount, deserializedLevel.NodeCount);

        // Assert nodes
        for (int i = 0; i < originalLevel.NodeCount; i++)
        {
            Assert.Equal(originalLevel.GetNode(i), deserializedLevel.GetNode(i));
        }
    }

    [Fact]
    public void RoundTrip_WithSHA256MultipleLevels_PreservesData()
    {
        // Arrange
        var originalCache = CreateSimpleCacheData(
            hashFunction: "SHA256",
            hashSize: 32,
            treeHeight: 5,
            startLevel: 1,
            endLevel: 3);

        // Act
        var serialized = CacheSerializer.Serialize(originalCache);
        var deserialized = CacheSerializer.Deserialize(serialized);

        // Assert metadata
        Assert.Equal(originalCache.Metadata.TreeHeight, deserialized.Metadata.TreeHeight);
        Assert.Equal(originalCache.Metadata.HashFunctionName, deserialized.Metadata.HashFunctionName);
        Assert.Equal(originalCache.Metadata.HashSizeInBytes, deserialized.Metadata.HashSizeInBytes);
        Assert.Equal(originalCache.Metadata.StartLevel, deserialized.Metadata.StartLevel);
        Assert.Equal(originalCache.Metadata.EndLevel, deserialized.Metadata.EndLevel);

        // Assert all levels
        for (int level = originalCache.Metadata.StartLevel; level <= originalCache.Metadata.EndLevel; level++)
        {
            var originalLevel = originalCache.GetLevel(level);
            var deserializedLevel = deserialized.GetLevel(level);
            
            Assert.Equal(originalLevel.Level, deserializedLevel.Level);
            Assert.Equal(originalLevel.NodeCount, deserializedLevel.NodeCount);

            // Assert all nodes in the level
            for (int i = 0; i < originalLevel.NodeCount; i++)
            {
                Assert.Equal(originalLevel.GetNode(i), deserializedLevel.GetNode(i));
            }
        }
    }

    [Fact]
    public void RoundTrip_WithSHA512_PreservesData()
    {
        // Arrange
        var originalCache = CreateSimpleCacheData(
            hashFunction: "SHA512",
            hashSize: 64,
            treeHeight: 4,
            startLevel: 0,
            endLevel: 2);

        // Act
        var serialized = CacheSerializer.Serialize(originalCache);
        var deserialized = CacheSerializer.Deserialize(serialized);

        // Assert metadata
        Assert.Equal(originalCache.Metadata.TreeHeight, deserialized.Metadata.TreeHeight);
        Assert.Equal(originalCache.Metadata.HashFunctionName, deserialized.Metadata.HashFunctionName);
        Assert.Equal(originalCache.Metadata.HashSizeInBytes, deserialized.Metadata.HashSizeInBytes);
        Assert.Equal(originalCache.Metadata.StartLevel, deserialized.Metadata.StartLevel);
        Assert.Equal(originalCache.Metadata.EndLevel, deserialized.Metadata.EndLevel);

        // Verify hash size is 64 for SHA512
        Assert.Equal(64, deserialized.Metadata.HashSizeInBytes);
    }

    [Fact]
    public void RoundTrip_WithBLAKE3_PreservesData()
    {
        // Arrange
        var originalCache = CreateSimpleCacheData(
            hashFunction: "BLAKE3",
            hashSize: 32,
            treeHeight: 6,
            startLevel: 2,
            endLevel: 4);

        // Act
        var serialized = CacheSerializer.Serialize(originalCache);
        var deserialized = CacheSerializer.Deserialize(serialized);

        // Assert metadata
        Assert.Equal(originalCache.Metadata.TreeHeight, deserialized.Metadata.TreeHeight);
        Assert.Equal(originalCache.Metadata.HashFunctionName, deserialized.Metadata.HashFunctionName);
        Assert.Equal(originalCache.Metadata.HashSizeInBytes, deserialized.Metadata.HashSizeInBytes);
        Assert.Equal(originalCache.Metadata.StartLevel, deserialized.Metadata.StartLevel);
        Assert.Equal(originalCache.Metadata.EndLevel, deserialized.Metadata.EndLevel);
    }

    [Fact]
    public void RoundTrip_WithLargeNodeCounts_PreservesData()
    {
        // Arrange - Create a cache with a large number of nodes
        var metadata = new CacheMetadata(10, "SHA256", 32, 5, 5);
        var levels = new Dictionary<int, CachedLevel>();
        
        // Create 1000 nodes at level 5
        int nodeCount = 1000;
        var nodes = new byte[nodeCount][];
        for (int i = 0; i < nodeCount; i++)
        {
            nodes[i] = CreateHash(32, (byte)(i % 256));
        }
        levels[5] = new CachedLevel(5, nodes);
        
        var originalCache = new CacheData(metadata, levels);

        // Act
        var serialized = CacheSerializer.Serialize(originalCache);
        var deserialized = CacheSerializer.Deserialize(serialized);

        // Assert
        var originalLevel = originalCache.GetLevel(5);
        var deserializedLevel = deserialized.GetLevel(5);
        Assert.Equal(nodeCount, deserializedLevel.NodeCount);
        
        // Verify a few sample nodes
        Assert.Equal(originalLevel.GetNode(0), deserializedLevel.GetNode(0));
        Assert.Equal(originalLevel.GetNode(500), deserializedLevel.GetNode(500));
        Assert.Equal(originalLevel.GetNode(999), deserializedLevel.GetNode(999));
    }

    [Fact]
    public void Serialize_WithMagicNumber_StartsWithCorrectBytes()
    {
        // Arrange
        var cacheData = CreateSimpleCacheData();

        // Act
        var serialized = CacheSerializer.Serialize(cacheData);

        // Assert - Check magic number "MKTC"
        Assert.Equal(0x4D, serialized[0]); // 'M'
        Assert.Equal(0x4B, serialized[1]); // 'K'
        Assert.Equal(0x54, serialized[2]); // 'T'
        Assert.Equal(0x43, serialized[3]); // 'C'
    }

    [Fact]
    public void Serialize_WithVersion_ContainsVersionByte()
    {
        // Arrange
        var cacheData = CreateSimpleCacheData();

        // Act
        var serialized = CacheSerializer.Serialize(cacheData);

        // Assert - Check version byte (position 4)
        Assert.Equal(1, serialized[4]); // Version 1
    }

    [Fact]
    public void Deserialize_WithTruncatedData_ThrowsArgumentException()
    {
        // Arrange
        var cacheData = CreateSimpleCacheData();
        var serialized = CacheSerializer.Serialize(cacheData);
        
        // Truncate the data
        var truncatedData = new byte[serialized.Length / 2];
        Array.Copy(serialized, truncatedData, truncatedData.Length);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CacheSerializer.Deserialize(truncatedData));
    }

    [Fact]
    public void Deserialize_WithExtraData_ThrowsArgumentException()
    {
        // Arrange
        var cacheData = CreateSimpleCacheData();
        var serialized = CacheSerializer.Serialize(cacheData);
        
        // Add extra bytes
        var extendedData = new byte[serialized.Length + 10];
        Array.Copy(serialized, extendedData, serialized.Length);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => CacheSerializer.Deserialize(extendedData));
        Assert.Contains("extra bytes", ex.Message);
    }

    [Fact]
    public void RoundTrip_WithLevelZero_PreservesData()
    {
        // Arrange - Test with level 0 (leaves)
        var originalCache = CreateSimpleCacheData(
            hashFunction: "SHA256",
            hashSize: 32,
            treeHeight: 3,
            startLevel: 0,
            endLevel: 0);

        // Act
        var serialized = CacheSerializer.Serialize(originalCache);
        var deserialized = CacheSerializer.Deserialize(serialized);

        // Assert
        Assert.Equal(0, deserialized.Metadata.StartLevel);
        Assert.Equal(0, deserialized.Metadata.EndLevel);
        
        var level = deserialized.GetLevel(0);
        Assert.Equal(0, level.Level);
    }

    [Fact]
    public void RoundTrip_WithSingleNodePerLevel_PreservesData()
    {
        // Arrange - Create cache with exactly one node per level
        var metadata = new CacheMetadata(3, "SHA256", 32, 1, 2);
        var levels = new Dictionary<int, CachedLevel>();
        
        for (int level = 1; level <= 2; level++)
        {
            var nodes = new byte[1][];
            nodes[0] = CreateHash(32, (byte)(level * 10));
            levels[level] = new CachedLevel(level, nodes);
        }
        
        var originalCache = new CacheData(metadata, levels);

        // Act
        var serialized = CacheSerializer.Serialize(originalCache);
        var deserialized = CacheSerializer.Deserialize(serialized);

        // Assert
        for (int level = 1; level <= 2; level++)
        {
            var originalLevel = originalCache.GetLevel(level);
            var deserializedLevel = deserialized.GetLevel(level);
            Assert.Equal(1, deserializedLevel.NodeCount);
            Assert.Equal(originalLevel.GetNode(0), deserializedLevel.GetNode(0));
        }
    }

    [Fact]
    public void RoundTrip_WithDifferentHashFunctionNames_PreservesNames()
    {
        // Test various hash function names
        var hashFunctionNames = new[] { "SHA256", "SHA512", "BLAKE3", "MD5", "CustomHash" };

        foreach (var hashName in hashFunctionNames)
        {
            // Arrange
            var originalCache = CreateSimpleCacheData(
                hashFunction: hashName,
                hashSize: 32,
                treeHeight: 3,
                startLevel: 1,
                endLevel: 1);

            // Act
            var serialized = CacheSerializer.Serialize(originalCache);
            var deserialized = CacheSerializer.Deserialize(serialized);

            // Assert
            Assert.Equal(hashName, deserialized.Metadata.HashFunctionName);
        }
    }

    [Fact]
    public void Serialize_IsDeterministic_ProducesSameOutputForSameInput()
    {
        // Arrange
        var cacheData = CreateSimpleCacheData();

        // Act
        var serialized1 = CacheSerializer.Serialize(cacheData);
        var serialized2 = CacheSerializer.Serialize(cacheData);

        // Assert
        Assert.Equal(serialized1, serialized2);
    }
}
