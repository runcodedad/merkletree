using System.Text;
using Xunit;
using InMemoryMerkleTree = MerkleTree.MerkleTree;

namespace MerkleTree.Tests;

/// <summary>
/// Tests for the MerkleTreeBuilder class, focusing on streaming/chunked input support.
/// </summary>
public class MerkleTreeStreamTests
{
    /// <summary>
    /// Helper method to create leaf data from strings.
    /// </summary>
    private static List<byte[]> CreateLeafData(params string[] data)
    {
        return data.Select(s => Encoding.UTF8.GetBytes(s)).ToList();
    }

    /// <summary>
    /// Helper method to create an async enumerable from leaf data.
    /// </summary>
    private static async IAsyncEnumerable<byte[]> CreateAsyncLeafData(params string[] data)
    {
        foreach (var item in data)
        {
            await Task.Yield(); // Simulate async operation
            yield return Encoding.UTF8.GetBytes(item);
        }
    }

    [Fact]
    public void Constructor_DefaultHashFunction_UsesSha256()
    {
        // Act
        var builder = new MerkleTreeStream();

        // Assert
        Assert.NotNull(builder.HashFunction);
        Assert.Equal("SHA-256", builder.HashFunction.Name);
    }

    [Fact]
    public void Constructor_CustomHashFunction_UsesProvidedFunction()
    {
        // Arrange
        var hashFunction = new Sha512HashFunction();

        // Act
        var builder = new MerkleTreeStream(hashFunction);

        // Assert
        Assert.Same(hashFunction, builder.HashFunction);
    }

    [Fact]
    public void Constructor_WithNullHashFunction_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MerkleTreeStream(null!));
    }

    [Fact]
    public void Build_WithNullLeafData_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new MerkleTreeStream();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.Build(null!));
    }

    [Fact]
    public void Build_WithEmptyLeafData_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var emptyData = new List<byte[]>();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build(emptyData));
    }

    [Fact]
    public void Build_WithSingleLeaf_ReturnsCorrectMetadata()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateLeafData("leaf1");

        // Act
        var metadata = builder.Build(leafData);

        // Assert
        Assert.NotNull(metadata);
        Assert.NotNull(metadata.RootHash);
        Assert.Equal(0, metadata.Height);
        Assert.Equal(1, metadata.LeafCount);
    }

    [Fact]
    public void Build_WithTwoLeaves_ReturnsCorrectMetadata()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateLeafData("leaf1", "leaf2");

        // Act
        var metadata = builder.Build(leafData);

        // Assert
        Assert.NotNull(metadata);
        Assert.NotNull(metadata.RootHash);
        Assert.Equal(1, metadata.Height);
        Assert.Equal(2, metadata.LeafCount);
    }

    [Fact]
    public void Build_WithThreeLeaves_ReturnsCorrectMetadata()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3");

        // Act
        var metadata = builder.Build(leafData);

        // Assert
        Assert.NotNull(metadata);
        Assert.NotNull(metadata.RootHash);
        Assert.Equal(2, metadata.Height);
        Assert.Equal(3, metadata.LeafCount);
    }

    [Fact]
    public void Build_WithFourLeaves_ReturnsCorrectMetadata()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3", "leaf4");

        // Act
        var metadata = builder.Build(leafData);

        // Assert
        Assert.NotNull(metadata);
        Assert.NotNull(metadata.RootHash);
        Assert.Equal(2, metadata.Height);
        Assert.Equal(4, metadata.LeafCount);
    }

    [Fact]
    public void Build_WithEightLeaves_ReturnsCorrectMetadata()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateLeafData("l1", "l2", "l3", "l4", "l5", "l6", "l7", "l8");

        // Act
        var metadata = builder.Build(leafData);

        // Assert
        Assert.NotNull(metadata);
        Assert.NotNull(metadata.RootHash);
        Assert.Equal(3, metadata.Height);
        Assert.Equal(8, metadata.LeafCount);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(7, 3)]
    [InlineData(8, 3)]
    [InlineData(15, 4)]
    [InlineData(16, 4)]
    [InlineData(17, 5)]
    public void Build_WithVariousLeafCounts_ReturnsCorrectHeight(int leafCount, int expectedHeight)
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = Enumerable.Range(1, leafCount)
            .Select(i => Encoding.UTF8.GetBytes($"leaf{i}"))
            .ToList();

        // Act
        var metadata = builder.Build(leafData);

        // Assert
        Assert.Equal(expectedHeight, metadata.Height);
        Assert.Equal(leafCount, metadata.LeafCount);
    }

    [Fact]
    public void Build_ProducesDeterministicResults()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3");

        // Act
        var metadata1 = builder.Build(leafData);
        var metadata2 = builder.Build(leafData);

        // Assert
        Assert.Equal(metadata1.RootHash, metadata2.RootHash);
        Assert.Equal(metadata1.Height, metadata2.Height);
        Assert.Equal(metadata1.LeafCount, metadata2.LeafCount);
    }

    [Fact]
    public void Build_MatchesOriginalMerkleTreeRootHash()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3");

        // Act
        var builderMetadata = builder.Build(leafData);
        var originalTree = new MerkleTree(leafData);
        var originalRootHash = originalTree.GetRootHash();

        // Assert
        Assert.Equal(originalRootHash, builderMetadata.RootHash);
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
    public void Build_WithVariousLeafCounts_MatchesOriginalMerkleTree(int leafCount)
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = Enumerable.Range(1, leafCount)
            .Select(i => Encoding.UTF8.GetBytes($"leaf{i}"))
            .ToList();

        // Act
        var builderMetadata = builder.Build(leafData);
        var originalTree = new MerkleTree(leafData);
        var originalRootHash = originalTree.GetRootHash();

        // Assert
        Assert.Equal(originalRootHash, builderMetadata.RootHash);
    }

    [Fact]
    public async Task BuildAsync_WithNullLeafData_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new MerkleTreeStream();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await builder.BuildAsync(null!));
    }

    [Fact]
    public async Task BuildAsync_WithEmptyLeafData_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new MerkleTreeStream();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await builder.BuildAsync(EmptyAsyncEnumerable()));
    }

    private static async IAsyncEnumerable<byte[]> EmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }

    [Fact]
    public async Task BuildAsync_WithSingleLeaf_ReturnsCorrectMetadata()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateAsyncLeafData("leaf1");

        // Act
        var metadata = await builder.BuildAsync(leafData);

        // Assert
        Assert.NotNull(metadata);
        Assert.NotNull(metadata.RootHash);
        Assert.Equal(0, metadata.Height);
        Assert.Equal(1, metadata.LeafCount);
    }

    [Fact]
    public async Task BuildAsync_WithMultipleLeaves_ReturnsCorrectMetadata()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateAsyncLeafData("leaf1", "leaf2", "leaf3");

        // Act
        var metadata = await builder.BuildAsync(leafData);

        // Assert
        Assert.NotNull(metadata);
        Assert.NotNull(metadata.RootHash);
        Assert.Equal(2, metadata.Height);
        Assert.Equal(3, metadata.LeafCount);
    }

    [Fact]
    public async Task BuildAsync_MatchesSyncBuild()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafDataSync = CreateLeafData("leaf1", "leaf2", "leaf3");
        var leafDataAsync = CreateAsyncLeafData("leaf1", "leaf2", "leaf3");

        // Act
        var syncMetadata = builder.Build(leafDataSync);
        var asyncMetadata = await builder.BuildAsync(leafDataAsync);

        // Assert
        Assert.Equal(syncMetadata.RootHash, asyncMetadata.RootHash);
        Assert.Equal(syncMetadata.Height, asyncMetadata.Height);
        Assert.Equal(syncMetadata.LeafCount, asyncMetadata.LeafCount);
    }

    [Fact]
    public async Task BuildAsync_MatchesOriginalMerkleTree()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafDataSync = CreateLeafData("leaf1", "leaf2", "leaf3");
        var leafDataAsync = CreateAsyncLeafData("leaf1", "leaf2", "leaf3");

        // Act
        var asyncMetadata = await builder.BuildAsync(leafDataAsync);
        var originalTree = new MerkleTree(leafDataSync);
        var originalRootHash = originalTree.GetRootHash();

        // Assert
        Assert.Equal(originalRootHash, asyncMetadata.RootHash);
    }

    [Fact]
    public void Build_WithDifferentHashFunctions_ProducesDifferentResults()
    {
        // Arrange
        var builder256 = new MerkleTreeStream(new Sha256HashFunction());
        var builder512 = new MerkleTreeStream(new Sha512HashFunction());
        var leafData = CreateLeafData("leaf1", "leaf2");

        // Act
        var metadata256 = builder256.Build(leafData);
        var metadata512 = builder512.Build(leafData);

        // Assert
        Assert.NotEqual(metadata256.RootHash.Length, metadata512.RootHash.Length);
        Assert.NotEqual(metadata256.RootHash, metadata512.RootHash);
    }

    [Fact]
    public void Build_ProcessesStreamCorrectly()
    {
        // This test verifies that Build processes leaves correctly

        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = Enumerable.Range(1, 1000)
            .Select(i => Encoding.UTF8.GetBytes($"leaf{i}"))
            .ToList();

        // Act
        var metadata = builder.Build(leafData);

        // Assert
        Assert.Equal(1000, metadata.LeafCount);
        Assert.NotNull(metadata.RootHash);
    }

    private static IEnumerable<byte[]> GenerateStreamingLeaves(int count, int maxMaterialization)
    {
        // This generator simulates streaming data
        for (int i = 0; i < count; i++)
        {
            yield return Encoding.UTF8.GetBytes($"leaf{i}");
        }
    }

    [Fact]
    public void Metadata_Constructor_WithNullRootHash_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MerkleTreeMetadata(null!, 0, 1));
    }

    [Fact]
    public void Metadata_Properties_ReturnCorrectValues()
    {
        // Arrange
        var rootHash = new byte[] { 1, 2, 3, 4 };
        var rootNode = new MerkleTreeNode(rootHash);
        var height = 5;
        var leafCount = 32L;

        // Act
        var metadata = new MerkleTreeMetadata(rootNode, height, leafCount);

        // Assert
        Assert.Same(rootNode, metadata.Root);
        Assert.Equal(rootHash, metadata.RootHash);
        Assert.Equal(height, metadata.Height);
        Assert.Equal(leafCount, metadata.LeafCount);
    }

    #region Proof Generation Tests

    [Fact]
    public void GenerateProof_WithSingleLeaf_GeneratesEmptyProof()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateLeafData("leaf1");

        // Act
        var proof = builder.GenerateProof(leafData, 0, leafData.Count);

        // Assert
        Assert.NotNull(proof);
        Assert.Equal(0, proof.LeafIndex);
        Assert.Equal(0, proof.TreeHeight);
        Assert.Empty(proof.SiblingHashes);
        Assert.Empty(proof.SiblingIsRight);
        Assert.Equal(leafData[0], proof.LeafValue);
    }

    [Fact]
    public void GenerateProof_WithTwoLeaves_GeneratesValidProof()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateLeafData("leaf1", "leaf2");
        var metadata = builder.Build(leafData);

        // Act - Generate proof for first leaf
        var proof0 = builder.GenerateProof(leafData, 0, leafData.Count);

        // Assert
        Assert.Equal(0, proof0.LeafIndex);
        Assert.Equal(1, proof0.TreeHeight);
        Assert.Single(proof0.SiblingHashes);
        Assert.Single(proof0.SiblingIsRight);
        Assert.True(proof0.SiblingIsRight[0]); // Sibling is on the right
        Assert.Equal(leafData[0], proof0.LeafValue);

        // Verify proof
        var hashFunction = new Sha256HashFunction();
        Assert.True(proof0.Verify(metadata.RootHash, hashFunction));

        // Act - Generate proof for second leaf
        var proof1 = builder.GenerateProof(leafData, 1, leafData.Count);

        // Assert
        Assert.Equal(1, proof1.LeafIndex);
        Assert.Equal(1, proof1.TreeHeight);
        Assert.Single(proof1.SiblingHashes);
        Assert.Single(proof1.SiblingIsRight);
        Assert.False(proof1.SiblingIsRight[0]); // Sibling is on the left
        Assert.Equal(leafData[1], proof1.LeafValue);

        // Verify proof
        Assert.True(proof1.Verify(metadata.RootHash, hashFunction));
    }

    [Fact]
    public void GenerateProof_WithThreeLeaves_GeneratesValidProof()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3");
        var metadata = builder.Build(leafData);
        var hashFunction = new Sha256HashFunction();

        // Act & Assert - Generate and verify proofs for all leaves
        for (int i = 0; i < 3; i++)
        {
            var proof = builder.GenerateProof(leafData, i, leafData.Count);
            Assert.Equal(i, proof.LeafIndex);
            Assert.Equal(2, proof.TreeHeight);
            Assert.Equal(2, proof.SiblingHashes.Length);
            Assert.Equal(2, proof.SiblingIsRight.Length);
            Assert.Equal(leafData[i], proof.LeafValue);
            Assert.True(proof.Verify(metadata.RootHash, hashFunction));
        }
    }

    [Fact]
    public void GenerateProof_WithSevenLeaves_GeneratesValidProof()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateLeafData("l1", "l2", "l3", "l4", "l5", "l6", "l7");
        var metadata = builder.Build(leafData);
        var hashFunction = new Sha256HashFunction();

        // Act & Assert - Generate and verify proofs for all leaves
        for (int i = 0; i < 7; i++)
        {
            var proof = builder.GenerateProof(leafData, i, leafData.Count);
            Assert.Equal(i, proof.LeafIndex);
            Assert.Equal(3, proof.TreeHeight);
            Assert.Equal(3, proof.SiblingHashes.Length);
            Assert.Equal(3, proof.SiblingIsRight.Length);
            Assert.Equal(leafData[i], proof.LeafValue);
            Assert.True(proof.Verify(metadata.RootHash, hashFunction));
        }
    }

    [Fact]
    public void GenerateProof_WithInvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateLeafData("leaf1", "leaf2");

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.GenerateProof(leafData, -1, leafData.Count));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.GenerateProof(leafData, 2, leafData.Count));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.GenerateProof(leafData, 100, leafData.Count));
    }

    [Fact]
    public void GenerateProof_WithNullLeafData_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new MerkleTreeStream();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.GenerateProof(null!, 0, 1));
    }

    [Fact]
    public void GenerateProof_WithEmptyLeafData_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var emptyData = new List<byte[]>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => builder.GenerateProof(emptyData, 0, 0));
    }

    [Fact]
    public void GenerateProof_ProducesIdenticalResultsToMerkleTree()
    {
        // Arrange
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3", "leaf4");
        
        var tree = new InMemoryMerkleTree(leafData);
        var stream = new MerkleTreeStream();
        var streamMetadata = stream.Build(leafData);

        // Assert root hashes match
        Assert.Equal(tree.GetRootHash(), streamMetadata.RootHash);

        // Act & Assert - Compare proofs for each leaf
        var hashFunction = new Sha256HashFunction();
        for (int i = 0; i < 4; i++)
        {
            var treeProof = tree.GenerateProof(i);
            var streamProof = stream.GenerateProof(leafData, i, leafData.Count);

            // Proofs should have same structure
            Assert.Equal(treeProof.LeafIndex, streamProof.LeafIndex);
            Assert.Equal(treeProof.TreeHeight, streamProof.TreeHeight);
            Assert.Equal(treeProof.SiblingHashes.Length, streamProof.SiblingHashes.Length);

            // Both should verify against the same root
            Assert.True(treeProof.Verify(tree.GetRootHash(), hashFunction));
            Assert.True(streamProof.Verify(streamMetadata.RootHash, hashFunction));
            
            // Sibling hashes should match
            for (int j = 0; j < treeProof.SiblingHashes.Length; j++)
            {
                Assert.Equal(treeProof.SiblingHashes[j], streamProof.SiblingHashes[j]);
                Assert.Equal(treeProof.SiblingIsRight[j], streamProof.SiblingIsRight[j]);
            }
        }
    }

    [Fact]
    public async Task GenerateProofAsync_WithSingleLeaf_GeneratesEmptyProof()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateAsyncLeafData("leaf1");

        // Act
        var proof = await builder.GenerateProofAsync(leafData, 0, 1);

        // Assert
        Assert.NotNull(proof);
        Assert.Equal(0, proof.LeafIndex);
        Assert.Equal(0, proof.TreeHeight);
        Assert.Empty(proof.SiblingHashes);
        Assert.Empty(proof.SiblingIsRight);
    }

    [Fact]
    public async Task GenerateProofAsync_WithThreeLeaves_GeneratesValidProof()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafDataSync = CreateLeafData("leaf1", "leaf2", "leaf3");
        var leafDataAsync = CreateAsyncLeafData("leaf1", "leaf2", "leaf3");
        var metadata = await builder.BuildAsync(CreateAsyncLeafData("leaf1", "leaf2", "leaf3"));
        var hashFunction = new Sha256HashFunction();

        // Act
        var proof1 = await builder.GenerateProofAsync(CreateAsyncLeafData("leaf1", "leaf2", "leaf3"), 1, 3);

        // Assert
        Assert.Equal(1, proof1.LeafIndex);
        Assert.Equal(2, proof1.TreeHeight);
        Assert.Equal(2, proof1.SiblingHashes.Length);
        Assert.Equal(2, proof1.SiblingIsRight.Length);
        Assert.True(proof1.Verify(metadata.RootHash, hashFunction));
    }

    [Fact]
    public async Task GenerateProofAsync_WithInvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateAsyncLeafData("leaf1", "leaf2");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await builder.GenerateProofAsync(leafData, -1, 2));
    }

    [Fact]
    public async Task GenerateProofAsync_WithNullLeafData_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new MerkleTreeStream();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await builder.GenerateProofAsync(null!, 0, 1));
    }

    [Fact]
    public void GenerateProof_WithLargeTree_GeneratesValidProof()
    {
        // Arrange - Create a larger tree with 100 leaves
        var leafData = Enumerable.Range(0, 100)
            .Select(i => Encoding.UTF8.GetBytes($"leaf{i}"))
            .ToList();
        
        var builder = new MerkleTreeStream();
        var metadata = builder.Build(leafData);
        var hashFunction = new Sha256HashFunction();

        // Act & Assert - Test a few representative leaves
        var testIndices = new[] { 0, 1, 50, 99 };
        foreach (var index in testIndices)
        {
            var proof = builder.GenerateProof(leafData, index, leafData.Count);
            var isValid = proof.Verify(metadata.RootHash, hashFunction);
            
            Assert.True(isValid, $"Proof for leaf {index} should be valid");
            Assert.Equal(leafData[index], proof.LeafValue);
        }
    }

    #endregion
}
