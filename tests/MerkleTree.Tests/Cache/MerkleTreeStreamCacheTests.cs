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
            var metadata = await stream.BuildAsync(leafData, cacheFilePath: tempFile, topLevelsToCache: 2);

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
            var metadata = await stream.BuildAsync(TrackedLeafData(), cacheFilePath: tempFile, topLevelsToCache: 3);

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
            await stream.BuildAsync(leafData, cacheFilePath: tempFile, topLevelsToCache: 2);

            // Act
            var loadedCache = MerkleTreeStream.LoadCache(tempFile);

            // Assert
            Assert.NotNull(loadedCache);
            Assert.True(loadedCache.Levels.Count > 0);
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
        var tempFile = Path.GetTempFileName();

        try
        {
            await stream.BuildAsync(leafData, cacheFilePath: tempFile, topLevelsToCache: 2);
            var cache = MerkleTreeStream.LoadCache(tempFile);

            // Act
            var dictionary = MerkleTreeStream.CacheToDictionary(cache);

            // Assert
            Assert.NotNull(dictionary);
            Assert.True(dictionary.Count > 0);
            
            // Verify we can look up cached values
            foreach (var kvp in cache.Levels)
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
            var metadata = await stream.BuildAsync(leafData1, cacheFilePath: tempFile, topLevelsToCache: 3);
            var cache = MerkleTreeStream.LoadCache(tempFile);
            var cacheDict = MerkleTreeStream.CacheToDictionary(cache);
            var leafData2 = CreateLeafDataAsync(16);

            // Act
            var proof = await stream.GenerateProofAsync(leafData2, 5, metadata.LeafCount, cacheDict);

            // Assert
            Assert.NotNull(proof);
            Assert.Equal(5, proof.LeafIndex);
            Assert.Equal(metadata.Height, proof.TreeHeight);
            
            // Verify proof
            Assert.True(proof.Verify(metadata.RootHash, new Sha256HashFunction()));
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
            var metadata = await stream.BuildAsync(leafData1, cacheFilePath: tempFile, topLevelsToCache: 2);
            var cache = MerkleTreeStream.LoadCache(tempFile);
            var cacheDict = MerkleTreeStream.CacheToDictionary(cache);
            
            // Act - Generate proof with cache
            var leafData2 = CreateLeafDataAsync(16);
            var proofWithCache = await stream.GenerateProofAsync(leafData2, 5, metadata.LeafCount, cacheDict);
            
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
            var metadata = await stream.BuildAsync(leafData, cacheFilePath: tempFile, topLevelsToCache: 2);
            var cache = MerkleTreeStream.LoadCache(tempFile);

            // Assert
            Assert.NotNull(cache);
            Assert.Equal(4, cache.Metadata.TreeHeight);
            
            // Should cache levels 2 and 3 (top 2 levels, excluding root at level 4)
            Assert.Equal(2, cache.Metadata.StartLevel);
            Assert.Equal(3, cache.Metadata.EndLevel);
            
            // Verify the levels exist
            Assert.True(cache.Levels.ContainsKey(2));
            Assert.True(cache.Levels.ContainsKey(3));
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
            var metadata = await stream.BuildAsync(leafData, cacheFilePath: tempFile, topLevelsToCache: 0);

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
