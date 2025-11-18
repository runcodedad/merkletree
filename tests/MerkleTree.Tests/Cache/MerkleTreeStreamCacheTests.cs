using Xunit;
using MerkleTree.Core;
using MerkleTree.Cache;
using MerkleTree.Hashing;
using System.Text;

namespace MerkleTree.Tests.Cache;

/// <summary>
/// Tests for cache integration with MerkleTreeStream.
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
    public async Task BuildAsync_WithoutCacheConfig_ReturnsNullCache()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData = CreateLeafDataAsync(8);

        // Act
        var (metadata, cache) = await stream.BuildAsync(leafData, null);

        // Assert
        Assert.NotNull(metadata);
        Assert.Null(cache);
    }

    [Fact]
    public async Task BuildAsync_WithCacheConfig_ReturnsCache()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData = CreateLeafDataAsync(8);
        var cacheConfig = new CacheConfiguration(1, 2);

        // Act
        var (metadata, cache) = await stream.BuildAsync(leafData, cacheConfig);

        // Assert
        Assert.NotNull(metadata);
        Assert.NotNull(cache);
        Assert.Equal(1, cache.Metadata.StartLevel);
        Assert.Equal(2, cache.Metadata.EndLevel);
    }

    [Fact]
    public async Task BuildAsync_WithTopLevelsConfig_BuildsCorrectCache()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData1 = CreateLeafDataAsync(16);
        
        // First, build without cache to get tree height
        var metadataOnly = await stream.BuildAsync(leafData1);
        var treeHeight = metadataOnly.Height;
        
        // Build with cache for top 2 levels
        var leafData2 = CreateLeafDataAsync(16);
        var cacheConfig = CacheConfiguration.ForTopLevels(treeHeight, 2);

        // Act
        var (metadata, cache) = await stream.BuildAsync(leafData2, cacheConfig);

        // Assert
        Assert.NotNull(cache);
        Assert.Equal(treeHeight - 2, cache.Metadata.StartLevel);
        Assert.Equal(treeHeight - 1, cache.Metadata.EndLevel);
    }

    [Fact]
    public async Task SaveCache_WithValidCache_WritesFile()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData = CreateLeafDataAsync(8);
        var cacheConfig = new CacheConfiguration(1, 2);
        var (metadata, cache) = await stream.BuildAsync(leafData, cacheConfig);
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            MerkleTreeStream.SaveCache(cache!, tempFile);

            // Assert
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
    public void SaveCache_WithNullCache_ThrowsArgumentNullException()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                MerkleTreeStream.SaveCache(null!, tempFile));
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
        var cacheConfig = new CacheConfiguration(1, 2);
        var (metadata, cache) = await stream.BuildAsync(leafData, cacheConfig);
        var tempFile = Path.GetTempFileName();

        try
        {
            MerkleTreeStream.SaveCache(cache!, tempFile);

            // Act
            var loadedCache = MerkleTreeStream.LoadCache(tempFile);

            // Assert
            Assert.NotNull(loadedCache);
            Assert.Equal(cache.Metadata.StartLevel, loadedCache.Metadata.StartLevel);
            Assert.Equal(cache.Metadata.EndLevel, loadedCache.Metadata.EndLevel);
            Assert.Equal(cache.Metadata.TreeHeight, loadedCache.Metadata.TreeHeight);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CacheToDictionary_ConvertsCorrectly()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData = CreateLeafDataAsync(8);
        var cacheConfig = new CacheConfiguration(1, 2);
        var (metadata, cache) = await stream.BuildAsync(leafData, cacheConfig);

        // Act
        var dictionary = MerkleTreeStream.CacheToDictionary(cache!);

        // Assert
        Assert.NotNull(dictionary);
        Assert.True(dictionary.Count > 0);
        
        // Verify we can look up cached values
        foreach (var kvp in cache!.Levels)
        {
            int level = kvp.Key;
            var cachedLevel = kvp.Value;
            
            for (long i = 0; i < cachedLevel.NodeCount; i++)
            {
                Assert.True(dictionary.ContainsKey((level, i)));
                Assert.Equal(cachedLevel.GetNode(i), dictionary[(level, i)]);
            }
        }
    }

    [Fact]
    public async Task GenerateProofAsync_WithCache_UsesCache()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData1 = CreateLeafDataAsync(16);
        var cacheConfig = new CacheConfiguration(1, 3);
        var (metadata, cache) = await stream.BuildAsync(leafData1, cacheConfig);
        
        var cacheDict = MerkleTreeStream.CacheToDictionary(cache!);
        var leafData2 = CreateLeafDataAsync(16);

        // Act
        var proof = await stream.GenerateProofAsync(leafData2, 5, metadata.LeafCount, cacheDict);

        // Assert
        Assert.NotNull(proof);
        Assert.Equal(5, proof.LeafIndex);
        Assert.Equal(metadata.Height, proof.TreeHeight);
    }

    [Fact]
    public async Task GenerateProofAsync_WithAndWithoutCache_ProduceSameProof()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData1 = CreateLeafDataAsync(16);
        var cacheConfig = new CacheConfiguration(1, 2);
        var (metadata, cache) = await stream.BuildAsync(leafData1, cacheConfig);
        
        var cacheDict = MerkleTreeStream.CacheToDictionary(cache!);
        
        // Act - Generate proof with cache
        var leafData2 = CreateLeafDataAsync(16);
        var proofWithCache = await stream.GenerateProofAsync(
            leafData2, 5, metadata.LeafCount, cacheDict);
        
        // Act - Generate proof without cache
        var leafData3 = CreateLeafDataAsync(16);
        var proofWithoutCache = await stream.GenerateProofAsync(
            leafData3, 5, metadata.LeafCount, null);

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

    [Fact]
    public async Task BuildAsync_WithLevelZeroCache_CachesLeaves()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData = CreateLeafDataAsync(8);
        var cacheConfig = new CacheConfiguration(0, 1);

        // Act
        var (metadata, cache) = await stream.BuildAsync(leafData, cacheConfig);

        // Assert
        Assert.NotNull(cache);
        Assert.Equal(0, cache.Metadata.StartLevel);
        Assert.True(cache.Levels.ContainsKey(0));
    }

    [Fact]
    public async Task SaveAndLoadCache_RoundTrip_PreservesData()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData1 = CreateLeafDataAsync(16);
        var cacheConfig = new CacheConfiguration(1, 3);
        var (metadata, cache) = await stream.BuildAsync(leafData1, cacheConfig);
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act - Save and load
            MerkleTreeStream.SaveCache(cache!, tempFile);
            var loadedCache = MerkleTreeStream.LoadCache(tempFile);

            // Generate proofs using both caches
            var originalDict = MerkleTreeStream.CacheToDictionary(cache!);
            var loadedDict = MerkleTreeStream.CacheToDictionary(loadedCache);
            
            var leafData2 = CreateLeafDataAsync(16);
            var leafData3 = CreateLeafDataAsync(16);
            
            var originalProof = await stream.GenerateProofAsync(
                leafData2, 7, metadata.LeafCount, originalDict);
            var loadedProof = await stream.GenerateProofAsync(
                leafData3, 7, metadata.LeafCount, loadedDict);

            // Assert - Proofs should be identical
            Assert.Equal(originalProof.LeafIndex, loadedProof.LeafIndex);
            Assert.Equal(originalProof.TreeHeight, loadedProof.TreeHeight);
            
            for (int i = 0; i < originalProof.SiblingHashes.Length; i++)
            {
                Assert.Equal(originalProof.SiblingHashes[i], loadedProof.SiblingHashes[i]);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task BuildAsync_WithDisabledCache_ReturnsNullCache()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData = CreateLeafDataAsync(8);
        var cacheConfig = CacheConfiguration.Disabled();

        // Act
        var (metadata, cache) = await stream.BuildAsync(leafData, cacheConfig);

        // Assert
        Assert.NotNull(metadata);
        Assert.Null(cache);
    }

    [Fact]
    public async Task BuildAsync_CachesCorrectNumberOfNodes()
    {
        // Arrange
        var stream = new MerkleTreeStream();
        var leafData = CreateLeafDataAsync(16); // 16 leaves -> height 4
        var cacheConfig = new CacheConfiguration(2, 3); // Cache levels 2 and 3

        // Act
        var (metadata, cache) = await stream.BuildAsync(leafData, cacheConfig);

        // Assert
        Assert.NotNull(cache);
        
        // Level 2 should have 4 nodes (16 / 2^2)
        Assert.True(cache.Levels.ContainsKey(2));
        Assert.Equal(4, cache.GetLevel(2).NodeCount);
        
        // Level 3 should have 2 nodes (16 / 2^3)
        Assert.True(cache.Levels.ContainsKey(3));
        Assert.Equal(2, cache.GetLevel(3).NodeCount);
    }
}
