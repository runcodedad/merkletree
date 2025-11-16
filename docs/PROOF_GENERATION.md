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

## Streaming Trees (Small Datasets)

For datasets that fit in memory but use streaming construction:

```csharp
var builder = new MerkleTreeStream();

// Build tree from streaming data
var leafData = GetLeafData(); // IEnumerable<byte[]>
var metadata = builder.Build(leafData);

// Generate proof (rebuilds tree each time)
var proof = builder.GenerateProof(leafData, leafIndex: 100);

// Verify the proof
var hashFunction = new Sha256HashFunction();
bool isValid = proof.Verify(metadata.RootHash, hashFunction);
```

### Using a Cache for Multiple Proofs

When generating multiple proofs, use an external cache to avoid rebuilding the tree:

```csharp
var builder = new MerkleTreeStream();
var metadata = builder.Build(leafData);

// Create a cache to reuse across multiple proof generations
var cache = new Dictionary<(int level, long index), byte[]>();

// Generate multiple proofs using the same cache
var proof1 = builder.GenerateProof(leafData, 100, cache);
var proof2 = builder.GenerateProof(leafData, 200, cache);
var proof3 = builder.GenerateProof(leafData, 300, cache);
// After the first proof, subsequent proofs reuse cached hashes
```

**Performance:**
- Without cache: O(n) time per proof - rebuilds the entire tree
- With cache: First proof is O(n), subsequent proofs reuse cached hashes
- Cache stores ~2n entries (all tree levels)

## Streaming Trees (Large Datasets)

For datasets larger than available memory, provide the `leafCount` parameter to enable streaming mode:

```csharp
var builder = new MerkleTreeStream();

// You need to know the leaf count beforehand
long leafCount = 1_000_000;

// Build tree first
var metadata = builder.Build(GetLeafDataStream());

// Generate proof without loading all data into memory
// Pass leafCount to enable streaming mode
var proof = builder.GenerateProof(
    GetLeafDataStream(),  // Re-provide the data stream
    leafIndex: 50000,     // Index to prove
    leafCount: leafCount); // Enables streaming mode

// Verify
bool isValid = proof.Verify(metadata.RootHash, new Sha256HashFunction());
```

### With Cache for Large Datasets

```csharp
var cache = new Dictionary<(int level, long index), byte[]>();

// Generate multiple proofs with streaming and caching
var proof1 = builder.GenerateProof(GetLeafDataStream(), 100, leafCount, cache);
var proof2 = builder.GenerateProof(GetLeafDataStream(), 200, leafCount, cache);

// Cache persists across calls, improving performance
```

**Important Notes:**
- When `leafCount` is provided, streaming mode is enabled automatically
- The leaf data stream must be re-enumerable (or provide a fresh stream for each call)
- Only one tree level is kept in memory at a time
- The leaf at the requested index will be accessed by enumerating to that position
- Cache is externally managed for flexibility
- If `leafCount` is omitted, the method converts data to a list (suitable for smaller datasets)

## Async Proof Generation

For async scenarios:

```csharp
var builder = new MerkleTreeStream();
var metadata = await builder.BuildAsync(GetLeafDataAsync());

// Without cache (list mode)
var proof = await builder.GenerateProofAsync(
    GetLeafDataAsync(), 
    leafIndex: 1000);

// With streaming mode for large datasets
long leafCount = 1_000_000;
var proof2 = await builder.GenerateProofAsync(
    GetLeafDataAsync(), 
    leafIndex: 1000,
    leafCount: leafCount);

// With cache and streaming
var cache = new Dictionary<(int level, long index), byte[]>();
var proof3 = await builder.GenerateProofAsync(
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
| In-memory tree | O(1) | O(1) | Tree already built |
| Streaming (list mode, no cache) | O(n) | O(n) for list | Converts to list, rebuilds tree |
| Streaming (list mode, with cache) | O(n) first, O(1) after | O(n) | Cache stores all hashes |
| Streaming (leafCount mode) | O(n) | O(n/2) per level | Memory efficient, processes stream |

## Best Practices

1. **Use in-memory trees when possible**: If your dataset fits in memory, use `MerkleTree` for best performance
2. **Cache for multiple proofs**: If generating multiple proofs, always use a cache
3. **Provide leafCount for large datasets**: Pass the `leafCount` parameter to enable streaming mode and avoid loading all data into memory
4. **Re-enumerable streams**: Ensure your data stream can be enumerated multiple times when using streaming mode
5. **Hash function consistency**: Use the same hash function for building and verification

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
