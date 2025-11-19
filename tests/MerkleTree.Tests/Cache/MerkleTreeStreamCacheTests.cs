using Xunit;
using MerkleTree.Core;
using MerkleTree.Cache;
using MerkleTree.Hashing;
using System.Text;

namespace MerkleTree.Tests.Cache;

/// <summary>
/// Tests for file-based cache integration with MerkleTreeStream.
/// </summary>
public class MerkleTreeStreamCacheTests
{
    /// <summary>
    /// Helper method to create sample leaf data as async enumerable.
    /// </summary>
    private static async IAsyncEnumerable<byte[]> CreateLeafDataAsync(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Yield(); // Simulate async operation
            yield return Encoding.UTF8.GetBytes($"data{i}");
        }
    }

    [Fact]
    public async Task BuildAsync_WithoutCachePath_DoesNotCreateCacheFile()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData = CreateLeafDataAsync(8);

        // Act
        var metadata = await stream.BuildAsync(leafData);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(3, metadata.Height); // 8 leaves -> height 3
    }

    [Fact]
    public async Task BuildAsync_WithCachePath_CreatesCacheFile()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData = CreateLeafDataAsync(16);
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            var cacheConfig = new CacheConfiguration(tempFile, topLevelsToCache: 2);
            var metadata = await stream.BuildAsync(leafData, cacheConfig);

            // Assert
            Assert.NotNull(metadata);
            Assert.True(File.Exists(tempFile));
            var fileInfo = new FileInfo(tempFile);
            Assert.True(fileInfo.Length > 0);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task BuildAsync_WithCache_DoesNotRequireMultiplePasses()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var tempFile = Path.GetTempFileName();
        int dataAccessCount = 0;

        async IAsyncEnumerable<byte[]> TrackedLeafData()
        {
            for (int i = 0; i < 16; i++)
            {
                dataAccessCount++;
                await Task.Yield();
                yield return Encoding.UTF8.GetBytes($"data{i}");
            }
        }

        try
        {
            // Act
            var cacheConfig = new CacheConfiguration(tempFile, topLevelsToCache: 3);
            var metadata = await stream.BuildAsync(TrackedLeafData(), cacheConfig);

            // Assert - Data should only be accessed once (single pass)
            Assert.Equal(16, dataAccessCount);
            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadCache_WithValidFile_LoadsCache()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData = CreateLeafDataAsync(8);
        var tempFile = Path.GetTempFileName();

        try
        {
            var cacheConfig = new CacheConfiguration(tempFile, topLevelsToCache: 2);
            await stream.BuildAsync(leafData, cacheConfig);

            // Act
            var loadedCache = CacheHelper.LoadCache(tempFile);

            // Assert
            Assert.NotNull(loadedCache);
            Assert.NotNull(loadedCache.Data);
            Assert.NotNull(loadedCache.Statistics);
            Assert.True(loadedCache.Data.Levels.Count > 0);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CacheWithStats_TracksStatistics()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData = CreateLeafDataAsync(8);
        var tempFile = Path.GetTempFileName();

        try
        {
            var cacheConfig = new CacheConfiguration(tempFile, topLevelsToCache: 2);
            await stream.BuildAsync(leafData, cacheConfig);
            var cache = CacheHelper.LoadCache(tempFile);

            // Act - Use TryGetNode to trigger statistics tracking
            bool hit = cache.TryGetNode(1, 0, out var value);
            bool miss = cache.TryGetNode(0, 999, out var missValue);

            // Assert
            Assert.True(hit);
            Assert.NotNull(value);
            Assert.False(miss);
            Assert.Equal(1, cache.Statistics.Hits);
            Assert.Equal(1, cache.Statistics.Misses);
            Assert.Equal(2, cache.Statistics.TotalLookups);
            Assert.Equal(50.0, cache.Statistics.HitRate);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GenerateProofAsync_WithCache_UsesCache()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData1 = CreateLeafDataAsync(16);
        var tempFile = Path.GetTempFileName();

        try
        {
            var cacheConfig = new CacheConfiguration(tempFile, topLevelsToCache: 3);
            var metadata = await stream.BuildAsync(leafData1, cacheConfig);
            var cache = CacheHelper.LoadCache(tempFile);
            var leafData2 = CreateLeafDataAsync(16);

            // Act
            var proof = await stream.GenerateProofAsync(leafData2, 5, metadata.LeafCount, cache);

            // Assert
            Assert.NotNull(proof);
            Assert.Equal(5, proof.LeafIndex);
            Assert.Equal(metadata.Height, proof.TreeHeight);
            
            // Verify proof
            Assert.True(proof.Verify(metadata.RootHash, new Sha256HashFunction()));
            
            // Verify cache statistics are tracked
            Assert.True(cache.Statistics.TotalLookups > 0);
            Assert.True(cache.Statistics.Hits > 0);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GenerateProofAsync_WithAndWithoutCache_ProduceSameProof()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData1 = CreateLeafDataAsync(16);
        var tempFile = Path.GetTempFileName();

        try
        {
            var cacheConfig = new CacheConfiguration(tempFile, topLevelsToCache: 2);
            var metadata = await stream.BuildAsync(leafData1, cacheConfig);
            var cache = CacheHelper.LoadCache(tempFile);
            
            // Act - Generate proof with cache
            var leafData2 = CreateLeafDataAsync(16);
            var proofWithCache = await stream.GenerateProofAsync(leafData2, 5, metadata.LeafCount, cache);
            
            // Act - Generate proof without cache
            var leafData3 = CreateLeafDataAsync(16);
            var proofWithoutCache = await stream.GenerateProofAsync(leafData3, 5, metadata.LeafCount, null);

            // Assert - Both proofs should be identical
            Assert.Equal(proofWithCache.LeafIndex, proofWithoutCache.LeafIndex);
            Assert.Equal(proofWithCache.TreeHeight, proofWithoutCache.TreeHeight);
            Assert.Equal(proofWithCache.LeafValue, proofWithoutCache.LeafValue);
            Assert.Equal(proofWithCache.SiblingHashes.Length, proofWithoutCache.SiblingHashes.Length);
            
            for (int i = 0; i < proofWithCache.SiblingHashes.Length; i++)
            {
                Assert.Equal(proofWithCache.SiblingHashes[i], proofWithoutCache.SiblingHashes[i]);
                Assert.Equal(proofWithCache.SiblingIsRight[i], proofWithoutCache.SiblingIsRight[i]);
            }

            // Both proofs should verify
            var rootHash = metadata.RootHash;
            Assert.True(proofWithCache.Verify(rootHash, new Sha256HashFunction()));
            Assert.True(proofWithoutCache.Verify(rootHash, new Sha256HashFunction()));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task BuildAsync_CachesCorrectNumberOfLevels()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData = CreateLeafDataAsync(16); // 16 leaves -> height 4
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act - Cache top 2 levels
            var cacheConfig = new CacheConfiguration(tempFile, topLevelsToCache: 2);
            var metadata = await stream.BuildAsync(leafData, cacheConfig);
            var cache = CacheHelper.LoadCache(tempFile);

            // Assert
            Assert.NotNull(cache);
            Assert.NotNull(cache.Data);
            Assert.Equal(4, cache.Data.Metadata.TreeHeight);
            
            // Should cache levels 2 and 3 (top 2 levels, excluding root at level 4)
            Assert.Equal(2, cache.Data.Metadata.StartLevel);
            Assert.Equal(3, cache.Data.Metadata.EndLevel);
            
            // Verify the levels exist
            Assert.True(cache.Data.Levels.ContainsKey(2));
            Assert.True(cache.Data.Levels.ContainsKey(3));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task BuildAsync_WithZeroTopLevels_DoesNotCreateCache()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData = CreateLeafDataAsync(8);
        var tempFile = Path.GetTempFileName();
        
        // Delete the file so we can check if it gets created
        File.Delete(tempFile);

        try
        {
            // Act
            var cacheConfig = new CacheConfiguration(tempFile, topLevelsToCache: 0);
            var metadata = await stream.BuildAsync(leafData, cacheConfig);

            // Assert
            Assert.NotNull(metadata);
            Assert.False(File.Exists(tempFile)); // Cache file should not be created
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
