using System.Text;
using Xunit;
using MerkleTree.Hashing;
using MerkleTreeClass = MerkleTree.Core.MerkleTree;

namespace MerkleTree.Tests.Hashing;

/// <summary>
/// Tests for domain separation in hash primitives to prevent collision attacks.
/// </summary>
/// <remarks>
/// These tests verify that leaf hashes, internal node hashes, and padding hashes
/// are properly domain-separated to prevent collision attacks where an attacker
/// could construct data that produces the same hash as an internal node.
/// </remarks>
public class DomainSeparationTests
{
    /// <summary>
    /// Helper method to create test data.
    /// </summary>
    private static byte[] CreateTestData(string data)
    {
        return Encoding.UTF8.GetBytes(data);
    }

    [Fact]
    public void LeafHash_CannotCollideWithInternalNodeHash()
    {
        // This test verifies that no leaf data can produce the same hash as an internal node
        // by ensuring domain separation (leaf uses 0x00 prefix, internal nodes use 0x01 prefix)

        // Arrange
        var hashFunction = new Sha256HashFunction();
        
        // Create a simple tree with two leaves
        var leaf1Data = CreateTestData("leaf1");
        var leaf2Data = CreateTestData("leaf2");
        var leafData = new List<byte[]> { leaf1Data, leaf2Data };
        var tree = new MerkleTreeClass(leafData, hashFunction);
        
        // Get the root hash (which is an internal node hash)
        var rootHash = tree.GetRootHash();
        
        // Now try to create a single-leaf tree where the leaf data is constructed
        // to mimic what would produce an internal node hash
        // If domain separation is working, this should NOT produce the same hash
        
        // Compute what the internal node would hash (without domain separation)
        var leaf1Hash = hashFunction.ComputeHash(new byte[] { 0x00 }.Concat(leaf1Data).ToArray());
        var leaf2Hash = hashFunction.ComputeHash(new byte[] { 0x00 }.Concat(leaf2Data).ToArray());
        var internalNodeData = new byte[] { 0x01 }.Concat(leaf1Hash).Concat(leaf2Hash).ToArray();
        
        // Try to create a leaf with this data
        var attackLeafData = new List<byte[]> { internalNodeData };
        var attackTree = new MerkleTreeClass(attackLeafData, hashFunction);
        var attackHash = attackTree.GetRootHash();
        
        // Assert - The hashes should be different because of domain separation
        Assert.NotEqual(rootHash, attackHash);
    }

    [Fact]
    public void LeafHash_CannotCollideWithPaddingHash()
    {
        // This test verifies that leaf hashes cannot collide with padding hashes
        
        // Arrange
        var hashFunction = new Sha256HashFunction();
        
        // Create a tree with 3 leaves (which will have padding)
        var leaf1Data = CreateTestData("leaf1");
        var leaf2Data = CreateTestData("leaf2");
        var leaf3Data = CreateTestData("leaf3");
        var leafData = new List<byte[]> { leaf1Data, leaf2Data, leaf3Data };
        var tree = new MerkleTreeClass(leafData, hashFunction);
        
        var rootHash = tree.GetRootHash();
        
        // Try to create a tree where a leaf contains data that mimics a padding hash
        // Padding hash is computed as Hash("MERKLE_PADDING" || unpaired_hash)
        var leaf3Hash = hashFunction.ComputeHash(new byte[] { 0x00 }.Concat(leaf3Data).ToArray());
        var paddingPrefix = Encoding.UTF8.GetBytes("MERKLE_PADDING");
        var paddingLikeData = paddingPrefix.Concat(leaf3Hash).ToArray();
        
        var attackData = new List<byte[]> { leaf1Data, leaf2Data, paddingLikeData };
        var attackTree = new MerkleTreeClass(attackData, hashFunction);
        var attackHash = attackTree.GetRootHash();
        
        // Assert - The hashes should be different
        Assert.NotEqual(rootHash, attackHash);
    }

    [Fact]
    public void InternalNodeHash_CannotCollideWithPaddingHash()
    {
        // This test verifies that internal node hashes cannot collide with padding hashes
        
        // Arrange
        var hashFunction = new Sha256HashFunction();
        
        // Create a tree with 3 leaves (has padding)
        var leafData1 = new List<byte[]> 
        { 
            CreateTestData("a"), 
            CreateTestData("b"), 
            CreateTestData("c") 
        };
        var tree1 = new MerkleTreeClass(leafData1, hashFunction);
        var hash1 = tree1.GetRootHash();
        
        // Create a tree with 4 leaves (no padding, but has an internal node in same position)
        var leafData2 = new List<byte[]> 
        { 
            CreateTestData("a"), 
            CreateTestData("b"), 
            CreateTestData("c"),
            CreateTestData("c")  // Duplicate to create 4 leaves
        };
        var tree2 = new MerkleTreeClass(leafData2, hashFunction);
        var hash2 = tree2.GetRootHash();
        
        // Assert - Different tree structures should produce different hashes
        // This indirectly tests that padding and internal nodes use different domain separators
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void DomainSeparation_ConsistentAcrossHashFunctions()
    {
        // Verify domain separation works consistently across different hash functions
        
        // Arrange
        var leaf1 = CreateTestData("leaf1");
        var leaf2 = CreateTestData("leaf2");
        var leafData = new List<byte[]> { leaf1, leaf2 };
        
        // Act - Build trees with different hash functions
        var treeSha256 = new MerkleTreeClass(leafData, new Sha256HashFunction());
        var treeSha512 = new MerkleTreeClass(leafData, new Sha512HashFunction());
        
        // Assert - Both should be valid trees (no exceptions thrown)
        Assert.NotNull(treeSha256.GetRootHash());
        Assert.NotNull(treeSha512.GetRootHash());
        
        // The hashes will be different (different hash functions), but both should be valid
        Assert.Equal(32, treeSha256.GetRootHash().Length); // SHA-256 produces 32 bytes
        Assert.Equal(64, treeSha512.GetRootHash().Length); // SHA-512 produces 64 bytes
    }

    [Fact]
    public void DomainSeparation_DifferentDataProducesDifferentHashes()
    {
        // Verify that domain separation preserves the collision resistance property
        
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var leafData1 = new List<byte[]> { CreateTestData("data1"), CreateTestData("data2") };
        var leafData2 = new List<byte[]> { CreateTestData("data1"), CreateTestData("data3") };
        
        // Act
        var tree1 = new MerkleTreeClass(leafData1, hashFunction);
        var tree2 = new MerkleTreeClass(leafData2, hashFunction);
        
        // Assert - Different input should produce different root hashes
        Assert.NotEqual(tree1.GetRootHash(), tree2.GetRootHash());
    }

    [Fact]
    public void DomainSeparation_SameDataProducesSameHash()
    {
        // Verify that domain separation maintains determinism
        
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var leafData = new List<byte[]> 
        { 
            CreateTestData("leaf1"), 
            CreateTestData("leaf2"),
            CreateTestData("leaf3") 
        };
        
        // Act
        var tree1 = new MerkleTreeClass(leafData, hashFunction);
        var tree2 = new MerkleTreeClass(leafData, hashFunction);
        
        // Assert - Same input should produce same root hash
        Assert.Equal(tree1.GetRootHash(), tree2.GetRootHash());
    }

    [Fact]
    public void LeafHash_WithEmptyData_ProducesDifferentHashThanInternalNode()
    {
        // Edge case: test with empty leaf data
        
        // Arrange
        var hashFunction = new Sha256HashFunction();
        
        // Tree with one empty leaf
        var singleLeaf = new List<byte[]> { Array.Empty<byte>() };
        var tree1 = new MerkleTreeClass(singleLeaf, hashFunction);
        var hash1 = tree1.GetRootHash();
        
        // Tree with two empty leaves (creates an internal node)
        var twoLeaves = new List<byte[]> { Array.Empty<byte>(), Array.Empty<byte>() };
        var tree2 = new MerkleTreeClass(twoLeaves, hashFunction);
        var hash2 = tree2.GetRootHash();
        
        // Assert - Should be different due to domain separation
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void PaddingHash_IsDeterministic()
    {
        // Verify that padding hashes are deterministic
        
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var leafData = new List<byte[]> 
        { 
            CreateTestData("a"), 
            CreateTestData("b"), 
            CreateTestData("c") 
        };
        
        // Act - Build the same tree twice
        var tree1 = new MerkleTreeClass(leafData, hashFunction);
        var tree2 = new MerkleTreeClass(leafData, hashFunction);
        
        // Assert - Should produce identical results
        Assert.Equal(tree1.GetRootHash(), tree2.GetRootHash());
    }

    [Fact]
    public void LeafHash_CannotForgeInternalNodeStructure()
    {
        // Advanced attack test: try to forge an internal node structure by crafting leaf data
        
        // Arrange
        var hashFunction = new Sha256HashFunction();
        
        // Create a legitimate 2-level tree
        var legitLeaves = new List<byte[]>
        {
            CreateTestData("A"),
            CreateTestData("B"),
            CreateTestData("C"),
            CreateTestData("D")
        };
        var legitTree = new MerkleTreeClass(legitLeaves, hashFunction);
        var legitRoot = legitTree.GetRootHash();
        
        // Now try to create a forged tree that attempts to produce the same root
        // by crafting a leaf that contains pre-hashed data
        var leafAHash = hashFunction.ComputeHash(new byte[] { 0x00 }.Concat(CreateTestData("A")).ToArray());
        var leafBHash = hashFunction.ComputeHash(new byte[] { 0x00 }.Concat(CreateTestData("B")).ToArray());
        var leafCHash = hashFunction.ComputeHash(new byte[] { 0x00 }.Concat(CreateTestData("C")).ToArray());
        var leafDHash = hashFunction.ComputeHash(new byte[] { 0x00 }.Concat(CreateTestData("D")).ToArray());
        
        // Compute internal hashes
        var leftInternal = hashFunction.ComputeHash(new byte[] { 0x01 }.Concat(leafAHash).Concat(leafBHash).ToArray());
        var rightInternal = hashFunction.ComputeHash(new byte[] { 0x01 }.Concat(leafCHash).Concat(leafDHash).ToArray());
        
        // Try to forge by creating leaves that contain these internal hashes
        var forgedLeaves = new List<byte[]>
        {
            leftInternal,
            rightInternal
        };
        var forgedTree = new MerkleTreeClass(forgedLeaves, hashFunction);
        var forgedRoot = forgedTree.GetRootHash();
        
        // Assert - The forged tree should NOT produce the same root due to domain separation
        Assert.NotEqual(legitRoot, forgedRoot);
    }
}
