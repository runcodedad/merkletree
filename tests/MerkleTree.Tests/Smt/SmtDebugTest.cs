using System.Text;
using MerkleTree.Hashing;
using MerkleTree.Smt;
using MerkleTree.Smt.Persistence;

namespace MerkleTree.Tests.Smt;

public class SmtDebugTest
{
    [Fact]
    public async Task Debug_SingleInsert_ShouldBeRetrievable()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var tree = new SparseMerkleTree(hashFunction, depth: 8);
        var storage = new InMemorySmtStorage();
        
        // Use first random key from Property test with seed 42
        var random = new Random(42);
        var key = new byte[random.Next(1, 100)];
        random.NextBytes(key);
        var value = new byte[random.Next(1, 200)];
        random.NextBytes(value);
        
        Console.WriteLine($"Key length: {key.Length}, Value length: {value.Length}");
        Console.WriteLine($"Key: {Convert.ToHexString(key)}");
        Console.WriteLine($"Value: {Convert.ToHexString(value)}");
        
        ReadOnlyMemory<byte> root = tree.ZeroHashes[tree.Depth];
        Console.WriteLine($"Empty root: {Convert.ToHexString(root.Span)}");
        
        // Act - Insert
        var updateResult = await tree.UpdateAsync(key, value, root, storage);
        Console.WriteLine($"New root: {Convert.ToHexString(updateResult.NewRootHash.Span)}");
        Console.WriteLine($"Nodes to persist: {updateResult.NodesToPersist.Count}");
        
        await storage.WriteBatchAsync(updateResult.NodesToPersist);
        Console.WriteLine($"Storage node count: {storage.NodeCount}");
        
        // Try to retrieve
        var getResult = await tree.GetAsync(key, updateResult.NewRootHash, storage);
        
        Console.WriteLine($"Found: {getResult.Found}");
        
        // Assert
        Assert.True(getResult.Found, "Key should be found after insert");
        Assert.NotNull(getResult.Value);
        Assert.True(getResult.Value.Value.Span.SequenceEqual(value));
    }

    [Fact]
    public async Task Debug_TwoInserts_BothShouldBeRetrievable()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var tree = new SparseMerkleTree(hashFunction, depth: 8);
        var storage = new InMemorySmtStorage();
        
        // Generate first 2 random keys/values with seed 42 - mimicking the failing test
        var random = new Random(42);
        var keys = new List<byte[]>();
        var values = new List<byte[]>();
        
        for (int i = 0; i < 2; i++)
        {
            var key = new byte[random.Next(1, 100)];
            random.NextBytes(key);
            var value = new byte[random.Next(1, 200)];
            random.NextBytes(value);
            keys.Add(key);
            values.Add(value);
        }
        
        ReadOnlyMemory<byte> root = tree.ZeroHashes[tree.Depth];
        
        // Act - Insert both keys
        for (int i = 0; i < 2; i++)
        {
            Console.WriteLine($"\n=== Inserting key {i} ===");
            Console.WriteLine($"Key: {Convert.ToHexString(keys[i])}");
            Console.WriteLine($"Value: {Convert.ToHexString(values[i])}");
            Console.WriteLine($"Current root: {Convert.ToHexString(root.Span)}");
            
            var updateResult = await tree.UpdateAsync(keys[i], values[i], root, storage);
            Console.WriteLine($"New root: {Convert.ToHexString(updateResult.NewRootHash.Span)}");
            Console.WriteLine($"Nodes to persist: {updateResult.NodesToPersist.Count}");
            
            await storage.WriteBatchAsync(updateResult.NodesToPersist);
            root = updateResult.NewRootHash;
            Console.WriteLine($"Storage node count: {storage.NodeCount}");
        }
        
        // Assert - Try to retrieve both keys
        for (int i = 0; i < 2; i++)
        {
            Console.WriteLine($"\n=== Retrieving key {i} ===");
            var getResult = await tree.GetAsync(keys[i], root, storage);
            Console.WriteLine($"Found: {getResult.Found}");
            
            Assert.True(getResult.Found, $"Key {i} should be found after insert");
            Assert.NotNull(getResult.Value);
            Assert.True(getResult.Value.Value.Span.SequenceEqual(values[i]), $"Value {i} should match");
        }
    }

    [Fact]
    public async Task Debug_FiveInserts_AllShouldBeRetrievable()
    {
        // Arrange - Exact replication of the failing property test
        var hashFunction = new Sha256HashFunction();
        var tree = new SparseMerkleTree(hashFunction, depth: 8);
        var storage = new InMemorySmtStorage();
        
        // Generate first 5 random keys/values with seed 42 - mimicking the failing test
        var random = new Random(42);
        var keys = new List<byte[]>();
        var values = new List<byte[]>();
        
        for (int i = 0; i < 5; i++)
        {
            var key = new byte[random.Next(1, 100)];
            random.NextBytes(key);
            var value = new byte[random.Next(1, 200)];
            random.NextBytes(value);
            keys.Add(key);
            values.Add(value);
        }
        
        ReadOnlyMemory<byte> root = tree.ZeroHashes[tree.Depth];
        
        // Act - Insert all 5 keys
        for (int i = 0; i < 5; i++)
        {
            Console.WriteLine($"\n=== Inserting key {i} ===");
            Console.WriteLine($"Key: {Convert.ToHexString(keys[i])}");
            var keyHash = tree.HashKey(keys[i]);
            Console.WriteLine($"Key hash: {Convert.ToHexString(keyHash)}");
            var bitPath = tree.GetBitPath(keys[i]);
            Console.WriteLine($"Bit path (first 8 bits): {string.Join("", bitPath.Take(8).Select(b => b ? "1" : "0"))}");
            
            var updateResult = await tree.UpdateAsync(keys[i], values[i], root, storage);
            Console.WriteLine($"New root: {Convert.ToHexString(updateResult.NewRootHash.Span)}");
            Console.WriteLine($"Nodes to persist: {updateResult.NodesToPersist.Count}");
            
            await storage.WriteBatchAsync(updateResult.NodesToPersist);
            root = updateResult.NewRootHash;
            Console.WriteLine($"Storage node count: {storage.NodeCount}");
            
            // Immediately try to retrieve the just-inserted key
            var immediateGet = await tree.GetAsync(keys[i], root, storage);
            Console.WriteLine($"Immediate retrieval: {immediateGet.Found}");
        }
        
        Console.WriteLine($"\n=== After all inserts ===");
        Console.WriteLine($"Final storage node count: {storage.NodeCount}");
        Console.WriteLine($"Final root: {Convert.ToHexString(root.Span)}");
        
        // Assert - Try to retrieve all 5 keys
        for (int i = 0; i < 5; i++)
        {
            Console.WriteLine($"\n=== Retrieving key {i} ===");
            Console.WriteLine($"Key: {Convert.ToHexString(keys[i])}");
            var getResult = await tree.GetAsync(keys[i], root, storage);
            Console.WriteLine($"Found: {getResult.Found}");
            
            Assert.True(getResult.Found, $"Key {i} should be found after insert");
            if (getResult.Found)
            {
                Assert.NotNull(getResult.Value);
                Assert.True(getResult.Value.Value.Span.SequenceEqual(values[i]), $"Value {i} should match");
            }
        }
    }
}
