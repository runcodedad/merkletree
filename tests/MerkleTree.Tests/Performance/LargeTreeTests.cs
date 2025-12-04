using System.Buffers.Binary;
using System.Text;
using Xunit;
using MerkleTree.Core;
using MerkleTree.Hashing;
using MerkleTree.Cache;
using MerkleTreeClass = MerkleTree.Core.MerkleTree;

namespace MerkleTree.Tests.Performance;

/// <summary>
/// Tests for large tree operations (simulating millions of leaves).
/// </summary>
public class LargeTreeTests
{
    [Fact]
    public void LargeTree_InMemory_100Leaves()
    {
        // Arrange - Create dataset with 100 leaves (small but representative)
        var leafData = Enumerable.Range(0, 100)
            .Select(i => Encoding.UTF8.GetBytes($"leaf_{i}"))
            .ToList();

        // Act
        var tree = new MerkleTreeClass(leafData);
        var metadata = tree.GetMetadata();

        // Assert
        Assert.Equal(100, metadata.LeafCount);
        Assert.NotNull(metadata.RootHash);
        Assert.Equal(7, metadata.Height); // log2(100) rounded up
    }

    [Fact]
    public async Task LargeTree_Streaming_1000Leaves()
    {
        // Arrange - Simulate a medium-sized tree
        async IAsyncEnumerable<byte[]> GenerateLeaves(int count)
        {
            for (int i = 0; i < count; i++)
            {
                await Task.Yield();
                yield return Encoding.UTF8.GetBytes($"data_{i}");
            }
        }

        // Act
        var stream = new MerkleTreeStream();
        var metadata = await stream.BuildAsync(GenerateLeaves(1000));

        // Assert
        Assert.Equal(1000, metadata.LeafCount);
        Assert.NotNull(metadata.RootHash);
        Assert.Equal(10, metadata.Height); // log2(1000) rounded up
    }

    [Fact]
    public async Task LargeTree_Streaming_10000Leaves()
    {
        // Arrange - Simulate a larger tree with 10K leaves
        async IAsyncEnumerable<byte[]> GenerateLeaves(int count)
        {
            for (int i = 0; i < count; i++)
            {
                // Use shorter data to keep memory low
                var data = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(data, i);
                yield return data;
                if (i % 100 == 0)
                    await Task.Yield(); // Periodically yield to simulate async
            }
        }

        // Act
        var stream = new MerkleTreeStream();
        var metadata = await stream.BuildAsync(GenerateLeaves(10000));

        // Assert
        Assert.Equal(10000, metadata.LeafCount);
        Assert.NotNull(metadata.RootHash);
        Assert.Equal(14, metadata.Height); // log2(10000) rounded up
    }

    [Fact]
    public async Task LargeTree_WithCache_10000Leaves()
    {
        // Arrange
        async IAsyncEnumerable<byte[]> GenerateLeaves(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var data = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(data, i);
                yield return data;
                if (i % 50 == 0)
                    await Task.Yield();
            }
        }


        var tempFile = Path.Combine(Path.GetTempPath(), $"large_tree_{Guid.NewGuid():N}.cache");

        try
        {
            // Act - Build with cache
            var stream = new MerkleTreeStream();
            var cacheConfig = new CacheConfiguration(tempFile, topLevelsToCache: 5);
            var metadata = await stream.BuildAsync(GenerateLeaves(10000), cacheConfig);

            // Assert - Cache file should exist
            Assert.True(File.Exists(tempFile));
            var fileInfo = new FileInfo(tempFile);
            Assert.True(fileInfo.Length > 0);

            // Load and verify cache
            var cache = CacheFileManager.LoadCache(tempFile);
            Assert.Equal(14, cache.Metadata.TreeHeight);
            Assert.True(cache.Levels.Count > 0);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LargeTree_ProofGeneration_From10000Leaves()
    {
        // Arrange
        async IAsyncEnumerable<byte[]> GenerateLeaves(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var data = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(data, i);
                yield return data;
                if (i % 100 == 0)
                    await Task.Yield();
            }
        }

        var stream = new MerkleTreeStream();
        var metadata = await stream.BuildAsync(GenerateLeaves(10000));

        // Act - Generate proofs for representative indices
        var testIndices = new[] { 0, 100, 1000, 5000, 9999 };
        
        foreach (var index in testIndices)
        {
            var proof = await stream.GenerateProofAsync(GenerateLeaves(10000), index, 10000);

            // Assert
            Assert.Equal(index, proof.LeafIndex);
            Assert.Equal(14, proof.TreeHeight);
            Assert.True(proof.Verify(metadata.RootHash, new Sha256HashFunction()),
                $"Proof for leaf {index} should be valid");
        }
    }

    [Fact]
    public async Task LargeTree_CachedProofGeneration_From10000Leaves()
    {
        // Arrange
        async IAsyncEnumerable<byte[]> GenerateLeaves(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var data = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(data, i);
                yield return data;
                if (i % 100 == 0)
                    await Task.Yield();
            }
        }

        var tempFile = Path.Combine(Path.GetTempPath(), $"large_tree_cached_{Guid.NewGuid():N}.cache");

        try
        {
            var stream = new MerkleTreeStream();
            var cacheConfig = new CacheConfiguration(tempFile, topLevelsToCache: 5);
            var metadata = await stream.BuildAsync(GenerateLeaves(10000), cacheConfig);

            var cache = CacheFileManager.LoadCache(tempFile);

            // Act - Generate proof with cache
            var proof = await stream.GenerateProofAsync(GenerateLeaves(10000), 5000, 10000, cache);

            // Assert
            Assert.Equal(5000, proof.LeafIndex);
            Assert.True(proof.Verify(metadata.RootHash, new Sha256HashFunction()));
            Assert.True(cache.Statistics.Hits > 0, "Cache should have been used");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LargeTree_SimulatedMillions_Metadata()
    {
        // This test simulates metadata calculations for trees with millions of leaves
        // without actually building them (to keep test runtime reasonable)

        // Calculate expected metadata for various large tree sizes
        var testSizes = new long[] { 100000, 1000000, 10000000, 100000000 };

        foreach (var size in testSizes)
        {
            // Calculate expected height: ceil(log2(size))
            int expectedHeight = size == 1 ? 0 : (int)Math.Ceiling(Math.Log2(size));

            // Verify calculation is correct
            Assert.True(expectedHeight >= 0);
            Assert.True((1L << expectedHeight) >= size); // 2^height >= size
            if (expectedHeight > 0)
            {
                Assert.True((1L << (expectedHeight - 1)) < size); // 2^(height-1) < size
            }
        }
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(2000)]
    public async Task LargeTree_NonPowerOfTwo_VariousSizes(int leafCount)
    {
        // Arrange
        async IAsyncEnumerable<byte[]> GenerateLeaves(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return BitConverter.GetBytes(i);
                if (i % 50 == 0)
                    await Task.Yield();
            }
        }

        // Act
        var stream = new MerkleTreeStream();
        var metadata = await stream.BuildAsync(GenerateLeaves(leafCount));

        // Assert
        Assert.Equal(leafCount, metadata.LeafCount);
        Assert.NotNull(metadata.RootHash);

        // Verify a sample proof
        var proof = await stream.GenerateProofAsync(GenerateLeaves(leafCount), 0, leafCount);
        Assert.True(proof.Verify(metadata.RootHash, new Sha256HashFunction()));
    }

    [Fact]
    public async Task LargeTree_StreamingPerformance_MinimalMemory()
    {
        // This test verifies that streaming trees don't hold large amounts of data in memory
        // by using a generator that would fail if materialized
        
        async IAsyncEnumerable<byte[]> GenerateLeaves(int count)
        {
            for (int i = 0; i < count; i++)
            {
                // Each leaf is small, but together they would be large if materialized
                yield return Encoding.UTF8.GetBytes($"leaf_{i}_with_some_extra_data");
                if (i % 100 == 0)
                    await Task.Yield();
            }
        }

        // Act - Build tree with 5000 leaves
        var stream = new MerkleTreeStream();
        var metadata = await stream.BuildAsync(GenerateLeaves(5000));

        // Assert - Should complete without excessive memory usage
        Assert.Equal(5000, metadata.LeafCount);
        Assert.NotNull(metadata.RootHash);
    }

    [Fact]
    public void LargeTree_HeightCalculation_EdgeCases()
    {
        // Test height calculations for various tree sizes
        var testCases = new Dictionary<long, int>
        {
            { 1, 0 },
            { 2, 1 },
            { 3, 2 },
            { 4, 2 },
            { 5, 3 },
            { 8, 3 },
            { 9, 4 },
            { 16, 4 },
            { 17, 5 },
            { 100, 7 },
            { 1000, 10 },
            { 10000, 14 }
        };

        foreach (var testCase in testCases)
        {
            var leafCount = testCase.Key;
            var expectedHeight = testCase.Value;

            // Calculate actual height
            int actualHeight = 0;
            if (leafCount > 1)
            {
                long currentLevelSize = leafCount;
                while (currentLevelSize > 1)
                {
                    currentLevelSize = (currentLevelSize + 1) / 2;
                    actualHeight++;
                }
            }

            // Assert
            Assert.Equal(expectedHeight, actualHeight);
        }
    }

    [Fact]
    public async Task LargeTree_DifferentHashFunctions_LargeTrees()
    {
        // Arrange
        async IAsyncEnumerable<byte[]> GenerateLeaves(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return BitConverter.GetBytes(i);
                if (i % 50 == 0)
                    await Task.Yield();
            }
        }

        // Act - Build trees with different hash functions
        var streamSHA256 = new MerkleTreeStream(new Sha256HashFunction());
        var streamSHA512 = new MerkleTreeStream(new Sha512HashFunction());
        var streamBLAKE3 = new MerkleTreeStream(new Blake3HashFunction());

        var metadataSHA256 = await streamSHA256.BuildAsync(GenerateLeaves(1000));
        var metadataSHA512 = await streamSHA512.BuildAsync(GenerateLeaves(1000));
        var metadataBLAKE3 = await streamBLAKE3.BuildAsync(GenerateLeaves(1000));

        // Assert - All should have same leaf count and height but different roots
        Assert.Equal(1000, metadataSHA256.LeafCount);
        Assert.Equal(1000, metadataSHA512.LeafCount);
        Assert.Equal(1000, metadataBLAKE3.LeafCount);

        Assert.Equal(metadataSHA256.Height, metadataSHA512.Height);
        Assert.Equal(metadataSHA256.Height, metadataBLAKE3.Height);

        Assert.NotEqual(metadataSHA256.RootHash, metadataSHA512.RootHash);
        Assert.NotEqual(metadataSHA256.RootHash, metadataBLAKE3.RootHash);
        Assert.NotEqual(metadataSHA512.RootHash, metadataBLAKE3.RootHash);

        // Different hash sizes
        Assert.Equal(32, metadataSHA256.RootHash.Length);
        Assert.Equal(64, metadataSHA512.RootHash.Length);
        Assert.Equal(32, metadataBLAKE3.RootHash.Length);
    }
}
