using System.Text;
using Xunit;
using MerkleTree.Hashing;
using MerkleTreeClass = MerkleTree.Core.MerkleTree;

namespace MerkleTree.Tests.Hashing;

/// <summary>
/// Tests for the IHashFunction interface.
/// </summary>
public class IHashFunctionTests
{
    /// <summary>
    /// Helper method to create test data.
    /// </summary>
    private static byte[] CreateTestData(string data)
    {
        return Encoding.UTF8.GetBytes(data);
    }

    [Fact]
    public void Interface_ExposesCorrectMetadata()
    {
        // Arrange
        IHashFunction hashFunction = new Sha256HashFunction();

        // Act & Assert
        Assert.NotNull(hashFunction.Name);
        Assert.NotEmpty(hashFunction.Name);
        Assert.True(hashFunction.HashSizeInBytes > 0);
    }

    [Fact]
    public void MerkleTree_WithSha256HashFunction_UsesCorrectHashFunction()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var leafData = new List<byte[]>
        {
            CreateTestData("leaf1"),
            CreateTestData("leaf2")
        };

        // Act
        var tree = new MerkleTreeClass(leafData, hashFunction);

        // Assert
        Assert.NotNull(tree.HashFunction);
        Assert.Equal("SHA-256", tree.HashFunction.Name);
        Assert.Equal(32, tree.HashFunction.HashSizeInBytes);
    }

#if NET10_0_OR_GREATER
    [Fact]
    public void MerkleTree_WithBlake3HashFunction_UsesCorrectHashFunction()
    {
        // Arrange
        var hashFunction = new Blake3HashFunction();
        var leafData = new List<byte[]>
        {
            CreateTestData("leaf1"),
            CreateTestData("leaf2")
        };

        // Act
        var tree = new MerkleTreeClass(leafData, hashFunction);

        // Assert
        Assert.NotNull(tree.HashFunction);
        Assert.Equal("BLAKE3", tree.HashFunction.Name);
        Assert.Equal(32, tree.HashFunction.HashSizeInBytes);
    }

    [Fact]
    public void MerkleTree_WithDifferentHashFunctions_ProducesDifferentRootHashes()
    {
        // Arrange
        var sha256 = new Sha256HashFunction();
        var blake3 = new Blake3HashFunction();
        var leafData = new List<byte[]>
        {
            CreateTestData("leaf1"),
            CreateTestData("leaf2"),
            CreateTestData("leaf3")
        };

        // Act
        var treeSha256 = new MerkleTreeClass(leafData, sha256);
        var treeBlake3 = new MerkleTreeClass(leafData, blake3);

        // Assert
        Assert.NotEqual(treeSha256.GetRootHash(), treeBlake3.GetRootHash());
    }
#endif

    [Fact]
    public void MerkleTree_DefaultConstructor_UsesSha256()
    {
        // Arrange
        var leafData = new List<byte[]>
        {
            CreateTestData("leaf1"),
            CreateTestData("leaf2")
        };

        // Act
        var tree = new MerkleTreeClass(leafData);

        // Assert
        Assert.NotNull(tree.HashFunction);
        Assert.Equal("SHA-256", tree.HashFunction.Name);
        Assert.Equal(32, tree.HashFunction.HashSizeInBytes);
    }

    [Fact]
    public void MerkleTree_CanSwapHashFunctionWithoutChangingCoreLogic()
    {
        // This test validates that the abstraction allows swapping hash functions
        // without requiring changes to the MerkleTree core logic

        // Arrange
        var sha256 = new Sha256HashFunction();
        var leafData = new List<byte[]>
        {
            CreateTestData("leaf1"),
            CreateTestData("leaf2"),
            CreateTestData("leaf3")
        };

        // Act - Create trees with different hash functions
        var tree1 = new MerkleTreeClass(leafData, sha256);
        var tree2 = new MerkleTreeClass(leafData, sha256);

        // Assert - Same hash function produces same results
        Assert.Equal(tree1.GetRootHash(), tree2.GetRootHash());
        Assert.Equal(tree1.HashFunction.Name, tree2.HashFunction.Name);
    }
}
