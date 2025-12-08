using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using MerkleTree.Hashing;
using MerkleTree.Smt;
using MerkleTree.Smt.Persistence;

namespace MerkleTree.Tests.Smt.Persistence;

/// <summary>
/// Tests for the InMemorySmtStorage reference implementation.
/// </summary>
/// <remarks>
/// These tests validate the reference adapter and demonstrate how persistence
/// interfaces should behave.
/// </remarks>
public class InMemorySmtStorageTests
{
    private readonly InMemorySmtStorage _storage;
    private readonly IHashFunction _hashFunction;

    public InMemorySmtStorageTests()
    {
        _storage = new InMemorySmtStorage();
        _hashFunction = new Sha256HashFunction();
    }

    #region Node Reader Tests

    [Fact]
    public async Task ReadNodeByHashAsync_NonExistentNode_ReturnsNull()
    {
        // Arrange
        var hash = _hashFunction.ComputeHash(Encoding.UTF8.GetBytes("test"));

        // Act
        var result = await _storage.ReadNodeByHashAsync(new ReadOnlyMemory<byte>(hash));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ReadNodeByHashAsync_EmptyHash_ThrowsArgumentException()
    {
        // Arrange
        var emptyHash = ReadOnlyMemory<byte>.Empty;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _storage.ReadNodeByHashAsync(emptyHash));
    }

    [Fact]
    public async Task ReadNodeByHashAsync_ExistingNode_ReturnsNode()
    {
        // Arrange
        var nodeData = Encoding.UTF8.GetBytes("test-node-data");
        var hash = _hashFunction.ComputeHash(nodeData);
        var blob = SmtNodeBlob.Create(new ReadOnlyMemory<byte>(hash), new ReadOnlyMemory<byte>(nodeData));
        await _storage.WriteNodeAsync(blob);

        // Act
        var result = await _storage.ReadNodeByHashAsync(new ReadOnlyMemory<byte>(hash));

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Hash.Span.SequenceEqual(hash));
        Assert.True(result.SerializedNode.Span.SequenceEqual(nodeData));
    }

    [Fact]
    public async Task ReadNodeByPathAsync_NonExistentNode_ReturnsNull()
    {
        // Arrange
        var path = new bool[] { true, false, true };

        // Act
        var result = await _storage.ReadNodeByPathAsync(new ReadOnlyMemory<bool>(path));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ReadNodeByPathAsync_EmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var emptyPath = ReadOnlyMemory<bool>.Empty;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _storage.ReadNodeByPathAsync(emptyPath));
    }

    [Fact]
    public async Task ReadNodeByPathAsync_ExistingNode_ReturnsNode()
    {
        // Arrange
        var nodeData = Encoding.UTF8.GetBytes("test-node-data");
        var hash = _hashFunction.ComputeHash(nodeData);
        var path = new bool[] { true, false, true };
        var blob = SmtNodeBlob.CreateWithPath(
            new ReadOnlyMemory<byte>(hash),
            new ReadOnlyMemory<byte>(nodeData),
            new ReadOnlyMemory<bool>(path));
        await _storage.WriteNodeAsync(blob);

        // Act
        var result = await _storage.ReadNodeByPathAsync(new ReadOnlyMemory<bool>(path));

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Hash.Span.SequenceEqual(hash));
        Assert.True(result.SerializedNode.Span.SequenceEqual(nodeData));
        Assert.True(result.Path.HasValue);
        Assert.True(result.Path.Value.Span.SequenceEqual(path));
    }

    [Fact]
    public async Task NodeExistsAsync_NonExistentNode_ReturnsFalse()
    {
        // Arrange
        var hash = _hashFunction.ComputeHash(Encoding.UTF8.GetBytes("test"));

        // Act
        var result = await _storage.NodeExistsAsync(new ReadOnlyMemory<byte>(hash));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task NodeExistsAsync_ExistingNode_ReturnsTrue()
    {
        // Arrange
        var nodeData = Encoding.UTF8.GetBytes("test-node-data");
        var hash = _hashFunction.ComputeHash(nodeData);
        var blob = SmtNodeBlob.Create(new ReadOnlyMemory<byte>(hash), new ReadOnlyMemory<byte>(nodeData));
        await _storage.WriteNodeAsync(blob);

        // Act
        var result = await _storage.NodeExistsAsync(new ReadOnlyMemory<byte>(hash));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task NodeExistsAsync_EmptyHash_ThrowsArgumentException()
    {
        // Arrange
        var emptyHash = ReadOnlyMemory<byte>.Empty;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _storage.NodeExistsAsync(emptyHash));
    }

    #endregion

    #region Node Writer Tests

    [Fact]
    public async Task WriteNodeAsync_ValidNode_StoresNode()
    {
        // Arrange
        var nodeData = Encoding.UTF8.GetBytes("test-node-data");
        var hash = _hashFunction.ComputeHash(nodeData);
        var blob = SmtNodeBlob.Create(new ReadOnlyMemory<byte>(hash), new ReadOnlyMemory<byte>(nodeData));

        // Act
        await _storage.WriteNodeAsync(blob);

        // Assert
        var result = await _storage.ReadNodeByHashAsync(new ReadOnlyMemory<byte>(hash));
        Assert.NotNull(result);
        Assert.Equal(1, _storage.NodeCount);
    }

    [Fact]
    public async Task WriteNodeAsync_NullNode_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _storage.WriteNodeAsync(null!));
    }

    [Fact]
    public async Task WriteNodeAsync_DuplicateNode_IsIdempotent()
    {
        // Arrange
        var nodeData = Encoding.UTF8.GetBytes("test-node-data");
        var hash = _hashFunction.ComputeHash(nodeData);
        var blob = SmtNodeBlob.Create(new ReadOnlyMemory<byte>(hash), new ReadOnlyMemory<byte>(nodeData));

        // Act - write same node twice
        await _storage.WriteNodeAsync(blob);
        await _storage.WriteNodeAsync(blob);

        // Assert - should still have only one node
        Assert.Equal(1, _storage.NodeCount);
        var result = await _storage.ReadNodeByHashAsync(new ReadOnlyMemory<byte>(hash));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task WriteBatchAsync_ValidNodes_StoresAllNodes()
    {
        // Arrange
        var nodes = new List<SmtNodeBlob>();
        for (int i = 0; i < 5; i++)
        {
            var nodeData = Encoding.UTF8.GetBytes($"node-{i}");
            var hash = _hashFunction.ComputeHash(nodeData);
            nodes.Add(SmtNodeBlob.Create(new ReadOnlyMemory<byte>(hash), new ReadOnlyMemory<byte>(nodeData)));
        }

        // Act
        await _storage.WriteBatchAsync(nodes);

        // Assert
        Assert.Equal(5, _storage.NodeCount);
        foreach (var node in nodes)
        {
            var result = await _storage.ReadNodeByHashAsync(node.Hash);
            Assert.NotNull(result);
        }
    }

    [Fact]
    public async Task WriteBatchAsync_NullNodes_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _storage.WriteBatchAsync(null!));
    }

    [Fact]
    public async Task WriteBatchAsync_EmptyList_CompletesSuccessfully()
    {
        // Arrange
        var emptyList = new List<SmtNodeBlob>();

        // Act
        await _storage.WriteBatchAsync(emptyList);

        // Assert
        Assert.Equal(0, _storage.NodeCount);
    }

    [Fact]
    public async Task FlushAsync_CompletesSuccessfully()
    {
        // Act & Assert - should complete without error
        await _storage.FlushAsync();
    }

    #endregion

    #region Snapshot Manager Tests

    [Fact]
    public async Task CreateSnapshotAsync_ValidSnapshot_StoresSnapshot()
    {
        // Arrange
        var rootHash = _hashFunction.ComputeHash(Encoding.UTF8.GetBytes("root"));
        var metadata = new Dictionary<string, string> { { "version", "1.0" } };

        // Act
        await _storage.CreateSnapshotAsync(
            "snapshot-1",
            new ReadOnlyMemory<byte>(rootHash),
            metadata);

        // Assert
        Assert.Equal(1, _storage.SnapshotCount);
        var snapshot = await _storage.GetSnapshotAsync("snapshot-1");
        Assert.NotNull(snapshot);
        Assert.Equal("snapshot-1", snapshot.Name);
        Assert.True(snapshot.RootHash.Span.SequenceEqual(rootHash));
        Assert.NotNull(snapshot.Metadata);
        Assert.Equal("1.0", snapshot.Metadata["version"]);
    }

    [Fact]
    public async Task CreateSnapshotAsync_NullName_ThrowsArgumentNullException()
    {
        // Arrange
        var rootHash = _hashFunction.ComputeHash(Encoding.UTF8.GetBytes("root"));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _storage.CreateSnapshotAsync(null!, new ReadOnlyMemory<byte>(rootHash)));
    }

    [Fact]
    public async Task CreateSnapshotAsync_EmptyName_ThrowsArgumentException()
    {
        // Arrange
        var rootHash = _hashFunction.ComputeHash(Encoding.UTF8.GetBytes("root"));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _storage.CreateSnapshotAsync("", new ReadOnlyMemory<byte>(rootHash)));
    }

    [Fact]
    public async Task CreateSnapshotAsync_EmptyRootHash_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _storage.CreateSnapshotAsync("snapshot-1", ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public async Task CreateSnapshotAsync_DuplicateName_IsIdempotent()
    {
        // Arrange
        var rootHash1 = _hashFunction.ComputeHash(Encoding.UTF8.GetBytes("root1"));
        var rootHash2 = _hashFunction.ComputeHash(Encoding.UTF8.GetBytes("root2"));

        // Act - create same snapshot name twice with different roots
        await _storage.CreateSnapshotAsync("snapshot-1", new ReadOnlyMemory<byte>(rootHash1));
        await _storage.CreateSnapshotAsync("snapshot-1", new ReadOnlyMemory<byte>(rootHash2));

        // Assert - should have latest root hash
        Assert.Equal(1, _storage.SnapshotCount);
        var snapshot = await _storage.GetSnapshotAsync("snapshot-1");
        Assert.NotNull(snapshot);
        Assert.True(snapshot.RootHash.Span.SequenceEqual(rootHash2));
    }

    [Fact]
    public async Task GetSnapshotAsync_NonExistentSnapshot_ReturnsNull()
    {
        // Act
        var result = await _storage.GetSnapshotAsync("non-existent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ListSnapshotsAsync_NoSnapshots_ReturnsEmptyList()
    {
        // Act
        var result = await _storage.ListSnapshotsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ListSnapshotsAsync_MultipleSnapshots_ReturnsAllNames()
    {
        // Arrange
        var rootHash1 = _hashFunction.ComputeHash(Encoding.UTF8.GetBytes("root1"));
        var rootHash2 = _hashFunction.ComputeHash(Encoding.UTF8.GetBytes("root2"));
        await _storage.CreateSnapshotAsync("snapshot-1", new ReadOnlyMemory<byte>(rootHash1));
        await _storage.CreateSnapshotAsync("snapshot-2", new ReadOnlyMemory<byte>(rootHash2));

        // Act
        var result = await _storage.ListSnapshotsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("snapshot-1", result);
        Assert.Contains("snapshot-2", result);
    }

    [Fact]
    public async Task DeleteSnapshotAsync_ExistingSnapshot_RemovesSnapshot()
    {
        // Arrange
        var rootHash = _hashFunction.ComputeHash(Encoding.UTF8.GetBytes("root"));
        await _storage.CreateSnapshotAsync("snapshot-1", new ReadOnlyMemory<byte>(rootHash));

        // Act
        await _storage.DeleteSnapshotAsync("snapshot-1");

        // Assert
        Assert.Equal(0, _storage.SnapshotCount);
        var result = await _storage.GetSnapshotAsync("snapshot-1");
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteSnapshotAsync_NonExistentSnapshot_IsIdempotent()
    {
        // Act & Assert - should not throw
        await _storage.DeleteSnapshotAsync("non-existent");
    }

    [Fact]
    public async Task RestoreSnapshotAsync_ExistingSnapshot_ReturnsRootHash()
    {
        // Arrange
        var rootHash = _hashFunction.ComputeHash(Encoding.UTF8.GetBytes("root"));
        await _storage.CreateSnapshotAsync("snapshot-1", new ReadOnlyMemory<byte>(rootHash));

        // Act
        var result = await _storage.RestoreSnapshotAsync("snapshot-1");

        // Assert
        Assert.True(result.Span.SequenceEqual(rootHash));
    }

    [Fact]
    public async Task RestoreSnapshotAsync_NonExistentSnapshot_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _storage.RestoreSnapshotAsync("non-existent"));
    }

    #endregion

    #region Metadata Store Tests

    [Fact]
    public async Task StoreMetadataAsync_ValidMetadata_StoresMetadata()
    {
        // Arrange
        var metadata = SmtMetadata.Create(_hashFunction, 8);

        // Act
        await _storage.StoreMetadataAsync(metadata);

        // Assert
        var result = await _storage.LoadMetadataAsync();
        Assert.NotNull(result);
        Assert.Equal(metadata.HashAlgorithmId, result.HashAlgorithmId);
        Assert.Equal(metadata.TreeDepth, result.TreeDepth);
    }

    [Fact]
    public async Task StoreMetadataAsync_NullMetadata_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _storage.StoreMetadataAsync(null!));
    }

    [Fact]
    public async Task LoadMetadataAsync_NoMetadata_ReturnsNull()
    {
        // Act
        var result = await _storage.LoadMetadataAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task MetadataExistsAsync_NoMetadata_ReturnsFalse()
    {
        // Act
        var result = await _storage.MetadataExistsAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task MetadataExistsAsync_MetadataExists_ReturnsTrue()
    {
        // Arrange
        var metadata = SmtMetadata.Create(_hashFunction, 8);
        await _storage.StoreMetadataAsync(metadata);

        // Act
        var result = await _storage.MetadataExistsAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task UpdateCurrentRootAsync_ValidRoot_UpdatesRoot()
    {
        // Arrange
        var metadata = SmtMetadata.Create(_hashFunction, 8);
        await _storage.StoreMetadataAsync(metadata);
        var rootHash = _hashFunction.ComputeHash(Encoding.UTF8.GetBytes("root"));

        // Act
        await _storage.UpdateCurrentRootAsync(new ReadOnlyMemory<byte>(rootHash));

        // Assert
        var result = await _storage.GetCurrentRootAsync();
        Assert.NotNull(result);
        Assert.True(result.Value.Span.SequenceEqual(rootHash));
    }

    [Fact]
    public async Task UpdateCurrentRootAsync_NoMetadata_ThrowsInvalidOperationException()
    {
        // Arrange
        var rootHash = _hashFunction.ComputeHash(Encoding.UTF8.GetBytes("root"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _storage.UpdateCurrentRootAsync(new ReadOnlyMemory<byte>(rootHash)));
    }

    [Fact]
    public async Task UpdateCurrentRootAsync_EmptyRoot_ThrowsArgumentException()
    {
        // Arrange
        var metadata = SmtMetadata.Create(_hashFunction, 8);
        await _storage.StoreMetadataAsync(metadata);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _storage.UpdateCurrentRootAsync(ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public async Task GetCurrentRootAsync_NoRoot_ReturnsNull()
    {
        // Act
        var result = await _storage.GetCurrentRootAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Clear_RemovesAllData()
    {
        // Arrange
        var metadata = SmtMetadata.Create(_hashFunction, 8);
        await _storage.StoreMetadataAsync(metadata);
        
        var nodeData = Encoding.UTF8.GetBytes("test-node");
        var hash = _hashFunction.ComputeHash(nodeData);
        await _storage.WriteNodeAsync(SmtNodeBlob.Create(new ReadOnlyMemory<byte>(hash), new ReadOnlyMemory<byte>(nodeData)));
        
        var rootHash = _hashFunction.ComputeHash(Encoding.UTF8.GetBytes("root"));
        await _storage.CreateSnapshotAsync("snapshot-1", new ReadOnlyMemory<byte>(rootHash));

        // Act
        _storage.Clear();

        // Assert
        Assert.Equal(0, _storage.NodeCount);
        Assert.Equal(0, _storage.SnapshotCount);
        var loadedMetadata = await _storage.LoadMetadataAsync();
        Assert.Null(loadedMetadata);
    }

    #endregion
}
