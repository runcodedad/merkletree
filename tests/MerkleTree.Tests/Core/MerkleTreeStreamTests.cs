using System.Text;
using MerkleTree.Core;
using MerkleTree.Hashing;
using MerkleTreeClass = MerkleTree.Core.MerkleTree;

namespace MerkleTree.Tests.Core;

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
    public async Task BuildAsync_MatchesOriginalMerkleTree()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafDataSync = CreateLeafData("leaf1", "leaf2", "leaf3");
        var leafDataAsync = CreateAsyncLeafData("leaf1", "leaf2", "leaf3");

        // Act
        var asyncMetadata = await builder.BuildAsync(leafDataAsync);
        var originalTree = new MerkleTreeClass(leafDataSync);
        var originalRootHash = originalTree.GetRootHash();

        // Assert
        Assert.Equal(originalRootHash, asyncMetadata.RootHash);
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

    [Fact]
    public async Task GenerateProof_WithSingleLeaf_GeneratesEmptyProof()
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
    public async Task GenerateProof_WithInvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateAsyncLeafData("leaf1", "leaf2");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await builder.GenerateProofAsync(leafData, -1, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await builder.GenerateProofAsync(CreateAsyncLeafData("leaf1", "leaf2"), 2, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await builder.GenerateProofAsync(CreateAsyncLeafData("leaf1", "leaf2"), 100, 2));
    }

    [Fact]
    public async Task GenerateProof_WithNullLeafData_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new MerkleTreeStream();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await builder.GenerateProofAsync(null!, 0, 1));
    }

    [Fact]
    public async Task GenerateProof_WithEmptyLeafData_ThrowsArgumentException()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var emptyData = CreateAsyncLeafData();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await builder.GenerateProofAsync(emptyData, 0, 0));
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
}
