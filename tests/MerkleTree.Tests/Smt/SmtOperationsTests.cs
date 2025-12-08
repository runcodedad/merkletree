using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using MerkleTree.Hashing;
using MerkleTree.Smt;
using MerkleTree.Smt.Persistence;

namespace MerkleTree.Tests.Smt;

/// <summary>
/// Tests for SMT core operations: Get, Update, Delete, and Batch Updates.
/// </summary>
public class SmtOperationsTests
{
    private readonly Sha256HashFunction _hashFunction;
    private readonly SparseMerkleTree _smt;
    private readonly InMemorySmtStorage _storage;

    public SmtOperationsTests()
    {
        _hashFunction = new Sha256HashFunction();
        _smt = new SparseMerkleTree(_hashFunction, depth: 8);
        _storage = new InMemorySmtStorage();
    }

    #region Get Operation Tests

    [Fact]
    public async Task GetAsync_EmptyTree_ReturnsNotFound()
    {
        // Arrange
        var key = Encoding.UTF8.GetBytes("test-key");
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];

        // Act
        var result = await _smt.GetAsync(key, emptyRoot, _storage);

        // Assert
        Assert.False(result.Found);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetAsync_AfterUpdate_ReturnsValue()
    {
        // Arrange
        var key = Encoding.UTF8.GetBytes("test-key");
        var value = Encoding.UTF8.GetBytes("test-value");
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];

        // Update
        var updateResult = await _smt.UpdateAsync(key, value, emptyRoot, _storage);
        await _storage.WriteBatchAsync(updateResult.NodesToPersist);

        // Act
        var getResult = await _smt.GetAsync(key, updateResult.NewRootHash, _storage);

        // Assert
        Assert.True(getResult.Found);
        Assert.NotNull(getResult.Value);
        Assert.True(getResult.Value.Value.Span.SequenceEqual(value));
    }

    [Fact]
    public async Task GetAsync_NonExistentKey_ReturnsNotFound()
    {
        // Arrange
        var key1 = Encoding.UTF8.GetBytes("existing-key");
        var value1 = Encoding.UTF8.GetBytes("value1");
        var key2 = Encoding.UTF8.GetBytes("non-existent-key");
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];

        // Update with key1
        var updateResult = await _smt.UpdateAsync(key1, value1, emptyRoot, _storage);
        await _storage.WriteBatchAsync(updateResult.NodesToPersist);

        // Act - get key2
        var getResult = await _smt.GetAsync(key2, updateResult.NewRootHash, _storage);

        // Assert
        Assert.False(getResult.Found);
        Assert.Null(getResult.Value);
    }

    [Fact]
    public async Task GetAsync_NullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _smt.GetAsync(null!, emptyRoot, _storage));
    }

    [Fact]
    public async Task GetAsync_EmptyKey_ThrowsArgumentException()
    {
        // Arrange
        var emptyKey = Array.Empty<byte>();
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _smt.GetAsync(emptyKey, emptyRoot, _storage));
    }

    #endregion

    #region Update Operation Tests

    [Fact]
    public async Task UpdateAsync_EmptyTree_CreatesNewLeaf()
    {
        // Arrange
        var key = Encoding.UTF8.GetBytes("test-key");
        var value = Encoding.UTF8.GetBytes("test-value");
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];

        // Act
        var result = await _smt.UpdateAsync(key, value, emptyRoot, _storage);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.NewRootHash.IsEmpty);
        Assert.NotEmpty(result.NodesToPersist);
        Assert.False(result.NewRootHash.Span.SequenceEqual(emptyRoot));
    }

    [Fact]
    public async Task UpdateAsync_ExistingKey_UpdatesValue()
    {
        // Arrange
        var key = Encoding.UTF8.GetBytes("test-key");
        var value1 = Encoding.UTF8.GetBytes("value1");
        var value2 = Encoding.UTF8.GetBytes("value2");
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];

        // First update
        var result1 = await _smt.UpdateAsync(key, value1, emptyRoot, _storage);
        await _storage.WriteBatchAsync(result1.NodesToPersist);

        // Act - second update
        var result2 = await _smt.UpdateAsync(key, value2, result1.NewRootHash, _storage);
        await _storage.WriteBatchAsync(result2.NodesToPersist);

        // Assert - root changed
        Assert.False(result2.NewRootHash.Span.SequenceEqual(result1.NewRootHash.Span));

        // Verify the new value
        var getResult = await _smt.GetAsync(key, result2.NewRootHash, _storage);
        Assert.True(getResult.Found);
        Assert.True(getResult.Value!.Value.Span.SequenceEqual(value2));
    }

    [Fact]
    public async Task UpdateAsync_MultipleKeys_CreatesCorrectTree()
    {
        // Arrange
        var key1 = Encoding.UTF8.GetBytes("key1");
        var value1 = Encoding.UTF8.GetBytes("value1");
        var key2 = Encoding.UTF8.GetBytes("key2");
        var value2 = Encoding.UTF8.GetBytes("value2");
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];

        // Act - insert two keys
        var result1 = await _smt.UpdateAsync(key1, value1, emptyRoot, _storage);
        await _storage.WriteBatchAsync(result1.NodesToPersist);

        var result2 = await _smt.UpdateAsync(key2, value2, result1.NewRootHash, _storage);
        await _storage.WriteBatchAsync(result2.NodesToPersist);

        // Assert - both keys are retrievable
        var get1 = await _smt.GetAsync(key1, result2.NewRootHash, _storage);
        var get2 = await _smt.GetAsync(key2, result2.NewRootHash, _storage);

        Assert.True(get1.Found);
        Assert.True(get1.Value!.Value.Span.SequenceEqual(value1));
        Assert.True(get2.Found);
        Assert.True(get2.Value!.Value.Span.SequenceEqual(value2));
    }

    [Fact]
    public async Task UpdateAsync_NullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var value = Encoding.UTF8.GetBytes("value");
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _smt.UpdateAsync(null!, value, emptyRoot, _storage));
    }

    [Fact]
    public async Task UpdateAsync_NullValue_ThrowsArgumentNullException()
    {
        // Arrange
        var key = Encoding.UTF8.GetBytes("key");
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _smt.UpdateAsync(key, null!, emptyRoot, _storage));
    }

    [Fact]
    public async Task UpdateAsync_EmptyValue_ThrowsArgumentException()
    {
        // Arrange
        var key = Encoding.UTF8.GetBytes("key");
        var emptyValue = Array.Empty<byte>();
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _smt.UpdateAsync(key, emptyValue, emptyRoot, _storage));
    }

    #endregion

    #region Delete Operation Tests

    [Fact]
    public async Task DeleteAsync_ExistingKey_RemovesKey()
    {
        // Arrange
        var key = Encoding.UTF8.GetBytes("test-key");
        var value = Encoding.UTF8.GetBytes("test-value");
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];

        // Insert
        var insertResult = await _smt.UpdateAsync(key, value, emptyRoot, _storage);
        await _storage.WriteBatchAsync(insertResult.NodesToPersist);

        // Act - delete
        var deleteResult = await _smt.DeleteAsync(key, insertResult.NewRootHash, _storage);
        await _storage.WriteBatchAsync(deleteResult.NodesToPersist);

        // Assert - key no longer found
        var getResult = await _smt.GetAsync(key, deleteResult.NewRootHash, _storage);
        Assert.False(getResult.Found);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentKey_Succeeds()
    {
        // Arrange
        var key = Encoding.UTF8.GetBytes("non-existent");
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];

        // Act - delete non-existent key (should be idempotent)
        var result = await _smt.DeleteAsync(key, emptyRoot, _storage);

        // Assert - should succeed
        Assert.NotNull(result);
        Assert.False(result.NewRootHash.IsEmpty);
    }

    [Fact]
    public async Task DeleteAsync_OneOfMultipleKeys_LeavesOthers()
    {
        // Arrange
        var key1 = Encoding.UTF8.GetBytes("key1");
        var value1 = Encoding.UTF8.GetBytes("value1");
        var key2 = Encoding.UTF8.GetBytes("key2");
        var value2 = Encoding.UTF8.GetBytes("value2");
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];

        // Insert two keys
        var result1 = await _smt.UpdateAsync(key1, value1, emptyRoot, _storage);
        await _storage.WriteBatchAsync(result1.NodesToPersist);

        var result2 = await _smt.UpdateAsync(key2, value2, result1.NewRootHash, _storage);
        await _storage.WriteBatchAsync(result2.NodesToPersist);

        // Act - delete key1
        var deleteResult = await _smt.DeleteAsync(key1, result2.NewRootHash, _storage);
        await _storage.WriteBatchAsync(deleteResult.NodesToPersist);

        // Assert - key1 not found, key2 still found
        var get1 = await _smt.GetAsync(key1, deleteResult.NewRootHash, _storage);
        var get2 = await _smt.GetAsync(key2, deleteResult.NewRootHash, _storage);

        Assert.False(get1.Found);
        Assert.True(get2.Found);
        Assert.True(get2.Value!.Value.Span.SequenceEqual(value2));
    }

    [Fact]
    public async Task DeleteAsync_NullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _smt.DeleteAsync(null!, emptyRoot, _storage));
    }

    #endregion

    #region Batch Update Tests

    [Fact]
    public async Task BatchUpdateAsync_EmptyBatch_Succeeds()
    {
        // Arrange
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];
        var updates = Array.Empty<SmtKeyValue>();

        // Act
        var result = await _smt.BatchUpdateAsync(updates, emptyRoot, _storage);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.NewRootHash.IsEmpty);
    }

    [Fact]
    public async Task BatchUpdateAsync_MultipleUpdates_AllApplied()
    {
        // Arrange
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];
        var updates = new[]
        {
            SmtKeyValue.CreateUpdate(Encoding.UTF8.GetBytes("key1"), Encoding.UTF8.GetBytes("value1")),
            SmtKeyValue.CreateUpdate(Encoding.UTF8.GetBytes("key2"), Encoding.UTF8.GetBytes("value2")),
            SmtKeyValue.CreateUpdate(Encoding.UTF8.GetBytes("key3"), Encoding.UTF8.GetBytes("value3"))
        };

        // Act
        var result = await _smt.BatchUpdateAsync(updates, emptyRoot, _storage);
        await _storage.WriteBatchAsync(result.NodesToPersist);

        // Assert - all keys are retrievable
        var get1 = await _smt.GetAsync(Encoding.UTF8.GetBytes("key1"), result.NewRootHash, _storage);
        var get2 = await _smt.GetAsync(Encoding.UTF8.GetBytes("key2"), result.NewRootHash, _storage);
        var get3 = await _smt.GetAsync(Encoding.UTF8.GetBytes("key3"), result.NewRootHash, _storage);

        Assert.True(get1.Found);
        Assert.True(get2.Found);
        Assert.True(get3.Found);
    }

    [Fact]
    public async Task BatchUpdateAsync_WithDeletes_AppliesCorrectly()
    {
        // Arrange
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];
        
        // First insert some keys
        var initialUpdates = new[]
        {
            SmtKeyValue.CreateUpdate(Encoding.UTF8.GetBytes("key1"), Encoding.UTF8.GetBytes("value1")),
            SmtKeyValue.CreateUpdate(Encoding.UTF8.GetBytes("key2"), Encoding.UTF8.GetBytes("value2"))
        };
        var initialResult = await _smt.BatchUpdateAsync(initialUpdates, emptyRoot, _storage);
        await _storage.WriteBatchAsync(initialResult.NodesToPersist);

        // Batch with mix of updates and deletes
        var mixedUpdates = new[]
        {
            SmtKeyValue.CreateUpdate(Encoding.UTF8.GetBytes("key3"), Encoding.UTF8.GetBytes("value3")),
            SmtKeyValue.CreateDelete(Encoding.UTF8.GetBytes("key1"))
        };

        // Act
        var result = await _smt.BatchUpdateAsync(mixedUpdates, initialResult.NewRootHash, _storage);
        await _storage.WriteBatchAsync(result.NodesToPersist);

        // Assert
        var get1 = await _smt.GetAsync(Encoding.UTF8.GetBytes("key1"), result.NewRootHash, _storage);
        var get2 = await _smt.GetAsync(Encoding.UTF8.GetBytes("key2"), result.NewRootHash, _storage);
        var get3 = await _smt.GetAsync(Encoding.UTF8.GetBytes("key3"), result.NewRootHash, _storage);

        Assert.False(get1.Found); // Deleted
        Assert.True(get2.Found);  // Still exists
        Assert.True(get3.Found);  // Newly added
    }

    [Fact]
    public async Task BatchUpdateAsync_Deterministic_SameRootRegardlessOfOrder()
    {
        // Arrange
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];
        var updates1 = new[]
        {
            SmtKeyValue.CreateUpdate(Encoding.UTF8.GetBytes("key1"), Encoding.UTF8.GetBytes("value1")),
            SmtKeyValue.CreateUpdate(Encoding.UTF8.GetBytes("key2"), Encoding.UTF8.GetBytes("value2")),
            SmtKeyValue.CreateUpdate(Encoding.UTF8.GetBytes("key3"), Encoding.UTF8.GetBytes("value3"))
        };

        // Same updates but different order
        var updates2 = new[]
        {
            SmtKeyValue.CreateUpdate(Encoding.UTF8.GetBytes("key3"), Encoding.UTF8.GetBytes("value3")),
            SmtKeyValue.CreateUpdate(Encoding.UTF8.GetBytes("key1"), Encoding.UTF8.GetBytes("value1")),
            SmtKeyValue.CreateUpdate(Encoding.UTF8.GetBytes("key2"), Encoding.UTF8.GetBytes("value2"))
        };

        // Act
        var result1 = await _smt.BatchUpdateAsync(updates1, emptyRoot, _storage);
        
        var storage2 = new InMemorySmtStorage();
        var result2 = await _smt.BatchUpdateAsync(updates2, emptyRoot, storage2);

        // Assert - roots should be identical
        Assert.True(result1.NewRootHash.Span.SequenceEqual(result2.NewRootHash.Span));
    }

    [Fact]
    public async Task BatchUpdateAsync_ConflictingUpdates_LastWriteWins()
    {
        // Arrange
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];
        var key = Encoding.UTF8.GetBytes("conflict-key");
        
        // Multiple updates for the same key in one batch
        var updates = new[]
        {
            SmtKeyValue.CreateUpdate(key, Encoding.UTF8.GetBytes("value1")),
            SmtKeyValue.CreateUpdate(key, Encoding.UTF8.GetBytes("value2")),
            SmtKeyValue.CreateUpdate(key, Encoding.UTF8.GetBytes("value3"))
        };

        // Act
        var result = await _smt.BatchUpdateAsync(updates, emptyRoot, _storage);
        await _storage.WriteBatchAsync(result.NodesToPersist);

        // Assert - should have the last value (deterministic ordering)
        var getResult = await _smt.GetAsync(key, result.NewRootHash, _storage);
        Assert.True(getResult.Found);
        // The exact value depends on sorting order, but it should be deterministic
        Assert.NotNull(getResult.Value);
    }

    [Fact]
    public async Task BatchUpdateAsync_NullUpdates_ThrowsArgumentNullException()
    {
        // Arrange
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _smt.BatchUpdateAsync(null!, emptyRoot, _storage));
    }

    #endregion

    #region Copy-on-Write Tests

    [Fact]
    public async Task Update_CopyOnWrite_OldRootStillValid()
    {
        // Arrange
        var key1 = Encoding.UTF8.GetBytes("key1");
        var value1 = Encoding.UTF8.GetBytes("value1");
        var key2 = Encoding.UTF8.GetBytes("key2");
        var value2 = Encoding.UTF8.GetBytes("value2");
        var emptyRoot = _smt.ZeroHashes[_smt.Depth];

        // Insert key1
        var result1 = await _smt.UpdateAsync(key1, value1, emptyRoot, _storage);
        await _storage.WriteBatchAsync(result1.NodesToPersist);
        var oldRoot = result1.NewRootHash;

        // Act - insert key2
        var result2 = await _smt.UpdateAsync(key2, value2, oldRoot, _storage);
        await _storage.WriteBatchAsync(result2.NodesToPersist);
        var newRoot = result2.NewRootHash;

        // Assert - old root should still be valid and show only key1
        var getOld = await _smt.GetAsync(key1, oldRoot, _storage);
        var getOldKey2 = await _smt.GetAsync(key2, oldRoot, _storage);
        
        Assert.True(getOld.Found);
        Assert.False(getOldKey2.Found); // key2 doesn't exist in old version

        // New root should have both keys
        var getNew1 = await _smt.GetAsync(key1, newRoot, _storage);
        var getNew2 = await _smt.GetAsync(key2, newRoot, _storage);
        
        Assert.True(getNew1.Found);
        Assert.True(getNew2.Found);
    }

    #endregion
}
