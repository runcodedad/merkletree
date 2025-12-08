# Sparse Merkle Tree (SMT) Persistence Abstraction

## Overview

The SMT persistence abstraction provides a storage-agnostic interface layer that enables Sparse Merkle Trees to work with any storage backend: in-memory, on-disk databases, remote servers, cloud storage, or blockchain systems. This design ensures SMT core operations remain independent of persistence implementation details.

## Design Principles

### Storage Agnostic
- Core SMT operations use only abstract interfaces
- No direct file system, database, or network code in core
- Adapters handle all storage-specific logic

### Deterministic & Reproducible
- Same inputs always produce same outputs
- Cross-platform compatibility guaranteed
- Serialization format version tracked

### Pluggable Architecture
- Multiple storage backends can coexist
- Easy to implement custom adapters
- Reference implementation provided for testing

### Thread-Safe & Concurrent
- All interfaces support concurrent operations
- Read operations never block other reads
- Write operations use appropriate locking/transactions

### Idempotent Operations
- Duplicate writes cause no errors
- Snapshot operations can be safely retried
- Consistent behavior across failures

## Core Interfaces

### 1. SmtNodeBlob

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

### 2. ISmtNodeReader

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
- **Thread-safe**: Multiple concurrent reads supported
- **Consistent**: Always returns latest committed data or null
- **Non-blocking**: Read operations don't block other reads

**Usage:**
```csharp
// Read a node by hash
var node = await reader.ReadNodeByHashAsync(nodeHash);
if (node != null)
{
    // Process node data
    ProcessNode(node.SerializedNode);
}

// Check if node exists (lightweight)
bool exists = await reader.NodeExistsAsync(nodeHash);

// Read by path (optional - not all adapters support this)
var nodeAtPath = await reader.ReadNodeByPathAsync(bitPath);
```

### 3. ISmtNodeWriter

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
- **Atomic**: Batch writes are all-or-nothing where possible
- **Idempotent**: Writing same node multiple times causes no error
- **Thread-safe**: Multiple threads can write concurrently

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

### 4. ISmtSnapshotManager

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
- **Immutable**: Snapshots never change after creation
- **Thread-safe**: Snapshot operations don't interfere with tree operations
- **Idempotent**: Deleting non-existent snapshot succeeds without error

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

### 5. ISmtMetadataStore

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
- **Atomic**: Metadata updates are all-or-nothing
- **Idempotent**: Storing identical metadata multiple times is safe
- **Thread-safe**: Concurrent metadata operations are supported

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

## Reference Implementation: InMemorySmtStorage

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
- **Thread-safe**: Uses lock-based synchronization
- **Fast**: O(1) lookups using hash-based dictionaries
- **Simple**: No external dependencies
- **Limited**: Not suitable for large datasets or durability requirements

## Implementing a Custom Adapter

To implement a custom storage adapter:

1. **Implement the interfaces** you need (all or subset)
2. **Document concurrency guarantees** in your implementation
3. **Handle errors appropriately** (throw InvalidOperationException for adapter errors)
4. **Ensure idempotency** for write operations
5. **Test thoroughly** using the test patterns from InMemorySmtStorageTests

### Example: File-Based Adapter Skeleton

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

## Error Handling

All persistence interfaces follow consistent error handling:

### Expected Errors
- **ArgumentNullException**: Null arguments
- **ArgumentException**: Invalid arguments (empty hash, empty name, etc.)
- **InvalidOperationException**: Adapter-level errors (I/O failure, network issue, corrupted data)
- **OperationCanceledException**: Operation canceled via CancellationToken

### Not Found vs. Error
- **Missing nodes**: Return null (expected for sparse trees)
- **I/O errors**: Throw InvalidOperationException
- **Corrupt data**: Throw InvalidOperationException

### Example Error Handling

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

## Testing Your Adapter

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

## Performance Considerations

### Read Performance
- **Hash-based lookups**: Should be O(1) or O(log n)
- **Path-based lookups**: Optional - can be slower or unsupported
- **Existence checks**: Should be faster than full reads

### Write Performance
- **Batch writes**: Should be more efficient than individual writes
- **Buffering**: Consider buffering writes in memory before flushing
- **Transactions**: Use database transactions for atomicity

### Storage Optimization
- **Deduplication**: Nodes with same hash share storage
- **Compression**: Consider compressing node data
- **Indexing**: Index by both hash and path for fast lookups
- **Caching**: Cache frequently accessed nodes

## Integration with SMT Core

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

## Security Considerations

### Input Validation
- Always validate hash lengths and formats
- Sanitize snapshot names to prevent path traversal
- Limit metadata size to prevent memory attacks

### Access Control
- Implement authentication/authorization in your adapter
- Consider read-only vs. read-write interfaces
- Audit sensitive operations (snapshots, deletions)

### Data Integrity
- Verify hash matches node content on read
- Use checksums for corruption detection
- Implement backup and recovery procedures

## Future Extensions

Potential future enhancements to the persistence abstraction:

- **Garbage collection interface**: Clean up unreferenced nodes
- **Compression interface**: Optional compression of node data
- **Replication interface**: Multi-node storage synchronization
- **Migration interface**: Upgrade between storage versions
- **Metrics interface**: Monitor storage performance and health

## References

- [SMT Roadmap](./SMT_Roadmap.md) - Overall SMT implementation plan
- [SMT Metadata](./SMT_METADATA.md) - Metadata structure and versioning
- Source: `src/MerkleTree/Smt/Persistence/`
- Tests: `tests/MerkleTree.Tests/Smt/Persistence/`
