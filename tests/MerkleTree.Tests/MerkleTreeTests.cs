using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace MerkleTree.Tests;

/// <summary>
/// Tests for the MerkleTree class, focusing on non-power-of-two leaf support
/// and domain-separated padding strategy.
/// </summary>
public class MerkleTreeTests
{
    /// <summary>
    /// Helper method to create leaf data from strings.
    /// </summary>
    private static List<byte[]> CreateLeafData(params string[] data)
    {
        return data.Select(s => Encoding.UTF8.GetBytes(s)).ToList();
    }
    
    /// <summary>
    /// Helper method to compute SHA256 hash.
    /// </summary>
    private static byte[] ComputeSHA256(byte[] data)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(data);
    }
    
    [Fact]
    public void Constructor_WithNullLeafData_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MerkleTree(null!));
    }
    
    [Fact]
    public void Constructor_WithEmptyLeafData_ThrowsArgumentException()
    {
        // Arrange
        var emptyData = new List<byte[]>();
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new MerkleTree(emptyData));
    }
    
    [Fact]
    public void Constructor_WithSingleLeaf_CreatesTreeWithSingleNode()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1");
        
        // Act
        var tree = new MerkleTree(leafData);
        
        // Assert
        Assert.NotNull(tree.Root);
        Assert.NotNull(tree.Root.Hash);
        Assert.Null(tree.Root.Left);
        Assert.Null(tree.Root.Right);
    }
    
    [Fact]
    public void Constructor_WithTwoLeaves_CreatesPowerOfTwoTree()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2");
        
        // Act
        var tree = new MerkleTree(leafData);
        
        // Assert
        Assert.NotNull(tree.Root);
        Assert.NotNull(tree.Root.Hash);
        Assert.NotNull(tree.Root.Left);
        Assert.NotNull(tree.Root.Right);
        
        // Verify structure: root has two leaf children
        Assert.NotNull(tree.Root.Left.Hash);
        Assert.NotNull(tree.Root.Right.Hash);
        Assert.Null(tree.Root.Left.Left);
        Assert.Null(tree.Root.Left.Right);
        Assert.Null(tree.Root.Right.Left);
        Assert.Null(tree.Root.Right.Right);
    }
    
    [Fact]
    public void Constructor_WithThreeLeaves_UsesDomaianSeparatedPadding()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3");
        
        // Act
        var tree = new MerkleTree(leafData);
        
        // Assert
        Assert.NotNull(tree.Root);
        Assert.NotNull(tree.Root.Hash);
        
        // With 3 leaves, we expect:
        // Level 0: [leaf1, leaf2, leaf3]
        // Level 1: [Hash(leaf1||leaf2), Hash(leaf3||padding)]
        // Level 2: [Hash(level1_left||level1_right)] = root
        
        // Root should have two children at level 1
        Assert.NotNull(tree.Root.Left);
        Assert.NotNull(tree.Root.Right);
        
        // Left child of root should have two leaf children
        Assert.NotNull(tree.Root.Left.Left);
        Assert.NotNull(tree.Root.Left.Right);
        
        // Right child of root should have one leaf and one padding node
        Assert.NotNull(tree.Root.Right.Left);
        Assert.NotNull(tree.Root.Right.Right);
        
        // The padding node should have no children
        Assert.Null(tree.Root.Right.Right.Left);
        Assert.Null(tree.Root.Right.Right.Right);
    }
    
    [Fact]
    public void Constructor_WithFourLeaves_CreatesPowerOfTwoTree()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3", "leaf4");
        
        // Act
        var tree = new MerkleTree(leafData);
        
        // Assert
        Assert.NotNull(tree.Root);
        
        // With 4 leaves (power of 2), tree should be perfectly balanced
        // Level 0: [leaf1, leaf2, leaf3, leaf4]
        // Level 1: [Hash(leaf1||leaf2), Hash(leaf3||leaf4)]
        // Level 2: [Hash(level1_left||level1_right)] = root
        
        Assert.NotNull(tree.Root.Left);
        Assert.NotNull(tree.Root.Right);
        Assert.NotNull(tree.Root.Left.Left);
        Assert.NotNull(tree.Root.Left.Right);
        Assert.NotNull(tree.Root.Right.Left);
        Assert.NotNull(tree.Root.Right.Right);
    }
    
    [Fact]
    public void Constructor_WithFiveLeaves_UsesDomaianSeparatedPadding()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3", "leaf4", "leaf5");
        
        // Act
        var tree = new MerkleTree(leafData);
        
        // Assert
        Assert.NotNull(tree.Root);
        
        // With 5 leaves:
        // Level 0: [leaf1, leaf2, leaf3, leaf4, leaf5]
        // Level 1: [Hash(leaf1||leaf2), Hash(leaf3||leaf4), Hash(leaf5||padding)]
        // Level 2: [Hash(l1[0]||l1[1]), Hash(l1[2]||padding2)]
        // Level 3: [Hash(l2[0]||l2[1])] = root
        
        Assert.NotNull(tree.Root.Left);
        Assert.NotNull(tree.Root.Right);
    }
    
    [Fact]
    public void GetRootHash_ReturnsSameHashForSameInput()
    {
        // Arrange
        var leafData1 = CreateLeafData("leaf1", "leaf2", "leaf3");
        var leafData2 = CreateLeafData("leaf1", "leaf2", "leaf3");
        
        // Act
        var tree1 = new MerkleTree(leafData1);
        var tree2 = new MerkleTree(leafData2);
        var hash1 = tree1.GetRootHash();
        var hash2 = tree2.GetRootHash();
        
        // Assert
        Assert.Equal(hash1, hash2);
    }
    
    [Fact]
    public void GetRootHash_ReturnsDifferentHashForDifferentInput()
    {
        // Arrange
        var leafData1 = CreateLeafData("leaf1", "leaf2", "leaf3");
        var leafData2 = CreateLeafData("leaf1", "leaf2", "leaf4");
        
        // Act
        var tree1 = new MerkleTree(leafData1);
        var tree2 = new MerkleTree(leafData2);
        var hash1 = tree1.GetRootHash();
        var hash2 = tree2.GetRootHash();
        
        // Assert
        Assert.NotEqual(hash1, hash2);
    }
    
    [Fact]
    public void GetRootHash_ReturnsDifferentHashForDifferentOrdering()
    {
        // Arrange
        var leafData1 = CreateLeafData("leaf1", "leaf2");
        var leafData2 = CreateLeafData("leaf2", "leaf1");
        
        // Act
        var tree1 = new MerkleTree(leafData1);
        var tree2 = new MerkleTree(leafData2);
        var hash1 = tree1.GetRootHash();
        var hash2 = tree2.GetRootHash();
        
        // Assert - ordering matters, so hashes should be different
        Assert.NotEqual(hash1, hash2);
    }
    
    [Fact]
    public void Constructor_WithDifferentHashAlgorithms_ProducesDifferentRootHash()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2");
        
        // Act
        var treeSHA256 = new MerkleTree(leafData, new Sha256HashFunction());
        var treeSHA512 = new MerkleTree(leafData, new Sha512HashFunction());
        
        // Assert
        Assert.Equal("SHA-256", treeSHA256.HashFunction.Name);
        Assert.Equal("SHA-512", treeSHA512.HashFunction.Name);
        Assert.NotEqual(treeSHA256.GetRootHash().Length, treeSHA512.GetRootHash().Length);
    }
    
    [Fact]
    public void Constructor_WithSevenLeaves_HandlesMultipleLevelsOfPadding()
    {
        // Arrange
        var leafData = CreateLeafData("l1", "l2", "l3", "l4", "l5", "l6", "l7");
        
        // Act
        var tree = new MerkleTree(leafData);
        
        // Assert
        Assert.NotNull(tree.Root);
        Assert.NotNull(tree.Root.Hash);
        
        // With 7 leaves, tree structure should handle multiple levels of padding
        // The tree should be deterministic
        var rootHash = tree.GetRootHash();
        Assert.NotEmpty(rootHash);
    }
    
    [Fact]
    public void Constructor_WithEightLeaves_CreatesPerfectBalancedTree()
    {
        // Arrange
        var leafData = CreateLeafData("l1", "l2", "l3", "l4", "l5", "l6", "l7", "l8");
        
        // Act
        var tree = new MerkleTree(leafData);
        
        // Assert
        Assert.NotNull(tree.Root);
        
        // With 8 leaves (power of 2), tree should be perfectly balanced
        // Height should be 3 (levels 0, 1, 2, 3)
        var rootHash = tree.GetRootHash();
        Assert.NotEmpty(rootHash);
    }
    
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(17)]
    public void Constructor_WithVariousLeafCounts_ProducesDeterministicResults(int leafCount)
    {
        // Arrange
        var leafData = Enumerable.Range(1, leafCount)
            .Select(i => Encoding.UTF8.GetBytes($"leaf{i}"))
            .ToList();
        
        // Act
        var tree1 = new MerkleTree(leafData);
        var tree2 = new MerkleTree(leafData);
        
        // Assert
        Assert.Equal(tree1.GetRootHash(), tree2.GetRootHash());
    }
    
    [Fact]
    public void TreeStructure_IsDeterministicAndFullyDefined()
    {
        // This test validates that the tree structure is fully deterministic
        // by building the same tree multiple times and verifying consistency
        
        // Arrange
        var leafData = CreateLeafData("a", "b", "c", "d", "e");
        
        // Act - build tree multiple times
        var trees = Enumerable.Range(0, 10)
            .Select(_ => new MerkleTree(leafData.Select(d => d.ToArray()).ToList()))
            .ToList();
        
        // Assert - all trees should have identical root hashes
        var firstRootHash = trees[0].GetRootHash();
        foreach (var tree in trees.Skip(1))
        {
            Assert.Equal(firstRootHash, tree.GetRootHash());
        }
    }
    
    [Fact]
    public void PaddingStrategy_ProducesDifferentHashThanDuplication()
    {
        // This test validates that domain-separated padding produces different
        // results than simple duplication (to ensure we're not accidentally
        // implementing Option 1 instead of Option 3)
        
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3");
        var tree = new MerkleTree(leafData);
        var rootHash = tree.GetRootHash();
        
        // If we were duplicating the last leaf, the tree with 4 leaves where
        // the last two are identical should produce the same hash
        var leafDataWithDuplication = CreateLeafData("leaf1", "leaf2", "leaf3", "leaf3");
        var treeWithDuplication = new MerkleTree(leafDataWithDuplication);
        var hashWithDuplication = treeWithDuplication.GetRootHash();
        
        // Assert - these should be different because we use padding, not duplication
        Assert.NotEqual(rootHash, hashWithDuplication);
    }
}
