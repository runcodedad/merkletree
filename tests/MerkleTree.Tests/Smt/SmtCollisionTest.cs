using System.Text;
using MerkleTree.Hashing;
using MerkleTree.Smt;
using MerkleTree.Smt.Persistence;

namespace MerkleTree.Tests.Smt;

/// <summary>
/// Tests for SMT handling of keys with identical depth-bit prefixes.
/// </summary>
public class SmtCollisionTest
{
    [Fact]
    public async Task TwoKeysWithIdenticalDepthBitPrefix_BothShouldBeRetrievable()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var tree = new SparseMerkleTree(hashFunction, depth: 8);
        var storage = new InMemorySmtStorage();
        
        // These specific keys from the random seed 42 sequence have identical first 8 bits
        // Key 1 hash: A1F70CDFEF5CED742AFBC9C63A997A39F41E1AD2F8E9E0C66619AF7B79C95A48
        // Key 2 hash: A17A9694BE3C340360FE029E6C909375CDF9A80A100F1DDD64449620A6ACE188
        // Both have bit path: 10100001 for first 8 bits
        // They diverge at bit 8: Key 1 has 1, Key 2 has 0
        
        var random = new Random(42);
        // Skip first key
        var skipKey = new byte[random.Next(1, 100)];
        random.NextBytes(skipKey);
        var skipValue = new byte[random.Next(1, 200)];
        random.NextBytes(skipValue);
        
        // Get Key 1
        var key1 = new byte[random.Next(1, 100)];
        random.NextBytes(key1);
        var value1 = new byte[random.Next(1, 200)];
        random.NextBytes(value1);
        
        // Get Key 2
        var key2 = new byte[random.Next(1, 100)];
        random.NextBytes(key2);
        var value2 = new byte[random.Next(1, 200)];
        random.NextBytes(value2);
        
        var key1Hash = tree.HashKey(key1);
        var key2Hash = tree.HashKey(key2);
        var key1Path = tree.GetBitPath(key1);
        var key2Path = tree.GetBitPath(key2);
        
        Console.WriteLine($"Key 1 hash: {Convert.ToHexString(key1Hash)}");
        Console.WriteLine($"Key 2 hash: {Convert.ToHexString(key2Hash)}");
        Console.WriteLine($"Key 1 path ({tree.Depth} bits): {string.Join("", key1Path.Select(b => b ? "1" : "0"))}");
        Console.WriteLine($"Key 2 path ({tree.Depth} bits): {string.Join("", key2Path.Select(b => b ? "1" : "0"))}");
        
        // Verify they have identical depth-bits (all 8 bits match)
        bool allMatch = true;
        for (int i = 0; i < tree.Depth; i++)
        {
            if (key1Path[i] != key2Path[i])
            {
                allMatch = false;
                Console.WriteLine($"Paths diverge at bit {i}");
                break;
            }
        }
        
        if (allMatch)
        {
            Console.WriteLine($"WARNING: Keys have IDENTICAL {tree.Depth}-bit prefixes!");
            Console.WriteLine("This tests the collision scenario where keys cannot be distinguished at this depth.");
        }
        
        ReadOnlyMemory<byte> root = tree.ZeroHashes[tree.Depth];
        
        // Act - Insert both keys
        Console.WriteLine("\n=== Inserting Key 1 ===");
        var result1 = await tree.UpdateAsync(key1, value1, root, storage);
        await storage.WriteBatchAsync(result1.NodesToPersist);
        root = result1.NewRootHash;
        Console.WriteLine($"After Key 1: storage has {storage.NodeCount} nodes");
        
        var get1Immediate = await tree.GetAsync(key1, root, storage);
        Console.WriteLine($"Key 1 immediate retrieval: {get1Immediate.Found}");
        Assert.True(get1Immediate.Found, "Key 1 should be retrievable immediately after insert");
        
        Console.WriteLine("\n=== Inserting Key 2 ===");
        var result2 = await tree.UpdateAsync(key2, value2, root, storage);
        await storage.WriteBatchAsync(result2.NodesToPersist);
        root = result2.NewRootHash;
        Console.WriteLine($"After Key 2: storage has {storage.NodeCount} nodes");
        
        var get2Immediate = await tree.GetAsync(key2, root, storage);
        Console.WriteLine($"Key 2 immediate retrieval: {get2Immediate.Found}");
        Assert.True(get2Immediate.Found, "Key 2 should be retrievable immediately after insert");
        
        // Assert - Both keys should still be retrievable
        Console.WriteLine("\n=== Final Retrieval ===");
        var get1Final = await tree.GetAsync(key1, root, storage);
        Console.WriteLine($"Key 1 final retrieval: {get1Final.Found}");
        
        var get2Final = await tree.GetAsync(key2, root, storage);
        Console.WriteLine($"Key 2 final retrieval: {get2Final.Found}");
        
        // This is the bug: Key 1 becomes unretrievable after Key 2 insert
        Assert.True(get1Final.Found, "Key 1 should still be retrievable after Key 2 insert (BUG: currently fails)");
        Assert.True(get2Final.Found, "Key 2 should be retrievable after insert");
    }
}
