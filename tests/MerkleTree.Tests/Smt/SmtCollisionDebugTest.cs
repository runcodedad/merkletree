using System.Text;
using MerkleTree.Hashing;
using MerkleTree.Smt;
using MerkleTree.Smt.Persistence;

namespace MerkleTree.Tests.Smt;

/// <summary>
/// Detailed debugging test for SMT collision handling.
/// </summary>
public class SmtCollisionDebugTest
{
    [Fact]
    public async Task DebugTwoKeysCollision_DetailedTrace()
    {
        // Arrange
        var hashFunction = new Sha256HashFunction();
        var tree = new SparseMerkleTree(hashFunction, depth: 8);
        var storage = new InMemorySmtStorage();
        
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
        
        // Get full bit paths (256 bits) for both keys
        var key1FullPath = HashUtils.GetBitPath(key1Hash, 256);
        var key2FullPath = HashUtils.GetBitPath(key2Hash, 256);
        
        Console.WriteLine($"Key 1 hash: {Convert.ToHexString(key1Hash)}");
        Console.WriteLine($"Key 2 hash: {Convert.ToHexString(key2Hash)}");
        Console.WriteLine($"Key 1 path (first 16 bits): {string.Join("", key1FullPath.Take(16).Select(b => b ? "1" : "0"))}");
        Console.WriteLine($"Key 2 path (first 16 bits): {string.Join("", key2FullPath.Take(16).Select(b => b ? "1" : "0"))}");
        
        int divergeAt = -1;
        for (int i = 0; i < 256; i++)
        {
            if (key1FullPath[i] != key2FullPath[i])
            {
                divergeAt = i;
                break;
            }
        }
        Console.WriteLine($"Paths diverge at bit: {divergeAt}");
        Console.WriteLine($"Key 1 bit {divergeAt}: {(key1FullPath[divergeAt] ? "1" : "0")}");
        Console.WriteLine($"Key 2 bit {divergeAt}: {(key2FullPath[divergeAt] ? "1" : "0")}");
        
        ReadOnlyMemory<byte> root = tree.ZeroHashes[tree.Depth];
        
        // Insert Key 1
        Console.WriteLine("\n=== Inserting Key 1 ===");
        var result1 = await tree.UpdateAsync(key1, value1, root, storage);
        await storage.WriteBatchAsync(result1.NodesToPersist);
        root = result1.NewRootHash;
        Console.WriteLine($"After Key 1: {storage.NodeCount} nodes in storage");
        Console.WriteLine($"Root hash: {Convert.ToHexString(root.ToArray())}");
        Console.WriteLine($"Nodes persisted: {result1.NodesToPersist.Count}");
        
        // Insert Key 2
        Console.WriteLine("\n=== Inserting Key 2 ===");
        var result2 = await tree.UpdateAsync(key2, value2, root, storage);
        Console.WriteLine($"Nodes to persist for Key 2: {result2.NodesToPersist.Count}");
        
        // Before writing, let's see what nodes are being persisted
        for (int i = 0; i < result2.NodesToPersist.Count; i++)
        {
            var node = result2.NodesToPersist[i];
            var pathStr = node.Path.HasValue ? string.Join("", node.Path.Value.ToArray().Select(b => b ? "1" : "0")) : "null";
            Console.WriteLine($"  Node {i}: hash={Convert.ToHexString(node.Hash.ToArray().Take(8).ToArray())}... path={pathStr}");
        }
        
        await storage.WriteBatchAsync(result2.NodesToPersist);
        root = result2.NewRootHash;
        Console.WriteLine($"After Key 2: {storage.NodeCount} nodes in storage");
        Console.WriteLine($"Root hash: {Convert.ToHexString(root.ToArray())}");
        
        // Try to manually traverse to see what's happening
        Console.WriteLine("\n=== Manual Traversal for Key 1 ===");
        Console.WriteLine($"Following path: {string.Join("", key1FullPath.Take(16).Select(b => b ? "1" : "0"))}");
        Console.WriteLine($"Root hash: {Convert.ToHexString(root.ToArray().Take(8).ToArray())}...");
        Console.WriteLine($"\nStorage contents: {storage.NodeCount} nodes");
        
        // Just count nodes by path length to understand structure
        var allNodes = new List<SmtNodeBlob>();
        for (int i = 0; i < result1.NodesToPersist.Count; i++)
        {
            allNodes.Add(result1.NodesToPersist[i]);
        }
        for (int i = 0; i < result2.NodesToPersist.Count; i++)
        {
            allNodes.Add(result2.NodesToPersist[i]);
        }
        
        var pathLengths = allNodes.Where(n => n.Path.HasValue).GroupBy(n => n.Path.Value.Length).OrderBy(g => g.Key);
        foreach (var group in pathLengths)
        {
            Console.WriteLine($"  {group.Count()} nodes with path length {group.Key}");
        }
        
        Console.WriteLine($"\nExtension nodes (path length > {tree.Depth}): {allNodes.Count(n => n.Path.HasValue && n.Path.Value.Length > tree.Depth)}");
        
        var get1 = await tree.GetAsync(key1, root, storage);
        var get2 = await tree.GetAsync(key2, root, storage);
        
        Console.WriteLine($"\nKey 1 found: {get1.Found}");
        Console.WriteLine($"Key 2 found: {get2.Found}");
        
        Assert.True(get1.Found, "Key 1 should be retrievable");
        Assert.True(get2.Found, "Key 2 should be retrievable");
    }
}
