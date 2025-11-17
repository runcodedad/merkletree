using System.Text;
using Xunit;
using MerkleTree.Core;
using MerkleTree.Hashing;
using MerkleTreeClass = MerkleTree.Core.MerkleTree;

namespace MerkleTree.Tests.Core;

/// <summary>
/// Tests for root serialization and deserialization functionality.
/// </summary>
public class RootSerializationTests
{
    /// <summary>
    /// Helper method to create leaf data from strings.
    /// </summary>
    private static List<byte[]> CreateLeafData(params string[] data)
    {
        return data.Select(s => Encoding.UTF8.GetBytes(s)).ToList();
    }

    [Fact]
    public void Serialize_WithValidNode_ReturnsHashBytes()
    {
        // Arrange
        var leafData = CreateLeafData("test1", "test2", "test3");
        var tree = new MerkleTreeClass(leafData);
        var root = tree.Root;

        // Act
        var serialized = root.Serialize();

        // Assert
        Assert.NotNull(serialized);
        Assert.Equal(32, serialized.Length); // SHA-256 produces 32 bytes
        Assert.Equal(tree.GetRootHash(), serialized);
    }

    [Fact]
    public void Serialize_ProducesDeterministicOutput()
    {
        // Arrange
        var leafData = CreateLeafData("data1", "data2", "data3");
        var tree1 = new MerkleTreeClass(leafData);
        var tree2 = new MerkleTreeClass(leafData);

        // Act
        var serialized1 = tree1.Root.Serialize();
        var serialized2 = tree2.Root.Serialize();

        // Assert
        Assert.Equal(serialized1, serialized2);
    }

    [Fact]
    public void Serialize_WithNullHash_ThrowsInvalidOperationException()
    {
        // Arrange
        var node = new MerkleTreeNode();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => node.Serialize());
    }

    [Fact]
    public void Deserialize_WithValidData_ReturnsNode()
    {
        // Arrange
        var leafData = CreateLeafData("test1", "test2");
        var tree = new MerkleTreeClass(leafData);
        var originalHash = tree.GetRootHash();

        // Act
        var deserialized = MerkleTreeNode.Deserialize(originalHash);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Hash);
        Assert.Equal(originalHash, deserialized.Hash);
    }

    [Fact]
    public void Deserialize_WithNullData_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => MerkleTreeNode.Deserialize(null!));
    }

    [Fact]
    public void Deserialize_WithEmptyData_ThrowsArgumentException()
    {
        // Arrange
        var emptyData = Array.Empty<byte>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => MerkleTreeNode.Deserialize(emptyData));
    }

    [Fact]
    public void RoundTrip_SerializeAndDeserialize_PreservesHash()
    {
        // Arrange
        var leafData = CreateLeafData("data1", "data2", "data3", "data4");
        var tree = new MerkleTreeClass(leafData);
        var originalRoot = tree.Root;

        // Act
        var serialized = originalRoot.Serialize();
        var deserialized = MerkleTreeNode.Deserialize(serialized);

        // Assert
        Assert.Equal(originalRoot.Hash, deserialized.Hash);
    }

    [Fact]
    public void Serialize_WithDifferentHashFunctions_ProducesDifferentSizes()
    {
        // Arrange
        var leafData = CreateLeafData("test1", "test2");
        var treeSha256 = new MerkleTreeClass(leafData, new Sha256HashFunction());
        var treeSha512 = new MerkleTreeClass(leafData, new Sha512HashFunction());
        var treeBlake3 = new MerkleTreeClass(leafData, new Blake3HashFunction());

        // Act
        var serializedSha256 = treeSha256.Root.Serialize();
        var serializedSha512 = treeSha512.Root.Serialize();
        var serializedBlake3 = treeBlake3.Root.Serialize();

        // Assert - SHA-256 produces 32 bytes
        Assert.Equal(32, serializedSha256.Length);

        // Assert - SHA-512 produces 64 bytes
        Assert.Equal(64, serializedSha512.Length);

        // Assert - BLAKE3 produces 32 bytes
        Assert.Equal(32, serializedBlake3.Length);

        // Assert - SHA-256 and SHA-512 produce different hashes
        Assert.NotEqual(serializedSha256.Length, serializedSha512.Length);
    }

    [Fact]
    public void RoundTrip_WithSha512_PreservesHash()
    {
        // Arrange
        var leafData = CreateLeafData("data1", "data2");
        var tree = new MerkleTreeClass(leafData, new Sha512HashFunction());
        var originalRoot = tree.Root;

        // Act
        var serialized = originalRoot.Serialize();
        var deserialized = MerkleTreeNode.Deserialize(serialized);

        // Assert
        Assert.Equal(64, serialized.Length);
        Assert.Equal(originalRoot.Hash, deserialized.Hash);
    }

    [Fact]
    public void RoundTrip_WithBlake3_PreservesHash()
    {
        // Arrange
        var leafData = CreateLeafData("data1", "data2", "data3");
        var tree = new MerkleTreeClass(leafData, new Blake3HashFunction());
        var originalRoot = tree.Root;

        // Act
        var serialized = originalRoot.Serialize();
        var deserialized = MerkleTreeNode.Deserialize(serialized);

        // Assert
        Assert.Equal(32, serialized.Length);
        Assert.Equal(originalRoot.Hash, deserialized.Hash);
    }

    [Fact]
    public void Serialize_MultipleTimes_ProducesSameOutput()
    {
        // Arrange
        var leafData = CreateLeafData("test1", "test2", "test3");
        var tree = new MerkleTreeClass(leafData);
        var root = tree.Root;

        // Act
        var serialized1 = root.Serialize();
        var serialized2 = root.Serialize();
        var serialized3 = root.Serialize();

        // Assert
        Assert.Equal(serialized1, serialized2);
        Assert.Equal(serialized2, serialized3);
    }

    [Fact]
    public void Deserialize_CreatesIndependentCopy()
    {
        // Arrange
        var leafData = CreateLeafData("data1");
        var tree = new MerkleTreeClass(leafData);
        var originalHash = tree.GetRootHash();

        // Act
        var deserialized = MerkleTreeNode.Deserialize(originalHash);

        // Modify the deserialized node's hash
        deserialized.Hash![0] = (byte)(deserialized.Hash[0] ^ 0xFF);

        // Assert - Original hash should be unchanged
        Assert.NotEqual(originalHash, deserialized.Hash);
    }

    [Fact]
    public void MetadataSerializeRoot_ReturnsRootHash()
    {
        // Arrange
        var leafData = CreateLeafData("test1", "test2");
        var tree = new MerkleTreeClass(leafData);
        var metadata = tree.GetMetadata();

        // Act
        var serialized = metadata.SerializeRoot();

        // Assert
        Assert.NotNull(serialized);
        Assert.Equal(32, serialized.Length);
        Assert.Equal(tree.GetRootHash(), serialized);
    }

    [Fact]
    public void MetadataDeserializeRoot_ReturnsValidNode()
    {
        // Arrange
        var leafData = CreateLeafData("test1", "test2", "test3");
        var tree = new MerkleTreeClass(leafData);
        var metadata = tree.GetMetadata();
        var serialized = metadata.SerializeRoot();

        // Act
        var deserialized = MerkleTreeMetadata.DeserializeRoot(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(tree.GetRootHash(), deserialized.Hash);
    }

    [Fact]
    public void MetadataRoundTrip_PreservesRootHash()
    {
        // Arrange
        var leafData = CreateLeafData("data1", "data2", "data3", "data4", "data5");
        var tree = new MerkleTreeClass(leafData);
        var metadata = tree.GetMetadata();

        // Act
        var serialized = metadata.SerializeRoot();
        var deserialized = MerkleTreeMetadata.DeserializeRoot(serialized);

        // Assert
        Assert.Equal(metadata.RootHash, deserialized.Hash);
    }

    [Fact]
    public void Serialize_WithSingleLeaf_WorksCorrectly()
    {
        // Arrange
        var leafData = CreateLeafData("single");
        var tree = new MerkleTreeClass(leafData);

        // Act
        var serialized = tree.Root.Serialize();
        var deserialized = MerkleTreeNode.Deserialize(serialized);

        // Assert
        Assert.Equal(32, serialized.Length);
        Assert.Equal(tree.GetRootHash(), deserialized.Hash);
    }

    [Fact]
    public void Serialize_WithOddNumberOfLeaves_WorksCorrectly()
    {
        // Arrange
        var leafData = CreateLeafData("a", "b", "c", "d", "e", "f", "g");
        var tree = new MerkleTreeClass(leafData);

        // Act
        var serialized = tree.Root.Serialize();
        var deserialized = MerkleTreeNode.Deserialize(serialized);

        // Assert
        Assert.Equal(32, serialized.Length);
        Assert.Equal(tree.GetRootHash(), deserialized.Hash);
    }

    [Fact]
    public void Serialize_WithPowerOfTwoLeaves_WorksCorrectly()
    {
        // Arrange
        var leafData = CreateLeafData("1", "2", "3", "4", "5", "6", "7", "8");
        var tree = new MerkleTreeClass(leafData);

        // Act
        var serialized = tree.Root.Serialize();
        var deserialized = MerkleTreeNode.Deserialize(serialized);

        // Assert
        Assert.Equal(32, serialized.Length);
        Assert.Equal(tree.GetRootHash(), deserialized.Hash);
    }
}
