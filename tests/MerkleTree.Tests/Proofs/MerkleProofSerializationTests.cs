using System.Buffers.Binary;
using System.Text;
using Xunit;
using MerkleTree.Hashing;
using MerkleTree.Proofs;
using MerkleTreeClass = MerkleTree.Core.MerkleTree;

namespace MerkleTree.Tests.Proofs;

/// <summary>
/// Tests for Merkle proof serialization and deserialization.
/// </summary>
public class MerkleProofSerializationTests
{
    /// <summary>
    /// Helper method to create leaf data from strings.
    /// </summary>
    private static List<byte[]> CreateLeafData(params string[] data)
    {
        return data.Select(s => Encoding.UTF8.GetBytes(s)).ToList();
    }

    [Fact]
    public void Serialize_WithSingleLeaf_ProducesValidBinary()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1");
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(0);

        // Act
        var serialized = proof.Serialize();

        // Assert
        Assert.NotNull(serialized);
        Assert.NotEmpty(serialized);
        // Version (1) + TreeHeight (4) + LeafIndex (8) + LeafValueLength (4) + LeafValue (5) + 
        // HashSize (4) + OrientationBytesLength (4) + OrientationBytes (0) + SiblingHashes (0)
        Assert.Equal(1 + 4 + 8 + 4 + 5 + 4 + 4 + 0 + 0, serialized.Length);
    }

    [Fact]
    public void Serialize_WithTwoLeaves_ProducesValidBinary()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2");
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(0);

        // Act
        var serialized = proof.Serialize();

        // Assert
        Assert.NotNull(serialized);
        Assert.NotEmpty(serialized);
        // Should contain 1 sibling hash (32 bytes for SHA-256) and 1 orientation bit (1 byte)
        int expectedSize = 1 + 4 + 8 + 4 + 5 + 4 + 4 + 1 + 32;
        Assert.Equal(expectedSize, serialized.Length);
    }

    [Fact]
    public void Serialize_WithThreeLeaves_ProducesValidBinary()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3");
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(1);

        // Act
        var serialized = proof.Serialize();

        // Assert
        Assert.NotNull(serialized);
        Assert.NotEmpty(serialized);
        // Should contain 2 sibling hashes (2 * 32 bytes) and 2 orientation bits (1 byte)
        int expectedSize = 1 + 4 + 8 + 4 + 5 + 4 + 4 + 1 + (2 * 32);
        Assert.Equal(expectedSize, serialized.Length);
    }

    [Fact]
    public void Deserialize_RoundTrip_WithSingleLeaf_PreservesProof()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1");
        var tree = new MerkleTreeClass(leafData);
        var originalProof = tree.GenerateProof(0);

        // Act
        var serialized = originalProof.Serialize();
        var deserializedProof = MerkleProof.Deserialize(serialized);

        // Assert
        Assert.Equal(originalProof.LeafIndex, deserializedProof.LeafIndex);
        Assert.Equal(originalProof.TreeHeight, deserializedProof.TreeHeight);
        Assert.Equal(originalProof.LeafValue, deserializedProof.LeafValue);
        Assert.Equal(originalProof.SiblingHashes.Length, deserializedProof.SiblingHashes.Length);
        Assert.Equal(originalProof.SiblingIsRight.Length, deserializedProof.SiblingIsRight.Length);
    }

    [Fact]
    public void Deserialize_RoundTrip_WithMultipleLeaves_PreservesProof()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3", "leaf4");
        var tree = new MerkleTreeClass(leafData);
        var originalProof = tree.GenerateProof(2);

        // Act
        var serialized = originalProof.Serialize();
        var deserializedProof = MerkleProof.Deserialize(serialized);

        // Assert
        Assert.Equal(originalProof.LeafIndex, deserializedProof.LeafIndex);
        Assert.Equal(originalProof.TreeHeight, deserializedProof.TreeHeight);
        Assert.Equal(originalProof.LeafValue, deserializedProof.LeafValue);
        Assert.Equal(originalProof.SiblingHashes.Length, deserializedProof.SiblingHashes.Length);
        Assert.Equal(originalProof.SiblingIsRight.Length, deserializedProof.SiblingIsRight.Length);

        // Verify sibling hashes
        for (int i = 0; i < originalProof.SiblingHashes.Length; i++)
        {
            Assert.Equal(originalProof.SiblingHashes[i], deserializedProof.SiblingHashes[i]);
        }

        // Verify orientation bits
        for (int i = 0; i < originalProof.SiblingIsRight.Length; i++)
        {
            Assert.Equal(originalProof.SiblingIsRight[i], deserializedProof.SiblingIsRight[i]);
        }
    }

    [Fact]
    public void Deserialize_RoundTrip_VerificationStillWorks()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3");
        var tree = new MerkleTreeClass(leafData);
        var rootHash = tree.GetRootHash();
        var originalProof = tree.GenerateProof(1);

        // Act
        var serialized = originalProof.Serialize();
        var deserializedProof = MerkleProof.Deserialize(serialized);

        // Assert - Both proofs should verify successfully
        var hashFunction = new Sha256HashFunction();
        Assert.True(originalProof.Verify(rootHash, hashFunction), "Original proof should verify");
        Assert.True(deserializedProof.Verify(rootHash, hashFunction), "Deserialized proof should verify");
    }

    [Fact]
    public void Serialize_IsDeterministic_ProducesSameOutputMultipleTimes()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3");
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(1);

        // Act
        var serialized1 = proof.Serialize();
        var serialized2 = proof.Serialize();
        var serialized3 = proof.Serialize();

        // Assert
        Assert.Equal(serialized1, serialized2);
        Assert.Equal(serialized2, serialized3);
    }

    [Fact]
    public void Serialize_WithDifferentProofs_ProducesDifferentOutput()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3");
        var tree = new MerkleTreeClass(leafData);
        var proof0 = tree.GenerateProof(0);
        var proof1 = tree.GenerateProof(1);
        var proof2 = tree.GenerateProof(2);

        // Act
        var serialized0 = proof0.Serialize();
        var serialized1 = proof1.Serialize();
        var serialized2 = proof2.Serialize();

        // Assert
        Assert.NotEqual(serialized0, serialized1);
        Assert.NotEqual(serialized1, serialized2);
        Assert.NotEqual(serialized0, serialized2);
    }

    [Fact]
    public void Serialize_WithSha256_CorrectHashSize()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2");
        var tree = new MerkleTreeClass(leafData, new Sha256HashFunction());
        var proof = tree.GenerateProof(0);

        // Act
        var serialized = proof.Serialize();

        // Assert - Extract hash size from serialized data
        int hashSizeOffset = 1 + 4 + 8 + 4 + 5; // After version, height, index, leaf value length, and leaf value
        int hashSize = BinaryPrimitives.ReadInt32LittleEndian(serialized.AsSpan(hashSizeOffset));
        Assert.Equal(32, hashSize); // SHA-256 produces 32 bytes
    }

    [Fact]
    public void Serialize_WithSha512_CorrectHashSize()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2");
        var tree = new MerkleTreeClass(leafData, new Sha512HashFunction());
        var proof = tree.GenerateProof(0);

        // Act
        var serialized = proof.Serialize();

        // Assert - Extract hash size from serialized data
        int hashSizeOffset = 1 + 4 + 8 + 4 + 5;
        int hashSize = BinaryPrimitives.ReadInt32LittleEndian(serialized.AsSpan(hashSizeOffset));
        Assert.Equal(64, hashSize); // SHA-512 produces 64 bytes
    }

    [Fact]
    public void Serialize_WithBlake3_CorrectHashSize()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2");
        var tree = new MerkleTreeClass(leafData, new Blake3HashFunction());
        var proof = tree.GenerateProof(0);

        // Act
        var serialized = proof.Serialize();

        // Assert - Extract hash size from serialized data
        int hashSizeOffset = 1 + 4 + 8 + 4 + 5;
        int hashSize = BinaryPrimitives.ReadInt32LittleEndian(serialized.AsSpan(hashSizeOffset));
        Assert.Equal(32, hashSize); // BLAKE3 produces 32 bytes
    }

    [Fact]
    public void Deserialize_WithNullData_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => MerkleProof.Deserialize(null!));
    }

    [Fact]
    public void Deserialize_WithEmptyData_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => MerkleProof.Deserialize(Array.Empty<byte>()));
        Assert.Contains("too short", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_WithInvalidVersion_ThrowsArgumentException()
    {
        // Arrange - Create data with unsupported version
        var data = new byte[] { 99, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => MerkleProof.Deserialize(data));
        Assert.Contains("version", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_WithTruncatedData_ThrowsArgumentException()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2");
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(0);
        var serialized = proof.Serialize();

        // Act - Truncate the data
        var truncated = new byte[serialized.Length - 10];
        Array.Copy(serialized, truncated, truncated.Length);

        // Assert
        var ex = Assert.Throws<ArgumentException>(() => MerkleProof.Deserialize(truncated));
        Assert.Contains("too short", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_WithExtraData_ThrowsArgumentException()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2");
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(0);
        var serialized = proof.Serialize();

        // Act - Add extra bytes
        var withExtra = new byte[serialized.Length + 10];
        serialized.CopyTo(withExtra, 0);

        // Assert
        var ex = Assert.Throws<ArgumentException>(() => MerkleProof.Deserialize(withExtra));
        Assert.Contains("extra bytes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_WithNegativeTreeHeight_ThrowsArgumentException()
    {
        // Arrange - Manually create data with negative tree height
        var data = new byte[30];
        data[0] = 1; // version
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1), -1); // negative tree height

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => MerkleProof.Deserialize(data));
        Assert.Contains("tree height", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("negative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_WithNegativeLeafIndex_ThrowsArgumentException()
    {
        // Arrange - Manually create data with negative leaf index
        var data = new byte[30];
        data[0] = 1; // version
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(1), 0); // tree height = 0
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(5), -1L); // negative leaf index

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => MerkleProof.Deserialize(data));
        Assert.Contains("leaf index", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("negative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialize_WithLargeTree_HandlesCorrectly()
    {
        // Arrange - Create a larger tree with 100 leaves
        var leafData = Enumerable.Range(0, 100)
            .Select(i => Encoding.UTF8.GetBytes($"leaf{i}"))
            .ToList();
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(50);

        // Act
        var serialized = proof.Serialize();
        var deserialized = MerkleProof.Deserialize(serialized);

        // Assert
        Assert.Equal(proof.LeafIndex, deserialized.LeafIndex);
        Assert.Equal(proof.TreeHeight, deserialized.TreeHeight);
        Assert.Equal(proof.LeafValue, deserialized.LeafValue);
        
        // Verify the deserialized proof still works
        var rootHash = tree.GetRootHash();
        var hashFunction = new Sha256HashFunction();
        Assert.True(deserialized.Verify(rootHash, hashFunction));
    }

    [Fact]
    public void Serialize_WithOrientationBits_PacksCorrectly()
    {
        // Arrange - Tree with 4 leaves has well-defined orientation patterns
        var leafData = CreateLeafData("leaf0", "leaf1", "leaf2", "leaf3");
        var tree = new MerkleTreeClass(leafData);
        
        // Act - Generate proofs for all leaves
        var proofs = new List<MerkleProof>();
        for (int i = 0; i < 4; i++)
        {
            proofs.Add(tree.GenerateProof(i));
        }

        // Serialize and deserialize all proofs
        var deserializedProofs = new List<MerkleProof>();
        foreach (var proof in proofs)
        {
            var serialized = proof.Serialize();
            deserializedProofs.Add(MerkleProof.Deserialize(serialized));
        }

        // Assert - Orientation bits should be preserved
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < proofs[i].SiblingIsRight.Length; j++)
            {
                Assert.Equal(proofs[i].SiblingIsRight[j], deserializedProofs[i].SiblingIsRight[j]);
            }
        }
    }

    [Fact]
    public void Serialize_WithNineOrientationBits_UsesMultipleBytes()
    {
        // Arrange - Create a tree that will have 9 levels (257 leaves = 2^8 + 1)
        // This tests that orientation bits spanning more than 8 bits work correctly
        var leafData = Enumerable.Range(0, 257)
            .Select(i => Encoding.UTF8.GetBytes($"leaf{i}"))
            .ToList();
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(128);

        // Act
        var serialized = proof.Serialize();
        var deserialized = MerkleProof.Deserialize(serialized);

        // Assert
        Assert.Equal(proof.TreeHeight, deserialized.TreeHeight);
        Assert.Equal(9, proof.TreeHeight); // Should be 9 levels
        
        // Verify all orientation bits are preserved
        for (int i = 0; i < proof.SiblingIsRight.Length; i++)
        {
            Assert.Equal(proof.SiblingIsRight[i], deserialized.SiblingIsRight[i]);
        }

        // Verify the proof still works
        var rootHash = tree.GetRootHash();
        var hashFunction = new Sha256HashFunction();
        Assert.True(deserialized.Verify(rootHash, hashFunction));
    }

    [Fact]
    public void Serialize_WithVariousLeafValueSizes_HandlesCorrectly()
    {
        // Arrange - Create leaves with different sizes
        var leafData = new List<byte[]>
        {
            Encoding.UTF8.GetBytes("short"),
            Encoding.UTF8.GetBytes("medium length value"),
            Encoding.UTF8.GetBytes("very long value that takes up many more bytes than the other values")
        };
        var tree = new MerkleTreeClass(leafData);

        // Act & Assert - Test each leaf
        for (int i = 0; i < 3; i++)
        {
            var proof = tree.GenerateProof(i);
            var serialized = proof.Serialize();
            var deserialized = MerkleProof.Deserialize(serialized);

            Assert.Equal(proof.LeafValue, deserialized.LeafValue);
            Assert.Equal(leafData[i], deserialized.LeafValue);
        }
    }

    [Fact]
    public void Serialize_Format_IsNotCompressed()
    {
        // This test verifies that serialization uses a straightforward binary format
        // without compression, making it easier to parse and implement in other languages
        
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2");
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(0);

        // Act
        var serialized = proof.Serialize();

        // Assert - Verify the format is readable
        // Version should be 1
        Assert.Equal(1, serialized[0]);
        
        // Tree height should be 1 (little-endian)
        int treeHeight = BinaryPrimitives.ReadInt32LittleEndian(serialized.AsSpan(1));
        Assert.Equal(1, treeHeight);
        
        // Leaf index should be 0 (little-endian)
        long leafIndex = BinaryPrimitives.ReadInt64LittleEndian(serialized.AsSpan(5));
        Assert.Equal(0, leafIndex);
        
        // Leaf value length should be 5 (little-endian)
        int leafValueLength = BinaryPrimitives.ReadInt32LittleEndian(serialized.AsSpan(13));
        Assert.Equal(5, leafValueLength);
    }

    [Fact]
    public void Deserialize_RoundTrip_WithAllHashFunctions_PreservesProof()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3");
        var hashFunctions = new IHashFunction[]
        {
            new Sha256HashFunction(),
            new Sha512HashFunction(),
            new Blake3HashFunction()
        };

        foreach (var hashFunction in hashFunctions)
        {
            var tree = new MerkleTreeClass(leafData, hashFunction);
            var proof = tree.GenerateProof(1);

            // Act
            var serialized = proof.Serialize();
            var deserialized = MerkleProof.Deserialize(serialized);

            // Assert
            var rootHash = tree.GetRootHash();
            Assert.True(deserialized.Verify(rootHash, hashFunction),
                $"Deserialized proof should verify with {hashFunction.GetType().Name}");
        }
    }
}
