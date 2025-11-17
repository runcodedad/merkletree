# Merkle Proof Generation and Verification

This document provides detailed information about generating and verifying Merkle proofs.

## Overview

Merkle proofs allow verification that a specific leaf is part of a Merkle tree without requiring access to all the data. A proof contains:
- The leaf value being proven
- The leaf's index in the tree
- Sibling hashes at each level from leaf to root
- Orientation bits indicating whether each sibling is on the left or right

## In-Memory Trees

For in-memory trees, proof generation is straightforward and efficient:

```csharp
using MerkleTree;
using System.Text;

var leafData = new List<byte[]>
{
    Encoding.UTF8.GetBytes("data1"),
    Encoding.UTF8.GetBytes("data2"),
    Encoding.UTF8.GetBytes("data3")
};

var tree = new MerkleTree(leafData);
var rootHash = tree.GetRootHash();

// Generate a proof for leaf at index 1
var proof = tree.GenerateProof(1);

Console.WriteLine($"Leaf Index: {proof.LeafIndex}");
Console.WriteLine($"Tree Height: {proof.TreeHeight}");
Console.WriteLine($"Sibling Hashes: {proof.SiblingHashes.Length}");

// Verify the proof
var hashFunction = new Sha256HashFunction();
bool isValid = proof.Verify(rootHash, hashFunction);
Console.WriteLine($"Proof Valid: {isValid}");
```

## Streaming Trees

`MerkleTreeStream` is designed for large datasets that don't fit in memory. It has been optimized for truly memory-efficient operation:

### Tree Building (O(1) memory per level)

The `Build` and `BuildAsync` methods now use **temporary file storage**:
- Writes level hashes to temp files instead of keeping in memory
- Only processes one pair of nodes at a time
- For 500GB of data with 10 billion leaves: uses only ~64 bytes of memory (one hash pair at a time)
- Automatically cleans up temp files after completion

### Proof Generation (O(log n) memory)

The `GenerateProof` method uses an **optimized approach that only computes O(log n) hashes**:
- **Only computes sibling hashes along the proof path** (not entire tree levels)
- For 1 million leaves: keeps only ~20 hashes in memory (tree height), not 1 million
- Computes hashes on-demand by streaming to specific indices
- Memory usage: O(log n) instead of O(n)

```csharp
var builder = new MerkleTreeStream();

// You need to know the leaf count beforehand
long leafCount = 1_000_000;

// Build tree first
var metadata = builder.Build(GetLeafDataStream());

// Generate proof - only computes necessary hashes
var proof = builder.GenerateProof(
    GetLeafDataStream(),  // Re-provide the data stream
    leafIndex: 50000,     // Index to prove
    leafCount: leafCount); // Required parameter

// Verify
bool isValid = proof.Verify(metadata.RootHash, new Sha256HashFunction());
```

### With Cache for Multiple Proofs

When generating multiple proofs, use a cache to avoid recomputing hashes:

```csharp
var cache = new Dictionary<(int level, long index), byte[]>();

// First proof: populates cache as it computes
var proof1 = builder.GenerateProof(GetLeafDataStream(), 100, leafCount, cache);

// Subsequent proofs: reuse cached hashes (much faster!)
var proof2 = builder.GenerateProof(GetLeafDataStream(), 200, leafCount, cache);
var proof3 = builder.GenerateProof(GetLeafDataStream(), 300, leafCount, cache);
```

The cache can also be a disk-based storage system for persistence across sessions.

**Important Notes:**
- `leafCount` is **required** - you must know the total number of leaves beforehand
- The leaf data stream must be re-enumerable (or provide a fresh stream for each call)
- **Memory usage: O(log n)** - only stores hashes along the proof path
- Without cache: streams data to compute each sibling hash
- With cache: retrieves cached hashes, avoiding re-streaming
- Cache is externally managed for maximum flexibility
- Can handle files of any size (500GB+) as long as they're streamable
- For datasets that fit in memory, use `MerkleTree` for O(1) proof generation

## Async Proof Generation

For async scenarios:

```csharp
var builder = new MerkleTreeStream();
var metadata = await builder.BuildAsync(GetLeafDataAsync());
long leafCount = 1_000_000;

// Generate proof asynchronously
var proof = await builder.GenerateProofAsync(
    GetLeafDataAsync(), 
    leafIndex: 1000,
    leafCount: leafCount);

// With cache for multiple proofs
var cache = new Dictionary<(int level, long index), byte[]>();
var proof2 = await builder.GenerateProofAsync(
    GetLeafDataAsync(), 
    leafIndex: 1000,
    leafCount: leafCount,
    cache);
```

## Proof Structure

A `MerkleProof` object contains:

| Property | Type | Description |
|----------|------|-------------|
| `LeafValue` | `byte[]` | The original data of the leaf being proven |
| `LeafIndex` | `long` | The 0-based position of the leaf in the tree |
| `TreeHeight` | `int` | The total height of the tree |
| `SiblingHashes` | `byte[][]` | Hashes of sibling nodes at each level (leaf to root) |
| `SiblingIsRight` | `bool[]` | Orientation: true if sibling is on right, false if on left |

## Verification

To verify a proof:

```csharp
var hashFunction = new Sha256HashFunction();
bool isValid = proof.Verify(expectedRootHash, hashFunction);
```

The verification process:
1. Hashes the leaf value
2. For each level, combines current hash with sibling hash (order determined by orientation)
3. Continues to the root
4. Compares computed root with expected root

## Use Cases

- **Inclusion Proofs**: Prove data exists in a tree without revealing other data
- **Distributed Verification**: Allow clients to verify data against a trusted root hash
- **Blockchain Applications**: Verify transactions in blocks
- **Data Integrity**: Prove specific data is part of a larger verified dataset
- **Audit Trails**: Verify log entries without accessing the entire log

## Performance Characteristics

| Scenario | Time Complexity | Space Complexity | Notes |
|----------|----------------|------------------|-------|
| `MerkleTree.GenerateProof` | O(1) | O(1) | Tree already built in memory |
| `MerkleTreeStream.GenerateProof` (no cache) | O(n log n) | **O(log n)** | Streams to compute only sibling hashes needed |
| `MerkleTreeStream.GenerateProof` (with cache) | O(n log n) first, O(log n) after | O(n) for cache | First proof populates cache, subsequent proofs reuse |

**Key Optimization**: `MerkleTreeStream` only keeps **O(log n) hashes** in memory (tree height), not O(n). For 1 million leaves, this is ~20 hashes instead of 1 million.

## Best Practices

1. **Choose the right class**:
   - Use `MerkleTree` for datasets that fit in memory (best performance: O(1) proofs)
   - Use `MerkleTreeStream` for large datasets that don't fit in memory (optimized: O(log n) memory)
2. **Cache for multiple proofs**: If generating multiple proofs with `MerkleTreeStream`, always use a cache to avoid re-streaming
3. **Know your leaf count**: `MerkleTreeStream.GenerateProof` requires the total leaf count as a parameter
4. **Re-enumerable streams**: Ensure your data stream can be enumerated multiple times when using `MerkleTreeStream`
5. **Hash function consistency**: Use the same hash function for building and verification
6. **Disk-based cache**: For very large datasets or persistent caching, implement cache as disk-based storage

## Error Handling

Common exceptions:

- `ArgumentNullException`: Null leaf data or hash function
- `ArgumentOutOfRangeException`: Invalid leaf index
- `InvalidOperationException`: Empty leaf data or stream size mismatch
- `ArgumentException`: Invalid leaf count (≤ 0)

## Example: Complete Workflow

```csharp
using MerkleTree;
using System.Text;

// 1. Prepare data
var leafData = Enumerable.Range(0, 1000)
    .Select(i => Encoding.UTF8.GetBytes($"item{i}"))
    .ToList();

// 2. Build tree
var tree = new MerkleTree(leafData);
var rootHash = tree.GetRootHash();

// 3. Generate proof
var proof = tree.GenerateProof(500);

// 4. Verify proof
var hashFunction = new Sha256HashFunction();
bool isValid = proof.Verify(rootHash, hashFunction);

Console.WriteLine($"Proof for leaf 500: {(isValid ? "VALID" : "INVALID")}");

// 5. Verify with modified data (should fail)
var tamperedProof = new MerkleProof(
    Encoding.UTF8.GetBytes("tampered"),  // Different data
    proof.LeafIndex,
    proof.TreeHeight,
    proof.SiblingHashes,
    proof.SiblingIsRight);

bool tamperedValid = tamperedProof.Verify(rootHash, hashFunction);
Console.WriteLine($"Tampered proof: {(tamperedValid ? "VALID" : "INVALID")}");
```

## See Also

- [Streaming Documentation](STREAMING.md) - Details on streaming tree construction
- [README](../README.md) - Getting started and overview
