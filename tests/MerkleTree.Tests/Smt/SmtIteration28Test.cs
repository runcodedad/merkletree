using MerkleTree.Hashing;
using MerkleTree.Smt;
using MerkleTree.Smt.Persistence;

namespace MerkleTree.Tests.Smt;

/// <summary>
/// Reproduce and debug the iteration 28 failure with 10 keys.
/// </summary>
public class SmtIteration28Test
{
    [Fact]
    public async Task ReproduceIteration28Failure()
    {
        const int RandomSeed = 42;
        const int KeyCount = 10;
        
        // Skip to iteration 28
        var random = new Random(RandomSeed);
        for (int skipIter = 0; skipIter < 28; skipIter++)
        {
            // Generate and discard keys for iterations 0-27
            for (int k = 0; k < KeyCount; k++)
            {
                var skipKey = new byte[random.Next(1, 100)];
                random.NextBytes(skipKey);
                var skipValue = new byte[random.Next(1, 200)];
                random.NextBytes(skipValue);
            }
        }
        
        // Now generate iteration 28's keys
        var keys = new List<byte[]>();
        var values = new List<byte[]>();
        for (int k = 0; k < KeyCount; k++)
        {
            var key = new byte[random.Next(1, 100)];
            random.NextBytes(key);
            keys.Add(key);
            
            var value = new byte[random.Next(1, 200)];
            random.NextBytes(value);
            values.Add(value);
        }
        
        // Setup tree
        var hashFunction = new Sha256HashFunction();
        var tree = new SparseMerkleTree(hashFunction, depth: 8);
        var storage = new InMemorySmtStorage();
        ReadOnlyMemory<byte> root = tree.ZeroHashes[tree.Depth];
        
        // Show key hashes and paths
        Console.WriteLine("=== Iteration 28 Keys ===");
        for (int i = 0; i < KeyCount; i++)
        {
            var keyHash = tree.HashKey(keys[i]);
            var bitPath = tree.GetBitPath(keys[i]);
            var pathStr = string.Join("", bitPath.Select(b => b ? "1" : "0"));
            Console.WriteLine($"Key {i}: hash={Convert.ToHexString(keyHash.Take(16).ToArray())}... path={pathStr}");
        }
        
        // Check for collisions
        var pathGroups = new Dictionary<string, List<int>>();
        for (int i = 0; i < KeyCount; i++)
        {
            var bitPath = tree.GetBitPath(keys[i]);
            var pathStr = string.Join("", bitPath.Select(b => b ? "1" : "0"));
            if (!pathGroups.ContainsKey(pathStr))
            {
                pathGroups[pathStr] = new List<int>();
            }
            pathGroups[pathStr].Add(i);
        }
        
        Console.WriteLine($"\n=== Collision Groups ===");
        foreach (var group in pathGroups.Where(g => g.Value.Count > 1))
        {
            Console.WriteLine($"Path {group.Key}: Keys {string.Join(", ", group.Value)}");
            
            // Show bits 8-16 for these keys
            foreach (var keyIdx in group.Value)
            {
                var keyHash = tree.HashKey(keys[keyIdx]);
                var fullPath = HashUtils.GetBitPath(keyHash, 20);
                var bits8to16 = string.Join("", fullPath.Skip(8).Take(8).Select(b => b ? "1" : "0"));
                Console.WriteLine($"  Key {keyIdx} bits 8-16: {bits8to16}");
            }
        }
        
        // Insert all keys
        Console.WriteLine($"\n=== Inserting Keys ===");
        for (int i = 0; i < KeyCount; i++)
        {
            Console.WriteLine($"Inserting key {i}...");
            var result = await tree.UpdateAsync(keys[i], values[i], root, storage);
            await storage.WriteBatchAsync(result.NodesToPersist);
            root = result.NewRootHash;
            Console.WriteLine($"  After insert: {storage.NodeCount} nodes, root={Convert.ToHexString(root.ToArray().Take(8).ToArray())}...");
            
            // Immediately verify this key
            var immediateGet = await tree.GetAsync(keys[i], root, storage);
            if (!immediateGet.Found)
            {
                Console.WriteLine($"  WARNING: Key {i} NOT found immediately after insert!");
            }
        }
        
        // Retrieve all keys
        Console.WriteLine($"\n=== Retrieving All Keys ===");
        for (int i = 0; i < KeyCount; i++)
        {
            var getResult = await tree.GetAsync(keys[i], root, storage);
            Console.WriteLine($"Key {i}: Found={getResult.Found}");
            if (!getResult.Found)
            {
                Console.WriteLine($"  FAILED TO RETRIEVE KEY {i}!");
            }
            Assert.True(getResult.Found, $"Key {i} should be retrievable");
        }
    }
}
