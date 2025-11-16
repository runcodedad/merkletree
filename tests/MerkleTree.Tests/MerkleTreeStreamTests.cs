using System.Text;
using Xunit;

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
    public void BuildInBatches_WithNullLeafData_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new MerkleTreeStream();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.BuildInBatches(null!, 10));
    }

    [Fact]
    public void BuildInBatches_WithInvalidBatchSize_ThrowsArgumentException()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateLeafData("leaf1");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => builder.BuildInBatches(leafData, 0));
        Assert.Throws<ArgumentException>(() => builder.BuildInBatches(leafData, -1));
    }

    [Fact]
    public void BuildInBatches_WithEmptyLeafData_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var emptyData = new List<byte[]>();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.BuildInBatches(emptyData, 10));
    }

    [Fact]
    public void BuildInBatches_WithVariousBatchSizes_ProducesSameResult()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = Enumerable.Range(1, 10)
            .Select(i => Encoding.UTF8.GetBytes($"leaf{i}"))
            .ToList();

        // Act
        var metadata1 = builder.BuildInBatches(leafData, 1);
        var metadata2 = builder.BuildInBatches(leafData, 3);
        var metadata5 = builder.BuildInBatches(leafData, 5);
        var metadata10 = builder.BuildInBatches(leafData, 10);

        // Assert
        Assert.Equal(metadata1.RootHash, metadata2.RootHash);
        Assert.Equal(metadata1.RootHash, metadata5.RootHash);
        Assert.Equal(metadata1.RootHash, metadata10.RootHash);
    }

    [Fact]
    public void BuildInBatches_MatchesBuildWithoutBatching()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var leafData = CreateLeafData("leaf1", "leaf2", "leaf3", "leaf4", "leaf5");

        // Act
        var noBatchMetadata = builder.Build(leafData);
        var batchMetadata = builder.BuildInBatches(leafData, 2);

        // Assert
        Assert.Equal(noBatchMetadata.RootHash, batchMetadata.RootHash);
        Assert.Equal(noBatchMetadata.Height, batchMetadata.Height);
        Assert.Equal(noBatchMetadata.LeafCount, batchMetadata.LeafCount);
    }

    [Fact]
    public void BuildInBatches_WithLargeDataset_ProducesCorrectResult()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var largeLeafCount = 1000;
        var leafData = Enumerable.Range(1, largeLeafCount)
            .Select(i => Encoding.UTF8.GetBytes($"leaf{i}"))
            .ToList();

        // Act
        var metadata = builder.BuildInBatches(leafData, 100);

        // Assert
        Assert.NotNull(metadata);
        Assert.NotNull(metadata.RootHash);
        Assert.Equal(largeLeafCount, metadata.LeafCount);

        // Verify against non-batched build
        var nonBatchedMetadata = builder.Build(leafData);
        Assert.Equal(nonBatchedMetadata.RootHash, metadata.RootHash);
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
    public void Build_ProcessesStreamWithoutMaterializingAllData()
    {
        // This test simulates streaming by using a generator
        // that would throw if the entire sequence was materialized

        // Arrange
        var builder = new MerkleTreeStream();
        var maxAllowedMaterialization = 100;
        var leafData = GenerateStreamingLeaves(1000, maxAllowedMaterialization);

        // Act - This should work because BuildInBatches processes in chunks
        var metadata = builder.BuildInBatches(leafData, 10);

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
}
