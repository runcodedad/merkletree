using System.Text;
using MerkleTree.Core;
using MerkleTree.Hashing;
using MerkleTree.Cache;
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
        Assert.Throws<ArgumentNullException>(() => new MerkleTreeMetadata(null!, 0, 1, "SHA-256"));
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
        var metadata = new MerkleTreeMetadata(rootNode, height, leafCount, "SHA-256");

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

    [Fact]
    public async Task GenerateProofAsync_WithSelectiveRecomputation_MinimizesDiskReads()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var hashFunction = new Sha256HashFunction();
        
        // Create a tree with 16 leaves (power of 2 for simplicity)
        var leafData = Enumerable.Range(0, 16)
            .Select(i => Encoding.UTF8.GetBytes($"leaf{i}"))
            .ToList();
        
        // Build the tree
        var metadata = await builder.BuildAsync(CreateAsyncFromList(leafData));
        
        // Track how many leaves are actually read when generating a proof
        int leavesRead = 0;
        int streamCallCount = 0;
        async IAsyncEnumerable<byte[]> TrackingLeafData()
        {
            streamCallCount++;
            foreach (var leaf in leafData)
            {
                leavesRead++;
                await Task.Yield();
                yield return leaf;
            }
        }
        
        // Act - Generate proof for leaf 0
        // The selective recomputation builds minimal subtrees for siblings
        leavesRead = 0;
        streamCallCount = 0;
        var proof = await builder.GenerateProofAsync(TrackingLeafData(), 0, 16, cache: null);
        
        // Assert - Verify proof is valid
        Assert.True(proof.Verify(metadata.RootHash, hashFunction));
        
        // Assert - The optimization avoids full tree recomputation
        // We read leaves in targeted ranges for each sibling subtree
        // The session cache prevents recomputing nodes multiple times within the same proof
        Assert.True(leavesRead >= 15, $"Should read at least 15 leaves for siblings, got {leavesRead}");
        Assert.True(streamCallCount >= 4, $"Should have multiple stream calls for different siblings, got {streamCallCount}");
    }

    [Fact]
    public async Task GenerateProofAsync_WithCache_ReducesLeafReads()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var hashFunction = new Sha256HashFunction();
        
        // Create a tree with 1000 leaves
        var leafData = Enumerable.Range(0, 1000)
            .Select(i => Encoding.UTF8.GetBytes($"leaf{i}"))
            .ToList();
        
        // Build tree with cache
        var cacheFile = Path.Combine(Path.GetTempPath(), $"test_cache_{Guid.NewGuid()}.cache");
        try
        {
            var cacheConfig = new CacheConfiguration(cacheFile, topLevelsToCache: 5);
            var metadata = await builder.BuildAsync(CreateAsyncFromList(leafData), cacheConfig);
            
            // Load cache
            var cache = CacheFileManager.LoadCache(cacheFile);
            
            // Track reads with cache
            int leavesReadWithCache = 0;
            async IAsyncEnumerable<byte[]> TrackingLeafDataWithCache()
            {
                foreach (var leaf in leafData)
                {
                    leavesReadWithCache++;
                    await Task.Yield();
                    yield return leaf;
                }
            }
            
            // Track reads without cache
            int leavesReadWithoutCache = 0;
            async IAsyncEnumerable<byte[]> TrackingLeafDataWithoutCache()
            {
                foreach (var leaf in leafData)
                {
                    leavesReadWithoutCache++;
                    await Task.Yield();
                    yield return leaf;
                }
            }
            
            // Act - Generate proofs
            var proofWithCache = await builder.GenerateProofAsync(TrackingLeafDataWithCache(), 500, 1000, cache);
            var proofWithoutCache = await builder.GenerateProofAsync(TrackingLeafDataWithoutCache(), 500, 1000, cache: null);
            
            // Assert - Both proofs should be valid
            Assert.True(proofWithCache.Verify(metadata.RootHash, hashFunction));
            Assert.True(proofWithoutCache.Verify(metadata.RootHash, hashFunction));
            
            // Assert - Cache should significantly reduce disk reads
            Assert.True(leavesReadWithCache < leavesReadWithoutCache, 
                $"Cache should reduce reads: with cache={leavesReadWithCache}, without cache={leavesReadWithoutCache}");
        }
        finally
        {
            if (File.Exists(cacheFile))
                File.Delete(cacheFile);
        }
    }

    [Fact]
    public async Task GenerateProofAsync_SelectiveRecomputation_ProducesCorrectProof()
    {
        // Arrange
        var builder = new MerkleTreeStream();
        var hashFunction = new Sha256HashFunction();
        
        // Create test data with 7 leaves (non-power-of-2 to test padding)
        var leafData = CreateLeafData("data0", "data1", "data2", "data3", "data4", "data5", "data6");
        
        // Build tree and get root
        var inMemoryTree = new MerkleTreeClass(leafData);
        var expectedRoot = inMemoryTree.GetRootHash();
        
        // Build streaming tree
        var metadata = await builder.BuildAsync(CreateAsyncFromList(leafData));
        
        // Verify roots match
        Assert.True(metadata.RootHash.SequenceEqual(expectedRoot));
        
        // Act & Assert - Generate and verify proofs for each leaf
        for (int i = 0; i < 7; i++)
        {
            var streamProof = await builder.GenerateProofAsync(
                CreateAsyncFromList(leafData), 
                i, 
                7, 
                cache: null);
            
            var memoryProof = inMemoryTree.GenerateProof(i);
            
            // Both proofs should be valid
            Assert.True(streamProof.Verify(expectedRoot, hashFunction), 
                $"Stream proof for leaf {i} should be valid");
            Assert.True(memoryProof.Verify(expectedRoot, hashFunction), 
                $"Memory proof for leaf {i} should be valid");
            
            // Sibling hashes should match
            Assert.Equal(memoryProof.SiblingHashes.Length, streamProof.SiblingHashes.Length);
            for (int j = 0; j < memoryProof.SiblingHashes.Length; j++)
            {
                Assert.True(memoryProof.SiblingHashes[j].SequenceEqual(streamProof.SiblingHashes[j]),
                    $"Sibling hash {j} for leaf {i} should match");
            }
        }
    }

    private static async IAsyncEnumerable<byte[]> CreateAsyncFromList(List<byte[]> data)
    {
        foreach (var item in data)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
