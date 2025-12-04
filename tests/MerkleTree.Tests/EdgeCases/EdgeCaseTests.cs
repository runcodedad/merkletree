using System.Buffers.Binary;
using System.Text;
using Xunit;
using MerkleTree.Core;
using MerkleTree.Hashing;
using MerkleTree.Proofs;
using MerkleTree.Cache;
using MerkleTreeClass = MerkleTree.Core.MerkleTree;

namespace MerkleTree.Tests.EdgeCases;

/// <summary>
/// Tests for edge cases and boundary conditions.
/// </summary>
public class EdgeCaseTests
{
    [Fact]
    public void EmptyByteArray_AsLeafData()
    {
        // Arrange - Empty byte arrays are valid leaf data
        var leafData = new List<byte[]>
        {
            Array.Empty<byte>(),
            Array.Empty<byte>(),
            Array.Empty<byte>()
        };

        // Act
        var tree = new MerkleTreeClass(leafData);

        // Assert
        Assert.NotNull(tree.Root);
        Assert.NotNull(tree.GetRootHash());
    }

    [Fact]
    public void VeryLargeLeafValue_SingleLeaf()
    {
        // Arrange - Single leaf with large data
        var largeData = new byte[1024 * 1024]; // 1MB
        Array.Fill(largeData, (byte)0xFF);
        var leafData = new List<byte[]> { largeData };

        // Act
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(0);

        // Assert
        Assert.NotNull(tree.GetRootHash());
        Assert.Equal(largeData, proof.LeafValue);
    }

    [Fact]
    public void MixedLeafSizes_DifferentLengths()
    {
        // Arrange - Leaves with varying sizes
        var leafData = new List<byte[]>
        {
            new byte[] { 1 },
            new byte[] { 1, 2, 3, 4, 5 },
            new byte[] { 1, 2 },
            new byte[1000],
            Array.Empty<byte>()
        };

        // Act
        var tree = new MerkleTreeClass(leafData);
        var hashFunction = new Sha256HashFunction();

        // Assert - Generate and verify proof for each leaf
        for (int i = 0; i < leafData.Count; i++)
        {
            var proof = tree.GenerateProof(i);
            Assert.True(proof.Verify(tree.GetRootHash(), hashFunction),
                $"Proof for leaf {i} should be valid");
        }
    }

    [Fact]
    public async Task StreamingTree_EmptyByteArrayLeaves()
    {
        // Arrange
        async IAsyncEnumerable<byte[]> GetEmptyLeaves()
        {
            for (int i = 0; i < 5; i++)
            {
                await Task.Yield();
                yield return Array.Empty<byte>();
            }
        }

        // Act
        var stream = new MerkleTreeStream();
        var metadata = await stream.BuildAsync(GetEmptyLeaves());

        // Assert
        Assert.Equal(5, metadata.LeafCount);
        Assert.NotNull(metadata.RootHash);
    }

    [Fact]
    public void MaxLeafIndex_BoundaryTest()
    {
        // Arrange
        var leafData = Enumerable.Range(0, 100)
            .Select(i => Encoding.UTF8.GetBytes($"leaf_{i}"))
            .ToList();
        var tree = new MerkleTreeClass(leafData);

        // Act - Test boundary indices
        var proofFirst = tree.GenerateProof(0);
        var proofLast = tree.GenerateProof(99);

        // Assert
        Assert.Equal(0, proofFirst.LeafIndex);
        Assert.Equal(99, proofLast.LeafIndex);
        
        var hashFunction = new Sha256HashFunction();
        Assert.True(proofFirst.Verify(tree.GetRootHash(), hashFunction));
        Assert.True(proofLast.Verify(tree.GetRootHash(), hashFunction));
    }

    [Fact]
    public void IdenticalLeafValues_AllSame()
    {
        // Arrange - All leaves have identical content
        var identicalData = Encoding.UTF8.GetBytes("same");
        var leafData = Enumerable.Range(0, 10)
            .Select(_ => identicalData.ToArray()) // Create copies
            .ToList();

        // Act
        var tree = new MerkleTreeClass(leafData);
        var hashFunction = new Sha256HashFunction();

        // Assert - All proofs should still be unique and valid
        for (int i = 0; i < 10; i++)
        {
            var proof = tree.GenerateProof(i);
            Assert.True(proof.Verify(tree.GetRootHash(), hashFunction),
                $"Proof for leaf {i} should be valid even with identical values");
        }
    }

    [Fact]
    public void CacheConfiguration_InvalidTopLevels_Negative()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"invalid_cache_{Guid.NewGuid():N}.cache");

        // Act & Assert - Negative topLevelsToCache should throw ArgumentException
        Assert.Throws<ArgumentException>(() => 
            new CacheConfiguration(tempFile, topLevelsToCache: -1));
    }

    [Fact]
    public void CacheConfiguration_NullFilePath()
    {
        // Act - Null file path is allowed (caching will be disabled)
        var config = new CacheConfiguration(null!, topLevelsToCache: 5);

        // Assert
        Assert.False(config.IsEnabled);
        Assert.Null(config.FilePath);
    }

    [Fact]
    public void CacheConfiguration_EmptyFilePath()
    {
        // Act - Empty file path is allowed (caching will be disabled)
        var config = new CacheConfiguration(string.Empty, topLevelsToCache: 5);

        // Assert
        Assert.False(config.IsEnabled);
        Assert.Equal(string.Empty, config.FilePath);
    }

    [Fact]
    public void CacheFileManager_LoadNonexistentFile()
    {
        // Arrange
        var nonexistentFile = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.cache");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => 
            CacheFileManager.LoadCache(nonexistentFile));
    }

    [Fact]
    public async Task CacheFileManager_LoadCorruptedFile()
    {
        // Arrange - Create a file with invalid data
        var tempFile = Path.Combine(Path.GetTempPath(), $"corrupted_{Guid.NewGuid():N}.cache");
        await File.WriteAllBytesAsync(tempFile, new byte[] { 1, 2, 3, 4, 5 });

        try
        {
            // Act & Assert - Implementation throws ArgumentException for invalid magic number
            Assert.Throws<ArgumentException>(() => 
                CacheFileManager.LoadCache(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task StreamingTree_CancellationToken_Honored()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        
        async IAsyncEnumerable<byte[]> GetLeaves()
        {
            for (int i = 0; i < 1000; i++)
            {
                if (i == 50)
                    cts.Cancel(); // Cancel mid-stream
                    
                await Task.Yield();
                var data = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(data, i);
                yield return data;
            }
        }

        var stream = new MerkleTreeStream();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await stream.BuildAsync(GetLeaves(), cancellationToken: cts.Token));
    }

    [Fact]
    public void ProofSerialization_MaximumSiblingHashes()
    {
        // Arrange - Create a large tree to get maximum sibling hashes
        var leafData = Enumerable.Range(0, 1000)
            .Select(i => Encoding.UTF8.GetBytes($"leaf_{i}"))
            .ToList();
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(500);

        // Act - Serialize and deserialize
        var serialized = proof.Serialize();
        var deserialized = MerkleProof.Deserialize(serialized);

        // Assert
        Assert.Equal(proof.SiblingHashes.Length, deserialized.SiblingHashes.Length);
        Assert.Equal(proof.TreeHeight, deserialized.TreeHeight);
    }

    [Fact]
    public void TreeNode_WithNullHash_Serialization()
    {
        // Arrange
        var node = new MerkleTreeNode(null!);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => node.Serialize());
    }

    [Fact]
    public void TreeNode_WithEmptyHash_Serialization()
    {
        // Arrange
        var node = new MerkleTreeNode(Array.Empty<byte>());

        // Act
        var serialized = node.Serialize();

        // Assert - Should handle empty hash
        Assert.Empty(serialized);
    }

    [Fact]
    public void MerkleTreeMetadata_NegativeHeight()
    {
        // Arrange
        var rootHash = new byte[] { 1, 2, 3 };
        var rootNode = new MerkleTreeNode(rootHash);

        // Act - Negative height is accepted (no validation in implementation)
        var metadata = new MerkleTreeMetadata(rootNode, -1, 10);

        // Assert - Metadata is created with the provided values
        Assert.Equal(-1, metadata.Height);
        Assert.Equal(10, metadata.LeafCount);
    }

    [Fact]
    public void MerkleTreeMetadata_ZeroLeafCount()
    {
        // Arrange
        var rootHash = new byte[] { 1, 2, 3 };
        var rootNode = new MerkleTreeNode(rootHash);

        // Act - Zero leaf count is accepted (no validation in implementation)
        var metadata = new MerkleTreeMetadata(rootNode, 0, 0);

        // Assert - Metadata is created with the provided values
        Assert.Equal(0, metadata.Height);
        Assert.Equal(0, metadata.LeafCount);
    }

    [Fact]
    public void MerkleTreeMetadata_NegativeLeafCount()
    {
        // Arrange
        var rootHash = new byte[] { 1, 2, 3 };
        var rootNode = new MerkleTreeNode(rootHash);

        // Act - Negative leaf count is accepted (no validation in implementation)
        var metadata = new MerkleTreeMetadata(rootNode, 0, -1);

        // Assert - Metadata is created with the provided values
        Assert.Equal(0, metadata.Height);
        Assert.Equal(-1, metadata.LeafCount);
    }

    [Fact]
    public async Task StreamingProof_LeafNotFound()
    {
        // Arrange
        async IAsyncEnumerable<byte[]> GetLeaves(int count)
        {
            for (int i = 0; i < count; i++)
            {
                await Task.Yield();
                var data = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(data, i);
                yield return data;
            }
        }

        var stream = new MerkleTreeStream();
        await stream.BuildAsync(GetLeaves(10));

        // Act & Assert - Request proof for leaf beyond count
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await stream.GenerateProofAsync(GetLeaves(10), 15, 10));
    }

    [Fact]
    public void ByteArrayComparison_DifferentLengths()
    {
        // Arrange
        var leafData1 = new List<byte[]>
        {
            new byte[] { 1, 2, 3 },
            new byte[] { 4, 5, 6 }
        };
        var leafData2 = new List<byte[]>
        {
            new byte[] { 1, 2, 3, 4 }, // Different length
            new byte[] { 4, 5, 6 }
        };

        // Act
        var tree1 = new MerkleTreeClass(leafData1);
        var tree2 = new MerkleTreeClass(leafData2);

        // Assert - Different leaf lengths should produce different root hashes
        Assert.NotEqual(tree1.GetRootHash(), tree2.GetRootHash());
    }

    [Fact]
    public async Task CacheStatistics_InitialState()
    {
        // Arrange
        async IAsyncEnumerable<byte[]> GetLeaves()
        {
            for (int i = 0; i < 10; i++)
            {
                await Task.Yield();
                var data = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(data, i);
                yield return data;
            }
        }

        var tempFile = Path.Combine(Path.GetTempPath(), $"stats_test_{Guid.NewGuid():N}.cache");

        try
        {
            var stream = new MerkleTreeStream();
            var cacheConfig = new CacheConfiguration(tempFile, topLevelsToCache: 2);
            await stream.BuildAsync(GetLeaves(), cacheConfig);

            // Act
            var cache = CacheFileManager.LoadCache(tempFile);

            // Assert - Initial statistics should be zero
            Assert.Equal(0, cache.Statistics.Hits);
            Assert.Equal(0, cache.Statistics.Misses);
            Assert.Equal(0, cache.Statistics.TotalLookups);
            Assert.Equal(0.0, cache.Statistics.HitRate);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void CachedLevel_GetNode_OutOfRange()
    {
        // Arrange - Create a cached level with specific nodes
        var nodes = new byte[][]
        {
            new byte[] { 1, 2, 3 },
            new byte[] { 4, 5, 6 }
        };
        var level = new CachedLevel(1, nodes);

        // Act & Assert - Request node beyond what's stored
        Assert.Throws<ArgumentOutOfRangeException>(() => level.GetNode(5));
    }

    [Fact]
    public void CachedLevel_Constructor_WithNullNode()
    {
        // Arrange
        var nodes = new byte[][]
        {
            new byte[] { 1, 2, 3 },
            null! // Null node
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new CachedLevel(1, nodes));
    }

    [Fact]
    public void CacheData_TryGetNode_InvalidLevel()
    {
        // Arrange
        var metadata = new CacheMetadata(
            treeHeight: 5,
            hashFunctionName: "SHA-256",
            hashSizeInBytes: 32,
            startLevel: 2,
            endLevel: 4
        );
        
        // Create levels dictionary
        var levels = new Dictionary<int, CachedLevel>();
        for (int i = 2; i <= 4; i++)
        {
            var nodes = new byte[][] { new byte[32] };
            levels[i] = new CachedLevel(i, nodes);
        }
        
        var cache = new CacheData(metadata, levels);

        // Act - Try to get node from level not in cache
        bool found = cache.TryGetNode(0, 0, out var hash);

        // Assert
        Assert.False(found);
        Assert.Null(hash);
    }

    [Fact]
    public void Proof_WithMaxHeight()
    {
        // Arrange - Create a tree with maximum practical height
        var leafData = Enumerable.Range(0, 500)
            .Select(i => {
                var data = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(data, i);
                return data;
            })
            .ToList();
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(250);

        // Act - Serialize and verify
        var serialized = proof.Serialize();
        var deserialized = MerkleProof.Deserialize(serialized);

        // Assert
        Assert.Equal(proof.TreeHeight, deserialized.TreeHeight);
        Assert.True(deserialized.Verify(tree.GetRootHash(), new Sha256HashFunction()));
    }
}
