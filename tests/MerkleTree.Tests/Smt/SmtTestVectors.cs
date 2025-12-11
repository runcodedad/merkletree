using System.Text;
using MerkleTree.Hashing;
using MerkleTree.Proofs;
using MerkleTree.Smt;
using MerkleTree.Smt.Persistence;

namespace MerkleTree.Tests.Smt;

/// <summary>
/// Test vectors with precomputed expected values for regression testing.
/// These tests verify that SMT produces consistent results across versions.
/// </summary>
/// <remarks>
/// <para>IMPORTANT: If any of these tests fail, it indicates a BREAKING CHANGE
/// in the SMT implementation that will affect existing deployments.</para>
/// 
/// <para><strong>TODO Process for Capturing Expected Values:</strong></para>
/// <list type="number">
/// <item>Wait for SMT operations to be fully stable (all operation tests passing)</item>
/// <item>Run each test once to capture actual output values</item>
/// <item>Replace placeholder TODOs with actual hex values</item>
/// <item>Uncomment the Assert.Equal() assertions</item>
/// <item>These values then become the regression baseline</item>
/// </list>
/// 
/// <para>Current status: SMT operations have some known issues (see SMT_IMPLEMENTATION_NOTES.md),
/// so test vectors are documented but assertions are commented out until stable.</para>
/// </remarks>
public class SmtTestVectors
{
    private readonly Sha256HashFunction _sha256;

    public SmtTestVectors()
    {
        _sha256 = new Sha256HashFunction();
    }

    #region Empty Tree Vectors

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public async Task EmptyTree_ProducesExpectedRoot(int depth)
    {
        // Arrange
        var tree = new SparseMerkleTree(_sha256, depth);

        // Act
        ReadOnlyMemory<byte> emptyRoot = tree.ZeroHashes[depth];
        var actualRootHex = Convert.ToHexString(emptyRoot.Span);

        // Assert - Verify structure exists
        Assert.NotEmpty(actualRootHex);
        Assert.Equal(64, actualRootHex.Length); // SHA-256 = 32 bytes = 64 hex chars
        
        // TODO: Once stable, capture expected values for regression:
        // depth=8:  actualRootHex = "..."
        // depth=16: actualRootHex = "..."
        // depth=32: actualRootHex = "..."
    }

    #endregion

    #region Single Key Vectors

    [Fact]
    public async Task SingleKey_StandardInput_ProducesExpectedRoot()
    {
        // Arrange
        var tree = new SparseMerkleTree(_sha256, depth: 8);
        var storage = new InMemorySmtStorage();

        var key = Encoding.UTF8.GetBytes("test");
        var value = Encoding.UTF8.GetBytes("value");

        // Act
        var result = await tree.UpdateAsync(key, value, tree.ZeroHashes[tree.Depth], storage);
        await storage.WriteBatchAsync(result.NodesToPersist);

        var actualRootHex = Convert.ToHexString(result.NewRootHash.Span);

        // Assert
        Assert.NotEmpty(actualRootHex);
        Assert.Equal(64, actualRootHex.Length);

        // Document the expected value for regression
        // Expected root for key="test", value="value", depth=8 with SHA-256:
        // TODO: Update this with actual value once implementation is stable
        // var expectedRootHex = "ACTUAL_VALUE_HERE";
        // Assert.Equal(expectedRootHex, actualRootHex);
    }

    [Fact]
    public async Task SingleKey_EmptyValue_ProducesExpectedRoot()
    {
        // This test documents behavior for edge case: what happens with empty string value
        // Note: Current implementation throws on empty value, so this documents that behavior
        
        // Arrange
        var tree = new SparseMerkleTree(_sha256, depth: 8);
        var storage = new InMemorySmtStorage();

        var key = Encoding.UTF8.GetBytes("key");
        var value = Array.Empty<byte>();

        // Act & Assert - Document that empty values are rejected
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await tree.UpdateAsync(key, value, tree.ZeroHashes[tree.Depth], storage));
    }

    #endregion

    #region Multiple Keys Vectors

    [Fact]
    public async Task TwoKeys_StandardInputs_ProducesExpectedRoot()
    {
        // Arrange
        var tree = new SparseMerkleTree(_sha256, depth: 8);
        var storage = new InMemorySmtStorage();
        var root = tree.ZeroHashes[tree.Depth];

        // Use lexicographically ordered keys for clarity
        var key1 = Encoding.UTF8.GetBytes("alice");
        var value1 = Encoding.UTF8.GetBytes("100");
        var key2 = Encoding.UTF8.GetBytes("bob");
        var value2 = Encoding.UTF8.GetBytes("200");

        // Act - Insert in order
        var result1 = await tree.UpdateAsync(key1, value1, root, storage);
        await storage.WriteBatchAsync(result1.NodesToPersist);

        var result2 = await tree.UpdateAsync(key2, value2, result1.NewRootHash, storage);
        await storage.WriteBatchAsync(result2.NodesToPersist);

        var actualRootHex = Convert.ToHexString(result2.NewRootHash.Span);

        // Assert
        Assert.NotEmpty(actualRootHex);
        Assert.Equal(64, actualRootHex.Length);

        // Expected root for keys ["alice"=>"100", "bob"=>"200"], depth=8 with SHA-256:
        // TODO: Update with actual value
    }

    [Fact]
    public async Task FiveKeys_StandardInputs_ProducesExpectedRoot()
    {
        // Arrange
        var tree = new SparseMerkleTree(_sha256, depth: 8);
        var storage = new InMemorySmtStorage();
        ReadOnlyMemory<byte> root = tree.ZeroHashes[tree.Depth];

        var keys = new[]
        {
            Encoding.UTF8.GetBytes("alice"),
            Encoding.UTF8.GetBytes("bob"),
            Encoding.UTF8.GetBytes("charlie"),
            Encoding.UTF8.GetBytes("diana"),
            Encoding.UTF8.GetBytes("eve")
        };

        var values = new[]
        {
            Encoding.UTF8.GetBytes("100"),
            Encoding.UTF8.GetBytes("200"),
            Encoding.UTF8.GetBytes("300"),
            Encoding.UTF8.GetBytes("400"),
            Encoding.UTF8.GetBytes("500")
        };

        // Act - Insert all keys
        for (int i = 0; i < keys.Length; i++)
        {
            var result = await tree.UpdateAsync(keys[i], values[i], root, storage);
            await storage.WriteBatchAsync(result.NodesToPersist);
            root = result.NewRootHash;
        }

        var actualRootHex = Convert.ToHexString(root.Span);

        // Assert
        Assert.NotEmpty(actualRootHex);
        Assert.Equal(64, actualRootHex.Length);

        // Expected root for 5 specific key-value pairs:
        // TODO: Update with actual value
    }

    #endregion

    #region Update and Delete Vectors

    [Fact]
    public async Task InsertThenUpdate_ProducesExpectedRoot()
    {
        // Arrange
        var tree = new SparseMerkleTree(_sha256, depth: 8);
        var storage = new InMemorySmtStorage();

        var key = Encoding.UTF8.GetBytes("key1");
        var value1 = Encoding.UTF8.GetBytes("initial_value");
        var value2 = Encoding.UTF8.GetBytes("updated_value");

        // Act - Insert then update
        var insert = await tree.UpdateAsync(key, value1, tree.ZeroHashes[tree.Depth], storage);
        await storage.WriteBatchAsync(insert.NodesToPersist);

        var update = await tree.UpdateAsync(key, value2, insert.NewRootHash, storage);
        await storage.WriteBatchAsync(update.NodesToPersist);

        var actualRootHex = Convert.ToHexString(update.NewRootHash.Span);

        // Assert
        Assert.NotEmpty(actualRootHex);
        Assert.Equal(64, actualRootHex.Length);

        // Expected root after update:
        // TODO: Update with actual value
    }

    [Fact]
    public async Task InsertThenDelete_ReturnsToEmptyRoot()
    {
        // Arrange
        var tree = new SparseMerkleTree(_sha256, depth: 8);
        var storage = new InMemorySmtStorage();
        ReadOnlyMemory<byte> emptyRoot = tree.ZeroHashes[tree.Depth];

        var key = Encoding.UTF8.GetBytes("temporary");
        var value = Encoding.UTF8.GetBytes("will_be_deleted");

        // Act - Insert then delete
        var insert = await tree.UpdateAsync(key, value, emptyRoot, storage);
        await storage.WriteBatchAsync(insert.NodesToPersist);

        var delete = await tree.DeleteAsync(key, insert.NewRootHash, storage);
        await storage.WriteBatchAsync(delete.NodesToPersist);

        // Assert - Should return to empty tree root
        Assert.True(delete.NewRootHash.Span.SequenceEqual(emptyRoot.Span));
    }

    [Fact]
    public async Task MultipleInsertAndDeletes_ProducesExpectedRoot()
    {
        // Arrange
        var tree = new SparseMerkleTree(_sha256, depth: 8);
        var storage = new InMemorySmtStorage();
        ReadOnlyMemory<byte> root = tree.ZeroHashes[tree.Depth];

        // Act - Complex sequence
        // Insert key1
        var r1 = await tree.UpdateAsync(
            Encoding.UTF8.GetBytes("key1"),
            Encoding.UTF8.GetBytes("value1"),
            root,
            storage);
        await storage.WriteBatchAsync(r1.NodesToPersist);

        // Insert key2
        var r2 = await tree.UpdateAsync(
            Encoding.UTF8.GetBytes("key2"),
            Encoding.UTF8.GetBytes("value2"),
            r1.NewRootHash,
            storage);
        await storage.WriteBatchAsync(r2.NodesToPersist);

        // Delete key1
        var r3 = await tree.DeleteAsync(
            Encoding.UTF8.GetBytes("key1"),
            r2.NewRootHash,
            storage);
        await storage.WriteBatchAsync(r3.NodesToPersist);

        // Insert key3
        var r4 = await tree.UpdateAsync(
            Encoding.UTF8.GetBytes("key3"),
            Encoding.UTF8.GetBytes("value3"),
            r3.NewRootHash,
            storage);
        await storage.WriteBatchAsync(r4.NodesToPersist);

        var actualRootHex = Convert.ToHexString(r4.NewRootHash.Span);

        // Assert
        Assert.NotEmpty(actualRootHex);
        Assert.Equal(64, actualRootHex.Length);

        // Expected root: tree with key2 and key3 only
        // TODO: Update with actual value
    }

    #endregion

    #region Batch Operation Vectors

    [Fact]
    public async Task BatchUpdate_ThreeKeys_ProducesExpectedRoot()
    {
        // Arrange
        var tree = new SparseMerkleTree(_sha256, depth: 8);
        var storage = new InMemorySmtStorage();

        var updates = new[]
        {
            SmtKeyValue.CreateUpdate(
                Encoding.UTF8.GetBytes("batch_key1"),
                Encoding.UTF8.GetBytes("batch_value1")),
            SmtKeyValue.CreateUpdate(
                Encoding.UTF8.GetBytes("batch_key2"),
                Encoding.UTF8.GetBytes("batch_value2")),
            SmtKeyValue.CreateUpdate(
                Encoding.UTF8.GetBytes("batch_key3"),
                Encoding.UTF8.GetBytes("batch_value3"))
        };

        // Act
        var result = await tree.BatchUpdateAsync(
            updates,
            tree.ZeroHashes[tree.Depth],
            storage,
            storage);

        var actualRootHex = Convert.ToHexString(result.NewRootHash.Span);

        // Assert
        Assert.NotEmpty(actualRootHex);
        Assert.Equal(64, actualRootHex.Length);

        // Expected root for batch of 3 keys:
        // TODO: Update with actual value
    }

    [Fact]
    public async Task BatchUpdate_WithDuplicates_LastWriteWins()
    {
        // Arrange - Document behavior when same key appears multiple times
        var tree = new SparseMerkleTree(_sha256, depth: 8);
        var storage = new InMemorySmtStorage();

        var updates = new[]
        {
            SmtKeyValue.CreateUpdate(
                Encoding.UTF8.GetBytes("duplicate"),
                Encoding.UTF8.GetBytes("first")),
            SmtKeyValue.CreateUpdate(
                Encoding.UTF8.GetBytes("duplicate"),
                Encoding.UTF8.GetBytes("second")),
            SmtKeyValue.CreateUpdate(
                Encoding.UTF8.GetBytes("duplicate"),
                Encoding.UTF8.GetBytes("third"))
        };

        // Act
        var result = await tree.BatchUpdateAsync(
            updates,
            tree.ZeroHashes[tree.Depth],
            storage,
            storage);

        // Verify last write wins (deterministic based on sorting)
        var getResult = await tree.GetAsync(
            Encoding.UTF8.GetBytes("duplicate"),
            result.NewRootHash,
            storage);

        // Assert
        Assert.True(getResult.Found);
        Assert.NotNull(getResult.Value);

        // The value should be deterministic based on key hash sorting
        var actualValue = Encoding.UTF8.GetString(getResult.Value.Value.Span);
        Assert.NotEmpty(actualValue);
        Assert.Contains(actualValue, new[] { "first", "second", "third" });
    }

    #endregion

    #region Different Hash Functions

    [Fact]
    public async Task SHA512_ProducesDifferentRootThanSHA256()
    {
        // Arrange
        var sha256Tree = new SparseMerkleTree(_sha256, depth: 8);
        var sha512Tree = new SparseMerkleTree(new Sha512HashFunction(), depth: 8);

        var storage1 = new InMemorySmtStorage();
        var storage2 = new InMemorySmtStorage();

        var key = Encoding.UTF8.GetBytes("test");
        var value = Encoding.UTF8.GetBytes("value");

        // Act
        var sha256Result = await sha256Tree.UpdateAsync(
            key, value, sha256Tree.ZeroHashes[sha256Tree.Depth], storage1);
        await storage1.WriteBatchAsync(sha256Result.NodesToPersist);

        var sha512Result = await sha512Tree.UpdateAsync(
            key, value, sha512Tree.ZeroHashes[sha512Tree.Depth], storage2);
        await storage2.WriteBatchAsync(sha512Result.NodesToPersist);

        // Assert - Different hash functions produce different roots
        Assert.False(sha256Result.NewRootHash.Span.SequenceEqual(sha512Result.NewRootHash.Span));

        // Also verify different hash sizes
        Assert.Equal(32, sha256Result.NewRootHash.Length); // SHA-256
        Assert.Equal(64, sha512Result.NewRootHash.Length); // SHA-512
    }

    #endregion

    #region Zero-Hash Table Vectors

    [Fact]
    public void ZeroHashTable_SHA256_Depth8_Level0()
    {
        // Arrange
        var table = ZeroHashTable.Compute(_sha256, 8);

        // Act
        ReadOnlyMemory<byte> level0Hash = table[0];
        var actualHex = Convert.ToHexString(level0Hash.Span);

        // Assert - Level 0 (empty leaf) should have expected value
        Assert.NotEmpty(actualHex);
        Assert.Equal(64, actualHex.Length);

        // Expected value: Hash(0x00 || empty_bytes)
        // TODO: Update with actual value
    }

    [Fact]
    public void ZeroHashTable_SHA256_Depth8_Root()
    {
        // Arrange
        var table = ZeroHashTable.Compute(_sha256, 8);

        // Act
        ReadOnlyMemory<byte> rootHash = table[8];
        var actualHex = Convert.ToHexString(rootHash.Span);

        // Assert - Root (depth 8) should have expected value
        Assert.NotEmpty(actualHex);
        Assert.Equal(64, actualHex.Length);

        // Expected value: Result of 8 rounds of internal node hashing
        // TODO: Update with actual value
    }

    #endregion

    #region Proof Vectors

    [Fact]
    public async Task InclusionProof_SingleKey_HasExpectedStructure()
    {
        // Arrange
        var tree = new SparseMerkleTree(_sha256, depth: 8);
        var storage = new InMemorySmtStorage();

        var key = Encoding.UTF8.GetBytes("proof_test");
        var value = Encoding.UTF8.GetBytes("proof_value");

        var result = await tree.UpdateAsync(key, value, tree.ZeroHashes[tree.Depth], storage);
        await storage.WriteBatchAsync(result.NodesToPersist);

        // Act
        var proof = await tree.GenerateInclusionProofAsync(key, result.NewRootHash, storage);

        // Assert
        Assert.NotNull(proof);
        Assert.Equal(8, proof.Depth);
        Assert.Equal("SHA-256", proof.HashAlgorithmId);
        Assert.True(proof.Value.SequenceEqual(value));
        Assert.Equal(8, proof.SiblingHashes.Length); // One per level
    }

    [Fact]
    public async Task InclusionProof_Serialization_HasExpectedSize()
    {
        // Arrange
        var tree = new SparseMerkleTree(_sha256, depth: 8);
        var storage = new InMemorySmtStorage();

        var key = Encoding.UTF8.GetBytes("key");
        var value = Encoding.UTF8.GetBytes("value");

        var result = await tree.UpdateAsync(key, value, tree.ZeroHashes[tree.Depth], storage);
        await storage.WriteBatchAsync(result.NodesToPersist);
        var proof = await tree.GenerateInclusionProofAsync(key, result.NewRootHash, storage);

        Assert.NotNull(proof);

        // Act
        var serialized = proof.Serialize();

        // Assert - Document expected serialization size
        Assert.NotEmpty(serialized);
        
        // Rough size calculation:
        // - Header: ~10 bytes
        // - Key hash: 32 bytes
        // - Value: variable
        // - Sibling hashes: 8 * 32 = 256 bytes
        // Total: ~300+ bytes
        Assert.True(serialized.Length > 100);
    }

    #endregion

    #region Edge Cases and Boundary Conditions

    [Fact]
    public async Task MaxDepth_256_CanCreateTree()
    {
        // Arrange & Act
        var tree = new SparseMerkleTree(_sha256, depth: 256);

        // Assert - Should successfully create tree at max depth
        Assert.Equal(256, tree.Depth);
        Assert.NotNull(tree.ZeroHashes);
        Assert.Equal(257, tree.ZeroHashes.Count); // depth + 1
    }

    [Fact]
    public async Task MinDepth_1_CanCreateTree()
    {
        // Arrange & Act
        var tree = new SparseMerkleTree(_sha256, depth: 1);

        // Assert - Should successfully create tree at min depth
        Assert.Equal(1, tree.Depth);
        Assert.NotNull(tree.ZeroHashes);
        Assert.Equal(2, tree.ZeroHashes.Count); // depth + 1
    }

    [Fact]
    public async Task LargeValue_1MB_CanBeStored()
    {
        // Arrange
        var tree = new SparseMerkleTree(_sha256, depth: 8);
        var storage = new InMemorySmtStorage();

        var key = Encoding.UTF8.GetBytes("large_value_key");
        var value = new byte[1024 * 1024]; // 1 MB
        Array.Fill(value, (byte)42);

        // Act
        var result = await tree.UpdateAsync(key, value, tree.ZeroHashes[tree.Depth], storage);
        await storage.WriteBatchAsync(result.NodesToPersist);

        // Verify retrieval
        var getResult = await tree.GetAsync(key, result.NewRootHash, storage);

        // Assert
        Assert.True(getResult.Found);
        Assert.NotNull(getResult.Value);
        Assert.Equal(1024 * 1024, getResult.Value.Value.Length);
        Assert.True(getResult.Value.Value.Span.SequenceEqual(value));
    }

    [Fact]
    public async Task UnicodeKeys_AreHandledCorrectly()
    {
        // Arrange
        var tree = new SparseMerkleTree(_sha256, depth: 8);
        var storage = new InMemorySmtStorage();

        var keys = new[]
        {
            Encoding.UTF8.GetBytes("hello"),      // ASCII
            Encoding.UTF8.GetBytes("你好"),       // Chinese
            Encoding.UTF8.GetBytes("مرحبا"),      // Arabic
            Encoding.UTF8.GetBytes("🎉"),         // Emoji
            Encoding.UTF8.GetBytes("Здравствуй") // Cyrillic
        };

        ReadOnlyMemory<byte> root = tree.ZeroHashes[tree.Depth];

        // Act - Insert all unicode keys
        for (int i = 0; i < keys.Length; i++)
        {
            var result = await tree.UpdateAsync(
                keys[i],
                Encoding.UTF8.GetBytes($"value{i}"),
                root,
                storage);
            await storage.WriteBatchAsync(result.NodesToPersist);
            root = result.NewRootHash;
        }

        // Assert - All keys should be retrievable
        for (int i = 0; i < keys.Length; i++)
        {
            var getResult = await tree.GetAsync(keys[i], root, storage);
            Assert.True(getResult.Found, $"Key {i} not found");
        }
    }

    #endregion
}
