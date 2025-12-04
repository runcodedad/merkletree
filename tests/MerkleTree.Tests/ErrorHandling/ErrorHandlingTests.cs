using System.Buffers.Binary;
using System.Text;
using Xunit;
using MerkleTree.Hashing;
using MerkleTree.Proofs;
using MerkleTree.Cache;
using MerkleTreeClass = MerkleTree.Core.MerkleTree;

namespace MerkleTree.Tests.ErrorHandling;

/// <summary>
/// Tests for comprehensive error handling as specified in the issue requirements.
/// </summary>
public class ErrorHandlingTests
{
    private static List<byte[]> CreateLeafData(params string[] data)
    {
        return data.Select(s => Encoding.UTF8.GetBytes(s)).ToList();
    }

    #region Mismatched Root Hash Tests

    [Fact]
    public void Verify_WithMismatchedRoot_ReturnsFalse()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3");
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(0);
        var hashFunction = new Sha256HashFunction();
        
        // Create a different root hash
        var wrongRootHash = new byte[32];
        Array.Fill(wrongRootHash, (byte)0xFF);

        // Act
        var result = proof.Verify(wrongRootHash, hashFunction);

        // Assert - Should return false, not throw
        Assert.False(result);
    }

    #endregion

    #region Invalid Proof Structure Tests

    [Fact]
    public void MerkleProof_Constructor_WithInconsistentSiblingHashSizes_ThrowsWithContext()
    {
        // Arrange - Create sibling hashes with different sizes
        var siblingHashes = new byte[][]
        {
            new byte[32], // First hash is 32 bytes
            new byte[64]  // Second hash is 64 bytes - inconsistent!
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new MerkleProof(
            new byte[] { 1, 2, 3 },
            0,
            2,
            siblingHashes,
            new bool[] { true, false }));
        
        // Verify error message contains context
        Assert.Contains("sibling", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("length", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_WithInconsistentHashSizes_ThrowsWithContext()
    {
        // Arrange - Create a valid proof and manipulate its serialized form
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3");
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(0);
        var serialized = proof.Serialize();
        
        // Manipulate the hash size field to be inconsistent
        // Hash size is at offset: version(1) + treeHeight(4) + leafIndex(8) + leafValueLength(4) + leafValue
        int hashSizeOffset = 1 + 4 + 8 + 4 + 5;
        
        // Change the hash size to something unrealistic
        BinaryPrimitives.WriteInt32LittleEndian(serialized.AsSpan(hashSizeOffset), 16); // Change from 32 to 16

        // Act & Assert - This will cause extra bytes detection since hash size doesn't match actual data
        var ex = Assert.Throws<ArgumentException>(() => MerkleProof.Deserialize(serialized));
        Assert.Contains("extra bytes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Missing Sibling Levels Tests

    [Fact]
    public void MerkleProof_Constructor_WithFewerSiblingsThanHeight_ThrowsWithContext()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new MerkleProof(
            new byte[] { 1, 2, 3 },
            0,
            3, // Height is 3
            new byte[][] { new byte[] { 1, 2 } }, // But only 1 sibling
            new bool[] { true }));
        
        // Verify error message has explicit context
        Assert.Contains("3", ex.Message); // Expected count
        Assert.Contains("1", ex.Message); // Actual count
        Assert.Contains("sibling", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MerkleProof_Constructor_WithMoreSiblingsThanHeight_ThrowsWithContext()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new MerkleProof(
            new byte[] { 1, 2, 3 },
            0,
            1, // Height is 1
            new byte[][] { new byte[] { 1, 2 }, new byte[] { 3, 4 } }, // But 2 siblings
            new bool[] { true, false }));
        
        // Verify error message has explicit context
        Assert.Contains("1", ex.Message); // Expected count
        Assert.Contains("2", ex.Message); // Actual count
        Assert.Contains("sibling", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Corrupted Leaf Entry Tests

    [Fact]
    public void CacheSerializer_Deserialize_WithCorruptedNodeData_ThrowsWithContext()
    {
        // Arrange - Create valid cache data
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3", "leaf4");
        var tree = new MerkleTreeClass(leafData);
        var metadata = new CacheMetadata(
            treeHeight: 2,
            hashFunctionName: "SHA256",
            hashSizeInBytes: 32,
            startLevel: 0,
            endLevel: 1);
        
        var levels = new Dictionary<int, CachedLevel>
        {
            [0] = new CachedLevel(0, new byte[][] { new byte[32], new byte[32] }),
            [1] = new CachedLevel(1, new byte[][] { new byte[32] })
        };
        
        var cacheData = new CacheData(metadata, levels);
        var serialized = CacheSerializer.Serialize(cacheData);
        
        // Corrupt a node by truncating the data
        var corrupted = new byte[serialized.Length - 10];
        Array.Copy(serialized, corrupted, corrupted.Length);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => CacheSerializer.Deserialize(corrupted));
        Assert.Contains("too short", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CachedLevel_Constructor_WithNullNode_ThrowsWithContext()
    {
        // Arrange
        var nodes = new byte[][] { new byte[32], null!, new byte[32] };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new CachedLevel(0, nodes));
        Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1", ex.Message); // Node index
    }

    #endregion

    #region Incorrect Leaf Size Tests

    [Fact]
    public void Deserialize_WithNegativeLeafValueLength_ThrowsWithContext()
    {
        // Arrange - Create data with negative leaf value length
        var data = new byte[30];
        data[0] = 1; // version
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1), 0); // tree height = 0
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(5), 0L); // leaf index = 0
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(13), -1); // negative leaf value length

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => MerkleProof.Deserialize(data));
        Assert.Contains("leaf value length", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("negative", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("-1", ex.Message);
    }

    [Fact]
    public void CacheSerializer_Deserialize_WithNegativeHashSize_ThrowsWithContext()
    {
        // Arrange - Manually create data with negative hash size
        var magicNumber = new byte[] { 0x4D, 0x4B, 0x54, 0x43 }; // "MKTC"
        var data = new byte[100];
        int offset = 0;
        
        // Magic number
        magicNumber.CopyTo(data, offset);
        offset += 4;
        
        // Version
        data[offset++] = 1;
        
        // Tree height
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), 2);
        offset += 4;
        
        // Hash function name length
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), 6);
        offset += 4;
        
        // Hash function name
        Encoding.UTF8.GetBytes("SHA256").CopyTo(data, offset);
        offset += 6;
        
        // Hash size (negative!)
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), -32);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => CacheSerializer.Deserialize(data));
        Assert.Contains("hash size", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("positive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Invalid Index Range Tests

    [Fact]
    public void GenerateProof_WithNegativeIndex_ThrowsWithContext()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2");
        var tree = new MerkleTreeClass(leafData);

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => tree.GenerateProof(-1));
        Assert.Contains("-1", ex.Message);
        Assert.Contains("index", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateProof_WithIndexBeyondBounds_ThrowsWithContext()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2");
        var tree = new MerkleTreeClass(leafData);

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => tree.GenerateProof(10));
        Assert.Contains("10", ex.Message);
        Assert.Contains("index", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_WithNegativeLeafIndex_ThrowsWithContext()
    {
        // Arrange - Create data with negative leaf index
        var data = new byte[30];
        data[0] = 1; // version
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1), 0); // tree height = 0
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(5), -5L); // negative leaf index

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => MerkleProof.Deserialize(data));
        Assert.Contains("leaf index", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("negative", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("-5", ex.Message);
    }

    #endregion

    #region Unexpected End of File / Truncated File Tests

    [Fact]
    public void Deserialize_WithTruncatedHeader_ThrowsWithContext()
    {
        // Arrange - Create data that's too short for header
        var data = new byte[5]; // Too short to contain full header
        data[0] = 1; // version

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => MerkleProof.Deserialize(data));
        Assert.Contains("too short", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("header", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_WithTruncatedLeafValue_ThrowsWithContext()
    {
        // Arrange - Create data with truncated leaf value
        var data = new byte[20];
        data[0] = 1; // version
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1), 0); // tree height = 0
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(5), 0L); // leaf index = 0
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(13), 100); // leaf value length = 100 (but data is too short)

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => MerkleProof.Deserialize(data));
        Assert.Contains("too short", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("leaf value", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_WithTruncatedSiblingHashes_ThrowsWithContext()
    {
        // Arrange - Create a valid small proof and truncate it
        var leafData = CreateLeafData("leaf1", "leaf2");
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(0);
        var serialized = proof.Serialize();
        
        // Truncate by removing last 10 bytes
        var truncated = new byte[serialized.Length - 10];
        Array.Copy(serialized, truncated, truncated.Length);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => MerkleProof.Deserialize(truncated));
        Assert.Contains("too short", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sibling hash", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CacheSerializer_Deserialize_WithTruncatedHeader_ThrowsWithContext()
    {
        // Arrange - Create data that's too short for header
        var data = new byte[10];
        data[0] = 0x4D; // 'M' from magic number
        data[1] = 0x4B; // 'K'
        data[2] = 0x54; // 'T'
        data[3] = 0x43; // 'C'
        data[4] = 1; // version

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => CacheSerializer.Deserialize(data));
        Assert.Contains("too short", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("header", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CacheSerializer_Deserialize_WithTruncatedLevelData_ThrowsWithContext()
    {
        // Arrange - Create valid cache data
        var leafData = CreateLeafData("leaf1", "leaf2");
        var tree = new MerkleTreeClass(leafData);
        var metadata = new CacheMetadata(
            treeHeight: 1,
            hashFunctionName: "SHA256",
            hashSizeInBytes: 32,
            startLevel: 0,
            endLevel: 0);
        
        var levels = new Dictionary<int, CachedLevel>
        {
            [0] = new CachedLevel(0, new byte[][] { new byte[32] })
        };
        
        var cacheData = new CacheData(metadata, levels);
        var serialized = CacheSerializer.Serialize(cacheData);
        
        // Truncate the level data section
        var truncated = new byte[serialized.Length - 20];
        Array.Copy(serialized, truncated, truncated.Length);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => CacheSerializer.Deserialize(truncated));
        Assert.Contains("too short", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_WithExtraBytes_ThrowsWithContext()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2");
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(0);
        var serialized = proof.Serialize();
        
        // Add extra bytes
        var withExtra = new byte[serialized.Length + 15];
        serialized.CopyTo(withExtra, 0);
        // Fill extra bytes with non-zero data
        for (int i = serialized.Length; i < withExtra.Length; i++)
        {
            withExtra[i] = 0xAA;
        }

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => MerkleProof.Deserialize(withExtra));
        Assert.Contains("extra bytes", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("15", ex.Message); // Number of extra bytes
    }

    #endregion

    #region Cache File I/O Error Tests

    [Fact]
    public void LoadCache_WithNonexistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonexistentPath = "/tmp/nonexistent_cache_" + Guid.NewGuid() + ".cache";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => CacheFileManager.LoadCache(nonexistentPath));
    }

    [Fact]
    public void LoadCache_WithNullPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CacheFileManager.LoadCache(null!));
    }

    #endregion

    #region Constructor Validation Tests for Null and Empty Sibling Hashes

    [Fact]
    public void MerkleProof_Constructor_WithNullSiblingHashAtIndex0_ThrowsWithContext()
    {
        // Act & Assert - Constructor now validates null sibling hashes
        var ex = Assert.Throws<ArgumentException>(() => new MerkleProof(
            new byte[] { 1, 2, 3 },
            0,
            1,
            new byte[][] { null! },
            new bool[] { true }));

        Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("index 0", ex.Message); // Specific index context
    }

    [Fact]
    public void MerkleProof_Constructor_WithNullSiblingHashAtIndex2_ThrowsWithContext()
    {
        // Act & Assert - Constructor validates null sibling hash at specific index
        var ex = Assert.Throws<ArgumentException>(() => new MerkleProof(
            new byte[] { 1, 2, 3 },
            0,
            3,
            new byte[][] { new byte[32], new byte[32], null! },
            new bool[] { true, false, true }));

        Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("index 2", ex.Message); // Specific index context
    }

    [Fact]
    public void MerkleProof_Constructor_WithInconsistentSiblingHashSizesAtIndex1_ThrowsWithContext()
    {
        // Act & Assert - Constructor validates inconsistent sibling hash sizes
        var ex = Assert.Throws<ArgumentException>(() => new MerkleProof(
            new byte[] { 1, 2, 3 },
            0,
            2,
            new byte[][] { new byte[32], new byte[16] }, // Different sizes!
            new bool[] { true, false }));

        Assert.Contains("index 1", ex.Message); // Specific index context
        Assert.Contains("16", ex.Message); // Actual size
        Assert.Contains("32", ex.Message); // Expected size
        Assert.Contains("inconsistent", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_WithEmptySiblingHash_ThrowsWithContext()
    {
        // Arrange - Create proof with empty sibling hash (0 length is valid for constructor check, but not for Verify)
        var proof = new MerkleProof(
            new byte[] { 1, 2, 3 },
            0,
            1,
            new byte[][] { Array.Empty<byte>() },
            new bool[] { true });

        var hashFunction = new Sha256HashFunction();
        var rootHash = new byte[32];

        // Act & Assert - Verify should catch empty hash
        var ex = Assert.Throws<InvalidOperationException>(() => proof.Verify(rootHash, hashFunction));
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("level 0", ex.Message); // Specific level context
    }

    #endregion
}
