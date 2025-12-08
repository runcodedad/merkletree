# Sparse Merkle Tree (SMT)

A storage-agnostic, deterministic Sparse Merkle Tree implementation supporting pluggable persistence adapters and proof generation. This document covers metadata structure, usage, implementation roadmap, and best practices.

## Table of Contents

- [Overview](#overview)
- [Core Tree Model and API](#core-tree-model-and-api)
- [Metadata Structure](#metadata-structure)
- [Creating and Using Metadata](#creating-and-using-metadata)
- [Serialization](#serialization)
- [Persistence Abstraction](#persistence-abstraction)
- [Implementation Roadmap](#implementation-roadmap)
- [Best Practices](#best-practices)
- [API Documentation](#api-documentation)

## Overview

The SMT core library is built on three key principles:

- **Storage-Agnostic**: Core contains no DB, filesystem, or blockchain logic
- **Deterministic**: Identical inputs produce identical roots across platforms
- **Pluggable**: Hashing and persistence are abstracted and injectable

The metadata structure provides essential information for deterministic reproduction of Sparse Merkle Tree roots and proofs across machines, platforms, and implementations.

## Core Tree Model and API

The `SparseMerkleTree` class provides the primary API for creating and managing Sparse Merkle Trees. It implements a deterministic, binary tree structure that maps arbitrary-length keys to fixed-depth bit paths and supports efficient storage and proof generation.

### Overview

The core tree model consists of:

- **SparseMerkleTree**: Main API class with configurable depth and pluggable hash functions
- **Node Types**: Three distinct node types representing the tree structure
- **Key-to-Path Mapping**: Deterministic conversion of arbitrary keys to fixed-length bit paths
- **Domain-Separated Hashing**: Prevention of collision attacks using distinct prefixes for leaves and internal nodes

### Key Features

- **Configurable Depth**: Default depth of 256 for SHA-256 (matching output size), customizable at initialization
- **Arbitrary-Length Keys**: Accepts keys of any length; internally maps them to fixed-length bit paths via hash function
- **Three Node Types**: Empty nodes (zero-hashes), leaf nodes (key-value pairs), internal nodes (two children)
- **Domain Separation**: Uses `0x00` prefix for leaves, `0x01` prefix for internal nodes to prevent collision attacks
- **Immutable Structures**: All nodes use `ReadOnlyMemory<byte>` for thread-safe, immutable data
- **Thread-Safe Operations**: All operations are thread-safe and can be called concurrently
- **Cross-Platform Determinism**: Identical inputs produce identical outputs on all platforms

### Creating a Sparse Merkle Tree

#### Default Depth (256)

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var hashFunction = new Sha256HashFunction();
var tree = new SparseMerkleTree(hashFunction);

Console.WriteLine($"Tree depth: {tree.Depth}"); // Output: 256
Console.WriteLine($"Max capacity: 2^{tree.Depth} keys");
```

#### Custom Depth

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var hashFunction = new Sha256HashFunction();
var tree = new SparseMerkleTree(hashFunction, treeDepth: 160);

Console.WriteLine($"Tree depth: {tree.Depth}"); // Output: 160
Console.WriteLine($"Hash algorithm: {tree.Metadata.HashAlgorithmId}"); // Output: SHA-256
```

**Depth Guidelines:**

- **Small Trees (depth 8-16)**: Testing, small datasets (256 to 65K keys)
- **Medium Trees (depth 32-64)**: Application-specific use cases (billions to quintillions of keys)
- **Large Trees (depth 160-256)**: Cryptographic applications, blockchain systems (2^160 to 2^256 keys)

### Key-to-Path Mapping

Sparse Merkle Trees map arbitrary-length keys to fixed-length bit paths using the hash function. This ensures uniform distribution and fixed tree depth regardless of key format.

#### Mapping Process

1. **Hash the Key**: Apply the hash function to the key bytes
2. **Convert to Bits**: Extract bits from the hash output
3. **Truncate or Pad**: Adjust to match tree depth
4. **Use as Path**: Each bit determines left (0/false) or right (1/true) traversal

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var hashFunction = new Sha256HashFunction();
var tree = new SparseMerkleTree(hashFunction, treeDepth: 256);

// Convert arbitrary key to bit path
var key = Encoding.UTF8.GetBytes("user:alice:balance");
var bitPath = tree.GetBitPath(key);

Console.WriteLine($"Key length: {key.Length} bytes");
Console.WriteLine($"Bit path length: {bitPath.Length} bits"); // Output: 256
Console.WriteLine($"First 8 bits: {string.Join("", bitPath[0..8].Select(b => b ? "1" : "0"))}");
```

#### Path Properties

- **Deterministic**: Same key always produces same path
- **Uniform Distribution**: Hash function ensures even distribution across tree
- **Fixed Length**: Always matches tree depth (e.g., 256 bits for depth 256)
- **Platform Independent**: Identical across all platforms and implementations

### Node Types

The SMT uses three distinct node types to represent the tree structure efficiently:

#### 1. Empty Node (SmtEmptyNode)

Represents an empty subtree. Uses precomputed zero-hashes from the zero-hash table for efficiency.

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var hashFunction = new Sha256HashFunction();
var tree = new SparseMerkleTree(hashFunction);

// Create an empty node at level 0 (leaf level)
var emptyNode = tree.CreateEmptyNode(level: 0);

Console.WriteLine($"Node type: {emptyNode.GetType().Name}"); // Output: SmtEmptyNode
Console.WriteLine($"Level: {emptyNode.Level}");
Console.WriteLine($"Hash: {Convert.ToHexString(emptyNode.Hash.Span)}");

// Empty nodes at different levels have different hashes
var emptyLevel5 = tree.CreateEmptyNode(level: 5);
Console.WriteLine($"Different hash: {!emptyNode.Hash.Span.SequenceEqual(emptyLevel5.Hash.Span)}");
```

**Properties:**

- Uses zero-hash table for O(1) hash lookup
- No key or value stored
- Level determines which zero-hash to use
- Memory efficient (shares zero-hash table across all empty nodes)

#### 2. Leaf Node (SmtLeafNode)

Represents a key-value pair at the bottom of the tree.

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var hashFunction = new Sha256HashFunction();
var tree = new SparseMerkleTree(hashFunction);

// Create a leaf node
var key = Encoding.UTF8.GetBytes("account:123");
var value = Encoding.UTF8.GetBytes("balance:1000");
var leafNode = tree.CreateLeafNode(key, value);

Console.WriteLine($"Node type: {leafNode.GetType().Name}"); // Output: SmtLeafNode
Console.WriteLine($"Key hash: {Convert.ToHexString(leafNode.KeyHash.Span)}");
Console.WriteLine($"Value: {Encoding.UTF8.GetString(leafNode.Value.Span)}");
Console.WriteLine($"Hash: {Convert.ToHexString(leafNode.Hash.Span)}");
```

**Hash Computation:**

```
leaf_hash = Hash(0x00 || key || value)
```

Where:
- `0x00` is the leaf domain separator
- `||` denotes byte concatenation

**Properties:**

- Stores both key and value as `ReadOnlyMemory<byte>`
- Uses domain separator `0x00` to prevent collision with internal nodes
- Immutable after creation
- Key and value can be arbitrary length

#### 3. Internal Node (SmtInternalNode)

Represents an internal node with left and right children.

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var hashFunction = new Sha256HashFunction();
var tree = new SparseMerkleTree(hashFunction);

// Create internal node from two children
var leftChild = tree.CreateEmptyNode(level: 0);
var rightChild = tree.CreateLeafNode(
    Encoding.UTF8.GetBytes("key1"),
    Encoding.UTF8.GetBytes("value1")
);
var internalNode = tree.CreateInternalNode(leftChild.Hash.ToArray(), rightChild.Hash.ToArray());

Console.WriteLine($"Node type: {internalNode.GetType().Name}"); // Output: SmtInternalNode
Console.WriteLine($"Left hash: {Convert.ToHexString(internalNode.LeftHash.Span)}");
Console.WriteLine($"Right hash: {Convert.ToHexString(internalNode.RightHash.Span)}");
Console.WriteLine($"Node hash: {Convert.ToHexString(internalNode.Hash.Span)}");
```

**Hash Computation:**

```
internal_hash = Hash(0x01 || left_child_hash || right_child_hash)
```

Where:
- `0x01` is the internal node domain separator
- `||` denotes byte concatenation

**Properties:**

- Stores hashes of left and right children
- Uses domain separator `0x01` to prevent collision with leaf nodes
- Children can be any node type (empty, leaf, or internal)
- Hash is deterministic based on children's hashes

### Domain-Separated Hashing

Domain separation prevents collision attacks by ensuring that leaf nodes and internal nodes produce different hashes even with identical input data.

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var hashFunction = new Sha256HashFunction();
var tree = new SparseMerkleTree(hashFunction);

// Create a leaf node
var leafKey = new byte[] { 0x01, 0x02 };
var leafValue = new byte[] { 0x03, 0x04 };
var leafNode = tree.CreateLeafNode(leafKey, leafValue);

// Create an internal node with same byte sequence
var leftHash = new byte[] { 0x01, 0x02 };
var rightHash = new byte[] { 0x03, 0x04 };
// Note: This is a conceptual example - actual API may differ for direct hash construction

// The hashes will be different due to domain separation:
// Leaf: Hash(0x00 || 0x01 || 0x02 || 0x03 || 0x04)
// Internal: Hash(0x01 || left_hash || right_hash)
Console.WriteLine("Domain separation ensures different hashes for different node types");
```

**Security Benefits:**

- **Prevents Second-Preimage Attacks**: Attacker cannot find leaf that hashes to same value as internal node
- **Ensures Collision Resistance**: Different node types cannot produce same hash
- **Standard Practice**: Follows industry best practices for Merkle tree implementations

### Accessing Tree Metadata

The tree exposes metadata for inspection and serialization:

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var hashFunction = new Sha256HashFunction();
var tree = new SparseMerkleTree(hashFunction, treeDepth: 160);

// Access metadata
var metadata = tree.Metadata;
Console.WriteLine($"Hash Algorithm: {metadata.HashAlgorithmId}");
Console.WriteLine($"Tree Depth: {metadata.TreeDepth}");
Console.WriteLine($"SMT Core Version: {metadata.SmtCoreVersion}");
Console.WriteLine($"Serialization Format Version: {metadata.SerializationFormatVersion}");

// Access zero-hash table
var zeroHashes = metadata.ZeroHashes;
Console.WriteLine($"Zero-hash table size: {zeroHashes.Count}"); // Output: 161 (depth + 1)

// Get zero-hash for specific level
var zeroHashLevel0 = zeroHashes[0]; // Empty leaf
var zeroHashLevel160 = zeroHashes[160]; // Empty root

// Serialize metadata for storage
byte[] serializedMetadata = metadata.Serialize();
Console.WriteLine($"Serialized metadata size: {serializedMetadata.Length} bytes");
```

### Thread Safety

All tree operations are thread-safe and can be called concurrently:

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var hashFunction = new Sha256HashFunction();
var tree = new SparseMerkleTree(hashFunction);

// Safe to call from multiple threads
var tasks = Enumerable.Range(0, 100).Select(i =>
    Task.Run(() =>
    {
        var key = Encoding.UTF8.GetBytes($"key{i}");
        var value = Encoding.UTF8.GetBytes($"value{i}");
        var node = tree.CreateLeafNode(key, value);
        return node.Hash;
    })
);

var hashes = await Task.WhenAll(tasks);
Console.WriteLine($"Created {hashes.Length} nodes concurrently");
```

**Thread-Safety Guarantees:**

- Node creation methods are thread-safe
- Metadata access is thread-safe (immutable after creation)
- Key-to-path conversion is thread-safe
- Multiple threads can create nodes concurrently without locks

### Complete Example

```csharp
using System.Text;
using MerkleTree.Hashing;
using MerkleTree.Smt;

// Initialize tree with SHA-256 and default depth
var hashFunction = new Sha256HashFunction();
var tree = new SparseMerkleTree(hashFunction);

Console.WriteLine($"Created SMT with depth {tree.Depth}");
Console.WriteLine($"Using {tree.Metadata.HashAlgorithmId}");

// Example 1: Convert keys to bit paths
var userKey = Encoding.UTF8.GetBytes("user:alice");
var bitPath = tree.GetBitPath(userKey);
Console.WriteLine($"Key 'user:alice' maps to {bitPath.Length}-bit path");

// Example 2: Create different node types
var emptyNode = tree.CreateEmptyNode(level: 0);
Console.WriteLine($"Empty node hash: {Convert.ToHexString(emptyNode.Hash.Span)}");

var leafNode = tree.CreateLeafNode(
    Encoding.UTF8.GetBytes("account:123"),
    Encoding.UTF8.GetBytes("balance:1000")
);
Console.WriteLine($"Leaf node hash: {Convert.ToHexString(leafNode.Hash.Span)}");

var internalNode = tree.CreateInternalNode(emptyNode.Hash.ToArray(), leafNode.Hash.ToArray());
Console.WriteLine($"Internal node hash: {Convert.ToHexString(internalNode.Hash.Span)}");

// Example 3: Access metadata
var metadata = tree.Metadata;
Console.WriteLine($"\nMetadata:");
Console.WriteLine($"  Hash Algorithm: {metadata.HashAlgorithmId}");
Console.WriteLine($"  Depth: {metadata.TreeDepth}");
Console.WriteLine($"  Core Version: {metadata.SmtCoreVersion}");
Console.WriteLine($"  Zero-hash table entries: {metadata.ZeroHashes.Count}");

// Example 4: Verify determinism
var tree2 = new SparseMerkleTree(hashFunction);
var leafNode2 = tree2.CreateLeafNode(
    Encoding.UTF8.GetBytes("account:123"),
    Encoding.UTF8.GetBytes("balance:1000")
);
bool hashesMatch = leafNode.Hash.Span.SequenceEqual(leafNode2.Hash.Span);
Console.WriteLine($"\nDeterminism verified: {hashesMatch}");
```

### Design Principles

The core tree model follows these design principles:

1. **Immutability**: All node structures are immutable after creation
2. **Type Safety**: Distinct types for empty, leaf, and internal nodes prevent errors
3. **Determinism**: Same inputs always produce same outputs across all platforms
4. **Performance**: Zero-hash table precomputation provides O(1) empty node creation
5. **Security**: Domain separation prevents collision attacks
6. **Simplicity**: Clean API with minimal surface area
7. **Testability**: Pure functions enable easy testing and verification

### Next Steps

With the core tree model in place, you can:

- **Store Nodes**: Use the persistence interfaces to store nodes in your chosen backend
- **Build Trees**: Implement insert, update, and delete operations using the node creation API
- **Generate Proofs**: Create inclusion and non-inclusion proofs using the bit-path mapping
- **Verify Trees**: Use metadata to ensure compatibility across different trees
- **Optimize Performance**: Leverage zero-hash table for efficient sparse tree operations

See the following sections for more details on metadata, serialization, and persistence.

## Metadata Structure

The `SmtMetadata` class contains the following core components:

### Hash Algorithm ID

- **Type**: `string`
- **Purpose**: Identifies the cryptographic hash function used for tree construction
- **Examples**: `"SHA-256"`, `"SHA-512"`, `"BLAKE3"`
- **Determinism**: Must match the hash function name exactly across all implementations

### Tree Depth

- **Type**: `int`
- **Purpose**: Specifies the number of levels in the tree
- **Range**: Minimum 1, typical values: 8-256
- **Capacity**: A depth of N supports up to 2^N keys
  - Depth 8 → 256 keys
  - Depth 16 → 65,536 keys
  - Depth 32 → 4,294,967,296 keys
  - Depth 64 → 18,446,744,073,709,551,616 keys
  - Depth 256 → 2^256 keys (common for cryptographic applications)

### Zero-Hash Table

- **Type**: `ZeroHashTable`
- **Purpose**: Precomputed hash values for empty subtrees at each level
- **Size**: Contains `depth + 1` entries (one per level, including root)
- **Computation**: Deterministic, based on hash algorithm and tree depth

#### Zero-Hash Table Generation

The zero-hash table is computed deterministically using domain-separated hashing to ensure consistency across implementations and prevent collision attacks.

**Algorithm:**

```
Level 0 (leaf):
  zero[0] = Hash(0x00 || empty_bytes)
  
Level N (internal, N > 0):
  zero[N] = Hash(0x01 || zero[N-1] || zero[N-1])
```

Where:
- `0x00` is the leaf domain separator
- `0x01` is the internal node domain separator
- `empty_bytes` is an empty byte array
- `||` denotes concatenation

**Properties:**

1. **Deterministic**: Same hash algorithm and depth always produce identical tables
2. **Unique**: All zero-hashes at different levels are unique
3. **Domain-Separated**: Collision attacks between leaves and internal nodes are prevented
4. **Platform-Independent**: Computation is identical across all platforms and implementations

**Example (SHA-256, Depth 8):**

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var hashFunction = new Sha256HashFunction();
var zeroHashes = ZeroHashTable.Compute(hashFunction, depth: 8);

// Access zero-hash for level 0 (empty leaf)
byte[] leafZeroHash = zeroHashes[0];

// Access zero-hash for level 8 (empty root)
byte[] rootZeroHash = zeroHashes[8];

// Verify the table is correct
bool isValid = zeroHashes.Verify(hashFunction); // Returns true
```

### SMT Core Version

- **Type**: `int`
- **Purpose**: Tracks the version of the SMT core implementation
- **Current**: Version 1
- **Breaking Changes**: Incremented when tree structure, hashing strategy, or core algorithms change incompatibly

### Serialization Format Version

- **Type**: `int`
- **Purpose**: Tracks the binary serialization format version
- **Current**: Version 1
- **Breaking Changes**: Incremented when the serialization format changes incompatibly

## Creating and Using Metadata

### Option 1: Factory Method (Recommended)

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var hashFunction = new Sha256HashFunction();
var metadata = SmtMetadata.Create(hashFunction, treeDepth: 256);

Console.WriteLine($"Algorithm: {metadata.HashAlgorithmId}");
Console.WriteLine($"Depth: {metadata.TreeDepth}");
Console.WriteLine($"Core Version: {metadata.SmtCoreVersion}");
Console.WriteLine($"Format Version: {metadata.SerializationFormatVersion}");
```

### Option 2: Constructor (Advanced)

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var hashFunction = new Sha256HashFunction();
var zeroHashes = ZeroHashTable.Compute(hashFunction, 256);

var metadata = new SmtMetadata(
    hashAlgorithmId: hashFunction.Name,
    treeDepth: 256,
    zeroHashes: zeroHashes,
    smtCoreVersion: 1,
    serializationFormatVersion: 1
);
```

## Serialization

The metadata can be serialized to a platform-independent binary format for storage or transmission.

### Binary Format (Version 1)

```
Offset | Size | Description
-------|------|------------
0      | 4    | Serialization format version (little-endian)
4      | 4    | SMT core version (little-endian)
8      | 4    | Tree depth (little-endian)
12     | 4    | Hash algorithm ID length (little-endian)
16     | N    | Hash algorithm ID string (UTF-8)
16+N   | M    | Zero-hash table (serialized)
```

All numeric values use little-endian byte order for cross-platform compatibility.

### Serialization Example

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var hashFunction = new Sha256HashFunction();
var metadata = SmtMetadata.Create(hashFunction, 256);

// Serialize to bytes
byte[] serialized = metadata.Serialize();

// Save to file (example)
File.WriteAllBytes("smt_metadata.bin", serialized);

// Deserialize from bytes
byte[] loaded = File.ReadAllBytes("smt_metadata.bin");
var deserialized = SmtMetadata.Deserialize(loaded);

// Verify integrity
bool matches = deserialized.HashAlgorithmId == metadata.HashAlgorithmId
            && deserialized.TreeDepth == metadata.TreeDepth;
```

### Determinism Guarantees

The SMT metadata system provides strong determinism guarantees:

1. **Cross-Platform**: Identical metadata on Windows, Linux, macOS, and any .NET-supported platform
2. **Cross-Implementation**: Same hash algorithm and depth produce identical zero-hash tables in any correct implementation
3. **Cross-Version**: Metadata from version N can be read by version N (forward compatibility requires explicit migration)
4. **Reproducible**: Same inputs always produce bit-identical serialized output

#### Testing Determinism

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

// Create metadata multiple times
var metadata1 = SmtMetadata.Create(new Sha256HashFunction(), 256);
var metadata2 = SmtMetadata.Create(new Sha256HashFunction(), 256);

// Verify zero-hashes are identical
for (int level = 0; level <= 256; level++)
{
    Assert.Equal(metadata1.ZeroHashes[level], metadata2.ZeroHashes[level]);
}

// Verify serialization is deterministic
byte[] serialized1 = metadata1.Serialize();
byte[] serialized2 = metadata2.Serialize();
Assert.Equal(serialized1, serialized2);
```

### Versioning and Migration

#### Version Changes

**SMT Core Version** changes indicate breaking changes to:
- Tree structure or organization
- Hashing strategy or domain separation
- Core algorithms (insert, update, delete, proof generation)
- Zero-hash computation method

**Serialization Format Version** changes indicate breaking changes to:
- Binary serialization format
- Field order or encoding
- Data types or sizes

#### Migration Strategy

When versions change, migration may be required:

1. **Non-Breaking Changes**: Patch versions don't change metadata versions
2. **Breaking Changes**: Major versions increment metadata versions
3. **Manual Migration**: Tooling to migrate old states is out-of-scope; migration must be handled manually

#### Migration Example

```csharp
// Version 1 metadata
var v1Metadata = SmtMetadata.Deserialize(oldSerializedData);

if (v1Metadata.SmtCoreVersion != SmtMetadata.CurrentSmtCoreVersion)
{
    Console.WriteLine("WARNING: Metadata from older SMT core version.");
    Console.WriteLine($"Current: {SmtMetadata.CurrentSmtCoreVersion}, " +
                     $"Loaded: {v1Metadata.SmtCoreVersion}");
    
    // Manual migration required
    // 1. Extract tree data using old version
    // 2. Rebuild tree with new version
    // 3. Verify root hashes match expectations
}
```

#### Breaking Change Documentation

Breaking changes will be documented in release notes with:
- Clear description of what changed
- Migration steps (if available)
- Compatibility matrix
- Deprecation timeline (if applicable)

### Validation

#### Validating Metadata Integrity

```csharp
using MerkleTree.Hashing;
using MerkleTree.Smt;

var metadata = SmtMetadata.Deserialize(serializedData);

// 1. Check versions
if (metadata.SmtCoreVersion != SmtMetadata.CurrentSmtCoreVersion)
{
    Console.WriteLine("WARNING: Version mismatch");
}

// 2. Verify zero-hash table
var hashFunction = GetHashFunction(metadata.HashAlgorithmId);
bool isValid = metadata.ZeroHashes.Verify(hashFunction);

if (!isValid)
{
    throw new InvalidOperationException("Zero-hash table verification failed. " +
                                       "Data may be corrupted or computed with different algorithm.");
}

// 3. Validate depth range
if (metadata.TreeDepth < 1 || metadata.TreeDepth > 256)
{
    throw new InvalidOperationException($"Invalid tree depth: {metadata.TreeDepth}");
}
```

#### Usage in Core Operations

The metadata is used throughout SMT operations:

1. **Initialization**: Create metadata when initializing a new SMT
2. **Persistence**: Store metadata alongside tree nodes for later reconstruction
3. **Proof Generation**: Include metadata hash in proofs for verification
4. **Proof Verification**: Load metadata to verify proofs and check compatibility
5. **Tree Comparison**: Compare metadata to ensure trees are compatible for merging or comparison

**Example: Proof Verification**

```csharp
// Verifier receives: proof, key, value, metadata
var metadata = SmtMetadata.Deserialize(metadataBytes);
var hashFunction = GetHashFunction(metadata.HashAlgorithmId);

// Verify the proof uses correct metadata
bool proofValid = VerifyProof(
    proof,
    key,
    value,
    metadata.ZeroHashes,
    hashFunction
);
```

## Persistence Abstraction

The SMT persistence abstraction provides a storage-agnostic interface layer that enables Sparse Merkle Trees to work with any storage backend: in-memory, on-disk databases, remote servers, cloud storage, or blockchain systems. This design ensures SMT core operations remain independent of persistence implementation details.

### Design Principles

- **Storage Agnostic**: Core SMT operations use only abstract interfaces—no direct file system, database, or network code
- **Deterministic & Reproducible**: Same inputs always produce same outputs with cross-platform compatibility
- **Pluggable Architecture**: Multiple storage backends can coexist; easy to implement custom adapters
- **Thread-Safe & Concurrent**: All interfaces support concurrent operations; read operations never block other reads
- **Idempotent Operations**: Duplicate writes cause no errors; snapshot operations can be safely retried

### SmtNodeBlob

A minimal structure representing a persisted node:

```csharp
public sealed class SmtNodeBlob
{
    public ReadOnlyMemory<byte> Hash { get; }
    public ReadOnlyMemory<bool>? Path { get; }
    public ReadOnlyMemory<byte> SerializedNode { get; }
}
```

**Fields:**
- **Hash**: Unique identifier for the node (used as primary key)
- **Path**: Optional bit-path from root to node (false = left, true = right)
- **SerializedNode**: Binary serialized node data (format defined by SMT implementation)

**Usage:**

```csharp
// Create a node blob without path
var blob = SmtNodeBlob.Create(hash, serializedNode);

// Create a node blob with path
var blobWithPath = SmtNodeBlob.CreateWithPath(hash, serializedNode, path);
```

### ISmtNodeReader

Interface for reading SMT nodes from storage.

```csharp
public interface ISmtNodeReader
{
    Task<SmtNodeBlob?> ReadNodeByHashAsync(
        ReadOnlyMemory<byte> hash,
        CancellationToken cancellationToken = default);
    
    Task<SmtNodeBlob?> ReadNodeByPathAsync(
        ReadOnlyMemory<bool> path,
        CancellationToken cancellationToken = default);
    
    Task<bool> NodeExistsAsync(
        ReadOnlyMemory<byte> hash,
        CancellationToken cancellationToken = default);
}
```

**Guarantees:**
- Thread-safe: Multiple concurrent reads supported
- Consistent: Always returns latest committed data or null
- Non-blocking: Read operations don't block other reads

**Usage:**

```csharp
// Read a node by hash
var node = await reader.ReadNodeByHashAsync(nodeHash);
if (node != null)
{
    ProcessNode(node.SerializedNode);
}

// Check if node exists (lightweight)
bool exists = await reader.NodeExistsAsync(nodeHash);

// Read by path (optional - not all adapters support this)
var nodeAtPath = await reader.ReadNodeByPathAsync(bitPath);
```

### ISmtNodeWriter

Interface for writing SMT nodes to storage.

```csharp
public interface ISmtNodeWriter
{
    Task WriteBatchAsync(
        IReadOnlyList<SmtNodeBlob> nodes,
        CancellationToken cancellationToken = default);
    
    Task WriteNodeAsync(
        SmtNodeBlob node,
        CancellationToken cancellationToken = default);
    
    Task FlushAsync(CancellationToken cancellationToken = default);
}
```

**Guarantees:**
- Atomic: Batch writes are all-or-nothing where possible
- Idempotent: Writing same node multiple times causes no error
- Thread-safe: Multiple threads can write concurrently

**Usage:**

```csharp
// Write a single node
await writer.WriteNodeAsync(nodeBlob);

// Write multiple nodes atomically
var nodesToWrite = new List<SmtNodeBlob> { node1, node2, node3 };
await writer.WriteBatchAsync(nodesToWrite);

// Ensure all writes are durable
await writer.FlushAsync();
```

### ISmtSnapshotManager

Interface for managing logical snapshots of SMT state.

```csharp
public interface ISmtSnapshotManager
{
    Task CreateSnapshotAsync(
        string snapshotName,
        ReadOnlyMemory<byte> rootHash,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);
    
    Task<SmtSnapshotInfo?> GetSnapshotAsync(
        string snapshotName,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<string>> ListSnapshotsAsync(
        CancellationToken cancellationToken = default);
    
    Task DeleteSnapshotAsync(
        string snapshotName,
        CancellationToken cancellationToken = default);
    
    Task<ReadOnlyMemory<byte>> RestoreSnapshotAsync(
        string snapshotName,
        CancellationToken cancellationToken = default);
}
```

**Guarantees:**
- Immutable: Snapshots never change after creation
- Thread-safe: Snapshot operations don't interfere with tree operations
- Idempotent: Deleting non-existent snapshot succeeds without error

**Usage:**

```csharp
// Create a snapshot with metadata
var metadata = new Dictionary<string, string>
{
    { "version", "1.0" },
    { "created_by", "user123" }
};
await snapshotMgr.CreateSnapshotAsync("checkpoint-1", currentRoot, metadata);

// List all snapshots
var snapshots = await snapshotMgr.ListSnapshotsAsync();
foreach (var name in snapshots)
{
    Console.WriteLine($"Snapshot: {name}");
}

// Restore from snapshot
var restoredRoot = await snapshotMgr.RestoreSnapshotAsync("checkpoint-1");

// Delete old snapshot
await snapshotMgr.DeleteSnapshotAsync("old-checkpoint");
```

### ISmtMetadataStore

Interface for storing and retrieving SMT metadata.

```csharp
public interface ISmtMetadataStore
{
    Task StoreMetadataAsync(
        SmtMetadata metadata,
        CancellationToken cancellationToken = default);
    
    Task<SmtMetadata?> LoadMetadataAsync(
        CancellationToken cancellationToken = default);
    
    Task UpdateCurrentRootAsync(
        ReadOnlyMemory<byte> rootHash,
        CancellationToken cancellationToken = default);
    
    Task<ReadOnlyMemory<byte>?> GetCurrentRootAsync(
        CancellationToken cancellationToken = default);
    
    Task<bool> MetadataExistsAsync(
        CancellationToken cancellationToken = default);
}
```

**Guarantees:**
- Atomic: Metadata updates are all-or-nothing
- Idempotent: Storing identical metadata multiple times is safe
- Thread-safe: Concurrent metadata operations are supported

**Usage:**

```csharp
// Store metadata when creating a tree
var metadata = SmtMetadata.Create(hashFunction, treeDepth: 256);
await metadataStore.StoreMetadataAsync(metadata);

// Load metadata when opening a tree
var loadedMetadata = await metadataStore.LoadMetadataAsync();
if (loadedMetadata == null)
{
    throw new InvalidOperationException("Tree not initialized");
}

// Update current root after tree modifications
await metadataStore.UpdateCurrentRootAsync(newRootHash);

// Get current root (lightweight)
var currentRoot = await metadataStore.GetCurrentRootAsync();
```

### Reference Implementation: InMemorySmtStorage

A complete in-memory implementation of all persistence interfaces is provided for testing and development.

```csharp
var storage = new InMemorySmtStorage();

// Use it with all interfaces
ISmtNodeReader reader = storage;
ISmtNodeWriter writer = storage;
ISmtSnapshotManager snapshotMgr = storage;
ISmtMetadataStore metadataStore = storage;

// Store metadata
await storage.StoreMetadataAsync(metadata);

// Write nodes
await storage.WriteBatchAsync(nodes);

// Create snapshot
await storage.CreateSnapshotAsync("snapshot-1", rootHash);

// Clear all data (useful for testing)
storage.Clear();
```

**Characteristics:**
- Thread-safe: Uses lock-based synchronization
- Fast: O(1) lookups using hash-based dictionaries
- Simple: No external dependencies
- Limited: Not suitable for large datasets or durability requirements

### Implementing a Custom Adapter

To implement a custom storage adapter:

1. Implement the interfaces you need (all or subset)
2. Document concurrency guarantees in your implementation
3. Handle errors appropriately (throw InvalidOperationException for adapter errors)
4. Ensure idempotency for write operations
5. Test thoroughly using the test patterns from InMemorySmtStorageTests

**Example: File-Based Adapter Skeleton**

```csharp
public class FileBasedSmtStorage : ISmtNodeReader, ISmtNodeWriter
{
    private readonly string _basePath;
    private readonly object _writeLock = new object();

    public FileBasedSmtStorage(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(basePath);
    }

    public async Task<SmtNodeBlob?> ReadNodeByHashAsync(
        ReadOnlyMemory<byte> hash,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetNodePath(hash);
        if (!File.Exists(filePath))
            return null;

        try
        {
            var data = await File.ReadAllBytesAsync(filePath, cancellationToken);
            return SmtNodeBlob.Create(hash, new ReadOnlyMemory<byte>(data));
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("Failed to read node from disk", ex);
        }
    }

    public async Task WriteBatchAsync(
        IReadOnlyList<SmtNodeBlob> nodes,
        CancellationToken cancellationToken = default)
    {
        // Use lock to ensure atomic batch writes
        await Task.Run(() =>
        {
            lock (_writeLock)
            {
                foreach (var node in nodes)
                {
                    var filePath = GetNodePath(node.Hash);
                    File.WriteAllBytes(filePath, node.SerializedNode.ToArray());
                }
            }
        }, cancellationToken);
    }

    private string GetNodePath(ReadOnlyMemory<byte> hash)
    {
        var hashHex = Convert.ToHexString(hash.Span);
        // Use subdirectories to avoid too many files in one directory
        var subDir = hashHex.Substring(0, 2);
        var dirPath = Path.Combine(_basePath, "nodes", subDir);
        Directory.CreateDirectory(dirPath);
        return Path.Combine(dirPath, hashHex);
    }

    // Implement other interface methods...
}
```

### Error Handling

All persistence interfaces follow consistent error handling:

**Expected Errors:**
- `ArgumentNullException`: Null arguments
- `ArgumentException`: Invalid arguments (empty hash, empty name, etc.)
- `InvalidOperationException`: Adapter-level errors (I/O failure, network issue, corrupted data)
- `OperationCanceledException`: Operation canceled via CancellationToken

**Not Found vs. Error:**
- Missing nodes: Return null (expected for sparse trees)
- I/O errors: Throw InvalidOperationException
- Corrupt data: Throw InvalidOperationException

**Example Error Handling**

```csharp
try
{
    var node = await reader.ReadNodeByHashAsync(hash);
    if (node == null)
    {
        // Missing node is expected in sparse trees
        Console.WriteLine("Node not found - using zero hash");
        node = GetZeroNode();
    }
}
catch (InvalidOperationException ex)
{
    // Adapter-level error (I/O, network, corruption)
    _logger.LogError(ex, "Failed to read node from storage");
    throw;
}
catch (OperationCanceledException)
{
    // Operation was canceled
    _logger.LogInformation("Read operation canceled");
    throw;
}
```

### Testing Your Adapter

Use the test patterns from `InMemorySmtStorageTests.cs` to validate your adapter:

```csharp
public class MyCustomAdapterTests
{
    private MyCustomAdapter _adapter;

    [Fact]
    public async Task ReadNodeByHashAsync_NonExistentNode_ReturnsNull()
    {
        // Arrange
        var hash = ComputeHash("test");

        // Act
        var result = await _adapter.ReadNodeByHashAsync(hash);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task WriteNodeAsync_DuplicateNode_IsIdempotent()
    {
        // Arrange
        var blob = CreateTestBlob();

        // Act - write twice
        await _adapter.WriteNodeAsync(blob);
        await _adapter.WriteNodeAsync(blob);

        // Assert - should succeed without error
        var result = await _adapter.ReadNodeByHashAsync(blob.Hash);
        Assert.NotNull(result);
    }

    // Add more tests based on InMemorySmtStorageTests patterns...
}
```

### Performance Considerations

**Read Performance:**
- Hash-based lookups: Should be O(1) or O(log n)
- Path-based lookups: Optional—can be slower or unsupported
- Existence checks: Should be faster than full reads

**Write Performance:**
- Batch writes: Should be more efficient than individual writes
- Buffering: Consider buffering writes in memory before flushing
- Transactions: Use database transactions for atomicity

**Storage Optimization:**
- Deduplication: Nodes with same hash share storage
- Compression: Consider compressing node data
- Indexing: Index by both hash and path for fast lookups
- Caching: Cache frequently accessed nodes

### Integration with SMT Core

SMT core operations will use these interfaces exclusively:

```csharp
// Tree initialization
var metadata = SmtMetadata.Create(hashFunction, 256);
await metadataStore.StoreMetadataAsync(metadata);

// Tree updates
var nodesToWrite = ComputeUpdatedNodes(key, value);
await nodeWriter.WriteBatchAsync(nodesToWrite);
await metadataStore.UpdateCurrentRootAsync(newRootHash);

// Tree queries
var currentRoot = await metadataStore.GetCurrentRootAsync();
var proofNodes = await ReadProofPath(nodeReader, key, currentRoot);

// Snapshots
await snapshotMgr.CreateSnapshotAsync("before-update", currentRoot);
// ... make changes ...
if (needsRollback)
{
    var previousRoot = await snapshotMgr.RestoreSnapshotAsync("before-update");
    await metadataStore.UpdateCurrentRootAsync(previousRoot);
}
```

### Security Considerations

**Input Validation:**
- Always validate hash lengths and formats
- Sanitize snapshot names to prevent path traversal
- Limit metadata size to prevent memory attacks

**Access Control:**
- Implement authentication/authorization in your adapter
- Consider read-only vs. read-write interfaces
- Audit sensitive operations (snapshots, deletions)

**Data Integrity:**
- Verify hash matches node content on read
- Use checksums for corruption detection
- Implement backup and recovery procedures

### Future Extensions

Potential future enhancements to the persistence abstraction:

- Garbage collection interface: Clean up unreferenced nodes
- Compression interface: Optional compression of node data
- Replication interface: Multi-node storage synchronization
- Migration interface: Upgrade between storage versions
- Metrics interface: Monitor storage performance and health

## Implementation Roadmap

This roadmap outlines the minimal, dependency-aware implementation path for the SMT core library. It is intentionally simple and focused on delivering a clean, storage-agnostic core that supports deterministic hashing, proofs, and pluggable persistence adapters.

### Guiding Principles

- Storage-agnostic: core contains no DB, filesystem, or blockchain logic
- Deterministic: identical inputs produce identical roots across platforms
- Pluggable: hashing and persistence are abstracted and injectable
- Minimal persistence surface: core returns nodes-to-persist but never persists directly

### High-Level Phases (Recommended Order)

#### 1. Foundation — Hashing & Metadata

- Implement hash algorithm abstraction and domain-separated hashing primitives (Issue #39) ✅ **COMPLETED**
- Define metadata and zero-hash generation utilities (Issue #43) ✅ **COMPLETED**
- **Deliverables:**
  - HashAlgorithm interface (SHA-256 default) ✅ **COMPLETED**
  - Deterministic zero-hash table generator ✅ **COMPLETED**
  - SMT metadata structure (hash algorithm, depth, versions) ✅ **COMPLETED**
- **Why first**: Hashing and metadata are prerequisites for bit-path derivation and proof verification

#### 2. Interfaces & Model

- Define persistence abstraction interfaces (read/write, batch, snapshots, metadata) (Issue #42) ✅ **COMPLETED**
- Implement the SMT tree model (key → bit-path mapping, node types, depth config) (Issue #38) ✅ COMPLETED
- **Deliverables:**
  - Persistence interfaces (language-idiomatic) ✅ **COMPLETED**
  - Tree model APIs and bit-path utilities
- **Why next**: Core operations and tests depend on these abstractions

#### 3. Core Operations & Errors

- Implement Get / Update / Delete and deterministic batch updates (return nodes-to-persist) (Issue #40) 
- Define a consistent error model for verification, depth mismatch, and adapter failures (Issue #44)
- **Deliverables:**
  - Deterministic batch semantics (documented)
  - Typed error classes/codes
- **Why now**: Operations require model, hashing, and persistence interfaces

#### 4. Proofs & Verification

- Implement inclusion and non-inclusion proofs, optional compression, and verification routines (Issue #41)
- **Deliverables:**
  - Proof generation APIs (inclusion/non-inclusion)
  - Proof compression (omit canonical zeros) + bitmask
  - Verification utility: verify(root, key, proof) → valid/invalid with error reasons

#### 5. Testing, Reference Adapter, CI

- Add an in-memory reference adapter and a test suite: determinism, proof correctness, property tests
- Integrate tests into CI, include test vectors and property tests

#### 6. Documentation & Constraints Enforcement

- Add CONTRIBUTING.md and CI/lint checks to enforce "no DB / no filesystem / no blockchain" rules (Issue #46)
- **Deliverables:**
  - CONTRIBUTING guidance
  - CI check(s) for banned imports/usages

### Quick Timeline (Example)

- Foundation (Phase 1): 1–2 sprints
- Interfaces & Model (Phase 2): 1–2 sprints
- Core Ops & Errors (Phase 3): 2–3 sprints
- Proofs & Verification (Phase 4): 2–3 sprints
- Tests & CI (Phase 5): ongoing, start concurrently with Phase 2

Adjust per team size and desired depth of test coverage.

### Using This Roadmap

- Start by assigning or working on the tracked issues in the phase order above
- Create explicit issues for "Core Operations" and "Testing & Reference Adapter" if you want them tracked in the repository
- Use the in-memory adapter (from testing phase) to validate adapter implementations
- Keep the metadata/hash primitives stable — changes here are breaking

## Best Practices

1. **Always Store Metadata**: Save metadata alongside tree data for reproducibility
2. **Version Checks**: Check version compatibility before loading serialized data
3. **Verify Zero-Hashes**: Use `ZeroHashTable.Verify()` to detect corruption
4. **Document Changes**: Record metadata hash in logs when creating trees
5. **Test Determinism**: Include determinism tests in your test suite
6. **Avoid Mixing**: Don't mix nodes from trees with different metadata

## API Documentation

Full API documentation is available in the XML documentation comments in the source code:

- `SmtMetadata.cs` - Metadata structure and factory methods
- `ZeroHashTable.cs` - Zero-hash table generation and verification
- `SmtNodeBlob.cs` - Node blob structure for persistence
- `ISmtNodeReader.cs` - Node reading interface
- `ISmtNodeWriter.cs` - Node writing interface
- `ISmtSnapshotManager.cs` - Snapshot management interface
- `ISmtMetadataStore.cs` - Metadata storage interface
- `InMemorySmtStorage.cs` - Reference in-memory implementation

### Related Documentation

- [SMT Persistence Source](./Persistence/) - Storage adapter implementation files
- [Test Examples](../../../tests/MerkleTree.Tests/Smt/) - Complete test patterns and examples
- [Hash Functions](../Hashing/IHashFunction.cs) - Hash function interface
