using System.Text;
using Xunit;
using MerkleTree.Exceptions;
using MerkleTree.Hashing;
using MerkleTree.Proofs;
using MerkleTree.Smt;
using MerkleTree.Smt.Persistence;

namespace MerkleTree.Tests.Proofs;

/// <summary>
/// Tests for Sparse Merkle Tree proof generation and verification.
/// </summary>
public class SmtProofTests
{
    private readonly IHashFunction _hashFunction;
    private readonly SparseMerkleTree _smt;

    public SmtProofTests()
    {
        _hashFunction = new Sha256HashFunction();
        _smt = new SparseMerkleTree(_hashFunction, depth: 8);
    }

    [Fact]
    public async Task GenerateInclusionProof_SingleKeyValue_GeneratesValidProof()
    {
        // Arrange
        var storage = new InMemorySmtStorage();
        var key = Encoding.UTF8.GetBytes("key1");
        var value = Encoding.UTF8.GetBytes("value1");

        // Insert key-value pair
        var updateResult = await _smt.UpdateAsync(key, value, _smt.ZeroHashes[_smt.Depth], storage);
        await storage.WriteBatchAsync(updateResult.NodesToPersist);

        // Act
        var proof = await _smt.GenerateInclusionProofAsync(key, updateResult.NewRootHash, storage);

        // Assert
        Assert.NotNull(proof);
        Assert.Equal(value, proof.Value);
        Assert.Equal(8, proof.Depth);
        Assert.False(proof.IsCompressed);
        
        // Verify the proof
        bool isValid = proof.Verify(updateResult.NewRootHash.ToArray(), _hashFunction, _smt.ZeroHashes);
        Assert.True(isValid);
    }

    [Fact]
    public async Task GenerateInclusionProof_WithCompression_OmitsZeroHashes()
    {
        // Arrange
        var storage = new InMemorySmtStorage();
        var key = Encoding.UTF8.GetBytes("key1");
        var value = Encoding.UTF8.GetBytes("value1");

        // Insert key-value pair
        var updateResult = await _smt.UpdateAsync(key, value, _smt.ZeroHashes[_smt.Depth], storage);
        await storage.WriteBatchAsync(updateResult.NodesToPersist);

        // Act - Generate compressed proof
        var proof = await _smt.GenerateInclusionProofAsync(key, updateResult.NewRootHash, storage, compress: true);

        // Assert
        Assert.NotNull(proof);
        Assert.True(proof.IsCompressed);
        
        // Compressed proof should have fewer siblings than uncompressed
        // (most siblings should be zero-hashes in a sparse tree)
        Assert.True(proof.SiblingHashes.Length < proof.Depth);
        
        // Verify the compressed proof still works
        bool isValid = proof.Verify(updateResult.NewRootHash.ToArray(), _hashFunction, _smt.ZeroHashes);
        Assert.True(isValid);
    }

    [Fact]
    public async Task GenerateInclusionProof_MultipleKeys_GeneratesValidProofs()
    {
        // Arrange
        var storage = new InMemorySmtStorage();
        var keys = new[] { "key1", "key2", "key3" };
        var values = new[] { "value1", "value2", "value3" };

        // Insert multiple key-value pairs
        ReadOnlyMemory<byte> rootHash = _smt.ZeroHashes[_smt.Depth];
        for (int i = 0; i < keys.Length; i++)
        {
            var key = Encoding.UTF8.GetBytes(keys[i]);
            var value = Encoding.UTF8.GetBytes(values[i]);
            var updateResult = await _smt.UpdateAsync(key, value, rootHash, storage);
            await storage.WriteBatchAsync(updateResult.NodesToPersist);
            rootHash = updateResult.NewRootHash;
        }

        // Act & Assert - Generate and verify proofs for all keys
        for (int i = 0; i < keys.Length; i++)
        {
            var key = Encoding.UTF8.GetBytes(keys[i]);
            var value = Encoding.UTF8.GetBytes(values[i]);
            var proof = await _smt.GenerateInclusionProofAsync(key, rootHash, storage);

            Assert.NotNull(proof);
            Assert.Equal(value, proof.Value);
            
            bool isValid = proof.Verify(rootHash.ToArray(), _hashFunction, _smt.ZeroHashes);
            Assert.True(isValid, $"Proof for key{i + 1} should be valid");
        }
    }

    [Fact]
    public async Task GenerateInclusionProof_NonExistentKey_ReturnsNull()
    {
        // Arrange
        var storage = new InMemorySmtStorage();
        var key1 = Encoding.UTF8.GetBytes("key1");
        var value1 = Encoding.UTF8.GetBytes("value1");

        var updateResult = await _smt.UpdateAsync(key1, value1, _smt.ZeroHashes[_smt.Depth], storage);
        await storage.WriteBatchAsync(updateResult.NodesToPersist);

        // Act - Try to generate proof for a key that doesn't exist
        var nonExistentKey = Encoding.UTF8.GetBytes("nonexistent");
        var proof = await _smt.GenerateInclusionProofAsync(nonExistentKey, updateResult.NewRootHash, storage);

        // Assert
        Assert.Null(proof);
    }

    [Fact]
    public async Task GenerateNonInclusionProof_NonExistentKey_GeneratesValidEmptyPathProof()
    {
        // Arrange
        var storage = new InMemorySmtStorage();
        var key1 = Encoding.UTF8.GetBytes("key1");
        var value1 = Encoding.UTF8.GetBytes("value1");

        var updateResult = await _smt.UpdateAsync(key1, value1, _smt.ZeroHashes[_smt.Depth], storage);
        await storage.WriteBatchAsync(updateResult.NodesToPersist);

        // Act - Generate non-inclusion proof for a key that doesn't exist
        var nonExistentKey = Encoding.UTF8.GetBytes("nonexistent");
        var proof = await _smt.GenerateNonInclusionProofAsync(nonExistentKey, updateResult.NewRootHash, storage);

        // Assert
        Assert.NotNull(proof);
        Assert.Equal(8, proof.Depth);
        
        // Verify the proof
        bool isValid = proof.Verify(updateResult.NewRootHash.ToArray(), _hashFunction, _smt.ZeroHashes);
        Assert.True(isValid);
    }

    [Fact]
    public async Task GenerateNonInclusionProof_ExistingKey_ReturnsNull()
    {
        // Arrange
        var storage = new InMemorySmtStorage();
        var key = Encoding.UTF8.GetBytes("key1");
        var value = Encoding.UTF8.GetBytes("value1");

        var updateResult = await _smt.UpdateAsync(key, value, _smt.ZeroHashes[_smt.Depth], storage);
        await storage.WriteBatchAsync(updateResult.NodesToPersist);

        // Act - Try to generate non-inclusion proof for an existing key
        var proof = await _smt.GenerateNonInclusionProofAsync(key, updateResult.NewRootHash, storage);

        // Assert
        Assert.Null(proof);
    }

    [Fact]
    public async Task InclusionProof_Serialization_RoundTrip()
    {
        // Arrange
        var storage = new InMemorySmtStorage();
        var key = Encoding.UTF8.GetBytes("key1");
        var value = Encoding.UTF8.GetBytes("value1");

        var updateResult = await _smt.UpdateAsync(key, value, _smt.ZeroHashes[_smt.Depth], storage);
        await storage.WriteBatchAsync(updateResult.NodesToPersist);
        var proof = await _smt.GenerateInclusionProofAsync(key, updateResult.NewRootHash, storage);
        Assert.NotNull(proof);

        // Act - Serialize and deserialize
        var serialized = proof.Serialize();
        var deserialized = SmtInclusionProof.Deserialize(serialized);

        // Assert
        Assert.Equal(proof.KeyHash, deserialized.KeyHash);
        Assert.Equal(proof.Value, deserialized.Value);
        Assert.Equal(proof.Depth, deserialized.Depth);
        Assert.Equal(proof.HashAlgorithmId, deserialized.HashAlgorithmId);
        Assert.Equal(proof.IsCompressed, deserialized.IsCompressed);
        Assert.Equal(proof.SiblingHashes.Length, deserialized.SiblingHashes.Length);
        
        // Deserialized proof should still verify
        bool isValid = deserialized.Verify(updateResult.NewRootHash.ToArray(), _hashFunction, _smt.ZeroHashes);
        Assert.True(isValid);
    }

    [Fact]
    public async Task NonInclusionProof_Serialization_RoundTrip()
    {
        // Arrange
        var storage = new InMemorySmtStorage();
        var key1 = Encoding.UTF8.GetBytes("key1");
        var value1 = Encoding.UTF8.GetBytes("value1");

        var updateResult = await _smt.UpdateAsync(key1, value1, _smt.ZeroHashes[_smt.Depth], storage);
        await storage.WriteBatchAsync(updateResult.NodesToPersist);
        
        var nonExistentKey = Encoding.UTF8.GetBytes("nonexistent");
        var proof = await _smt.GenerateNonInclusionProofAsync(nonExistentKey, updateResult.NewRootHash, storage);
        Assert.NotNull(proof);

        // Act - Serialize and deserialize
        var serialized = proof.Serialize();
        var deserialized = SmtNonInclusionProof.Deserialize(serialized);

        // Assert
        Assert.Equal(proof.KeyHash, deserialized.KeyHash);
        Assert.Equal(proof.Depth, deserialized.Depth);
        Assert.Equal(proof.HashAlgorithmId, deserialized.HashAlgorithmId);
        Assert.Equal(proof.IsCompressed, deserialized.IsCompressed);
        Assert.Equal(proof.ProofType, deserialized.ProofType);
        
        // Deserialized proof should still verify
        bool isValid = deserialized.Verify(updateResult.NewRootHash.ToArray(), _hashFunction, _smt.ZeroHashes);
        Assert.True(isValid);
    }

    [Fact]
    public async Task InclusionProof_WithModifiedValue_FailsVerification()
    {
        // Arrange
        var storage = new InMemorySmtStorage();
        var key = Encoding.UTF8.GetBytes("key1");
        var value = Encoding.UTF8.GetBytes("value1");

        var updateResult = await _smt.UpdateAsync(key, value, _smt.ZeroHashes[_smt.Depth], storage);
        await storage.WriteBatchAsync(updateResult.NodesToPersist);
        var proof = await _smt.GenerateInclusionProofAsync(key, updateResult.NewRootHash, storage);
        Assert.NotNull(proof);

        // Act - Create modified proof with different value
        var modifiedProof = new SmtInclusionProof(
            proof.KeyHash,
            Encoding.UTF8.GetBytes("modified_value"),
            proof.Depth,
            proof.HashAlgorithmId,
            proof.SiblingHashes,
            proof.SiblingBitmask,
            proof.IsCompressed);

        // Assert - Modified proof should fail verification
        bool isValid = modifiedProof.Verify(updateResult.NewRootHash.ToArray(), _hashFunction, _smt.ZeroHashes);
        Assert.False(isValid);
    }

    [Fact]
    public async Task InclusionProof_WithWrongRootHash_FailsVerification()
    {
        // Arrange
        var storage = new InMemorySmtStorage();
        var key = Encoding.UTF8.GetBytes("key1");
        var value = Encoding.UTF8.GetBytes("value1");

        var updateResult = await _smt.UpdateAsync(key, value, _smt.ZeroHashes[_smt.Depth], storage);
        await storage.WriteBatchAsync(updateResult.NodesToPersist);
        var proof = await _smt.GenerateInclusionProofAsync(key, updateResult.NewRootHash, storage);
        Assert.NotNull(proof);

        // Act - Verify against a different root hash
        var wrongRootHash = _smt.ZeroHashes[_smt.Depth];
        bool isValid = proof.Verify(wrongRootHash, _hashFunction, _smt.ZeroHashes);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void InclusionProof_Deserialize_InvalidData_ThrowsMalformedProofException()
    {
        // Arrange - Create invalid data (too short)
        var invalidData = new byte[] { 1, 2, 3 };

        // Act & Assert
        var exception = Assert.Throws<MalformedProofException>(() => SmtInclusionProof.Deserialize(invalidData));
        Assert.Equal("INSUFFICIENT_DATA", exception.ErrorCode);
    }

    [Fact]
    public void InclusionProof_Deserialize_WrongProofType_ThrowsMalformedProofException()
    {
        // Arrange - Create data with wrong proof type
        var invalidData = new byte[100];
        invalidData[0] = 1; // version
        invalidData[1] = 0x02; // wrong proof type (0x02 is non-inclusion)
        invalidData[2] = 0; // flags

        // Act & Assert
        var exception = Assert.Throws<MalformedProofException>(() => SmtInclusionProof.Deserialize(invalidData));
        Assert.Equal("INVALID_PROOF_TYPE", exception.ErrorCode);
    }

    [Fact]
    public void NonInclusionProof_Deserialize_InvalidData_ThrowsMalformedProofException()
    {
        // Arrange
        var invalidData = new byte[] { 1 };

        // Act & Assert
        var exception = Assert.Throws<MalformedProofException>(() => SmtNonInclusionProof.Deserialize(invalidData));
        Assert.Equal("INSUFFICIENT_DATA", exception.ErrorCode);
    }

    [Fact]
    public async Task CompressedProof_HasSmallerSize_ThanUncompressed()
    {
        // Arrange
        var storage = new InMemorySmtStorage();
        var key = Encoding.UTF8.GetBytes("key1");
        var value = Encoding.UTF8.GetBytes("value1");

        var updateResult = await _smt.UpdateAsync(key, value, _smt.ZeroHashes[_smt.Depth], storage);
        await storage.WriteBatchAsync(updateResult.NodesToPersist);

        // Act - Generate both compressed and uncompressed proofs
        var uncompressedProof = await _smt.GenerateInclusionProofAsync(key, updateResult.NewRootHash, storage, compress: false);
        var compressedProof = await _smt.GenerateInclusionProofAsync(key, updateResult.NewRootHash, storage, compress: true);

        Assert.NotNull(uncompressedProof);
        Assert.NotNull(compressedProof);

        // Serialize both
        var uncompressedSize = uncompressedProof.Serialize().Length;
        var compressedSize = compressedProof.Serialize().Length;

        // Assert - Compressed proof should be smaller
        Assert.True(compressedSize < uncompressedSize, 
            $"Compressed size ({compressedSize}) should be less than uncompressed size ({uncompressedSize})");

        // Both should still verify
        Assert.True(uncompressedProof.Verify(updateResult.NewRootHash.ToArray(), _hashFunction, _smt.ZeroHashes));
        Assert.True(compressedProof.Verify(updateResult.NewRootHash.ToArray(), _hashFunction, _smt.ZeroHashes));
    }

    [Fact]
    public async Task InclusionProof_EmptyTree_KeyNotFound()
    {
        // Arrange
        var storage = new InMemorySmtStorage();
        var emptyRootHash = _smt.ZeroHashes[_smt.Depth];
        var key = Encoding.UTF8.GetBytes("key1");

        // Act - Try to generate proof for empty tree
        var proof = await _smt.GenerateInclusionProofAsync(key, emptyRootHash, storage);

        // Assert
        Assert.Null(proof);
    }

    [Fact]
    public async Task NonInclusionProof_EmptyTree_GeneratesValidProof()
    {
        // Arrange
        var storage = new InMemorySmtStorage();
        var emptyRootHash = _smt.ZeroHashes[_smt.Depth];
        var key = Encoding.UTF8.GetBytes("key1");

        // Act - Generate non-inclusion proof for empty tree
        var proof = await _smt.GenerateNonInclusionProofAsync(key, emptyRootHash, storage);

        // Assert
        Assert.NotNull(proof);
        Assert.Equal(NonInclusionProofType.EmptyPath, proof.ProofType);
        
        // Verify the proof
        bool isValid = proof.Verify(emptyRootHash, _hashFunction, _smt.ZeroHashes);
        Assert.True(isValid);
    }

    [Fact]
    public void InclusionProof_VerifyWithWrongHashFunction_ThrowsArgumentException()
    {
        // Arrange
        var keyHash = new byte[32];
        var value = Encoding.UTF8.GetBytes("value1");
        var siblingHashes = new byte[8][];
        for (int i = 0; i < 8; i++)
            siblingHashes[i] = new byte[32];
        var bitmask = new byte[1];
        for (int i = 0; i < 8; i++)
            SmtProof.SetBit(bitmask, i, true);

        var proof = new SmtInclusionProof(
            keyHash,
            value,
            8,
            "SHA-256",
            siblingHashes,
            bitmask,
            false);

        var rootHash = new byte[32];
        var wrongHashFunction = new Sha512HashFunction(); // Different hash function
        var zeroHashes = ZeroHashTable.Compute(new Sha256HashFunction(), 8);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            proof.Verify(rootHash, wrongHashFunction, zeroHashes));
        Assert.Contains("Hash function", exception.Message);
    }

    [Fact]
    public void InclusionProof_VerifyWithWrongDepth_ThrowsArgumentException()
    {
        // Arrange
        var keyHash = new byte[32];
        var value = Encoding.UTF8.GetBytes("value1");
        var siblingHashes = new byte[8][];
        for (int i = 0; i < 8; i++)
            siblingHashes[i] = new byte[32];
        var bitmask = new byte[1];
        for (int i = 0; i < 8; i++)
            SmtProof.SetBit(bitmask, i, true);

        var proof = new SmtInclusionProof(
            keyHash,
            value,
            8,
            "SHA-256",
            siblingHashes,
            bitmask,
            false);

        var rootHash = new byte[32];
        var hashFunction = new Sha256HashFunction();
        var wrongDepthZeroHashes = ZeroHashTable.Compute(hashFunction, 16); // Different depth

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            proof.Verify(rootHash, hashFunction, wrongDepthZeroHashes));
        Assert.Contains("depth", exception.Message);
    }
}
