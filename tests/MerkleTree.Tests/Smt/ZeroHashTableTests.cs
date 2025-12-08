using System;
using System.Buffers.Binary;
using System.Linq;
using Xunit;
using MerkleTree.Hashing;
using MerkleTree.Smt;

namespace MerkleTree.Tests.Smt;

/// <summary>
/// Tests for the ZeroHashTable class to verify determinism and correctness.
/// </summary>
public class ZeroHashTableTests
{
    [Fact]
    public void Compute_WithValidParameters_CreatesTable()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        int depth = 8;

        // Act
        var table = ZeroHashTable.Compute(hashFunction, depth);

        // Assert
        Assert.NotNull(table);
        Assert.Equal(hashFunction.Name, table.HashAlgorithmId);
        Assert.Equal(depth, table.Depth);
        Assert.Equal(hashFunction.HashSizeInBytes, table.HashSizeInBytes);
        Assert.Equal(depth + 1, table.Count);
    }

    [Fact]
    public void Compute_NullHashFunction_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ZeroHashTable.Compute(null!, 8));
    }

    [Fact]
    public void Compute_InvalidDepth_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => ZeroHashTable.Compute(hashFunction, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ZeroHashTable.Compute(hashFunction, -1));
    }

    [Fact]
    public void Compute_SameInputs_ProduceIdenticalTables()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        int depth = 16;

        // Act - Compute multiple times
        var table1 = ZeroHashTable.Compute(hashFunction, depth);
        var table2 = ZeroHashTable.Compute(hashFunction, depth);
        var table3 = ZeroHashTable.Compute(hashFunction, depth);

        // Assert - All tables should be identical
        for (int level = 0; level <= depth; level++)
        {
            Assert.Equal(table1[level], table2[level]);
            Assert.Equal(table2[level], table3[level]);
            Assert.Equal(table1[level], table3[level]);
        }
    }

    [Fact]
    public void Compute_DifferentHashFunctions_ProduceDifferentTables()
    {
        // Arrange
        var sha256 = new Sha256HashFunction();
        var sha512 = new Sha512HashFunction();
        int depth = 8;

        // Act
        var table256 = ZeroHashTable.Compute(sha256, depth);
        var table512 = ZeroHashTable.Compute(sha512, depth);

        // Assert - Tables should be different
        Assert.NotEqual(table256.HashAlgorithmId, table512.HashAlgorithmId);
        Assert.NotEqual(table256.HashSizeInBytes, table512.HashSizeInBytes);
        Assert.NotEqual(table256[0], table512[0]);
    }

    [Fact]
    public void Compute_DifferentDepths_ProduceDifferentTableSizes()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();

        // Act
        var table8 = ZeroHashTable.Compute(hashFunction, 8);
        var table16 = ZeroHashTable.Compute(hashFunction, 16);

        // Assert
        Assert.Equal(9, table8.Count);
        Assert.Equal(17, table16.Count);
        
        // Lower levels should match
        for (int level = 0; level <= 8; level++)
        {
            Assert.Equal(table8[level], table16[level]);
        }
    }

    [Fact]
    public void Indexer_ValidLevel_ReturnsHash()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var table = ZeroHashTable.Compute(hashFunction, 8);

        // Act
        var level0Hash = table[0];
        var level8Hash = table[8];

        // Assert
        Assert.NotNull(level0Hash);
        Assert.Equal(hashFunction.HashSizeInBytes, level0Hash.Length);
        Assert.NotNull(level8Hash);
        Assert.Equal(hashFunction.HashSizeInBytes, level8Hash.Length);
    }

    [Fact]
    public void Indexer_InvalidLevel_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var table = ZeroHashTable.Compute(hashFunction, 8);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => table[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => table[9]);
    }

    [Fact]
    public void Indexer_ReturnsCopy_ModificationDoesNotAffectOriginal()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var table = ZeroHashTable.Compute(hashFunction, 8);
        var originalHash = table[0];

        // Act - Modify the returned copy
        var copy = table[0];
        copy[0] = 0xFF;

        // Assert - Original should be unchanged
        var afterModification = table[0];
        Assert.Equal(originalHash, afterModification);
        Assert.NotEqual(copy, afterModification);
    }

    [Fact]
    public void GetAllHashes_ReturnsAllHashes()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        int depth = 8;
        var table = ZeroHashTable.Compute(hashFunction, depth);

        // Act
        var allHashes = table.GetAllHashes();

        // Assert
        Assert.NotNull(allHashes);
        Assert.Equal(depth + 1, allHashes.Count);
        
        for (int i = 0; i <= depth; i++)
        {
            Assert.Equal(table[i], allHashes[i]);
        }
    }

    [Fact]
    public void Serialize_Deserialize_RoundTrip()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        int depth = 8;
        var original = ZeroHashTable.Compute(hashFunction, depth);

        // Act
        var serialized = original.Serialize();
        var deserialized = ZeroHashTable.Deserialize(serialized);

        // Assert
        Assert.Equal(original.HashAlgorithmId, deserialized.HashAlgorithmId);
        Assert.Equal(original.Depth, deserialized.Depth);
        Assert.Equal(original.HashSizeInBytes, deserialized.HashSizeInBytes);
        Assert.Equal(original.Count, deserialized.Count);

        for (int level = 0; level <= depth; level++)
        {
            Assert.Equal(original[level], deserialized[level]);
        }
    }

    [Fact]
    public void Serialize_ProducesDeterministicOutput()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var table = ZeroHashTable.Compute(hashFunction, 8);

        // Act - Serialize multiple times
        var serialized1 = table.Serialize();
        var serialized2 = table.Serialize();
        var serialized3 = table.Serialize();

        // Assert - All serializations should be identical
        Assert.Equal(serialized1, serialized2);
        Assert.Equal(serialized2, serialized3);
    }

    [Fact]
    public void Deserialize_NullData_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ZeroHashTable.Deserialize(null!));
    }

    [Fact]
    public void Deserialize_InvalidData_ThrowsArgumentException()
    {
        // Arrange
        const int Int32SizeInBytes = 4;
        var tooShort = new byte[5];
        var invalidDepth = new byte[12];
        // Set depth to 0 (invalid) using little-endian byte order
        BinaryPrimitives.WriteInt32LittleEndian(invalidDepth.AsSpan(0, Int32SizeInBytes), 0);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ZeroHashTable.Deserialize(tooShort));
        Assert.Throws<ArgumentException>(() => ZeroHashTable.Deserialize(invalidDepth));
    }

    [Fact]
    public void Verify_WithMatchingHashFunction_ReturnsTrue()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var table = ZeroHashTable.Compute(hashFunction, 8);

        // Act
        bool isValid = table.Verify(hashFunction);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Verify_WithDifferentHashFunction_ReturnsFalse()
    {
        // Arrange
        var sha256 = new Sha256HashFunction();
        var sha512 = new Sha512HashFunction();
        var table = ZeroHashTable.Compute(sha256, 8);

        // Act
        bool isValid = table.Verify(sha512);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Verify_AfterDeserialization_ReturnsTrue()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var original = ZeroHashTable.Compute(hashFunction, 8);
        var serialized = original.Serialize();
        var deserialized = ZeroHashTable.Deserialize(serialized);

        // Act
        bool isValid = deserialized.Verify(hashFunction);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Compute_ZeroHashesAreUnique_AcrossLevels()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        int depth = 16;
        var table = ZeroHashTable.Compute(hashFunction, depth);

        // Act - Collect all hashes as hex strings for easy comparison
        var hashSet = new System.Collections.Generic.HashSet<string>();
        for (int level = 0; level <= depth; level++)
        {
            hashSet.Add(Convert.ToHexString(table[level]));
        }

        // Assert - All hashes should be unique
        Assert.Equal(depth + 1, hashSet.Count);
    }

    [Fact]
    public void Compute_LargeDepth_Succeeds()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        int depth = 256; // Large depth for real-world SMT

        // Act
        var table = ZeroHashTable.Compute(hashFunction, depth);

        // Assert
        Assert.Equal(257, table.Count);
        Assert.All(table.GetAllHashes(), hash => Assert.Equal(32, hash.Length));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    public void Compute_VariousDepths_AllSucceed(int depth)
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();

        // Act
        var table = ZeroHashTable.Compute(hashFunction, depth);

        // Assert
        Assert.Equal(depth, table.Depth);
        Assert.Equal(depth + 1, table.Count);
    }
}
