using System.Text;
using Xunit;
using MerkleTree.Hashing;
using MerkleTree.Cache;
using MerkleTree.Proofs;
using MerkleTreeClass = MerkleTree.Core.MerkleTree;
using MerkleTreeStreamClass = MerkleTree.Core.MerkleTreeStream;

namespace MerkleTree.Tests.Core;

/// <summary>
/// Tests to verify complete determinism of all Merkle tree operations.
/// </summary>
/// <remarks>
/// These tests ensure that:
/// - Same inputs always produce identical outputs
/// - Serialization is platform-independent (endianness-safe)
/// - No system-specific dependencies affect results
/// - All operations are 100% deterministic
/// </remarks>
public class DeterminismTests
{
    /// <summary>
    /// Helper method to create leaf data from strings.
    /// </summary>
    private static List<byte[]> CreateLeafData(params string[] data)
    {
        return data.Select(s => Encoding.UTF8.GetBytes(s)).ToList();
    }

    [Fact]
    public void MerkleTree_SameInputs_ProduceIdenticalRootHash()
    {
        // Arrange
        var leafData = CreateLeafData("data1", "data2", "data3", "data4", "data5");

        // Act - Build tree multiple times
        var tree1 = new MerkleTreeClass(leafData);
        var tree2 = new MerkleTreeClass(leafData);
        var tree3 = new MerkleTreeClass(leafData);

        var root1 = tree1.GetRootHash();
        var root2 = tree2.GetRootHash();
        var root3 = tree3.GetRootHash();

        // Assert - All roots should be identical
        Assert.Equal(root1, root2);
        Assert.Equal(root2, root3);
        Assert.Equal(root1, root3);
    }

    [Fact]
    public void MerkleTree_RepeatedBuilds_ProduceSameMetadata()
    {
        // Arrange
        var leafData = CreateLeafData("a", "b", "c", "d", "e", "f", "g");

        // Act - Build tree multiple times
        var tree1 = new MerkleTreeClass(leafData);
        var tree2 = new MerkleTreeClass(leafData);

        var metadata1 = tree1.GetMetadata();
        var metadata2 = tree2.GetMetadata();

        // Assert
        Assert.Equal(metadata1.RootHash, metadata2.RootHash);
        Assert.Equal(metadata1.Height, metadata2.Height);
        Assert.Equal(metadata1.LeafCount, metadata2.LeafCount);
    }

    [Fact]
    public async Task MerkleTreeStream_SameInputs_ProduceIdenticalRootHash()
    {
        // Arrange
        var leafData = CreateLeafData("data1", "data2", "data3", "data4", "data5");

        async IAsyncEnumerable<byte[]> GetAsyncLeaves()
        {
            foreach (var leaf in leafData)
            {
                await Task.Yield();
                yield return leaf;
            }
        }

        // Act - Build tree multiple times
        var stream1 = new MerkleTreeStreamClass();
        var stream2 = new MerkleTreeStreamClass();
        var stream3 = new MerkleTreeStreamClass();

        var metadata1 = await stream1.BuildAsync(GetAsyncLeaves());
        var metadata2 = await stream2.BuildAsync(GetAsyncLeaves());
        var metadata3 = await stream3.BuildAsync(GetAsyncLeaves());

        // Assert - All roots should be identical
        Assert.Equal(metadata1.RootHash, metadata2.RootHash);
        Assert.Equal(metadata2.RootHash, metadata3.RootHash);
    }

    [Fact]
    public void MerkleProof_SerializeDeserialize_ProducesIdenticalData()
    {
        // Arrange
        var leafData = CreateLeafData("data1", "data2", "data3", "data4");
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(1);

        // Act - Serialize and deserialize multiple times
        var serialized1 = proof.Serialize();
        var deserialized1 = MerkleProof.Deserialize(serialized1);
        
        var serialized2 = deserialized1.Serialize();
        var deserialized2 = MerkleProof.Deserialize(serialized2);
        
        var serialized3 = deserialized2.Serialize();

        // Assert - All serializations should be byte-for-byte identical
        Assert.Equal(serialized1, serialized2);
        Assert.Equal(serialized2, serialized3);
        
        // Verify proof contents are preserved
        Assert.Equal(proof.LeafValue, deserialized1.LeafValue);
        Assert.Equal(proof.LeafIndex, deserialized1.LeafIndex);
        Assert.Equal(proof.TreeHeight, deserialized1.TreeHeight);
        Assert.Equal(proof.SiblingHashes.Length, deserialized1.SiblingHashes.Length);
        Assert.Equal(proof.SiblingIsRight, deserialized1.SiblingIsRight);
    }

    [Fact]
    public void MerkleProof_MultipleSerializations_AreIdentical()
    {
        // Arrange
        var leafData = CreateLeafData("data1", "data2", "data3");
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(0);

        // Act - Serialize multiple times
        var serialized1 = proof.Serialize();
        var serialized2 = proof.Serialize();
        var serialized3 = proof.Serialize();

        // Assert - All should be byte-for-byte identical
        Assert.Equal(serialized1, serialized2);
        Assert.Equal(serialized2, serialized3);
    }

    [Fact]
    public async Task CacheSerializer_SerializeDeserialize_ProducesIdenticalData()
    {
        // Arrange
        var leafData = CreateLeafData("a", "b", "c", "d", "e", "f", "g", "h");
        var stream = new MerkleTreeStreamClass();
        var tempFile = Path.Combine(Path.GetTempPath(), $"determinism_test_{Guid.NewGuid():N}.cache");
        
        try
        {
            async IAsyncEnumerable<byte[]> GetAsyncLeaves()
            {
                foreach (var leaf in leafData)
                {
                    await Task.Yield();
                    yield return leaf;
                }
            }

            var cacheConfig = new CacheConfiguration(tempFile, topLevelsToCache: 2);
            var metadata = await stream.BuildAsync(GetAsyncLeaves(), cacheConfig);
            
            var cache = CacheFileManager.LoadCache(tempFile);

            // Act - Serialize and deserialize multiple times
            var serialized1 = CacheSerializer.Serialize(cache);
            var deserialized1 = CacheSerializer.Deserialize(serialized1);
            
            var serialized2 = CacheSerializer.Serialize(deserialized1);
            var deserialized2 = CacheSerializer.Deserialize(serialized2);
            
            var serialized3 = CacheSerializer.Serialize(deserialized2);

            // Assert - All serializations should be byte-for-byte identical
            Assert.Equal(serialized1, serialized2);
            Assert.Equal(serialized2, serialized3);
            
            // Verify metadata is preserved
            Assert.Equal(cache.Metadata.TreeHeight, deserialized1.Metadata.TreeHeight);
            Assert.Equal(cache.Metadata.HashFunctionName, deserialized1.Metadata.HashFunctionName);
            Assert.Equal(cache.Metadata.HashSizeInBytes, deserialized1.Metadata.HashSizeInBytes);
            Assert.Equal(cache.Metadata.StartLevel, deserialized1.Metadata.StartLevel);
            Assert.Equal(cache.Metadata.EndLevel, deserialized1.Metadata.EndLevel);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CacheSerializer_MultipleSaveAndLoad_ProducesIdenticalCache()
    {
        // Arrange
        var leafData = CreateLeafData("data1", "data2", "data3", "data4", "data5", "data6");
        var stream = new MerkleTreeStreamClass();
        var tempFile1 = Path.Combine(Path.GetTempPath(), $"determinism_test1_{Guid.NewGuid():N}.cache");
        var tempFile2 = Path.Combine(Path.GetTempPath(), $"determinism_test2_{Guid.NewGuid():N}.cache");
        
        try
        {
            async IAsyncEnumerable<byte[]> GetAsyncLeaves()
            {
                foreach (var leaf in leafData)
                {
                    await Task.Yield();
                    yield return leaf;
                }
            }

            // Build with cache
            var cacheConfig1 = new CacheConfiguration(tempFile1, topLevelsToCache: 3);
            var metadata1 = await stream.BuildAsync(GetAsyncLeaves(), cacheConfig1);
            
            // Load and save to different file
            var cache1 = CacheFileManager.LoadCache(tempFile1);
            var serialized = CacheSerializer.Serialize(cache1);
            await File.WriteAllBytesAsync(tempFile2, serialized);
            
            // Load again
            var cache2 = CacheFileManager.LoadCache(tempFile2);

            // Act - Serialize both
            var serialized1 = CacheSerializer.Serialize(cache1);
            var serialized2 = CacheSerializer.Serialize(cache2);

            // Assert - Serializations should be identical
            Assert.Equal(serialized1, serialized2);
        }
        finally
        {
            if (File.Exists(tempFile1))
                File.Delete(tempFile1);
            if (File.Exists(tempFile2))
                File.Delete(tempFile2);
        }
    }

    [Fact]
    public void AllHashFunctions_ProduceDeterministicResults()
    {
        // Arrange
        var leafData = CreateLeafData("data1", "data2", "data3");
        var hashFunctions = new IHashFunction[]
        {
            new Sha256HashFunction(),
            new Sha512HashFunction(),
            new Blake3HashFunction()
        };

        foreach (var hashFunction in hashFunctions)
        {
            // Act - Build tree multiple times with same hash function
            var tree1 = new MerkleTreeClass(leafData, hashFunction);
            var tree2 = new MerkleTreeClass(leafData, hashFunction);

            var root1 = tree1.GetRootHash();
            var root2 = tree2.GetRootHash();

            // Assert - Roots should be identical
            Assert.Equal(root1, root2);
        }
    }

    [Fact]
    public void MerkleTreeNode_Serialization_IsDeterministic()
    {
        // Arrange
        var leafData = CreateLeafData("data1", "data2", "data3");
        var tree = new MerkleTreeClass(leafData);

        // Act - Serialize root multiple times
        var serialized1 = tree.Root.Serialize();
        var serialized2 = tree.Root.Serialize();
        var serialized3 = tree.Root.Serialize();

        // Assert - All should be identical
        Assert.Equal(serialized1, serialized2);
        Assert.Equal(serialized2, serialized3);
    }

    [Fact]
    public void MerkleTreeMetadata_SerializeRoot_IsDeterministic()
    {
        // Arrange
        var leafData = CreateLeafData("data1", "data2", "data3", "data4");
        var tree = new MerkleTreeClass(leafData);
        var metadata = tree.GetMetadata();

        // Act - Serialize root multiple times
        var serialized1 = metadata.SerializeRoot();
        var serialized2 = metadata.SerializeRoot();
        var serialized3 = metadata.SerializeRoot();

        // Assert - All should be identical
        Assert.Equal(serialized1, serialized2);
        Assert.Equal(serialized2, serialized3);
    }

    [Fact]
    public void NonPowerOfTwoTree_IsDeterministic()
    {
        // Arrange - Test various non-power-of-two leaf counts
        var leafCounts = new[] { 3, 5, 6, 7, 9, 10, 11, 13, 14, 15 };

        foreach (var count in leafCounts)
        {
            var leafData = Enumerable.Range(0, count)
                .Select(i => Encoding.UTF8.GetBytes($"data{i}"))
                .ToList();

            // Act - Build tree multiple times
            var tree1 = new MerkleTreeClass(leafData);
            var tree2 = new MerkleTreeClass(leafData);

            // Assert - Roots should be identical
            Assert.Equal(tree1.GetRootHash(), tree2.GetRootHash());
        }
    }

    [Fact]
    public async Task StreamingTree_AsyncBuild_IsDeterministic()
    {
        // Arrange
        var leafData = CreateLeafData("data1", "data2", "data3", "data4", "data5");

        async IAsyncEnumerable<byte[]> GetAsyncLeaves()
        {
            foreach (var leaf in leafData)
            {
                await Task.Yield();
                yield return leaf;
            }
        }

        // Act - Build tree multiple times asynchronously
        var stream1 = new MerkleTreeStreamClass();
        var stream2 = new MerkleTreeStreamClass();

        var metadata1 = await stream1.BuildAsync(GetAsyncLeaves());
        var metadata2 = await stream2.BuildAsync(GetAsyncLeaves());

        // Assert - Roots should be identical
        Assert.Equal(metadata1.RootHash, metadata2.RootHash);
    }

    [Fact]
    public void LittleEndianEncoding_IsConsistentAcrossRuns()
    {
        // Arrange - Use the same proof across multiple serializations
        var leafData = CreateLeafData("data1", "data2", "data3", "data4");
        var tree = new MerkleTreeClass(leafData);
        var proof = tree.GenerateProof(2);

        // Act - Serialize proof 10 times
        var serializations = new byte[10][];
        for (int i = 0; i < 10; i++)
        {
            serializations[i] = proof.Serialize();
        }

        // Assert - All serializations should be byte-for-byte identical
        for (int i = 1; i < serializations.Length; i++)
        {
            Assert.Equal(serializations[0], serializations[i]);
        }
    }

    [Fact]
    public void TreeWithLargeLeafCount_IsDeterministic()
    {
        // Arrange - Create a larger tree
        var leafData = Enumerable.Range(0, 1000)
            .Select(i => Encoding.UTF8.GetBytes($"data{i}"))
            .ToList();

        // Act - Build tree twice
        var tree1 = new MerkleTreeClass(leafData);
        var tree2 = new MerkleTreeClass(leafData);

        // Assert
        Assert.Equal(tree1.GetRootHash(), tree2.GetRootHash());
        Assert.Equal(tree1.GetMetadata().Height, tree2.GetMetadata().Height);
    }

    [Fact]
    public void ProofVerification_WithSerializedProof_IsConsistent()
    {
        // Arrange
        var leafData = CreateLeafData("data1", "data2", "data3", "data4");
        var tree = new MerkleTreeClass(leafData);
        var originalProof = tree.GenerateProof(1);
        var rootHash = tree.GetRootHash();
        var hashFunction = new Sha256HashFunction();

        // Act - Serialize, deserialize, and verify multiple times
        var serialized = originalProof.Serialize();
        
        for (int i = 0; i < 5; i++)
        {
            var deserializedProof = MerkleProof.Deserialize(serialized);
            bool isValid = deserializedProof.Verify(rootHash, hashFunction);
            
            // Assert - Proof should always be valid
            Assert.True(isValid);
        }
    }
}
