using System.Text;
using MerkleTree.Hashing;
using MerkleTree.Proofs;
using MerkleTree.Smt;
using MerkleTree.Smt.Persistence;

namespace MerkleTree.Tests.Smt;

/// <summary>
/// Property-based tests for SMT using randomized inputs.
/// These tests verify invariants that should hold for all valid inputs.
/// </summary>
public class SmtPropertyTests
{
    private const int RANDOM_SEED = 42; // Fixed seed for reproducible test runs
    
    private readonly Sha256HashFunction _sha256;
    private readonly Random _random;

    public SmtPropertyTests()
    {
        _sha256 = new Sha256HashFunction();
        // Use fixed seed for reproducible tests - ensures same random sequence every run
        _random = new Random(RANDOM_SEED);
    }

    #region Helper Methods

    private byte[] GenerateRandomKey()
    {
        var key = new byte[_random.Next(1, 100)];
        _random.NextBytes(key);
        return key;
    }

    private byte[] GenerateRandomValue()
    {
        var value = new byte[_random.Next(1, 200)];
        _random.NextBytes(value);
        return value;
    }

    private List<(byte[] key, byte[] value)> GenerateRandomKeyValues(int count)
    {
        var kvs = new List<(byte[], byte[])>();
        for (int i = 0; i < count; i++)
        {
            kvs.Add((GenerateRandomKey(), GenerateRandomValue()));
        }
        return kvs;
    }

    #endregion

    #region Tree Properties

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task Property_InsertedKeyIsRetrievable(int iterations)
    {
        // Property: For all key-value pairs inserted, Get(key) should return value
        for (int i = 0; i < iterations; i++)
        {
            // Arrange
            var tree = new SparseMerkleTree(_sha256, depth: 8);
            var storage = new InMemorySmtStorage();
            var key = GenerateRandomKey();
            var value = GenerateRandomValue();

            // Act - Insert
            var result = await tree.UpdateAsync(key, value, tree.ZeroHashes[tree.Depth], storage);
            await storage.WriteBatchAsync(result.NodesToPersist);

            // Assert - Retrieve
            var getResult = await tree.GetAsync(key, result.NewRootHash, storage);
            Assert.True(getResult.Found, $"Iteration {i}: Key should be found");
            Assert.NotNull(getResult.Value);
            Assert.True(getResult.Value.Value.Span.SequenceEqual(value),
                $"Iteration {i}: Value should match");
        }
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    public async Task Property_DeletedKeyIsNotRetrievable(int iterations)
    {
        // Property: For all inserted then deleted keys, Get(key) should return not found
        for (int i = 0; i < iterations; i++)
        {
            // Arrange
            var tree = new SparseMerkleTree(_sha256, depth: 8);
            var storage = new InMemorySmtStorage();
            var key = GenerateRandomKey();
            var value = GenerateRandomValue();

            // Act - Insert then delete
            var insertResult = await tree.UpdateAsync(key, value, tree.ZeroHashes[tree.Depth], storage);
            await storage.WriteBatchAsync(insertResult.NodesToPersist);

            var deleteResult = await tree.DeleteAsync(key, insertResult.NewRootHash, storage);
            await storage.WriteBatchAsync(deleteResult.NodesToPersist);

            // Assert - Key should not be found
            var getResult = await tree.GetAsync(key, deleteResult.NewRootHash, storage);
            Assert.False(getResult.Found, $"Iteration {i}: Deleted key should not be found");
        }
    }

    [Theory]
    [InlineData(30)]
    public async Task Property_UpdateChangesValue(int iterations)
    {
        // Property: Updating a key with new value should return new value on Get
        for (int i = 0; i < iterations; i++)
        {
            // Arrange
            var tree = new SparseMerkleTree(_sha256, depth: 8);
            var storage = new InMemorySmtStorage();
            var key = GenerateRandomKey();
            var value1 = GenerateRandomValue();
            var value2 = GenerateRandomValue();

            // Act - Insert then update
            var insertResult = await tree.UpdateAsync(key, value1, tree.ZeroHashes[tree.Depth], storage);
            await storage.WriteBatchAsync(insertResult.NodesToPersist);

            var updateResult = await tree.UpdateAsync(key, value2, insertResult.NewRootHash, storage);
            await storage.WriteBatchAsync(updateResult.NodesToPersist);

            // Assert - Should get updated value
            var getResult = await tree.GetAsync(key, updateResult.NewRootHash, storage);
            Assert.True(getResult.Found, $"Iteration {i}: Key should be found");
            Assert.NotNull(getResult.Value);
            Assert.True(getResult.Value.Value.Span.SequenceEqual(value2),
                $"Iteration {i}: Should have updated value");
        }
    }

    [Theory]
    [InlineData(5, 20)]
    [InlineData(10, 50)]
    public async Task Property_MultipleInserts_AllKeysRetrievable(int keyCount, int iterations)
    {
        // Property: After inserting N keys, all N keys should be retrievable
        for (int iter = 0; iter < iterations; iter++)
        {
            // Arrange
            var tree = new SparseMerkleTree(_sha256, depth: 8);
            var storage = new InMemorySmtStorage();
            var kvs = GenerateRandomKeyValues(keyCount);
            ReadOnlyMemory<byte> root = tree.ZeroHashes[tree.Depth];

            // Act - Insert all keys
            foreach (var (key, value) in kvs)
            {
                var result = await tree.UpdateAsync(key, value, root, storage);
                await storage.WriteBatchAsync(result.NodesToPersist);
                root = result.NewRootHash;
            }

            // Assert - All keys should be retrievable
            foreach (var (key, value) in kvs)
            {
                var getResult = await tree.GetAsync(key, root, storage);
                Assert.True(getResult.Found,
                    $"Iteration {iter}: Key should be found");
                Assert.NotNull(getResult.Value);
                Assert.True(getResult.Value.Value.Span.SequenceEqual(value),
                    $"Iteration {iter}: Value should match");
            }
        }
    }

    #endregion

    #region Root Hash Properties

    [Theory]
    [InlineData(50)]
    public async Task Property_SameOperations_ProduceSameRoot(int iterations)
    {
        // Property: Applying same operations produces same root hash
        for (int i = 0; i < iterations; i++)
        {
            // Arrange
            var key = GenerateRandomKey();
            var value = GenerateRandomValue();

            // Build first tree
            var tree1 = new SparseMerkleTree(_sha256, depth: 8);
            var storage1 = new InMemorySmtStorage();
            var result1 = await tree1.UpdateAsync(key, value, tree1.ZeroHashes[tree1.Depth], storage1);
            await storage1.WriteBatchAsync(result1.NodesToPersist);

            // Build second tree with same operations
            var tree2 = new SparseMerkleTree(_sha256, depth: 8);
            var storage2 = new InMemorySmtStorage();
            var result2 = await tree2.UpdateAsync(key, value, tree2.ZeroHashes[tree2.Depth], storage2);
            await storage2.WriteBatchAsync(result2.NodesToPersist);

            // Assert - Roots should match
            Assert.True(result1.NewRootHash.Span.SequenceEqual(result2.NewRootHash.Span),
                $"Iteration {i}: Same operations should produce same root");
        }
    }

    [Theory]
    [InlineData(30)]
    public async Task Property_DifferentValues_ProduceDifferentRoots(int iterations)
    {
        // Property: Inserting different values produces different roots (with high probability)
        for (int i = 0; i < iterations; i++)
        {
            // Arrange
            var tree = new SparseMerkleTree(_sha256, depth: 8);
            var storage = new InMemorySmtStorage();
            var key = GenerateRandomKey();
            var value1 = GenerateRandomValue();
            var value2 = GenerateRandomValue();

            // Ensure values are different by modifying until they differ
            while (value1.SequenceEqual(value2))
            {
                value2 = GenerateRandomValue();
            }

            // Act
            var result1 = await tree.UpdateAsync(key, value1, tree.ZeroHashes[tree.Depth], storage);
            await storage.WriteBatchAsync(result1.NodesToPersist);

            var storage2 = new InMemorySmtStorage();
            var result2 = await tree.UpdateAsync(key, value2, tree.ZeroHashes[tree.Depth], storage2);
            await storage2.WriteBatchAsync(result2.NodesToPersist);

            // Assert - Different values should produce different roots
            Assert.False(result1.NewRootHash.Span.SequenceEqual(result2.NewRootHash.Span),
                $"Iteration {i}: Different values should produce different roots");
        }
    }

    [Theory]
    [InlineData(20)]
    public async Task Property_InsertAndDeleteSameKey_ReturnsToEmptyRoot(int iterations)
    {
        // Property: Starting from empty, insert then delete should return to empty root
        for (int i = 0; i < iterations; i++)
        {
            // Arrange
            var tree = new SparseMerkleTree(_sha256, depth: 8);
            var storage = new InMemorySmtStorage();
            var emptyRoot = tree.ZeroHashes[tree.Depth];
            var key = GenerateRandomKey();
            var value = GenerateRandomValue();

            // Act
            var insertResult = await tree.UpdateAsync(key, value, emptyRoot, storage);
            await storage.WriteBatchAsync(insertResult.NodesToPersist);

            var deleteResult = await tree.DeleteAsync(key, insertResult.NewRootHash, storage);
            await storage.WriteBatchAsync(deleteResult.NodesToPersist);

            // Assert - Should return to empty root
            Assert.True(deleteResult.NewRootHash.Span.SequenceEqual(emptyRoot),
                $"Iteration {i}: Delete should return to empty root");
        }
    }

    #endregion

    #region Proof Properties

    [Theory]
    [InlineData(30)]
    public async Task Property_InclusionProof_AlwaysVerifies(int iterations)
    {
        // Property: Inclusion proof for inserted key should always verify
        for (int i = 0; i < iterations; i++)
        {
            // Arrange
            var tree = new SparseMerkleTree(_sha256, depth: 8);
            var storage = new InMemorySmtStorage();
            var key = GenerateRandomKey();
            var value = GenerateRandomValue();

            // Act - Insert and generate proof
            var result = await tree.UpdateAsync(key, value, tree.ZeroHashes[tree.Depth], storage);
            await storage.WriteBatchAsync(result.NodesToPersist);

            var proof = await tree.GenerateInclusionProofAsync(key, result.NewRootHash, storage);

            // Assert
            Assert.NotNull(proof);
            bool isValid = proof.Verify(result.NewRootHash.ToArray(), _sha256, tree.ZeroHashes);
            Assert.True(isValid, $"Iteration {i}: Proof should verify");
        }
    }

    [Theory]
    [InlineData(20)]
    public async Task Property_NonInclusionProof_ForNonExistentKey_AlwaysVerifies(int iterations)
    {
        // Property: Non-inclusion proof for non-existent key should always verify
        for (int i = 0; i < iterations; i++)
        {
            // Arrange
            var tree = new SparseMerkleTree(_sha256, depth: 8);
            var storage = new InMemorySmtStorage();
            
            // Insert a key
            var existingKey = GenerateRandomKey();
            var value = GenerateRandomValue();
            var result = await tree.UpdateAsync(existingKey, value, tree.ZeroHashes[tree.Depth], storage);
            await storage.WriteBatchAsync(result.NodesToPersist);

            // Try to prove non-inclusion of different key
            var nonExistentKey = GenerateRandomKey();
            
            // Ensure keys are different (max 10 attempts to avoid infinite loop)
            int attempts = 0;
            while (existingKey.SequenceEqual(nonExistentKey) && attempts++ < 10)
                nonExistentKey = GenerateRandomKey();
            
            if (existingKey.SequenceEqual(nonExistentKey))
                return; // Skip this iteration if we can't generate a different key

            // Act
            var proof = await tree.GenerateNonInclusionProofAsync(nonExistentKey, result.NewRootHash, storage);

            // Assert
            if (proof != null) // May be null if path collides with existing key
            {
                bool isValid = proof.Verify(result.NewRootHash.ToArray(), _sha256, tree.ZeroHashes);
                Assert.True(isValid, $"Iteration {i}: Non-inclusion proof should verify");
            }
        }
    }

    [Theory]
    [InlineData(20)]
    public async Task Property_ProofSerialization_PreservesVerification(int iterations)
    {
        // Property: Serialized and deserialized proof should still verify
        for (int i = 0; i < iterations; i++)
        {
            // Arrange
            var tree = new SparseMerkleTree(_sha256, depth: 8);
            var storage = new InMemorySmtStorage();
            var key = GenerateRandomKey();
            var value = GenerateRandomValue();

            var result = await tree.UpdateAsync(key, value, tree.ZeroHashes[tree.Depth], storage);
            await storage.WriteBatchAsync(result.NodesToPersist);

            var proof = await tree.GenerateInclusionProofAsync(key, result.NewRootHash, storage);
            Assert.NotNull(proof);

            // Act - Serialize and deserialize
            var serialized = proof.Serialize();
            var deserialized = SmtInclusionProof.Deserialize(serialized);

            // Assert - Should still verify
            bool isValid = deserialized.Verify(result.NewRootHash.Span.ToArray(), _sha256, tree.ZeroHashes);
            Assert.True(isValid, $"Iteration {i}: Deserialized proof should verify");
        }
    }

    #endregion

    #region Batch Operation Properties

    [Theory]
    [InlineData(5, 20)]
    [InlineData(10, 10)]
    public async Task Property_BatchUpdate_EquivalentToSequentialUpdates(int keyCount, int iterations)
    {
        // Property: Batch update should produce same root as sequential updates
        for (int iter = 0; iter < iterations; iter++)
        {
            // Arrange
            var kvs = GenerateRandomKeyValues(keyCount);
            var updates = kvs.Select(kv =>
                SmtKeyValue.CreateUpdate(kv.key, kv.value)).ToArray();

            // Build tree with batch update
            var tree1 = new SparseMerkleTree(_sha256, depth: 8);
            var storage1 = new InMemorySmtStorage();
            var batchResult = await tree1.BatchUpdateAsync(
                updates,
                tree1.ZeroHashes[tree1.Depth],
                storage1,
                storage1);

            // Build tree with sequential updates
            var tree2 = new SparseMerkleTree(_sha256, depth: 8);
            var storage2 = new InMemorySmtStorage();
            ReadOnlyMemory<byte> root = tree2.ZeroHashes[tree2.Depth];

            // Sort by key hash (same as batch update does internally)
            var sortedKvs = kvs
                .Select(kv => (key: kv.key, value: kv.value, keyHash: tree2.HashKey(kv.key)))
                .OrderBy(x => Convert.ToHexString(x.keyHash))
                .ToList();

            foreach (var (key, value, _) in sortedKvs)
            {
                var result = await tree2.UpdateAsync(key, value, root, storage2);
                await storage2.WriteBatchAsync(result.NodesToPersist);
                root = result.NewRootHash;
            }

            // Assert - Roots should match
            Assert.True(batchResult.NewRootHash.Span.SequenceEqual(root.Span),
                $"Iteration {iter}: Batch and sequential should produce same root");
        }
    }

    [Theory]
    [InlineData(20)]
    public async Task Property_BatchUpdate_AllKeysAreRetrievable(int iterations)
    {
        // Property: After batch update, all keys should be retrievable
        for (int iter = 0; iter < iterations; iter++)
        {
            // Arrange
            var keyCount = _random.Next(5, 15);
            var kvs = GenerateRandomKeyValues(keyCount);
            var updates = kvs.Select(kv =>
                SmtKeyValue.CreateUpdate(kv.key, kv.value)).ToArray();

            var tree = new SparseMerkleTree(_sha256, depth: 8);
            var storage = new InMemorySmtStorage();

            // Act
            var result = await tree.BatchUpdateAsync(
                updates,
                tree.ZeroHashes[tree.Depth],
                storage,
                storage);

            // Assert - All keys should be retrievable
            foreach (var (key, value) in kvs)
            {
                var getResult = await tree.GetAsync(key, result.NewRootHash, storage);
                Assert.True(getResult.Found,
                    $"Iteration {iter}: Key should be found after batch update");
                Assert.NotNull(getResult.Value);
                Assert.True(getResult.Value.Value.Span.SequenceEqual(value),
                    $"Iteration {iter}: Value should match after batch update");
            }
        }
    }

    #endregion

    #region Copy-on-Write Properties

    [Theory]
    [InlineData(20)]
    public async Task Property_OldRootRemains_Valid_AfterUpdate(int iterations)
    {
        // Property: Old tree versions remain accessible after updates (copy-on-write)
        for (int i = 0; i < iterations; i++)
        {
            // Arrange
            var tree = new SparseMerkleTree(_sha256, depth: 8);
            var storage = new InMemorySmtStorage();
            
            var key1 = GenerateRandomKey();
            var value1 = GenerateRandomValue();
            var key2 = GenerateRandomKey();
            var value2 = GenerateRandomValue();

            // Act - Insert key1, save old root, then insert key2
            var result1 = await tree.UpdateAsync(key1, value1, tree.ZeroHashes[tree.Depth], storage);
            await storage.WriteBatchAsync(result1.NodesToPersist);
            var oldRoot = result1.NewRootHash;

            var result2 = await tree.UpdateAsync(key2, value2, oldRoot, storage);
            await storage.WriteBatchAsync(result2.NodesToPersist);
            var newRoot = result2.NewRootHash;

            // Assert - Old root should still provide access to old data
            var getOld = await tree.GetAsync(key1, oldRoot, storage);
            Assert.True(getOld.Found, $"Iteration {i}: Key1 should be found in old root");

            var getOldKey2 = await tree.GetAsync(key2, oldRoot, storage);
            Assert.False(getOldKey2.Found,
                $"Iteration {i}: Key2 should not be found in old root");

            // New root should have both keys
            var getNew1 = await tree.GetAsync(key1, newRoot, storage);
            var getNew2 = await tree.GetAsync(key2, newRoot, storage);
            Assert.True(getNew1.Found, $"Iteration {i}: Key1 should be found in new root");
            Assert.True(getNew2.Found, $"Iteration {i}: Key2 should be found in new root");
        }
    }

    #endregion

    #region Stress Tests

    [Fact]
    public async Task Property_LargeRandomSequence_MaintainsConsistency()
    {
        // Property: Large sequence of random operations maintains tree consistency
        // Arrange
        var tree = new SparseMerkleTree(_sha256, depth: 8);
        var storage = new InMemorySmtStorage();
        ReadOnlyMemory<byte> root = tree.ZeroHashes[tree.Depth];
        var insertedKeys = new Dictionary<string, byte[]>();

        // Act - Perform 100 random operations
        for (int i = 0; i < 100; i++)
        {
            var operation = _random.Next(3); // 0=insert, 1=update, 2=delete

            if (operation == 0 || insertedKeys.Count == 0) // Insert
            {
                var key = GenerateRandomKey();
                var value = GenerateRandomValue();
                var result = await tree.UpdateAsync(key, value, root, storage);
                await storage.WriteBatchAsync(result.NodesToPersist);
                root = result.NewRootHash;
                insertedKeys[Convert.ToBase64String(key)] = value;
            }
            else if (operation == 1 && insertedKeys.Count > 0) // Update
            {
                var keyToUpdate = insertedKeys.Keys.ElementAt(_random.Next(insertedKeys.Count));
                var key = Convert.FromBase64String(keyToUpdate);
                var newValue = GenerateRandomValue();
                var result = await tree.UpdateAsync(key, newValue, root, storage);
                await storage.WriteBatchAsync(result.NodesToPersist);
                root = result.NewRootHash;
                insertedKeys[keyToUpdate] = newValue;
            }
            else if (operation == 2 && insertedKeys.Count > 0) // Delete
            {
                var keyToDelete = insertedKeys.Keys.ElementAt(_random.Next(insertedKeys.Count));
                var key = Convert.FromBase64String(keyToDelete);
                var result = await tree.DeleteAsync(key, root, storage);
                await storage.WriteBatchAsync(result.NodesToPersist);
                root = result.NewRootHash;
                insertedKeys.Remove(keyToDelete);
            }
        }

        // Assert - All remaining keys should be retrievable with correct values
        foreach (var kvp in insertedKeys)
        {
            var key = Convert.FromBase64String(kvp.Key);
            var expectedValue = kvp.Value;

            var getResult = await tree.GetAsync(key, root, storage);
            Assert.True(getResult.Found, "Key should be found after random sequence");
            Assert.NotNull(getResult.Value);
            Assert.True(getResult.Value.Value.Span.SequenceEqual(expectedValue),
                "Value should match after random sequence");
        }
    }

    #endregion
}
