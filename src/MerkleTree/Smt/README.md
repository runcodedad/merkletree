# Sparse Merkle Tree (SMT)

A storage-agnostic, deterministic Sparse Merkle Tree implementation supporting pluggable persistence adapters and proof generation. This document covers metadata structure, usage, implementation roadmap, and best practices.

## Table of Contents

- [Overview](#overview)
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
- Implement the SMT tree model (key → bit-path mapping, node types, depth config) (Issue #38)
- **Deliverables:**
  - Persistence interfaces (language-idiomatic) ✅ **COMPLETED**
  - Tree model APIs and bit-path utilities
- **Why next**: Core operations and tests depend on these abstractions

#### 3. Core Operations & Errors

- Implement Get / Update / Delete and deterministic batch updates (return nodes-to-persist)
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
