using System;
using System.Text;
using Xunit;
using MerkleTree.Smt;

namespace MerkleTree.Tests.Smt;

/// <summary>
/// Tests for SMT node types (SmtEmptyNode, SmtLeafNode, SmtInternalNode).
/// </summary>
public class SmtNodeTests
{
    #region SmtEmptyNode Tests

    [Fact]
    public void SmtEmptyNode_WithValidParameters_CreatesNode()
    {
        // Arrange
        int level = 5;
        var zeroHash = new byte[32];
        Array.Fill(zeroHash, (byte)0xFF);

        // Act
        var node = new SmtEmptyNode(level, zeroHash);

        // Assert
        Assert.NotNull(node);
        Assert.Equal(SmtNodeType.Empty, node.NodeType);
        Assert.Equal(level, node.Level);
        Assert.Equal(zeroHash, node.Hash.ToArray());
    }

    [Fact]
    public void SmtEmptyNode_NegativeLevel_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var zeroHash = new byte[32];

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SmtEmptyNode(-1, zeroHash));
    }

    [Fact]
    public void SmtEmptyNode_EmptyHash_ThrowsArgumentException()
    {
        // Arrange
        var emptyHash = Array.Empty<byte>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SmtEmptyNode(0, emptyHash));
    }

    [Fact]
    public void SmtEmptyNode_LevelZero_IsValid()
    {
        // Arrange
        var zeroHash = new byte[32];

        // Act
        var node = new SmtEmptyNode(0, zeroHash);

        // Assert
        Assert.Equal(0, node.Level);
    }

    [Fact]
    public void SmtEmptyNode_HighLevel_IsValid()
    {
        // Arrange
        var zeroHash = new byte[32];
        int highLevel = 256;

        // Act
        var node = new SmtEmptyNode(highLevel, zeroHash);

        // Assert
        Assert.Equal(highLevel, node.Level);
    }

    #endregion

    #region SmtLeafNode Tests

    [Fact]
    public void SmtLeafNode_WithValidParameters_CreatesNode()
    {
        // Arrange
        var keyHash = new byte[32];
        var value = Encoding.UTF8.GetBytes("test-value");
        var nodeHash = new byte[32];
        Array.Fill(keyHash, (byte)1);
        Array.Fill(nodeHash, (byte)2);

        // Act
        var node = new SmtLeafNode(keyHash, value, nodeHash);

        // Assert
        Assert.NotNull(node);
        Assert.Equal(SmtNodeType.Leaf, node.NodeType);
        Assert.Equal(keyHash, node.KeyHash.ToArray());
        Assert.Equal(value, node.Value.ToArray());
        Assert.Equal(nodeHash, node.Hash.ToArray());
        Assert.Null(node.OriginalKey);
    }

    [Fact]
    public void SmtLeafNode_WithOriginalKey_StoresKey()
    {
        // Arrange
        var keyHash = new byte[32];
        var value = Encoding.UTF8.GetBytes("test-value");
        var nodeHash = new byte[32];
        var originalKey = Encoding.UTF8.GetBytes("original-key");

        // Act
        var node = new SmtLeafNode(keyHash, value, nodeHash, originalKey);

        // Assert
        Assert.NotNull(node.OriginalKey);
        Assert.Equal(originalKey, node.OriginalKey.Value.ToArray());
    }

    [Fact]
    public void SmtLeafNode_EmptyKeyHash_ThrowsArgumentException()
    {
        // Arrange
        var emptyKeyHash = Array.Empty<byte>();
        var value = Encoding.UTF8.GetBytes("test-value");
        var nodeHash = new byte[32];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SmtLeafNode(emptyKeyHash, value, nodeHash));
    }

    [Fact]
    public void SmtLeafNode_EmptyValue_ThrowsArgumentException()
    {
        // Arrange
        var keyHash = new byte[32];
        var emptyValue = Array.Empty<byte>();
        var nodeHash = new byte[32];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SmtLeafNode(keyHash, emptyValue, nodeHash));
    }

    [Fact]
    public void SmtLeafNode_EmptyNodeHash_ThrowsArgumentException()
    {
        // Arrange
        var keyHash = new byte[32];
        var value = Encoding.UTF8.GetBytes("test-value");
        var emptyNodeHash = Array.Empty<byte>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SmtLeafNode(keyHash, value, emptyNodeHash));
    }

    [Fact]
    public void SmtLeafNode_WithoutOriginalKey_OriginalKeyIsNull()
    {
        // Arrange
        var keyHash = new byte[32];
        var value = Encoding.UTF8.GetBytes("test-value");
        var nodeHash = new byte[32];

        // Act
        var node = new SmtLeafNode(keyHash, value, nodeHash);

        // Assert
        Assert.Null(node.OriginalKey);
    }

    [Fact]
    public void SmtLeafNode_LargeValue_IsValid()
    {
        // Arrange
        var keyHash = new byte[32];
        var largeValue = new byte[10000]; // 10KB value
        var nodeHash = new byte[32];
        Array.Fill(largeValue, (byte)0xAA);

        // Act
        var node = new SmtLeafNode(keyHash, largeValue, nodeHash);

        // Assert
        Assert.Equal(10000, node.Value.Length);
        Assert.Equal(largeValue, node.Value.ToArray());
    }

    #endregion

    #region SmtInternalNode Tests

    [Fact]
    public void SmtInternalNode_WithValidParameters_CreatesNode()
    {
        // Arrange
        var leftHash = new byte[32];
        var rightHash = new byte[32];
        var nodeHash = new byte[32];
        Array.Fill(leftHash, (byte)1);
        Array.Fill(rightHash, (byte)2);
        Array.Fill(nodeHash, (byte)3);

        // Act
        var node = new SmtInternalNode(leftHash, rightHash, nodeHash);

        // Assert
        Assert.NotNull(node);
        Assert.Equal(SmtNodeType.Internal, node.NodeType);
        Assert.Equal(leftHash, node.LeftHash.ToArray());
        Assert.Equal(rightHash, node.RightHash.ToArray());
        Assert.Equal(nodeHash, node.Hash.ToArray());
    }

    [Fact]
    public void SmtInternalNode_EmptyLeftHash_ThrowsArgumentException()
    {
        // Arrange
        var emptyLeftHash = Array.Empty<byte>();
        var rightHash = new byte[32];
        var nodeHash = new byte[32];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SmtInternalNode(emptyLeftHash, rightHash, nodeHash));
    }

    [Fact]
    public void SmtInternalNode_EmptyRightHash_ThrowsArgumentException()
    {
        // Arrange
        var leftHash = new byte[32];
        var emptyRightHash = Array.Empty<byte>();
        var nodeHash = new byte[32];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SmtInternalNode(leftHash, emptyRightHash, nodeHash));
    }

    [Fact]
    public void SmtInternalNode_EmptyNodeHash_ThrowsArgumentException()
    {
        // Arrange
        var leftHash = new byte[32];
        var rightHash = new byte[32];
        var emptyNodeHash = Array.Empty<byte>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SmtInternalNode(leftHash, rightHash, emptyNodeHash));
    }

    [Fact]
    public void SmtInternalNode_SameChildHashes_IsValid()
    {
        // Arrange
        var childHash = new byte[32];
        var nodeHash = new byte[32];
        Array.Fill(childHash, (byte)0xAA);

        // Act
        var node = new SmtInternalNode(childHash, childHash, nodeHash);

        // Assert
        Assert.Equal(childHash, node.LeftHash.ToArray());
        Assert.Equal(childHash, node.RightHash.ToArray());
    }

    [Fact]
    public void SmtInternalNode_DifferentHashSizes_IsValid()
    {
        // Arrange - SHA-512 produces 64-byte hashes
        var leftHash = new byte[64];
        var rightHash = new byte[64];
        var nodeHash = new byte[64];
        Array.Fill(leftHash, (byte)1);
        Array.Fill(rightHash, (byte)2);
        Array.Fill(nodeHash, (byte)3);

        // Act
        var node = new SmtInternalNode(leftHash, rightHash, nodeHash);

        // Assert
        Assert.Equal(64, node.LeftHash.Length);
        Assert.Equal(64, node.RightHash.Length);
        Assert.Equal(64, node.Hash.Length);
    }

    #endregion

    #region Node Type Tests

    [Fact]
    public void NodeType_EmptyNode_ReturnsEmpty()
    {
        // Arrange
        var node = new SmtEmptyNode(0, new byte[32]);

        // Act
        var nodeType = node.NodeType;

        // Assert
        Assert.Equal(SmtNodeType.Empty, nodeType);
    }

    [Fact]
    public void NodeType_LeafNode_ReturnsLeaf()
    {
        // Arrange
        var keyHash = new byte[32];
        var value = Encoding.UTF8.GetBytes("value");
        var nodeHash = new byte[32];
        var node = new SmtLeafNode(keyHash, value, nodeHash);

        // Act
        var nodeType = node.NodeType;

        // Assert
        Assert.Equal(SmtNodeType.Leaf, nodeType);
    }

    [Fact]
    public void NodeType_InternalNode_ReturnsInternal()
    {
        // Arrange
        var leftHash = new byte[32];
        var rightHash = new byte[32];
        var nodeHash = new byte[32];
        var node = new SmtInternalNode(leftHash, rightHash, nodeHash);

        // Act
        var nodeType = node.NodeType;

        // Assert
        Assert.Equal(SmtNodeType.Internal, nodeType);
    }

    #endregion

    #region Memory Semantics Tests

    [Fact]
    public void SmtLeafNode_KeyHashProperty_ReturnsReadOnlyMemory()
    {
        // Arrange
        var keyHash = new byte[32];
        Array.Fill(keyHash, (byte)1);
        var value = Encoding.UTF8.GetBytes("value");
        var nodeHash = new byte[32];
        var node = new SmtLeafNode(keyHash, value, nodeHash);

        // Act
        var retrievedKeyHash = node.KeyHash;

        // Assert - ReadOnlyMemory wraps the data
        Assert.Equal(32, retrievedKeyHash.Length);
        Assert.All(retrievedKeyHash.ToArray(), b => Assert.Equal(1, b));
    }

    [Fact]
    public void SmtLeafNode_ValueProperty_ReturnsReadOnlyMemory()
    {
        // Arrange
        var keyHash = new byte[32];
        var value = Encoding.UTF8.GetBytes("original");
        var nodeHash = new byte[32];
        var node = new SmtLeafNode(keyHash, value, nodeHash);

        // Act
        var retrievedValue = node.Value;

        // Assert - ReadOnlyMemory wraps the data
        Assert.Equal(value.Length, retrievedValue.Length);
        Assert.Equal(value, retrievedValue.ToArray());
    }

    [Fact]
    public void SmtInternalNode_HashProperties_ReturnReadOnlyMemory()
    {
        // Arrange
        var leftHash = new byte[32];
        var rightHash = new byte[32];
        Array.Fill(leftHash, (byte)1);
        Array.Fill(rightHash, (byte)2);
        var nodeHash = new byte[32];
        var node = new SmtInternalNode(leftHash, rightHash, nodeHash);

        // Act
        var retrievedLeftHash = node.LeftHash;
        var retrievedRightHash = node.RightHash;

        // Assert - ReadOnlyMemory wraps the data
        Assert.Equal(32, retrievedLeftHash.Length);
        Assert.Equal(32, retrievedRightHash.Length);
        Assert.All(retrievedLeftHash.ToArray(), b => Assert.Equal(1, b));
        Assert.All(retrievedRightHash.ToArray(), b => Assert.Equal(2, b));
    }

    #endregion
}
