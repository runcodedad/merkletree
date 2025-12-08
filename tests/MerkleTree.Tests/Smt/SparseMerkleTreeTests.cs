using System;
using System.Linq;
using System.Text;
using Xunit;
using MerkleTree.Hashing;
using MerkleTree.Smt;

namespace MerkleTree.Tests.Smt;

/// <summary>
/// Tests for the SparseMerkleTree class to verify core model functionality,
/// key-to-path mapping, and determinism.
/// </summary>
public class SparseMerkleTreeTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithHashFunction_CreatesTreeWithDefaultDepth()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();

        // Act
        var smt = new SparseMerkleTree(hashFunction);

        // Assert
        Assert.NotNull(smt);
        Assert.Equal(256, smt.Depth); // SHA-256 = 32 bytes * 8 bits
        Assert.Equal(hashFunction.Name, smt.HashAlgorithmId);
        Assert.NotNull(smt.Metadata);
        Assert.NotNull(smt.ZeroHashes);
    }

    [Fact]
    public void Constructor_WithHashFunctionAndDepth_CreatesTreeWithCustomDepth()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        int customDepth = 16;

        // Act
        var smt = new SparseMerkleTree(hashFunction, customDepth);

        // Assert
        Assert.NotNull(smt);
        Assert.Equal(customDepth, smt.Depth);
        Assert.Equal(hashFunction.Name, smt.HashAlgorithmId);
    }

    [Fact]
    public void Constructor_WithMetadata_CreatesTreeFromExistingMetadata()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var metadata = SmtMetadata.Create(hashFunction, 8);

        // Act
        var smt = new SparseMerkleTree(hashFunction, metadata);

        // Assert
        Assert.NotNull(smt);
        Assert.Equal(metadata.TreeDepth, smt.Depth);
        Assert.Equal(metadata, smt.Metadata);
    }

    [Fact]
    public void Constructor_NullHashFunction_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SparseMerkleTree(null!));
        Assert.Throws<ArgumentNullException>(() => new SparseMerkleTree(null!, 8));
    }

    [Fact]
    public void Constructor_NullMetadata_ThrowsArgumentNullException()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SparseMerkleTree(hashFunction, null!));
    }

    [Fact]
    public void Constructor_InvalidDepth_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SparseMerkleTree(hashFunction, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SparseMerkleTree(hashFunction, -1));
    }

    [Fact]
    public void Constructor_DepthExceedsHashSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction(); // 256 bits

        // Act & Assert - Try to create tree with depth > 256
        Assert.Throws<ArgumentOutOfRangeException>(() => new SparseMerkleTree(hashFunction, 257));
    }

    [Fact]
    public void Constructor_MismatchedHashFunction_ThrowsArgumentException()
    {
        // Arrange
        var sha256 = new Sha256HashFunction();
        var sha512 = new Sha512HashFunction();
        var metadata = SmtMetadata.Create(sha256, 8);

        // Act & Assert - Try to use SHA-512 with SHA-256 metadata
        Assert.Throws<ArgumentException>(() => new SparseMerkleTree(sha512, metadata));
    }

    [Fact]
    public void Constructor_DifferentHashFunctions_CreateTreesWithDifferentDepths()
    {
        // Arrange & Act
        var sha256Tree = new SparseMerkleTree(new Sha256HashFunction());
        var sha512Tree = new SparseMerkleTree(new Sha512HashFunction());

        // Assert
        Assert.Equal(256, sha256Tree.Depth);
        Assert.Equal(512, sha512Tree.Depth);
    }

    #endregion

    #region GetBitPath Tests

    [Fact]
    public void GetBitPath_WithValidKey_ReturnsCorrectLengthPath()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction, 8);
        var key = Encoding.UTF8.GetBytes("test-key");

        // Act
        var path = smt.GetBitPath(key);

        // Assert
        Assert.NotNull(path);
        Assert.Equal(8, path.Length);
    }

    [Fact]
    public void GetBitPath_WithSameKey_ReturnsSamePath()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction, 16);
        var key = Encoding.UTF8.GetBytes("consistent-key");

        // Act
        var path1 = smt.GetBitPath(key);
        var path2 = smt.GetBitPath(key);

        // Assert
        Assert.Equal(path1, path2);
    }

    [Fact]
    public void GetBitPath_WithDifferentKeys_ReturnsDifferentPaths()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction, 256);
        var key1 = Encoding.UTF8.GetBytes("key-one");
        var key2 = Encoding.UTF8.GetBytes("key-two");

        // Act
        var path1 = smt.GetBitPath(key1);
        var path2 = smt.GetBitPath(key2);

        // Assert
        Assert.NotEqual(path1, path2);
    }

    [Fact]
    public void GetBitPath_NullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var smt = new SparseMerkleTree(new Sha256HashFunction());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => smt.GetBitPath((byte[])null!));
    }

    [Fact]
    public void GetBitPath_EmptyKey_ThrowsArgumentException()
    {
        // Arrange
        var smt = new SparseMerkleTree(new Sha256HashFunction());
        var emptyKey = Array.Empty<byte>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => smt.GetBitPath(emptyKey));
    }

    [Fact]
    public void GetBitPath_WithReadOnlyMemory_ReturnsCorrectPath()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction, 8);
        var key = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("test-key"));

        // Act
        var path = smt.GetBitPath(key);

        // Assert
        Assert.NotNull(path);
        Assert.Equal(8, path.Length);
    }

    [Fact]
    public void GetBitPath_ReadOnlyMemoryVsByteArray_ProducesSamePath()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction, 16);
        var keyBytes = Encoding.UTF8.GetBytes("test-key");
        var keyMemory = new ReadOnlyMemory<byte>(keyBytes);

        // Act
        var pathFromBytes = smt.GetBitPath(keyBytes);
        var pathFromMemory = smt.GetBitPath(keyMemory);

        // Assert
        Assert.Equal(pathFromBytes, pathFromMemory);
    }

    [Fact]
    public void GetBitPath_DeterministicAcrossMultipleCalls()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction, 256);
        var key = Encoding.UTF8.GetBytes("deterministic-test");

        // Act - Call multiple times
        var paths = Enumerable.Range(0, 10)
            .Select(_ => smt.GetBitPath(key))
            .ToList();

        // Assert - All paths should be identical
        var firstPath = paths[0];
        foreach (var path in paths.Skip(1))
        {
            Assert.Equal(firstPath, path);
        }
    }

    #endregion

    #region HashKey Tests

    [Fact]
    public void HashKey_WithValidKey_ReturnsHash()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction);
        var key = Encoding.UTF8.GetBytes("test-key");

        // Act
        var hash = smt.HashKey(key);

        // Assert
        Assert.NotNull(hash);
        Assert.Equal(hashFunction.HashSizeInBytes, hash.Length);
    }

    [Fact]
    public void HashKey_SameKey_ReturnsSameHash()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction);
        var key = Encoding.UTF8.GetBytes("consistent-key");

        // Act
        var hash1 = smt.HashKey(key);
        var hash2 = smt.HashKey(key);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashKey_DifferentKeys_ReturnsDifferentHashes()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction);
        var key1 = Encoding.UTF8.GetBytes("key-one");
        var key2 = Encoding.UTF8.GetBytes("key-two");

        // Act
        var hash1 = smt.HashKey(key1);
        var hash2 = smt.HashKey(key2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void HashKey_NullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var smt = new SparseMerkleTree(new Sha256HashFunction());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => smt.HashKey((byte[])null!));
    }

    [Fact]
    public void HashKey_EmptyKey_ThrowsArgumentException()
    {
        // Arrange
        var smt = new SparseMerkleTree(new Sha256HashFunction());
        var emptyKey = Array.Empty<byte>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => smt.HashKey(emptyKey));
    }

    [Fact]
    public void HashKey_WithReadOnlyMemory_ReturnsCorrectHash()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction);
        var key = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("test-key"));

        // Act
        var hash = smt.HashKey(key);

        // Assert
        Assert.NotNull(hash);
        Assert.Equal(hashFunction.HashSizeInBytes, hash.Length);
    }

    #endregion

    #region CreateEmptyNode Tests

    [Fact]
    public void CreateEmptyNode_WithValidLevel_CreatesNode()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction, 8);

        // Act
        var emptyNode = smt.CreateEmptyNode(0);

        // Assert
        Assert.NotNull(emptyNode);
        Assert.Equal(SmtNodeType.Empty, emptyNode.NodeType);
        Assert.Equal(0, emptyNode.Level);
        Assert.NotEmpty(emptyNode.Hash.ToArray());
    }

    [Fact]
    public void CreateEmptyNode_DifferentLevels_ProduceDifferentHashes()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction, 8);

        // Act
        var node0 = smt.CreateEmptyNode(0);
        var node1 = smt.CreateEmptyNode(1);

        // Assert
        Assert.NotEqual(node0.Hash.ToArray(), node1.Hash.ToArray());
    }

    [Fact]
    public void CreateEmptyNode_SameLevel_ProducesSameHash()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction, 8);

        // Act
        var node1 = smt.CreateEmptyNode(3);
        var node2 = smt.CreateEmptyNode(3);

        // Assert
        Assert.Equal(node1.Hash.ToArray(), node2.Hash.ToArray());
    }

    [Fact]
    public void CreateEmptyNode_NegativeLevel_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var smt = new SparseMerkleTree(new Sha256HashFunction(), 8);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => smt.CreateEmptyNode(-1));
    }

    [Fact]
    public void CreateEmptyNode_LevelExceedsDepth_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var smt = new SparseMerkleTree(new Sha256HashFunction(), 8);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => smt.CreateEmptyNode(9));
    }

    [Fact]
    public void CreateEmptyNode_UsesZeroHashTable()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction, 8);
        int level = 5;

        // Act
        var emptyNode = smt.CreateEmptyNode(level);
        var expectedHash = smt.ZeroHashes[level];

        // Assert
        Assert.Equal(expectedHash, emptyNode.Hash.ToArray());
    }

    #endregion

    #region CreateLeafNode Tests

    [Fact]
    public void CreateLeafNode_WithValidKeyAndValue_CreatesNode()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction);
        var key = Encoding.UTF8.GetBytes("test-key");
        var value = Encoding.UTF8.GetBytes("test-value");

        // Act
        var leafNode = smt.CreateLeafNode(key, value);

        // Assert
        Assert.NotNull(leafNode);
        Assert.Equal(SmtNodeType.Leaf, leafNode.NodeType);
        Assert.NotEmpty(leafNode.Hash.ToArray());
        Assert.NotEmpty(leafNode.KeyHash.ToArray());
        Assert.Equal(value, leafNode.Value.ToArray());
        Assert.Null(leafNode.OriginalKey);
    }

    [Fact]
    public void CreateLeafNode_WithIncludeOriginalKey_StoresOriginalKey()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction);
        var key = Encoding.UTF8.GetBytes("test-key");
        var value = Encoding.UTF8.GetBytes("test-value");

        // Act
        var leafNode = smt.CreateLeafNode(key, value, includeOriginalKey: true);

        // Assert
        Assert.NotNull(leafNode.OriginalKey);
        Assert.Equal(key, leafNode.OriginalKey.Value.ToArray());
    }

    [Fact]
    public void CreateLeafNode_SameKeyAndValue_ProducesSameHash()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction);
        var key = Encoding.UTF8.GetBytes("test-key");
        var value = Encoding.UTF8.GetBytes("test-value");

        // Act
        var node1 = smt.CreateLeafNode(key, value);
        var node2 = smt.CreateLeafNode(key, value);

        // Assert
        Assert.Equal(node1.Hash.ToArray(), node2.Hash.ToArray());
    }

    [Fact]
    public void CreateLeafNode_DifferentKeys_ProducesDifferentHashes()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction);
        var key1 = Encoding.UTF8.GetBytes("key-one");
        var key2 = Encoding.UTF8.GetBytes("key-two");
        var value = Encoding.UTF8.GetBytes("same-value");

        // Act
        var node1 = smt.CreateLeafNode(key1, value);
        var node2 = smt.CreateLeafNode(key2, value);

        // Assert
        Assert.NotEqual(node1.Hash.ToArray(), node2.Hash.ToArray());
    }

    [Fact]
    public void CreateLeafNode_DifferentValues_ProducesDifferentHashes()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction);
        var key = Encoding.UTF8.GetBytes("same-key");
        var value1 = Encoding.UTF8.GetBytes("value-one");
        var value2 = Encoding.UTF8.GetBytes("value-two");

        // Act
        var node1 = smt.CreateLeafNode(key, value1);
        var node2 = smt.CreateLeafNode(key, value2);

        // Assert
        Assert.NotEqual(node1.Hash.ToArray(), node2.Hash.ToArray());
    }

    [Fact]
    public void CreateLeafNode_NullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var smt = new SparseMerkleTree(new Sha256HashFunction());
        var value = Encoding.UTF8.GetBytes("test-value");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => smt.CreateLeafNode(null!, value));
    }

    [Fact]
    public void CreateLeafNode_NullValue_ThrowsArgumentNullException()
    {
        // Arrange
        var smt = new SparseMerkleTree(new Sha256HashFunction());
        var key = Encoding.UTF8.GetBytes("test-key");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => smt.CreateLeafNode(key, null!));
    }

    [Fact]
    public void CreateLeafNode_EmptyKey_ThrowsArgumentException()
    {
        // Arrange
        var smt = new SparseMerkleTree(new Sha256HashFunction());
        var value = Encoding.UTF8.GetBytes("test-value");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => smt.CreateLeafNode(Array.Empty<byte>(), value));
    }

    [Fact]
    public void CreateLeafNode_EmptyValue_ThrowsArgumentException()
    {
        // Arrange
        var smt = new SparseMerkleTree(new Sha256HashFunction());
        var key = Encoding.UTF8.GetBytes("test-key");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => smt.CreateLeafNode(key, Array.Empty<byte>()));
    }

    #endregion

    #region CreateInternalNode Tests

    [Fact]
    public void CreateInternalNode_WithValidHashes_CreatesNode()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction);
        var leftHash = new byte[32];
        var rightHash = new byte[32];
        Array.Fill(leftHash, (byte)1);
        Array.Fill(rightHash, (byte)2);

        // Act
        var internalNode = smt.CreateInternalNode(leftHash, rightHash);

        // Assert
        Assert.NotNull(internalNode);
        Assert.Equal(SmtNodeType.Internal, internalNode.NodeType);
        Assert.NotEmpty(internalNode.Hash.ToArray());
        Assert.Equal(leftHash, internalNode.LeftHash.ToArray());
        Assert.Equal(rightHash, internalNode.RightHash.ToArray());
    }

    [Fact]
    public void CreateInternalNode_SameHashes_ProducesSameNodeHash()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction);
        var leftHash = new byte[32];
        var rightHash = new byte[32];
        Array.Fill(leftHash, (byte)1);
        Array.Fill(rightHash, (byte)2);

        // Act
        var node1 = smt.CreateInternalNode(leftHash, rightHash);
        var node2 = smt.CreateInternalNode(leftHash, rightHash);

        // Assert
        Assert.Equal(node1.Hash.ToArray(), node2.Hash.ToArray());
    }

    [Fact]
    public void CreateInternalNode_DifferentLeftHash_ProducesDifferentNodeHash()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction);
        var leftHash1 = new byte[32];
        var leftHash2 = new byte[32];
        var rightHash = new byte[32];
        Array.Fill(leftHash1, (byte)1);
        Array.Fill(leftHash2, (byte)3);
        Array.Fill(rightHash, (byte)2);

        // Act
        var node1 = smt.CreateInternalNode(leftHash1, rightHash);
        var node2 = smt.CreateInternalNode(leftHash2, rightHash);

        // Assert
        Assert.NotEqual(node1.Hash.ToArray(), node2.Hash.ToArray());
    }

    [Fact]
    public void CreateInternalNode_DifferentRightHash_ProducesDifferentNodeHash()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction);
        var leftHash = new byte[32];
        var rightHash1 = new byte[32];
        var rightHash2 = new byte[32];
        Array.Fill(leftHash, (byte)1);
        Array.Fill(rightHash1, (byte)2);
        Array.Fill(rightHash2, (byte)4);

        // Act
        var node1 = smt.CreateInternalNode(leftHash, rightHash1);
        var node2 = smt.CreateInternalNode(leftHash, rightHash2);

        // Assert
        Assert.NotEqual(node1.Hash.ToArray(), node2.Hash.ToArray());
    }

    [Fact]
    public void CreateInternalNode_NullLeftHash_ThrowsArgumentNullException()
    {
        // Arrange
        var smt = new SparseMerkleTree(new Sha256HashFunction());
        var rightHash = new byte[32];

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => smt.CreateInternalNode(null!, rightHash));
    }

    [Fact]
    public void CreateInternalNode_NullRightHash_ThrowsArgumentNullException()
    {
        // Arrange
        var smt = new SparseMerkleTree(new Sha256HashFunction());
        var leftHash = new byte[32];

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => smt.CreateInternalNode(leftHash, null!));
    }

    [Fact]
    public void CreateInternalNode_EmptyLeftHash_ThrowsArgumentException()
    {
        // Arrange
        var smt = new SparseMerkleTree(new Sha256HashFunction());
        var rightHash = new byte[32];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => smt.CreateInternalNode(Array.Empty<byte>(), rightHash));
    }

    [Fact]
    public void CreateInternalNode_EmptyRightHash_ThrowsArgumentException()
    {
        // Arrange
        var smt = new SparseMerkleTree(new Sha256HashFunction());
        var leftHash = new byte[32];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => smt.CreateInternalNode(leftHash, Array.Empty<byte>()));
    }

    #endregion

    #region Cross-Platform Determinism Tests

    [Fact]
    public void GetBitPath_SameKeyOnDifferentInstances_ProducesSamePath()
    {
        // Arrange - Simulate different machines/processes
        var hashFunction1 = new Sha256HashFunction();
        var hashFunction2 = new Sha256HashFunction();
        var smt1 = new SparseMerkleTree(hashFunction1, 256);
        var smt2 = new SparseMerkleTree(hashFunction2, 256);
        var key = Encoding.UTF8.GetBytes("cross-platform-test");

        // Act
        var path1 = smt1.GetBitPath(key);
        var path2 = smt2.GetBitPath(key);

        // Assert - Should be identical across "machines"
        Assert.Equal(path1, path2);
    }

    [Fact]
    public void CreateLeafNode_SameInputsOnDifferentInstances_ProducesSameHash()
    {
        // Arrange - Simulate different machines/processes
        var hashFunction1 = new Sha256HashFunction();
        var hashFunction2 = new Sha256HashFunction();
        var smt1 = new SparseMerkleTree(hashFunction1);
        var smt2 = new SparseMerkleTree(hashFunction2);
        var key = Encoding.UTF8.GetBytes("deterministic-key");
        var value = Encoding.UTF8.GetBytes("deterministic-value");

        // Act
        var node1 = smt1.CreateLeafNode(key, value);
        var node2 = smt2.CreateLeafNode(key, value);

        // Assert - Should produce identical hashes
        Assert.Equal(node1.Hash.ToArray(), node2.Hash.ToArray());
        Assert.Equal(node1.KeyHash.ToArray(), node2.KeyHash.ToArray());
    }

    [Fact]
    public void CreateEmptyNode_SameLevelOnDifferentInstances_ProducesSameHash()
    {
        // Arrange - Simulate different machines/processes
        var hashFunction1 = new Sha256HashFunction();
        var hashFunction2 = new Sha256HashFunction();
        var smt1 = new SparseMerkleTree(hashFunction1, 16);
        var smt2 = new SparseMerkleTree(hashFunction2, 16);
        int level = 5;

        // Act
        var node1 = smt1.CreateEmptyNode(level);
        var node2 = smt2.CreateEmptyNode(level);

        // Assert - Should produce identical zero-hashes
        Assert.Equal(node1.Hash.ToArray(), node2.Hash.ToArray());
    }

    [Fact]
    public void Metadata_SameConfiguration_ProducesSameZeroHashes()
    {
        // Arrange - Simulate different machines/processes
        var smt1 = new SparseMerkleTree(new Sha256HashFunction(), 8);
        var smt2 = new SparseMerkleTree(new Sha256HashFunction(), 8);

        // Act & Assert - Compare all zero-hashes
        for (int level = 0; level <= 8; level++)
        {
            var hash1 = smt1.ZeroHashes[level];
            var hash2 = smt2.ZeroHashes[level];
            Assert.Equal(hash1, hash2);
        }
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void FullWorkflow_CreateTreeAndNodes_WorksCorrectly()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction, 8);

        // Act - Create various nodes
        var key1 = Encoding.UTF8.GetBytes("user-1");
        var key2 = Encoding.UTF8.GetBytes("user-2");
        var value1 = Encoding.UTF8.GetBytes("data-1");
        var value2 = Encoding.UTF8.GetBytes("data-2");

        var path1 = smt.GetBitPath(key1);
        var path2 = smt.GetBitPath(key2);
        var leaf1 = smt.CreateLeafNode(key1, value1);
        var leaf2 = smt.CreateLeafNode(key2, value2);
        var emptyNode = smt.CreateEmptyNode(0);
        var internalNode = smt.CreateInternalNode(leaf1.Hash.ToArray(), leaf2.Hash.ToArray());

        // Assert - All operations should succeed
        Assert.Equal(8, path1.Length);
        Assert.Equal(8, path2.Length);
        Assert.NotNull(leaf1);
        Assert.NotNull(leaf2);
        Assert.NotNull(emptyNode);
        Assert.NotNull(internalNode);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    public void Constructor_VariousDepths_CreatesValidTrees(int depth)
    {
        // Arrange & Act
        var hashFunction = new Sha256HashFunction();
        var smt = new SparseMerkleTree(hashFunction, depth);

        // Assert
        Assert.Equal(depth, smt.Depth);
        Assert.Equal(depth, smt.ZeroHashes.Depth);
        Assert.NotNull(smt.Metadata);
    }

    #endregion
}
