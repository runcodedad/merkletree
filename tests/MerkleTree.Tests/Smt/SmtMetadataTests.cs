using System;
using System.Buffers.Binary;
using Xunit;
using MerkleTree.Hashing;
using MerkleTree.Smt;

namespace MerkleTree.Tests.Smt;

/// <summary>
/// Tests for the SmtMetadata class to verify determinism, serialization, and versioning.
/// </summary>
public class SmtMetadataTests
{
    [Fact]
    public void Create_WithValidParameters_CreatesMetadata()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        int treeDepth = 8;

        // Act
        var metadata = SmtMetadata.Create(hashFunction, treeDepth);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(hashFunction.Name, metadata.HashAlgorithmId);
        Assert.Equal(treeDepth, metadata.TreeDepth);
        Assert.NotNull(metadata.ZeroHashes);
        Assert.Equal(treeDepth, metadata.ZeroHashes.Depth);
        Assert.Equal(SmtMetadata.CurrentSmtCoreVersion, metadata.SmtCoreVersion);
        Assert.Equal(SmtMetadata.CurrentSerializationFormatVersion, metadata.SerializationFormatVersion);
    }

    [Fact]
    public void Create_NullHashFunction_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SmtMetadata.Create(null!, 8));
    }

    [Fact]
    public void Create_InvalidDepth_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => SmtMetadata.Create(hashFunction, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SmtMetadata.Create(hashFunction, -1));
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesMetadata()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var zeroHashes = ZeroHashTable.Compute(hashFunction, 8);

        // Act
        var metadata = new SmtMetadata(
            hashAlgorithmId: hashFunction.Name,
            treeDepth: 8,
            zeroHashes: zeroHashes,
            smtCoreVersion: 1,
            serializationFormatVersion: 1);

        // Assert
        Assert.Equal(hashFunction.Name, metadata.HashAlgorithmId);
        Assert.Equal(8, metadata.TreeDepth);
        Assert.Equal(zeroHashes, metadata.ZeroHashes);
        Assert.Equal(1, metadata.SmtCoreVersion);
        Assert.Equal(1, metadata.SerializationFormatVersion);
    }

    [Fact]
    public void Constructor_NullHashAlgorithmId_ThrowsArgumentNullException()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var zeroHashes = ZeroHashTable.Compute(hashFunction, 8);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SmtMetadata(
            hashAlgorithmId: null!,
            treeDepth: 8,
            zeroHashes: zeroHashes));
    }

    [Fact]
    public void Constructor_EmptyHashAlgorithmId_ThrowsArgumentException()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var zeroHashes = ZeroHashTable.Compute(hashFunction, 8);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SmtMetadata(
            hashAlgorithmId: "",
            treeDepth: 8,
            zeroHashes: zeroHashes));
    }

    [Fact]
    public void Constructor_NullZeroHashes_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SmtMetadata(
            hashAlgorithmId: "SHA-256",
            treeDepth: 8,
            zeroHashes: null!));
    }

    [Fact]
    public void Constructor_MismatchedDepth_ThrowsArgumentException()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var zeroHashes = ZeroHashTable.Compute(hashFunction, 8);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new SmtMetadata(
            hashAlgorithmId: hashFunction.Name,
            treeDepth: 16, // Doesn't match zeroHashes depth of 8
            zeroHashes: zeroHashes));
        
        Assert.Contains("depth", ex.Message.ToLower());
    }

    [Fact]
    public void Constructor_MismatchedHashAlgorithm_ThrowsArgumentException()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var zeroHashes = ZeroHashTable.Compute(hashFunction, 8);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new SmtMetadata(
            hashAlgorithmId: "SHA-512", // Doesn't match zeroHashes algorithm
            treeDepth: 8,
            zeroHashes: zeroHashes));
        
        Assert.Contains("algorithm", ex.Message.ToLower());
    }

    [Fact]
    public void Constructor_NegativeVersions_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var zeroHashes = ZeroHashTable.Compute(hashFunction, 8);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SmtMetadata(
            hashAlgorithmId: hashFunction.Name,
            treeDepth: 8,
            zeroHashes: zeroHashes,
            smtCoreVersion: -1));

        Assert.Throws<ArgumentOutOfRangeException>(() => new SmtMetadata(
            hashAlgorithmId: hashFunction.Name,
            treeDepth: 8,
            zeroHashes: zeroHashes,
            smtCoreVersion: 1,
            serializationFormatVersion: -1));
    }

    [Fact]
    public void Create_SameInputs_ProducesIdenticalMetadata()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        int treeDepth = 16;

        // Act - Create multiple times
        var metadata1 = SmtMetadata.Create(hashFunction, treeDepth);
        var metadata2 = SmtMetadata.Create(hashFunction, treeDepth);
        var metadata3 = SmtMetadata.Create(hashFunction, treeDepth);

        // Assert - All metadata should be identical
        Assert.Equal(metadata1.HashAlgorithmId, metadata2.HashAlgorithmId);
        Assert.Equal(metadata2.HashAlgorithmId, metadata3.HashAlgorithmId);
        Assert.Equal(metadata1.TreeDepth, metadata2.TreeDepth);
        Assert.Equal(metadata2.TreeDepth, metadata3.TreeDepth);

        // Verify zero-hashes are identical
        for (int level = 0; level <= treeDepth; level++)
        {
            Assert.Equal(metadata1.ZeroHashes[level], metadata2.ZeroHashes[level]);
            Assert.Equal(metadata2.ZeroHashes[level], metadata3.ZeroHashes[level]);
        }
    }

    [Fact]
    public void Serialize_Deserialize_RoundTrip()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var original = SmtMetadata.Create(hashFunction, 8);

        // Act
        var serialized = original.Serialize();
        var deserialized = SmtMetadata.Deserialize(serialized);

        // Assert
        Assert.Equal(original.HashAlgorithmId, deserialized.HashAlgorithmId);
        Assert.Equal(original.TreeDepth, deserialized.TreeDepth);
        Assert.Equal(original.SmtCoreVersion, deserialized.SmtCoreVersion);
        Assert.Equal(original.SerializationFormatVersion, deserialized.SerializationFormatVersion);

        // Verify zero-hashes are identical
        for (int level = 0; level <= original.TreeDepth; level++)
        {
            Assert.Equal(original.ZeroHashes[level], deserialized.ZeroHashes[level]);
        }
    }

    [Fact]
    public void Serialize_ProducesDeterministicOutput()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var metadata = SmtMetadata.Create(hashFunction, 8);

        // Act - Serialize multiple times
        var serialized1 = metadata.Serialize();
        var serialized2 = metadata.Serialize();
        var serialized3 = metadata.Serialize();

        // Assert - All serializations should be identical
        Assert.Equal(serialized1, serialized2);
        Assert.Equal(serialized2, serialized3);
    }

    [Fact]
    public void Deserialize_NullData_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SmtMetadata.Deserialize(null!));
    }

    [Fact]
    public void Deserialize_InvalidData_ThrowsArgumentException()
    {
        // Arrange
        var tooShort = new byte[10];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => SmtMetadata.Deserialize(tooShort));
    }

    [Fact]
    public void Deserialize_UnsupportedVersion_ThrowsInvalidOperationException()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var metadata = SmtMetadata.Create(hashFunction, 8);
        var serialized = metadata.Serialize();

        // Modify the serialization version to an unsupported value using little-endian byte order
        var unsupportedVersion = 999;
        BinaryPrimitives.WriteInt32LittleEndian(serialized.AsSpan(0, 4), unsupportedVersion);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => SmtMetadata.Deserialize(serialized));
    }

    [Fact]
    public void Serialize_DifferentHashFunctions_ProduceDifferentOutput()
    {
        // Arrange
        var sha256 = new Sha256HashFunction();
        var sha512 = new Sha512HashFunction();
        var metadata256 = SmtMetadata.Create(sha256, 8);
        var metadata512 = SmtMetadata.Create(sha512, 8);

        // Act
        var serialized256 = metadata256.Serialize();
        var serialized512 = metadata512.Serialize();

        // Assert
        Assert.NotEqual(serialized256, serialized512);
    }

    [Fact]
    public void Serialize_DifferentDepths_ProduceDifferentOutput()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var metadata8 = SmtMetadata.Create(hashFunction, 8);
        var metadata16 = SmtMetadata.Create(hashFunction, 16);

        // Act
        var serialized8 = metadata8.Serialize();
        var serialized16 = metadata16.Serialize();

        // Assert
        Assert.NotEqual(serialized8, serialized16);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    public void Create_VariousDepths_AllSucceed(int depth)
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();

        // Act
        var metadata = SmtMetadata.Create(hashFunction, depth);

        // Assert
        Assert.Equal(depth, metadata.TreeDepth);
        Assert.Equal(depth, metadata.ZeroHashes.Depth);
    }

    [Fact]
    public void Create_WithDifferentHashFunctions_ProducesDifferentZeroHashes()
    {
        // Arrange
        var sha256 = new Sha256HashFunction();
        var sha512 = new Sha512HashFunction();
        int depth = 8;

        // Act
        var metadata256 = SmtMetadata.Create(sha256, depth);
        var metadata512 = SmtMetadata.Create(sha512, depth);

        // Assert
        Assert.NotEqual(metadata256.HashAlgorithmId, metadata512.HashAlgorithmId);
        Assert.NotEqual(metadata256.ZeroHashes[0], metadata512.ZeroHashes[0]);
    }

    [Fact]
    public void CurrentVersionConstants_HaveExpectedValues()
    {
        // Assert - Verify version constants are as expected
        Assert.Equal(1, SmtMetadata.CurrentSmtCoreVersion);
        Assert.Equal(1, SmtMetadata.CurrentSerializationFormatVersion);
    }

    [Fact]
    public void Serialize_Deserialize_PreservesVersions()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var zeroHashes = ZeroHashTable.Compute(hashFunction, 8);
        var original = new SmtMetadata(
            hashAlgorithmId: hashFunction.Name,
            treeDepth: 8,
            zeroHashes: zeroHashes,
            smtCoreVersion: 2, // Custom version
            serializationFormatVersion: 1);

        // Act
        var serialized = original.Serialize();
        var deserialized = SmtMetadata.Deserialize(serialized);

        // Assert
        Assert.Equal(2, deserialized.SmtCoreVersion);
        Assert.Equal(1, deserialized.SerializationFormatVersion);
    }

    [Fact]
    public void Metadata_CrossPlatformDeterminism_SHA256()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        int depth = 8;

        // Act
        var metadata = SmtMetadata.Create(hashFunction, depth);

        // Assert - Verify the first few zero-hashes have expected values
        // These are deterministic and should be the same across all platforms
        var level0 = metadata.ZeroHashes[0];
        Assert.Equal(32, level0.Length); // SHA-256 produces 32 bytes
        
        // The level 0 hash should be Hash(0x00) which is deterministic
        var expectedLevel0 = hashFunction.ComputeHash(new byte[] { 0x00 });
        Assert.Equal(expectedLevel0, level0);
    }

    [Fact]
    public void Metadata_CrossPlatformDeterminism_SHA512()
    {
        // Arrange
        var hashFunction = new Sha512HashFunction();
        int depth = 8;

        // Act
        var metadata = SmtMetadata.Create(hashFunction, depth);

        // Assert
        var level0 = metadata.ZeroHashes[0];
        Assert.Equal(64, level0.Length); // SHA-512 produces 64 bytes
        
        // The level 0 hash should be Hash(0x00) which is deterministic
        var expectedLevel0 = hashFunction.ComputeHash(new byte[] { 0x00 });
        Assert.Equal(expectedLevel0, level0);
    }
}
